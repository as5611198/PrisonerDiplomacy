import { describe, expect, it } from "vitest";
import { describeAiProviders, publicHttpsHost } from "../src/admin";

describe("admin provider metadata", () => {
  it("returns only the public HTTPS host", () => {
    expect(publicHttpsHost("https://relay.example.com/v1/chat/completions?token=secret"))
      .toBe("relay.example.com");
    expect(publicHttpsHost("http://relay.example.com/v1")).toBeNull();
    expect(publicHttpsHost("not a url")).toBeNull();
  });

  it("reports configuration without exposing credentials", () => {
    const summary = describeAiProviders({
      ENVIRONMENT: "staging",
      TRIAGE_ENABLED: "TRUE",
      TRIAGE_MODEL: "gemini-3.7-flash",
      GEMINI_API_KEY: "gemini-secret",
      REPAIR_ENABLED: "true",
      REPAIR_MODEL: "gpt-5.6-sol",
      REPAIR_AI_PROVIDER: "AI-HUB",
      REPAIR_AI_ENDPOINT: "https://relay.example.com/v1",
      REPAIR_AI_API_KEY: "repair-secret",
      REPAIR_SOURCE_REF: "0123456789abcdef0123456789abcdef01234567"
    } as Env);

    expect(summary).toEqual({
      environment: "staging",
      triage: {
        enabled: true,
        configured: true,
        provider: "Google Gemini API",
        model: "gemini-3.7-flash"
      },
      repair: {
        enabled: true,
        configured: true,
        provider: "AI-HUB",
        endpoint_host: "relay.example.com",
        model: "gpt-5.6-sol",
        source_ref: "0123456789abcdef0123456789abcdef01234567"
      }
    });
    expect(JSON.stringify(summary)).not.toContain("secret");
  });
});
