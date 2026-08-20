# Prisoner Diplomacy telemetry Worker

This is an independent Cloudflare Worker project. It does not change the RimWorld mod assembly. The current Workshop build intentionally has an empty telemetry endpoint, so no player traffic is sent until a backend is deployed and explicitly configured in a later mod release.

## Current scope

- `POST /api/report-error`: bounded JSON ingestion for schema version 1.
- Server-side validation and redaction of paths and secret-like values.
- SHA-256 event payload digest and event-level idempotency by `event_id`.
- D1 aggregate rows by `error_hash` plus a separate event index.
- R2 JSON objects at `logs/{error_hash}/{timestamp}-{event_id}.json`.
- D1-backed per-IP rate limiting using a salted one-minute bucket. The raw IP is never stored.
- Admin-only pending issue query, issue detail, event detail, and status/triage patch endpoints.
- A daily maintenance Cron that removes expired rate-limit buckets.
- Optional daily Gemini 3.7 Flash triage, guarded by a daily issue budget and confidence/severity gate.
- Optional GPT 5.6 Sol repair-candidate generation through an OpenAI-compatible relay.
- Bounded immediate provider retries plus a persisted 30-minute retry schedule for relay failures.

The AI workflow is deliberately separate from ingestion. The public endpoint never waits for a provider, and AI is disabled by default. A successful repair response is stored as `fix_candidate`; it never edits the repository, runs a build, or marks an issue `resolved` automatically.

## AI workflow configuration

The Worker has two Cron schedules:

- `0 3 * * *`: selects the most frequent untriaged issues and calls the official Gemini API (`TRIAGE_MODEL`, default `gemini-3.7-flash`).
- `*/30 * * * *`: retries queued repair candidates through `REPAIR_AI_ENDPOINT` using `REPAIR_MODEL`, default `gpt-5.6-sol`.

Enable each stage explicitly with `TRIAGE_ENABLED=true` or `REPAIR_ENABLED=true`. The default limits are 20 triage issues/day, 6 repair runs/day, one repair issue per 30-minute run, and a 30-minute retry delay. Immediate transient failures are retried once in the same run; persistent failures remain in D1 and are retried by Cron on later runs without an automatic stop date.

Required secrets when the corresponding stage is enabled:

```text
GEMINI_API_KEY             # official Google Gemini API key
REPAIR_AI_ENDPOINT         # OpenAI-compatible /chat/completions relay URL
REPAIR_AI_API_KEY          # relay credential
```

The repair relay must return an OpenAI-compatible JSON response with `choices[0].message.content` containing an object with `root_cause`, `affected_files`, `patch`, `tests`, and `risks`. Keep the relay credential server-side; it is never sent to the mod.

## Local smoke

```powershell
npm install
npm run generate-types
npx wrangler d1 migrations apply prisoner-diplomacy-telemetry --local
npx wrangler dev --local --port 8790
```

In another terminal, send a schema-valid payload to `http://127.0.0.1:8790/api/report-error`. A retry with the same `event_id` returns `200` with `duplicate: true` and does not increment `occurrence_count`.

```powershell
npm run typecheck
npm test
npm run deploy:dry
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

Production follows the same sequence with `--env production`. Verify `/healthz`, then send one synthetic report before any mod endpoint is enabled.

## Admin API

Admin requests require `Authorization: Bearer <ADMIN_TOKEN>`:

- `GET /api/admin/pending-top?limit=3`
- `GET /api/admin/issues/{error_hash}`
- `GET /api/admin/events/{event_id}`
- `PATCH /api/admin/issues/{error_hash}` with a bounded status or triage JSON object
- `POST /api/admin/jobs/triage` to run the budgeted triage job immediately
- `POST /api/admin/jobs/repair` to run the budgeted repair job immediately

The admin token is never accepted on the public report endpoint and is never embedded in the mod.
