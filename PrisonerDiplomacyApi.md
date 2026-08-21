# Prisoner Diplomacy API v1.2

This is the public modder guide for Prisoner Diplomacy `1.2.0`. It is written for RimWorld 1.6 add-ons that want to describe race-specific diplomacy, fixed special-item rewards, read-only status panels, or one of the core event families.

## 1. Purpose

The API lets another mod register stable metadata without taking ownership of the transaction. It is suitable for optional integrations: the add-on can be absent, loaded late, or disabled without invalidating Prisoner Diplomacy saves.

The public assembly types are in `Source/PrisonerDiplomacy/Api`. The namespace remains `PrisonerDiplomacy` after the source reorganization.

## 2. Requirements

- RimWorld 1.6 and a C# assembly referencing the Prisoner Diplomacy DLL.
- C# 7.3-compatible syntax and the same .NET Framework profile used by the target game.
- A unique reverse-domain `ExtensionId`, adapter ID, event ID, and translation-key prefix.
- No compile-time dependency on internal GameComponent fields, save lists, or UI windows.

Reference the DLL as an optional dependency in the add-on project. At runtime, detect the assembly before registering and fail closed when it is absent.

## 3. Public entry points

```csharp
PrisonerDiplomacyApi.ApiVersion
PrisonerDiplomacyBackendApi.ApiVersion
PrisonerDiplomacyExtensionRegistry.Register(IPrisonerDiplomacyExtension extension)
PrisonerDiplomacyBackendApi.GetPrisonerSnapshots(Map map)
PrisonerDiplomacyBackendApi.GetFactionSnapshots(Map map)
PrisonerDiplomacyBackendApi.TryGetActiveDealSnapshot(Pawn prisoner, out PrisonerDiplomacyDealSnapshot snapshot)
PrisonerDiplomacyBackendApi.PreviewDemand(
    Pawn prisoner, Pawn negotiator, RewardDemand demand, out string reasonKey)
```

The backend also exposes event definitions, event snapshots, special-reward options, diplomatic-value adjustments, and lookup helpers. These are reads or previews; they do not create a deal or mutate a Pawn.

## 4. Registration lifecycle

Register from the add-on `Mod` constructor only after checking that the Prisoner Diplomacy assembly is available:

```csharp
public sealed class MyAddonMod : Mod
{
    public MyAddonMod(ModContentPack content) : base(content)
    {
        PrisonerDiplomacyExtensionRegistry.Register(new MyDiplomacyAddon());
    }
}
```

The registry rejects null extensions, blank IDs, duplicate IDs, incompatible versions, duplicate event IDs, duplicate adapter IDs, and adapters that declare an incompatible version. Registration is process-local; the registered metadata is not itself saved. Persistent event state belongs to the core.

## 5. Authority boundary

`PrisonerDiplomacyGameComponent` is the only authority for `PrisonerRecord`, `PrisonerDeal`, Pawn ownership, release confirmation, reward removal/spawning, deadlines, faction reserves, faction memory, and event outcomes.

An add-on callback must not spawn, remove, destroy, recruit, release, transfer, or otherwise mutate a Pawn or Thing. It must not infer success from a letter, AI text, or a UI click. Wait for a read-only snapshot that reflects a verified core transition.

## 6. API version compatibility

The current version is `1.2.0`; the major version is `1`. An extension must declare a parseable version with the same major and a version less than or equal to the current version:

```csharp
public string ApiVersion => PrisonerDiplomacyApi.ApiVersion;
```

When an optional integration starts, compare `PrisonerDiplomacyBackendApi.ApiVersion`. If the major version differs, disable the integration and keep the base add-on loaded. Do not guess at newer fields.

## 7. Read-only snapshots

Use snapshots instead of internal lists:

```csharp
IReadOnlyList<PrisonerDiplomacyFactionSnapshot> factions =
    PrisonerDiplomacyBackendApi.GetFactionSnapshots(map);

if (PrisonerDiplomacyBackendApi.TryGetActiveDealSnapshot(
        prisoner, out PrisonerDiplomacyDealSnapshot deal))
{
    Log.Message(deal.StateKey + " / " + deal.OfferRemainingTicks);
}
```

`PrisonerDiplomacyPrisonerSnapshot` includes eligibility, health category, diplomatic value, active deal ID, negotiation count, and special reward IDs. Faction snapshots include negotiation type, finance summary, memory summary, strategic status, and available counts. Event snapshots include definition/extension IDs, stage, retry count, source deal, and intermediary label. Objects expose private setters by design.

## 8. Translation keys

Event definitions and reward definitions carry `LabelKey` and `DescriptionKey`; they are translation keys, not display text. Prefix keys with the add-on package ID and ship both `Languages/English/Keyed/*.xml` and `Languages/ChineseTraditional/Keyed/*.xml` when those languages are supported.

```xml
<MyAddon_RaceRewardLabel>Ancient gene-seed</MyAddon_RaceRewardLabel>
<MyAddon_RaceRewardDescription>A culturally important reward.</MyAddon_RaceRewardDescription>
```

Never put player-specific names or unbounded user text in a key. Keep placeholders stable between languages.

## 9. Minimal Add-on structure

The repository includes [`Source/PrisonerDiplomacy/Api/PrisonerDiplomacySampleAddon.cs`](Source/PrisonerDiplomacy/Api/PrisonerDiplomacySampleAddon.cs). A minimal external add-on looks like this:

```csharp
using System.Collections.Generic;

namespace MyAddon
{
    public sealed class MyDiplomacyAddon : PrisonerDiplomacy.IPrisonerDiplomacyExtension
    {
        public string ExtensionId => "author.myaddon";
        public string ApiVersion => PrisonerDiplomacy.PrisonerDiplomacyApi.ApiVersion;

        public IEnumerable<PrisonerDiplomacy.PrisonerDiplomacyEventDefinition>
            GetEventDefinitions()
        {
            yield return new PrisonerDiplomacy.PrisonerDiplomacyEventDefinition(
                "author.myaddon.neutral_trade",
                "MyAddon_EventLabel",
                "MyAddon_EventDescription",
                PrisonerDiplomacy.PrisonerDiplomacyEventKind.NeutralTradeCaravan);
        }

        public IEnumerable<PrisonerDiplomacy.IPrisonerDiplomacyRaceAdapter>
            GetRaceAdapters()
        {
            yield return new MyRaceAdapter();
        }
    }
}
```

Keep registration idempotent from the add-on side. The registry itself rejects a second registration with the same ID.

## 10. Race and Special Reward Adapter Guide

Implement `IPrisonerDiplomacyRaceAdapter` when a race, PawnKind, or faction technology level should change the diplomatic value or expose a fixed ThingDef reward.

```csharp
private sealed class MyRaceAdapter : PrisonerDiplomacy.IPrisonerDiplomacyRaceAdapter
{
    public string AdapterId => "author.myaddon.ancient.race";
    public string ApiVersion => PrisonerDiplomacy.PrisonerDiplomacyApi.ApiVersion;

    public bool AppliesTo(PrisonerDiplomacy.PrisonerDiplomacyRaceContext context)
    {
        return context != null && context.RaceDefName == "MyAncientRace";
    }

    public int GetDiplomaticValueAdjustment(
        PrisonerDiplomacy.PrisonerDiplomacyRaceContext context)
    {
        return 50;
    }

    public IEnumerable<PrisonerDiplomacy.PrisonerDiplomacySpecialRewardDefinition>
        GetSpecialRewards(PrisonerDiplomacy.PrisonerDiplomacyRaceContext context)
    {
        yield return new PrisonerDiplomacy.PrisonerDiplomacySpecialRewardDefinition(
            "author.myaddon.ancient.core",
            "MyAddon_AncientCoreLabel",
            "MyAddon_AncientCoreDescription",
            "ComponentIndustrial",
            3);
    }
}
```

`RaceDefName`, `PawnKindDefName`, `FactionDefName`, `Faction`, and `FactionTechLevel` are context inputs. `AppliesTo` and `GetSpecialRewards` must be deterministic and side-effect free. The core clamps each adapter's value adjustment to `-1000..1000`, resolves the requested `ThingDef`, validates availability and material caps, persists the selected reward, and fulfills it exactly once after verified delivery.

Do not call `ThingMaker`, `GenPlace`, `Thing.SplitOff`, inventory APIs, or faction/Pawn APIs that mutate state from an adapter. Return no reward when the current context does not apply. Use stable IDs; changing a reward ID after release can make old saved deals unreadable.

## 11. Determinism and validation rules

- Use only stable Def names and bounded values in callbacks.
- Do not use wall-clock time, random state, network responses, or mutable global state to decide an adapter result.
- Treat `PreviewDemand` as a preview. It does not reserve a faction budget or create a deal.
- Clamp or reject user-provided text before using it in optional narrative prompts.
- Re-check the current Pawn, faction, deal, negotiation round, reward legality, and deadlines on the core side.
- If an adapter throws, the registry logs a warning and ignores that adapter for the current query.

## 12. Debugging and compatibility testing

Use a disposable save with developer mode enabled. The core debug menu includes custom-race discovery, adapter reward toggles, event forcing/advance/cancel/logging, world trade-point routing, compatibility reports, save repair, and a complete smoke command.

Recommended add-on checks:

1. Load without Prisoner Diplomacy and confirm the add-on disables its optional integration cleanly.
2. Load with Prisoner Diplomacy and register once; log the registered ID and API version.
3. Test a matching and non-matching race, no-stock reward, invalid ThingDef, and mixed reward demand.
4. Save and reload after the offer, accepted, released, and fulfilled states.
5. Run the core smoke test and inspect the log for `PASS cases=127`, exceptions, or missing Def warnings.

Do not ship a dependency that changes the global mod load order or patches internal transaction methods solely to read state.

## 13. Diplomacy Event Add-on Guide

An event add-on starts by registering a `PrisonerDiplomacyEventDefinition`:

```csharp
yield return new PrisonerDiplomacy.PrisonerDiplomacyEventDefinition(
    "author.myaddon.public_trial",
    "MyAddon_TrialLabel",
    "MyAddon_TrialDescription",
    PrisonerDiplomacy.PrisonerDiplomacyEventKind.PublicWarCrimeTrial,
    requiresPrisoner: true);
```

In v1.2, the `Kind` selects one of the core-owned, deterministic event state machines: `NeutralTradeCaravan`, `RansomAmbushRetaliation`, `FalseSurrenderInfiltration`, or `PublicWarCrimeTrial`. The core owns scheduling, choice letters, retries, persistence, validation, world-object cleanup, and outcomes. Registered definitions are discoverable through `GetEventDefinitions()` and event state is observable through `GetEventSnapshots()`.

Registration alone does not schedule the definition. Core-defined triggers schedule the built-in families; a third-party definition is catalog metadata until the core invokes it or a later API version publishes a validated public scheduling request.

The v1.2 public surface intentionally does **not** expose the internal event scheduler or a custom event-execution callback. An external add-on can supply translated metadata and select a core event family, but it must not call internal `TryScheduleExtensionEvent`, construct `PrisonerDiplomacyEventRecord` objects in the core save list, or patch `TryExecuteDiplomacyEvent`. A fully custom event loop should remain in the add-on's own state and use this API only for read-only context until a future API version publishes an explicit event executor contract.

For the current built-in families:

- **Neutral trade:** the core creates a persistent world-map trade point, verifies a Caravan contains the specified prisoner, then routes the handoff through normal release and fulfillment checks.
- **False surrender:** the core applies the infiltration consequence and records the causal event.
- **Public trial:** the core presents a player choice and applies the documented faction-memory and retaliation consequences.
- **Ransom ambush:** the core links the retaliation to the source deal and retries safely when the target state is temporarily unavailable.

Every event is independently persisted, retried up to four times, retained for 60 in-game days after completion/failure, and exposed as a snapshot. Add-on UI should render the snapshot and call only documented public actions; it should never claim that an event completed before `State == Completed` is observed.

## 14. Persona providers and AI negotiation advisory

Faction persona text is bounded narrative context. Players may set a global fallback or a per-faction override in the mod settings. A race or faction add-on can provide a default without editing the player's settings:

```csharp
public sealed class DragonianPersona : PrisonerDiplomacy.IPrisonerDiplomacyPersonaProvider
{
    public string ProviderId => "author.dragonians.persona";
    public string ApiVersion => PrisonerDiplomacy.PrisonerDiplomacyApi.ApiVersion;

    public bool AppliesTo(PrisonerDiplomacy.PrisonerDiplomacyRaceContext context)
        => context?.RaceDefName == "Dragonian";

    public string GetPersona(PrisonerDiplomacy.PrisonerDiplomacyRaceContext context)
        => "proud, private, status-conscious, and protective of bloodline interests";
}
```

Register it once from the add-on's `Mod` constructor with `PrisonerDiplomacyExtensionRegistry.RegisterPersonaProvider`. Persona text is normalized to 500 characters and marked as untrusted prompt context; it cannot alter rewards, deadlines, incidents, or transaction state.

AI negotiation adjustments are opt-in and disabled by default. When enabled, the model may return only three categorical signals for a live counteroffer: `urgency`, `concession`, and `leverageResponse`. The core applies a small bounded cost adjustment to the existing counteroffer, then re-validates reward types, faction reserve, material cap, prisoner context, and negotiation state. Accepted deals and completed transactions are never rewritten. A stale, malformed, cancelled, or unavailable response falls back to the deterministic terms.

## 15. Read-only UI extensions

Add-ons can register presentation-only content in the negotiation window through `IPrisonerDiplomacyUiExtension`:

```csharp
PrisonerDiplomacyUiExtensionRegistry.Register(new MyHeaderExtension());
```

The three regions are `FactionHeader`, `PrisonerSummary`, and `NegotiationBody`. `GetHeight` is called before drawing; return `0` when the extension does not apply, otherwise return a stable bounded height and draw only inside the provided Rect.

`PrisonerDiplomacyUiContext` exposes read-only faction, prisoner, and deal snapshots plus `CompactLayout`. Add-on UI must not mutate referenced Pawn/Faction objects, create transaction buttons, claim a transition completed, or use reflection to call the internal window/controller. Restore `GUI.color`, `Text.Anchor`, and `Text.Font` after drawing. Exceptions are isolated per extension, but repeated layout or drawing failures remain an add-on defect.

The core `PrisonerDiplomacyUiTheme` is internal and not part of the public compatibility contract. Add-ons should own their colors and controls. A complete working Header strip is in [`ExampleAddon/Source/ExampleHeaderUiExtension.cs`](ExampleAddon/Source/ExampleHeaderUiExtension.cs); the copyable minimal version is [`ExampleAddon/Templates/ReadOnlyUiExtension.cs`](ExampleAddon/Templates/ReadOnlyUiExtension.cs).

## Compatibility summary

Use only the types in this guide. Declare a required dependency for direct compile-time integration (or isolate a genuinely optional integration behind reflection), fail closed on a major-version mismatch, preserve stable IDs, and let the core remain the sole transaction authority. See [`Source/README.md`](Source/README.md), [`Docs/Architecture.md`](Docs/Architecture.md), [`Docs/RewardCatalog.md`](Docs/RewardCatalog.md), [`Docs/AddonQuickstart.md`](Docs/AddonQuickstart.md), and the complete [`ExampleAddon`](ExampleAddon) for the implementation boundary and working examples.
