import {
  AiIssueContext,
  AiProviderError,
  AiSample,
  RepairCandidate,
  TriageDecision,
  callGeminiTriage,
  callRepairAi,
  withImmediateRetries
} from "./ai";
import { parsePayloadJson, sanitizeText } from "./validation";
import { loadRepairSourceContext } from "./repair-source-context";
import type { RepairSourceContext } from "./repair-source-context";

interface QueueIssue extends AiIssueContext {
  r2_log_key: string;
  repair_status: string;
  repair_attempt_count: number;
  repair_next_attempt_at: string | null;
}

interface EventRow {
  event_id: string;
  error_hash: string;
  captured_at_utc: string;
  received_at: string;
  r2_log_key: string;
  payload_sha256: string;
}

interface AiAttemptRow {
  id: number;
}

function isEnabled(value: string | undefined): boolean {
  return value?.trim().toLowerCase() === "true";
}

function parseLimit(value: string | undefined, fallback: number, maximum: number): number {
  const parsed = Number.parseInt(value ?? "", 10);
  return Number.isFinite(parsed) && parsed > 0 ? Math.min(parsed, maximum) : fallback;
}

function parseConfidence(value: string | undefined, fallback: number): number {
  const parsed = Number.parseFloat(value ?? "");
  return Number.isFinite(parsed) && parsed >= 0 && parsed <= 1 ? parsed : fallback;
}

function isoAfterMinutes(minutes: number): string {
  return new Date(Date.now() + Math.max(1, minutes) * 60_000).toISOString();
}

function safeErrorCode(error: unknown): string {
  if (error instanceof AiProviderError) {
    return error.code.slice(0, 128);
  }
  return "unknown_provider_error";
}

function safeErrorDetail(error: unknown): string {
  const message = error instanceof Error ? error.message : String(error);
  return sanitizeText(message, 512);
}

async function sleep(milliseconds: number): Promise<void> {
  await new Promise<void>(resolve => setTimeout(resolve, milliseconds));
}

async function readR2Text(object: R2ObjectBody, maximumBytes: number): Promise<string> {
  const reader = object.body.getReader();
  const chunks: Uint8Array[] = [];
  let total = 0;
  try {
    while (true) {
      const result = await reader.read();
      if (result.done) {
        break;
      }
      total += result.value.byteLength;
      if (total > maximumBytes) {
        await reader.cancel("sample_too_large");
        return "";
      }
      chunks.push(result.value);
    }
  } finally {
    reader.releaseLock();
  }
  const bytes = new Uint8Array(total);
  let offset = 0;
  for (const chunk of chunks) {
    bytes.set(chunk, offset);
    offset += chunk.byteLength;
  }
  return new TextDecoder().decode(bytes);
}

async function loadSamples(env: Env, errorHash: string): Promise<AiSample[]> {
  const events = await env.DB.prepare(
    `SELECT event_id, error_hash, captured_at_utc, received_at, r2_log_key, payload_sha256
       FROM error_report_events
      WHERE error_hash = ?
      ORDER BY captured_at_utc DESC
      LIMIT 3`
  ).bind(errorHash).all<EventRow>();
  const samples: AiSample[] = [];
  for (const event of events.results) {
    const object = await env.LOGS.get(event.r2_log_key);
    if (!object) {
      continue;
    }
    const text = await readR2Text(object, 262_144);
    if (!text) {
      continue;
    }
    try {
      const payload = parsePayloadJson(text);
      samples.push({ event_id: event.event_id, captured_at_utc: event.captured_at_utc, payload });
    } catch {
      // A malformed stored payload is not sent to an AI provider.
    }
  }
  return samples;
}

async function acquireLock(env: Env, jobKey: string, owner: string, leaseMinutes: number): Promise<boolean> {
  const now = new Date().toISOString();
  const lockedUntil = isoAfterMinutes(leaseMinutes);
  await env.DB.prepare(
    `INSERT OR IGNORE INTO ai_job_locks (job_key, owner, locked_until) VALUES (?, ?, ?)`
  ).bind(jobKey, owner, lockedUntil).run();
  const result = await env.DB.prepare(
    `UPDATE ai_job_locks
        SET owner = ?, locked_until = ?
      WHERE job_key = ? AND (locked_until < ? OR owner = ?)`
  ).bind(owner, lockedUntil, jobKey, now, owner).run();
  return Number(result.meta?.changes ?? 0) === 1;
}

async function releaseLock(env: Env, jobKey: string, owner: string): Promise<void> {
  await env.DB.prepare("DELETE FROM ai_job_locks WHERE job_key = ? AND owner = ?").bind(jobKey, owner).run();
}

async function claimDailyBudget(env: Env, kind: "triage" | "repair", maximum: number): Promise<boolean> {
  const usageDay = new Date().toISOString().slice(0, 10);
  const now = new Date().toISOString();
  await env.DB.prepare(
    `INSERT OR IGNORE INTO ai_daily_usage (usage_day, triage_count, repair_attempt_count, updated_at)
     VALUES (?, 0, 0, ?)`
  ).bind(usageDay, now).run();
  const column = kind === "triage" ? "triage_count" : "repair_attempt_count";
  const result = await env.DB.prepare(
    `UPDATE ai_daily_usage
        SET ${column} = ${column} + 1, updated_at = ?
      WHERE usage_day = ? AND ${column} < ?`
  ).bind(now, usageDay, maximum).run();
  return Number(result.meta?.changes ?? 0) === 1;
}

async function beginAttempt(env: Env, hash: string, stage: "triage" | "repair", provider: string): Promise<AiAttemptRow> {
  const latest = await env.DB.prepare(
    "SELECT COALESCE(MAX(attempt_number), 0) AS attempt_number FROM ai_attempts WHERE error_hash = ? AND stage = ?"
  ).bind(hash, stage).first<{ attempt_number: number }>();
  const attemptNumber = Number(latest?.attempt_number ?? 0) + 1;
  const startedAt = new Date().toISOString();
  const inserted = await env.DB.prepare(
    `INSERT INTO ai_attempts
       (error_hash, stage, attempt_number, provider, status, started_at)
     VALUES (?, ?, ?, ?, 'started', ?)`
  ).bind(hash, stage, attemptNumber, provider, startedAt).run();
  const id = Number(inserted.meta?.last_row_id ?? 0);
  return { id };
}

async function finishAttempt(
  env: Env,
  attempt: AiAttemptRow,
  success: boolean,
  errorCode: string | null,
  resultJson: string | null,
  nextRetryAt: string | null
): Promise<void> {
  await env.DB.prepare(
    `UPDATE ai_attempts
        SET status = ?, finished_at = ?, next_retry_at = ?, error_code = ?, result_json = ?
      WHERE id = ?`
  ).bind(
    success ? "succeeded" : "failed",
    new Date().toISOString(),
    nextRetryAt,
    errorCode,
    resultJson,
    attempt.id
  ).run();
}

async function updateTriageSuccess(env: Env, issue: QueueIssue, decision: TriageDecision): Promise<void> {
  const minimum = parseConfidence(env.TRIAGE_MIN_CONFIDENCE, 0.8);
  const queueRepair = decision.send_to_repair_ai
    && decision.classification === "likely_internal"
    && decision.confidence >= minimum
    && (decision.severity === "high" || decision.severity === "critical");
  const now = new Date().toISOString();
  await env.DB.prepare(
    `UPDATE error_reports
        SET triage_classification = ?,
            triage_confidence = ?,
            triage_severity = ?,
            triage_evidence_json = ?,
            triage_provider = 'gemini-3.7-flash',
            triaged_at = ?,
            triage_last_error = NULL,
            triage_last_attempt_at = ?,
            triage_retry_after = NULL,
            repair_status = CASE WHEN ? = 1 AND repair_status = 'none' THEN 'queued' ELSE repair_status END,
            updated_at = ?
      WHERE hash = ? AND triage_classification IS NULL`
  ).bind(
    decision.classification,
    decision.confidence,
    decision.severity,
    JSON.stringify(decision.evidence),
    now,
    now,
    queueRepair ? 1 : 0,
    now,
    issue.hash
  ).run();
}

async function updateTriageFailure(env: Env, hash: string, error: unknown): Promise<void> {
  const now = new Date().toISOString();
  const retryAfter = new Date(Date.now() + 24 * 60 * 60_000).toISOString();
  await env.DB.prepare(
    `UPDATE error_reports
        SET triage_last_error = ?, triage_last_attempt_at = ?, triage_retry_after = ?, updated_at = ?
      WHERE hash = ?`
  ).bind(safeErrorCode(error), now, retryAfter, now, hash).run();
}

async function queryTriageQueue(env: Env, limit: number): Promise<QueueIssue[]> {
  const now = new Date().toISOString();
  const result = await env.DB.prepare(
    `SELECT hash, error_message, exception_type, operation, source, trust_level,
            mod_version, game_version, occurrence_count, first_seen, last_seen,
            triage_classification, triage_confidence, triage_severity,
            r2_log_key, repair_status, repair_attempt_count, repair_next_attempt_at
       FROM error_reports
      WHERE status = 'pending'
        AND triage_classification IS NULL
        AND (triage_retry_after IS NULL OR triage_retry_after <= ?)
      ORDER BY occurrence_count DESC, last_seen DESC
      LIMIT ?`
  ).bind(now, limit).all<QueueIssue>();
  return result.results;
}

export async function runDailyTriage(env: Env): Promise<void> {
  if (!isEnabled(env.TRIAGE_ENABLED) || !env.GEMINI_API_KEY) {
    console.log(JSON.stringify({ message: "triage_disabled_or_unconfigured" }));
    return;
  }
  const issues = await queryTriageQueue(env, parseLimit(env.TRIAGE_MAX_ISSUES_PER_DAY, 20, 100));
  for (const issue of issues) {
    const owner = crypto.randomUUID();
    const jobKey = `triage:${issue.hash}`;
    if (!(await acquireLock(env, jobKey, owner, 10))) {
      continue;
    }
    try {
      if (!(await claimDailyBudget(env, "triage", parseLimit(env.TRIAGE_MAX_ISSUES_PER_DAY, 20, 100)))) {
        break;
      }
      const attempt = await beginAttempt(env, issue.hash, "triage", "gemini-3.7-flash");
      try {
        const samples = await loadSamples(env, issue.hash);
        const decision = await withImmediateRetries(
          () => callGeminiTriage(env, issue, samples),
          2,
          async (error, retryAttempt) => {
            console.warn(JSON.stringify({ message: "triage_provider_retry", hash: issue.hash, code: error.code, attempt: retryAttempt }));
            await sleep(Math.min(3_000, retryAttempt * 1_000));
          }
        );
        await updateTriageSuccess(env, issue, decision);
        await finishAttempt(env, attempt, true, null, JSON.stringify(decision), null);
      } catch (error) {
        console.warn(JSON.stringify({
          message: "triage_provider_failed",
          hash: issue.hash,
          code: safeErrorCode(error),
          detail: safeErrorDetail(error)
        }));
        await updateTriageFailure(env, issue.hash, error);
        await finishAttempt(env, attempt, false, safeErrorCode(error), null, isoAfterMinutes(24 * 60));
      }
    } finally {
      await releaseLock(env, jobKey, owner);
    }
  }
}

async function queryRepairQueue(env: Env, limit: number): Promise<QueueIssue[]> {
  const now = new Date().toISOString();
  const result = await env.DB.prepare(
    `SELECT hash, error_message, exception_type, operation, source, trust_level,
            mod_version, game_version, occurrence_count, first_seen, last_seen,
            triage_classification, triage_confidence, triage_severity,
            r2_log_key, repair_status, repair_attempt_count, repair_next_attempt_at
       FROM error_reports
      WHERE repair_status IN ('queued', 'retry_wait')
        AND (repair_next_attempt_at IS NULL OR repair_next_attempt_at <= ?)
        AND triage_classification = 'likely_internal'
        AND triage_confidence >= ?
        AND status NOT IN ('ignored', 'resolved', 'fix_candidate')
      ORDER BY occurrence_count DESC, last_seen DESC
      LIMIT ?`
  ).bind(now, parseConfidence(env.TRIAGE_MIN_CONFIDENCE, 0.8), limit).all<QueueIssue>();
  return result.results;
}

async function markRepairStarted(env: Env, hash: string): Promise<void> {
  const now = new Date().toISOString();
  await env.DB.prepare(
    `UPDATE error_reports
        SET repair_status = 'in_progress',
            repair_attempt_count = repair_attempt_count + 1,
            repair_last_attempt_at = ?,
            repair_last_error = NULL,
            updated_at = ?
      WHERE hash = ?`
  ).bind(now, now, hash).run();
}

async function storeRepairCandidate(
  env: Env,
  issue: QueueIssue,
  candidate: RepairCandidate,
  sourceContext: RepairSourceContext
): Promise<void> {
  const now = new Date().toISOString();
  const key = `repair-candidates/${issue.hash}/${now.replace(/[^0-9]/g, "").slice(0, 14)}-${issue.repair_attempt_count + 1}.json`;
  const persistedCandidate = {
    ...candidate,
    source_ref: sourceContext.ref,
    source_files: sourceContext.files
  };
  const body = JSON.stringify({
    schema_version: 1,
    generated_at: now,
    error_hash: issue.hash,
    model: env.REPAIR_MODEL || "gpt-5.6-sol",
    source_ref: sourceContext.ref,
    source_files: sourceContext.files,
    candidate: persistedCandidate
  });
  await env.LOGS.put(key, body, {
    httpMetadata: { contentType: "application/json", cacheControl: "no-store" },
    customMetadata: { error_hash: issue.hash, stage: "repair_candidate", schema_version: "1" }
  });
  await env.DB.prepare(
    `UPDATE error_reports
        SET repair_status = 'candidate',
            status = 'fix_candidate',
            repair_candidate_json = ?,
            repair_candidate_r2_key = ?,
            repair_next_attempt_at = NULL,
            repair_last_error = NULL,
            updated_at = ?
      WHERE hash = ?`
  ).bind(JSON.stringify(persistedCandidate), key, now, issue.hash).run();
}

async function markRepairRetry(env: Env, hash: string, error: unknown): Promise<string> {
  const now = new Date().toISOString();
  const nextRetryAt = isoAfterMinutes(parseLimit(env.REPAIR_RETRY_MINUTES, 30, 24 * 60));
  await env.DB.prepare(
    `UPDATE error_reports
        SET repair_status = 'retry_wait',
            repair_next_attempt_at = ?,
            repair_last_error = ?,
            updated_at = ?
      WHERE hash = ?`
  ).bind(nextRetryAt, safeErrorCode(error), now, hash).run();
  return nextRetryAt;
}

export async function runRepairRetries(env: Env): Promise<void> {
  if (!isEnabled(env.REPAIR_ENABLED) || !env.REPAIR_AI_ENDPOINT || !env.REPAIR_AI_API_KEY) {
    console.log(JSON.stringify({ message: "repair_disabled_or_unconfigured" }));
    return;
  }
  const issues = await queryRepairQueue(env, parseLimit(env.REPAIR_ATTEMPTS_PER_RUN, 1, 5));
  const dailyMaximum = parseLimit(env.REPAIR_MAX_ATTEMPTS_PER_DAY, 24, 100);
  for (const issue of issues) {
    const owner = crypto.randomUUID();
    const jobKey = `repair:${issue.hash}`;
    if (!(await acquireLock(env, jobKey, owner, 20))) {
      continue;
    }
    try {
      if (!(await claimDailyBudget(env, "repair", dailyMaximum))) {
        break;
      }
      await markRepairStarted(env, issue.hash);
      const attempt = await beginAttempt(env, issue.hash, "repair", "gpt-5.6-sol");
      try {
        const samples = await loadSamples(env, issue.hash);
        const sourceContext = await loadRepairSourceContext(env, issue, samples);
        const candidate = await callRepairAi(env, issue, samples, sourceContext.text);
        await storeRepairCandidate(env, issue, candidate, sourceContext);
        await finishAttempt(env, attempt, true, null, JSON.stringify(candidate), null);
      } catch (error) {
        console.warn(JSON.stringify({
          message: "repair_provider_failed",
          hash: issue.hash,
          code: safeErrorCode(error),
          detail: safeErrorDetail(error)
        }));
        const nextRetryAt = await markRepairRetry(env, issue.hash, error);
        await finishAttempt(env, attempt, false, safeErrorCode(error), null, nextRetryAt);
      }
    } finally {
      await releaseLock(env, jobKey, owner);
    }
  }
}
