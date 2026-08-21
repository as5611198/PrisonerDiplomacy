# Steam upload guide

## Positioning

This is a separate playable SDK/reference add-on. It is not required to use Prisoner Diplomacy and should not be bundled into the core Workshop item.

## First upload

1. Build, validate, and deploy with `Tools/DeployToRimWorld.ps1 -Clean`.
2. Confirm `ExampleAddon/About/PublishedFileId.txt` does not exist.
3. Start RimWorld, select **Prisoner Diplomacy: Example Add-on**, and use the in-game Workshop uploader to create a new item.
4. Upload it as **Hidden**.
5. Use `About/Preview.png` for the preview. The validator enforces 860x480 and less than 1 MB.
6. On the Workshop page, add [Prisoner Diplomacy](https://steamcommunity.com/sharedfiles/filedetails/?id=3787243156) as a required item.
7. Add suitable tags: `Mod`, `1.6`, `Translation`, and `Utilities` or the closest current Workshop choices. Do not imply this is a race/content framework independent of the core.
8. Fill each language field independently using the title/file map in `README.md`.
9. Subscribe to the hidden item from a clean test profile and verify load order, registration, translations, textures, and the read-only inspector.
10. Make the item Public only after the hidden-item test passes.

Steam creates `About/PublishedFileId.txt` after the first successful upload. Keep that local file for future updates, but do not copy the core ID and do not commit it as part of the reusable source template.

## Screenshot order

1. English cover.
2. Negotiation Header strip and special reward selector.
3. API Inspector Overview tab.
4. Low-tech diplomatic seal and industrial encrypted ledger in game.
5. Developer actions / copied diagnostic report.
6. GitHub Cookbook and Templates page.

The cover is enough for the first hidden upload; add real UI screenshots before public promotion so players can see what the installed add-on changes.

## Update workflow

1. Bump `About/About.xml`, `Source/AssemblyInfo.cs`, and Workshop changelog together.
2. Run `Tools/Package.ps1` and record the ZIP SHA-256.
3. Deploy and run the load smoke plus the relevant manual test rows.
4. Confirm the local `PublishedFileId.txt` points to this Example Add-on item.
5. Upload through the in-game uploader.
6. Recheck required-item metadata and all localized descriptions after Steam finishes processing.

## Release links

- Core Workshop item: https://steamcommunity.com/sharedfiles/filedetails/?id=3787243156
- Source and API docs: https://github.com/as5611198/PrisonerDiplomacy/tree/main/ExampleAddon
- Canonical API guide: https://github.com/as5611198/PrisonerDiplomacy/blob/main/PrisonerDiplomacyApi.md
