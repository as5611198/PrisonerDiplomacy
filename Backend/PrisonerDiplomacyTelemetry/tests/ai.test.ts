import { describe, expect, it } from "vitest";
import {
  AiProviderError,
  buildRepairPrompt,
  buildTriagePrompt,
  normalizeRepairEndpoint,
  parseRepairCandidate,
  withImmediateRetries
} from "../src/ai";
import { TelemetryPayload } from "../src/validation";

function samplePayload(): TelemetryPayload {
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
    message: "message",
    stack_trace: "x".repeat(32_768),
    active_mod_list: Array.from({ length: 512 }, (_, index) => ({
      package_id: `mod.${index}`,
      version: "1.0.0"
    }))
  };
}

const issue = {
  hash: "abcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcd",
  error_message: "message",
  exception_type: "System.InvalidOperationException",
  operation: "accept_deal",
  source: "transaction_sentinel",
  trust_level: "high",
  mod_version: "1.2.0",
  game_version: "1.6.4633",
  occurrence_count: 3,
  first_seen: "2026-08-20T12:00:00.000Z",
  last_seen: "2026-08-20T12:34:56.000Z"
};

describe("AI workflow guards", () => {
  it("retries a transient provider error once and then succeeds", async () => {
    let calls = 0;
    const result = await withImmediateRetries(
      async () => {
        calls += 1;
        if (calls === 1) {
          throw new AiProviderError("timeout", "timeout", true);
        }
        return "ok";
      },
      2,
      async () => undefined
    );

    expect(result).toBe("ok");
    expect(calls).toBe(2);
  });

  it("does not retry a non-retryable provider error", async () => {
    let calls = 0;
    await expect(withImmediateRetries(
      async () => {
        calls += 1;
        throw new AiProviderError("invalid_json", "invalid", false);
      },
      2,
      async () => undefined
    )).rejects.toMatchObject({ code: "invalid_json" });
    expect(calls).toBe(1);
  });

  it("bounds untrusted telemetry before building provider prompts", () => {
    const samples = [{
      event_id: samplePayload().event_id,
      captured_at_utc: samplePayload().captured_at_utc,
      payload: samplePayload()
    }];
    const triagePrompt = buildTriagePrompt(issue, samples);
    const repairPrompt = buildRepairPrompt(issue, samples);

    expect(triagePrompt.length).toBeLessThan(50_000);
    expect(repairPrompt.length).toBeLessThan(50_000);
    expect(triagePrompt).toContain("UNTRUSTED TELEMETRY DATA START");
  });

  it("accepts a relay base URL or a complete chat-completions URL", () => {
    expect(normalizeRepairEndpoint("https://relay.example/v1"))
      .toBe("https://relay.example/v1/chat/completions");
    expect(normalizeRepairEndpoint("https://relay.example/v1/chat/completions"))
      .toBe("https://relay.example/v1/chat/completions");
    expect(normalizeRepairEndpoint("https://relay.example"))
      .toBe("https://relay.example/v1/chat/completions");
    expect(() => normalizeRepairEndpoint("http://relay.example/v1"))
      .toThrowError(/HTTPS/);
  });

  it("normalizes a structured relay patch for human review", () => {
    const candidate = parseRepairCandidate(JSON.stringify({
      root_cause: "Null guard missing",
      affected_files: ["Source/Deal.cs"],
      patch: { file: "Source/Deal.cs", changes: ["add null guard"] },
      tests: ["Run smoke test"],
      risks: ["May hide invalid state"]
    }));

    expect(candidate.patch).toContain('"file": "Source/Deal.cs"');
  });
});
