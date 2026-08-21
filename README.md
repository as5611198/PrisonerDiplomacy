# Prisoner Diplomacy

Prisoner Diplomacy is a RimWorld 1.6 mod for deterministic prisoner ransom, exchange, and faction diplomacy. It adds a complete negotiation workflow while keeping vanilla comms, prisoner release, caravan movement, and the mod's `PrisonerDiplomacyGameComponent` authoritative.

**Current version:** `1.2.0`  
**Save schema:** `17`  
**Status:** release-ready candidate. The author accepts remaining edge-case risk for post-release community QA; automated build, localization, smoke, and telemetry gates remain mandatory.

## What it does

- Player and faction-initiated negotiations through the vanilla comms console.
- Silver, technology-tier faction supplies (medicine, meals, components, fuel, steel, textiles, and advanced materials), goodwill, ceasefire, intelligence, and mixed rewards with budget, reserve, material-cap, and exactly-once fulfillment checks.
- Counteroffers with revised terms, exchange of one captured prisoner for one kidnapped colonist, and compensation/refund handling.
- Persistent faction memory for reliability, treatment, resentment, historical grievances, and relationship context.
- Pirate deal risk, delayed payment, armed rescue, jailbreak pressure, and retaliation consequences.
- Strategic ceasefires and one-use early-warning intelligence that affect only eligible proactive raids.
- Staged follow-up events: neutral world-map trade points, false surrender and infiltration, public trials, rescue, and ransom ambush retaliation.
- Save migration and conservative compatibility repair across older save schemas.
- Optional AI narrative replies. AI is asynchronous, cancellable, and privacy-gated. Narrative output is presentation-only by default; an explicit opt-in can let it return bounded categorical advisory signals for live counteroffers, which the deterministic core clamps and re-validates.
- Optional RimChat coexistence with safe owner selection and isolation guards.
- Opt-in, PrisonerDiplomacy-only anonymous error reports with a bounded queue, explicit per-report/session consent, and fixed 30/180-day retention.
- A compact themed negotiation UI, persistent faction browser, agreement/history/event tabs, and developer-mode diagnostics.
- A versioned extension API for race adapters, special item rewards, event definitions, read-only snapshots, and community add-ons.

## Installation

1. Build or obtain the `1.6` mod folder.
2. Copy the folder into `RimWorld/Mods/PrisonerDiplomacy`.
3. Enable **Harmony** before Prisoner Diplomacy in the RimWorld mod list.
4. Optional integrations are detected at runtime and do not become hard dependencies.

The repository's build output targets `1.6/Assemblies`. The checked-in XML definitions and language files are the runtime content for RimWorld 1.6.

## Player guide

New players should start at [`Docs/PlayerGuide/README.md`](Docs/PlayerGuide/README.md). The complete guide is maintained as separate pages for [Traditional Chinese](Docs/PlayerGuide/PlayerGuide.zh-TW.md), [English](Docs/PlayerGuide/PlayerGuide.en.md), [Simplified Chinese](Docs/PlayerGuide/PlayerGuide.zh-CN.md), [Japanese](Docs/PlayerGuide/PlayerGuide.ja.md), and [Korean](Docs/PlayerGuide/PlayerGuide.ko.md). It covers entry points, eligible prisoners, reward limits, counteroffers, the required release workflow, agreements, neutral exchange points, optional AI, and diagnostics.

## Localization

The mod ships complete keyed localization for English, Traditional Chinese, Simplified Chinese, Japanese, and Korean. All language files contain the same 573 keys, including negotiation UI, reward-value hints, debug tools, AI advisory text, strategic agreements, extension events, and telemetry consent text. Run `pwsh -NoProfile -ExecutionPolicy Bypass -File .\Tools\ValidateLocalization.ps1` after changing a keyed file. `Tools/GenerateLocalization.ps1` can regenerate the three additional translations from the English source when needed.

## Player entry points

The vanilla comms console is the complete entry point. A player can negotiate immediately when an eligible prisoner and negotiator are available; no additional console or AI service is required. Faction letters remain available for unsolicited offers and event choices. Developer mode exposes repeatable actions under the **Prisoner Diplomacy** category for creating prisoners, offers, counteroffers, exchanges, event stages, world trade points, rewards, raid consequences, save-repair cases, and diagnostic reports.

AI narratives and RimChat integrations are optional presentation or ownership layers. The opt-in AI advisory can only adjust an unaccepted counteroffer inside deterministic reserve, inventory, reward, and stale-context limits; neither integration can bypass the transaction state machine.

## Build

The default RimWorld path is configured in `Directory.Build.props`. Override it for another installation:

```powershell
dotnet build .\PrisonerDiplomacy.csproj -c Release -t:Rebuild --nologo
dotnet build .\PrisonerDiplomacy.csproj -c Release -p:RimWorldDir="D:\Games\RimWorld"
```

The project targets `net48`, C# 7.3, and includes `*.cs` recursively, so source folders are organizational boundaries rather than project-file lists.

## Smoke test

The assembly contains an isolated command-line smoke test. It does not send external AI requests and does not replace manual UI testing:

```powershell
& 'E:\SteamLibrary\steamapps\common\RimWorld\RimWorldWin64.exe' `
  -savedatafolder=C:\CodexPDTest `
  -logFile C:\CodexPDTest\SmokeTest.log `
  -quicktest `
  -pdsmoketest `
  -popupwindow
```

A healthy run ends with `Prisoner Diplomacy SmokeTest] PASS cases=127`. The smoke suite covers reward delivery, exchanges, ceasefire/intelligence, pirate risk, counteroffers, event persistence, neutral trade points, migration, diagnostics, AI guards, RimChat isolation, API registration, and the offline-safe error-telemetry contract. Manual checks are still required for visual layout, translated long labels, UI scaling, button reachability, consent dialog layout, and real caravan/event playthroughs.

Use [`Docs/ReleaseChecklist.md`](Docs/ReleaseChecklist.md) as the upload gate. It keeps automated package evidence separate from the author's final visual and gameplay acceptance pass.

## Source organization

See [`Source/README.md`](Source/README.md) for the folder map and authoritative transaction flow. The short architecture guide is [`Docs/Architecture.md`](Docs/Architecture.md).

| Area | Location |
| --- | --- |
| Core deals, rewards, records, economy | [`Source/PrisonerDiplomacy/Core`](Source/PrisonerDiplomacy/Core) |
| Public API and sample add-on | [`Source/PrisonerDiplomacy/Api`](Source/PrisonerDiplomacy/Api) |
| Events and neutral world-map exchange | [`Source/PrisonerDiplomacy/Events`](Source/PrisonerDiplomacy/Events) |
| Strategic consequences and raids | [`Source/PrisonerDiplomacy/Strategic`](Source/PrisonerDiplomacy/Strategic) |
| UI and theme | [`Source/PrisonerDiplomacy/UI`](Source/PrisonerDiplomacy/UI) |
| Optional AI and RimChat | [`Source/PrisonerDiplomacy/AI`](Source/PrisonerDiplomacy/AI), [`Source/PrisonerDiplomacy/Integration`](Source/PrisonerDiplomacy/Integration) |
| Debug and compatibility tooling | [`Source/PrisonerDiplomacy/Debug`](Source/PrisonerDiplomacy/Debug), [`Source/PrisonerDiplomacy/Compatibility`](Source/PrisonerDiplomacy/Compatibility) |

## Modder documentation

[`PrisonerDiplomacyApi.md`](PrisonerDiplomacyApi.md) is the public v1.2 guide. It documents registration, version checks, read-only snapshots, race and special-reward adapters, persona providers, bounded AI advisory signals, deterministic validation, and event add-ons. [`Compatibility.md`](Compatibility.md) is the player-facing compatibility report: it mirrors the current Prisoner Realism integration list, records the verified Prisoner Realism load-order checks, and explains optional DefModExtensions and integration boundaries. [`Docs/TelemetryPrivacy.md`](Docs/TelemetryPrivacy.md) documents optional error-report contents, Google Gemini and AI-HUB processing, and retention. The local telemetry repair verifier creates isolated, tested review candidates and never marks an issue resolved automatically. [`FAQ.md`](FAQ.md), [`KnownIssues.md`](KnownIssues.md), and [`WorkshopDescription.md`](WorkshopDescription.md) contain player-facing release material.

The reward catalog is documented in [`Docs/RewardCatalog.md`](Docs/RewardCatalog.md). New authors can start with the step-by-step [`Docs/AddonQuickstart.md`](Docs/AddonQuickstart.md), then use sections 10-14 of the API guide for the complete adapter, event, persona, and compatibility contract. The project is open source on [GitHub](https://github.com/as5611198/PrisonerDiplomacy); the Steam Workshop release will link back to this canonical repository.

The API is intentionally fail-closed: duplicate IDs, incompatible major versions, adapter exceptions, invalid ThingDefs, and stale event context do not get to mutate gameplay state. Add-ons should use public contracts only and must not reflect into save lists or call internal GameComponent methods.

## Community

QQ discussion group: [戰俘外交（Prisoner Diplomacy）模組討論群](https://qun.qq.com/universal-share/share?ac=1&authKey=kO4hgI4yAGKZaIMkDgtwdF7V9G9aylRatK8pqb&busi_data=eyJncm91cENvZGUiOiIyMTE3ODQ2ODgiLCJ0b2tlbiI6InJMNDZ0VDd2RnhHSjhBbE51dVhQOUR6NTNhMlR4cjdhQUVNcmVlUzQybGJTMEg4MHd2ZGlxT1JLWXBYdDVNQXMiLCJ1aW4iOiIzODMxMDIzMDUwIn0%3D&data=ii_Z7GGfk0K0tX3nuOIWOG9w0Vt8TpomZx82ytn1-cooF1oRHAXYR8Nss77V5VBQER3K33djQUT_bNS6Lt1UXg&svctype=4&tempid=h5_group_info) (群號：`211784688`).

## Authorship

本模組 100% 由 Codex（GPT-5.6 SOL）製作；本人只提供想法。

This mod was 100% produced by Codex (GPT-5.6 SOL); the project owner provided the ideas only.

## License

| Material | Terms |
| --- | --- |
| C# source code | Apache License 2.0 with the project-specific Non-Commercial Exception in [`LICENSE`](LICENSE) |
| Artwork, textures, screenshots used as promotional assets, and branding | CC BY-NC-ND 4.0; see [`ASSET-LICENSE.md`](ASSET-LICENSE.md) |
| RimWorld, Harmony, RimChat, and other third-party material | Their respective authors' licenses |

The project code statement is:

> 本專案程式碼基於 Apache 2.0 授權開源。除原條款外，任何衍生作品、二創整合包或分發版本均不得用於直接或間接之商業營利行為（包含但不限於付費下載、付費訂閱牆專屬內容）。

Because this additional restriction applies, the combined project terms are not the unmodified OSI-approved Apache License 2.0. Read the complete notices before redistributing code or assets.

## Development history

The detailed version history is kept in [`Docs/CHANGELOG.md`](Docs/CHANGELOG.md). Contributions should preserve the deterministic authority boundary, add a debug or smoke path for new state transitions, and update the API guide when a public contract changes.
