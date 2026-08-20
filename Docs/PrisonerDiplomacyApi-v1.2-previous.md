# Prisoner Diplomacy Extension API v1.2

This document describes the public extension surface shipped with Prisoner Diplomacy 1.2.0.

## Authority boundary

An extension may register event definitions, race adapters, special reward metadata, and diplomatic value adjustments. It cannot directly mutate `PrisonerRecord`, `PrisonerDeal`, Pawn faction, payment, release, deadlines, or completion state. The core validates and applies all gameplay transitions.

The stable entry points are:

```csharp
PrisonerDiplomacyApi.ApiVersion
PrisonerDiplomacyExtensionRegistry.Register(IPrisonerDiplomacyExtension extension)
PrisonerDiplomacyBackendApi.GetEventDefinitions()
PrisonerDiplomacyBackendApi.GetSpecialRewardOptions(Pawn prisoner, Faction faction)
PrisonerDiplomacyBackendApi.GetDiplomaticValueAdjustment(Pawn prisoner, Faction faction)
```

The current API version is `1.2.0`. Extensions must use the same major version and may target an older minor version. Duplicate extension IDs, event IDs, and adapter IDs are rejected.

## Minimal add-on

The repository includes `PrisonerDiplomacySampleAddon`, a minimal example that registers one event and one race adapter. Its test reward remains inactive until the developer action enables it. A separate add-on can use the same contract without referencing internal GameComponent methods:

```csharp
public sealed class MyDiplomacyAddon : IPrisonerDiplomacyExtension
{
    public string ExtensionId => "author.myaddon";
    public string ApiVersion => PrisonerDiplomacyApi.ApiVersion;

    public IEnumerable<PrisonerDiplomacyEventDefinition> GetEventDefinitions()
    {
        yield return new PrisonerDiplomacyEventDefinition(
            "myaddon.neutral_exchange",
            "MyAddon_EventLabel",
            "MyAddon_EventText",
            PrisonerDiplomacyEventKind.NeutralTradeCaravan);
    }

    public IEnumerable<IPrisonerDiplomacyRaceAdapter> GetRaceAdapters()
    {
        yield return new MyRaceAdapter();
    }
}
```

Register from the add-on's `Mod` constructor after both assemblies are loaded:

```csharp
PrisonerDiplomacyExtensionRegistry.Register(new MyDiplomacyAddon());
```

## Adapter rules

`AppliesTo` and `GetSpecialRewards` must be side-effect free. `GetDiplomaticValueAdjustment` is clamped to `-1000..1000` per adapter. In v1.2 an adapter may describe a fixed item reward with `RequiredThingDefName` and `MinimumCount`. The core resolves the ThingDef, validates eligibility, includes its market-value cost in budget and material-cap checks, persists the selected item, and performs exactly-once drop-pod fulfillment. Add-ons must not spawn, remove, or destroy items from an adapter callback.

## Built-in event families

The core registers these versioned definitions:

- `core.neutral_trade`: a staged neutral trade-point proposal with an explicit accept/reject letter. The core creates one persistent world-map exchange point, exposes a vanilla Caravan arrival action, verifies the specified prisoner is in the arriving Caravan, and only then hands off the Pawn and invokes normal exactly-once fulfillment. Failed or stale arrivals do not mutate the deal.
- `core.ransom_ambush`: a causal retaliation raid tied to the source ransom deal.
- `core.false_surrender`: a high-skill infiltration consequence that can start a prison break or damage faction memory.
- `core.public_trial`: a leader-only trial that improves allied goodwill and schedules retaliation from the original faction.

All extension event records are persisted independently from normal transaction records, retried up to four times, and retained for 60 in-game days for the event-history UI. Snapshots expose creation time, trigger time, stage, retry count, source deal, and intermediary label without allowing mutation.

## Compatibility

Use only public types in this document. Internal classes, save lists, and UI windows are intentionally not extension points. Check `PrisonerDiplomacyBackendApi.ApiVersion` before enabling optional integration and fail closed when the major version is different.
