# Workshop Artwork

The two corrected 16:9 promotional covers are:

- `PrisonerDiplomacy-cover-en.png`: primary Steam Workshop cover and RimWorld `About/Preview.png` source.
- `PrisonerDiplomacy-cover-zh-TW.png`: Traditional Chinese second carousel image.

Both are promotional artwork rather than in-game screenshots. Place real negotiation, faction browser, Caravan exchange, and event-history screenshots after them in the Workshop carousel.

Original inputs are retained under `Source/`. Regenerate the corrected covers with:

```powershell
& .\Tools\GenerateWorkshopPreview.ps1
```

Artwork terms are defined in `ASSET-LICENSE.md` at the repository root.
