import { describe, expect, it } from "vitest";
import {
  DEFAULT_AGGREGATE_RETENTION_DAYS,
  DEFAULT_DETAIL_LOG_RETENTION_DAYS,
  STALE_REPAIR_ATTEMPT_MINUTES,
  buildRetentionCutoffs,
  buildStaleRepairCutoff,
  resolveRetentionPolicy,
  runTransientMaintenance
} from "../src/maintenance";

describe("telemetry retention policy", () => {
  it("uses the documented 30-day and 180-day defaults", () => {
    const policy = resolveRetentionPolicy({});

    expect(policy.detailLogDays).toBe(DEFAULT_DETAIL_LOG_RETENTION_DAYS);
    expect(policy.aggregateDays).toBe(DEFAULT_AGGREGATE_RETENTION_DAYS);
    expect(policy.batchSize).toBe(100);
    expect(policy.maximumBatches).toBe(10);
  });

  it("never expires aggregates before detailed logs", () => {
    const policy = resolveRetentionPolicy({
      DETAIL_LOG_RETENTION_DAYS: "90",
      AGGREGATE_RETENTION_DAYS: "20",
      RETENTION_BATCH_SIZE: "500",
      RETENTION_MAX_BATCHES_PER_RUN: "500"
    });

    expect(policy.detailLogDays).toBe(90);
    expect(policy.aggregateDays).toBe(90);
    expect(policy.batchSize).toBe(100);
    expect(policy.maximumBatches).toBe(20);
  });

  it("builds deterministic UTC cutoffs", () => {
    const cutoffs = buildRetentionCutoffs(
      new Date("2026-08-20T12:00:00.000Z"),
      resolveRetentionPolicy({})
    );

    expect(cutoffs.detailCutoff).toBe("2026-07-21T12:00:00.000Z");
    expect(cutoffs.aggregateCutoff).toBe("2026-02-21T12:00:00.000Z");
  });

  it("recovers interrupted repair attempts only after their worker lease window", () => {
    expect(STALE_REPAIR_ATTEMPT_MINUTES).toBe(20);
    expect(buildStaleRepairCutoff(new Date("2026-08-21T14:40:00.000Z")))
      .toBe("2026-08-21T14:20:00.000Z");
  });

  it("requeues stale repair rows and closes their unfinished audit attempts", async () => {
    const statements: Array<{ sql: string; values: unknown[] }> = [];
    const db = {
      prepare(sql: string) {
        return {
          bind(...values: unknown[]) {
            statements.push({ sql, values });
            return this;
          }
        };
      },
      async batch() {
        return [];
      }
    } as unknown as D1Database;

    await runTransientMaintenance(
      { DB: db } as Env,
      new Date("2026-08-21T14:40:00.000Z")
    );

    expect(statements).toHaveLength(4);
    expect(statements[1].sql).toContain("repair_status = 'retry_wait'");
    expect(statements[1].sql).toContain("repair_last_error = 'worker_execution_interrupted'");
    expect(statements[1].values).toEqual([
      "2026-08-21T14:40:00.000Z",
      "2026-08-21T14:40:00.000Z",
      "2026-08-21T14:20:00.000Z"
    ]);
    expect(statements[2].sql).toContain("status = 'failed'");
    expect(statements[2].sql).toContain("error_code = 'worker_execution_interrupted'");
    expect(statements[3].sql).toContain("DELETE FROM ai_job_locks");
  });
});
