# Prisoner Diplomacy: Example Add-on

[English](README.md) | [繁體中文](README.zh-TW.md)

A working, source-included RimWorld 1.6 add-on for the public Prisoner Diplomacy 1.2 API. It is both a playable companion mod and a reference project that authors can copy without touching private transaction state.

## What the installed add-on demonstrates

- `IPrisonerDiplomacyExtension` registration with stable IDs and API-version checks.
- One deterministic context adapter that reads race, PawnKind, faction Def, and technology.
- Two custom `ThingDef` rewards whose pricing, validation, persistence, and exactly-once delivery stay in the core.
- Small diplomatic-value adjustments: `+10` for vanilla humans and an additional `+25` for Empire prisoners.
- Bounded narrative personas for Empire, pirate, and tribal factions.
- All four v1.2 event-definition families as discoverable metadata.
- A compact `IPrisonerDiplomacyUiExtension` header strip using read-only context.
- A read-only API Inspector for extension IDs, prisoners, factions, deals, rewards, event definitions, and persisted event snapshots.
- `PreviewDemand` and snapshot diagnostics through developer-mode actions.
- English, Traditional Chinese, Simplified Chinese, Japanese, and Korean localization.

Low-technology factions expose two **diplomatic seals** as a special reward. Industrial and higher factions expose one **encrypted diplomatic ledger**. These items appear in the normal Prisoner Diplomacy reward selector and are delivered only after the core verifies the correct handoff.

## Requirements

- RimWorld 1.6
- [Prisoner Diplomacy](https://steamcommunity.com/sharedfiles/filedetails/?id=3787243156) 1.2 or a compatible API 1.x release
- Harmony, inherited through the core mod dependency

Load this add-on after Prisoner Diplomacy. Its package ID is `g1061.prisonerdiplomacy.exampleaddon`.

## Use it in game

1. Enable Prisoner Diplomacy and this add-on.
2. Capture a valid prisoner and open the normal Prisoner Diplomacy negotiation window.
3. The header shows the example adapter adjustment and available special-reward count.
4. Low-tech and industrial/spacer factions expose different special items.
5. Open **Mod Settings > Prisoner Diplomacy: Example Add-on > Open read-only API inspector**.
6. In developer mode, search the **Prisoner Diplomacy Example Add-on** debug category for snapshot, preview, context, and report actions.

## Build

From the Prisoner Diplomacy repository root:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\ExampleAddon\Tools\Build.ps1
```

The project references `..\1.6\Assemblies\PrisonerDiplomacy.dll` and writes its assembly to `ExampleAddon\1.6\Assemblies`. Override `RimWorldDir` or `PrisonerDiplomacyRoot` with MSBuild properties when building elsewhere.

After deploying both mods, run the isolated load smoke:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\ExampleAddon\Tools\RunLoadSmoke.ps1
```

It creates a temporary Savedata folder, loads only Harmony, owned DLC, the core, and this Add-on, requires `PASS cases=127`, checks all three registrations, and rejects Add-on texture/translation errors. The script safely closes only the test process it started.

## Documentation

- [`Docs/API-Cookbook.md`](Docs/API-Cookbook.md): canonical API walkthrough and contract table.
- [`Docs/API-Cookbook.zh-TW.md`](Docs/API-Cookbook.zh-TW.md): Traditional Chinese orientation.
- [`Docs/TestGuide.md`](Docs/TestGuide.md): player and author test matrix.
- [`Templates`](Templates): minimal extension, custom-race adapter, and read-only UI templates.
- [`Workshop`](Workshop): five localized Steam descriptions and upload notes.

## Critical authority boundary

This add-on never releases or transfers Pawns, creates or completes deals, spawns negotiated rewards, edits faction reserves, or schedules internal events. It returns metadata and consumes snapshots. Prisoner Diplomacy remains the sole transaction authority.

API 1.2 external event definitions are catalog metadata. Registration does not schedule them. A custom event loop must remain in the add-on's own save component and may use Prisoner Diplomacy only for read-only context until a public scheduler/executor contract exists.

## License

C# source is distributed under Apache License 2.0 plus the project Non-Commercial Exception in [`LICENSE`](LICENSE). Artwork, textures, screenshots, and branding use CC BY-NC-ND 4.0 under [`ASSET-LICENSE.md`](ASSET-LICENSE.md).

This add-on was 100% produced by Codex (GPT-5.6 SOL); the project owner provided the ideas only.
