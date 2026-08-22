using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace PrisonerDiplomacy
{
    public sealed partial class PrisonerDiplomacyGameComponent
    {
        private CompatibilityRepairSummary lastRepairSummary = new CompatibilityRepairSummary();

        public CompatibilityRepairSummary LastRepairSummary => lastRepairSummary;

        public CompatibilityRepairSummary RunCompatibilityRepair()
        {
            lastRepairSummary = RepairCompatibilityData(true);
            return lastRepairSummary;
        }

        private CompatibilityRepairSummary RepairCompatibilityData(bool logChanges)
        {
            CompatibilityRepairSummary summary = new CompatibilityRepairSummary();
            int now = Find.TickManager?.TicksGame ?? 0;
            records = records ?? new List<PrisonerRecord>();
            deals = deals ?? new List<PrisonerDeal>();
            factionNegotiationMemories = factionNegotiationMemories ?? new List<FactionNegotiationMemory>();
            commTargets = commTargets ?? new List<PrisonerDiplomacyCommTarget>();
            aiNarratives = aiNarratives ?? new List<AiNarrativeRecord>();
            factionStrategicStates = factionStrategicStates ?? new List<FactionStrategicState>();
            strategicFollowups = strategicFollowups ?? new List<StrategicFollowupEvent>();
            diplomacyEvents = diplomacyEvents ?? new List<PrisonerDiplomacyEventRecord>();

            summary.RemovedRecords += records.RemoveAll(record => record == null
                || (record.Pawn == null && string.IsNullOrEmpty(record.PawnLoadId)
                    && record.OriginalFaction == null));
            summary.RemovedDeals += deals.RemoveAll(deal => deal == null
                || (string.IsNullOrEmpty(deal.DealId)
                    && deal.Prisoner == null
                    && string.IsNullOrEmpty(deal.PrisonerLoadId)));

            foreach (PrisonerRecord record in records.ToList())
            {
                try
                {
                    if (record.Pawn != null)
                    {
                        string loadId = record.Pawn.GetUniqueLoadID();
                        if (!string.Equals(record.PawnLoadId, loadId, StringComparison.Ordinal))
                        {
                            record.PawnLoadId = loadId;
                            summary.RebuiltLinks++;
                        }

                        if (record.OriginalFaction == null)
                        {
                            record.OriginalFaction = record.Pawn.HomeFaction ?? record.Pawn.Faction;
                            summary.RebuiltLinks++;
                        }
                    }

                    record.CapturedHealthPercent = Clamp(record.CapturedHealthPercent, 0f, 1f, 1f);
                    record.LastMissingPartCount = Math.Max(0, record.LastMissingPartCount);
                    record.LastPermanentInjuryCount = Math.Max(0, record.LastPermanentInjuryCount);
                    record.StarvationTicks = Math.Max(0, record.StarvationTicks);
                    record.RecentBattleEvents = record.RecentBattleEvents ?? new List<PrisonerBattleEvent>();
                    record.RecentBattleEvents.RemoveAll(item => item == null || string.IsNullOrWhiteSpace(item.Description));
                    record.RecentBattleEvents = record.RecentBattleEvents
                        .OrderByDescending(item => item.Tick)
                        .Take(4)
                        .ToList();
                    foreach (PrisonerBattleEvent battleEvent in record.RecentBattleEvents)
                    {
                        battleEvent.Description = battleEvent.Description.Length > 240
                            ? battleEvent.Description.Substring(0, 240)
                            : battleEvent.Description;
                    }
                    EnsureFactionOfferSchedule(record);
                }
                catch (Exception exception)
                {
                    CompatibilityDiagnostics.LogErrorOnce(
                        "record-repair:" + (record.PawnLoadId ?? "missing"),
                        "Could not repair prisoner record.",
                        exception);
                }
            }

            foreach (IGrouping<string, PrisonerRecord> group in records
                .Where(record => !string.IsNullOrEmpty(record.PawnLoadId))
                .GroupBy(record => record.PawnLoadId, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .ToList())
            {
                PrisonerRecord keep = group
                    .OrderByDescending(record => record.Pawn != null)
                    .ThenByDescending(record => !string.IsNullOrEmpty(record.ActiveDealId))
                    .ThenByDescending(record => record.LastTreatmentCheckTick >= 0)
                    .ThenByDescending(record => record.CapturedMarketValue > 0f)
                    .ThenBy(record => record.CapturedTick)
                    .First();
                foreach (PrisonerRecord duplicate in group.Where(record => record != keep).ToList())
                {
                    MergeRecordState(keep, duplicate);
                    records.Remove(duplicate);
                    summary.MergedRecords++;
                }
            }

            HashSet<string> usedDealIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (PrisonerDeal deal in deals.ToList())
            {
                try
                {
                    if (string.IsNullOrEmpty(deal.DealId) || usedDealIds.Contains(deal.DealId))
                    {
                        deal.DealId = NextRepairDealId(usedDealIds);
                        summary.RebuiltLinks++;
                    }
                    usedDealIds.Add(deal.DealId);

                    if (deal.Prisoner != null)
                    {
                        string loadId = deal.Prisoner.GetUniqueLoadID();
                        if (!string.Equals(deal.PrisonerLoadId, loadId, StringComparison.Ordinal))
                        {
                            deal.PrisonerLoadId = loadId;
                            summary.RebuiltLinks++;
                        }
                    }

                    if (deal.ReturnedHostage != null)
                    {
                        deal.ReturnedHostageLoadId = deal.ReturnedHostage.GetUniqueLoadID();
                    }

                    NormalizeDealRewards(deal, summary);
                    Map repairedMap = ResolveDealMap(deal, false);
                    if (repairedMap != deal.Map)
                    {
                        SetDealMap(deal, repairedMap);
                        summary.ReassignedMaps++;
                    }

                    deal.NegotiationRound = Math.Max(0, deal.NegotiationRound);
                    deal.DeadlineExtensionCount = Math.Max(0, deal.DeadlineExtensionCount);
                    deal.PirateRiskEventAttempts = Math.Max(0, deal.PirateRiskEventAttempts);
                }
                catch (Exception exception)
                {
                    CompatibilityDiagnostics.LogErrorOnce(
                        "deal-repair:" + (deal.DealId ?? "missing"),
                        "Could not repair deal.",
                        exception);
                }
            }

            foreach (IGrouping<string, PrisonerDeal> group in deals
                .Where(deal => deal.IsActive && !string.IsNullOrEmpty(deal.PrisonerLoadId))
                .GroupBy(deal => deal.PrisonerLoadId, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .ToList())
            {
                PrisonerDeal keep = group
                    .OrderByDescending(deal => DealStatePriority(deal.State))
                    .ThenByDescending(deal => deal.AcceptedTick)
                    .ThenByDescending(deal => deal.CreatedTick)
                    .First();
                foreach (PrisonerDeal duplicate in group.Where(deal => deal != keep).ToList())
                {
                    CancelConflictingDeal(duplicate, now);
                    summary.CancelledConflictingDeals++;
                }
            }

            int removedMemories = factionNegotiationMemories.RemoveAll(memory => memory == null || memory.Faction == null);
            summary.RemovedMemories += removedMemories;
            foreach (FactionNegotiationMemory memory in factionNegotiationMemories)
            {
                memory.RecentEvents = memory.RecentEvents ?? new List<PrisonerMemoryEvent>();
                memory.RecentEvents.RemoveAll(item => item == null || string.IsNullOrEmpty(item.ReasonKey));
                memory.Reliability = Clamp(memory.Reliability, -100f, 100f, 0f);
                memory.Treatment = Clamp(memory.Treatment, -100f, 100f, 0f);
                memory.Resentment = Clamp(memory.Resentment, -100f, 100f, 0f);
                memory.ResentmentFloor = Clamp(memory.ResentmentFloor, 0f, 100f, 0f);
                memory.DiplomaticReserve = Clamp(memory.DiplomaticReserve, -1f, 10000000f, -1f);
                FactionPrisonerMemoryUtility.ClampMemory(memory);
                memory.Impatience = Math.Max(0, memory.Impatience);
                memory.SuccessfulDeals = Math.Max(0, memory.SuccessfulDeals);
                memory.RejectedNegotiations = Math.Max(0, memory.RejectedNegotiations);
                AiNarrativeContextUtility.EnsurePersona(memory, memory.Faction);
            }

            foreach (IGrouping<Faction, FactionNegotiationMemory> group in factionNegotiationMemories
                .Where(memory => memory.Faction != null)
                .GroupBy(memory => memory.Faction)
                .Where(group => group.Count() > 1)
                .ToList())
            {
                FactionNegotiationMemory keep = group
                    .OrderByDescending(memory => memory.MemoryUpdatedTick)
                    .ThenByDescending(memory => memory.SuccessfulDeals + memory.RejectedNegotiations)
                    .First();
                foreach (FactionNegotiationMemory duplicate in group.Where(memory => memory != keep).ToList())
                {
                    keep.RecentEvents.AddRange(duplicate.RecentEvents ?? new List<PrisonerMemoryEvent>());
                    keep.SuccessfulDeals = Math.Max(keep.SuccessfulDeals, duplicate.SuccessfulDeals);
                    keep.RejectedNegotiations = Math.Max(keep.RejectedNegotiations, duplicate.RejectedNegotiations);
                    keep.Reliability = ChooseMoreInformative(keep.Reliability, duplicate.Reliability);
                    keep.Treatment = ChooseMoreInformative(keep.Treatment, duplicate.Treatment);
                    keep.Resentment = ChooseMoreInformative(keep.Resentment, duplicate.Resentment);
                    factionNegotiationMemories.Remove(duplicate);
                    summary.RemovedMemories++;
                }
                keep.RecentEvents = keep.RecentEvents
                    .OrderByDescending(item => item.Tick)
                    .Take(8)
                    .ToList();
            }

            summary.RemovedStrategicStates += factionStrategicStates.RemoveAll(state => state == null || state.Faction == null);
            foreach (FactionStrategicState state in factionStrategicStates)
            {
                if (state.WarnedRaidMap != null && Find.Maps != null && !Find.Maps.Contains(state.WarnedRaidMap))
                {
                    state.WarnedRaidMap = null;
                    state.ClearWarnedRaid();
                }

                if (state.CeasefireExpiresTick < -1)
                {
                    state.CeasefireExpiresTick = -1;
                }
                if (state.IntelExpiresTick < -1)
                {
                    state.IntelExpiresTick = -1;
                }
                if (state.CareCreditGrantedTick < -1)
                {
                    state.CareCreditGrantedTick = -1;
                }
            }

            foreach (IGrouping<Faction, FactionStrategicState> group in factionStrategicStates
                .Where(state => state.Faction != null)
                .GroupBy(state => state.Faction)
                .Where(group => group.Count() > 1)
                .ToList())
            {
                FactionStrategicState keep = group
                    .OrderByDescending(state => state.CeasefireExpiresTick)
                    .ThenByDescending(state => state.IntelExpiresTick)
                    .First();
                foreach (FactionStrategicState duplicate in group.Where(state => state != keep).ToList())
                {
                    keep.CeasefireExpiresTick = Math.Max(keep.CeasefireExpiresTick, duplicate.CeasefireExpiresTick);
                    keep.IntelAvailable |= duplicate.IntelAvailable;
                    keep.IntelExpiresTick = Math.Max(keep.IntelExpiresTick, duplicate.IntelExpiresTick);
                    if (!keep.CareCreditAvailable && duplicate.CareCreditAvailable)
                    {
                        keep.CareCreditAvailable = true;
                        keep.CareCreditSourceDealId = duplicate.CareCreditSourceDealId;
                        keep.CareCreditSourcePawnLabel = duplicate.CareCreditSourcePawnLabel;
                        keep.CareCreditGrantedTick = duplicate.CareCreditGrantedTick;
                    }
                    if (keep.WarnedRaidFireTick < duplicate.WarnedRaidFireTick)
                    {
                        keep.WarnedRaidFireTick = duplicate.WarnedRaidFireTick;
                        keep.WarnedRaidDef = duplicate.WarnedRaidDef;
                        keep.WarnedRaidMap = duplicate.WarnedRaidMap;
                        keep.WarnedRaidPoints = duplicate.WarnedRaidPoints;
                    }
                    factionStrategicStates.Remove(duplicate);
                    summary.RemovedStrategicStates++;
                }
            }

            // A previous version could have queued a physical positive-return gift
            // for a permanent hostile faction. Convert that pending outcome into
            // the current one-time care credit instead of spawning silver on load.
            foreach (StrategicFollowupEvent legacyGift in strategicFollowups
                .Where(item => item.Kind == StrategicFollowupKind.PositiveGift
                    && IsCareCreditEligible(item.Faction))
                .ToList())
            {
                GrantCareCredit(
                    legacyGift.Faction,
                    legacyGift.SourcePawnLabel,
                    legacyGift.SourceDealId,
                    false);
                strategicFollowups.Remove(legacyGift);
                summary.RemovedFollowups++;
            }

            summary.RemovedFollowups += strategicFollowups.RemoveAll(followup => followup == null
                || string.IsNullOrEmpty(followup.EventId)
                || followup.Faction == null);
            foreach (PrisonerDiplomacyEventRecord orphanedEvent in diplomacyEvents
                .Where(eventRecord => eventRecord == null
                    || string.IsNullOrEmpty(eventRecord.EventId)
                    || eventRecord.Faction == null
                    || eventRecord.State == PrisonerDiplomacyEventState.Completed
                        && eventRecord.OutcomeApplied == false)
                .ToList())
            {
                CleanupNeutralTradePoint(orphanedEvent);
            }

            summary.RemovedDiplomacyEvents += diplomacyEvents.RemoveAll(eventRecord => eventRecord == null
                || string.IsNullOrEmpty(eventRecord.EventId)
                || eventRecord.Faction == null
                || eventRecord.State == PrisonerDiplomacyEventState.Completed
                    && eventRecord.OutcomeApplied == false);
            foreach (WorldObject_PrisonerDiplomacyTradePoint orphanedPoint in Find.WorldObjects?.AllWorldObjects
                .OfType<WorldObject_PrisonerDiplomacyTradePoint>()
                .Where(point => point != null
                    && !diplomacyEvents.Any(eventRecord => eventRecord != null
                        && eventRecord.EventId == point.EventId
                        && eventRecord.IsActive
                        && eventRecord.WorldTradePointRequested))
                .ToList() ?? new List<WorldObject_PrisonerDiplomacyTradePoint>())
            {
                orphanedPoint.Destroy();
            }
            summary.RemovedCommTargets += commTargets.RemoveAll(target =>
                target == null || !target.IsHub && target.Faction == null);
            summary.RemovedNarratives += aiNarratives.RemoveAll(narrative => narrative == null
                || string.IsNullOrEmpty(narrative.ContextId));
            foreach (AiNarrativeRecord narrative in aiNarratives)
            {
                narrative.PlayerNote = AiNegotiationNoteUtility.Normalize(narrative.PlayerNote);
                narrative.PlayerEmotion = AiNegotiationNoteUtility.Classify(narrative.PlayerNote);
            }

            foreach (IGrouping<Faction, PrisonerDiplomacyCommTarget> group in commTargets
                .Where(target => target.Faction != null)
                .GroupBy(target => target.Faction)
                .Where(group => group.Count() > 1)
                .ToList())
            {
                foreach (PrisonerDiplomacyCommTarget duplicate in group.Skip(1).ToList())
                {
                    commTargets.Remove(duplicate);
                    summary.RemovedCommTargets++;
                }
            }

            foreach (PrisonerDiplomacyCommTarget duplicate in commTargets
                .Where(target => target.IsHub)
                .Skip(1)
                .ToList())
            {
                commTargets.Remove(duplicate);
                summary.RemovedCommTargets++;
            }

            foreach (IGrouping<string, StrategicFollowupEvent> group in strategicFollowups
                .Where(followup => !string.IsNullOrEmpty(followup.EventId))
                .GroupBy(followup => followup.EventId, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .ToList())
            {
                foreach (StrategicFollowupEvent duplicate in group.Skip(1).ToList())
                {
                    strategicFollowups.Remove(duplicate);
                    summary.RemovedFollowups++;
                }
            }

            // ActiveDealId is a derived link. Reconcile it against the repaired deal list
            // without clearing valid links first, so a clean save remains clean on reload.
            Dictionary<string, string> expectedActiveDealIds = deals
                .Where(deal => deal.IsActive
                    && !string.IsNullOrEmpty(deal.DealId)
                    && !string.IsNullOrEmpty(deal.PrisonerLoadId))
                .GroupBy(deal => deal.PrisonerLoadId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key,
                    group => group.OrderByDescending(deal => DealStatePriority(deal.State))
                        .ThenByDescending(deal => deal.AcceptedTick)
                        .ThenByDescending(deal => deal.CreatedTick)
                        .First().DealId,
                    StringComparer.Ordinal);

            foreach (PrisonerRecord record in records)
            {
                string expectedDealId = null;
                if (!string.IsNullOrEmpty(record.PawnLoadId))
                {
                    expectedActiveDealIds.TryGetValue(record.PawnLoadId, out expectedDealId);
                }

                if (!string.Equals(record.ActiveDealId, expectedDealId, StringComparison.Ordinal))
                {
                    record.ActiveDealId = expectedDealId;
                    summary.RebuiltLinks++;
                }
            }

            int maximumDealSequence = FindMaximumSequence(deals.Select(deal => deal.DealId), "PD-");
            if (nextDealSequence <= maximumDealSequence)
            {
                nextDealSequence = maximumDealSequence + 1;
                summary.RepairedSequences++;
            }

            int maximumEventSequence = FindMaximumSequence(strategicFollowups.Select(item => item.EventId), "PD-EVT-");
            if (nextStrategicEventSequence <= maximumEventSequence)
            {
                nextStrategicEventSequence = maximumEventSequence + 1;
                summary.RepairedSequences++;
            }

            int maximumDiplomacyEventSequence = FindMaximumSequence(
                diplomacyEvents.Select(item => item.EventId),
                "PD-WEVT-");
            if (nextStrategicEventSequence <= maximumDiplomacyEventSequence)
            {
                nextStrategicEventSequence = maximumDiplomacyEventSequence + 1;
                summary.RepairedSequences++;
            }

            if (logChanges && summary.Changed)
            {
                Log.Warning("[Prisoner Diplomacy Compatibility] Repaired loaded data: " + summary.ToLogString() + ".");
            }

            return summary;
        }

        private static void MergeRecordState(PrisonerRecord keep, PrisonerRecord duplicate)
        {
            if (keep.Pawn == null)
            {
                keep.Pawn = duplicate.Pawn;
            }
            if (keep.OriginalFaction == null)
            {
                keep.OriginalFaction = duplicate.OriginalFaction;
            }
            keep.CapturedTick = Math.Min(keep.CapturedTick, duplicate.CapturedTick);
            keep.CapturedMarketValue = Math.Max(keep.CapturedMarketValue, duplicate.CapturedMarketValue);
            keep.DiplomaticValue = Math.Max(keep.DiplomaticValue, duplicate.DiplomaticValue);
            keep.NegotiationCount = Math.Max(keep.NegotiationCount, duplicate.NegotiationCount);
            keep.CriticalRecoveryRecorded |= duplicate.CriticalRecoveryRecorded;
            keep.MalnutritionRecorded |= duplicate.MalnutritionRecorded;
            keep.TerminalOutcomeRecorded |= duplicate.TerminalOutcomeRecorded;
            keep.PlayerCausedPermanentHarm |= duplicate.PlayerCausedPermanentHarm;
            keep.LastPlayerTreatmentTick = Math.Max(keep.LastPlayerTreatmentTick, duplicate.LastPlayerTreatmentTick);
            keep.LastPermanentHarmTick = Math.Max(keep.LastPermanentHarmTick, duplicate.LastPermanentHarmTick);
            keep.RecentBattleEvents = (keep.RecentBattleEvents ?? new List<PrisonerBattleEvent>())
                .Concat(duplicate.RecentBattleEvents ?? new List<PrisonerBattleEvent>())
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.Description))
                .OrderByDescending(item => item.Tick)
                .Take(4)
                .ToList();
        }

        private static int DealStatePriority(DealState state)
        {
            switch (state)
            {
                case DealState.FulfillmentPending: return 5;
                case DealState.ReleaseOrdered: return 4;
                case DealState.AcceptedAwaitingRelease: return 3;
                case DealState.Negotiating: return 2;
                case DealState.Offered: return 1;
                default: return 0;
            }
        }

        private void CancelConflictingDeal(PrisonerDeal deal, int now)
        {
            if (deal.CompensationCharged && !deal.PrisonerDelivered)
            {
                Map refundMap = ResolveDealMap(deal, true);
                bool refunded = deal.PlayerCompensationSilver > 0
                    ? PrisonerExchangeUtility.TryRefundSilver(refundMap, deal.PlayerCompensationSilver, out _)
                    : deal.PlayerCompensationThingDef != null
                        && PrisonerExchangeUtility.TryRefundThings(
                            refundMap,
                            deal.PlayerCompensationThingDef,
                            deal.PlayerCompensationThingCount,
                            out _);
                if (refunded)
                {
                    deal.CompensationCharged = false;
                }
                else
                {
                    CompatibilityDiagnostics.LogIssueOnce(
                        "conflict-refund:" + deal.DealId,
                        "Could not refund compensation while cancelling duplicate deal=" + deal.DealId + ".");
                }
            }
            deal.State = DealState.Cancelled;
            deal.CompletedTick = now;
            deal.FailureNotified = true;
            deal.VanillaReleaseConfirmed = false;
            deal.PrisonerDelivered = false;
            ClearRecordDeal(deal);
            RemoveOfferLetter(deal.DealId);
            CompatibilityDiagnostics.LogIssueOnce(
                "duplicate-deal:" + deal.DealId,
                "Cancelled duplicate active deal=" + deal.DealId
                    + " prisoner=" + (deal.PrisonerLoadId ?? "?") + ".");
        }

        private static void NormalizeDealRewards(PrisonerDeal deal, CompatibilityRepairSummary summary)
        {
            if (deal.Rewards == null)
            {
                deal.Rewards = new RewardDemand { Silver = Math.Max(0, deal.SilverAmount) };
                summary.NormalizedRewards++;
            }

            RewardDemand rewards = deal.Rewards;
            NormalizeRewardDemand(rewards, summary);
            if (deal.LastPlayerDemand != null)
            {
                NormalizeRewardDemand(deal.LastPlayerDemand, summary);
            }
        }

        private static void NormalizeRewardDemand(RewardDemand rewards, CompatibilityRepairSummary summary)
        {
            if (rewards == null)
            {
                return;
            }

            int oldSilver = rewards.Silver;
            int oldSupplyCount = rewards.SupplyCount;
            int oldGoodwill = rewards.Goodwill;
            int oldCeasefireDays = rewards.CeasefireDays;
            ThingDef oldSupply = rewards.SupplyDef;
            string oldSpecialRewardId = rewards.SpecialRewardId;
            ThingDef oldSpecialRewardThingDef = rewards.SpecialRewardThingDef;
            int oldSpecialRewardCount = rewards.SpecialRewardCount;
            rewards.Silver = Math.Max(0, rewards.Silver);
            rewards.Goodwill = Math.Max(0, rewards.Goodwill);
            rewards.CeasefireDays = Math.Max(0, Math.Min(30, rewards.CeasefireDays));
            rewards.SupplyCount = Math.Max(0, rewards.SupplyCount);
            if (rewards.SupplyDef == null || rewards.SupplyCount == 0)
            {
                rewards.SupplyCount = 0;
                if (rewards.SupplyDef != null && rewards.SupplyCount == 0)
                {
                    rewards.SupplyDef = null;
                }
            }

            if (string.IsNullOrWhiteSpace(rewards.SpecialRewardId)
                || rewards.SpecialRewardThingDef == null
                || rewards.SpecialRewardCount <= 0)
            {
                rewards.SpecialRewardId = null;
                rewards.SpecialRewardThingDef = null;
                rewards.SpecialRewardCount = 0;
            }

            if (oldSilver != rewards.Silver
                || oldSupplyCount != rewards.SupplyCount
                || oldGoodwill != rewards.Goodwill
                || oldCeasefireDays != rewards.CeasefireDays
                || oldSupply != rewards.SupplyDef
                || oldSpecialRewardId != rewards.SpecialRewardId
                || oldSpecialRewardThingDef != rewards.SpecialRewardThingDef
                || oldSpecialRewardCount != rewards.SpecialRewardCount)
            {
                summary.NormalizedRewards++;
            }
        }

        private string NextRepairDealId(ISet<string> usedIds)
        {
            string id;
            do
            {
                id = "PD-" + nextDealSequence++.ToString("D6");
            }
            while (usedIds.Contains(id));
            return id;
        }

        private static int FindMaximumSequence(IEnumerable<string> ids, string prefix)
        {
            int maximum = 0;
            foreach (string id in ids ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrEmpty(id) || !id.StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                if (int.TryParse(id.Substring(prefix.Length), out int value))
                {
                    maximum = Math.Max(maximum, value);
                }
            }
            return maximum;
        }

        private static float Clamp(float value, float minimum, float maximum, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return fallback;
            }
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private static float ChooseMoreInformative(float first, float second)
        {
            if (float.IsNaN(first) || float.IsInfinity(first))
            {
                return second;
            }
            if (float.IsNaN(second) || float.IsInfinity(second))
            {
                return first;
            }
            return Math.Abs(second) > Math.Abs(first) ? second : first;
        }

        private static void SetDealMap(PrisonerDeal deal, Map map)
        {
            if (deal == null)
            {
                return;
            }

            deal.Map = map;
            deal.MapLoadId = map?.GetUniqueLoadID();
        }

        private static Map ResolveDealMap(PrisonerDeal deal, bool allowFallback)
        {
            if (deal == null)
            {
                return null;
            }

            if (deal.Map != null && Find.Maps != null && Find.Maps.Contains(deal.Map))
            {
                deal.MapLoadId = deal.Map.GetUniqueLoadID();
                return deal.Map;
            }

            Map prisonerMap = deal.Prisoner?.MapHeld;
            if (prisonerMap != null && Find.Maps != null && Find.Maps.Contains(prisonerMap))
            {
                return prisonerMap;
            }

            if (!string.IsNullOrEmpty(deal.MapLoadId) && Find.Maps != null)
            {
                Map restored = Find.Maps.FirstOrDefault(map => map != null
                    && string.Equals(map.GetUniqueLoadID(), deal.MapLoadId, StringComparison.Ordinal));
                if (restored != null)
                {
                    return restored;
                }
            }

            if (!allowFallback || Find.Maps == null)
            {
                return null;
            }

            return Find.Maps
                .Where(map => map != null && map.IsPlayerHome)
                .OrderBy(map => map.GetUniqueLoadID(), StringComparer.Ordinal)
                .FirstOrDefault();
        }
    }
}
