# Copyable templates

These files are intentionally excluded from the Example Add-on build. Copy only the surfaces your add-on needs, then replace every `YourAuthor`, `YourAddon`, package ID, Def name, translation key, and stable API ID.

| File | Use it when |
| --- | --- |
| `MinimalExtension.cs` | You need registration, version checks, and event metadata |
| `CustomRaceFactionAdapter.cs` | A race/faction changes value or adds a special item |
| `PersonaProvider.cs` | A race/faction needs a distinctive narrative voice |
| `ReadOnlyUiExtension.cs` | You need a compact read-only negotiation UI contribution |
| `YourAddon.csproj.template` | You need a standalone RimWorld 1.6 project |
| `About.xml.template` | You need the correct core dependency/load order |

Do not copy the Example Add-on package ID. Do not package `PrisonerDiplomacy.dll` with your add-on. Read [`../Docs/API-Cookbook.md`](../Docs/API-Cookbook.md) before adding any stateful behavior.
