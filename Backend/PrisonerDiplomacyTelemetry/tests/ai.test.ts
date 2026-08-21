import { describe, expect, it } from "vitest";
import {
  AiProviderError,
  buildRepairPrompt,
  buildTriagePrompt,
  normalizeRepairEndpoint,
  parseRepairCandidate,
  parseTriageDecision,
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

  it("bounds structured Gemini evidence without weakening the decision fields", () => {
    const decision = parseTriageDecision(JSON.stringify({
      classification: "likely_internal",
      confidence: 0.93,
      severity: "high",
      evidence: [{ frame: "PrisonerDiplomacy.GameComponent.AcceptDeal", reason: "internal frame" }],
      send_to_repair_ai: true
    }));

    expect(decision.evidence).toHaveLength(1);
    expect(decision.evidence[0]).toContain('"frame"');
    expect(decision.send_to_repair_ai).toBe(true);
  });

  it("treats oversized or missing evidence as bounded non-authoritative detail", () => {
    const oversized = parseTriageDecision(JSON.stringify({
      classification: "likely_internal",
      confidence: 0.9,
      severity: "high",
      evidence: Array.from({ length: 12 }, () => "x".repeat(900)),
      send_to_repair_ai: true
    }));
    const missing = parseTriageDecision(JSON.stringify({
      classification: "insufficient_evidence",
      confidence: 0.4,
      severity: "low",
      send_to_repair_ai: false
    }));

    expect(oversized.evidence).toHaveLength(8);
    expect(oversized.evidence.every(item => item.length === 512)).toBe(true);
    expect(missing.evidence).toEqual([]);
  });

  it("bounds untrusted telemetry before building provider prompts", () => {
    const samples = [{
      event_id: samplePayload().event_id,
      captured_at_utc: samplePayload().captured_at_utc,
      payload: samplePayload()
    }];
    const triagePrompt = buildTriagePrompt(issue, samples);
    const repairPrompt = buildRepairPrompt(
      issue,
      samples,
      "Repository source ref: 0123456789abcdef0123456789abcdef01234567\npublic void AcceptDeal() {}"
    );

    expect(triagePrompt.length).toBeLessThan(50_000);
    expect(repairPrompt.length).toBeLessThan(55_000);
    expect(triagePrompt).toContain("UNTRUSTED TELEMETRY DATA START");
    expect(repairPrompt).toContain("TRUSTED PUBLIC REPOSITORY SOURCE START");
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
      root_cause: { summary: "Null guard missing", method: "Deal.Accept" },
      affected_files: { path: "Source/Deal.cs", method: "Deal.Accept" },
      patch: "diff --git a/Source/Deal.cs b/Source/Deal.cs\n--- a/Source/Deal.cs\n+++ b/Source/Deal.cs\n@@ -1 +1 @@\n-old\n+new",
      tests: "Run smoke test",
      risks: { severity: "medium", detail: "May hide invalid state" }
    }));

    expect(candidate.root_cause).toContain('"summary": "Null guard missing"');
    expect(candidate.affected_files[0]).toContain('"path": "Source/Deal.cs"');
    expect(candidate.patch).toContain("diff --git a/Source/Deal.cs");

    const wrappedCandidate = parseRepairCandidate(JSON.stringify({
      root_cause: "Null guard missing",
      affected_files: ["Source/Deal.cs"],
      patch: "```diff\ndiff --git a/Source/Deal.cs b/Source/Deal.cs\n--- a/Source/Deal.cs\n+++ b/Source/Deal.cs\n@@ -1 +1 @@\n-old\n+new\n*** End Patch\n```",
      tests: ["Run smoke test"],
      risks: []
    }));
    expect(wrappedCandidate.patch).toBe(
      "diff --git a/Source/Deal.cs b/Source/Deal.cs\n--- a/Source/Deal.cs\n+++ b/Source/Deal.cs\n@@ -1 +1 @@\n-old\n+new"
    );

    const withoutKnownFiles = parseRepairCandidate(JSON.stringify({
      root_cause: "Insufficient source context",
      affected_files: [],
      patch: "diff --git a/Source/Deal.cs b/Source/Deal.cs\n--- a/Source/Deal.cs\n+++ b/Source/Deal.cs\n@@ -1 +1 @@\n-old\n+new",
      tests: [],
      risks: []
    }));
    expect(withoutKnownFiles.affected_files).toEqual([]);
  });

  it("rejects repair prose that is not an applicable unified diff", () => {
    expect(() => parseRepairCandidate(JSON.stringify({
      root_cause: "Null guard missing",
      affected_files: ["Source/Deal.cs"],
      patch: "Add a null guard to Deal.Accept",
      tests: ["Run smoke test"],
      risks: []
    }))).toThrowError(/unified diff/);

    expect(() => parseRepairCandidate(JSON.stringify({
      root_cause: "Null guard missing",
      affected_files: ["Source/Deal.cs"],
      patch: "Explanation first\ndiff --git a/Source/Deal.cs b/Source/Deal.cs\n--- a/Source/Deal.cs\n+++ b/Source/Deal.cs\n@@ -1 +1 @@\n-old\n+new",
      tests: ["Run smoke test"],
      risks: []
    }))).toThrowError(/unified diff/);
  });
});
