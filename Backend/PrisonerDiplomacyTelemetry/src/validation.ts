export const DEFAULT_MAX_BODY_BYTES = 262_144;
export const MAX_MESSAGE_CHARACTERS = 4_096;
export const MAX_STACK_CHARACTERS = 32_768;
export const MAX_MOD_ENTRIES = 512;

const EVENT_ID_PATTERN = /^[0-9a-f]{32}$/i;
const ERROR_HASH_PATTERN = /^[0-9a-f]{64}$/i;
const PATH_PATTERN = /(?:[A-Za-z]:\\[^\r\n\t ]+|(?:\/home|\/Users)\/[^\s]+)(?=$|[\s,)])/gi;
const SOURCE_PATH_PATTERN = /\s+in\s+[^\r\n]+:line\s+(\d+)/gi;
const SECRET_PATTERN = /\b(api[_ -]?key|authorization|bearer|access[_ -]?token|secret)\b\s*[:=]?\s*[^\s,;]+/gi;
const CONTROL_CHARACTER_PATTERN = /[\u0000-\u0008\u000B\u000C\u000E-\u001F\u007F]/g;

export interface TelemetryModEntry {
  package_id: string;
  version: string;
}

export interface TelemetryDealContext {
  deal_id?: string;
  state?: string;
  origin?: string;
  negotiation_round: number;
  prisoner_delivered: boolean;
  reward_issued: boolean;
}

export interface TelemetryPayload {
  schema_version: 1;
  event_id: string;
  error_hash: string;
  hash_algorithm: "sha256";
  captured_at_utc: string;
  source: string;
  trust_level: string;
  operation: string;
  mod_version: string;
  game_version: string;
  exception_type: string;
  message: string;
  stack_trace: string;
  deal_context?: TelemetryDealContext;
  active_mod_list: TelemetryModEntry[];
}

export class PayloadValidationError extends Error {
  public readonly code: "invalid_json" | "invalid_payload";

  public constructor(code: "invalid_json" | "invalid_payload", message: string) {
    super(message);
    this.name = "PayloadValidationError";
    this.code = code;
  }
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function boundedString(value: unknown, field: string, maximum: number, required = true): string {
  if (typeof value !== "string") {
    if (!required && (value === undefined || value === null)) {
      return "";
    }
    throw new PayloadValidationError("invalid_payload", `${field}_type`);
  }
  if (value.length > maximum) {
    throw new PayloadValidationError("invalid_payload", `${field}_too_long`);
  }
  return sanitizeText(value, maximum);
}

function boundedInteger(value: unknown, field: string, minimum: number, maximum: number): number {
  if (!Number.isInteger(value) || (value as number) < minimum || (value as number) > maximum) {
    throw new PayloadValidationError("invalid_payload", `${field}_range`);
  }
  return value as number;
}

function boundedBoolean(value: unknown, field: string): boolean {
  if (typeof value !== "boolean") {
    throw new PayloadValidationError("invalid_payload", `${field}_type`);
  }
  return value;
}

export function sanitizeText(value: string, maximum: number): string {
  const withoutControls = value.replace(CONTROL_CHARACTER_PATTERN, "");
  const withoutSecrets = withoutControls.replace(SECRET_PATTERN, "$1=<redacted>");
  const withoutSourcePaths = withoutSecrets.replace(SOURCE_PATH_PATTERN, " in <redacted>:line $1");
  const withoutPaths = withoutSourcePaths.replace(PATH_PATTERN, "<redacted-path>");
  return withoutPaths.slice(0, maximum);
}

function validateCapturedAt(value: unknown): string {
  const capturedAt = boundedString(value, "captured_at_utc", 64);
  if (Number.isNaN(Date.parse(capturedAt))) {
    throw new PayloadValidationError("invalid_payload", "captured_at_utc_format");
  }
  return new Date(capturedAt).toISOString();
}

function validateDealContext(value: unknown): TelemetryDealContext | undefined {
  if (value === undefined || value === null) {
    return undefined;
  }
  if (!isRecord(value)) {
    throw new PayloadValidationError("invalid_payload", "deal_context_type");
  }

  const dealId = value.deal_id === undefined || value.deal_id === null
    ? undefined
    : boundedString(value.deal_id, "deal_id", 64);
  const state = value.state === undefined || value.state === null
    ? undefined
    : boundedString(value.state, "deal_state", 128);
  const origin = value.origin === undefined || value.origin === null
    ? undefined
    : boundedString(value.origin, "deal_origin", 128);

  return {
    ...(dealId ? { deal_id: dealId } : {}),
    ...(state ? { state } : {}),
    ...(origin ? { origin } : {}),
    negotiation_round: boundedInteger(value.negotiation_round, "negotiation_round", 0, 100_000),
    prisoner_delivered: boundedBoolean(value.prisoner_delivered, "prisoner_delivered"),
    reward_issued: boundedBoolean(value.reward_issued, "reward_issued")
  };
}

function validateActiveMods(value: unknown): TelemetryModEntry[] {
  if (!Array.isArray(value)) {
    throw new PayloadValidationError("invalid_payload", "active_mod_list_type");
  }
  if (value.length > MAX_MOD_ENTRIES) {
    throw new PayloadValidationError("invalid_payload", "active_mod_list_too_large");
  }

  const result: TelemetryModEntry[] = [];
  const seen = new Set<string>();
  for (const entry of value) {
    if (!isRecord(entry)) {
      throw new PayloadValidationError("invalid_payload", "active_mod_entry_type");
    }
    const packageId = boundedString(entry.package_id, "package_id", 160);
    const version = boundedString(entry.version, "mod_version_entry", 64, false);
    const normalizedPackageId = packageId.toLowerCase();
    if (seen.has(normalizedPackageId)) {
      continue;
    }
    seen.add(normalizedPackageId);
    result.push({ package_id: packageId, version });
  }
  return result;
}

export function validatePayload(value: unknown): TelemetryPayload {
  if (!isRecord(value)) {
    throw new PayloadValidationError("invalid_payload", "root_type");
  }
  if (value.schema_version !== 1) {
    throw new PayloadValidationError("invalid_payload", "schema_version");
  }

  const eventId = boundedString(value.event_id, "event_id", 64);
  if (!EVENT_ID_PATTERN.test(eventId)) {
    throw new PayloadValidationError("invalid_payload", "event_id_format");
  }
  const errorHash = boundedString(value.error_hash, "error_hash", 128).toLowerCase();
  if (!ERROR_HASH_PATTERN.test(errorHash)) {
    throw new PayloadValidationError("invalid_payload", "error_hash_format");
  }
  if (value.hash_algorithm !== "sha256") {
    throw new PayloadValidationError("invalid_payload", "hash_algorithm");
  }

  return {
    schema_version: 1,
    event_id: eventId.toLowerCase(),
    error_hash: errorHash,
    hash_algorithm: "sha256",
    captured_at_utc: validateCapturedAt(value.captured_at_utc),
    source: boundedString(value.source, "source", 64),
    trust_level: boundedString(value.trust_level, "trust_level", 16),
    operation: boundedString(value.operation, "operation", 256),
    mod_version: boundedString(value.mod_version, "mod_version", 64),
    game_version: boundedString(value.game_version, "game_version", 64),
    exception_type: boundedString(value.exception_type, "exception_type", 256),
    message: boundedString(value.message, "message", MAX_MESSAGE_CHARACTERS),
    stack_trace: boundedString(value.stack_trace, "stack_trace", MAX_STACK_CHARACTERS),
    ...(value.deal_context === undefined || value.deal_context === null
      ? {}
      : { deal_context: validateDealContext(value.deal_context) }),
    active_mod_list: validateActiveMods(value.active_mod_list)
  };
}

export function parsePayloadJson(body: string): TelemetryPayload {
  let value: unknown;
  try {
    value = JSON.parse(body) as unknown;
  } catch {
    throw new PayloadValidationError("invalid_json", "invalid_json");
  }
  return validatePayload(value);
}

export function parsePositiveLimit(value: string | undefined, fallback: number, maximum: number): number {
  const parsed = Number.parseInt(value ?? "", 10);
  if (!Number.isFinite(parsed) || parsed < 1) {
    return fallback;
  }
  return Math.min(parsed, maximum);
}
