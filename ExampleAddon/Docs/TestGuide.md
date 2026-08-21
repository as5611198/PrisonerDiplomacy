# Example Add-on Test Guide

This guide separates static validation, load validation, read-only API checks, and end-to-end player checks. Use a disposable save for developer actions that change core state.

## 1. Automated preflight

From the core repository root:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\ExampleAddon\Tools\Validate.ps1
pwsh -NoProfile -ExecutionPolicy Bypass -File .\ExampleAddon\Tools\Build.ps1
```

The validator checks:

- required package files and the new-Workshop-item guard;
- XML parsing for About, Defs, and all language files;
- identical Keyed keys and placeholders across five languages;
- identical DefInjected fields across five languages;
- every custom `texPath` resolves to a PNG;
- Preview dimensions and the Steam 1 MB limit;
- required package/dependency/version values;
- no copied core DLL;
- no references to private core state, Harmony patches, or reward spawning in Example Add-on source.

The build must finish with zero errors and produce:

```text
ExampleAddon/1.6/Assemblies/PrisonerDiplomacyExampleAddon.dll
```

## 2. Load-order smoke test

After deploying both mods, the automated path is:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\ExampleAddon\Tools\RunLoadSmoke.ps1
```

It uses a new temporary Savedata folder and keeps the log path for review. It never changes the player's normal mod list.

Load in this order:

1. Harmony
2. Core and owned DLC
3. Prisoner Diplomacy
4. Prisoner Diplomacy: Example Add-on

On startup, find one line like:

```text
[Prisoner Diplomacy Example Add-on] 1.0.0 initialized against API 1.2.0. extension=True persona=True ui=True.
```

Treat any of the following as a failure:

- XML parse error or missing `ThingDef`;
- failed assembly resolution;
- purple/missing item texture;
- duplicate package ID;
- extension, persona, or UI registration is `False` on a clean single-load setup;
- a Prisoner Diplomacy or Example Add-on exception;
- missing translation-key warnings for `PDX_` keys.

The included deployment script refuses to overwrite the installed package while RimWorld is running.

## 3. Registration and inspector checks

Open **Options > Mod settings > Prisoner Diplomacy: Example Add-on**.

1. Toggle the header strip off and on.
2. Toggle example personas off and on.
3. Enable verbose example logging, open the inspector once, and confirm one full report is logged.
4. Open the API Inspector.
5. Confirm the Overview tab reports compatible API versions and all three registrations as `True`.
6. Confirm both special reward IDs and all four event-definition IDs are listed.
7. Confirm the Events tab labels external event definitions as metadata and does not claim they were scheduled.
8. Use **Copy full report** and verify the clipboard contains Overview, Prisoners, Factions, and Events sections.

## 4. Context matrix

Use vanilla factions so the sample has no race-mod prerequisite.

| Test context | Expected value | Expected reward | Expected persona |
| --- | ---: | --- | --- |
| Human, Empire | `+35` | 1 encrypted ledger | Imperial persona |
| Human, pirate faction | `+10` | 1 encrypted ledger for industrial pirates | Pirate persona |
| Human, tribal faction | `+10` | 2 diplomatic seals | Tribal persona |
| Non-human mod race, Empire | `+25` | 1 encrypted ledger | Imperial persona |
| Non-human, unrelated faction | `0` | Tech-based reward | No sample persona |

For each row:

1. Select the prisoner with **Debug actions > Prisoner Diplomacy Example Add-on > Log selected prisoner adapter context**.
2. Confirm the faction Def, adjustment, reward ID, and persona in the log.
3. Open the normal Prisoner Diplomacy negotiation window.
4. Confirm the compact Header strip matches the logged adjustment and reward count.

The sample adapter requires both a Pawn and faction. Null or invalid contexts must return no runtime adjustment and must not throw.

## 5. Reward UI and delivery

Use the core developer tools to create a valid prisoner and open a negotiation.

### Low-tech faction

1. Select the diplomatic-seal special reward.
2. Confirm the UI shows quantity 2 and market value 90 per item / 180 total where values are displayed.
3. Preview and submit a legal demand.
4. Complete the core's dedicated release workflow.
5. Confirm exactly two `PDX_DiplomaticSeal` items arrive.
6. Save/reload and advance time; confirm they are not delivered again.

### Industrial-or-higher faction

Repeat with one encrypted diplomatic ledger. Expected market value is 600.

### Failure cases

- Ask for terms beyond reserve/material limits: core must counter or reject normally.
- Cancel or fail the release: no special reward should appear.
- Kill, recruit, enslave, transfer, or lose the prisoner before completion: core owns the cancellation; no reward should appear.
- Remove the Add-on from a copied save with an unresolved special reward: core must fail closed rather than substitute another item.

The Add-on never calls `ThingMaker` or spawns these items itself.

## 6. Read-only preview

Use **Preview 250 silver for selected prisoner** on a valid prisoner.

Expected behavior:

- the best available living free colonist by Social skill is selected;
- the log shows outcome, assessment, fair value, budget, reserve, and chance;
- no deal appears;
- no reserve changes;
- no negotiation count or cooldown changes;
- repeating the action in unchanged context returns the same deterministic result.

Invalid prisoner or missing negotiator should show a localized rejection message, not throw.

## 7. Language pass

Repeat one launch in each language:

- English
- 繁體中文
- 简体中文
- 日本語
- 한국어

Check About name/description fallback, Mod Settings, inspector buttons/tabs, Header strip, reward label/description, event metadata labels, and both item labels/descriptions. Technical report body remains canonical English by design.

Check UI scale 1.0, 1.25, and 1.5. Long localized text must remain inside the inspector/settings window. The Header extension must stay at a stable 28-pixel layout height and must not introduce a new workspace scrollbar by itself.

## 8. Save/reload and coexistence

1. Save with the Add-on enabled and no active deal.
2. Save with a special reward selected in an active core deal.
3. Save after verified delivery.
4. Reload each save and inspect the public report.
5. Add a second well-behaved adapter/persona/UI extension if available and confirm IDs compose deterministically.
6. Confirm duplicate IDs are rejected without replacing the first registration.
7. Confirm an exception in a test adapter is isolated by the core and cannot mutate a transaction.

## 9. Core smoke suite

After deploying both mods, run the normal core command-line smoke test. A healthy core build ends with:

```text
[Prisoner Diplomacy SmokeTest] PASS cases=127
```

This proves backend invariants and API registry behavior. It does not prove translated UI layout, custom texture framing, real player clicks, or a full event/caravan playthrough. Record those separately.

## 10. Steam release check

Run:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\ExampleAddon\Tools\Package.ps1
```

Before the first upload:

- verify `About/PublishedFileId.txt` does not exist;
- use `About/Preview.png` as the Workshop preview;
- set Prisoner Diplomacy Workshop item `3787243156` as a required dependency;
- upload as Hidden first;
- subscribe to the hidden item in a clean profile and repeat the load-order smoke;
- only then make it Public.

After Steam creates a Workshop ID, keep `PublishedFileId.txt` local for updates but do not commit it to the source template.
