import { describe, expect, it } from "vitest";
import {
  DEFAULT_AGGREGATE_RETENTION_DAYS,
  DEFAULT_DETAIL_LOG_RETENTION_DAYS,
  buildRetentionCutoffs,
  resolveRetentionPolicy
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
});
