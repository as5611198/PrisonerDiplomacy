# Prisoner Diplomacy Steam Copy Guide

This file contains the short copy, screenshot order, and final publication checks that sit around the full BBCode descriptions.

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
6. Add one settings or consent-dialog screenshot only after the production telemetry receiver is verified.

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

The public description may state that reports are optional, consent-gated, sanitized, asynchronous, and unable to modify the player's installation. It must not claim that the production receiver is live until all of the following are confirmed:

- `ProductionReportEndpoint` contains the final HTTPS endpoint.
- Worker schema validation, payload limits, rate limiting, and event-id deduplication are deployed.
- D1/R2 retention and deletion policy is documented.
- The privacy text matches the exact production payload fields.
- A real opt-in test report reaches the receiver without exposing save names, Pawn names, local paths, or secrets.
- The AI repair pipeline produces isolated repair candidates only; it cannot push directly to the release branch or modify a player's game.

Until those checks pass, the repository release candidate should continue to describe telemetry as offline or pending receiver configuration.

## Final upload pass

- Replace release-candidate wording with the confirmed public release status.
- Confirm the displayed version and save schema.
- Confirm all five localization files have the same key set and placeholders.
- Run the final release build and isolated smoke test with the exact Workshop payload.
- Complete the real neutral trade-point Caravan handoff.
- Capture the final telemetry consent dialog at the longest localization.
- Add the Workshop URL, source repository URL, and issue-report URL once public.
- Verify image order, captions, credits, licenses, and the Harmony dependency on the uploaded page.
