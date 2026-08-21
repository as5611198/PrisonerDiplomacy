# Prisoner Diplomacy 1.2 API Cookbook

[English](API-Cookbook.md) | [繁體中文](API-Cookbook.zh-TW.md)

This is the canonical technical guide for the source-included Example Add-on. It describes the public API that is available in Prisoner Diplomacy 1.2.0. The complete working implementation is in [`../Source`](../Source).

## 1. Contract in one page

An add-on may:

- register stable extension, adapter, persona-provider, reward, and event-definition IDs;
- inspect the prisoner race, PawnKind, original faction Def, and faction technology;
- return a bounded diplomatic-value adjustment;
- expose fixed item rewards backed by valid `ThingDef` records;
- provide bounded narrative persona text;
- read documented prisoner, deal, faction, and event snapshots;
- request a read-only deterministic demand preview;
- draw read-only content in three negotiation-window regions.

An add-on must not:

- create, accept, edit, complete, or cancel a Prisoner Diplomacy deal;
- release, transfer, kill, recruit, or otherwise mutate a Pawn;
- spawn negotiated rewards or remove payment;
- edit faction reserves, memories, ceasefires, intelligence, or event records;
- reflect into `PrisonerDiplomacyGameComponent` or patch private transaction methods;
- treat generated narrative as proof that a gameplay transition occurred.

The core validates every demand, persists every accepted term, verifies the prisoner handoff, and delivers every reward exactly once.

## 2. Build and dependency setup

Target RimWorld 1.6 and .NET Framework 4.8. Reference RimWorld assemblies and the released core DLL, but never copy `PrisonerDiplomacy.dll` into the add-on.

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <LangVersion>7.3</LangVersion>
    <AssemblyName>MyPrisonerDiplomacyAddon</AssemblyName>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    <OutputPath>1.6\Assemblies\</OutputPath>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="Assembly-CSharp">
      <HintPath>$(RimWorldDir)\RimWorldWin64_Data\Managed\Assembly-CSharp.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="PrisonerDiplomacy">
      <HintPath>$(PrisonerDiplomacyRoot)\1.6\Assemblies\PrisonerDiplomacy.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>
</Project>
```

Declare the dependency and load order in `About/About.xml`:

```xml
<modDependencies>
  <li>
    <packageId>g1061.prisonerdiplomacy</packageId>
    <displayName>Prisoner Diplomacy</displayName>
    <steamWorkshopUrl>steam://url/CommunityFilePage/3787243156</steamWorkshopUrl>
  </li>
</modDependencies>
<loadAfter>
  <li>g1061.prisonerdiplomacy</li>
</loadAfter>
```

This is a required compile-time integration. Do not advertise the add-on as usable without the core unless you build a separate reflection-only compatibility assembly.

## 3. Register once from a Mod constructor

The Example Add-on registers three independent public surfaces:

```csharp
public ExampleAddonMod(ModContentPack content) : base(content)
{
    bool extensionRegistered = PrisonerDiplomacyExtensionRegistry.Register(
        new ExampleDiplomacyExtension());
    bool personaRegistered = PrisonerDiplomacyExtensionRegistry.RegisterPersonaProvider(
        new ExamplePersonaProvider());
    bool uiRegistered = PrisonerDiplomacyUiExtensionRegistry.Register(
        new ExampleHeaderUiExtension());
}
```

`Register` returns `false` for null objects, missing IDs, incompatible API versions, or duplicate IDs. Log the result during development. Never generate an ID from a translated label or a Pawn name.

Recommended ID scheme:

```text
author.package.feature
author.package.adapter.race-name
author.package.reward.item-name
author.package.persona.faction-name
author.package.ui.header-status
```

IDs become part of diagnostics and persisted records. Treat them as save-facing contracts and keep them stable after release.

## 4. Version compatibility

The installed core reports `PrisonerDiplomacyBackendApi.ApiVersion`. API 1.x accepts registrations whose declared version has the same major version and is not newer than the installed API.

The Example Add-on requires at least `1.2.0`:

```csharp
Version required = new Version("1.2.0");
Version current = new Version(PrisonerDiplomacyBackendApi.ApiVersion);
bool compatible = current.Major == required.Major && current >= required;
```

Fail closed when parsing fails or the major version differs. Do not catch a missing method and silently continue with partially registered gameplay content.

## 5. Extension and event-definition catalog

`IPrisonerDiplomacyExtension` groups event metadata and race/faction adapters:

```csharp
public interface IPrisonerDiplomacyExtension
{
    string ExtensionId { get; }
    string ApiVersion { get; }
    IEnumerable<PrisonerDiplomacyEventDefinition> GetEventDefinitions();
    IEnumerable<IPrisonerDiplomacyRaceAdapter> GetRaceAdapters();
}
```

The four public event families are:

| `PrisonerDiplomacyEventKind` | Core story family |
| --- | --- |
| `NeutralTradeCaravan` | Physical neutral handoff on the world map |
| `FalseSurrenderInfiltration` | Infiltration warning and jailbreak consequence |
| `PublicWarCrimeTrial` | Trial choice, execution verification, and goodwill effects |
| `RansomAmbushRetaliation` | Ambush and later retaliation chain |

Important limitation: API 1.2 registrations are discoverable catalog metadata only. They do not enqueue, schedule, choose, advance, or resolve an event. External code cannot call the internal scheduler/executor. `GetEventDefinitions()` is useful for documentation, compatibility discovery, and future-proof IDs.

If an add-on needs a completely custom event today, it must own its own `GameComponent`, save records, letters, incidents, retries, and outcomes. It may read Prisoner Diplomacy snapshots as context, but it must not write core event records or claim to be a core transaction.

## 6. Race and faction context adapters

`IPrisonerDiplomacyRaceAdapter` receives a read-only `PrisonerDiplomacyRaceContext`:

| Field | Meaning |
| --- | --- |
| `Prisoner` | Current Pawn reference; inspect only |
| `Faction` | The relevant original/negotiating faction; inspect only |
| `RaceDefName` | Race `ThingDef` name, such as `Human` |
| `PawnKindDefName` | PawnKind Def name |
| `FactionDefName` | Faction Def name, such as `Empire` |
| `FactionTechLevel` | Vanilla `TechLevel` from the faction Def |

Filter narrowly in `AppliesTo`:

```csharp
public bool AppliesTo(PrisonerDiplomacyRaceContext context)
{
    return context != null
        && context.Prisoner != null
        && context.Faction != null
        && context.RaceDefName == "MyDragonRace";
}
```

The value adjustment is an integer added to the core diplomatic valuation. Each adapter result is clamped to `-1000..1000`; multiple applicable adapters are summed. Keep the adjustment explainable and small enough that a player cannot bypass the core economy.

```csharp
public int GetDiplomaticValueAdjustment(PrisonerDiplomacyRaceContext context)
{
    return context.FactionDefName == "MyDragonEmpire" ? 80 : 30;
}
```

The adapter may be called more than once while UI and previews are open. It must be fast, deterministic, side-effect free, and safe when `context` is null during catalog discovery.

## 7. Special item rewards

Return fixed metadata from the adapter:

```csharp
yield return new PrisonerDiplomacySpecialRewardDefinition(
    "author.dragon.reward.ember-core",
    "DragonAddon_EmberCoreLabel",
    "DragonAddon_EmberCoreDescription",
    "DragonAddon_EmberCore",
    1);
```

Constructor fields:

| Field | Contract |
| --- | --- |
| `RewardId` | Stable, globally unique ID |
| `LabelKey` | Keyed translation key shown in the negotiation UI |
| `DescriptionKey` | Keyed tooltip/description key |
| `RequiredThingDefName` | Stable item `ThingDef.defName` |
| `MinimumCount` | Fixed offered count, clamped to at least 1 |

The required ThingDef must resolve to a valid item with a positive stack limit and positive market value. Prisoner Diplomacy uses the current market value and count for budget/material-cap validation. It persists the chosen reward and creates the stack only after the verified release workflow. The issuing faction does not need a physical stockpile.

Do not call `ThingMaker`, `GenSpawn`, `Destroy`, inventory reservation, random APIs, or network services from an adapter. Do not return weapons, apparel, buildings, quest-only objects, zero-value items, or generated objects with unstable Def identities.

The Example Add-on demonstrates context-sensitive availability:

- Neolithic through Medieval factions: two `PDX_DiplomaticSeal` items, market value 90 each.
- Industrial and higher factions: one `PDX_EncryptedDiplomaticLedger`, market value 600.

The null-context catalog call should return every reward definition the adapter may expose. A real context call should return only rewards legal for that prisoner and faction.

## 8. Persona providers

Personas are bounded, untrusted narrative hints:

```csharp
public sealed class DragonPersonaProvider : IPrisonerDiplomacyPersonaProvider
{
    public string ProviderId => "author.dragon.persona.default";
    public string ApiVersion => PrisonerDiplomacyApi.ApiVersion;

    public bool AppliesTo(PrisonerDiplomacyRaceContext context)
    {
        return context?.RaceDefName == "MyDragonRace";
    }

    public string GetPersona(PrisonerDiplomacyRaceContext context)
    {
        return "proud, possessive, formal, status-conscious, and protective of clan honor";
    }
}
```

The core normalizes the returned string and uses the first applicable provider by stable provider-ID order. Persona text can affect wording only. It cannot change silver, materials, goodwill, ceasefire duration, intelligence, acceptance chance, deadlines, or event outcomes.

Keep it short. Do not include API keys, player file paths, raw prompts, instructions to ignore rules, or claims that a reward was paid.

## 9. Read-only backend API

The public static entry point is `PrisonerDiplomacyBackendApi`:

| Method | Result |
| --- | --- |
| `GetRegisteredExtensionIds()` | Stable registered extension IDs |
| `GetEventDefinitions()` | Built-in and add-on event metadata catalog |
| `GetSpecialRewardOptions(Pawn, Faction)` | Rewards applicable to this context |
| `GetEventSnapshots()` | Persisted core event snapshots |
| `GetDiplomaticValueAdjustment(Pawn, Faction)` | Combined clamped adapter adjustment |
| `GetPrisonerSnapshots(Map)` | Public prisoner snapshots for a map |
| `GetFactionSnapshots(Map)` | Public faction/contact snapshots for a map |
| `TryGetPrisonerSnapshot(Pawn, Map, out ...)` | One prisoner snapshot |
| `TryGetActiveDealSnapshot(Pawn, out ...)` | Current active deal, if any |
| `PreviewDemand(Pawn, Pawn, RewardDemand, out string)` | Deterministic read-only evaluation |

Snapshot objects expose references such as `Pawn` and `Faction` for identity and presentation. The properties are read-only, but RimWorld objects themselves are mutable. Treat every referenced game object as inspect-only.

`PreviewDemand` runs the core evaluator without starting a negotiation, consuming reserve, applying cooldown, or writing a deal. The result is still contextual and can become stale immediately. Never use it as authorization to spawn a reward or release a Pawn.

## 10. Read-only UI extensions

Register an `IPrisonerDiplomacyUiExtension` separately. Three regions are available:

| Region | Intended content |
| --- | --- |
| `FactionHeader` | Compact faction/race context |
| `PrisonerSummary` | Small read-only prisoner detail |
| `NegotiationBody` | Bounded explanatory or compatibility content |

The core calls `GetHeight` first and then gives the extension exactly that height:

```csharp
public float GetHeight(
    PrisonerDiplomacyUiRegion region,
    PrisonerDiplomacyUiContext context,
    float width)
{
    return region == PrisonerDiplomacyUiRegion.FactionHeader
        && context?.Prisoner?.Pawn != null ? 28f : 0f;
}

public void Draw(
    PrisonerDiplomacyUiRegion region,
    Rect rect,
    PrisonerDiplomacyUiContext context)
{
    Widgets.Label(rect, "Read-only add-on context");
}
```

`PrisonerDiplomacyUiContext` contains faction, prisoner, and deal snapshots plus `CompactLayout`. Return `0` for irrelevant regions. Keep the height stable, clip/shorten long text, restore `GUI.color`, `Text.Font`, and `Text.Anchor`, and avoid buttons that mutate transaction state. Exceptions are isolated and logged, but a broken extension still degrades the player experience.

The internal core theme is not public API. Draw your own add-on theme rather than referencing internal colors or methods.

## 11. Localization

Use Keyed translations for reward/event/UI text and DefInjected translations for custom items. Ship the same keys and placeholders in every supported language.

```xml
<LanguageData>
  <DragonAddon_EmberCoreLabel>ember core</DragonAddon_EmberCoreLabel>
  <DragonAddon_EmberCoreDescription>A clan-bound diplomatic artifact.</DragonAddon_EmberCoreDescription>
</LanguageData>
```

Namespace keys and Def names. Do not reuse the Example Add-on's `PDX_` prefix in a new published project.

## 12. Diagnostics and testing

The installed Example Add-on includes:

- **Mod Settings > Prisoner Diplomacy: Example Add-on > Open read-only API inspector**;
- an Overview tab showing registration IDs and catalogs;
- Prisoners, Factions, and Events snapshot tabs;
- a copyable full report;
- developer actions for a full report, one prisoner snapshot, a 250-silver preview, and adapter context.

These tools are read-only. The core's own developer actions remain the correct way to create deterministic test prisoners, force offers, advance core events, and exercise exactly-once delivery.

Run [`TestGuide.md`](TestGuide.md) before release. The repository scripts validate XML, localization parity, texture references, the authority boundary, output contents, and Workshop artwork.

## 13. Failure model

The registry rejects duplicate or incompatible registrations. Adapter `AppliesTo`, reward enumeration, and persona calls are isolated so an exception fails closed and produces a warning. UI extension exceptions are isolated per extension.

Your add-on should still validate at startup, return empty collections instead of null where practical, avoid throwing from normal non-matching contexts, and log concise IDs rather than private player data.

## 14. Choosing the right extension design

| Goal | Recommended surface |
| --- | --- |
| Different value for one race/faction | Race adapter |
| One fixed cultural item reward | Race adapter + ThingDef |
| Different AI narrative voice | Persona provider |
| Show compatibility/context in negotiation UI | Read-only UI extension |
| Inspect current core state | Backend snapshots |
| Estimate a legal demand | `PreviewDemand` |
| Advertise support for a core event family | Event-definition metadata |
| Implement a completely new event state machine | Add-on-owned component; snapshots only |
| Change deal acceptance, payment, or release | Not supported; propose a new public core API |

When the public API does not expose a mutation, that is an authority boundary, not an invitation to use reflection. Open an issue with a concrete use case and required invariants instead.

## 15. Copy points

- [`../Templates/MinimalExtension.cs`](../Templates/MinimalExtension.cs): registration and version guard.
- [`../Templates/CustomRaceFactionAdapter.cs`](../Templates/CustomRaceFactionAdapter.cs): race/faction value and special reward.
- [`../Templates/PersonaProvider.cs`](../Templates/PersonaProvider.cs): narrative persona.
- [`../Templates/ReadOnlyUiExtension.cs`](../Templates/ReadOnlyUiExtension.cs): bounded UI region.
- [`../Source`](../Source): full playable implementation and diagnostics.

Start by changing every package ID, namespace, Def name, translation prefix, and stable API ID. Then remove features you do not need. A small deterministic adapter is safer than copying the whole sample unchanged.
