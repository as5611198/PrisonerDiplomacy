# Architecture Notes

Prisoner Diplomacy is a deterministic RimWorld 1.6 transaction system with optional presentation layers. The core owns every persistent transition. Integrations never infer success from a message or an AI response; they wait for a verified snapshot or state transition.

## Boundaries

- **Core:** records, deals, reward validation, release markers, payment guards, save migration, and faction memory.
- **Events:** staged follow-up events and neutral world-map handoffs. Event records are persisted independently from ordinary deals.
- **Strategic:** ceasefire and intelligence status, raid suppression, rescue, retaliation, and causal follow-ups.
- **AI:** asynchronous, cancellable narrative generation with optional, bounded advisory signals for live counteroffers. Requests are bound to a context and rejected when stale; accepted deals are never rewritten.
- **UI:** Verse `Window`/IMGUI controls and read-only snapshots. UI extensions cannot mutate game state.
- **API:** stable contracts for add-ons. Registration is version checked and duplicate IDs fail closed.

## Save safety

Persistent fields are exposed through the normal RimWorld `IExposable` path. New fields use conservative defaults when loading older saves. Compatibility repair removes or cancels only unrecoverable mod-owned state; it never rewrites vanilla Pawn ownership.

## Review checklist

When adding a feature, keep the mutation in the GameComponent, add a deterministic debug action or smoke assertion, persist the minimum state needed for reload, and expose read-only API data only after the transition is authoritative. See [`PrisonerDiplomacyApi.md`](../PrisonerDiplomacyApi.md) for the public extension contract.
