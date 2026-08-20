import { parsePositiveLimit } from "./validation";

export const DEFAULT_DETAIL_LOG_RETENTION_DAYS = 30;
export const DEFAULT_AGGREGATE_RETENTION_DAYS = 180;

const DEFAULT_RETENTION_BATCH_SIZE = 100;
const MAXIMUM_RETENTION_BATCH_SIZE = 100;
const DEFAULT_RETENTION_BATCHES_PER_RUN = 10;
const MAXIMUM_RETENTION_BATCHES_PER_RUN = 20;
const MILLISECONDS_PER_DAY = 24 * 60 * 60 * 1_000;

interface RetentionConfig {
  DETAIL_LOG_RETENTION_DAYS?: string;
  AGGREGATE_RETENTION_DAYS?: string;
  RETENTION_BATCH_SIZE?: string;
  RETENTION_MAX_BATCHES_PER_RUN?: string;
}

interface ExpiredEventRow {
  event_id: string;
  r2_log_key: string;
}

interface ExpiredAggregateRow {
  hash: string;
  repair_candidate_r2_key: string | null;
}

export interface RetentionPolicy {
  detailLogDays: number;
  aggregateDays: number;
  batchSize: number;
  maximumBatches: number;
}

export interface MaintenanceSummary {
  detail_cutoff: string;
  aggregate_cutoff: string;
  expired_event_count: number;
  expired_aggregate_count: number;
}

export function resolveRetentionPolicy(config: RetentionConfig): RetentionPolicy {
  const detailLogDays = parsePositiveLimit(
    config.DETAIL_LOG_RETENTION_DAYS,
    DEFAULT_DETAIL_LOG_RETENTION_DAYS,
    365
  );
  const aggregateDays = Math.max(
    detailLogDays,
    parsePositiveLimit(
      config.AGGREGATE_RETENTION_DAYS,
      DEFAULT_AGGREGATE_RETENTION_DAYS,
      3_650
    )
  );
  return {
    detailLogDays,
    aggregateDays,
    batchSize: parsePositiveLimit(
      config.RETENTION_BATCH_SIZE,
      DEFAULT_RETENTION_BATCH_SIZE,
      MAXIMUM_RETENTION_BATCH_SIZE
    ),
    maximumBatches: parsePositiveLimit(
      config.RETENTION_MAX_BATCHES_PER_RUN,
      DEFAULT_RETENTION_BATCHES_PER_RUN,
      MAXIMUM_RETENTION_BATCHES_PER_RUN
    )
  };
}

export function buildRetentionCutoffs(now: Date, policy: RetentionPolicy): {
  detailCutoff: string;
  aggregateCutoff: string;
} {
  return {
    detailCutoff: new Date(now.getTime() - policy.detailLogDays * MILLISECONDS_PER_DAY).toISOString(),
    aggregateCutoff: new Date(now.getTime() - policy.aggregateDays * MILLISECONDS_PER_DAY).toISOString()
  };
}

function numberedPlaceholders(count: number): string {
  return Array.from({ length: count }, (_, index) => `?${index + 1}`).join(", ");
}

function uniqueNonEmpty(values: Array<string | null>): string[] {
  return Array.from(new Set(values.filter((value): value is string => Boolean(value))));
}

async function deleteExpiredEvents(
  env: Env,
  cutoff: string,
  policy: RetentionPolicy
): Promise<number> {
  let deleted = 0;
  for (let batch = 0; batch < policy.maximumBatches; batch += 1) {
    const result = await env.DB.prepare(
      `SELECT event_id, r2_log_key
         FROM error_report_events
        WHERE received_at < ?
        ORDER BY received_at
        LIMIT ?`
    ).bind(cutoff, policy.batchSize).all<ExpiredEventRow>();
    if (result.results.length === 0) {
      break;
    }

    const eventIds = result.results.map(row => row.event_id);
    const logKeys = uniqueNonEmpty(result.results.map(row => row.r2_log_key));
    if (logKeys.length > 0) {
      await env.LOGS.delete(logKeys);
    }

    const eventPlaceholders = numberedPlaceholders(eventIds.length);
    const statements: D1PreparedStatement[] = [];
    if (logKeys.length > 0) {
      statements.push(env.DB.prepare(
        `UPDATE error_reports
            SET r2_log_key = ''
          WHERE r2_log_key IN (${numberedPlaceholders(logKeys.length)})`
      ).bind(...logKeys));
    }
    statements.push(env.DB.prepare(
      `DELETE FROM error_report_events WHERE event_id IN (${eventPlaceholders})`
    ).bind(...eventIds));
    await env.DB.batch(statements);

    deleted += eventIds.length;
    if (eventIds.length < policy.batchSize) {
      break;
    }
  }
  return deleted;
}

async function deleteExpiredAggregates(
  env: Env,
  cutoff: string,
  policy: RetentionPolicy
): Promise<number> {
  let deleted = 0;
  for (let batch = 0; batch < policy.maximumBatches; batch += 1) {
    const result = await env.DB.prepare(
      `SELECT hash, repair_candidate_r2_key
         FROM error_reports
        WHERE COALESCE(last_received_at, updated_at) < ?
        ORDER BY COALESCE(last_received_at, updated_at)
        LIMIT ?`
    ).bind(cutoff, policy.batchSize).all<ExpiredAggregateRow>();
    if (result.results.length === 0) {
      break;
    }

    const hashes = result.results.map(row => row.hash);
    const candidateKeys = uniqueNonEmpty(result.results.map(row => row.repair_candidate_r2_key));
    if (candidateKeys.length > 0) {
      await env.LOGS.delete(candidateKeys);
    }

    const placeholders = numberedPlaceholders(hashes.length);
    await env.DB.batch([
      env.DB.prepare(`DELETE FROM ai_attempts WHERE error_hash IN (${placeholders})`).bind(...hashes),
      env.DB.prepare(`DELETE FROM error_reports WHERE hash IN (${placeholders})`).bind(...hashes)
    ]);

    deleted += hashes.length;
    if (hashes.length < policy.batchSize) {
      break;
    }
  }
  return deleted;
}

export async function runTransientMaintenance(env: Env, now = new Date()): Promise<void> {
  await env.DB.batch([
    env.DB.prepare("DELETE FROM ingest_rate_limits WHERE expires_at < ?").bind(now.getTime()),
    env.DB.prepare("DELETE FROM ai_job_locks WHERE locked_until < ?").bind(now.toISOString())
  ]);
}

export async function runRetentionCleanup(env: Env, now = new Date()): Promise<MaintenanceSummary> {
  const policy = resolveRetentionPolicy(env);
  const { detailCutoff, aggregateCutoff } = buildRetentionCutoffs(now, policy);

  const expiredEventCount = await deleteExpiredEvents(env, detailCutoff, policy);
  const expiredAggregateCount = await deleteExpiredAggregates(env, aggregateCutoff, policy);
  await env.DB.batch([
    env.DB.prepare("DELETE FROM ai_attempts WHERE started_at < ?").bind(aggregateCutoff),
    env.DB.prepare("DELETE FROM ai_daily_usage WHERE usage_day < ?").bind(aggregateCutoff.slice(0, 10))
  ]);

  const summary: MaintenanceSummary = {
    detail_cutoff: detailCutoff,
    aggregate_cutoff: aggregateCutoff,
    expired_event_count: expiredEventCount,
    expired_aggregate_count: expiredAggregateCount
  };
  console.log(JSON.stringify({ message: "retention_cleanup_complete", ...summary }));
  return summary;
}
