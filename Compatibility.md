# Prisoner Diplomacy Compatibility Report

This report describes the compatibility boundaries of Prisoner Diplomacy 1.2.0 for RimWorld 1.6.
It is intentionally more conservative than a list of every mod that happens to load: a mod is only
marked `Verified` when the relevant build and interaction path have been checked. A `Compatibility
target` entry means that Prisoner Diplomacy has no hard dependency or known replacement hook, but the
exact installed version still needs player QA.

## Requirements and load order

- RimWorld 1.6 is required.
- Harmony is required and must load before Prisoner Diplomacy.
- Prisoner Diplomacy does not require RimChat, AI providers, Humanoid Alien Races, Hospitality,
  Combat Extended, or any other optional mod.
- Prisoner Realism should load after Harmony, the DLC, and its own listed integrations. Prisoner
  Diplomacy may load after Prisoner Realism; both orders have passed the backend smoke check.
- Back up important saves before changing a large mod list. The compatibility report does not replace
  a save-specific load test.

## Status definitions

| Status | Meaning |
| --- | --- |
| Verified | The named version or interaction path was checked with the current release candidate. |
| Compatible boundary | No direct hook overlap is present in the current source; the other mod owns the listed feature. |
| Compatibility target | No hard dependency or known conflict, but the exact release still needs manual QA. |
| Optional handoff | The integration is detected and the overlapping Prisoner Diplomacy feature yields to the other mod. |

## Core integrations

| Mod or environment | Status | Interaction |
| --- | --- | --- |
| RimWorld 1.6 + installed DLC | Verified | Deterministic negotiation, release, reward, save-migration, and event smoke paths pass. |
| Prisoner Realism 1.6 (Workshop 3760196312) | Verified | Both use the vanilla prison-break entry point. Prisoner Diplomacy supplies diplomatic triggers and outcomes; Prisoner Realism controls door, guard, turret, cell participation, combat, and surrender behavior. |
| RimChat 1.5.12 | Verified | Ransom ownership and duplicate-action guards are isolated and version-checked. RimChat is optional and cannot complete a Prisoner Diplomacy deal. |
| Humanoid Alien Races + Hospitality | Verified | Eligible custom Pawns are observed safely; excluded, temporary, summoned, and unstable Pawns are skipped. |
| Custom race or faction mods | Compatibility target | No compile-time dependency. Authors may register a race/faction adapter or use the compatibility DefModExtension. |
| Vanilla Factions Expanded and other faction expansions | Compatibility target | Faction overrides can mark a faction as non-negotiating, transactional, or diplomatic. |
| Pawn Surrender, Hostage Taker, Talk Before Bloodshed | Compatibility target | Prisoner Diplomacy observes the resulting player prisoner and does not replace capture hooks. |

## Prisoner-management mods

The following entries mirror the current Prisoner Realism compatibility list. Prisoner Diplomacy does
not replace these features; it observes the resulting prisoner state and owns only its negotiation,
agreement, reward, and diplomatic-memory state.

| Mod | Status | Interaction |
| --- | --- | --- |
| CPERS: Arrest Here | Compatible boundary | Prisoner Diplomacy does not patch arrest jobs. It registers a Pawn after the game has made that Pawn a valid player prisoner. |
| Prisoners Don't Have Keys | Compatible boundary | Prisoner Diplomacy does not patch `Building_Door.PawnCanOpen`, lockpicking, or door jobs. Door and key behavior remains owned by the other mod. |
| Prisoners Should Fear Turrets | Compatible boundary | Prisoner Diplomacy does not patch turret target acquisition. Turret behavior remains owned by the other mod. |
| Prisoners Are Not Swines | Compatible boundary | Prisoner Diplomacy does not patch prisoner cleaning or work selection. |
| Lower Prisoner Expectation | Compatible boundary | Prisoner Diplomacy reads health, importance, resistance, treatment, and faction memory; it does not replace expectation calculation. |
| Set Owner for Prisoner Beds | Compatible boundary | Prisoner Diplomacy does not patch bed assignment or bed-owner gizmos. |
| Prison Labor | Compatible boundary | Prisoner Diplomacy does not patch prisoner work bills. Prison Labor remains the work authority when installed. |
| Prison Commons (Continued) | Compatibility target | Prisoner Diplomacy does not patch prison-room, commons-door, food-search, or recreation routing. Test the exact commons release with large multi-room prisons. |
| Bondage Furniture | Compatibility target | Furniture and restraint behavior remain owned by Bondage Furniture. Prisoner Diplomacy only evaluates a Pawn if RimWorld reports an eligible player prisoner. |
| RimHUD | Compatible boundary | Prisoner Diplomacy uses its own negotiation, faction-browser, agreement, history, and event windows and does not alter the RimHUD layout. |
| No Sympathy for Prisoners | Compatible boundary | Prisoner Diplomacy does not replace social-sympathy thoughts. Its faction memory is a separate diplomatic record. |

## Combat, surgery, and consequences

| Mod | Status | Interaction |
| --- | --- | --- |
| Combat Extended | Compatibility target | No compile-time dependency and no CE-owned weapon or turret state is required. Causal raid generation uses the normal faction pawn-group interface; test the exact CE build and turret settings. |
| War Crimes Expanded 2 | Compatibility target | Prisoner Diplomacy records damage, permanent harm, and body-part removal through vanilla health/surgery boundaries. It does not call WCE2 internals; the exact torture recipe should be checked if it is expected to count as organ harvesting or permanent harm. |
| Prisoner Realism surgery reactions | Verified boundary | Prisoner Diplomacy updates faction treatment memory while Prisoner Realism adds prisoner and witness reactions. Neither patch suppresses the other. |

## Prisoner Realism overlap details

The two mods share a small number of vanilla method boundaries, but do not install competing
transaction systems:

1. `PrisonBreakUtility.StartPrisonBreak(Pawn)` is called by both mods. Prisoner Diplomacy calls it
   only for a diplomatic event such as pirate jailbreak incitement or false surrender. Prisoner
   Realism changes what happens after the break starts.
2. `Pawn_HealthTracker.PostApplyDamage` is observed by both mods. Prisoner Diplomacy records the
   battle cause, injury, and permanent-harm context; Prisoner Realism may make a breaking prisoner
   surrender after taking damage.
3. Medical `RecipeWorker.ApplyOnPawn` is observed by both mods. Prisoner Diplomacy records organ
   removal and faction resentment; Prisoner Realism adds the victim and witness relationship reaction.
4. Raid execution can be observed by both mods. Prisoner Diplomacy marks causal rescue or retaliation
   raids, while Prisoner Realism may trigger assisted breaks for prisoners already near a break.

Expected combined behavior:

- A prisoner who leaves the map without a valid release is an escape failure for an active deal.
- A prisoner who surrenders during a Prisoner Realism break remains held; Prisoner Diplomacy does not
  mark that as an escape.
- A raid, riot, hunger strike, surgery, or recidivism event can change prisoner conditions and thereby
  affect later diplomatic memory, but neither mod silently completes a deal for the other.

## Optional DefModExtension

Race or faction authors can opt out without a hard assembly reference when their load order permits
the extension:

```xml
<modExtensions>
  <li Class="PrisonerDiplomacy.PrisonerDiplomacyPawnCompatibilityExtension">
    <ExcludeFromDiplomacy>true</ExcludeFromDiplomacy>
    <TemporaryPawn>true</TemporaryPawn>
    <ExclusionReason>summoned pawn</ExclusionReason>
  </li>
</modExtensions>
```

The faction extension supports `Automatic`, `NonNegotiating`, `Transactional`, and `Diplomatic`
overrides.

## Verification evidence

- Prisoner Realism build 40 plus Prisoner Diplomacy 1.2.0 was loaded in both mod orders.
- Both runs ended with `Prisoner Diplomacy SmokeTest] PASS cases=127` and no related compatibility
  error.
- The smoke test validates backend state transitions only. Manual QA is still required for a complete
  prison-break combat sequence, mid-break surrender, large Prison Commons layouts, Combat Extended
  weapons/turrets, and third-party torture recipes.

## Reporting a compatibility issue

Please include:

1. RimWorld version, DLC list, and both mod versions.
2. The complete active mod list and load order.
3. Whether the issue happened during negotiation, release, a prison break, surgery, raid, or a
   follow-up event.
4. A `Prisoner Diplomacy -> Copy diagnostic report` result and the relevant `Player.log` excerpt.

Never include AI provider keys, save files, or private API credentials in a public report.
