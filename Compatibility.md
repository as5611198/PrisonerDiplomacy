# Prisoner Diplomacy Compatibility Matrix

The core mod has no compile-time dependency on optional race, visitor, combat, AI, or dialogue mods.

| Environment | Status | Notes |
| --- | --- | --- |
| RimWorld 1.6 + installed DLC | Verified | Deterministic smoke test passes. |
| RimChat 1.5.12 | Verified | Ransom ownership and duplicate-action guard are isolated and version-checked. |
| Humanoid Alien Races + Hospitality | Verified | Smoke test passes; special or excluded Pawns are skipped safely. |
| Combat Extended 1.6 build | Compatibility target | No compile-time dependency; test with the exact installed build. |
| Vanilla Factions Expanded / custom faction mods | Compatibility target | Use faction overrides when a faction should be non-negotiating or transactional. |
| Custom race mods | Compatibility target | Non-humanlike, temporary, summoned, and unstable-ID Pawns are excluded. Authors may use the compatibility DefModExtension. |
| Pawn Surrender / HostageTaker / Talk Before Bloodshed | Compatibility target | The mod observes resulting player prisoners and does not replace capture hooks. |

## Optional DefModExtension

Race or faction authors can opt out without a hard assembly reference when their load order permits the extension:

```xml
<modExtensions>
  <li Class="PrisonerDiplomacy.PrisonerDiplomacyPawnCompatibilityExtension">
    <ExcludeFromDiplomacy>true</ExcludeFromDiplomacy>
    <TemporaryPawn>true</TemporaryPawn>
    <ExclusionReason>summoned pawn</ExclusionReason>
  </li>
</modExtensions>
```

The faction extension supports `Automatic`, `NonNegotiating`, `Transactional`, and `Diplomatic` overrides.
