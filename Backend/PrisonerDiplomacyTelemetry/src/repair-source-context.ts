import { AiProviderError } from "./ai";
import type { AiIssueContext, AiSample } from "./ai";
import { REPAIR_SOURCE_INDEX } from "./repair-source-index.generated";
import type { RepairSourceIndexEntry } from "./repair-source-index.generated";

const RAW_SOURCE_ROOT = "https://raw.githubusercontent.com/as5611198/PrisonerDiplomacy";
const maximumFetchedFileCharacters = 350_000;
const maximumFiles = 6;
const defaultMaximumContextCharacters = 180_000;
const maximumCharactersPerFile = 55_000;
const ignoredSignals = new Set([
  "at", "bool", "exception", "false", "get", "int", "list", "null", "object",
  "prisonerdiplomacy", "rimworld", "set", "string", "system", "true", "void"
]);

export interface RepairSourceContext {
  ref: string;
  files: string[];
  text: string;
}

interface RepairSignals {
  types: Set<string>;
  methods: Set<string>;
  keywords: Set<string>;
}

interface RankedEntry {
  entry: RepairSourceIndexEntry;
  score: number;
}

function normalizeSignal(value: string): string {
  return value.replace(/`\d+$/, "").replace(/[^A-Za-z0-9_]/g, "").toLowerCase();
}

function addSignal(target: Set<string>, value: string): void {
  const normalized = normalizeSignal(value);
  if (normalized.length >= 3 && !ignoredSignals.has(normalized)) {
    target.add(normalized);
  }
}

export function extractRepairSignals(issue: AiIssueContext, samples: AiSample[]): RepairSignals {
  const types = new Set<string>();
  const methods = new Set<string>();
  const keywords = new Set<string>();
  const telemetryText = [
    issue.error_message,
    issue.operation,
    issue.source,
    ...samples.flatMap(sample => [sample.payload.message, sample.payload.stack_trace])
  ].join("\n");

  for (const match of telemetryText.matchAll(/PrisonerDiplomacy(?:\.[A-Za-z_][A-Za-z0-9_`+]*){1,}/g)) {
    const parts = match[0].split(".");
    if (parts.length >= 2) {
      addSignal(methods, parts.at(-1) ?? "");
      addSignal(types, parts.at(-2) ?? "");
      for (const part of parts.slice(1, -2)) {
        addSignal(keywords, part);
      }
    }
  }
  for (const word of `${issue.operation} ${issue.source}`.split(/[^A-Za-z0-9_]+/)) {
    addSignal(keywords, word);
  }
  return { types, methods, keywords };
}

function includesSignal(values: readonly string[], signals: Set<string>): boolean {
  return values.some(value => signals.has(normalizeSignal(value)));
}

export function rankRepairSourceEntries(signals: RepairSignals): RankedEntry[] {
  return REPAIR_SOURCE_INDEX.map(entry => {
    const normalizedPath = entry.path.toLowerCase();
    let score = 0;
    if (includesSignal(entry.symbols, signals.types)) {
      score += 100;
    }
    if (includesSignal(entry.methods, signals.methods)) {
      score += 70;
    }
    for (const signal of signals.types) {
      if (normalizedPath.includes(signal)) {
        score += 35;
      }
    }
    for (const signal of signals.methods) {
      if (normalizedPath.includes(signal)) {
        score += 15;
      }
    }
    for (const signal of signals.keywords) {
      if (normalizedPath.includes(signal)) {
        score += 8;
      }
    }
    return { entry, score };
  }).filter(item => item.score > 0)
    .sort((left, right) => right.score - left.score || left.entry.path.localeCompare(right.entry.path));
}

function contextMaximum(value: string | undefined): number {
  const parsed = Number.parseInt(value ?? "", 10);
  if (!Number.isFinite(parsed) || parsed < 40_000) {
    return defaultMaximumContextCharacters;
  }
  return Math.min(parsed, 300_000);
}

function assertSourceRef(value: string | undefined): string {
  const ref = value?.trim() ?? "";
  if (!/^[a-f0-9]{40}$/i.test(ref)) {
    throw new AiProviderError(
      "invalid_repair_source_ref",
      "REPAIR_SOURCE_REF must be a full Git commit SHA",
      false
    );
  }
  return ref.toLowerCase();
}

async function fetchSourceFile(ref: string, entry: RepairSourceIndexEntry): Promise<string> {
  const url = `${RAW_SOURCE_ROOT}/${ref}/${entry.path}`;
  let response: Response;
  try {
    response = await fetch(url, { headers: { Accept: "text/plain" } });
  } catch {
    throw new AiProviderError("repair_source_network_error", "Public repair source could not be fetched", true);
  }
  if (!response.ok) {
    const retryable = response.status === 408 || response.status === 429 || response.status >= 500;
    throw new AiProviderError(
      `repair_source_http_${response.status}`,
      `Public repair source returned HTTP ${response.status}`,
      retryable,
      response.status
    );
  }
  const source = await response.text();
  if (source.length === 0 || source.length > maximumFetchedFileCharacters) {
    throw new AiProviderError(
      "invalid_repair_source_size",
      "Public repair source was empty or exceeded the file limit",
      false
    );
  }
  return source.replace(/\r\n/g, "\n");
}

function matchingLineIndexes(lines: string[], signals: RepairSignals): number[] {
  const results = new Set<number>();
  for (const needles of [signals.methods, signals.types]) {
    for (let index = 0; index < lines.length; index++) {
      const normalized = lines[index].toLowerCase();
      if ([...needles].some(needle => normalized.includes(needle))) {
        results.add(index);
      }
    }
  }
  return [...results];
}

function mergeRanges(ranges: Array<[number, number]>): Array<[number, number]> {
  const sorted = ranges.sort((left, right) => left[0] - right[0]);
  const merged: Array<[number, number]> = [];
  for (const range of sorted) {
    const previous = merged.at(-1);
    if (previous && range[0] <= previous[1] + 1) {
      previous[1] = Math.max(previous[1], range[1]);
    } else {
      merged.push([...range]);
    }
  }
  return merged;
}

export function createSourceExcerpt(source: string, signals: RepairSignals, maximum: number): string {
  if (source.length <= maximum) {
    return source;
  }
  const lines = source.split("\n");
  const matches = matchingLineIndexes(lines, signals);
  const ranges: Array<[number, number]> = [[0, Math.min(lines.length - 1, 35)]];
  for (const match of matches.slice(0, 8)) {
    ranges.push([Math.max(0, match - 65), Math.min(lines.length - 1, match + 110)]);
  }
  let excerpt = "";
  for (const [start, end] of mergeRanges(ranges)) {
    const marker = start > 0 ? `\n/* SOURCE LINES ${start + 1}-${end + 1}; OTHER LINES OMITTED */\n` : "";
    const candidate = `${marker}${lines.slice(start, end + 1).join("\n")}\n`;
    if (excerpt.length + candidate.length > maximum) {
      const remaining = maximum - excerpt.length;
      if (remaining > 1_000) {
        excerpt += candidate.slice(0, remaining);
      }
      break;
    }
    excerpt += candidate;
  }
  return excerpt || source.slice(0, maximum);
}

export async function loadRepairSourceContext(
  env: Env,
  issue: AiIssueContext,
  samples: AiSample[]
): Promise<RepairSourceContext> {
  const ref = assertSourceRef(env.REPAIR_SOURCE_REF);
  const signals = extractRepairSignals(issue, samples);
  const ranked = rankRepairSourceEntries(signals).slice(0, maximumFiles);
  if (ranked.length === 0) {
    throw new AiProviderError(
      "repair_source_not_identified",
      "No repository source file matched the Prisoner Diplomacy stack",
      false
    );
  }

  const fetched = await Promise.allSettled(
    ranked.map(async item => ({ entry: item.entry, source: await fetchSourceFile(ref, item.entry) }))
  );
  const successful = fetched.flatMap(result => result.status === "fulfilled" ? [result.value] : []);
  if (successful.length === 0) {
    const firstFailure = fetched.find(result => result.status === "rejected") as PromiseRejectedResult | undefined;
    if (firstFailure?.reason instanceof AiProviderError) {
      throw firstFailure.reason;
    }
    throw new AiProviderError("repair_source_unavailable", "No matching public repair source was available", true);
  }

  const maximum = contextMaximum(env.REPAIR_SOURCE_MAX_CHARACTERS);
  let remaining = maximum;
  const sections: string[] = [];
  const files: string[] = [];
  for (const item of successful) {
    const header = `\n=== TRUSTED SOURCE FILE ${item.entry.path} @ ${ref} ===\n`;
    const available = Math.min(maximumCharactersPerFile, remaining - header.length);
    if (available < 2_000) {
      break;
    }
    const excerpt = createSourceExcerpt(item.source, signals, available);
    sections.push(`${header}${excerpt}`);
    files.push(item.entry.path);
    remaining -= header.length + excerpt.length;
  }
  if (files.length === 0) {
    throw new AiProviderError("repair_source_context_empty", "Repair source context was empty", false);
  }
  return {
    ref,
    files,
    text: [
      `Repository source ref: ${ref}`,
      "Only the file contents below are trusted source. Omission markers are context markers, not repository code.",
      ...sections
    ].join("\n")
  };
}
