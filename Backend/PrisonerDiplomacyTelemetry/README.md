# Prisoner Diplomacy telemetry Worker

This is the independent Cloudflare Worker project used by the RimWorld mod assembly. The production receiver is deployed, but the mod still sends no report until it detects a guarded Prisoner Diplomacy exception and the player explicitly allows that report or the current session.

## Current scope

- `POST /api/report-error`: bounded JSON ingestion for schema version 1.
- Server-side validation and redaction of paths and secret-like values.
- SHA-256 event payload digest and event-level idempotency by `event_id`.
- D1 aggregate rows by `error_hash` plus a separate event index.
- R2 JSON objects at `logs/{error_hash}/{timestamp}-{event_id}.json`.
- D1-backed per-IP rate limiting using a salted one-minute bucket. The raw IP is never stored.
- Admin-only pending issue query, issue detail, event detail, and status/triage patch endpoints.
- Admin-only repair-candidate queue and provider metadata endpoints. Provider metadata exposes only model names, configuration flags, and the public relay host.
- Short-lived rate-limit and job-lock cleanup on every Cron invocation.
- Daily retention cleanup: detailed event/R2 logs after 30 days and aggregate/AI records after 180 days.
- Optional daily Gemini 3.7 Flash triage, guarded by a daily issue budget and confidence/severity gate.
- Optional GPT 5.6 Sol repair-candidate generation through an OpenAI-compatible relay, using bounded excerpts from a fixed public Git commit selected from stack symbols.
- One repair-provider call per run, a hard 24-call daily ceiling, and a persisted 30-minute retry schedule for relay failures.

The AI workflow is deliberately separate from ingestion. The public endpoint never waits for a provider, and AI is disabled by default. Before calling the repair model, the Worker matches stack-frame type and method names against a generated source index, fetches only the top matching C# files from the immutable `REPAIR_SOURCE_REF` commit in the public GitHub repository, and bounds the excerpts by `REPAIR_SOURCE_MAX_CHARACTERS`. If source cannot be identified or fetched, the repair call is not made. A successful response must contain a Git unified diff and is stored as `fix_candidate`; common outer Markdown or `*** End Patch` wrappers are removed before strict marker checks, while the isolated verifier remains authoritative through `git apply --check`. The Worker itself never edits the repository or marks an issue `resolved`. The separate local verifier applies the diff in an isolated Git worktree, builds it, runs localization and RimWorld Smoke Test, and opens a review PR; human approval is still required before `resolved`.

The production environment has both AI stages enabled after separate production credentials and a full staging verification were completed on 2026-08-21. The disclosed providers are the official Google Gemini API for triage and AI-HUB (`ai.aiyuhub.com`) as the OpenAI-compatible repair relay. See [`../../Docs/TelemetryPrivacy.md`](../../Docs/TelemetryPrivacy.md) before changing providers or data processing.

## AI workflow configuration

The Worker has two Cron schedules:

- `0 3 * * *`: selects the most frequent untriaged issues and calls the official Gemini API (`TRIAGE_MODEL`, default `gemini-3.7-flash`).
- `*/30 * * * *`: retries queued repair candidates through `REPAIR_AI_ENDPOINT` using `REPAIR_MODEL`, default `gpt-5.6-sol`.

The daily schedule also removes detailed reports older than `DETAIL_LOG_RETENTION_DAYS` (30), then removes aggregate statistics and related AI records older than `AGGREGATE_RETENTION_DAYS` (180). Retention uses the server receipt time, not the client clock. Cleanup is bounded by `RETENTION_BATCH_SIZE` and `RETENTION_MAX_BATCHES_PER_RUN` and safely resumes on the next run.

Enable each stage explicitly with `TRIAGE_ENABLED=true` or `REPAIR_ENABLED=true`. The default limits are 20 triage issues/day, 24 repair-provider calls/day, one repair issue per 30-minute run, and a 30-minute retry delay. Gemini triage may retry a transient failure once immediately. Repair uses exactly one provider call per run, so its hard daily maximum is 24 rather than 48. Persistent repair failures remain in D1 and resume on later Cron runs and future days without an automatic stop date. If a Worker execution is interrupted before it can write the provider result, transient maintenance recovers the stale `in_progress` attempt after its 20-minute lease window and returns it to the persisted retry queue.

Required secrets when the corresponding stage is enabled:

```text
GEMINI_API_KEY             # official Google Gemini API key
REPAIR_AI_ENDPOINT         # HTTPS OpenAI-compatible base URL or /chat/completions URL
REPAIR_AI_API_KEY          # relay credential
REPAIR_SOURCE_REF          # full immutable Git commit SHA for public repair source
REPAIR_SOURCE_MAX_CHARACTERS # total trusted source-context bound
```

The repair relay may be configured as a host, a `/v1` base URL, or a complete `/chat/completions` URL. It must return an OpenAI-compatible JSON response with `choices[0].message.content` containing an object with `root_cause`, `affected_files`, `patch`, `tests`, and `risks`; `patch` must be one Git unified diff. Keep the relay credential server-side; it is never sent to the mod.

Regenerate and commit the symbol index whenever C# source changes. The check command fails if the committed index is stale:

```powershell
npm run generate-source-index
npm run check
```

## Isolated repair verification

Run the local verifier from the repository root. Production uses the ignored `.production-admin-token`; staging reads `ADMIN_TOKEN` from the ignored backend `.dev.vars` file:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\Tools\Invoke-TelemetryRepair.ps1 -Environment staging
pwsh -NoProfile -ExecutionPolicy Bypass -File .\Tools\Invoke-TelemetryRepair.ps1 -Environment production -PublishPullRequest
```

The verifier downloads the candidate and up to three sanitized event samples into ignored `TelemetryRepairReports`, requires a Git unified diff, permits changes only under `Source/PrisonerDiplomacy`, selected `1.6/Defs`, or the keyed localization files, and rejects common process, reflection, network, registry, and direct filesystem additions. It uses a detached temporary worktree, runs Release build and localization checks, temporarily swaps only the exact candidate test files into the known local RimWorld mod install for `PASS cases=127`, and restores them in `finally`. RimWorld's Unity quicktest does not advance with a fully hidden game window, so a short test window can appear only while an accepted candidate is being verified. A successful run creates `codex/telemetry-*`; `-PublishPullRequest` pushes it and opens a PR. The D1 issue remains `analyzing` until a human approves it. Invalid or failed candidates become `needs_repro`, not `resolved`.

Install the production checker as a hidden current-user Windows task running every 30 minutes:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\Tools\Install-TelemetryRepairTask.ps1 -Environment production
```

`-CandidateFile <fixture.json> -ValidationOnly` runs the same patch/build/Smoke path without an API call, branch, PR, or D1 status change.

## Local smoke

```powershell
npm install
npm run generate-source-index
npm run generate-types
npx wrangler d1 migrations apply prisoner-diplomacy-telemetry --local
npx wrangler dev --local --port 8790
```

In another terminal, send a schema-valid payload to `http://127.0.0.1:8790/api/report-error`. A retry with the same `event_id` returns `200` with `duplicate: true` and does not increment `occurrence_count`.

```powershell
npm run typecheck
npm test
npx wrangler deploy --dry-run --env production
```

`.dev.vars` is local-only and is ignored by git. Use a different random value for `ADMIN_TOKEN` outside local development.

## Cloudflare setup

Login is only required for resource creation and deployment. Do not run these commands until the target Cloudflare account is confirmed:

```powershell
npx wrangler login
npx wrangler whoami
npx wrangler d1 create prisoner-diplomacy-telemetry-staging
npx wrangler r2 bucket create prisoner-diplomacy-telemetry-logs-staging
npx wrangler d1 create prisoner-diplomacy-telemetry-production
npx wrangler r2 bucket create prisoner-diplomacy-telemetry-logs-production
```

Copy the returned D1 IDs into the matching `database_id` fields in `wrangler.jsonc`. The all-zero IDs in the checked-in config are local placeholders and must not be used for a remote deployment.

Apply the schema and set secrets per environment:

```powershell
npx wrangler d1 migrations apply prisoner-diplomacy-telemetry-staging --remote --env staging
npx wrangler secret put ADMIN_TOKEN --env staging
npx wrangler secret put RATE_LIMIT_SALT --env staging
npx wrangler deploy --env staging
```

Only add the AI secrets and set the two enable flags after provider access has been tested. A staging deployment with both flags set to `false` is a safe backend-only smoke target.

To replace the OpenAI-compatible repair relay in staging, rerun the same-named secret commands. Wrangler overwrites the previous values; enter each value only at the interactive prompt so credentials do not enter shell history:

```powershell
npx wrangler secret put REPAIR_AI_ENDPOINT --env staging
npx wrangler secret put REPAIR_AI_API_KEY --env staging
```

Production follows the same sequence with `--env production`. Run `npm run generate-source-index` and `npm run check` before deployment, verify `/healthz` and `/api/admin/provider-info`, then send one synthetic report before treating the AI workflow as active.

## Admin API

Admin requests require `Authorization: Bearer <ADMIN_TOKEN>`:

- `GET /api/admin/pending-top?limit=3`
- `GET /api/admin/fix-candidates?limit=3`
- `GET /api/admin/provider-info`
- `GET /api/admin/issues/{error_hash}`
- `GET /api/admin/events/{event_id}`
- `PATCH /api/admin/issues/{error_hash}` with a bounded status or triage JSON object
- `POST /api/admin/jobs/triage` to run the budgeted triage job immediately and return after it completes
- `POST /api/admin/jobs/repair` to run the budgeted repair job immediately and return after it completes
- `POST /api/admin/jobs/maintenance` to run transient and retention cleanup and return deletion counts

The admin token is never accepted on the public report endpoint and is never embedded in the mod.

The manual AI job endpoints deliberately keep their authenticated HTTP request open until the provider work and D1 state update finish. HTTP `waitUntil()` is not used for these jobs because Cloudflare cancels unfinished post-response work after 30 seconds. Cron executions remain the normal unattended path and have their own longer scheduled-event lifetime.
