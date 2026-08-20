# Prisoner Diplomacy

RimWorld 1.6 mod implementing Prisoner Diplomacy 1.2.0: ransom, exchange, strategic ceasefire and intelligence, causal follow-up events, pirate-risk, faction-memory, dynamic deadlines, a versioned extension API, staged neutral handoffs, core-fulfilled special rewards, infiltration, public trials, RimChat coexistence, and optional AI narratives. Steam Workshop publication remains intentionally deferred until the 1.2 event and reward pass receives manual gameplay validation.

## 1.2 formal release candidate (offline)

- Delivers the complete deterministic transaction loop: enemy proposals, comms-console player demands, acceptance, rejection, counteroffers, marked vanilla release, safe map exit, and exactly-once fulfillment.
- Supports up to three reward types in one deal: silver, faction supplies, goodwill, ceasefire, or one-use early-warning intelligence, subject to legality, budget, reserve, and material-cap checks.
- Expands the built-in faction-supply catalog by technology tier: medicine, meals, kibble, components, chemfuel, steel, cloth, plasteel, uranium, synthread, and hyperweave. Optional missing definitions are skipped safely.
- Documents the core reward catalog and a standalone add-on quickstart for race/faction adapters and special item rewards. The public repository includes the project authorship statement, QQ community link, and open-source contribution guidance.
- Supports one-for-one kidnapped-colonist exchanges with optional player compensation, without duplicating or losing either Pawn.
- Keeps hidden `Reliability`, `Treatment`, and `Resentment` memory causal and readable through messages without exposing raw values.
- Distinguishes diplomatic factions, transactional pirate factions, and non-negotiating factions, with disclosed pirate risk and deterministic mitigation.
- Preserves multi-colony delivery-map identity, save/load repair, duplicate-payment guards, and safe removal from existing saves.
- Keeps vanilla comms as the complete entry point. RimChat and AI remain optional, cancellable, privacy-gated layers; the opt-in AI advisory can only feed bounded counteroffer guidance back through the deterministic core and never becomes a transaction authority.
- Uses save schema 17 so schema 16 and older saves migrate safely; staged-event and special-reward fields use conservative defaults on older saves.
- This checkout is built and deployed locally for testing only. No Steam Workshop upload or publication is performed.
- Neutral world handoffs create a persistent WorldObject and use the vanilla Caravan pathing and arrival-action flow. The target prisoner must be present in the arriving Caravan; invalid or stale arrivals are rejected without changing the deal.
- Developer mode includes force-world-point, list-world-points, selected-point completion, event advance/cancel, and event-state logging actions for repeatable manual testing.
- The world trade-point inspector no longer emits an empty-line error, and the debug caravan handoff is fenced from the normal prisoner-escape detector. Public trials now apply a vanilla goodwill penalty to the executed faction, while ambush letters retain the prisoner and source-deal labels.
- Neutral world-trade arrival now avoids duplicate `WorldPawns` registration, suppresses the same-faction debug warning, and destroys an empty debug caravan after the prisoner handoff. Multi-Pawn player caravans remain intact so colonists can return normally.
- AI Persona context now supports a bounded global fallback, per-faction settings overrides, and optional race/faction `IPrisonerDiplomacyPersonaProvider` registrations. Persona text is sanitized and remains narrative-only context.
- Added an opt-in bounded AI negotiation advisory path. Strict JSON may return only categorical urgency, concession, and leverage signals; the deterministic core can re-scale an unaccepted counteroffer within reserve, inventory, reward, and stale-context validation. Accepted or completed deals are never rewritten, and the option defaults to false.
- Added a developer action to log and copy the final sanitized AI context preview for a selected Pawn.
- Added deterministic debug actions for urgent/threatened and conciliatory advisory signals, so the numeric adjustment path can be tested without an external AI request.

### Custom negotiation UI and extension contract

The negotiation window now uses a compact two-column IMGUI dashboard with a faction header, case list, scrollable negotiation workspace, fixed action bar, and optional AI narrative panel. The stable extension surface is deliberately separate from the window implementation. External integrations should read the UI-independent snapshots in `PrisonerDiplomacyBackendApi` rather than inspecting or mutating save records directly:

- `GetPrisonerSnapshots(map)` and `GetFactionSnapshots(map)` expose only eligible, map-local negotiation data.
- `TryGetActiveDealSnapshot(prisoner)` exposes the current deal, deadlines, reward description, pirate risk, and release availability.
- `PreviewDemand(prisoner, negotiator, demand, out reasonKey)` validates and evaluates a demand without creating a deal or changing game state.
- `GetEventSnapshots()` exposes persisted event state, stages, retries, source deals, and intermediary labels.
- Race adapters can expose fixed item rewards; the core validates, prices, saves, and fulfills them without giving add-ons a direct mutation callback.

The deterministic `PrisonerDiplomacyGameComponent` remains the only authority for submitting demands, accepting or rejecting offers, ordering release, paying rewards, and changing faction memory. The snapshot objects are read-only and are safe to refresh when a custom window opens or a negotiation round changes.

`PrisonerDiplomacyUiExtensionRegistry` accepts ordered `IPrisonerDiplomacyUiExtension` implementations for faction headers, prisoner summaries, and negotiation-body regions. Extensions receive a read-only `PrisonerDiplomacyUiContext`; exceptions are isolated and cannot mutate transaction state. The current implementation stays on Verse `Window`/Unity IMGUI for input, scaling, lifecycle, and mod compatibility. A theme layer (`PrisonerDiplomacyUiTheme`) centralizes colors, spacing, panels, badges, metrics, progress bars, and signal animation so future visual changes do not require rewriting transaction controls. `ReduceUiMotion` disables the lightweight AI signal pulse and narrative fade without changing gameplay.

### Developer-mode test workflow

Enable developer mode, open the debug actions menu, and use the `Prisoner Diplomacy` category. The new actions are intended for disposable test saves:

- `Spawn test prisoner near selected Pawn` creates an eligible player prisoner without combat setup; `Force ransom offer` then creates the faction proposal.
- `Reset selected Pawn negotiation cooldowns` allows repeated negotiation attempts. The fixed silver actions submit 100, 250, 500, 1000, or 2000 silver without editing the normal window.
- `Force selected Pawn into a counteroffer` finds a deterministic countered demand. Use `Revise counteroffer +50/+100/+250 silver`, then accept or reject it.
- `Generate test kidnapped colonist for selected Pawn` populates the selected faction's hostage list for exchange testing. The normal exchange controls still decide the actual deal.
- `Spawn custom-race test prisoner` and `Generate custom-race kidnapped colonist` build their submenus automatically from every loaded non-Human humanlike race and PawnKind. No per-race compatibility code is required; entries only exist for races provided by currently loaded mods. Prisoner spawning requires a matching active negotiating faction, while kidnapped colonists use the selected prisoner's original faction as captor and remain members of the player faction.
- `Log detected custom races` writes each detected mod, package ID, Race Def, preferred PawnKind, candidate PawnKinds, and matching active faction to the game log for troubleshooting.
- `Order selected ransom release only` creates the release-ordered save point without leaving the map. `Mark selected deal delivered, payment pending` creates the post-release/pre-payment save point. `Issue only the next pending reward` creates a partial-reward save point, and `Complete selected ransom delivery` finishes the remaining rewards.
- `Make selected pirate payment due now` resolves a delayed payment immediately. `Empty selected faction reserve` tests reserve exhaustion and counteroffer rejection.
- `Trigger eligible test raid` uses the selected prisoner's original faction only when vanilla considers it hostile; active ceasefires are checked before vanilla can substitute another faction. Friendly or support factions are reported as ineligible instead of spawning an unrelated faction's raid.
- `Add tendable injury and record player treatment` prepares the medical deadline-extension path. `Cancel selected accepted deal` tests the no-payment cancellation path. `Simulate selected prisoner sale or transfer` and `Simulate selected prisoner enslavement` exercise the corresponding failure paths without changing vanilla ownership. Existing actions cover death, escape, recruitment, expiry, memory, ceasefire, intelligence, raids, compatibility repair, and the RimChat guard.
- The `Event:` debug actions force neutral exchange, false surrender, public trial, and ransom ambush paths; they can also advance, cancel, and log persisted event stages. `Event: force false surrender warning now` and `Event: force false surrender jailbreak now` select each infiltration branch deterministically. `Rewards: toggle debug special reward` exposes a three-component adapter reward for end-to-end UI and fulfillment tests.

The actions operate only in developer mode and change the current save by design. Use a copy or a disposable test save when testing intermediate states.

## 0.8 compatibility hardening

- Tracks the delivery map with a stable map ID and updates it when a prisoner is legally transferred between player maps.
- Repairs stale references, duplicate records, duplicate active deals, invalid rewards, invalid faction-memory entries, removed Def references, and sequence collisions while loading a save.
- Keeps a single broken Pawn, Deal, faction-memory entry, or strategic follow-up from aborting the entire periodic scan.
- Supports optional `PrisonerDiplomacyPawnCompatibilityExtension` and `PrisonerDiplomacyFactionCompatibilityExtension` DefModExtensions so race and faction mods can explicitly exclude temporary or non-diplomatic content without a hard dependency.
- Exposes compatibility logging and save-repair actions under the developer-mode `Prisoner Diplomacy` category. Slow-scan logging is opt-in in the mod settings.
- Automatically logs a compact report of active priority mods, maps, spawned prisoners, records, deals, and RimChat status after starting or loading a game.

The repair pass is conservative: it never edits vanilla Pawn ownership, removes a Pawn from a faction, or invents a missing transaction result. An unrecoverable transaction is cancelled safely and left in the mod's retained history. Removing the mod after a save remains safe because all persistent state is owned by this mod's GameComponent; the core game Pawn and faction data are never modified by the repair pass.

## 0.9 public Beta and release preparation

- Adds player-facing controls for enemy-initiated offer generation, offer frequency, ransom valuation, faction reserves, faction memory, and message detail without exposing internal formulas.
- Keeps existing accepted or active deals authoritative when a setting is changed. Disabling new enemy offers only blocks future passive offers.
- Adds an explicit privacy opt-in before AI narrative summaries can be sent to an external service. AI remains optional, disabled by default, and never controls gameplay state.
- Adds deterministic faction persona archetypes, ideology signals, recent battle details, faction-leader relationship context, historical grievances, and a bounded roleplay note whose emotion tag affects tone only.
- Adds developer actions for exact Pawn valuation, accepting or rejecting a selected deal, forcing expiry, simulating death/escape/recruitment failure, adjusting memory, refilling a reserve, and copying a sanitized diagnostic report.
- Diagnostic reports include mod version, save schema, active deal counts, maps, RimChat status, priority compatibility mods, and sanitized settings. API Keys are never included.
- Migrates schema 16 and older saves to schema 17 without changing existing transaction outcomes. New staged-event and special-reward fields use conservative defaults and are stored in the normal save data.
- Expands deterministic smoke coverage to `PASS cases=127`, including physical special-reward delivery, duplicate-delivery protection, refusal cooldowns, permanent transaction history, persistent comms access, diagnostic privacy, the offline error-telemetry contract, and the versioned extension registry/event snapshot contract.
- Adds opt-in, PrisonerDiplomacy-only error telemetry with explicit transaction sentinels, sanitized snapshots, bounded session deduplication, and asynchronous finite-retry upload. The Workshop build remains offline until the Cloudflare receiver endpoint is configured; unrelated global RimWorld log errors are not intercepted.
- Keeps the 0.6.5 AI provider implementation and RimChat isolation contract unchanged apart from the explicit external-context consent gate.

Release documents are in `WorkshopDescription.md`, `FAQ.md`, `Compatibility.md`, and `KnownIssues.md`.

## 0.1 scope

- Registers player-held humanlike prisoners and preserves their originating faction.
- Calculates a simplified diplomatic value and fixed silver offer.
- Creates faction-initiated accept/reject ransom letters.
- Adds a dedicated **Release for ransom** command after acceptance.
- Uses the vanilla prisoner release work flow.
- Pays by drop pod only after the marked prisoner completes vanilla release and exits the assigned map.
- Cancels without payment on death, escape, recruitment, enslavement, sale/transfer, invalid faction, or expiry.
- Persists records, deals, delivery state, and the reward-issued guard in the save.
- Includes developer-mode actions for offer creation, state inspection, and delivery simulation.

## 0.2 scope

- Adds player-initiated ransom demands through a powered comms console.
- Uses the selected pawn as the negotiator and applies their Social skill to acceptance chance and preview precision.
- Keeps negotiation outcomes deterministic for the prisoner, negotiator, and demanded amount.
- Applies prisoner and faction cooldowns only after a demand is formally submitted.
- Adds a prisoner diplomatic-status command, including a clear warning when a downed prisoner cannot yet be escorted out.
- Accepted demands use the same safe release, delivery verification, drop-pod payment, and duplicate-payment protection as faction offers.

## 0.2.2 changes

- Schedules the prisoner's faction to make contact after a deterministic 3-7 day delay when no ransom deal is active.
- Biases important prisoners toward earlier faction contact while keeping every delay inside the same 3-7 day window.
- Reschedules passive contact after a player demand so an overdue faction offer cannot appear immediately after negotiation.
- Shows a one-time notice explaining passive faction contact and immediate comms-console negotiation.
- Shows the estimated faction-contact time in the prisoner diplomatic-status window.
- Migrates existing saves by assigning deterministic contact times to records created before 0.2.2.

## 0.3 scope

- Adds persistent faction counteroffers through the comms console.
- Limits each negotiation to two player offers followed by acceptance or rejection of the final counteroffer.
- Adds silver, safe faction-appropriate supplies, and vanilla goodwill rewards.
- Allows up to two reward types in a mixed demand.
- Applies the negotiator's Social skill to the negotiation budget, response, and preview precision.
- Gives each faction a diplomatic reserve that is consumed by completed deals and recovers over roughly 45 days.
- Reserves funding for active accepted deals so a faction cannot promise the same resources multiple times.
- Applies a colony-wealth material reward limit to silver and supplies.
- Tracks repeated absurd demands and temporarily suspends negotiations when faction patience is exhausted.
- Shows qualitative faction finances and a compact deal-history summary in the negotiation window.
- Migrates 0.2.x silver deals into the new multi-reward data format.

## 0.3.1 changes

- Expands the range of demands that receive a faction counteroffer instead of an immediate rejection.
- Converts demands above the colony material limit into capped counteroffers when negotiation is otherwise possible.
- Starts revised terms from the faction's current offer and adds exact `+50`, `+100`, and `+250` silver shortcuts.

## 0.4 scope

- Reads colonists still held by the selected faction from RimWorld's kidnapped-pawn tracker.
- Adds one-for-one exchanges between one held enemy prisoner and one kidnapped colonist.
- Calculates the returned colonist at 125% of diplomatic value and charges only the remaining silver difference.
- Holds silver compensation when the exchange is accepted, refunds it when the exchange fails before delivery, and never charges it twice.
- Returns the colonist by tracked drop pod only after the enemy prisoner safely leaves through the dedicated exchange command.
- Persists the hostage reference, compensation, delivery, and return guards across saves.
- Makes the negotiation detail pane scroll independently so faction information, counteroffers, reward fields, and exchange controls cannot overlap.

## 0.4.1 changes

- Keeps the comms negotiation window open when a faction returns a counteroffer, including the final counteroffer.
- Makes the final counteroffer use the faction's full affordable negotiation budget instead of repeating the lower opening counteroffer.

## 0.5 scope

- Adds persistent hidden `Reliability`, `Treatment`, and `Resentment` memory for each negotiating faction.
- Records meaningful captivity events including recovery from critical condition, prolonged starvation, player-caused permanent harm, body-part removal, release, death, recruitment, enslavement, sale, escape, and agreement outcomes.
- Makes faction memory affect future negotiation budgets, player-demand responses, and faction-initiated offers.
- Adds qualitative prisoner-diplomacy descriptions and the latest meaningful cause to the faction page without exposing exact values.
- Announces major changes with their cause while keeping small changes in the faction's recent event history.
- Decays memories over time while preserving a lasting resentment floor for player-caused deaths of leaders and core members.
- Gives unconditional vanilla release positive treatment and reliability effects and reduces resentment, with stronger effects after critical recovery or for important prisoners.
- Migrates existing saves neutrally without inventing past treatment events.

## 0.5.1 changes

- Keeps the base three-day fulfillment deadline for accepted agreements.
- Automatically extends the deadline by two days when it expires while the prisoner is downed or has player-tendable injuries.
- Requires a player-faction doctor to have actually completed medical treatment after the agreement was accepted.
- Requires new treatment before each later extension, preventing an unattended prisoner from receiving unlimited time.
- Persists extension counts and treatment checkpoints and migrates 0.5.0 saves safely.

## 0.5.2 changes

- Calculates the negotiation detail scroll area from the actual available width and translated text height.
- Keeps revised-demand and exchange buttons fully reachable below long counteroffer and assessment text.
- Adds bottom padding and dynamic heights for counteroffer hints, final-offer notices, and unavailable reasons.

## 0.6 changes

- Classifies factions as `NonNegotiating`, `Transactional`, or `Diplomatic`.
- Excludes non-negotiating factions from prisoner records and comms negotiation.
- Lets permanently hostile transactional factions negotiate without changing ordinary goodwill.
- Applies pirate willingness by prisoner importance so ordinary members receive lower offers while leaders and core figures remain valuable.
- Allows pirate faction offers to use safe faction-appropriate supplies as an alternative to silver.
- Discloses a deterministic delayed-payment risk before a risky pirate release and provides a cancellation option.
- Adds persistent faction-type overrides and settings for permanent-enemy negotiation and pirate risks.

## 0.6.1 changes

- Detects the active `yancy.rimchat` package and displays its version and compatibility status.
- Adds a prisoner-ransom system owner setting: Prisoner Diplomacy, RimChat, or safe isolation.
- Keeps the vanilla comms console as the complete Prisoner Diplomacy entry point.
- Blocks overlapping RimChat `pay_prisoner_ransom` Actions when a verified executor signature is available, without referencing RimChat at compile time.
- Stops new Prisoner Diplomacy deals when RimChat is selected as owner, while preserving existing deals so they can finish safely.
- Uses safe isolation for unknown or changed RimChat versions and shows a one-time warning; no external deal state is guessed.
- Bumps the save schema to version 10 with a persisted one-time compatibility-warning flag.

## 0.6.5 changes

- Adds an experimental AI narrative layer that is disabled by default and has no RimChat dependency.
- Ports the provider-oriented API setup from RimWorld Auto AI Translation Core without creating a runtime dependency on that mod.
- Supports OpenAI, Google Gemini, DeepSeek, Grok, GLM, Alibaba DashScope, OpenRouter, and custom OpenAI-compatible services.
- Provides provider presets, custom Base URLs, model-list fetching, manual model entry, connection testing, masked API keys, and request timeouts.
- Migrates the original 0.6.5 Endpoint, model, API Key, and keyless-local-service settings automatically.
- Keeps every acceptance, rejection, counteroffer, reward, payment, deadline, seed, Pawn change, and Deal transition in deterministic game code.
- Sends only the current faction, ideology signals, prisoner identity and health category, recent battle and relationship context, qualitative faction memory, an optional bounded roleplay note, and the already-decided formal result.
- Binds every response to a request ID, context ID, and candidate version, then validates the current Pawn, faction, Deal state, and negotiation round on the main thread.
- Cancels superseded or closed-window counteroffer requests and rejects stale responses after context changes.
- Persists adopted narrative text in save version 11; pending requests fall back after load and are never regenerated automatically.
- Falls back to standard translated templates on disabled AI, missing configuration, timeouts, network failures, invalid JSON, missing fields, oversized output, or stale context.
- Blocks non-local plaintext HTTP endpoints and never logs API keys or full response bodies.
- Clearly marks generated text as narrative only and keeps the formal game terms visible beside it.

## 0.7 changes

- Adds 5-30 day temporary ceasefires using the planned nonlinear diplomatic-value cost formula.
- Blocks only new, controlled proactive raids from the agreeing faction; forced and quest-linked raids remain explicitly exempt.
- Ends a ceasefire and applies a severe reliability and resentment penalty when player forces attack that faction.
- Adds 60-day, single-use early-warning intelligence that delays the next eligible raid by 6-12 hours and reports a qualitative threat band, likely attack style, and direction when available.
- Persists strategic agreements, delayed warned raids, and causal follow-up events in save version 12.
- Adds important-prisoner rescue operations, important-death warnings and retaliation raids, and later recovery acknowledgements for critically injured prisoners who recovered under player care. Ordinary factions may send a delayed silver gift; permanently hostile transactional factions instead grant a one-time care credit for a 10% next-negotiation budget concession. Rescue is a targeted extraction with an escort from the original faction; it does not use the normal raid looting or building-destruction behavior.
- Expands disclosed pirate risks to delayed payment, armed rescue, jailbreak incitement, and post-delivery ambushes.
- Keeps completed rewards intact during pirate ambushes and provides pre-release cancellation or lower-reward safer terms as countermeasures.
- Adds developer actions for a 10-day ceasefire, early-warning intelligence, an eligible test raid, and an immediate recovery-return test that grants either a physical gift or permanent-enemy care credit as appropriate.
- Keeps the AI narrative layer optional and presentation-only while deterministic gameplay remains authoritative; provider requests remain cancellable, bound to the current negotiation context, and safe to fall back.

## 1.2 AI narrative completion

- Differentiates imperial nobility, brutal pirate factions, ancestral tribes, diplomatic factions, and ideology-driven groups through deterministic persona context.
- Injects up to three recent prisoner battle details, direct relationship to the faction leader, and recent faction-memory grievances into the narrative prompt.
- Adds an optional roleplay note field in the negotiation window. The note is normalized to a short bounded string and classified as neutral, respectful, conciliatory, urgent, or threatening. It can shape the faction's emotional wording but never changes reward formulas, deal state, deadlines, or game effects.

## Build

The project expects RimWorld at `E:\SteamLibrary\steamapps\common\RimWorld`. Override it when needed:

```powershell
dotnet build -c Release -p:RimWorldDir="D:\Games\RimWorld"
```

The DLL is written directly to `1.6\Assemblies`.

## Smoke test

The assembly includes a command-line-only smoke test. It does not run during normal play.

```powershell
& 'E:\SteamLibrary\steamapps\common\RimWorld\RimWorldWin64.exe' `
  -savedatafolder=C:\CodexPDTest `
  -logFile C:\CodexPDTest\SmokeTest.log `
  -quicktest `
  -pdsmoketest `
  -popupwindow
```

A passing run writes `Prisoner Diplomacy SmokeTest] PASS cases=127` after validating physical silver, supply, and adapter-defined special-reward delivery; goodwill and mixed rewards; ceasefire and intelligence; exactly-once guards; refusal cooldowns; permanent transaction and event history contracts; persistent comms access; proactive-raid suppression; causal follow-ups; pirate risks; counteroffers; exchanges; medical deadlines; memory and migration; faction behavior; AI guards; save repair; diagnostic privacy; the offline error-telemetry contract; RimChat isolation; and the versioned extension API. The smoke test never performs an external AI request.

For an existing save, start with the same `-savedatafolder` and use `-pdloadtest=SaveName`. The mod logs the loaded schema, record/deal counts, active deal state, compatibility report, and any repair summary. The load test does not invent a transaction result: missing or unresolvable transaction data is retained or cancelled safely.
