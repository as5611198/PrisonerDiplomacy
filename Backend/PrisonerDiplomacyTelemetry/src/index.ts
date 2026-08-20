import {
  DEFAULT_MAX_BODY_BYTES,
  PayloadValidationError,
  TelemetryPayload,
  parsePayloadJson,
  parsePositiveLimit
} from "./validation";
import { runDailyTriage, runRepairRetries } from "./workflows";

const ALLOWED_STATUSES = new Set([
  "pending",
  "analyzing",
  "fix_candidate",
  "needs_repro",
  "resolved",
  "ignored"
]);
const ALLOWED_TRIAGE_CLASSIFICATIONS = new Set([
  "likely_internal",
  "likely_external_conflict",
  "duplicate",
  "insufficient_evidence"
]);
const ALLOWED_SEVERITIES = new Set(["low", "medium", "high", "critical"]);

interface PendingIssueRow {
  hash: string;
  error_message: string;
  exception_type: string;
  operation: string;
  source: string;
  trust_level: string;
  mod_version: string;
  game_version: string;
  occurrence_count: number;
  first_seen: string;
  last_seen: string;
  r2_log_key: string;
  status: string;
  triage_classification: string | null;
  triage_confidence: number | null;
  triage_severity: string | null;
  triage_evidence_json: string | null;
  triage_provider: string | null;
  triaged_at: string | null;
  triage_last_error: string | null;
  triage_last_attempt_at: string | null;
  triage_retry_after: string | null;
  repair_status: string;
  repair_attempt_count: number;
  repair_last_attempt_at: string | null;
  repair_next_attempt_at: string | null;
  repair_last_error: string | null;
  repair_candidate_json: string | null;
  repair_candidate_r2_key: string | null;
  created_at: string;
  updated_at: string;
}

interface EventRow {
  event_id: string;
  error_hash: string;
  captured_at_utc: string;
  received_at: string;
  r2_log_key: string;
  payload_sha256: string;
}

interface StatusPatch {
  status?: string;
  classification?: string;
  confidence?: number;
  severity?: string;
  evidence?: string[];
  provider?: string;
}

class HttpError extends Error {
  public readonly status: number;
  public readonly code: string;

  public constructor(status: number, code: string) {
    super(code);
    this.status = status;
    this.code = code;
  }
}

function jsonResponse(body: unknown, status = 200, extraHeaders: Record<string, string> = {}): Response {
  const headers = new Headers({
    "Content-Type": "application/json; charset=utf-8",
    "Cache-Control": "no-store",
    "X-Content-Type-Options": "nosniff",
    ...extraHeaders
  });
  return new Response(JSON.stringify(body), { status, headers });
}

function methodNotAllowed(allow: string): Response {
  return jsonResponse({ error: "method_not_allowed" }, 405, { Allow: allow });
}

function logError(message: string, details: Record<string, unknown> = {}): void {
  console.error(JSON.stringify({ message, ...details }));
}

async function readBoundedBody(request: Request, maximumBytes: number): Promise<string> {
  const contentLength = request.headers.get("Content-Length");
  if (contentLength !== null && /^\d+$/.test(contentLength) && Number(contentLength) > maximumBytes) {
    throw new HttpError(413, "payload_too_large");
  }
  if (!request.body) {
    return "";
  }

  const reader = request.body.getReader();
  const chunks: Uint8Array[] = [];
  let total = 0;
  try {
    while (true) {
      const result = await reader.read();
      if (result.done) {
        break;
      }
      const chunk = result.value;
      total += chunk.byteLength;
      if (total > maximumBytes) {
        await reader.cancel("payload_too_large");
        throw new HttpError(413, "payload_too_large");
      }
      chunks.push(chunk);
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

async function sha256Hex(value: string): Promise<string> {
  const digest = await crypto.subtle.digest("SHA-256", new TextEncoder().encode(value));
  return Array.from(new Uint8Array(digest), byte => byte.toString(16).padStart(2, "0")).join("");
}

async function verifyAdmin(request: Request, env: Env): Promise<boolean> {
  const expected = env.ADMIN_TOKEN?.trim();
  if (!expected) {
    return false;
  }
  const authorization = request.headers.get("Authorization") ?? "";
  const match = /^Bearer\s+(.+)$/i.exec(authorization);
  if (!match) {
    return false;
  }

  const [providedHash, expectedHash] = await Promise.all([
    crypto.subtle.digest("SHA-256", new TextEncoder().encode(match[1])),
    crypto.subtle.digest("SHA-256", new TextEncoder().encode(expected))
  ]);
  return crypto.subtle.timingSafeEqual(providedHash, expectedHash);
}

async function enforceRateLimit(request: Request, env: Env): Promise<Response | null> {
  const maximum = parsePositiveLimit(env.MAX_REPORTS_PER_IP_PER_MINUTE, 30, 300);
  const bucketStart = Math.floor(Date.now() / 60_000) * 60_000;
  const expiresAt = bucketStart + 120_000;
  const address = request.headers.get("CF-Connecting-IP")
    ?? request.headers.get("X-Forwarded-For")?.split(",", 1)[0]?.trim()
    ?? "unknown";
  const salt = env.RATE_LIMIT_SALT?.trim() || "local-development-only";
  const ipHash = await sha256Hex(`${salt}:${address.slice(0, 128)}`);

  await env.DB.prepare(
    `INSERT INTO ingest_rate_limits (bucket_start, ip_hash, request_count, expires_at)
     VALUES (?, ?, 1, ?)
     ON CONFLICT(bucket_start, ip_hash) DO UPDATE SET
       request_count = ingest_rate_limits.request_count + 1,
       expires_at = excluded.expires_at`
  ).bind(bucketStart, ipHash, expiresAt).run();

  const row = await env.DB.prepare(
    "SELECT request_count FROM ingest_rate_limits WHERE bucket_start = ? AND ip_hash = ?"
  ).bind(bucketStart, ipHash).first<{ request_count: number }>();
  if (!row || row.request_count <= maximum) {
    return null;
  }

  const retryAfter = Math.max(1, Math.ceil((bucketStart + 60_000 - Date.now()) / 1000));
  return jsonResponse({ error: "rate_limited" }, 429, { "Retry-After": String(retryAfter) });
}

function buildLogKey(payload: TelemetryPayload): string {
  const compactTimestamp = payload.captured_at_utc.replace(/[^0-9]/g, "").slice(0, 14) || "unknown";
  return `logs/${payload.error_hash}/${compactTimestamp}-${payload.event_id}.json`;
}

async function ingestReport(payload: TelemetryPayload, env: Env): Promise<Response> {
  const receivedAt = new Date().toISOString();
  const r2LogKey = buildLogKey(payload);
  const payloadJson = JSON.stringify(payload);
  const payloadSha256 = await sha256Hex(payloadJson);

  const existingEvent = await env.DB.prepare(
    "SELECT event_id, error_hash, captured_at_utc, received_at, r2_log_key, payload_sha256 FROM error_report_events WHERE event_id = ?"
  ).bind(payload.event_id).first<EventRow>();
  if (existingEvent && (existingEvent.error_hash !== payload.error_hash
      || existingEvent.payload_sha256 !== payloadSha256)) {
    throw new HttpError(409, "event_id_conflict");
  }

  await env.LOGS.put(r2LogKey, payloadJson, {
    httpMetadata: { contentType: "application/json", cacheControl: "no-store" },
    customMetadata: {
      error_hash: payload.error_hash,
      event_id: payload.event_id,
      schema_version: "1"
    }
  });

  const eventInsert = await env.DB.prepare(
    `INSERT OR IGNORE INTO error_report_events
       (event_id, error_hash, captured_at_utc, received_at, r2_log_key, payload_sha256)
     VALUES (?, ?, ?, ?, ?, ?)`
  ).bind(
    payload.event_id,
    payload.error_hash,
    payload.captured_at_utc,
    receivedAt,
    r2LogKey,
    payloadSha256
  ).run();

  const inserted = Number(eventInsert.meta?.changes ?? 0) === 1;
  if (!inserted) {
    const eventAfterInsert = await env.DB.prepare(
      "SELECT event_id, error_hash, captured_at_utc, received_at, r2_log_key, payload_sha256 FROM error_report_events WHERE event_id = ?"
    ).bind(payload.event_id).first<EventRow>();
    if (!eventAfterInsert || eventAfterInsert.error_hash !== payload.error_hash
        || eventAfterInsert.payload_sha256 !== payloadSha256) {
      throw new HttpError(409, "event_id_conflict");
    }
  }

  const aggregateNow = new Date().toISOString();
  await env.DB.prepare(
    `INSERT INTO error_reports
       (hash, error_message, exception_type, operation, source, trust_level,
        mod_version, game_version, occurrence_count, first_seen, last_seen,
        r2_log_key, status, created_at, updated_at)
     VALUES (?, ?, ?, ?, ?, ?, ?, ?,
        (SELECT COUNT(*) FROM error_report_events WHERE error_hash = ?),
        ?, ?, ?, 'pending', ?, ?)
     ON CONFLICT(hash) DO UPDATE SET
       error_message = excluded.error_message,
       exception_type = excluded.exception_type,
       operation = excluded.operation,
       source = excluded.source,
       trust_level = excluded.trust_level,
       mod_version = excluded.mod_version,
       game_version = excluded.game_version,
       occurrence_count = MAX(error_reports.occurrence_count, excluded.occurrence_count),
       first_seen = MIN(error_reports.first_seen, excluded.first_seen),
       last_seen = MAX(error_reports.last_seen, excluded.last_seen),
       r2_log_key = excluded.r2_log_key,
       status = CASE WHEN ? = 0 THEN error_reports.status
                     WHEN error_reports.status = 'ignored' THEN 'ignored'
                     ELSE 'pending' END,
       updated_at = excluded.updated_at`
  ).bind(
    payload.error_hash,
    payload.message,
    payload.exception_type,
    payload.operation,
    payload.source,
    payload.trust_level,
    payload.mod_version,
    payload.game_version,
    payload.error_hash,
    payload.captured_at_utc,
    payload.captured_at_utc,
    r2LogKey,
    aggregateNow,
    aggregateNow,
    inserted ? 1 : 0
  ).run();

  const aggregate = await env.DB.prepare(
    "SELECT occurrence_count, status FROM error_reports WHERE hash = ?"
  ).bind(payload.error_hash).first<{ occurrence_count: number; status: string }>();
  if (!aggregate) {
    throw new Error("aggregate_missing_after_insert");
  }

  return jsonResponse({
    accepted: true,
    duplicate: !inserted,
    event_id: payload.event_id,
    error_hash: payload.error_hash,
    occurrence_count: aggregate.occurrence_count,
    status: aggregate.status
  }, inserted ? 201 : 200);
}

async function handleReport(request: Request, env: Env): Promise<Response> {
  const rateLimitResponse = await enforceRateLimit(request, env);
  if (rateLimitResponse) {
    return rateLimitResponse;
  }
  const maximum = parsePositiveLimit(env.MAX_BODY_BYTES, DEFAULT_MAX_BODY_BYTES, DEFAULT_MAX_BODY_BYTES);
  const body = await readBoundedBody(request, maximum);
  if (!body) {
    throw new PayloadValidationError("invalid_json", "empty_body");
  }
  const payload = parsePayloadJson(body);
  return ingestReport(payload, env);
}

function isErrorHash(value: string | undefined): value is string {
  return value !== undefined && /^[0-9a-f]{64}$/i.test(value);
}

async function handlePendingTop(request: Request, env: Env): Promise<Response> {
  if (!(await verifyAdmin(request, env))) {
    return jsonResponse({ error: "unauthorized" }, env.ADMIN_TOKEN ? 401 : 503);
  }
  const url = new URL(request.url);
  const limit = parsePositiveLimit(url.searchParams.get("limit") ?? undefined, 3, 20);
  const result = await env.DB.prepare(
    `SELECT hash, error_message, exception_type, operation, source, trust_level,
            mod_version, game_version, occurrence_count, first_seen, last_seen,
            r2_log_key, status, triage_classification, triage_confidence,
            triage_severity, triage_evidence_json, triage_provider, triaged_at,
            triage_last_error, triage_last_attempt_at, triage_retry_after,
            repair_status, repair_attempt_count, repair_last_attempt_at,
            repair_next_attempt_at, repair_last_error, repair_candidate_json,
            repair_candidate_r2_key,
            created_at, updated_at
       FROM error_reports
      WHERE status = 'pending'
      ORDER BY occurrence_count DESC, last_seen DESC
      LIMIT ?`
  ).bind(limit).all<PendingIssueRow>();
  return jsonResponse({ generated_at: new Date().toISOString(), issues: result.results });
}

async function handleIssue(request: Request, env: Env, hash: string): Promise<Response> {
  if (!(await verifyAdmin(request, env))) {
    return jsonResponse({ error: "unauthorized" }, env.ADMIN_TOKEN ? 401 : 503);
  }
  if (!isErrorHash(hash)) {
    return jsonResponse({ error: "invalid_hash" }, 400);
  }
  const issue = await env.DB.prepare("SELECT * FROM error_reports WHERE hash = ?")
    .bind(hash.toLowerCase()).first<PendingIssueRow>();
  if (!issue) {
    return jsonResponse({ error: "not_found" }, 404);
  }
  const events = await env.DB.prepare(
    `SELECT event_id, error_hash, captured_at_utc, received_at, r2_log_key, payload_sha256
       FROM error_report_events
      WHERE error_hash = ?
      ORDER BY captured_at_utc DESC
      LIMIT 3`
  ).bind(hash.toLowerCase()).all<EventRow>();
  return jsonResponse({ issue, events: events.results });
}

async function handleEvent(request: Request, env: Env, eventId: string): Promise<Response> {
  if (!(await verifyAdmin(request, env))) {
    return jsonResponse({ error: "unauthorized" }, env.ADMIN_TOKEN ? 401 : 503);
  }
  if (!/^[0-9a-f]{32}$/i.test(eventId)) {
    return jsonResponse({ error: "invalid_event_id" }, 400);
  }
  const event = await env.DB.prepare(
    "SELECT event_id, error_hash, captured_at_utc, received_at, r2_log_key, payload_sha256 FROM error_report_events WHERE event_id = ?"
  ).bind(eventId.toLowerCase()).first<EventRow>();
  if (!event) {
    return jsonResponse({ error: "not_found" }, 404);
  }
  const object = await env.LOGS.get(event.r2_log_key);
  if (!object) {
    return jsonResponse({ error: "log_not_found", event }, 503);
  }
  return new Response(object.body, {
    status: 200,
    headers: {
      "Content-Type": "application/json; charset=utf-8",
      "Cache-Control": "no-store",
      "X-Content-Type-Options": "nosniff"
    }
  });
}

async function handleStatusPatch(request: Request, env: Env, hash: string): Promise<Response> {
  if (!(await verifyAdmin(request, env))) {
    return jsonResponse({ error: "unauthorized" }, env.ADMIN_TOKEN ? 401 : 503);
  }
  if (!isErrorHash(hash)) {
    return jsonResponse({ error: "invalid_hash" }, 400);
  }
  const body = await readBoundedBody(request, 16_384);
  let patch: StatusPatch;
  try {
    patch = JSON.parse(body) as StatusPatch;
  } catch {
    return jsonResponse({ error: "invalid_json" }, 400);
  }
  if (!patch || typeof patch !== "object" || Array.isArray(patch)) {
    return jsonResponse({ error: "invalid_patch" }, 400);
  }
  if (patch.status !== undefined && !ALLOWED_STATUSES.has(patch.status)) {
    return jsonResponse({ error: "invalid_status" }, 400);
  }
  if (patch.classification !== undefined && !ALLOWED_TRIAGE_CLASSIFICATIONS.has(patch.classification)) {
    return jsonResponse({ error: "invalid_classification" }, 400);
  }
  if (patch.confidence !== undefined && (!Number.isFinite(patch.confidence) || patch.confidence < 0 || patch.confidence > 1)) {
    return jsonResponse({ error: "invalid_confidence" }, 400);
  }
  if (patch.severity !== undefined && !ALLOWED_SEVERITIES.has(patch.severity)) {
    return jsonResponse({ error: "invalid_severity" }, 400);
  }
  if (patch.evidence !== undefined && (!Array.isArray(patch.evidence) || patch.evidence.length > 8
    || patch.evidence.some(item => typeof item !== "string" || item.length > 512))) {
    return jsonResponse({ error: "invalid_evidence" }, 400);
  }
  if (patch.provider !== undefined && (typeof patch.provider !== "string" || patch.provider.length > 128)) {
    return jsonResponse({ error: "invalid_provider" }, 400);
  }
  if (Object.keys(patch).length === 0) {
    return jsonResponse({ error: "empty_patch" }, 400);
  }

  const existing = await env.DB.prepare("SELECT hash FROM error_reports WHERE hash = ?")
    .bind(hash.toLowerCase()).first<{ hash: string }>();
  if (!existing) {
    return jsonResponse({ error: "not_found" }, 404);
  }

  const now = new Date().toISOString();
  await env.DB.prepare(
    `UPDATE error_reports
        SET status = COALESCE(?, status),
            triage_classification = COALESCE(?, triage_classification),
            triage_confidence = COALESCE(?, triage_confidence),
            triage_severity = COALESCE(?, triage_severity),
            triage_evidence_json = COALESCE(?, triage_evidence_json),
            triage_provider = COALESCE(?, triage_provider),
            triaged_at = CASE WHEN ? IS NULL THEN triaged_at ELSE ? END,
            updated_at = ?
      WHERE hash = ?`
  ).bind(
    patch.status ?? null,
    patch.classification ?? null,
    patch.confidence ?? null,
    patch.severity ?? null,
    patch.evidence ? JSON.stringify(patch.evidence) : null,
    patch.provider ?? null,
    patch.classification ?? null,
    patch.classification ? now : null,
    now,
    hash.toLowerCase()
  ).run();
  const updated = await env.DB.prepare("SELECT * FROM error_reports WHERE hash = ?")
    .bind(hash.toLowerCase()).first<PendingIssueRow>();
  return jsonResponse({ issue: updated });
}

async function runMaintenance(env: Env): Promise<void> {
  const now = Date.now();
  await env.DB.prepare("DELETE FROM ingest_rate_limits WHERE expires_at < ?").bind(now).run();
  console.log(JSON.stringify({ message: "maintenance_complete", environment: env.ENVIRONMENT ?? "unknown" }));
}

async function handleAiJob(
  request: Request,
  env: Env,
  ctx: ExecutionContext,
  job: "triage" | "repair"
): Promise<Response> {
  if (!(await verifyAdmin(request, env))) {
    return jsonResponse({ error: "unauthorized" }, env.ADMIN_TOKEN ? 401 : 503);
  }
  const requestedAt = new Date().toISOString();
  const task = job === "triage" ? runDailyTriage(env) : runRepairRetries(env);
  ctx.waitUntil(task.catch(error => {
    logError("manual_ai_job_failed", {
      job,
      error: error instanceof Error ? error.message : String(error)
    });
  }));
  return jsonResponse({ accepted: true, job, requested_at: requestedAt }, 202);
}

async function route(request: Request, env: Env, ctx: ExecutionContext): Promise<Response> {
  const url = new URL(request.url);
  if (url.pathname === "/healthz") {
    if (request.method !== "GET") {
      return methodNotAllowed("GET");
    }
    return jsonResponse({ ok: true, service: "prisoner-diplomacy-telemetry", environment: env.ENVIRONMENT ?? "unknown" });
  }
  if (url.pathname === "/api/report-error") {
    if (request.method !== "POST") {
      return methodNotAllowed("POST");
    }
    return handleReport(request, env);
  }
  if (url.pathname === "/api/admin/pending-top") {
    if (request.method !== "GET") {
      return methodNotAllowed("GET");
    }
    return handlePendingTop(request, env);
  }

  const aiJobMatch = /^\/api\/admin\/jobs\/(triage|repair)$/.exec(url.pathname);
  if (aiJobMatch) {
    if (request.method !== "POST") {
      return methodNotAllowed("POST");
    }
    return handleAiJob(request, env, ctx, aiJobMatch[1] as "triage" | "repair");
  }

  const issueMatch = /^\/api\/admin\/issues\/([^/]+)$/.exec(url.pathname);
  if (issueMatch) {
    if (request.method === "GET") {
      return handleIssue(request, env, issueMatch[1]);
    }
    if (request.method === "PATCH") {
      return handleStatusPatch(request, env, issueMatch[1]);
    }
    return methodNotAllowed("GET, PATCH");
  }

  const eventMatch = /^\/api\/admin\/events\/([^/]+)$/.exec(url.pathname);
  if (eventMatch) {
    if (request.method !== "GET") {
      return methodNotAllowed("GET");
    }
    return handleEvent(request, env, eventMatch[1]);
  }

  return jsonResponse({ error: "not_found" }, 404);
}

export default {
  async fetch(request: Request, env: Env, ctx: ExecutionContext): Promise<Response> {
    try {
      return await route(request, env, ctx);
    } catch (error) {
      if (error instanceof HttpError) {
        return jsonResponse({ error: error.code }, error.status);
      }
      if (error instanceof PayloadValidationError) {
        return jsonResponse({ error: error.code }, error.code === "invalid_json" ? 400 : 422);
      }
      logError("request_failed", {
        path: new URL(request.url).pathname,
        error: error instanceof Error ? error.message : String(error)
      });
      return jsonResponse({ error: "internal_error" }, 500);
    }
  },

  async scheduled(controller: ScheduledController, env: Env, ctx: ExecutionContext): Promise<void> {
    const jobs: Promise<void>[] = [runMaintenance(env)];
    if (controller.cron === "0 3 * * *") {
      jobs.push(runDailyTriage(env));
    }
    if (controller.cron === "*/30 * * * *") {
      jobs.push(runRepairRetries(env));
    }
    ctx.waitUntil(Promise.all(jobs).then(() => undefined));
  }
} satisfies ExportedHandler<Env>;
