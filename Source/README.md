# Source Layout

The C# project keeps one `PrisonerDiplomacy` namespace while separating files by responsibility. The SDK-style project includes source files recursively, so moving a file does not change its public type name or add-on compatibility.

| Folder | Ownership |
| --- | --- |
| `AI/` | Optional narrative providers, prompt context, bounded roleplay notes, Persona providers, and opt-in advisory signals. The deterministic core remains authoritative. |
| `Api/` | Versioned public contracts, read-only snapshots, extension registry, and the minimal sample add-on. |
| `Compatibility/` | Harmony entry points, save repair, diagnostics, and compatibility extensions. |
| `Core/` | Authoritative prisoner records, deals, rewards, negotiation economy, faction memory, and the base GameComponent. |
| `Debug/` | Developer-mode actions and deterministic test helpers. |
| `Events/` | Persisted diplomacy events, event letters, neutral world-map trade points, and event state-machine code. |
| `Integration/` | Optional RimChat detection and isolation patches. |
| `Strategic/` | Ceasefires, intelligence, raids, causal follow-ups, rescue, and strategic consequences. |
| `UI/` | Windows, IMGUI theme/layout helpers, alerts, ransom letters, and UI extension points. |

Bootstrap and static configuration files remain directly under this directory: `PrisonerDiplomacyMod`, `PrisonerDiplomacyDefOf`, `PrisonerDiplomacySettings`, `PrisonerDiplomacyTuning`, and `AssemblyInfo`.

## Authoritative flow

`PrisonerDiplomacyGameComponent` is the only gameplay authority. UI, AI, RimChat, and add-ons may request or preview operations through public contracts, but only the core validates and mutates deals, Pawn ownership, payment, deadlines, event outcomes, and faction memory.

```text
UI / vanilla comms / optional integration
                 |
                 v
       PrisonerDiplomacyGameComponent
                 |
       validation -> state transition
                 |
       release confirmation -> fulfillment
```

Partial class files in `Core/`, `Events/`, `Strategic/`, `Compatibility/`, and `Debug/` intentionally share the same GameComponent type. They are split by responsibility for reviewability, not as separate runtime services.
