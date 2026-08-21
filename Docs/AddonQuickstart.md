# Add-on Quickstart

This guide shows the smallest practical Prisoner Diplomacy extension. It is intended for a separate race, faction, item, or event add-on, not for editing the core mod.

## 1. Folder layout

```text
MyPrisonerAddon/
  About/
    About.xml
  1.6/
    Assemblies/
      MyPrisonerAddon.dll
    Languages/
      English/Keyed/MyPrisonerAddon.xml
      ChineseTraditional/Keyed/MyPrisonerAddon.xml
  Source/
    MyPrisonerAddon.cs
```

The add-on should list `g1061.prisonerdiplomacy` as a dependency and load after it. Reference the released `PrisonerDiplomacy.dll` at build time, but do not copy or modify the core assembly in the add-on package.

## 2. Register once

```csharp
using System.Collections.Generic;
using Verse;

namespace MyPrisonerAddon
{
    public sealed class MyPrisonerAddonMod : Mod
    {
        public MyPrisonerAddonMod(ModContentPack content) : base(content)
        {
            PrisonerDiplomacy.PrisonerDiplomacyExtensionRegistry.Register(
                new MyPrisonerAddonExtension());
        }
    }

    public sealed class MyPrisonerAddonExtension
        : PrisonerDiplomacy.IPrisonerDiplomacyExtension
    {
        public string ExtensionId => "author.myaddon.prisoner-diplomacy";
        public string ApiVersion => PrisonerDiplomacy.PrisonerDiplomacyApi.ApiVersion;

        public IEnumerable<PrisonerDiplomacy.PrisonerDiplomacyEventDefinition>
            GetEventDefinitions()
        {
            yield return new PrisonerDiplomacy.PrisonerDiplomacyEventDefinition(
                "author.myaddon.dragon_exchange",
                "MyAddon_DragonExchangeLabel",
                "MyAddon_DragonExchangeDescription",
                PrisonerDiplomacy.PrisonerDiplomacyEventKind.NeutralTradeCaravan);
        }

        public IEnumerable<PrisonerDiplomacy.IPrisonerDiplomacyRaceAdapter>
            GetRaceAdapters()
        {
            yield return new MyDragonAdapter();
        }
    }
}
```

Registration is idempotent from the registry's perspective: duplicate extension IDs, incompatible major versions, and duplicate adapter IDs are rejected. Keep IDs stable after release so existing saves remain understandable.

## 3. Add a special item reward

```csharp
private sealed class MyDragonAdapter
    : PrisonerDiplomacy.IPrisonerDiplomacyRaceAdapter
{
    public string AdapterId => "author.myaddon.dragon.reward";
    public string ApiVersion => PrisonerDiplomacy.PrisonerDiplomacyApi.ApiVersion;

    public bool AppliesTo(PrisonerDiplomacy.PrisonerDiplomacyRaceContext context)
    {
        return context?.RaceDefName == "MyDragonRace";
    }

    public int GetDiplomaticValueAdjustment(
        PrisonerDiplomacy.PrisonerDiplomacyRaceContext context)
    {
        return 75;
    }

    public IEnumerable<PrisonerDiplomacy.PrisonerDiplomacySpecialRewardDefinition>
        GetSpecialRewards(PrisonerDiplomacy.PrisonerDiplomacyRaceContext context)
    {
        yield return new PrisonerDiplomacy.PrisonerDiplomacySpecialRewardDefinition(
            "author.myaddon.dragon.ember_core",
            "MyAddon_EmberCoreLabel",
            "MyAddon_EmberCoreDescription",
            "MyAddon_EmberCore",
            1);
    }
}
```

`MyAddon_EmberCore` must resolve to an item `ThingDef` with a positive stack limit and positive market value. The core prices and delivers it. The adapter must not call `ThingMaker`, spawn items, mutate a Pawn or faction, reserve inventory, use random state, or call a network service.

## 4. Add translations

```xml
<?xml version="1.0" encoding="utf-8"?>
<LanguageData>
  <MyAddon_DragonExchangeLabel>Dragon exchange</MyAddon_DragonExchangeLabel>
  <MyAddon_DragonExchangeDescription>Offer a neutral handoff recognized by the dragon faction.</MyAddon_DragonExchangeDescription>
  <MyAddon_EmberCoreLabel>Ember core</MyAddon_EmberCoreLabel>
  <MyAddon_EmberCoreDescription>A culturally important dragon artifact.</MyAddon_EmberCoreDescription>
</LanguageData>
```

Every language file should contain the same keys and placeholders. Keep add-on keys namespaced with the add-on ID to avoid collisions.

## 5. What the API does and does not do

The core accepts event metadata and adapter-provided reward metadata. It owns all transaction state, Pawn release, payment, deadlines, event persistence, retries, and exactly-once fulfillment. In API v1.2, a registered event definition is metadata unless it selects a core event family that the core schedules; the public API does not expose the internal event scheduler or allow an add-on to construct save records.

For a fully custom event loop, keep the state in the add-on and use Prisoner Diplomacy snapshots only as read-only context. Never reflect into `PrisonerDiplomacyGameComponent`, patch internal transaction methods, or claim completion before a documented completed snapshot exists.

## 6. Test checklist

1. Confirm `About.xml` declares Prisoner Diplomacy as required and places the add-on after it. A direct compile-time API integration is not expected to run without the core assembly.
2. Load with Prisoner Diplomacy 1.2 and verify the extension ID, API version, adapter ID, and translated labels.
3. Test matching and non-matching races, missing reward definitions, no-stock fulfillment, invalid ThingDefs, and mixed demands.
4. Save and reload after an offer, accepted deal, release order, and fulfilled reward.
5. Enable the core debug special-reward toggle and verify the reward is delivered exactly once.
6. Run the core smoke test and inspect the log for `PASS cases=127`, exceptions, and missing definitions.

The complete standalone [`ExampleAddon`](../ExampleAddon) implements this quickstart as a playable, source-included project. Its [API Cookbook](../ExampleAddon/Docs/API-Cookbook.md), [Traditional Chinese guide](../ExampleAddon/Docs/API-Cookbook.zh-TW.md), templates, diagnostics, test matrix, five-language Workshop copy, and packaging scripts can be copied without depending on private core code.
