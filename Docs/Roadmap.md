# Post-1.2 Development Roadmap

[English](Roadmap.md) | [繁體中文](Roadmap.zh-TW.md)

Status: observation and community QA. Prisoner Diplomacy 1.2.0 is the current public baseline. This document records candidate work; it is not a promise that every item will ship or that the listed version numbers are fixed.

## 1. Current decision

Let 1.2.0 run in real player environments before changing its save schema, transaction state machine, or public mutation surface.

The initial observation window is 7-14 days from the public release or latest stability hotfix. During this period:

- accept player QA, compatibility reports, telemetry signatures, and localization feedback;
- reproduce and classify defects;
- fix confirmed release-blocking regressions when needed;
- do not start a large feature branch merely because an idea is attractive;
- do not change accepted-deal semantics or API authority boundaries without a reviewed design.

The observation window may be extended when a new hotfix resets the stability baseline.

## 2. What can interrupt the hold

An immediate 1.2.x patch is justified by any confirmed issue involving:

- save corruption, lost Pawns, duplicated or missing rewards, or irreversible deal-state errors;
- a reproducible red error in a normal supported workflow;
- failure to load with the declared dependency and load order;
- a privacy, credential, telemetry-consent, or remote-service isolation defect;
- a widespread UI blocker that prevents accepting, rejecting, releasing, or closing a deal;
- a compatibility regression caused directly by Prisoner Diplomacy;
- a localization or packaging defect that prevents a supported language or Workshop build from loading.

Cosmetic requests, isolated balance preferences, and unverified third-party conflicts remain in triage until they are reproduced or form a clear pattern.

## 3. Evidence to collect

The post-release review should group evidence by signature rather than raw comment count:

| Area | Evidence |
| --- | --- |
| Transactions | Duplicate/missing payment, stale deal, release, exchange, deadline, or reserve signatures |
| Save safety | New-game, existing-save, schema migration, removal, and reload results |
| Events | Neutral handoff, false surrender, trial, ambush, retry, cleanup, and world-object reports |
| UI | Resolution, UI scale, long localization, action reachability, and persistent faction-browser reports |
| Compatibility | Package IDs, load order, other mod version, reproducible steps, and isolated logs |
| Performance | Large faction/prisoner counts, periodic scan cost, UI refresh cost, and log spam |
| API/Add-ons | Registration failures, missing use cases, unsafe workarounds authors feel forced to use |
| AI/Telemetry | Consent, redaction, timeout, stale response, provider isolation, and repair-pipeline outcomes |

Do not use subscriber count alone as a development gate. A small number of precise, reproducible reports is more useful than a large number of impressions.

## 4. Version strategy

### 1.2.1 and later 1.2.x: stabilization only

Allowed scope:

- confirmed bug and compatibility fixes;
- localization, Workshop metadata, documentation, and tutorial corrections;
- diagnostics and reproduction tools;
- performance fixes that preserve results;
- fail-closed validation and clearer errors;
- additive read-only snapshot fields when binary compatibility is preserved.

Avoid in 1.2.x:

- a new save schema solely for optional content;
- batch-deal state, new event executors, or new transaction authority;
- changing accepted terms or rebalancing existing saves without migration policy;
- removing or renaming public API IDs.

Exit condition: no unresolved confirmed P0/P1 regression, no recurring severe telemetry signature, and the final package passes build, localization, static validation, load smoke, and the affected manual workflow.

### 1.3.0: validated public event requests

Primary objective: close the largest API 1.2 gap. External event definitions are currently discoverable metadata but cannot request scheduling through a public contract.

Recommended 1.3.0 scope:

1. Add a capability query, such as `event-request-v1`, so add-ons can detect support without guessing a version string.
2. Add an immutable `PrisonerDiplomacyEventRequest` carrying stable definition ID, faction/prisoner/source-deal identity, requested trigger window, and add-on deduplication key.
3. Add a validated request method that returns a typed result and reason key instead of exposing the internal scheduler.
4. Permit requests only for registered definitions and core-owned event families.
5. Validate ownership, current Pawn/faction/deal context, active-event conflicts, deadlines, duplicate keys, and save-safe references before persisting anything.
6. Return read-only event snapshots and committed lifecycle notifications after the core transition succeeds.
7. Isolate add-on observer exceptions and never roll back or reinterpret an already committed core result.
8. Add registration/capability/conflict diagnostics to the API Inspector.
9. Update the Example Add-on to demonstrate one explicit developer-mode scheduling request without creating ordinary-game event spam.
10. Publish API 1.3 documentation, migration notes, templates, and deterministic smoke coverage.

Not part of the first 1.3.0 contract:

- arbitrary callbacks that mutate Pawns, deals, rewards, faction memory, or core save lists;
- reflection access to `PrisonerDiplomacyGameComponent`;
- an add-on claiming completion before a committed `Completed` snapshot exists;
- unbounded custom result code running inside a core transaction;
- AI deciding whether an event or reward is authoritative.

Phase B, considered only after the validated request API is stable, may add restricted eligibility providers, translated choice metadata, and a small validated outcome-command vocabulary. Fully arbitrary custom events should continue to live in the add-on's own component.

Start gate:

- 1.2 has no unresolved confirmed P0/P1 issue for at least seven consecutive days;
- the recurring post-release error signatures are understood or fixed;
- at least two concrete add-on use cases justify the request fields and lifecycle;
- the API proposal includes save/reload, deduplication, cancellation, retry, ownership, and failure semantics;
- the Example Add-on can exercise the proposed API without private calls.

Acceptance gate:

- duplicate requests cannot create duplicate events or outcomes;
- invalid/stale requests fail without changing game state;
- save/reload preserves ownership and event identity;
- add-on removal or failure does not strand a core transaction;
- observer exceptions are isolated;
- API 1.2 add-ons continue to register;
- smoke and manual event workflows pass with core only and core plus Example Add-on.

### 1.3.x: diplomacy history and agreement center refinement

The current faction browser already exposes agreements, history, and events. The follow-up should deepen that surface instead of creating another competing window:

- unified chronological faction timeline;
- filters by faction, prisoner, deal, agreement, and event type;
- requested, agreed, delivered, failed, and refunded term details;
- ceasefire and intelligence remaining-time history;
- causal goodwill/memory summaries;
- retention and archive policy for long saves;
- copyable diagnostic summary without private text or credentials.

This work should remain read-only and should not change transaction results.

### 1.4.0 candidate: multi-prisoner and batch negotiation

Candidate scope:

- several prisoners in one proposal;
- several prisoners exchanged for one high-value hostage;
- faction-proposed group release terms;
- per-Pawn valuation and a combined budget summary;
- explicit atomic versus partial-completion policy;
- safe failure when one Pawn dies, escapes, transfers, or becomes invalid;
- exactly-once reward and refund behavior across the group;
- save migration and UI accessibility for large lists.

This feature requires a new state model and should not be implemented as a list bolted onto `PrisonerDeal`. Its specification must decide identity, ownership, partial success, release ordering, deadline extension, and rollback before code begins.

### 1.5.0 candidate: Special Reward API 2.0

Candidate scope:

- typed race/faction-specific reward providers;
- multiple fixed alternatives with core-owned selection and validation;
- quality, stuff, gene pack, implant, research-data, or other bounded reward descriptors;
- explicit value calculation and maximum-count contracts;
- missing-mod and removed-Def fallback policy;
- capability discovery and API 1.x compatibility;
- Example Add-on examples for both vanilla and one optional race/faction integration.

The core must still validate value, reserve, material cap, persistence, delivery, and exactly-once behavior. Providers must not spawn or reserve items directly.

### Later candidate: long-term AI diplomatic memory

Possible scope:

- concise summaries of recent negotiations;
- references to prior threats, concessions, broken promises, and humane treatment;
- merge order for core Persona, add-on Persona, and player override;
- visible sanitized Context preview;
- deterministic offline templates and stale-request cancellation.

AI should remain narrative-first. It may return only validated bounded signals; it must not freely set final rewards, execute events, rewrite accepted deals, or become the source of truth for memory.

## 5. Priority order

| Priority | Version/theme | Current action |
| --- | --- | --- |
| P0 | 1.2.x stability and data safety | Observe, reproduce, hotfix only when justified |
| P1 | 1.3 validated event-request API | Design after the stability start gate |
| P1 | 1.3.x history/agreement refinement | Specify as read-only UX work |
| P2 | 1.4 batch negotiation | Research state model and atomicity |
| P2 | 1.5 Reward API 2.0 | Collect real race/faction add-on requirements |
| P3 | Long-term AI diplomatic memory | Keep behind deterministic authority and privacy gates |

## 6. Explicitly deferred

- letting AI freely choose final silver, goodwill, material, ceasefire, or intelligence values;
- public methods that bypass core validation to accept, fulfill, or cancel deals;
- arbitrary add-on mutation callbacks inside core transactions;
- compatibility patches based only on another mod's name rather than reproduced behavior;
- multiplayer guarantees without a separate compatibility project;
- major balance changes during the initial observation window.

## 7. Review cadence

During the observation window, maintain one triage table with:

- signature or issue ID;
- severity and affected version;
- reproducibility status;
- save/log/mod-list evidence;
- owner and next action;
- target version or deferred reason.

At the end of the window, make one explicit decision:

1. release 1.2.1;
2. extend observation because the baseline changed;
3. freeze 1.2 and begin the 1.3 API specification;
4. defer feature development because evidence is insufficient.

This roadmap should be revised when evidence changes. Avoid silently converting a candidate into committed scope.
