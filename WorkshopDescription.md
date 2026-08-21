# Prisoner Diplomacy 1.2.0 Event and Extension Release Candidate

Prisoner Diplomacy turns captured enemies into a long-term diplomatic decision instead of a one-click sale.

After a prisoner is safely held, their faction may contact the colony with a ransom offer. The player can also use a powered comms console to negotiate silver, safe faction supplies, goodwill, hostage exchanges, temporary ceasefires, or one-use early-warning intelligence. Every agreement is deterministic and only becomes complete after the prisoner is released through the marked vanilla release flow and safely leaves the assigned map.

New players can follow the complete five-step walkthrough in the [`Docs/PlayerGuide`](Docs/PlayerGuide/README.md) hub, available in Traditional Chinese, English, Simplified Chinese, Japanese, and Korean.

## Features

- Enemy-initiated ransom offers and player-initiated comms negotiations
- Identity, health, importance, repeated-negotiation, goodwill, and faction-memory valuation
- Silver, technology-tier supplies, goodwill, one-for-one hostage exchange, ceasefire, and intelligence rewards
- Counteroffers with fixed negotiation rounds and `+50`, `+100`, and `+250` silver revisions
- Medical deadline extension for downed prisoners who are actively being treated
- Pirate risks with disclosure, safer terms, delayed payment, rescue, jailbreak, and ambush outcomes
- Strategic follow-up events caused by treatment, release, death, breach, and successful agreements
- Multi-colony delivery-map tracking and conservative repair of stale or duplicated save data
- Optional RimChat coexistence layer and optional AI narrative text with faction persona, ideology, battle-memory, relationship context, and bounded roleplay notes; deterministic gameplay remains authoritative
- Up to three reward types can be combined in one validated deal
- Race adapters can add fixed special item rewards that are priced, validated, persisted, and delivered by the core
- Neutral exchanges create a persistent world-map exchange point. A player Caravan must carry the specified prisoner to the point; arrival validates the event and hands the Pawn to the receiving faction before the core fulfills rewards
- A dedicated event-history tab records event state, stage, retries, source deal, and intermediary
- Optional anonymous error reporting is explicitly consent-gated, never uploads save files, applies client/server redaction, and uses fixed 30/180-day retention

## 1.2 release-candidate settings

The settings page exposes safe balance controls for passive-offer generation, offer frequency, ransom valuation, faction reserves, faction memory, pirate risks, strategic consequences, and message detail. Existing active agreements retain their original state when settings change.

AI narratives are disabled by default. External negotiation-summary transfer requires an explicit opt-in and never creates a payment, Pawn, event, deadline, or other game state. When enabled, the negotiation window can provide a short roleplay note for tone; the note is sanitized, classified, and never directly changes the formal deal. A separate opt-in bounded advisory mode may re-scale a still-unaccepted counteroffer only after deterministic validation.

## Compatibility

RimWorld 1.6 is required. Harmony is required. RimChat, Humanoid Alien Races, Hospitality, Combat Extended, Vanilla Factions Expanded, and custom race/faction mods are optional. Prisoner Realism 1.6 has been loaded with Prisoner Diplomacy 1.2.0 in both mod orders; it owns prison-break behavior while Prisoner Diplomacy owns diplomatic triggers and deal outcomes. See [`Compatibility.md`](Compatibility.md) and [`KnownIssues.md`](KnownIssues.md) for the current matrix, the Prisoner Realism overlap details, and limitations.

## Save safety

Version 1.2 uses save schema 17 and migrates schema 16 and older data to it. The migration is conservative: it repairs missing links, preserves transaction outcomes, and initializes staged-event and special-reward fields without changing vanilla Pawn ownership or inventing a missing transaction result. Back up important saves before installing any mod update.

The vanilla comms console remains the full negotiation entry point. Incoming letters still work without a console, active agreements remain reviewable through a persistent alert, and the optional portable diplomacy terminal opens known faction contacts from a selected colonist's equipment gizmo.

The built-in supply catalog expands by faction technology: medicine, meals, kibble, components, chemfuel, steel, cloth, plasteel, uranium, synthread, and hyperweave. Compatible race or faction add-ons can also register their own item `ThingDef` as a special reward through the public adapter API. The core validates the item and delivers it exactly once; add-ons never mutate the transaction state directly.

## AI and RimChat boundary

The vanilla comms console and Prisoner Diplomacy deterministic state machine are the only authorities for deals and rewards. RimChat and AI are optional presentation or integration layers; AI advisory output is merely a bounded input to the core's counteroffer path. Removing either does not remove or complete an existing Prisoner Diplomacy deal.

## Feedback

When reporting an issue, enable developer mode and use `Prisoner Diplomacy -> Copy diagnostic report`. Include the copied report, the save schema, the exact deal state, the active mod list, and the relevant `Player.log` excerpt. Never include API Keys. Error telemetry is optional and asks before first sending; persistent consent can be revoked in the mod settings, and declining it does not affect gameplay. Production AI analysis is enabled: Google Gemini performs triage and only qualifying internal defects go through the third-party AI-HUB relay for a GPT 5.6 Sol repair candidate. Provider failures never affect gameplay. See `Docs/TelemetryPrivacy.md` for exact fields, redaction limits, providers, processing and retention.

## Open source and community

The project is open source on [GitHub](https://github.com/as5611198/PrisonerDiplomacy). Extension authors can read `PrisonerDiplomacyApi.md`, `Docs/RewardCatalog.md`, and `Docs/AddonQuickstart.md` to build race adapters, special rewards, persona providers, and event metadata.

QQ discussion group: [戰俘外交（Prisoner Diplomacy）模組討論群](https://qun.qq.com/universal-share/share?ac=1&authKey=kO4hgI4yAGKZaIMkDgtwdF7V9G9aylRatK8pqb&busi_data=eyJncm91cENvZGUiOiIyMTE3ODQ2ODgiLCJ0b2tlbiI6InJMNDZ0VDd2RnhHSjhBbE51dVhQOUR6NTNhMlR4cjdhQUVNcmVlUzQybGJTMEg4MHd2ZGlxT1JLWXBYdDVNQXMiLCJ1aW4iOiIzODMxMDIzMDUwIn0%3D&data=ii_Z7GGfk0K0tX3nuOIWOG9w0Vt8TpomZx82ytn1-cooF1oRHAXYR8Nss77V5VBQER3K33djQUT_bNS6Lt1UXg&svctype=4&tempid=h5_group_info) (群號：`211784688`).

## Authorship

本模組 100% 由 Codex（GPT-5.6 SOL）製作；本人只提供想法。

This mod was 100% produced by Codex (GPT-5.6 SOL); the project owner provided the ideas only.

This document is prepared for the 1.2 Steam Workshop release candidate. Manual gameplay and world-map Caravan testing remain separate author acceptance checks.
