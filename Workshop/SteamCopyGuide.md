# Prisoner Diplomacy Steam Copy Guide

This file contains the short copy, screenshot order, and final publication checks that sit around the full BBCode descriptions.

## Description file

Paste one of the following files directly into the Steam Workshop description field. They already use Steam BBCode and include the current release, feature boundaries, privacy notice, GitHub link, QQ group, and authorship statement. Keep the full compatibility report in [`Compatibility.md`](../Compatibility.md) as the canonical matrix; the Steam description should stay concise and link back to the repository:

- International page: `Workshop/SteamDescription.en.txt`
- Traditional Chinese page: `Workshop/SteamDescription.zh-TW.txt`
- Simplified Chinese page: `Workshop/SteamDescription.zh-CN.txt`
- Japanese page: `Workshop/SteamDescription.ja.txt`
- Korean page: `Workshop/SteamDescription.ko.txt`

Use the English file as the default Workshop description for the English cover. Use one localized file when publishing a language-specific community presentation. Steam provides one description field per Workshop item, so do not paste all five versions unless you intentionally want a long multilingual page. `WorkshopDescription.md` is the longer Markdown document for GitHub and documentation; do not paste it into Steam unchanged.

The GitHub documentation entry for new players is [`Docs/PlayerGuide/README.md`](../Docs/PlayerGuide/README.md). It routes to separate Traditional Chinese, English, Simplified Chinese, Japanese, and Korean walkthroughs. Link to the matching page when answering Workshop questions instead of pasting the full tutorial into a comment.

## Workshop title

English storefront title:

`Prisoner Diplomacy`

Traditional Chinese presentation title:

`Prisoner Diplomacy｜戰俘外交`

Keep the English title as the Workshop item name unless Steam later supports a separate localized item title for the page.

## Short description

English:

> Turn captured enemies into ransom deals, hostage exchanges, ceasefires, intelligence, and lasting faction consequences. Negotiate through the vanilla comms console, honor the handoff, and live with what each faction remembers.

Traditional Chinese:

> 將敵方俘虜轉化為贖金、換俘、停火、情報與長期派系後果。透過原版通訊台交涉、完成實際交接，並承擔各派系對你留下的記憶。

## One-line promotional copy

English:

> Every prisoner has a value. Every agreement has a consequence.

Traditional Chinese:

> 每一名俘虜都有價值，每一份協議都有後果。

## Recommended image order

1. English cover: `Workshop/Artwork/PrisonerDiplomacy-cover-en.png`
2. Main negotiation workspace: `About/螢幕擷取畫面 2026-08-20 172120.png`
3. Counteroffer workflow: `About/螢幕擷取畫面 2026-08-20 172414.png`
4. Strategic agreement review: `About/螢幕擷取畫面 2026-08-20 172250.png`
5. Add a neutral world-map exchange screenshot after the final manual Caravan test.
6. Add one settings or consent-dialog screenshot now that the production telemetry receiver is verified, after the final visual QA pass.

The Chinese cover can be used as the second promotional image for Chinese community posts. The Workshop preview remains the English cover so the mod is immediately identifiable in an international mod list.

## Screenshot captions

Main negotiation workspace:

- EN: `Build a demand from silver, supplies, goodwill, ceasefire, intelligence, and adapter-defined rewards.`
- ZH-TW: `從白銀、物資、好感、停火、情報與 Adapter 特殊報酬中組合交涉條件。`

Counteroffer workflow:

- EN: `Accept the faction's offer, revise the terms, or end negotiations before the deadline.`
- ZH-TW: `接受派系還價、送出修訂條件，或在期限前結束交涉。`

Agreement review:

- EN: `Review ceasefire time, faction memory, transaction history, and extension events even when no prisoner remains.`
- ZH-TW: `即使已無俘虜，仍可查看停火時間、派系記憶、交易歷史與擴展事件。`

## Telemetry wording boundary

The public description may state that reports are optional, consent-gated, sanitized, asynchronous, and unable to modify the player's installation. The production receiver is now verified for the release candidate:

- `ProductionReportEndpoint` contains the final HTTPS endpoint.
- Worker schema validation, payload limits, rate limiting, and event-id deduplication are deployed.
- D1/R2 retention and deletion policy is documented.
- The privacy text matches the exact production payload fields.
- A real opt-in test report reaches the receiver without exposing save names, Pawn names, local paths, or secrets.
- The AI repair pipeline produces isolated repair candidates only; it cannot push directly to the release branch or modify a player's game.

Public descriptions may therefore describe the receiver as live and optional. Production AI triage and repair remain disabled until their separate provider credentials and rollout checks are complete.

## Final upload pass

- Replace release-candidate wording with the confirmed public release status.
- Confirm the displayed version and save schema.
- Confirm all five localization files have the same key set and placeholders.
- Run the final release build and isolated smoke test with the exact Workshop payload.
- Run `pwsh -NoProfile -ExecutionPolicy Bypass -File .\Tools\ValidateWorkshopDescriptions.ps1` before submitting the item update.
- Confirm `About/Preview.png` is strictly smaller than 1 MB; RimWorld's in-game uploader returns `LimitExceeded` for a larger preview even when the mod content upload succeeds.
- Complete the real neutral trade-point Caravan handoff.
- Capture the final telemetry consent dialog at the longest localization.
- Add the Workshop URL, source repository URL, and issue-report URL once public.
- Verify image order, captions, credits, licenses, and the Harmony dependency on the uploaded page.
