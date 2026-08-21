import { afterEach, describe, expect, it, vi } from "vitest";
import {
  createSourceExcerpt,
  extractRepairSignals,
  loadRepairSourceContext,
  rankRepairSourceEntries
} from "../src/repair-source-context";
import type { AiIssueContext, AiSample } from "../src/ai";
import type { TelemetryPayload } from "../src/validation";

const issue: AiIssueContext = {
  hash: "abcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcd",
  error_message: "accept failed",
  exception_type: "System.NullReferenceException",
  operation: "accept_deal",
  source: "transaction_sentinel",
  trust_level: "high",
  mod_version: "1.2.0",
  game_version: "1.6.4633",
  occurrence_count: 4,
  first_seen: "2026-08-20T12:00:00.000Z",
  last_seen: "2026-08-20T13:00:00.000Z"
};

const payload: TelemetryPayload = {
  schema_version: 1,
  event_id: "0123456789abcdef0123456789abcdef",
  error_hash: issue.hash,
  hash_algorithm: "sha256",
  captured_at_utc: "2026-08-20T12:34:56.000Z",
  source: issue.source,
  trust_level: issue.trust_level,
  operation: issue.operation,
  mod_version: issue.mod_version,
  game_version: issue.game_version,
  exception_type: issue.exception_type,
  message: issue.error_message,
  stack_trace: "at PrisonerDiplomacy.PrisonerDiplomacyGameComponent.AcceptDeal(PrisonerDeal deal)",
  active_mod_list: []
};

const samples: AiSample[] = [{
  event_id: payload.event_id,
  captured_at_utc: payload.captured_at_utc,
  payload
}];

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("repair source context", () => {
  it("ranks the stack type and method ahead of unrelated source", () => {
    const signals = extractRepairSignals(issue, samples);
    const ranked = rankRepairSourceEntries(signals);

    expect(ranked.length).toBeGreaterThan(0);
    expect(ranked.slice(0, 6).some(item =>
      item.entry.path.includes("PrisonerDiplomacyGameComponent"))).toBe(true);
  });

  it("keeps matching source lines while bounding a large file", () => {
    const signals = extractRepairSignals(issue, samples);
    const source = [
      "using System;",
      ...Array.from({ length: 300 }, (_, index) => `// filler ${index}`),
      "public void AcceptDeal(PrisonerDeal deal)",
      "{",
      "    deal.Accept();",
      "}",
      ...Array.from({ length: 300 }, (_, index) => `// tail ${index}`)
    ].join("\n");

    const excerpt = createSourceExcerpt(source, signals, 4_000);
    expect(excerpt.length).toBeLessThanOrEqual(4_000);
    expect(excerpt).toContain("public void AcceptDeal");
    expect(excerpt).toContain("SOURCE LINES");
  });

  it("fetches only ranked public files at a fixed commit", async () => {
    const fetchedUrls: string[] = [];
    vi.stubGlobal("fetch", vi.fn(async (input: string | URL | Request) => {
      fetchedUrls.push(String(input));
      return new Response("namespace PrisonerDiplomacy { public class PrisonerDiplomacyGameComponent { public void AcceptDeal() {} } }");
    }));

    const context = await loadRepairSourceContext({
      REPAIR_SOURCE_REF: "0123456789abcdef0123456789abcdef01234567",
      REPAIR_SOURCE_MAX_CHARACTERS: "50000"
    } as Env, issue, samples);

    expect(context.ref).toBe("0123456789abcdef0123456789abcdef01234567");
    expect(context.files.length).toBeGreaterThan(0);
    expect(context.files.length).toBeLessThanOrEqual(6);
    expect(context.text).toContain("TRUSTED SOURCE FILE");
    expect(fetchedUrls.every(url => url.startsWith(
      "https://raw.githubusercontent.com/as5611198/PrisonerDiplomacy/0123456789abcdef0123456789abcdef01234567/Source/PrisonerDiplomacy/"
    ))).toBe(true);
  });

  it("refuses a mutable branch name as repair source", async () => {
    await expect(loadRepairSourceContext({
      REPAIR_SOURCE_REF: "main"
    } as Env, issue, samples)).rejects.toMatchObject({ code: "invalid_repair_source_ref" });
  });
});
