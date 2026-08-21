# Reward Catalog

Prisoner Diplomacy has two separate reward paths. Core faction supplies are the stable, built-in choices available from a faction's technology tier. Special rewards are opt-in metadata supplied by an add-on adapter and validated by the core.

## Core faction supplies

The negotiation list is intentionally restricted to useful, tradeable items with stable vanilla `ThingDef` identities. The faction's technology tier controls which group is available:

| Faction tier | Built-in supplies |
| --- | --- |
| Neolithic and above | Herbal medicine, pemmican, simple meals, kibble |
| Industrial and above | Industrial medicine, industrial components, fine meals, packaged survival meals, chemfuel, steel, cloth |
| Spacer and above | Glitterworld medicine, spacer components, lavish meals, plasteel, uranium, synthread, hyperweave |

Unavailable DLC or optional-mod definitions are skipped safely. A missing `ThingDef` never appears as a selectable supply. The faction does not need to physically own the item; the deterministic negotiation reserve and material-cap rules price and validate the promise, while fulfillment creates the agreed stack only after the prisoner has safely completed the release workflow.

The list is deliberately not an unrestricted `ThingDef` browser. Weapons, apparel, buildings, quest objects, unstable generated items, and items with no positive market value are excluded so a faction cannot promise an invalid or exploit-prone object.

## Special item rewards from add-ons

Any compatible race, faction, or content mod can expose a fixed special item reward through `IPrisonerDiplomacyRaceAdapter`:

```csharp
public IEnumerable<PrisonerDiplomacySpecialRewardDefinition> GetSpecialRewards(
    PrisonerDiplomacyRaceContext context)
{
    if (context?.RaceDefName != "MyDragonRace")
    {
        yield break;
    }

    yield return new PrisonerDiplomacySpecialRewardDefinition(
        "myaddon.dragon.ember_core",
        "MyAddon_EmberCoreLabel",
        "MyAddon_EmberCoreDescription",
        "MyAddon_EmberCore",
        1);
}
```

The add-on owns the definition and translation keys. Prisoner Diplomacy then:

- resolves the requested `ThingDef` by stable def name;
- requires an item category, positive stack limit, and positive market value;
- checks that the adapter applies to the current prisoner and faction;
- includes the item cost in budget and material-cap checks;
- persists the reward ID, def, and count across saves;
- creates and delivers the item exactly once after verified prisoner release.

Adapters must be deterministic and side-effect free. They must not spawn, remove, destroy, or reserve things, mutate Pawns or factions, call the network, or depend on random state. A bad adapter, invalid def, duplicate ID, incompatible API version, or stale context fails closed and cannot mutate a transaction.

## Current sample reward

The core repository contains a developer-only sample add-on entry:

- ID: `sample.energy_core`
- Display: Industrial component cache
- Item: 3 `ComponentIndustrial`
- Availability: only while `Rewards: toggle debug special reward` is enabled

This sample exists for adapter and exactly-once fulfillment testing. It is not intended to be a permanent balance reward in ordinary play.

## Standalone playable example

The standalone [`ExampleAddon`](../ExampleAddon) demonstrates two real custom items, technology-aware availability, vanilla race/faction value adjustments, three persona families, four event-definition metadata records, a read-only UI extension, an API Inspector, developer diagnostics, and five-language localization. It remains separate from the core so players can subscribe optionally and modders can inspect or copy it without using internal transaction code.

- `PDX_DiplomaticSeal`: 2 items for Neolithic through Medieval factions, market value 90 each.
- `PDX_EncryptedDiplomaticLedger`: 1 item for Industrial or higher factions, market value 600.

The core still prices, validates, persists, and delivers these items exactly once. The Example Add-on only supplies metadata and ThingDefs.

See [`PrisonerDiplomacyApi.md`](../PrisonerDiplomacyApi.md), especially sections 10-15, and the Example Add-on [API Cookbook](../ExampleAddon/Docs/API-Cookbook.md) for the version contract, adapter rules, event metadata, persona providers, UI extensions, and test checklist.
