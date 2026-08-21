# Prisoner Diplomacy Beginner Guide

[繁體中文](PlayerGuide.zh-TW.md) | **English** | [简体中文](PlayerGuide.zh-CN.md) | [日本語](PlayerGuide.ja.md) | [한국어](PlayerGuide.ko.md)

Prisoner Diplomacy turns captured humanlike enemies into ransom, exchange, and faction-diplomacy opportunities. AI is optional. All payments and gameplay outcomes are decided by the mod's deterministic transaction system.

## Five-step quick start

1. Capture a living humanlike pawn from an eligible non-player faction.
2. Build and power a vanilla comms console, then have a capable colonist contact the prisoner's faction. You may also answer an incoming faction letter.
3. Select a prisoner, choose up to three reward types, and submit your terms.
4. After the faction accepts, select that exact prisoner and use **Release for ransom** or **Release for prisoner exchange**.
5. Let a vanilla warden escort the prisoner safely off the assigned map. Payment is delivered only after the departure is verified.

> **Important:** Accepting terms creates an agreement; it does not pay immediately. Ordinary **Release** is not a substitute for the dedicated release command.

## Requirements and load order

- RimWorld 1.6
- Harmony loaded before Prisoner Diplomacy
- No AI provider or RimChat installation is required

Existing saves are supported, but back up an important colony before adding or updating any mod. See the [compatibility report](../../Compatibility.md) before combining major prisoner, faction, race, or quest overhauls.

## Which prisoners are eligible?

An eligible target is alive, humanlike, currently a prisoner of your colony, and belongs to a visible non-player faction that can negotiate. Temporary quest lodgers, non-humanlike pawns, defeated or hidden factions, and pawns explicitly excluded by a compatibility add-on are ignored. Permanent-enemy factions are treated according to the mod setting and usually negotiate transactionally.

If a prisoner is absent from the case list, confirm their prisoner status and original faction first.

## Ways to open the system

- **Incoming letter:** enemy-initiated offers can arrive even without a comms console, if passive offers are enabled.
- **Vanilla comms console:** the complete entry point for starting your own negotiations.
- **Portable diplomacy terminal:** a colonist carrying, wearing, or equipping `PD_PortableDiplomacyTerminal` receives a gizmo that opens known faction contacts.
- **Active-agreement alert:** reopens the interface even when no eligible prisoners remain, so ceasefire, intelligence, and pending obligations can still be reviewed.

## Negotiation window

The left side lists prisoner cases. Choose a faction and prisoner before editing terms. The main tabs are:

- **Cases:** prisoners, faction context, terms, and counteroffers.
- **Agreements:** accepted deals, deadlines, ceasefires, intelligence, and treatment credit.
- **History:** completed, rejected, expired, and failed negotiations.
- **Events:** extension-event state, stage, source deal, retries, and intermediary.

![Negotiation workspace](<../../About/螢幕擷取畫面 2026-08-20 172120.png>)

![Agreements tab](<../../About/螢幕擷取畫面 2026-08-20 172250.png>)

## Rewards and limits

A validated deal may combine **at most three reward types**:

- silver;
- supplies appropriate to the faction's technology and resources;
- faction goodwill;
- one kidnapped colonist returned through prisoner exchange;
- a temporary ceasefire;
- one-use early-warning intelligence;
- a special item reward registered by a compatible add-on.

The supply selector shows each item's unit market value and the current total, helping you choose a sensible quantity. Faction reserves, material caps, inventory rules, and the prisoner's diplomatic value still limit the final offer.

## Terms and counteroffers

Choose ransom or prisoner-exchange mode, configure the reward rows, then select **Submit terms**. A faction may accept, reject, or counter. During a counteroffer, you can accept it, revise the reward rows, use a silver shortcut, or end the negotiation. A final counteroffer can only be accepted or rejected.

![Counteroffer workflow](<../../About/螢幕擷取畫面 2026-08-20 172414.png>)

An accepted deal reserves the prisoner and starts a deadline. Read disclosed pirate risks before accepting a high-value deal.

## Completing the release correctly

1. Open the prisoner's **Orders** tab after the deal is accepted.
2. Choose **Release for ransom** or **Release for prisoner exchange**.
3. Keep the prisoner alive, reachable, and on the map assigned to the deal.
4. Wait for a vanilla warden to escort them to the map edge.
5. The mod verifies the correct pawn and departure before delivering payment or returning the kidnapped colonist.

If the prisoner is downed, a medical deadline extension may be granted only after a player doctor actually treats a qualifying condition. It cannot be repeated indefinitely.

## What can invalidate a deal?

An accepted deal may fail if the prisoner dies, escapes, is recruited, enslaved, sold or transferred, leaves the wrong way, is released with the ordinary command, misses the deadline, or is no longer the correct pawn on the assigned map. These outcomes may also affect faction memory and future negotiations.

## Agreements, history, and strategy

The **Agreements** tab shows remaining deal deadlines and strategic effects. A ceasefire suppresses only eligible proactive hostile actions from that faction; attacking them can break it. Early-warning intelligence is consumed by the next eligible threat it detects. The **History** and **Events** tabs remain available after a faction has no prisoners, through the active-agreement alert or another valid entry point.

## Neutral exchange points and special events

Some follow-up stories arise from prisoner treatment, agreements, faction memory, pirate risks, or event add-ons. They can include rescue pressure, jailbreak or infiltration attempts, public trials, retaliation, and neutral exchange proposals.

For a neutral world-map exchange, accept the proposal, form a normal caravan carrying the specified prisoner, and travel to the marked exchange point. Arrival validates the prisoner and active deal before the handoff. Do not use the colony release command for this route unless the event explicitly directs you to do so.

## Optional AI

AI narratives are disabled by default. When enabled, AI may write faction-flavored replies using persona, ideology, relationship, injury, and sanitized player-note context. Text alone never creates payment or changes a Pawn. The separate bounded-advisory option may influence only an unaccepted counteroffer, and the deterministic core clamps and validates every result.

## Troubleshooting

- **No faction in the comms menu:** verify that the console is powered, the negotiator can use it, and the faction has an eligible prisoner and can negotiate.
- **No payment after accepting:** use the dedicated release command and wait until the correct prisoner safely leaves the assigned map.
- **Prisoner vanished from Cases:** open the **Agreements**, **History**, or **Events** tab through the active-agreement alert.
- **Neutral exchange will not complete:** the caravan must carry the specified prisoner and the linked deal/event must still be active.
- **RimChat or another ransom mod is installed:** do not negotiate the same prisoner through two systems. Check the selected transaction owner in Mod Settings.
- **Unexpected error:** enable developer mode and use **Prisoner Diplomacy > Copy diagnostic report**. Include the copied report, exact action, deal state, save, and mod list. Never share an API key.

More help: [FAQ](../../FAQ.md) | [Compatibility report](../../Compatibility.md) | [Known issues](../../KnownIssues.md) | [Telemetry privacy](../TelemetryPrivacy.md)
