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

## Future add-on examples

The planned standalone Steam sample add-on will demonstrate a custom race persona, one special item reward, one translated event definition, registration from a `Mod` constructor, and a safe dependency on the public API. It will remain separate from the core so modders can inspect the smallest practical extension without copying internal transaction code.

See [`PrisonerDiplomacyApi.md`](../PrisonerDiplomacyApi.md), especially sections 10-14, for the version contract, adapter rules, event metadata, persona providers, and test checklist.
