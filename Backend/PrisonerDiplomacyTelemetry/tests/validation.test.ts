import { describe, expect, it } from "vitest";
import {
  PayloadValidationError,
  parsePayloadJson,
  parsePositiveLimit,
  sanitizeText,
  validatePayload
} from "../src/validation";

function validPayload(): Record<string, unknown> {
  return {
    schema_version: 1,
    event_id: "0123456789abcdef0123456789abcdef",
    error_hash: "abcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcd",
    hash_algorithm: "sha256",
    captured_at_utc: "2026-08-20T12:34:56.000Z",
    source: "transaction_sentinel",
    trust_level: "high",
    operation: "accept_deal",
    mod_version: "1.2.0",
    game_version: "1.6.4633",
    exception_type: "System.InvalidOperationException",
    message: "api_key=should-not-leak C:\\Users\\Tester\\save",
    stack_trace: "at PrisonerDiplomacy.Core.DealEngine.Accept()",
    deal_context: {
      deal_id: "deal-1",
      state: "AwaitingPayment",
      origin: "player",
      negotiation_round: 2,
      prisoner_delivered: false,
      reward_issued: false
    },
    active_mod_list: [
      { package_id: "PrisonerDiplomacy", version: "1.2.0" },
      { package_id: "prisonerdiplomacy", version: "duplicate" }
    ]
  };
}

describe("telemetry payload validation", () => {
  it("normalizes timestamps, deduplicates mod IDs, and redacts secrets", () => {
    const payload = validatePayload(validPayload());
    expect(payload.captured_at_utc).toBe("2026-08-20T12:34:56.000Z");
    expect(payload.active_mod_list).toHaveLength(1);
    expect(payload.message).toContain("<redacted>");
    expect(payload.message).not.toContain("Tester");
  });

  it("parses JSON and rejects malformed identity fields", () => {
    expect(() => parsePayloadJson("not-json")).toThrowError(PayloadValidationError);
    const payload = validPayload();
    payload.event_id = "bad";
    expect(() => validatePayload(payload)).toThrowError(/event_id_format/);
  });

  it("rejects oversized stack traces and invalid deal values", () => {
    const oversized = validPayload();
    oversized.stack_trace = "x".repeat(32_769);
    expect(() => validatePayload(oversized)).toThrowError(/stack_trace_too_long/);

    const invalidDeal = validPayload();
    invalidDeal.deal_context = {
      negotiation_round: -1,
      prisoner_delivered: false,
      reward_issued: false
    };
    expect(() => validatePayload(invalidDeal)).toThrowError(/negotiation_round_range/);
  });

  it("redacts local paths and secret-like values", () => {
    const result = sanitizeText(
      "C:\\Users\\alice\\save api_key=abc123 /home/alice/config",
      4096
    );
    expect(result).toContain("<redacted-path>");
    expect(result).toContain("api_key=<redacted>");
    expect(result).not.toContain("alice");
    expect(result).not.toContain("abc123");
  });

  it("keeps rate and limit parsing bounded", () => {
    expect(parsePositiveLimit(undefined, 3, 20)).toBe(3);
    expect(parsePositiveLimit("999", 3, 20)).toBe(20);
    expect(parsePositiveLimit("0", 3, 20)).toBe(3);
  });
});
