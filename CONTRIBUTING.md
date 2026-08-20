# Contributing

Contributions are welcome when they preserve Prisoner Diplomacy's deterministic transaction boundary and the repository's combined license terms.

## Before changing code

Read [`Source/README.md`](Source/README.md), [`Docs/Architecture.md`](Docs/Architecture.md), [`Docs/AddonQuickstart.md`](Docs/AddonQuickstart.md), and [`PrisonerDiplomacyApi.md`](PrisonerDiplomacyApi.md). Put new code in the responsibility folder that owns the behavior. Keep public types in the `PrisonerDiplomacy` namespace unless an API-major migration explicitly changes that contract.

## Gameplay changes

- Keep all deal, Pawn, payment, deadline, memory, and event mutation in `PrisonerDiplomacyGameComponent`.
- Keep UI, AI, RimChat, and add-on callbacks presentation-only or request-only.
- Persist the minimum state required to survive save/reload and use conservative defaults for older schemas.
- Add a deterministic developer action or smoke assertion for each new state transition.
- Add English and Traditional Chinese translation keys together with identical placeholders.

## Verification

```powershell
dotnet build .\PrisonerDiplomacy.csproj -c Release -t:Rebuild --nologo
```

Run the isolated smoke workflow documented in [`README.md`](README.md), then inspect the log for `PASS cases=127`, Prisoner Diplomacy exceptions, and missing Def warnings. Backend smoke does not prove visual behavior; manually test changed windows at relevant UI scales and resolutions, including the telemetry consent dialog.

Do not include API keys, private save files, local RimWorld preferences, or unrelated generated files in a pull request.
