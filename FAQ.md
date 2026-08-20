# Prisoner Diplomacy FAQ

## Do I need a comms console?

The comms console is required for player-initiated negotiations. Enemy-initiated offers can still arrive without one, subject to the passive-offer setting and the normal contact delay.

## Does accepting a deal immediately pay the reward?

No. Acceptance only creates the agreement. The marked release command must be used, the prisoner must leave through the vanilla release flow, and the mod must verify the correct prisoner and map before delivery.

## What happens if the prisoner is still downed?

The normal three-day deadline can be extended by two days when the prisoner is incapacitated or has player-tendable injuries, but only after a player doctor has actually treated them. The same treatment cannot grant unlimited extensions.

## What happens if I disable a setting during an active deal?

Existing offers and accepted deals remain authoritative. Disabling enemy-initiated offers only blocks new passive offers. Disabling reserves or memory changes future calculations and does not rewrite completed history.

## Is AI required?

No. AI is disabled by default. Narrative text is optional, and a separate opt-in can allow bounded advisory signals for live counteroffers; the deterministic core still decides and validates every reward and action. The core mod works offline and without RimChat.

## Can I add a custom message during negotiation?

When AI narratives and external context transfer are enabled, the negotiation window accepts a short roleplay note. The note is used only as an emotional tone cue for the faction reply. It does not directly change the reward calculation, acceptance result, deadlines, or any other game state. If bounded AI negotiation adjustments are separately enabled, only a still-unaccepted counteroffer may be re-scaled inside deterministic caps after the response passes stale-context and reward validation.

## Why is a pirate deal risky?

Some transactional factions may disclose delayed payment, rescue, jailbreak, or ambush risks. The risk is deterministic, shown before release, and can sometimes be removed by accepting safer lower-reward terms.

## Can another mod add races, rewards, or events?

Yes. Prisoner Diplomacy 1.2 exposes a versioned extension API. Add-ons can register event definitions, race adapters, and special-reward metadata through `PrisonerDiplomacyExtensionRegistry`. The core still owns payment, release, Pawn state, deadlines, and transaction completion. See `PrisonerDiplomacyApi.md` for the compatibility contract and minimal example.

## What supplies can a faction offer?

The built-in list grows with faction technology: herbal medicine, pemmican, simple meals, and kibble are available at the low-tech tier; industrial factions can add industrial medicine, components, fine meals, survival meals, chemfuel, steel, and cloth; spacer factions can add glitterworld medicine, spacer components, lavish meals, plasteel, uranium, synthread, and hyperweave. Missing optional definitions are skipped safely.

Add-ons can also expose their own item as a special reward through a race or faction adapter. The item must be a stable, tradeable `ThingDef` with a positive market value, and the core validates its price, material cap, persistence, and exactly-once delivery. See [`Docs/RewardCatalog.md`](Docs/RewardCatalog.md) and [`Docs/AddonQuickstart.md`](Docs/AddonQuickstart.md).

## How do I report a problem?

Enable developer mode, open the `Prisoner Diplomacy` debug category, select `Copy diagnostic report`, and attach the copied text together with the exact save, deal state, and active mod list. Do not include API Keys.

## Where can I discuss the mod?

Join the QQ group [戰俘外交（Prisoner Diplomacy）模組討論群](https://qun.qq.com/universal-share/share?ac=1&authKey=kO4hgI4yAGKZaIMkgtwdF7V9G9aylRatK8pqb&busi_data=eyJncm91cENvZGUiOiIyMTE3ODQ2ODgiLCJ0b2tlbiI6InJMNDZ0VDd2RnhHSjhBbE51dVhQOUR6NTNhMlR4cjdhQUVNcmVlUzQybGJTMEg4MHd2ZGlxT1JLWXBYdDVNQXMiLCJ1aW4iOiIzODMxMDIzMDUwIn0%3D&data=ii_Z7GGfk0K0tX3nuOIWOG9w0Vt8TpomZx82ytn1-cooF1oRHAXYR8Nss77V5VBQER3K33djQUT_bNS6Lt1UXg&svctype=4&tempid=h5_group_info) (`211784688`).
