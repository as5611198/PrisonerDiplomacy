# Prisoner Diplomacy Known Issues

## Large mod lists

Very large mod lists can spend many minutes in RimWorld's external Def and asset initialization before any save is loaded. If the game becomes unresponsive before the Prisoner Diplomacy initialization line, inspect the log for the last external mod entry first. This is not evidence that a saved Prisoner Diplomacy deal failed.

## Optional external services

AI provider connectivity depends on the provider, endpoint, model, API Key, TLS configuration, rate limits, and local network. AI failures fall back to deterministic translated text and never block a deal; unavailable or stale advisory responses leave the deterministic counteroffer unchanged.

## Custom Pawn systems

Temporary, summoned, non-humanlike, quest-lodger, missing-GuestTracker, missing-PawnKind, unstable-ID, or non-negotiating-faction Pawns are intentionally excluded. This avoids creating an agreement for a Pawn that another mod cannot keep on the map.

## RimChat version changes

Unknown or changed RimChat versions enter safe isolation. Existing Prisoner Diplomacy deals remain owned by this mod and are not guessed, duplicated, or silently completed by the bridge.

## Workshop artwork

The 1.2 offline release-candidate code and deterministic smoke validation are complete. Steam Workshop publication is intentionally deferred until manual UI, Caravan world-map handoff, and large-mod-list checks are complete. Corrected English and Traditional Chinese promotional covers are stored under `Workshop/Artwork`; the English cover is also the RimWorld `About/Preview.png` image. Regenerate both covers with `Tools/GenerateWorkshopPreview.ps1` if the artwork changes.
