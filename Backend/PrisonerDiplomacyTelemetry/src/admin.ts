export interface AiProviderSummary {
  environment: string;
  triage: {
    enabled: boolean;
    configured: boolean;
    provider: "Google Gemini API";
    model: string;
  };
  repair: {
    enabled: boolean;
    configured: boolean;
    provider: string;
    endpoint_host: string | null;
    model: string;
    source_ref: string | null;
  };
}

function isEnabled(value: string | undefined): boolean {
  return value?.trim().toLowerCase() === "true";
}

export function publicHttpsHost(value: string | undefined): string | null {
  if (!value) {
    return null;
  }
  try {
    const url = new URL(value);
    return url.protocol === "https:" ? url.hostname.toLowerCase() : null;
  } catch {
    return null;
  }
}

export function describeAiProviders(env: Env): AiProviderSummary {
  const endpointHost = publicHttpsHost(env.REPAIR_AI_ENDPOINT);
  const sourceRef = /^[a-f0-9]{40}$/i.test(env.REPAIR_SOURCE_REF ?? "")
    ? env.REPAIR_SOURCE_REF
    : null;
  return {
    environment: env.ENVIRONMENT ?? "unknown",
    triage: {
      enabled: isEnabled(env.TRIAGE_ENABLED),
      configured: Boolean(env.GEMINI_API_KEY),
      provider: "Google Gemini API",
      model: env.TRIAGE_MODEL || "gemini-3.7-flash"
    },
    repair: {
      enabled: isEnabled(env.REPAIR_ENABLED),
      configured: Boolean(endpointHost && env.REPAIR_AI_API_KEY && sourceRef),
      provider: env.REPAIR_AI_PROVIDER || "Unspecified relay",
      endpoint_host: endpointHost,
      model: env.REPAIR_MODEL || "gpt-5.6-sol",
      source_ref: sourceRef
    }
  };
}
