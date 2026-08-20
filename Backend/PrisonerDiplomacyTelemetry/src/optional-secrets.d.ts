// AI providers are optional while both feature flags are false. These names are
// intentionally not Wrangler-required secrets so the telemetry-only Worker can deploy.
interface Env {
  GEMINI_API_KEY?: string;
  REPAIR_AI_ENDPOINT?: string;
  REPAIR_AI_API_KEY?: string;
}
