# Release Checklist

This checklist separates repository and package checks from the final in-game acceptance pass. A green automated check is package/load evidence; it is not a substitute for clicking through the UI or completing a world-map event in a real save.

## Completed for the 1.2.0 candidate

- [x] Release build completes with zero warnings and zero errors.
- [x] Deployed `1.6/Assemblies/PrisonerDiplomacy.dll` matches the release build hash.
- [x] Isolated `-quicktest -pdsmoketest` reaches `PASS cases=127`, including the offline telemetry contract self-test.
- [x] Smoke log has no `SmokeTest FAIL`, Prisoner Diplomacy exception, or missing required Def.
- [x] English, Traditional Chinese, Simplified Chinese, Japanese, and Korean keyed files each contain 570 keys with matching placeholders and newline markers (`Tools/ValidateLocalization.ps1`).
- [x] About, Def, and language XML files parse successfully.
- [x] Source is organized by responsibility and contains no remaining `Widgets.ButtonText` calls.
- [x] Apache 2.0 source notice, Non-Commercial Exception, and CC BY-NC-ND 4.0 asset notice are present.
- [x] English and Traditional Chinese promotional covers are prepared under `Workshop/Artwork`; the English cover is synchronized to `About/Preview.png` and can be regenerated with `Tools/GenerateWorkshopPreview.ps1`.

## Required author acceptance before Workshop upload

- [ ] Open the negotiation window from the vanilla comms console with active prisoner cases.
- [ ] Open the persistent faction browser when a faction has no prisoners; verify agreements, history, and event tabs remain usable.
- [ ] Click every themed action button, including submit, revised terms, accept, reject, close, and end negotiation.
- [ ] Verify long Traditional Chinese labels, warning markers, input rows, and bottom evaluation text at UI scales 1.0, 1.25, and 1.5.
- [ ] Verify a low-resolution layout has no clipped text or unnecessary vertical scrollbar.
- [ ] Complete at least one mixed-reward deal and one counteroffer in a real save.
- [ ] Complete a neutral world-map exchange with a real Caravan, including arrival, prisoner handoff, and reward fulfillment.
- [ ] Exercise a staged follow-up event and confirm the choice letter, retry state, and final consequence.
- [ ] Test a clean save and a migrated older save with the intended full mod list.
- [ ] Inspect the final Workshop page, screenshots, credits, and load-order instructions before publishing.

## Upload gate

Upload only after every item in the author acceptance section is checked. Keep the smoke log, build hash, and final manual-test notes with the release record.
