using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using PrisonerDiplomacy.Telemetry;

namespace PrisonerDiplomacy
{
    public sealed partial class PrisonerDiplomacyGameComponent : GameComponent
    {
        private const int ScanIntervalTicks = 2000;
        private const int TicksPerDay = 60000;
        private const int MinimumFactionContactDelayTicks = 3 * TicksPerDay;
        private const int MaximumFactionContactDelayTicks = 7 * TicksPerDay;
        private const int ProposalDurationTicks = 120000;
        private const int FulfillmentDurationTicks = 180000;
        private const int FulfillmentExtensionTicks = 2 * TicksPerDay;
        private const int ProposalCooldownTicks = 300000;
        private const int RejectedOfferCooldownTicks = 30 * TicksPerDay;
        private const int MutedFactionOfferCooldownTicks = 60 * TicksPerDay;
        private const int FactionOfferCooldownAfterRejectionTicks = 7 * TicksPerDay;
        private const int PlayerPrisonerCooldownTicks = 180000;
        private const int PlayerFactionCooldownTicks = 60000;
        private const int CompletedRetentionTicks = 600000;
        private const int MalnutritionThresholdTicks = 2 * TicksPerDay;
        private const int RecentMemoryEventRetentionTicks = 4 * FactionPrisonerMemoryUtility.MemoryYearTicks;
        private const int SaveVersion = 17;

        private int saveVersion = SaveVersion;
        private int nextDealSequence = 1;
        private int nextAiNarrativeSequence = 1;
        private int nextStrategicEventSequence = 1;
        private bool commandLineSmokeTestPending;
        private bool commandLineSmokeTestWaitLogged;
        private bool prisonerDiplomacyIntroShown;
        private bool rimChatCompatibilityWarningShown;
        private List<PrisonerRecord> records = new List<PrisonerRecord>();
        private List<PrisonerDeal> deals = new List<PrisonerDeal>();
        private List<PrisonerDealHistoryEntry> dealHistory = new List<PrisonerDealHistoryEntry>();
        private List<FactionNegotiationMemory> factionNegotiationMemories = new List<FactionNegotiationMemory>();
        private List<PrisonerDiplomacyCommTarget> commTargets = new List<PrisonerDiplomacyCommTarget>();
        private List<AiNarrativeRecord> aiNarratives = new List<AiNarrativeRecord>();
        private List<FactionStrategicState> factionStrategicStates = new List<FactionStrategicState>();
        private List<StrategicFollowupEvent> strategicFollowups = new List<StrategicFollowupEvent>();
        private List<PrisonerDiplomacyEventRecord> diplomacyEvents = new List<PrisonerDiplomacyEventRecord>();
        // CaravanExitMapUtility moves the prisoner through Pawn.ExitMap before
        // the world-trade arrival callback can verify the handoff. Keep that
        // transient exit out of the ordinary escape detector.
        private readonly HashSet<string> pendingCaravanTransferExits = new HashSet<string>();

        public static PrisonerDiplomacyGameComponent Current => Verse.Current.Game?.GetComponent<PrisonerDiplomacyGameComponent>();
        public IReadOnlyList<PrisonerRecord> Records => records;
        public IReadOnlyList<PrisonerDeal> Deals => deals;
        public IReadOnlyList<PrisonerDealHistoryEntry> DealHistory => dealHistory;
        public int SaveSchemaVersion => saveVersion;

        public PrisonerDiplomacyGameComponent(Game game)
        {
            commandLineSmokeTestPending = GenCommandLine.CommandLineArgPassed("pdsmoketest");
            if (commandLineSmokeTestPending)
            {
                Log.Message("[Prisoner Diplomacy SmokeTest] ARMED component created.");
            }
        }

        public override void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                CaptureTerminalDealHistory();
            }

            Scribe_Values.Look(ref saveVersion, "saveVersion", SaveVersion);
            Scribe_Values.Look(ref nextDealSequence, "nextDealSequence", 1);
            Scribe_Values.Look(ref nextAiNarrativeSequence, "nextAiNarrativeSequence", 1);
            Scribe_Values.Look(ref nextStrategicEventSequence, "nextStrategicEventSequence", 1);
            Scribe_Values.Look(ref prisonerDiplomacyIntroShown, "prisonerDiplomacyIntroShown", false);
            Scribe_Values.Look(ref rimChatCompatibilityWarningShown, "rimChatCompatibilityWarningShown", false);
            Scribe_Collections.Look(ref records, "prisonerRecords", LookMode.Deep);
            Scribe_Collections.Look(ref deals, "prisonerDeals", LookMode.Deep);
            Scribe_Collections.Look(ref dealHistory, "prisonerDealHistory", LookMode.Deep);
            Scribe_Collections.Look(ref factionNegotiationMemories, "factionNegotiationMemories", LookMode.Deep);
            Scribe_Collections.Look(ref commTargets, "commTargets", LookMode.Deep);
            Scribe_Collections.Look(ref aiNarratives, "aiNarratives", LookMode.Deep);
            Scribe_Collections.Look(ref factionStrategicStates, "factionStrategicStates", LookMode.Deep);
            Scribe_Collections.Look(ref strategicFollowups, "strategicFollowups", LookMode.Deep);
            Scribe_Collections.Look(ref diplomacyEvents, "diplomacyEvents", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                records = records ?? new List<PrisonerRecord>();
                deals = deals ?? new List<PrisonerDeal>();
                dealHistory = dealHistory ?? new List<PrisonerDealHistoryEntry>();
                factionNegotiationMemories = factionNegotiationMemories ?? new List<FactionNegotiationMemory>();
                commTargets = commTargets ?? new List<PrisonerDiplomacyCommTarget>();
                aiNarratives = aiNarratives ?? new List<AiNarrativeRecord>();
                factionStrategicStates = factionStrategicStates ?? new List<FactionStrategicState>();
                strategicFollowups = strategicFollowups ?? new List<StrategicFollowupEvent>();
                diplomacyEvents = diplomacyEvents ?? new List<PrisonerDiplomacyEventRecord>();
                AiNarrativeService.CancelAll();
                RepairLoadedData();
            }
        }

        public override void StartedNewGame()
        {
            AiNarrativeService.CancelAll();
            CompatibilityDiagnostics.Reset();
            ScanAndUpdate();
            TryShowRimChatCompatibilityWarning();
            CompatibilityDiagnostics.LogCompatibilityReport(records, deals);
            if (commandLineSmokeTestPending)
            {
                Log.Message("[Prisoner Diplomacy SmokeTest] StartedNewGame received.");
            }
        }

        public override void LoadedGame()
        {
            AiNarrativeService.CancelAll();
            CompatibilityDiagnostics.Reset();
            ScanAndUpdate();
            TryShowRimChatCompatibilityWarning();
            CompatibilityDiagnostics.LogCompatibilityReport(records, deals);
            if (lastRepairSummary?.Changed == true)
            {
                Messages.Message(
                    "PD_CompatibilityRepairApplied".Translate(lastRepairSummary.ToLogString()),
                    MessageTypeDefOf.NeutralEvent,
                    false);
            }
            if (GenCommandLine.TryGetCommandLineArg("pdloadtest", out string saveName))
            {
                PrisonerDeal active = deals.FirstOrDefault(deal => deal.IsActive);
                Log.Message("[Prisoner Diplomacy LoadTest] PASS save=" + saveName
                    + " saveVersion=" + saveVersion
                    + " records=" + records.Count
                    + " deals=" + deals.Count
                    + " activeState=" + (active != null ? active.State.ToString() : "None")
                    + " activeDeal=" + (active?.DealId ?? "None"));
            }
        }

        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % ScanIntervalTicks == 0)
            {
                ScanAndUpdate();
            }
        }

        public override void GameComponentUpdate()
        {
            ErrorTelemetryService.DrainMainThread();
            DrainAiNarrativeCompletions();
            if (commandLineSmokeTestPending)
            {
                TryRunCommandLineSmokeTest();
            }
        }

        public PrisonerDeal GetDeal(string dealId)
        {
            return deals.FirstOrDefault(deal => deal.DealId == dealId);
        }

        public PrisonerDeal GetActiveDeal(Pawn pawn)
        {
            if (pawn == null)
            {
                return null;
            }

            string loadId = pawn.GetUniqueLoadID();
            return deals.FirstOrDefault(deal => deal.IsActive && (deal.Prisoner == pawn || deal.PrisonerLoadId == loadId));
        }

        public IReadOnlyList<PrisonerDealHistoryEntry> GetDealHistory(Faction faction)
        {
            CaptureTerminalDealHistory();
            return dealHistory
                .Where(entry => entry != null && (faction == null || entry.Faction == faction
                    || !string.IsNullOrEmpty(entry.FactionLoadId) && entry.FactionLoadId == faction.GetUniqueLoadID()))
                .OrderByDescending(entry => entry.CompletedTick >= 0 ? entry.CompletedTick : entry.CreatedTick)
                .ThenByDescending(entry => entry.DealId)
                .ToList();
        }

        public bool HasPortableDiplomacyTerminal(Pawn pawn)
        {
            return pawn != null
                && PrisonerDiplomacyDefOf.PD_PortableDiplomacyTerminal != null
                && pawn.EquippedWornOrInventoryThings.Any(thing =>
                    thing?.def == PrisonerDiplomacyDefOf.PD_PortableDiplomacyTerminal);
        }

        public IReadOnlyList<Faction> GetKnownNegotiationFactions(Map map)
        {
            CaptureTerminalDealHistory();
            HashSet<Faction> factions = new HashSet<Faction>();
            foreach (PrisonerRecord record in records)
            {
                if (record?.OriginalFaction != null && (map == null || record.Pawn?.MapHeld == map))
                {
                    factions.Add(record.OriginalFaction);
                }
            }

            foreach (PrisonerDeal deal in deals.Where(item => item?.Faction != null
                && (map == null || item.Map == map || item.IsActive)))
            {
                factions.Add(deal.Faction);
            }

            foreach (FactionStrategicState state in factionStrategicStates.Where(item =>
                item?.Faction != null && HasActiveStrategicStatus(item, Find.TickManager?.TicksGame ?? 0)))
            {
                factions.Add(state.Faction);
            }

            foreach (FactionNegotiationMemory memory in factionNegotiationMemories.Where(item =>
                item?.Faction != null && (item.LastPlayerNegotiationTick >= 0
                    || item.SuccessfulDeals > 0 || item.RejectedNegotiations > 0)))
            {
                factions.Add(memory.Faction);
            }

            foreach (PrisonerDealHistoryEntry entry in dealHistory.Where(item => item?.Faction != null))
            {
                factions.Add(entry.Faction);
            }

            return factions
                .Where(item => item != null && item != Faction.OfPlayer)
                .OrderBy(item => item.Name)
                .ToList();
        }

        public IReadOnlyList<Faction> GetActiveAgreementFactions()
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            return deals.Where(deal => deal?.IsActive == true && deal.Faction != null)
                .Select(deal => deal.Faction)
                .Concat(factionStrategicStates
                    .Where(state => HasActiveStrategicStatus(state, now))
                    .Select(state => state.Faction))
                .Where(faction => faction != null)
                .Distinct()
                .OrderBy(faction => faction.Name)
                .ToList();
        }

        public bool HasActiveAgreementStatus => GetActiveAgreementFactions().Count > 0;

        public Faction GetFirstActiveAgreementFaction()
        {
            return GetActiveAgreementFactions().FirstOrDefault();
        }

        private void CaptureTerminalDealHistory()
        {
            if (dealHistory == null)
            {
                dealHistory = new List<PrisonerDealHistoryEntry>();
            }

            HashSet<string> knownIds = new HashSet<string>(dealHistory
                .Where(entry => entry != null && !string.IsNullOrEmpty(entry.DealId))
                .Select(entry => entry.DealId));
            foreach (PrisonerDeal deal in deals.Where(item => item != null && !item.IsActive
                && item.CompletedTick >= 0 && !string.IsNullOrEmpty(item.DealId)))
            {
                if (knownIds.Add(deal.DealId))
                {
                    PrisonerDealHistoryEntry entry = PrisonerDealHistoryEntry.Create(deal);
                    if (entry != null)
                    {
                        dealHistory.Add(entry);
                    }
                }
            }
        }

        public AiNarrativeRecord GetLatestAiNarrative(string dealId)
        {
            if (string.IsNullOrEmpty(dealId))
            {
                return null;
            }

            return aiNarratives
                .Where(item => item != null && item.DealId == dealId)
                .OrderByDescending(item => item.CreatedTick)
                .ThenByDescending(item => item.ContextId)
                .FirstOrDefault();
        }

        public AiNarrativeRecord GetLatestAiNarrative(Pawn prisoner)
        {
            if (prisoner == null)
            {
                return null;
            }

            string loadId = prisoner.GetUniqueLoadID();
            return aiNarratives
                .Where(item => item != null
                    && (item.Prisoner == prisoner || item.PrisonerLoadId == loadId))
                .OrderByDescending(item => item.CreatedTick)
                .ThenByDescending(item => item.ContextId)
                .FirstOrDefault();
        }

        public void CancelAiNarrativesForWindow(string windowContextId)
        {
            if (string.IsNullOrEmpty(windowContextId))
            {
                return;
            }

            int now = Find.TickManager?.TicksGame ?? 0;
            foreach (AiNarrativeRecord narrative in aiNarratives.Where(item => item != null
                && item.Status == AiNarrativeStatus.Waiting
                && item.WindowContextId == windowContextId))
            {
                AiNarrativeService.Cancel(narrative.RequestId);
                narrative.Status = AiNarrativeStatus.Fallback;
                narrative.FailureCode = "window_closed";
                narrative.ResolvedTick = now;
            }
        }

        public void DisableAiNarratives()
        {
            AiNarrativeService.CancelAll();
            int now = Find.TickManager?.TicksGame ?? 0;
            foreach (AiNarrativeRecord narrative in aiNarratives.Where(item => item != null
                && item.Status == AiNarrativeStatus.Waiting))
            {
                narrative.Status = AiNarrativeStatus.Fallback;
                narrative.FailureCode = "disabled";
                narrative.ResolvedTick = now;
            }
        }

        public PrisonerRecord GetRecord(Pawn pawn)
        {
            if (pawn == null)
            {
                return null;
            }

            string loadId = pawn.GetUniqueLoadID();
            return records.FirstOrDefault(record => record.Pawn == pawn || record.PawnLoadId == loadId);
        }

        public IReadOnlyList<PrisonerRecord> GetNegotiableRecords(Faction faction, Map map)
        {
            List<PrisonerRecord> negotiable = new List<PrisonerRecord>();
            foreach (PrisonerRecord record in records.ToList())
            {
                try
                {
                    if (record?.Pawn != null
                        && (faction == null || record.OriginalFaction == faction)
                        && record.Pawn.MapHeld == map
                        && PrisonerEligibilityUtility.IsEligible(record.Pawn, out _))
                    {
                        negotiable.Add(record);
                    }
                }
                catch (Exception exception)
                {
                    CompatibilityDiagnostics.LogErrorOnce(
                        "negotiable-record:" + (record?.PawnLoadId ?? "missing"),
                        "Skipped a negotiation record after a compatibility exception.",
                        exception);
                }
            }

            return negotiable
                .OrderByDescending(record => record.DiplomaticValue)
                .ThenBy(record => record.Pawn.LabelShort)
                .ToList();
        }

        public IReadOnlyList<Faction> GetNegotiableFactions(Map map)
        {
            return GetNegotiableRecords(null, map)
                .Select(record => record.OriginalFaction)
                .Where(faction => faction != null)
                .Distinct()
                .OrderBy(faction => faction.Name)
                .ToList();
        }

        public IReadOnlyList<Pawn> GetAvailableHostages(Faction faction)
        {
            return PrisonerExchangeUtility.AvailableHostages(faction)
                .Where(hostage => !IsHostageReserved(hostage))
                .ToList();
        }

        public bool TryCreatePrisonerExchange(
            PrisonerRecord record,
            Pawn negotiator,
            Pawn returnedHostage,
            ThingDef compensationThingDef,
            out string reasonKey,
            bool refreshRecordValue = true)
        {
            reasonKey = null;
            if (!CanCreateNewPrisonerDiplomacyDeal(out TaggedString ownershipReason))
            {
                Messages.Message(ownershipReason, MessageTypeDefOf.RejectInput, false);
                return false;
            }
            if (!CanStartPlayerNegotiation(record, out TaggedString unavailableReason))
            {
                Messages.Message(unavailableReason, MessageTypeDefOf.RejectInput, false);
                return false;
            }

            if (!PrisonerExchangeUtility.IsHeldByFaction(record.OriginalFaction, returnedHostage))
            {
                reasonKey = "PD_ExchangeHostageUnavailable";
                return false;
            }

            if (IsHostageReserved(returnedHostage))
            {
                reasonKey = "PD_ExchangeHostageReserved";
                return false;
            }

            if (refreshRecordValue)
            {
                RefreshRecordValue(record);
            }
            int compensation = PrisonerExchangeUtility.CalculateCompensation(record, returnedHostage);
            Map dealMap = record.Pawn.MapHeld;
            int compensationThingCount = compensationThingDef == null
                ? 0
                : PrisonerExchangeUtility.CalculateSupplyCount(record.OriginalFaction, compensationThingDef, compensation);
            if (compensationThingDef != null
                && (compensationThingCount <= 0
                    || compensationThingCount > SupplyRewardUtility.MaximumSupplyCount
                    || !PrisonerExchangeUtility.AvailableCompensationSupplies(
                        record.OriginalFaction,
                        compensation).Contains(compensationThingDef)))
            {
                reasonKey = "PD_ExchangeSupplyGapTooLarge";
                return false;
            }
            bool charged = compensationThingDef == null
                ? PrisonerExchangeUtility.TryChargeSilver(dealMap, compensation)
                : PrisonerExchangeUtility.TryChargeThings(dealMap, compensationThingDef, compensationThingCount);
            if (!charged)
            {
                reasonKey = compensationThingDef == null
                    ? "PD_ExchangeInsufficientSilver"
                    : "PD_ExchangeInsufficientSupplies";
                return false;
            }

            int now = Find.TickManager.TicksGame;
            FactionNegotiationMemory memory = GetFactionMemory(record.OriginalFaction, true);
            string dealId = "PD-" + nextDealSequence++.ToString("D6");
            record.LastPlayerNegotiationTick = now;
            record.NegotiationCount++;
            memory.LastPlayerNegotiationTick = now;
            ScheduleNextFactionOffer(record, now);

            PrisonerDeal deal = new PrisonerDeal
            {
                DealId = dealId,
                Prisoner = record.Pawn,
                PrisonerLoadId = record.PawnLoadId,
                Faction = record.OriginalFaction,
                Map = dealMap,
                MapLoadId = dealMap?.GetUniqueLoadID(),
                ReturnedHostage = returnedHostage,
                ReturnedHostageLoadId = returnedHostage.GetUniqueLoadID(),
                PlayerCompensationSilver = compensationThingDef == null ? compensation : 0,
                PlayerCompensationThingDef = compensationThingDef,
                PlayerCompensationThingCount = compensationThingCount,
                CompensationCharged = compensation > 0,
                CreatedTick = now,
                OfferExpiresTick = now,
                FulfillmentExpiresTick = now + FulfillmentDurationTicks,
                LastTreatmentTickAtExtension = now - 1,
                AcceptedTick = now,
                State = DealState.AcceptedAwaitingRelease,
                Origin = DealOrigin.PlayerDemand,
                Negotiator = negotiator,
                NegotiatorSocialSkill = PrisonerNegotiationUtility.GetSocialSkill(negotiator),
                NegotiationOutcome = NegotiationOutcome.Accepted,
                Rewards = new RewardDemand(),
                NegotiationType = FactionNegotiationUtility.GetType(record.OriginalFaction)
            };
            AssignPirateRisk(deal, PrisonerDiplomacyTuning.EffectiveReliability(memory));
            deals.Add(deal);
            record.ActiveDealId = deal.DealId;
            Messages.Message("PD_ExchangeAccepted".Translate(
                record.Pawn.LabelShortCap,
                returnedHostage.LabelShortCap,
                GetExchangeCompensationDescription(deal)), record.Pawn, MessageTypeDefOf.PositiveEvent, false);
            return true;
        }

        private bool IsHostageReserved(Pawn hostage)
        {
            if (hostage == null)
            {
                return false;
            }

            string loadId = hostage.GetUniqueLoadID();
            return deals.Any(deal => deal.IsActive
                && (deal.ReturnedHostage == hostage || deal.ReturnedHostageLoadId == loadId));
        }

        private static TaggedString GetExchangeCompensationDescription(PrisonerDeal deal)
        {
            if (deal?.PlayerCompensationThingDef != null && deal.PlayerCompensationThingCount > 0)
            {
                return "PD_ExchangeCompensationSupplies".Translate(
                    deal.PlayerCompensationThingCount,
                    deal.PlayerCompensationThingDef.LabelCap);
            }

            return "PD_ExchangeCompensationSilver".Translate(Math.Max(0, deal?.PlayerCompensationSilver ?? 0));
        }

        public IEnumerable<PrisonerDiplomacyCommTarget> GetCommTargets(Map map)
        {
            ScanAndUpdate();
            HashSet<Faction> factions = new HashSet<Faction>();
            if (RimChatIntegration.AllowsNewPrisonerDiplomacyDeals)
            {
                foreach (Faction faction in GetNegotiableFactions(map))
                {
                    factions.Add(faction);
                }
            }

            foreach (Faction faction in GetPersistentCommTargetFactions(map))
            {
                factions.Add(faction);
            }

            foreach (Faction faction in factions.OrderBy(item => item.Name))
            {
                yield return GetOrCreateCommTarget(faction);
            }
        }

        private IEnumerable<Faction> GetPersistentCommTargetFactions(Map map)
        {
            IEnumerable<Faction> activeDealFactions = deals
                .Where(deal => deal?.IsActive == true && deal.Map == map && deal.Faction != null)
                .Select(deal => deal.Faction);
            int now = Find.TickManager?.TicksGame ?? 0;
            IEnumerable<Faction> strategicFactions = factionStrategicStates
                .Where(state => HasActiveStrategicStatus(state, now))
                .Select(state => state.Faction);
            IEnumerable<Faction> historicalFactions = dealHistory
                .Where(entry => entry?.Faction != null)
                .Select(entry => entry.Faction);
            IEnumerable<Faction> contactedFactions = factionNegotiationMemories
                .Where(memory => memory?.Faction != null && (memory.LastPlayerNegotiationTick >= 0
                    || memory.SuccessfulDeals > 0 || memory.RejectedNegotiations > 0))
                .Select(memory => memory.Faction);
            return activeDealFactions
                .Concat(strategicFactions)
                .Concat(historicalFactions)
                .Concat(contactedFactions)
                .Where(faction => faction != null)
                .Distinct();
        }

        private PrisonerDiplomacyCommTarget GetOrCreateCommTarget(Faction faction)
        {
            PrisonerDiplomacyCommTarget target = commTargets.FirstOrDefault(item => item.Faction == faction);
            if (target == null)
            {
                target = new PrisonerDiplomacyCommTarget(faction);
                commTargets.Add(target);
            }
            return target;
        }

        public void RefreshNegotiationRecords()
        {
            ScanAndUpdate();
            foreach (PrisonerRecord record in records.ToList())
            {
                try
                {
                    RefreshRecordValue(record);
                }
                catch (Exception exception)
                {
                    CompatibilityDiagnostics.LogErrorOnce(
                        "record-value:" + (record?.PawnLoadId ?? "missing"),
                        "Could not refresh a prisoner value.",
                        exception);
                }
            }
        }

        public void LogCompatibilityReport()
        {
            CompatibilityDiagnostics.LogCompatibilityReport(records, deals);
        }

        public int GetEstimatedFactionContactTicks(PrisonerRecord record)
        {
            if (record == null
                || !string.IsNullOrEmpty(record.ActiveDealId)
                || !PrisonerEligibilityUtility.IsNegotiatingFaction(record.OriginalFaction))
            {
                return -1;
            }

            EnsureFactionOfferSchedule(record);
            int effectiveContactTick = record.LastProposalTick < 0
                ? record.ScheduledFactionOfferTick
                : Math.Max(record.ScheduledFactionOfferTick, record.LastProposalTick + ProposalCooldownTicks);
            FactionNegotiationMemory memory = GetFactionMemory(record.OriginalFaction, false);
            effectiveContactTick = Math.Max(effectiveContactTick, record.UnsolicitedOfferSuppressedUntilTick);
            effectiveContactTick = Math.Max(effectiveContactTick, memory?.UnsolicitedOffersSuppressedUntilTick ?? -1);
            return Math.Max(0, effectiveContactTick - Find.TickManager.TicksGame);
        }

        public void RefreshFactionOfferSchedules(bool anchorDueOffersToNow = false)
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            foreach (PrisonerRecord record in records.ToList())
            {
                if (record == null || record.Pawn == null || record.ActiveDealId != null)
                {
                    continue;
                }

                int anchor = anchorDueOffersToNow && record.ScheduledFactionOfferTick <= now
                    ? now
                    : Math.Max(record.CapturedTick, record.LastProposalTick);
                ScheduleNextFactionOffer(record, anchor);
            }
        }

        private static void RefreshRecordValue(PrisonerRecord record)
        {
            if (record?.Pawn == null || record.Pawn.Dead || record.Pawn.Destroyed)
            {
                return;
            }

            record.Importance = PrisonerValueCalculator.Classify(record.Pawn, record.OriginalFaction);
            record.DiplomaticValue = Math.Max(0,
                PrisonerValueCalculator.Calculate(record.Pawn, record.CapturedMarketValue, record.Importance)
                + PrisonerDiplomacyExtensionRegistry.GetDiplomaticValueAdjustment(
                    record.Pawn,
                    record.OriginalFaction));
        }

        public bool CanStartPlayerNegotiation(PrisonerRecord record, out TaggedString reason)
        {
            reason = TaggedString.Empty;
            if (!CanCreateNewPrisonerDiplomacyDeal(out reason))
            {
                return false;
            }
            if (record?.Pawn == null || !PrisonerEligibilityUtility.IsEligible(record.Pawn, out _))
            {
                reason = "PD_NegotiationUnavailable".Translate();
                return false;
            }

            if (GetActiveDeal(record.Pawn) != null)
            {
                reason = "PD_NegotiationActiveDeal".Translate();
                return false;
            }

            int now = Find.TickManager.TicksGame;
            int prisonerRemaining = record.LastPlayerNegotiationTick < 0
                ? 0
                : PlayerPrisonerCooldownTicks - (now - record.LastPlayerNegotiationTick);
            if (prisonerRemaining > 0)
            {
                reason = "PD_NegotiationPrisonerCooldown".Translate(prisonerRemaining.ToStringTicksToPeriod());
                return false;
            }

            FactionNegotiationMemory memory = GetFactionMemory(record.OriginalFaction, false);
            int suspensionRemaining = memory == null
                ? 0
                : memory.NegotiationSuspendedUntilTick - now;
            if (suspensionRemaining > 0)
            {
                reason = "PD_NegotiationSuspended".Translate(suspensionRemaining.ToStringTicksToPeriod());
                return false;
            }

            int factionRemaining = memory == null || memory.LastPlayerNegotiationTick < 0
                ? 0
                : PlayerFactionCooldownTicks - (now - memory.LastPlayerNegotiationTick);
            if (factionRemaining > 0)
            {
                reason = "PD_NegotiationFactionCooldown".Translate(factionRemaining.ToStringTicksToPeriod());
                return false;
            }

            return true;
        }

        public NegotiationResult SubmitPlayerDemand(PrisonerRecord record, Pawn negotiator, int demand)
        {
            return SubmitPlayerDemand(record, negotiator, new RewardDemand { Silver = demand });
        }

        public NegotiationResult SubmitPlayerDemand(
            PrisonerRecord record,
            Pawn negotiator,
            RewardDemand demand,
            string aiWindowContextId = null,
            string playerNote = null)
        {
            if (!CanStartPlayerNegotiation(record, out TaggedString reason))
            {
                Messages.Message(reason, MessageTypeDefOf.RejectInput, false);
                return null;
            }

            if (!NegotiationEconomyUtility.IsDemandValid(
                record.OriginalFaction,
                demand,
                out string invalidReasonKey,
                record.Pawn))
            {
                Messages.Message(invalidReasonKey.Translate(), MessageTypeDefOf.RejectInput, false);
                return null;
            }

            RefreshRecordValue(record);
            int now = Find.TickManager.TicksGame;
            FactionNegotiationMemory memory = GetFactionMemory(record.OriginalFaction, true);
            int reserve = GetSpendableReserve(record.OriginalFaction, now);
            int materialCap = NegotiationEconomyUtility.CalculateMaterialRewardCap(record.Pawn.MapHeld);
            int round = 1;
            float memoryMultiplier = GetFactionMemoryMultiplier(record.OriginalFaction, now);
            bool careCreditAvailable = HasCareCredit(record.OriginalFaction);
            NegotiationResult result = PrisonerNegotiationUtility.Evaluate(
                record,
                negotiator,
                demand,
                reserve,
                materialCap,
                round,
                memoryMultiplier,
                GetNegotiationBudgetMultiplier(record));
            record.LastPlayerNegotiationTick = now;
            record.NegotiationCount++;
            memory.LastPlayerNegotiationTick = now;
            ScheduleNextFactionOffer(record, now);

            if (result.Outcome == NegotiationOutcome.Accepted)
            {
                PrisonerDeal deal = CreatePlayerDeal(
                    record,
                    negotiator,
                    result,
                    DealState.AcceptedAwaitingRelease,
                    careCreditAvailable);
                if (careCreditAvailable)
                {
                    ConsumeCareCredit(record.OriginalFaction);
                }
                TaggedString response = "PD_PlayerDemandAcceptedRewards".Translate(
                    record.Pawn.LabelShortCap,
                    deal.Rewards.Description());
                Messages.Message(response, record.Pawn, MessageTypeDefOf.PositiveEvent, false);
                QueueAiNarrative(
                    AiNarrativeEventKind.PlayerDemandAccepted,
                    record.Pawn,
                    record.OriginalFaction,
                    deal,
                    "accepted",
                    deal.Rewards.Description(),
                    response.ToString(),
                    null,
                    -1,
                    playerNote);
            }
            else if (result.Outcome == NegotiationOutcome.Countered)
            {
                PrisonerDeal deal = CreatePlayerDeal(
                    record,
                    negotiator,
                    result,
                    DealState.Negotiating,
                    careCreditAvailable);
                if (careCreditAvailable)
                {
                    ConsumeCareCredit(record.OriginalFaction);
                }
                TaggedString response = "PD_PlayerDemandCountered".Translate(
                    record.Pawn.LabelShortCap,
                    deal.Rewards.Description());
                Messages.Message(response, record.Pawn, MessageTypeDefOf.NeutralEvent, false);
                QueueAiNarrative(
                    AiNarrativeEventKind.PlayerDemandCountered,
                    record.Pawn,
                    record.OriginalFaction,
                    deal,
                    "countered",
                    deal.Rewards.Description(),
                    response.ToString(),
                    aiWindowContextId,
                    -1,
                    playerNote);
            }
            else
            {
                ApplyRejectedNegotiation(memory, result);
                TaggedString response = "PD_PlayerDemandRejectedRewards".Translate(
                    record.Pawn.LabelShortCap,
                    demand.Description());
                Messages.Message(response, record.Pawn, MessageTypeDefOf.NegativeEvent, false);
                QueueAiNarrative(
                    AiNarrativeEventKind.PlayerDemandRejected,
                    record.Pawn,
                    record.OriginalFaction,
                    null,
                    "rejected",
                    demand.Description(),
                    response.ToString(),
                    null,
                    record.NegotiationCount,
                    playerNote);
            }

            return result;
        }

        public NegotiationResult RevisePlayerDemand(
            PrisonerDeal deal,
            RewardDemand demand,
            string aiWindowContextId = null,
            string playerNote = null)
        {
            if (deal == null || deal.State != DealState.Negotiating || deal.NegotiationRound >= 2)
            {
                return null;
            }

            string invalidReasonKey = null;
            PrisonerRecord record = GetRecord(deal.Prisoner);
            if (record == null || !ValidatePrisonerStillHeld(deal)
                || !NegotiationEconomyUtility.IsDemandValid(
                    deal.Faction,
                    demand,
                    out invalidReasonKey,
                    deal.Prisoner))
            {
                if (!string.IsNullOrEmpty(invalidReasonKey))
                {
                    Messages.Message(invalidReasonKey.Translate(), MessageTypeDefOf.RejectInput, false);
                }
                return null;
            }

            int now = Find.TickManager.TicksGame;
            FactionNegotiationMemory memory = GetFactionMemory(deal.Faction, true);
            int reserve = GetSpendableReserve(deal.Faction, now, deal);
            int materialCap = NegotiationEconomyUtility.CalculateMaterialRewardCap(deal.Map);
            int round = deal.NegotiationRound + 1;
            float memoryMultiplier = GetFactionMemoryMultiplier(deal.Faction, now);
            NegotiationResult result = PrisonerNegotiationUtility.Evaluate(
                record,
                deal.Negotiator,
                demand,
                reserve,
                materialCap,
                round,
                memoryMultiplier,
                GetNegotiationBudgetMultiplier(record, deal));
            deal.NegotiationRound = round;
            deal.LastPlayerDemand = demand.Clone();
            deal.NegotiationDemandCost = result.DemandCost;
            deal.NegotiationBudget = result.NegotiationBudget;
            deal.NegotiationOutcome = result.Outcome;
            deal.NegotiationSeed = result.Seed;

            if (result.Outcome == NegotiationOutcome.Accepted)
            {
                deal.Rewards = demand.Clone();
                deal.SilverAmount = deal.Rewards.Silver;
                AcceptNegotiatedDeal(deal);
                QueueAiNarrative(
                    AiNarrativeEventKind.PlayerDemandAccepted,
                    deal.Prisoner,
                    deal.Faction,
                    deal,
                    "accepted",
                    deal.Rewards.Description(),
                    "PD_PlayerDemandAcceptedRewards".Translate(
                        deal.Prisoner?.LabelShortCap ?? deal.PrisonerLoadId,
                        deal.Rewards.Description()).ToString(),
                    null,
                    -1,
                    playerNote);
            }
            else if (result.Outcome == NegotiationOutcome.Countered)
            {
                deal.Rewards = result.CounterOffer.Clone();
                deal.SilverAmount = deal.Rewards.Silver;
                deal.OfferExpiresTick = now + ProposalDurationTicks;
                TaggedString response = "PD_PlayerDemandFinalCounter".Translate(deal.Rewards.Description());
                Messages.Message(response, deal.Prisoner, MessageTypeDefOf.NeutralEvent, false);
                QueueAiNarrative(
                    AiNarrativeEventKind.FinalCounter,
                    deal.Prisoner,
                    deal.Faction,
                    deal,
                    "countered",
                    deal.Rewards.Description(),
                    response.ToString(),
                    aiWindowContextId,
                    -1,
                    playerNote);
            }
            else
            {
                ApplyRejectedNegotiation(memory, result);
                deal.State = DealState.Rejected;
                deal.CompletedTick = now;
                ClearRecordDeal(deal);
                TaggedString response = "PD_PlayerDemandRejectedRewards".Translate(
                    deal.Prisoner?.LabelShortCap ?? deal.PrisonerLoadId,
                    demand.Description());
                Messages.Message(response, MessageTypeDefOf.NegativeEvent, false);
                QueueAiNarrative(
                    AiNarrativeEventKind.PlayerDemandRejected,
                    deal.Prisoner,
                    deal.Faction,
                    deal,
                    "rejected",
                    demand.Description(),
                    response.ToString(),
                    null,
                    -1,
                    playerNote);
            }

            return result;
        }

        public bool AcceptCounterOffer(PrisonerDeal deal)
        {
            if (deal == null || deal.State != DealState.Negotiating || !ValidatePrisonerStillHeld(deal))
            {
                return false;
            }

            int now = Find.TickManager.TicksGame;
            int cost = NegotiationEconomyUtility.CalculateDemandCost(deal.Faction, deal.Rewards);
            int materialCost = NegotiationEconomyUtility.CalculateMaterialCost(deal.Faction, deal.Rewards);
            if (cost > GetSpendableReserve(deal.Faction, now, deal)
                || materialCost > NegotiationEconomyUtility.CalculateMaterialRewardCap(deal.Map))
            {
                Messages.Message("PD_CounterNoLongerAffordable".Translate(), deal.Prisoner, MessageTypeDefOf.RejectInput, false);
                return false;
            }

            AcceptNegotiatedDeal(deal);
            return true;
        }

        public void RejectCounterOffer(PrisonerDeal deal)
        {
            if (deal == null || deal.State != DealState.Negotiating)
            {
                return;
            }

            FactionNegotiationMemory memory = GetFactionMemory(deal.Faction, true);
            memory.RejectedNegotiations++;
            deal.State = DealState.Rejected;
            deal.CompletedTick = Find.TickManager.TicksGame;
            ClearRecordDeal(deal);
            Messages.Message("PD_CounterOfferRejected".Translate(deal.Prisoner?.LabelShortCap ?? deal.PrisonerLoadId), MessageTypeDefOf.NeutralEvent, false);
        }

        private PrisonerDeal CreatePlayerDeal(
            PrisonerRecord record,
            Pawn negotiator,
            NegotiationResult result,
            DealState state,
            bool careCreditApplied = false)
        {
            int now = Find.TickManager.TicksGame;
            string dealId = "PD-" + nextDealSequence++.ToString("D6");
            RewardDemand rewards = result.Outcome == NegotiationOutcome.Countered
                ? result.CounterOffer.Clone()
                : result.RequestedRewards.Clone();
            PrisonerDeal deal = new PrisonerDeal
            {
                DealId = dealId,
                Prisoner = record.Pawn,
                PrisonerLoadId = record.PawnLoadId,
                Faction = record.OriginalFaction,
                Map = record.Pawn.MapHeld,
                MapLoadId = record.Pawn.MapHeld?.GetUniqueLoadID(),
                SilverAmount = rewards.Silver,
                Rewards = rewards,
                LastPlayerDemand = result.RequestedRewards.Clone(),
                NegotiationRound = result.NegotiationRound,
                NegotiationBudget = result.NegotiationBudget,
                NegotiationDemandCost = result.DemandCost,
                CareCreditApplied = careCreditApplied,
                CreatedTick = now,
                OfferExpiresTick = state == DealState.Negotiating ? now + ProposalDurationTicks : now,
                FulfillmentExpiresTick = now + FulfillmentDurationTicks,
                LastTreatmentTickAtExtension = state == DealState.Negotiating ? -1 : now - 1,
                AcceptedTick = state == DealState.AcceptedAwaitingRelease ? now : -1,
                State = state,
                Origin = DealOrigin.PlayerDemand,
                Negotiator = negotiator,
                NegotiatorSocialSkill = result.SocialSkill,
                NegotiationOutcome = result.Outcome,
                NegotiationSeed = result.Seed,
                NegotiationType = FactionNegotiationUtility.GetType(record.OriginalFaction)
            };
            AssignPirateRisk(deal, PrisonerDiplomacyTuning.EffectiveReliability(
                GetFactionMemory(record.OriginalFaction, true)));
            deals.Add(deal);
            record.ActiveDealId = deal.DealId;
            return deal;
        }

        private void AcceptNegotiatedDeal(PrisonerDeal deal)
        {
            deal.State = DealState.AcceptedAwaitingRelease;
            deal.AcceptedTick = Find.TickManager.TicksGame;
            deal.FulfillmentExpiresTick = deal.AcceptedTick + FulfillmentDurationTicks;
            deal.LastTreatmentTickAtExtension = deal.AcceptedTick - 1;
            AssignPirateRisk(deal, PrisonerDiplomacyTuning.EffectiveReliability(
                GetFactionMemory(deal.Faction, true)));
            TryScheduleAcceptedDealEvents(deal);
            Messages.Message("PD_PlayerDemandAcceptedRewards".Translate(
                deal.Prisoner.LabelShortCap,
                deal.Rewards.Description()), deal.Prisoner, MessageTypeDefOf.PositiveEvent, false);
        }

        public bool AcceptDeal(string dealId)
        {
            PrisonerDeal deal = GetDeal(dealId);
            if (deal == null || deal.State != DealState.Offered || !ValidatePrisonerStillHeld(deal))
            {
                return false;
            }

            int now = Find.TickManager.TicksGame;
            if (now >= deal.OfferExpiresTick)
            {
                FailDeal(deal, DealState.Expired);
                return false;
            }

            deal.State = DealState.AcceptedAwaitingRelease;
            deal.AcceptedTick = now;
            deal.FulfillmentExpiresTick = now + FulfillmentDurationTicks;
            deal.LastTreatmentTickAtExtension = now - 1;
            AssignPirateRisk(deal, PrisonerDiplomacyTuning.EffectiveReliability(
                GetFactionMemory(deal.Faction, true)));
            TryScheduleAcceptedDealEvents(deal);
            PrisonerRecord record = GetRecord(deal.Prisoner);
            if (record != null)
            {
                record.ActiveDealId = deal.DealId;
                record.UnsolicitedOfferSuppressedUntilTick = -1;
                record.UnsolicitedOfferRejectionCount = 0;
            }

            FactionNegotiationMemory acceptedMemory = GetFactionMemory(deal.Faction, false);
            if (acceptedMemory != null)
            {
                acceptedMemory.UnsolicitedOffersSuppressedUntilTick = -1;
                acceptedMemory.UnsolicitedOfferRejectionCount = 0;
            }

            RemoveOfferLetter(deal.DealId);
            Messages.Message("PD_DealAccepted".Translate(deal.Prisoner.LabelShortCap), deal.Prisoner, MessageTypeDefOf.PositiveEvent, false);
            return true;
        }

        public void RejectDeal(string dealId)
        {
            RejectDeal(dealId, false);
        }

        public void RejectAndMuteFaction(string dealId)
        {
            RejectDeal(dealId, true);
        }

        private void RejectDeal(string dealId, bool muteFaction)
        {
            PrisonerDeal deal = GetDeal(dealId);
            if (deal == null || deal.State != DealState.Offered)
            {
                return;
            }

            int now = Find.TickManager.TicksGame;
            deal.State = DealState.Rejected;
            deal.CompletedTick = now;
            FactionNegotiationMemory memory = GetFactionMemory(deal.Faction, true);
            memory.RejectedNegotiations++;
            if (deal.Origin == DealOrigin.FactionOffer)
            {
                PrisonerRecord record = GetRecord(deal.Prisoner);
                if (record != null)
                {
                    record.UnsolicitedOfferRejectionCount++;
                    record.UnsolicitedOfferSuppressedUntilTick = Math.Max(
                        record.UnsolicitedOfferSuppressedUntilTick,
                        now + (muteFaction ? MutedFactionOfferCooldownTicks : RejectedOfferCooldownTicks));
                    ScheduleNextFactionOffer(record, now);
                }

                memory.UnsolicitedOfferRejectionCount++;
                memory.UnsolicitedOffersSuppressedUntilTick = Math.Max(
                    memory.UnsolicitedOffersSuppressedUntilTick,
                    now + (muteFaction
                        ? MutedFactionOfferCooldownTicks
                        : FactionOfferCooldownAfterRejectionTicks));
            }
            ClearRecordDeal(deal);
            RemoveOfferLetter(deal.DealId);
            Messages.Message((muteFaction ? "PD_DealRejectedAndFactionMuted" : "PD_DealRejected")
                .Translate(deal.Prisoner?.LabelShortCap ?? deal.PrisonerLoadId), MessageTypeDefOf.NeutralEvent, false);
        }

        public bool OrderRansomRelease(Pawn pawn)
        {
            PrisonerDeal deal = GetActiveDeal(pawn);
            if (deal == null || deal.State != DealState.AcceptedAwaitingRelease || !ValidatePrisonerStillHeld(deal))
            {
                return false;
            }

            if (Find.TickManager.TicksGame >= deal.FulfillmentExpiresTick)
            {
                if (!TryExtendFulfillment(deal, Find.TickManager.TicksGame))
                {
                    FailDeal(deal, DealState.Expired);
                    return false;
                }
            }

            if ((deal.ReturnedHostage != null || !string.IsNullOrEmpty(deal.ReturnedHostageLoadId))
                && !PrisonerExchangeUtility.IsHeldByFaction(deal.Faction, deal.ReturnedHostage))
            {
                FailDeal(deal, DealState.FailedHostageInvalid);
                return false;
            }

            SetDealMap(deal, pawn.MapHeld);
            deal.State = DealState.ReleaseOrdered;
            deal.ReleaseOrderedTick = Find.TickManager.TicksGame;
            pawn.guest.SetExclusiveInteraction(PrisonerInteractionModeDefOf.Release);
            Messages.Message("PD_RansomReleaseOrdered".Translate(pawn.LabelShortCap), pawn, MessageTypeDefOf.PositiveEvent, false);
            return true;
        }

        public bool CancelAcceptedDeal(Pawn pawn)
        {
            PrisonerDeal deal = GetActiveDeal(pawn);
            if (deal == null || deal.State != DealState.AcceptedAwaitingRelease)
            {
                return false;
            }

            deal.State = DealState.Cancelled;
            deal.CompletedTick = Find.TickManager.TicksGame;
            ClearRecordDeal(deal);
            Messages.Message("PD_DealCancelledByPlayer".Translate(pawn.LabelShortCap), pawn, MessageTypeDefOf.NeutralEvent, false);
            return true;
        }

        private static void AssignPirateRisk(PrisonerDeal deal, float reliability)
        {
            if (deal == null || deal.PirateRisk != PirateDealRisk.None)
            {
                return;
            }

            deal.NegotiationType = FactionNegotiationUtility.GetType(deal.Faction);
            deal.PirateRisk = FactionNegotiationUtility.DetermineRisk(
                deal.Faction,
                deal.DealId,
                deal.NegotiatorSocialSkill,
                reliability);
            if (deal.PirateRisk != PirateDealRisk.None
                && NegotiationEconomyUtility.CreateSaferPirateTerms(deal.Faction, deal.Rewards) == null)
            {
                deal.PirateRisk = PirateDealRisk.None;
            }
            if (deal.Rewards?.CeasefireDays > 0
                && deal.PirateRisk != PirateDealRisk.None
                && deal.PirateRisk != PirateDealRisk.DelayedPayment)
            {
                deal.PirateRisk = PirateDealRisk.DelayedPayment;
            }
            deal.PirateRiskDisclosed = deal.PirateRisk != PirateDealRisk.None;
        }

        public void NotifyVanillaRelease(Pawn pawn)
        {
            PrisonerDeal deal = GetActiveDeal(pawn);
            if (deal != null && deal.State == DealState.ReleaseOrdered)
            {
                deal.VanillaReleaseConfirmed = true;
            }
        }

        public void NotifyPlayerMedicalTreatment(Pawn patient, Pawn doctor)
        {
            if (patient == null
                || doctor == null
                || doctor.Faction != Faction.OfPlayer
                || !patient.IsPrisonerOfColony)
            {
                return;
            }

            PrisonerRecord record = GetRecord(patient);
            if (record != null)
            {
                record.LastPlayerTreatmentTick = Find.TickManager.TicksGame;
            }
        }

        public void NotifyPawnExited(Pawn pawn, Map mapBeforeExit, bool wasVanillaReleased)
        {
            string pawnLoadId = pawn?.GetUniqueLoadID();
            if (!string.IsNullOrEmpty(pawnLoadId)
                && pendingCaravanTransferExits.Remove(pawnLoadId))
            {
                return;
            }

            PrisonerDeal deal = GetActiveDeal(pawn);
            if (deal == null)
            {
                if (wasVanillaReleased)
                {
                    RecordUnconditionalRelease(pawn);
                }
                else
                {
                    RecordEscape(pawn);
                }
                return;
            }

            if (wasVanillaReleased
                && (deal.State == DealState.Offered || deal.State == DealState.Negotiating))
            {
                FailDeal(deal, DealState.Cancelled, false, false);
                RecordUnconditionalRelease(pawn);
                return;
            }

            if (deal.State != DealState.Offered && Find.TickManager.TicksGame >= deal.FulfillmentExpiresTick)
            {
                if (!TryExtendFulfillment(deal, Find.TickManager.TicksGame))
                {
                    FailDeal(deal, DealState.Expired);
                    return;
                }
            }

            // A pawn can be transferred between player maps between the release
            // command and the next 2,000-tick scan. Accept the actual exit map as
            // the authoritative delivery location when vanilla release confirmed it.
            if (deal.State == DealState.ReleaseOrdered
                && deal.VanillaReleaseConfirmed
                && wasVanillaReleased
                && mapBeforeExit != null
                && Find.Maps.Contains(mapBeforeExit)
                && deal.Map != mapBeforeExit)
            {
                SetDealMap(deal, mapBeforeExit);
                CompatibilityDiagnostics.LogIssueOnce(
                    "release-map-transfer:" + deal.DealId,
                    "Updated delivery map after a legal cross-map prisoner transfer for deal=" + deal.DealId + ".");
            }

            bool validRelease = deal.State == DealState.ReleaseOrdered
                && deal.VanillaReleaseConfirmed
                && wasVanillaReleased
                && !pawn.Dead
                && !pawn.Spawned
                && pawn.Map == null
                && pawn.Faction != Faction.OfPlayer
                && mapBeforeExit != null
                && deal.Map == mapBeforeExit;

            if (validRelease)
            {
                if ((deal.ReturnedHostage != null || !string.IsNullOrEmpty(deal.ReturnedHostageLoadId))
                    && !PrisonerExchangeUtility.IsHeldByFaction(deal.Faction, deal.ReturnedHostage))
                {
                    FailDeal(deal, DealState.FailedHostageInvalid);
                    return;
                }

                deal.PrisonerDelivered = true;
                deal.PrisonerDeliveredTick = Find.TickManager.TicksGame;
                deal.State = DealState.FulfillmentPending;
                FulfillDeal(deal);
                return;
            }

            if ((deal.State == DealState.Offered || deal.State == DealState.Negotiating)
                && !wasVanillaReleased)
            {
                FailDeal(deal, DealState.FailedEscaped, false, false);
                RecordEscape(pawn);
                return;
            }

            FailDeal(deal, DealState.FailedEscaped);
        }

        private void RecordEscape(Pawn pawn)
        {
            PrisonerRecord record = GetRecord(pawn);
            if (record == null || record.TerminalOutcomeRecorded)
            {
                return;
            }

            record.TerminalOutcomeRecorded = true;
            ApplyMemoryChange(record.OriginalFaction, 0f, 0f, 0f,
                "PD_MemoryEventEscaped", pawn.LabelShortCap, false);
        }

        public void NotifyPawnSold(Pawn pawn)
        {
            PrisonerRecord record = GetRecord(pawn);
            PrisonerDeal deal = GetActiveDeal(pawn);
            if (deal != null)
            {
                FailDeal(deal, DealState.FailedSoldOrTransferred);
            }
            else if (record != null && !record.TerminalOutcomeRecorded)
            {
                record.TerminalOutcomeRecorded = true;
                ApplyMemoryChange(record.OriginalFaction, 0f, -10f, 10f,
                    "PD_MemoryEventSold", pawn?.LabelShortCap, true);
            }
        }

        public void NotifyBodyPartRemoved(Pawn pawn)
        {
            PrisonerRecord record = GetRecord(pawn);
            if (record == null || record.TerminalOutcomeRecorded)
            {
                return;
            }

            int now = Find.TickManager.TicksGame;
            if (record.LastPermanentHarmTick == now)
            {
                return;
            }

            record.PlayerCausedPermanentHarm = true;
            record.LastPermanentHarmTick = now;
            record.LastMissingPartCount = PrisonerTreatmentUtility.CountMissingParts(pawn);
            ApplyMemoryChange(record.OriginalFaction, 0f, -18f, 18f,
                "PD_MemoryEventOrganHarvested", pawn.LabelShortCap, true);
        }

        public void NotifyPrisonerTookDamage(Pawn pawn, DamageInfo damageInfo)
        {
            PrisonerRecord record = GetRecord(pawn);
            if (record == null || record.TerminalOutcomeRecorded || damageInfo.Def == null)
            {
                return;
            }

            string attacker = damageInfo.Instigator is Pawn instigatorPawn
                ? instigatorPawn.LabelShortCap
                : damageInfo.Instigator?.LabelCap ?? "unknown source";
            string weapon = damageInfo.Weapon?.label;
            string damage = damageInfo.Def.label;
            string hitPart = damageInfo.HitPart?.LabelCap;
            string detail = string.IsNullOrWhiteSpace(weapon)
                ? attacker + " caused " + damage
                : attacker + " used " + weapon + " causing " + damage;
            if (!string.IsNullOrWhiteSpace(hitPart))
            {
                detail += " to " + hitPart;
            }

            record.RecentBattleEvents = record.RecentBattleEvents ?? new List<PrisonerBattleEvent>();
            record.RecentBattleEvents.Insert(0, new PrisonerBattleEvent
            {
                Tick = Find.TickManager.TicksGame,
                Description = detail.Length > 240 ? detail.Substring(0, 240) : detail
            });
            if (record.RecentBattleEvents.Count > 4)
            {
                record.RecentBattleEvents.RemoveRange(4, record.RecentBattleEvents.Count - 4);
            }
        }

        public void NotifyPlayerCausedPermanentHarm(Pawn pawn, int missingPartsBefore, DamageInfo damageInfo)
        {
            PrisonerRecord record = GetRecord(pawn);
            if (record == null
                || record.TerminalOutcomeRecorded
                || damageInfo.Instigator?.Faction != Faction.OfPlayer)
            {
                return;
            }

            int missingPartsAfter = PrisonerTreatmentUtility.CountMissingParts(pawn);
            if (missingPartsAfter <= missingPartsBefore && !damageInfo.InstantPermanentInjury)
            {
                return;
            }

            int now = Find.TickManager.TicksGame;
            if (record.LastPermanentHarmTick == now)
            {
                return;
            }

            record.PlayerCausedPermanentHarm = true;
            record.LastPermanentHarmTick = now;
            record.LastMissingPartCount = missingPartsAfter;
            record.LastPermanentInjuryCount = PrisonerTreatmentUtility.CountPermanentInjuries(pawn);
            ApplyMemoryChange(record.OriginalFaction, 0f, -12f, 10f,
                "PD_MemoryEventPermanentHarm", pawn.LabelShortCap, true);
        }

        public void NotifyPawnKilled(Pawn pawn, DamageInfo? damageInfo)
        {
            PrisonerRecord record = GetRecord(pawn);
            if (record == null || record.TerminalOutcomeRecorded)
            {
                return;
            }

            bool playerResponsible = damageInfo?.Instigator?.Faction == Faction.OfPlayer
                || record.PlayerCausedPermanentHarm
                || record.MalnutritionRecorded;
            PrisonerDeal deal = GetActiveDeal(pawn);
            if (deal != null)
            {
                FailDeal(deal, DealState.FailedPrisonerDead, playerResponsible);
            }
            else
            {
                RecordTerminalOutcome(record, DealState.FailedPrisonerDead, false, playerResponsible);
            }
        }

        public void NotifyPawnJoinedPlayer(Pawn pawn, bool enslaved)
        {
            PrisonerRecord record = GetRecord(pawn);
            if (record == null || record.TerminalOutcomeRecorded)
            {
                return;
            }

            DealState outcome = enslaved ? DealState.FailedEnslaved : DealState.FailedRecruited;
            PrisonerDeal deal = GetActiveDeal(pawn);
            if (deal != null)
            {
                FailDeal(deal, outcome, true);
            }
            else
            {
                RecordTerminalOutcome(record, outcome, false, true);
            }
        }

        public PrisonerDeal ForceOffer(Pawn pawn)
        {
            PrisonerRecord record = RegisterPawn(pawn);
            return record == null ? null : CreateOffer(record, true);
        }

        public bool DebugCompleteDelivery(Pawn pawn)
        {
            PrisonerDeal deal = GetActiveDeal(pawn);
            if (deal == null)
            {
                return false;
            }

            if (deal.State == DealState.Offered && !AcceptDeal(deal.DealId))
            {
                return false;
            }

            if (deal.State == DealState.AcceptedAwaitingRelease && !OrderRansomRelease(pawn))
            {
                return false;
            }

            if (deal.State == DealState.FulfillmentPending)
            {
                deal.PaymentDueTick = Find.TickManager.TicksGame;
                FulfillDeal(deal);
                return deal.State == DealState.Completed;
            }

            if (deal.State != DealState.ReleaseOrdered)
            {
                return false;
            }

            deal.VanillaReleaseConfirmed = true;
            deal.PrisonerDelivered = true;
            deal.PrisonerDeliveredTick = Find.TickManager.TicksGame;
            deal.State = DealState.FulfillmentPending;
            FulfillDeal(deal);
            return deal.State == DealState.Completed;
        }

        public void LogPawnDiagnostics(Pawn pawn)
        {
            PrisonerRecord record = GetRecord(pawn) ?? RegisterPawn(pawn);
            if (record == null)
            {
                Log.Message("[Prisoner Diplomacy Debug] No eligible record for pawn="
                    + (pawn?.LabelShortCap ?? "null") + ".");
                return;
            }

            Faction faction = record.OriginalFaction;
            FactionNegotiationMemory memory = GetFactionMemory(faction, true);
            int now = Find.TickManager.TicksGame;
            Log.Message("[Prisoner Diplomacy Debug] pawn=" + record.Pawn.LabelShortCap
                + " id=" + record.PawnLoadId
                + " faction=" + (faction?.Name ?? "null")
                + " type=" + FactionNegotiationUtility.GetType(faction)
                + " importance=" + record.Importance
                + " cv=" + record.DiplomaticValue
                + " adjustedCv=" + PrisonerDiplomacyTuning.ScaleRansomValue(record.DiplomaticValue)
                + " offer=" + PrisonerValueCalculator.CalculateOffer(record,
                    GetFactionMemoryMultiplier(faction, now))
                + " budget=" + NegotiationEconomyUtility.CalculateNegotiationBudget(
                    record, record.Pawn.MapHeld?.mapPawns?.FreeColonistsSpawned?.FirstOrDefault(),
                    GetFactionMemoryMultiplier(faction, now))
                + " reserve=" + GetSpendableReserve(faction, now)
                + " finances=" + GetFactionFinancialStatus(faction)
                + " memory=" + PrisonerDiplomacyTuning.EffectiveReliability(memory).ToString("F1")
                + "/" + (PrisonerDiplomacyTuning.FactionMemoryEnabled ? memory?.Treatment ?? 0f : 0f).ToString("F1")
                + "/" + (PrisonerDiplomacyTuning.FactionMemoryEnabled ? memory?.Resentment ?? 0f : 0f).ToString("F1")
                + " activeDeal=" + (GetActiveDeal(pawn)?.DealId ?? "none") + ".");
        }

        public bool DebugAcceptSelectedDeal(Pawn pawn)
        {
            PrisonerDeal deal = GetActiveDeal(pawn);
            if (deal == null)
            {
                deal = deals.FirstOrDefault(candidate => candidate?.Prisoner == pawn && candidate.State == DealState.Offered);
            }

            return deal != null && (deal.State == DealState.Offered
                ? AcceptDeal(deal.DealId)
                : deal.State == DealState.Negotiating && AcceptCounterOffer(deal));
        }

        public bool DebugRejectSelectedDeal(Pawn pawn)
        {
            PrisonerDeal deal = GetActiveDeal(pawn)
                ?? deals.FirstOrDefault(candidate => candidate?.Prisoner == pawn
                    && (candidate.State == DealState.Offered || candidate.State == DealState.Negotiating));
            if (deal == null)
            {
                return false;
            }

            if (deal.State == DealState.Negotiating)
            {
                RejectCounterOffer(deal);
            }
            else
            {
                RejectDeal(deal.DealId);
            }

            return true;
        }

        public bool DebugExpireSelectedDeal(Pawn pawn)
        {
            PrisonerDeal deal = GetActiveDeal(pawn)
                ?? deals.FirstOrDefault(candidate => candidate?.Prisoner == pawn && candidate.IsActive);
            if (deal == null)
            {
                return false;
            }

            int now = Find.TickManager.TicksGame;
            if (deal.State == DealState.Offered || deal.State == DealState.Negotiating)
            {
                deal.OfferExpiresTick = now;
            }
            else
            {
                deal.FulfillmentExpiresTick = now;
            }

            UpdateDeal(deal, now);
            return !deal.IsActive;
        }

        public bool DebugFailSelectedDeal(Pawn pawn, DealState failureState)
        {
            PrisonerDeal deal = GetActiveDeal(pawn);
            if (deal == null || !deal.IsActive)
            {
                PrisonerRecord record = GetRecord(pawn);
                if (record == null || record.TerminalOutcomeRecorded)
                {
                    return false;
                }

                RecordTerminalOutcome(record, failureState, false, true);
                return record.TerminalOutcomeRecorded;
            }

            FailDeal(deal, failureState, true);
            return !deal.IsActive;
        }

        public bool DebugAdjustMemory(Pawn pawn, float reliability, float treatment, float resentment)
        {
            PrisonerRecord record = GetRecord(pawn);
            if (record?.OriginalFaction == null)
            {
                return false;
            }

            ApplyMemoryChange(record.OriginalFaction, reliability, treatment, resentment,
                "PD_MemoryEventDebugAdjustment", pawn.LabelShortCap, false);
            return PrisonerDiplomacyTuning.FactionMemoryEnabled;
        }

        public bool DebugRefillReserve(Pawn pawn)
        {
            PrisonerRecord record = GetRecord(pawn);
            if (record?.OriginalFaction == null)
            {
                return false;
            }

            FactionNegotiationMemory memory = GetFactionMemory(record.OriginalFaction, true);
            memory.DiplomaticReserve = NegotiationEconomyUtility.CalculateMaximumReserve(record.OriginalFaction);
            memory.ReserveUpdatedTick = Find.TickManager.TicksGame;
            return true;
        }

        public void LogSummary()
        {
            int activeDeals = deals.Count(deal => deal.IsActive);
            Log.Message("[Prisoner Diplomacy] " + "PD_DebugRecords".Translate(records.Count, activeDeals));
            foreach (FactionNegotiationMemory memory in factionNegotiationMemories)
            {
                UpdateFactionMemory(memory, Find.TickManager.TicksGame);
                Log.Message("[Prisoner Diplomacy] Memory faction=" + memory.Faction?.Name
                    + " reliability=" + memory.Reliability.ToString("F1")
                    + " treatment=" + memory.Treatment.ToString("F1")
                    + " resentment=" + memory.Resentment.ToString("F1")
                    + " multiplier=" + FactionPrisonerMemoryUtility.CalculateMultiplier(memory.Faction, memory).ToString("F3"));
            }
        }

        internal IReadOnlyList<PrisonerDiplomacyEventRecord> DiplomacyEvents => diplomacyEvents;

        internal bool TryScheduleExtensionEvent(
            string definitionId,
            PrisonerDiplomacyEventKind kind,
            Faction faction,
            Map map,
            Pawn prisoner,
            string sourceDealId,
            int triggerTick,
            string extensionId = "g1061.prisonerdiplomacy.core")
        {
            if (string.IsNullOrWhiteSpace(definitionId)
                || faction == null
                || faction == Faction.OfPlayer
                || diplomacyEvents.Any(item => item != null
                    && item.IsActive
                    && item.DefinitionId == definitionId
                    && item.Faction == faction
                    && item.PrisonerLoadId == (prisoner?.GetUniqueLoadID() ?? string.Empty)))
            {
                return false;
            }

            int now = Find.TickManager?.TicksGame ?? 0;
            diplomacyEvents.Add(new PrisonerDiplomacyEventRecord
            {
                EventId = "PD-WEVT-" + nextStrategicEventSequence++.ToString("D6"),
                DefinitionId = definitionId,
                ExtensionId = extensionId,
                Kind = kind,
                State = PrisonerDiplomacyEventState.Scheduled,
                Faction = faction,
                Map = map,
                Prisoner = prisoner,
                PrisonerLoadId = prisoner?.GetUniqueLoadID(),
                PrisonerLabel = prisoner?.LabelShortCap,
                SourceDealId = sourceDealId,
                CreatedTick = now,
                TriggerTick = Math.Max(now, triggerTick)
            });
            return true;
        }

        internal bool TrySetExtensionEventState(
            string eventId,
            PrisonerDiplomacyEventState state,
            bool playerAccepted = false)
        {
            PrisonerDiplomacyEventRecord eventRecord = diplomacyEvents.FirstOrDefault(item =>
                item?.EventId == eventId);
            if (eventRecord == null || !eventRecord.IsActive && state == PrisonerDiplomacyEventState.Active)
            {
                return false;
            }

            eventRecord.State = state;
            eventRecord.PlayerAccepted |= playerAccepted;
            eventRecord.OutcomeApplied |= state == PrisonerDiplomacyEventState.Completed;
            return true;
        }

        private void ScanAndUpdate()
        {
            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
            int now = Find.TickManager.TicksGame;
            foreach (Map map in Find.Maps.ToList())
            {
                if (map?.mapPawns == null)
                {
                    CompatibilityDiagnostics.LogIssueOnce("invalid-map", "Skipped a map without MapPawns.");
                    continue;
                }

                List<Pawn> mapPrisoners;
                try
                {
                    mapPrisoners = map.mapPawns.PrisonersOfColonySpawned.ToList();
                }
                catch (Exception exception)
                {
                    CompatibilityDiagnostics.LogErrorOnce(
                        "map-scan:" + map.GetUniqueLoadID(),
                        "Could not enumerate prisoners on map=" + map.GetUniqueLoadID() + ".",
                        exception);
                    continue;
                }

                foreach (Pawn pawn in mapPrisoners)
                {
                    try
                    {
                        PrisonerRecord record = RegisterPawn(pawn);
                        UpdatePrisonerTreatment(record, now);
                        if (RimChatIntegration.AllowsNewPrisonerDiplomacyDeals
                            && PrisonerDiplomacyTuning.EnemyInitiatedRansomsEnabled
                            && record != null && GetActiveDeal(pawn) == null && ShouldCreateOffer(record, now))
                        {
                            CreateOffer(record, false);
                        }
                    }
                    catch (Exception exception)
                    {
                        CompatibilityDiagnostics.LogErrorOnce(
                            "pawn-scan:" + CompatibilityDiagnostics.SafePawnId(pawn),
                            "Skipped a prisoner after a compatibility exception.",
                            exception);
                    }
                }
            }

            foreach (PrisonerDeal deal in deals.ToList())
            {
                try
                {
                    UpdateDeal(deal, now);
                }
                catch (Exception exception)
                {
                    CompatibilityDiagnostics.LogErrorOnce(
                        "deal-update:" + (deal?.DealId ?? "missing"),
                        "Skipped deal update after a compatibility exception.",
                        exception);
                }
            }

            foreach (FactionNegotiationMemory memory in factionNegotiationMemories.ToList())
            {
                try
                {
                    UpdateFactionMemory(memory, now);
                }
                catch (Exception exception)
                {
                    CompatibilityDiagnostics.LogErrorOnce(
                        "memory-update:" + (memory?.Faction?.GetUniqueLoadID() ?? "missing"),
                        "Skipped faction memory update after a compatibility exception.",
                        exception);
                }
            }

            try
            {
                UpdateStrategicConsequences(now);
                UpdateExtensionEvents(now);
            }
            catch (Exception exception)
            {
                CompatibilityDiagnostics.LogErrorOnce(
                    "strategic-update",
                    "Skipped strategic consequence update after a compatibility exception.",
                    exception);
            }

            deals.RemoveAll(deal => !deal.IsActive && deal.CompletedTick >= 0 && now - deal.CompletedTick > CompletedRetentionTicks);
            stopwatch.Stop();
            CompatibilityDiagnostics.RecordScanDuration(stopwatch.ElapsedMilliseconds, Find.Maps.Count, records.Count, deals.Count);
        }

        private void UpdatePrisonerTreatment(PrisonerRecord record, int now)
        {
            Pawn pawn = record?.Pawn;
            if (pawn == null || record.TerminalOutcomeRecorded || !pawn.IsPrisonerOfColony)
            {
                return;
            }

            int elapsed = record.LastTreatmentCheckTick < 0
                ? 0
                : Math.Max(0, now - record.LastTreatmentCheckTick);
            record.LastTreatmentCheckTick = now;

            bool lifeThreatening = PrisonerTreatmentUtility.IsLifeThreatening(pawn);
            if (record.WasLifeThreatening && !lifeThreatening && !record.CriticalRecoveryRecorded
                && (pawn.health?.summaryHealth?.SummaryHealthPercent ?? 0f) >= 0.65f)
            {
                record.CriticalRecoveryRecorded = true;
                ApplyMemoryChange(record.OriginalFaction, 0f, 10f, -2f,
                    "PD_MemoryEventCriticalRecovery", pawn.LabelShortCap, true);
            }
            record.WasLifeThreatening = lifeThreatening;

            bool starving = pawn.needs?.food?.Starving == true;
            record.StarvationTicks = starving
                ? Math.Min(MalnutritionThresholdTicks, record.StarvationTicks + elapsed)
                : 0;
            if (record.StarvationTicks >= MalnutritionThresholdTicks && !record.MalnutritionRecorded)
            {
                record.MalnutritionRecorded = true;
                ApplyMemoryChange(record.OriginalFaction, 0f, -14f, 8f,
                    "PD_MemoryEventMalnutrition", pawn.LabelShortCap, true);
            }

            record.LastMissingPartCount = PrisonerTreatmentUtility.CountMissingParts(pawn);
            record.LastPermanentInjuryCount = PrisonerTreatmentUtility.CountPermanentInjuries(pawn);
        }

        private void RecordUnconditionalRelease(Pawn pawn)
        {
            PrisonerRecord record = GetRecord(pawn);
            if (record == null || record.TerminalOutcomeRecorded)
            {
                return;
            }

            CancelPendingRescueFollowups(record);
            record.TerminalOutcomeRecorded = true;
            bool recovered = record.CriticalRecoveryRecorded
                || record.CapturedHealthPercent <= 0.45f
                    && (pawn.health?.summaryHealth?.SummaryHealthPercent ?? 0f) >= 0.65f;
            float treatment = recovered ? 18f : 10f;
            float resentment = record.Importance >= PrisonerImportance.Core ? -14f : -7f;
            ApplyMemoryChange(record.OriginalFaction, 4f, treatment, resentment,
                recovered ? "PD_MemoryEventReleasedAfterTreatment" : "PD_MemoryEventUnconditionalRelease",
                pawn.LabelShortCap, true);
            if (recovered)
            {
                SchedulePositiveReturn(null, record);
            }
        }

        private void RecordTerminalOutcome(
            PrisonerRecord record,
            DealState state,
            bool agreementBroken,
            bool playerResponsible = false)
        {
            if (record == null || record.TerminalOutcomeRecorded)
            {
                return;
            }

            // Any terminal outcome makes a pending important-prisoner rescue
            // obsolete. This is especially important for the debug death
            // simulation, which intentionally leaves the Pawn alive.
            CancelPendingRescueFollowups(record);

            float importanceMultiplier = record.Importance == PrisonerImportance.Leader
                ? 2f
                : record.Importance == PrisonerImportance.Core
                    ? 1.6f
                    : record.Importance == PrisonerImportance.Notable
                        ? 1.25f
                        : 1f;

            float reliability = agreementBroken ? -24f : 0f;
            float treatment = 0f;
            float resentment = agreementBroken ? 22f : 0f;
            string reasonKey;
            switch (state)
            {
                case DealState.FailedPrisonerDead:
                    if (agreementBroken || playerResponsible)
                    {
                        treatment = -18f * importanceMultiplier;
                        resentment += 18f * importanceMultiplier;
                        reasonKey = agreementBroken ? "PD_MemoryEventDeathBreach" : "PD_MemoryEventExecution";
                    }
                    else
                    {
                        treatment = -6f * importanceMultiplier;
                        resentment += 3f * importanceMultiplier;
                        reasonKey = "PD_MemoryEventPrisonerDeath";
                    }
                    break;
                case DealState.FailedEnslaved:
                    treatment = -16f;
                    resentment += 14f;
                    reasonKey = agreementBroken ? "PD_MemoryEventEnslavedBreach" : "PD_MemoryEventEnslaved";
                    break;
                case DealState.FailedRecruited:
                    treatment = -5f;
                    resentment += 8f;
                    reasonKey = agreementBroken ? "PD_MemoryEventRecruitedBreach" : "PD_MemoryEventRecruited";
                    break;
                case DealState.FailedSoldOrTransferred:
                    treatment = -10f;
                    resentment += 10f;
                    reasonKey = agreementBroken ? "PD_MemoryEventSoldBreach" : "PD_MemoryEventSold";
                    break;
                case DealState.Expired:
                    if (!agreementBroken)
                    {
                        return;
                    }
                    reliability = agreementBroken ? -20f : 0f;
                    resentment = agreementBroken ? 16f : 0f;
                    reasonKey = "PD_MemoryEventAgreementExpired";
                    break;
                case DealState.FailedEscaped:
                    if (!agreementBroken)
                    {
                        return;
                    }
                    reliability = -18f;
                    resentment = 14f;
                    reasonKey = "PD_MemoryEventImproperRelease";
                    break;
                default:
                    return;
            }

            if (state != DealState.Expired)
            {
                record.TerminalOutcomeRecorded = true;
            }

            if (record.Importance >= PrisonerImportance.Core
                && state == DealState.FailedPrisonerDead
                && (agreementBroken || playerResponsible))
            {
                if (PrisonerDiplomacyTuning.FactionMemoryEnabled)
                {
                    FactionNegotiationMemory memory = GetFactionMemory(record.OriginalFaction, true);
                    memory.ResentmentFloor = Math.Max(memory.ResentmentFloor, record.Importance == PrisonerImportance.Leader ? 35f : 20f);
                }
                ScheduleImportantDeathRetaliation(record, agreementBroken);
            }

            ApplyMemoryChange(record.OriginalFaction, reliability, treatment, resentment,
                reasonKey, record.Pawn?.LabelShortCap ?? record.PawnLoadId, true);
        }

        private PrisonerRecord RegisterPawn(Pawn pawn)
        {
            Faction originalFaction;
            if (!PrisonerEligibilityUtility.IsEligible(pawn, out originalFaction, out string eligibilityIssue))
            {
                if (pawn?.IsPrisonerOfColony == true
                    && eligibilityIssue != "faction_non_negotiating"
                    && eligibilityIssue != "non_humanlike")
                {
                    CompatibilityDiagnostics.LogPawnExcluded(pawn, eligibilityIssue);
                }
                return null;
            }

            PrisonerRecord existing = GetRecord(pawn);
            if (existing != null)
            {
                existing.Pawn = pawn;
                if (existing.OriginalFaction == null)
                {
                    existing.OriginalFaction = originalFaction;
                }
                EnsureFactionOfferSchedule(existing);
                TryShowPrisonerDiplomacyIntro(existing);
                return existing;
            }

            PrisonerRecord record = new PrisonerRecord
            {
                Pawn = pawn,
                PawnLoadId = pawn.GetUniqueLoadID(),
                OriginalFaction = originalFaction,
                CapturedTick = Find.TickManager.TicksGame,
                CapturedMarketValue = pawn.MarketValue,
                CapturedHealthPercent = pawn.health?.summaryHealth?.SummaryHealthPercent ?? 1f,
                WasLifeThreatening = PrisonerTreatmentUtility.IsLifeThreatening(pawn),
                LastMissingPartCount = PrisonerTreatmentUtility.CountMissingParts(pawn),
                LastPermanentInjuryCount = PrisonerTreatmentUtility.CountPermanentInjuries(pawn),
                LastTreatmentCheckTick = Find.TickManager.TicksGame
            };
            record.Importance = PrisonerValueCalculator.Classify(pawn, originalFaction);
            record.DiplomaticValue = Math.Max(0,
                PrisonerValueCalculator.Calculate(pawn, record.CapturedMarketValue, record.Importance)
                + PrisonerDiplomacyExtensionRegistry.GetDiplomaticValueAdjustment(pawn, originalFaction));
            ScheduleNextFactionOffer(record, record.CapturedTick);
            records.Add(record);
            ScheduleImportantPrisonerRescue(record);
            ApplyMemoryChange(originalFaction, 0f, 0f, 0f,
                "PD_MemoryEventCaptured", pawn.LabelShortCap, false);
            TryShowPrisonerDiplomacyIntro(record);
            return record;
        }

        private bool ShouldCreateOffer(PrisonerRecord record, int now)
        {
            if (!PrisonerDiplomacyTuning.EnemyInitiatedRansomsEnabled
                || !RimChatIntegration.AllowsNewPrisonerDiplomacyDeals
                || record == null
                || !string.IsNullOrEmpty(record.ActiveDealId)
                || record.OriginalFaction == null
                || !PrisonerEligibilityUtility.IsNegotiatingFaction(record.OriginalFaction))
            {
                return false;
            }

            EnsureFactionOfferSchedule(record);
            if (now < record.ScheduledFactionOfferTick)
            {
                return false;
            }

            FactionNegotiationMemory memory = GetFactionMemory(record.OriginalFaction, false);
            if (now < record.UnsolicitedOfferSuppressedUntilTick
                || now < (memory?.UnsolicitedOffersSuppressedUntilTick ?? -1))
            {
                return false;
            }

            if (record.LastProposalTick >= 0 && now - record.LastProposalTick < ProposalCooldownTicks)
            {
                return false;
            }

            return true;
        }

        private void EnsureFactionOfferSchedule(PrisonerRecord record)
        {
            if (record != null && record.ScheduledFactionOfferTick < 0)
            {
                ScheduleNextFactionOffer(record, record.CapturedTick);
            }
        }

        private static void ScheduleNextFactionOffer(PrisonerRecord record, int anchorTick)
        {
            if (record == null)
            {
                return;
            }

            record.ScheduledFactionOfferTick = anchorTick + CalculateFactionContactDelayTicks(
                record.PawnLoadId,
                anchorTick,
                record.Importance);
        }

        private static int CalculateFactionContactDelayTicks(string pawnLoadId, int anchorTick, PrisonerImportance importance)
        {
            int seed = Gen.HashCombineInt(GenText.StableStringHash(pawnLoadId ?? string.Empty), anchorTick);
            Rand.PushState(seed);
            int baseDelay = Rand.RangeInclusive(5 * TicksPerDay, MaximumFactionContactDelayTicks);
            Rand.PopState();

            int importanceReduction;
            switch (importance)
            {
                case PrisonerImportance.Specialist:
                    importanceReduction = TicksPerDay / 2;
                    break;
                case PrisonerImportance.Notable:
                    importanceReduction = TicksPerDay;
                    break;
                case PrisonerImportance.Core:
                    importanceReduction = TicksPerDay + TicksPerDay / 2;
                    break;
                case PrisonerImportance.Leader:
                    importanceReduction = 2 * TicksPerDay;
                    break;
                default:
                    importanceReduction = 0;
                    break;
            }

            int delay = baseDelay - importanceReduction;
            delay = (int)Math.Round(delay / PrisonerDiplomacyTuning.OfferFrequencyMultiplier);
            return Math.Max(MinimumFactionContactDelayTicks, Math.Min(28 * TicksPerDay, delay));
        }

        private void TryShowPrisonerDiplomacyIntro(PrisonerRecord record)
        {
            if (prisonerDiplomacyIntroShown
                || GenCommandLine.CommandLineArgPassed("pdsmoketest")
                || Find.LetterStack == null)
            {
                return;
            }

            prisonerDiplomacyIntroShown = true;
            Find.LetterStack.ReceiveLetter(
                "PD_IntroLabel".Translate(),
                "PD_IntroText".Translate(),
                LetterDefOf.NeutralEvent,
                new LookTargets(record.Pawn),
                record.OriginalFaction);
        }

        private bool CanCreateNewPrisonerDiplomacyDeal(out TaggedString reason)
        {
            reason = TaggedString.Empty;
            if (RimChatIntegration.AllowsNewPrisonerDiplomacyDeals)
            {
                return true;
            }

            reason = "PD_NegotiationOwnedByRimChat".Translate();
            return false;
        }

        private void TryShowRimChatCompatibilityWarning()
        {
            if (rimChatCompatibilityWarningShown
                || !RimChatIntegration.RequiresCompatibilityWarning
                || GenCommandLine.CommandLineArgPassed("pdsmoketest")
                || Find.LetterStack == null)
            {
                return;
            }

            rimChatCompatibilityWarningShown = true;
            Find.LetterStack.ReceiveLetter(
                "PD_RimChatWarningLabel".Translate(),
                "PD_RimChatWarningText".Translate(
                    string.IsNullOrEmpty(RimChatIntegration.Version) ? "?" : RimChatIntegration.Version),
                LetterDefOf.NeutralEvent);
        }

        private PrisonerDeal CreateOffer(PrisonerRecord record, bool debugForced)
        {
            if (!RimChatIntegration.AllowsNewPrisonerDiplomacyDeals
                || record == null || record.Pawn == null || GetActiveDeal(record.Pawn) != null)
            {
                return null;
            }

            int now = Find.TickManager.TicksGame;
            FactionNegotiationMemory memory = GetFactionMemory(record.OriginalFaction, true);
            int reserve = GetSpendableReserve(record.OriginalFaction, now);
            int materialCap = NegotiationEconomyUtility.CalculateMaterialRewardCap(record.Pawn.MapHeld);
            if (!debugForced && reserve < PrisonerNegotiationUtility.MinimumDemand)
            {
                return null;
            }
            float memoryMultiplier = GetFactionMemoryMultiplier(record.OriginalFaction, now);
            int offeredValue = Math.Min(PrisonerValueCalculator.CalculateOffer(record, memoryMultiplier), Math.Min(reserve, materialCap));
            offeredValue = Math.Max(PrisonerNegotiationUtility.MinimumDemand, offeredValue / 50 * 50);
            string dealId = "PD-" + nextDealSequence++.ToString("D6");
            RewardDemand offeredRewards = CreateFactionOfferRewards(record, offeredValue, dealId);
            PrisonerDeal deal = new PrisonerDeal
            {
                DealId = dealId,
                Prisoner = record.Pawn,
                PrisonerLoadId = record.PawnLoadId,
                Faction = record.OriginalFaction,
                Map = record.Pawn.MapHeld,
                MapLoadId = record.Pawn.MapHeld?.GetUniqueLoadID(),
                SilverAmount = offeredRewards.Silver,
                Rewards = offeredRewards,
                NegotiationBudget = reserve,
                NegotiationDemandCost = NegotiationEconomyUtility.CalculateDemandCost(record.OriginalFaction, offeredRewards),
                CreatedTick = now,
                OfferExpiresTick = now + ProposalDurationTicks,
                State = DealState.Offered,
                Origin = DealOrigin.FactionOffer,
                NegotiationType = FactionNegotiationUtility.GetType(record.OriginalFaction)
            };
            AssignPirateRisk(deal, PrisonerDiplomacyTuning.EffectiveReliability(memory));
            deals.Add(deal);
            record.ActiveDealId = deal.DealId;
            record.LastProposalTick = now;
            ScheduleNextFactionOffer(record, now);
            string offerText = SendOfferLetter(deal, record);
            QueueAiNarrative(
                AiNarrativeEventKind.FactionOffer,
                deal.Prisoner,
                deal.Faction,
                deal,
                "offered",
                deal.Rewards.Description(),
                offerText);

            if (debugForced)
            {
                Messages.Message("PD_DebugOfferCreated".Translate(record.Pawn.LabelShortCap), record.Pawn, MessageTypeDefOf.PositiveEvent, false);
            }

            return deal;
        }

        private static RewardDemand CreateFactionOfferRewards(PrisonerRecord record, int offeredValue, string dealId)
        {
            if (PrisonerDiplomacyMod.Settings?.EnableStrategicConsequences != false
                && !GenCommandLine.CommandLineArgPassed("pdsmoketest"))
            {
                int strategicRoll = (Gen.HashCombineInt(GenText.StableStringHash(dealId), 6037) & int.MaxValue) % 100;
                if (strategicRoll < 18 && offeredValue >= NegotiationEconomyUtility.EarlyWarningIntelCost)
                {
                    RewardDemand intel = new RewardDemand { EarlyWarningIntel = true };
                    int remainder = offeredValue - NegotiationEconomyUtility.EarlyWarningIntelCost;
                    if (remainder >= PrisonerNegotiationUtility.MinimumDemand)
                    {
                        intel.Silver = remainder / 50 * 50;
                    }
                    return intel;
                }

                if (strategicRoll < 38
                    && offeredValue >= NegotiationEconomyUtility.CalculateCeasefireCost(
                        NegotiationEconomyUtility.MinimumCeasefireDays))
                {
                    int days = NegotiationEconomyUtility.MaximumCeasefireDays;
                    while (days > NegotiationEconomyUtility.MinimumCeasefireDays
                        && NegotiationEconomyUtility.CalculateCeasefireCost(days) > offeredValue)
                    {
                        days--;
                    }

                    RewardDemand ceasefire = new RewardDemand { CeasefireDays = days };
                    int remainder = offeredValue - NegotiationEconomyUtility.CalculateCeasefireCost(days);
                    if (remainder >= PrisonerNegotiationUtility.MinimumDemand)
                    {
                        ceasefire.Silver = remainder / 50 * 50;
                    }
                    return ceasefire;
                }
            }

            if (!FactionNegotiationUtility.IsTransactional(record.OriginalFaction)
                || GenCommandLine.CommandLineArgPassed("pdsmoketest")
                || !Rand.ChanceSeeded(0.38f, Gen.HashCombineInt(GenText.StableStringHash(dealId), 6043)))
            {
                return new RewardDemand { Silver = offeredValue };
            }

            List<ThingDef> supplies = SupplyRewardUtility.AvailableSupplies(record.OriginalFaction).ToList();
            if (supplies.Count == 0)
            {
                return new RewardDemand { Silver = offeredValue };
            }

            int selectedIndex = (Gen.HashCombineInt(GenText.StableStringHash(dealId), 6053) & int.MaxValue) % supplies.Count;
            ThingDef selected = supplies[selectedIndex];
            int count = Math.Max(1, (int)Math.Floor(offeredValue / Math.Max(1f, selected.BaseMarketValue * 1.15f)));
            count = Math.Min(SupplyRewardUtility.MaximumSupplyCount, count);
            RewardDemand reward = new RewardDemand { SupplyDef = selected, SupplyCount = count };
            while (reward.SupplyCount > 0
                && NegotiationEconomyUtility.CalculateDemandCost(record.OriginalFaction, reward) > offeredValue)
            {
                reward.SupplyCount--;
            }

            return reward.SupplyCount > 0
                ? reward
                : new RewardDemand { Silver = offeredValue };
        }

        private string SendOfferLetter(PrisonerDeal deal, PrisonerRecord record)
        {
            TaggedString label = "PD_RansomOfferLabel".Translate(deal.Prisoner.LabelShortCap);
            TaggedString text = "PD_RansomOfferText".Translate(
                deal.Faction.NameColored,
                deal.Rewards.Description(),
                deal.Prisoner.LabelShortCap,
                PrisonerValueCalculator.ImportanceLabel(record.Importance),
                record.DiplomaticValue,
                (deal.OfferExpiresTick - Find.TickManager.TicksGame).ToStringTicksToPeriod());
            ChoiceLetter_PrisonerRansomOffer letter = (ChoiceLetter_PrisonerRansomOffer)LetterMaker.MakeLetter(
                label,
                text,
                PrisonerDiplomacyDefOf.PD_PrisonerRansomOffer,
                new LookTargets(deal.Prisoner),
                deal.Faction);
            letter.DealId = deal.DealId;
            Find.LetterStack.ReceiveLetter(letter);
            return text.ToString();
        }

        private void UpdateDeal(PrisonerDeal deal, int now)
        {
            if (!deal.IsActive)
            {
                return;
            }

            if (!PrisonerEligibilityUtility.IsNegotiatingFaction(deal.Faction))
            {
                FailDeal(deal, DealState.FailedFactionInvalid);
                return;
            }

            if ((deal.ReturnedHostage != null || !string.IsNullOrEmpty(deal.ReturnedHostageLoadId))
                && !PrisonerExchangeUtility.IsHeldByFaction(deal.Faction, deal.ReturnedHostage))
            {
                FailDeal(deal, DealState.FailedHostageInvalid);
                return;
            }

            if (deal.State == DealState.FulfillmentPending)
            {
                FulfillDeal(deal);
                return;
            }

            UpdatePirateRiskEvent(deal, now);
            if (!deal.IsActive)
            {
                return;
            }

            if (deal.Prisoner != null && deal.Prisoner.Dead)
            {
                FailDeal(deal, DealState.FailedPrisonerDead);
                return;
            }

            if (deal.Prisoner == null || deal.Prisoner.Destroyed)
            {
                FailDeal(deal, DealState.Cancelled);
                return;
            }

                if (deal.Prisoner.Faction == Faction.OfPlayer)
                {
                    NotifyPawnJoinedPlayer(deal.Prisoner, deal.Prisoner.IsSlaveOfColony);
                    return;
                }

            if ((deal.State == DealState.Offered || deal.State == DealState.Negotiating)
                && now >= deal.OfferExpiresTick)
            {
                FailDeal(deal, DealState.Expired);
                return;
            }

            if (deal.State != DealState.Offered
                && deal.State != DealState.Negotiating
                && now >= deal.FulfillmentExpiresTick)
            {
                if (!TryExtendFulfillment(deal, now))
                {
                    FailDeal(deal, DealState.Expired);
                    return;
                }
            }

            if ((deal.State == DealState.AcceptedAwaitingRelease || deal.State == DealState.ReleaseOrdered)
                && deal.Prisoner.IsPrisonerOfColony
                && deal.Prisoner.MapHeld != null
                && deal.Map != deal.Prisoner.MapHeld)
            {
                SetDealMap(deal, deal.Prisoner.MapHeld);
            }

            if (!deal.Prisoner.IsPrisonerOfColony && deal.State != DealState.ReleaseOrdered)
            {
                FailDeal(deal, DealState.FailedEscaped);
            }
        }

        private bool ValidatePrisonerStillHeld(PrisonerDeal deal)
        {
            return deal.Prisoner != null
                && !deal.Prisoner.Dead
                && !deal.Prisoner.Destroyed
                && deal.Prisoner.IsPrisonerOfColony
                && deal.Prisoner.MapHeld != null;
        }

        private bool TryExtendFulfillment(PrisonerDeal deal, int now)
        {
            if (deal == null
                || (deal.State != DealState.AcceptedAwaitingRelease
                    && deal.State != DealState.ReleaseOrdered)
                || now < deal.FulfillmentExpiresTick)
            {
                return false;
            }

            PrisonerRecord record = GetRecord(deal.Prisoner);
            Pawn pawn = deal.Prisoner;
            if (record == null
                || pawn == null
                || !pawn.IsPrisonerOfColony
                || pawn.MapHeld == null
                || !IsMedicalExtensionCondition(pawn)
                || record.LastPlayerTreatmentTick < deal.AcceptedTick
                || record.LastPlayerTreatmentTick <= deal.LastTreatmentTickAtExtension)
            {
                return false;
            }

            deal.FulfillmentExpiresTick += FulfillmentExtensionTicks;
            deal.DeadlineExtensionCount++;
            deal.LastTreatmentTickAtExtension = record.LastPlayerTreatmentTick;
            Messages.Message("PD_DealDeadlineExtended".Translate(
                pawn.LabelShortCap,
                (FulfillmentExtensionTicks / TicksPerDay).ToString()),
                pawn,
                MessageTypeDefOf.NeutralEvent,
                false);
            return true;
        }

        private static bool IsMedicalExtensionCondition(Pawn pawn)
        {
            return pawn.Downed || pawn.health?.HasHediffsNeedingTendByPlayer(true) == true;
        }

        private void FulfillDeal(PrisonerDeal deal)
        {
            if (!deal.PrisonerDelivered || deal.RewardIssued)
            {
                return;
            }

            if (deal.PirateRisk == PirateDealRisk.DelayedPayment)
            {
                int now = Find.TickManager.TicksGame;
                if (deal.PaymentDueTick < 0)
                {
                    deal.PaymentDueTick = now + FactionNegotiationUtility.CalculatePaymentDelayTicks(deal.DealId);
                    TaggedString response = "PD_PiratePaymentDelayed".Translate(
                        deal.Faction?.NameColored ?? "?",
                        (deal.PaymentDueTick - now).ToStringTicksToPeriod());
                    Messages.Message(response,
                        MessageTypeDefOf.ThreatSmall,
                        false);
                    QueueAiNarrative(
                        AiNarrativeEventKind.PiratePaymentDelayed,
                        deal.Prisoner,
                        deal.Faction,
                        deal,
                        "payment_delayed",
                        deal.Rewards?.Description() ?? deal.SilverAmount.ToString(),
                        response.ToString());
                    return;
                }

                if (now < deal.PaymentDueTick)
                {
                    return;
                }
            }

            Map map = ResolveDealMap(deal, true);
            if (map == null)
            {
                return;
            }
            SetDealMap(deal, map);

            if (deal.ReturnedHostage != null || !string.IsNullOrEmpty(deal.ReturnedHostageLoadId))
            {
                FulfillExchangeDeal(deal, map);
                return;
            }

            RewardDemand rewards = deal.Rewards ?? new RewardDemand { Silver = deal.SilverAmount };
            IntVec3 rewardCell = DropCellFinder.TradeDropSpot(map);
            try
            {
                if (rewards.Silver > 0 && !deal.SilverRewardIssued)
                {
                    rewardCell = DeliverThings(map, ThingDefOf.Silver, rewards.Silver);
                    deal.SilverRewardIssued = true;
                }

                if (rewards.SupplyDef != null && rewards.SupplyCount > 0 && !deal.SupplyRewardIssued)
                {
                    rewardCell = DeliverThings(map, rewards.SupplyDef, rewards.SupplyCount);
                    deal.SupplyRewardIssued = true;
                }

                if (!string.IsNullOrWhiteSpace(rewards.SpecialRewardId)
                    && rewards.SpecialRewardThingDef != null
                    && rewards.SpecialRewardCount > 0
                    && !deal.SpecialRewardIssued)
                {
                    rewardCell = DeliverThings(
                        map,
                        rewards.SpecialRewardThingDef,
                        rewards.SpecialRewardCount);
                    deal.SpecialRewardIssued = true;
                }

                if (rewards.Goodwill > 0 && !deal.GoodwillRewardIssued)
                {
                    int goodwillToGrant = Math.Min(rewards.Goodwill, Math.Max(0, 100 - deal.Faction.PlayerGoodwill));
                    if (goodwillToGrant > 0
                        && !deal.Faction.TryAffectGoodwillWith(Faction.OfPlayer, goodwillToGrant, false, false, null))
                    {
                        throw new InvalidOperationException("Faction refused the agreed goodwill reward.");
                    }

                    deal.GoodwillRewardIssued = true;
                }

                if (rewards.CeasefireDays > 0 && !deal.CeasefireRewardIssued)
                {
                    ActivateCeasefire(deal.Faction, rewards.CeasefireDays, deal.DealId,
                        deal.Prisoner?.LabelShortCap ?? deal.PrisonerLoadId);
                    deal.CeasefireRewardIssued = true;
                }

                if (rewards.EarlyWarningIntel && !deal.IntelRewardIssued)
                {
                    ActivateEarlyWarningIntel(deal.Faction, deal.DealId,
                        deal.Prisoner?.LabelShortCap ?? deal.PrisonerLoadId);
                    deal.IntelRewardIssued = true;
                }

                if (!deal.ReserveCharged)
                {
                    FactionNegotiationMemory memory = GetFactionMemory(deal.Faction, true);
                    if (PrisonerDiplomacyTuning.FactionReservesEnabled)
                    {
                        GetAvailableReserve(memory, Find.TickManager.TicksGame);
                        memory.DiplomaticReserve = Math.Max(0f,
                            memory.DiplomaticReserve - NegotiationEconomyUtility.CalculateDemandCost(deal.Faction, rewards));
                    }
                    deal.ReserveCharged = true;
                }
            }
            catch (Exception exception)
            {
                ErrorTelemetryService.CaptureException(
                    exception,
                    "PrisonerDiplomacyGameComponent.FulfillDeal.rewards",
                    deal,
                    deal?.Prisoner,
                    "transaction_sentinel");
                Log.Error("[Prisoner Diplomacy] Failed to deliver rewards for " + deal.DealId + ": " + exception);
                return;
            }

            deal.RewardIssued = (rewards.Silver <= 0 || deal.SilverRewardIssued)
                && (rewards.SupplyDef == null || rewards.SupplyCount <= 0 || deal.SupplyRewardIssued)
                && (string.IsNullOrWhiteSpace(rewards.SpecialRewardId)
                    || rewards.SpecialRewardThingDef == null
                    || rewards.SpecialRewardCount <= 0
                    || deal.SpecialRewardIssued)
                && (rewards.Goodwill <= 0 || deal.GoodwillRewardIssued)
                && (rewards.CeasefireDays <= 0 || deal.CeasefireRewardIssued)
                && (!rewards.EarlyWarningIntel || deal.IntelRewardIssued)
                && deal.ReserveCharged;
            if (!deal.RewardIssued)
            {
                return;
            }

            deal.State = DealState.Completed;
            deal.CompletedTick = Find.TickManager.TicksGame;
            ClearRecordDeal(deal);

            FactionNegotiationMemory completedMemory = GetFactionMemory(deal.Faction, true);
            completedMemory.SuccessfulDeals++;
            completedMemory.Impatience = Math.Max(0, completedMemory.Impatience - 1);
            completedMemory.LastDealSummary = "PD_HistoryCompleted".Translate(
                deal.Prisoner?.LabelShortCap ?? deal.PrisonerLoadId,
                rewards.Description());
            ApplyMemoryChange(deal.Faction, 10f, 3f, -3f,
                "PD_MemoryEventAgreementCompleted",
                deal.Prisoner?.LabelShortCap ?? deal.PrisonerLoadId,
                true);
            PrisonerRecord completedRecord = GetRecord(deal.Prisoner);
            if (completedRecord != null)
            {
                completedRecord.TerminalOutcomeRecorded = true;
            }
            SchedulePositiveReturn(deal, completedRecord);
            SchedulePirateAmbush(deal);

            TaggedString completionText = "PD_DealCompletedRewardsText".Translate(
                deal.Prisoner?.LabelShortCap ?? deal.PrisonerLoadId,
                deal.Faction?.NameColored ?? "?",
                rewards.Description());
            Find.LetterStack.ReceiveLetter(
                "PD_DealCompletedLabel".Translate(deal.Prisoner?.LabelShortCap ?? deal.PrisonerLoadId),
                completionText,
                LetterDefOf.PositiveEvent,
                new LookTargets(new TargetInfo(rewardCell, map)),
                deal.Faction);
            QueueAiNarrative(
                AiNarrativeEventKind.DealCompleted,
                deal.Prisoner,
                deal.Faction,
                deal,
                "completed",
                rewards.Description(),
                completionText.ToString());
        }

        private void FulfillExchangeDeal(PrisonerDeal deal, Map map)
        {
            Pawn hostage = deal.ReturnedHostage;
            if (hostage == null || !PrisonerExchangeUtility.IsHeldByFaction(deal.Faction, hostage))
            {
                FailDeal(deal, DealState.FailedHostageInvalid);
                return;
            }

            if (!deal.HostageReturned)
            {
                if (!PrisonerExchangeUtility.TryReturnHostage(deal.Faction, hostage, map, out IntVec3 arrivalCell))
                {
                    return;
                }

                deal.HostageReturned = true;
                SetDealMap(deal, map);
                Find.LetterStack.ReceiveLetter(
                    "PD_ExchangeCompletedLabel".Translate(hostage.LabelShortCap),
                    "PD_ExchangeCompletedText".Translate(
                        deal.Prisoner?.LabelShortCap ?? deal.PrisonerLoadId,
                        hostage.LabelShortCap,
                        GetExchangeCompensationDescription(deal)),
                    LetterDefOf.PositiveEvent,
                    new LookTargets(new TargetInfo(arrivalCell, map)),
                    deal.Faction);
            }

            deal.RewardIssued = deal.HostageReturned;
            if (!deal.RewardIssued)
            {
                return;
            }

            deal.State = DealState.Completed;
            deal.CompletedTick = Find.TickManager.TicksGame;
            ClearRecordDeal(deal);
            FactionNegotiationMemory memory = GetFactionMemory(deal.Faction, true);
            memory.SuccessfulDeals++;
            memory.Impatience = Math.Max(0, memory.Impatience - 1);
            memory.LastDealSummary = "PD_HistoryExchangeCompleted".Translate(
                deal.Prisoner?.LabelShortCap ?? deal.PrisonerLoadId,
                hostage.LabelShortCap);
            ApplyMemoryChange(deal.Faction, 12f, 4f, -10f,
                "PD_MemoryEventExchangeCompleted",
                deal.Prisoner?.LabelShortCap ?? deal.PrisonerLoadId,
                true);
            PrisonerRecord completedRecord = GetRecord(deal.Prisoner);
            if (completedRecord != null)
            {
                completedRecord.TerminalOutcomeRecorded = true;
            }
            SchedulePositiveReturn(deal, completedRecord);
            SchedulePirateAmbush(deal);
            QueueAiNarrative(
                AiNarrativeEventKind.ExchangeCompleted,
                deal.Prisoner,
                deal.Faction,
                deal,
                "exchange_completed",
                "PD_ExchangeCompletedText".Translate(
                    deal.Prisoner?.LabelShortCap ?? deal.PrisonerLoadId,
                    hostage.LabelShortCap,
                    GetExchangeCompensationDescription(deal)).ToString(),
                "PD_ExchangeCompletedText".Translate(
                    deal.Prisoner?.LabelShortCap ?? deal.PrisonerLoadId,
                    hostage.LabelShortCap,
                    GetExchangeCompensationDescription(deal)).ToString());
        }

        private static IntVec3 DeliverThings(Map map, ThingDef thingDef, int amount)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            if (amount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            if (thingDef == null)
            {
                throw new ArgumentNullException(nameof(thingDef));
            }

            List<Thing> rewardStacks = new List<Thing>();
            int remaining = amount;
            int stackLimit = Math.Max(1, thingDef.stackLimit);
            while (remaining > 0)
            {
                Thing reward = ThingMaker.MakeThing(thingDef);
                reward.stackCount = Math.Min(remaining, stackLimit);
                rewardStacks.Add(reward);
                remaining -= reward.stackCount;
            }

            IntVec3 dropCell = DropCellFinder.TradeDropSpot(map);
            DropPodUtility.DropThingsNear(
                dropCell,
                map,
                rewardStacks,
                forbid: false,
                faction: null);

            int deliveredAmount = rewardStacks
                .Where(reward => !reward.Destroyed && (reward.Spawned || reward.ParentHolder != null))
                .Sum(reward => reward.stackCount);
            if (deliveredAmount != amount)
            {
                foreach (Thing reward in rewardStacks.Where(reward => !reward.Destroyed))
                {
                    reward.Destroy();
                }

                throw new InvalidOperationException(thingDef.defName + " drop-pod payload was incomplete; expected="
                    + amount + " actual=" + deliveredAmount + ".");
            }

            Log.Message("[Prisoner Diplomacy] Delivered " + amount + " " + thingDef.defName + " to " + map.GetUniqueLoadID() + ".");
            return dropCell;
        }

        private void FailDeal(
            PrisonerDeal deal,
            DealState failureState,
            bool playerResponsible = false,
            bool recordTerminalOutcome = true)
        {
            if (deal == null || !deal.IsActive)
            {
                return;
            }

            bool agreementBroken = deal.AcceptedTick >= 0
                || deal.State == DealState.AcceptedAwaitingRelease
                || deal.State == DealState.ReleaseOrdered
                || deal.State == DealState.FulfillmentPending;
            PrisonerRecord failedRecord = GetRecord(deal.Prisoner);
            deal.State = failureState;
            deal.CompletedTick = Find.TickManager.TicksGame;
            ClearRecordDeal(deal);
            RemoveOfferLetter(deal.DealId);
            if (deal.CompensationCharged && !deal.PrisonerDelivered && deal.PlayerCompensationSilver > 0)
            {
                Map refundMap = ResolveDealMap(deal, true);
                if (refundMap != null)
                {
                    if (PrisonerExchangeUtility.TryRefundSilver(refundMap, deal.PlayerCompensationSilver, out _))
                    {
                        deal.CompensationCharged = false;
                        Messages.Message("PD_ExchangeCompensationRefunded".Translate(deal.PlayerCompensationSilver),
                            MessageTypeDefOf.NeutralEvent, false);
                    }
                }
            }
            else if (deal.CompensationCharged
                && !deal.PrisonerDelivered
                && deal.PlayerCompensationThingDef != null
                && deal.PlayerCompensationThingCount > 0)
            {
                Map refundMap = ResolveDealMap(deal, true);
                if (PrisonerExchangeUtility.TryRefundThings(
                    refundMap,
                    deal.PlayerCompensationThingDef,
                    deal.PlayerCompensationThingCount,
                    out _))
                {
                    deal.CompensationCharged = false;
                    Messages.Message("PD_ExchangeCompensationRefundedSupplies".Translate(
                        deal.PlayerCompensationThingCount,
                        deal.PlayerCompensationThingDef.LabelCap), MessageTypeDefOf.NeutralEvent, false);
                }
            }

            if (!deal.FailureNotified && failureState != DealState.Rejected)
            {
                deal.FailureNotified = true;
                string pawnLabel = deal.Prisoner?.LabelShortCap ?? deal.PrisonerLoadId ?? "?";
                Find.LetterStack.ReceiveLetter(
                    "PD_DealFailedLabel".Translate(pawnLabel),
                    "PD_DealFailedText".Translate(pawnLabel, FailureReason(failureState)),
                    LetterDefOf.NegativeEvent,
                    deal.Map != null ? new LookTargets(deal.Map.Parent) : LookTargets.Invalid,
                    deal.Faction);
            }

            if (recordTerminalOutcome)
            {
                RecordTerminalOutcome(failedRecord, failureState, agreementBroken, playerResponsible);
            }

            if (failureState != DealState.Rejected)
            {
                string pawnLabel = deal.Prisoner?.LabelShortCap ?? deal.PrisonerLoadId ?? "?";
                TaggedString failureText = "PD_DealFailedText".Translate(pawnLabel, FailureReason(failureState));
                QueueAiNarrative(
                    AiNarrativeEventKind.DealFailed,
                    deal.Prisoner,
                    deal.Faction,
                    deal,
                    "failed",
                    FailureReason(failureState),
                    failureText.ToString());
            }
        }

        private void ClearRecordDeal(PrisonerDeal deal)
        {
            PrisonerRecord record = records.FirstOrDefault(item => item.ActiveDealId == deal.DealId);
            if (record != null)
            {
                record.ActiveDealId = null;
            }
        }

        private FactionNegotiationMemory GetFactionMemory(Faction faction, bool create)
        {
            if (faction == null)
            {
                return null;
            }

            FactionNegotiationMemory memory = factionNegotiationMemories.FirstOrDefault(item => item.Faction == faction);
            if (memory == null && create)
            {
                memory = new FactionNegotiationMemory { Faction = faction };
                factionNegotiationMemories.Add(memory);
            }

            return memory;
        }

        public string GetFactionMemoryDescription(Faction faction)
        {
            if (!PrisonerDiplomacyTuning.FactionMemoryEnabled)
            {
                return "PD_MemoryDisabled".Translate();
            }

            FactionNegotiationMemory memory = GetFactionMemory(faction, true);
            UpdateFactionMemory(memory, Find.TickManager.TicksGame);
            return FactionPrisonerMemoryUtility.Describe(memory);
        }

        public string GetFactionMemoryPageText(Faction faction)
        {
            if (faction == null || faction == Faction.OfPlayer)
            {
                return string.Empty;
            }

            if (!PrisonerDiplomacyTuning.FactionMemoryEnabled)
            {
                return "PD_MemoryDisabled".Translate();
            }

            FactionNegotiationMemory memory = GetFactionMemory(faction, true);
            UpdateFactionMemory(memory, Find.TickManager.TicksGame);
            string latestEvent = memory.RecentEvents
                .OrderByDescending(item => item.Tick)
                .Select(item => DescribeMemoryEvent(item))
                .FirstOrDefault(item => !string.IsNullOrEmpty(item));
            string memoryText = string.IsNullOrEmpty(latestEvent)
                ? "PD_FactionMemorySection".Translate(FactionPrisonerMemoryUtility.Describe(memory))
                : "PD_FactionMemorySectionWithEvent".Translate(
                    FactionPrisonerMemoryUtility.Describe(memory),
                    latestEvent);
            string strategicText = GetFactionStrategicStatus(faction);
            return string.IsNullOrEmpty(strategicText) ? memoryText : memoryText + "\n" + strategicText;
        }

        public float GetFactionMemoryMultiplier(Faction faction, int now)
        {
            if (!PrisonerDiplomacyTuning.FactionMemoryEnabled)
            {
                return 1f;
            }

            FactionNegotiationMemory memory = GetFactionMemory(faction, true);
            UpdateFactionMemory(memory, now);
            return FactionPrisonerMemoryUtility.CalculateMultiplier(faction, memory);
        }

        private void UpdateFactionMemory(FactionNegotiationMemory memory, int now)
        {
            if (memory == null)
            {
                return;
            }

            FactionPrisonerMemoryUtility.ApplyDecay(memory, now);
            memory.RecentEvents = memory.RecentEvents ?? new List<PrisonerMemoryEvent>();
            memory.RecentEvents.RemoveAll(item => item == null
                || string.IsNullOrEmpty(item.ReasonKey)
                || now - item.Tick > RecentMemoryEventRetentionTicks);
            if (memory.RecentEvents.Count > 8)
            {
                memory.RecentEvents = memory.RecentEvents
                    .OrderByDescending(item => item.Tick)
                    .Take(8)
                    .ToList();
            }
        }

        private void ApplyMemoryChange(
            Faction faction,
            float reliability,
            float treatment,
            float resentment,
            string reasonKey,
            string pawnLabel,
            bool notifyMajor)
        {
            if (!PrisonerDiplomacyTuning.FactionMemoryEnabled
                || faction == null || faction == Faction.OfPlayer)
            {
                return;
            }

            FactionNegotiationMemory memory = GetFactionMemory(faction, true);
            int now = Find.TickManager.TicksGame;
            UpdateFactionMemory(memory, now);
            memory.Reliability += reliability;
            memory.Treatment += treatment;
            memory.Resentment += resentment;
            FactionPrisonerMemoryUtility.ClampMemory(memory);
            memory.RecentEvents.Add(new PrisonerMemoryEvent
            {
                Tick = now,
                ReasonKey = reasonKey,
                PawnLabel = pawnLabel
            });
            UpdateFactionMemory(memory, now);

            float magnitude = Math.Abs(reliability) + Math.Abs(treatment) + Math.Abs(resentment);
            if (notifyMajor
                && PrisonerDiplomacyTuning.ShouldNotifyMemory(magnitude)
                && !GenCommandLine.CommandLineArgPassed("pdsmoketest"))
            {
                Messages.Message("PD_MemoryChanged".Translate(
                    faction.NameColored,
                    DescribeMemoryEvent(memory.RecentEvents.Last())),
                    MessageTypeDefOf.NeutralEvent,
                    false);
            }
        }

        private static string DescribeMemoryEvent(PrisonerMemoryEvent memoryEvent)
        {
            return memoryEvent == null || string.IsNullOrEmpty(memoryEvent.ReasonKey)
                ? string.Empty
                : memoryEvent.ReasonKey.Translate(memoryEvent.PawnLabel ?? "?").ToString();
        }

        public int GetAvailableReserve(Faction faction)
        {
            return GetSpendableReserve(faction, Find.TickManager.TicksGame);
        }

        public string GetFactionFinancialStatus(Faction faction)
        {
            if (!PrisonerDiplomacyTuning.FactionReservesEnabled)
            {
                return "PD_FinancesDisabled".Translate();
            }

            FactionNegotiationMemory memory = GetFactionMemory(faction, true);
            float maximum = NegotiationEconomyUtility.CalculateMaximumReserve(faction);
            float ratio = maximum <= 0f ? 0f : GetAvailableReserve(memory, Find.TickManager.TicksGame) / maximum;
            if (ratio >= 0.75f)
            {
                return "PD_FinancesStrong".Translate();
            }

            if (ratio >= 0.40f)
            {
                return "PD_FinancesCapable".Translate();
            }

            if (ratio >= 0.15f)
            {
                return "PD_FinancesLimited".Translate();
            }

            return "PD_FinancesDepleted".Translate();
        }

        public string GetFactionHistorySummary(Faction faction)
        {
            FactionNegotiationMemory memory = GetFactionMemory(faction, true);
            UpdateFactionMemory(memory, Find.TickManager.TicksGame);
            string memoryDescription = FactionPrisonerMemoryUtility.Describe(memory);
            if (!string.IsNullOrEmpty(memory.LastDealSummary))
            {
                string history = "PD_HistorySummaryWithMemory".Translate(
                    memory.SuccessfulDeals,
                    memory.RejectedNegotiations,
                    memory.LastDealSummary,
                    memoryDescription);
                string strategic = GetFactionStrategicStatus(faction);
                return string.IsNullOrEmpty(strategic) ? history : history + "\n" + strategic;
            }

            string emptyHistory = "PD_HistoryNoneWithMemory".Translate(memoryDescription);
            string emptyStrategic = GetFactionStrategicStatus(faction);
            return string.IsNullOrEmpty(emptyStrategic) ? emptyHistory : emptyHistory + "\n" + emptyStrategic;
        }

        private static float GetAvailableReserve(FactionNegotiationMemory memory, int now)
        {
            if (memory == null || memory.Faction == null)
            {
                return 0f;
            }

            float maximum = NegotiationEconomyUtility.CalculateMaximumReserve(memory.Faction);
            if (!PrisonerDiplomacyTuning.FactionReservesEnabled)
            {
                return maximum;
            }
            if (memory.DiplomaticReserve < 0f)
            {
                memory.DiplomaticReserve = maximum;
                memory.ReserveUpdatedTick = now;
                return memory.DiplomaticReserve;
            }

            if (memory.ReserveUpdatedTick < 0)
            {
                memory.ReserveUpdatedTick = now;
            }

            int elapsed = Math.Max(0, now - memory.ReserveUpdatedTick);
            if (elapsed > 0 && memory.DiplomaticReserve < maximum)
            {
                float recoveryPerTick = maximum / (NegotiationEconomyUtility.ReserveRecoveryDays * (float)TicksPerDay);
                memory.DiplomaticReserve = Math.Min(maximum, memory.DiplomaticReserve + elapsed * recoveryPerTick);
            }

            memory.DiplomaticReserve = Math.Min(maximum, memory.DiplomaticReserve);
            memory.ReserveUpdatedTick = now;
            return memory.DiplomaticReserve;
        }

        private int GetSpendableReserve(Faction faction, int now, PrisonerDeal excludedDeal = null)
        {
            FactionNegotiationMemory memory = GetFactionMemory(faction, true);
            float reserve = GetAvailableReserve(memory, now);
            if (!PrisonerDiplomacyTuning.FactionReservesEnabled)
            {
                return Math.Max(0, (int)Math.Floor(reserve));
            }

            int committed = deals
                .Where(deal => deal != excludedDeal
                    && deal.Faction == faction
                    && !deal.ReserveCharged
                    && (deal.State == DealState.Offered
                        || deal.State == DealState.AcceptedAwaitingRelease
                        || deal.State == DealState.ReleaseOrdered
                        || deal.State == DealState.FulfillmentPending))
                .Sum(deal => NegotiationEconomyUtility.CalculateDemandCost(
                    faction,
                    deal.Rewards ?? new RewardDemand { Silver = deal.SilverAmount }));
            return Math.Max(0, (int)Math.Floor(reserve) - committed);
        }

        private void ApplyRejectedNegotiation(FactionNegotiationMemory memory, NegotiationResult result)
        {
            if (memory == null)
            {
                return;
            }

            memory.RejectedNegotiations++;
            bool absurd = result != null
                && result.DemandCost > result.NegotiationBudget * PrisonerNegotiationUtility.MaximumCounterableRatio;
            if (!absurd)
            {
                return;
            }

            memory.Impatience++;
            ApplyMemoryChange(memory.Faction, -2f, 0f, 6f,
                "PD_MemoryEventAbsurdDemand", string.Empty, memory.Impatience >= 2);
            if (memory.Impatience >= 2)
            {
                int suspensionDays = Math.Min(7, 2 + memory.Impatience);
                memory.NegotiationSuspendedUntilTick = Math.Max(
                    memory.NegotiationSuspendedUntilTick,
                    Find.TickManager.TicksGame + suspensionDays * TicksPerDay);
            }
        }

        private static void RemoveOfferLetter(string dealId)
        {
            if (Find.LetterStack == null || string.IsNullOrEmpty(dealId))
            {
                return;
            }

            ChoiceLetter_PrisonerRansomOffer letter = Find.LetterStack.LettersListForReading
                .OfType<ChoiceLetter_PrisonerRansomOffer>()
                .FirstOrDefault(item => item.DealId == dealId);
            if (letter != null)
            {
                Find.LetterStack.RemoveLetter(letter);
            }
        }

        private AiNarrativeRecord QueueAiNarrative(
            AiNarrativeEventKind eventKind,
            Pawn prisoner,
            Faction faction,
            PrisonerDeal deal,
            string formalOutcome,
            string formalTerms,
            string fallbackText,
            string windowContextId = null,
            int expectedNegotiationCount = -1,
            string playerNote = null)
        {
            PrisonerDiplomacySettings settings = PrisonerDiplomacyMod.Settings;
            if (settings?.EnableAiNarratives != true || GenCommandLine.CommandLineArgPassed("pdsmoketest"))
            {
                return null;
            }

            foreach (AiNarrativeRecord pending in aiNarratives.Where(item => item != null
                && item.Status == AiNarrativeStatus.Waiting
                && (deal != null && item.DealId == deal.DealId
                    || deal == null && item.DealId == null && item.PrisonerLoadId == prisoner?.GetUniqueLoadID())).ToList())
            {
                AiNarrativeService.Cancel(pending.RequestId);
                pending.Status = AiNarrativeStatus.Fallback;
                pending.FailureCode = "superseded";
                pending.ResolvedTick = Find.TickManager.TicksGame;
            }

            string contextId = "PD-AI-" + nextAiNarrativeSequence++.ToString("D7");
            int candidateVersion = Gen.HashCombineInt(
                GenText.StableStringHash((formalOutcome ?? string.Empty) + "|" + (formalTerms ?? string.Empty)),
                deal?.NegotiationRound ?? expectedNegotiationCount) & int.MaxValue;
            AiNarrativeRecord narrative = new AiNarrativeRecord
            {
                ContextId = contextId,
                RequestId = Guid.NewGuid().ToString("N"),
                WindowContextId = windowContextId,
                DealId = deal?.DealId,
                Prisoner = prisoner,
                PrisonerLoadId = prisoner?.GetUniqueLoadID(),
                Faction = faction,
                EventKind = eventKind,
                Status = AiNarrativeStatus.Waiting,
                CandidateVersion = candidateVersion,
                HasExpectedDealState = deal != null,
                ExpectedDealState = deal?.State ?? DealState.Rejected,
                ExpectedNegotiationRound = deal?.NegotiationRound ?? -1,
                ExpectedNegotiationCount = expectedNegotiationCount,
                FormalOutcome = formalOutcome,
                FormalTerms = formalTerms,
                PlayerNote = AiNegotiationNoteUtility.Normalize(playerNote),
                PlayerEmotion = AiNegotiationNoteUtility.Classify(playerNote),
                FallbackText = fallbackText,
                CreatedTick = Find.TickManager.TicksGame
            };
            aiNarratives.Add(narrative);

            if (prisoner == null || faction == null)
            {
                narrative.Status = AiNarrativeStatus.Fallback;
                narrative.FailureCode = "invalid_context";
                narrative.ResolvedTick = narrative.CreatedTick;
                return narrative;
            }

            string issue = AiNarrativeService.ConfigurationIssue(settings);
            if (issue != null)
            {
                narrative.Status = AiNarrativeStatus.Fallback;
                narrative.FailureCode = issue;
                narrative.ResolvedTick = narrative.CreatedTick;
                return narrative;
            }

            PrisonerRecord record = GetRecord(prisoner);
            FactionNegotiationMemory memory = GetFactionMemory(faction, true);
            AiNarrativePrompt prompt = AiNarrativeContextUtility.BuildPrompt(
                narrative,
                record,
                memory,
                GetFactionFinancialStatus(faction));
            AiNarrativeService.Start(prompt, AiNarrativeService.SnapshotConfig(settings));
            return narrative;
        }

        private void DrainAiNarrativeCompletions()
        {
            while (AiNarrativeService.TryDequeue(out AiNarrativeCompletion completion))
            {
                AiNarrativeRecord narrative = aiNarratives.FirstOrDefault(item => item != null
                    && item.RequestId == completion.RequestId
                    && item.ContextId == completion.ContextId
                    && item.CandidateVersion == completion.CandidateVersion);
                if (narrative == null || narrative.Status != AiNarrativeStatus.Waiting)
                {
                    continue;
                }

                if (completion.Cancelled || !string.IsNullOrEmpty(completion.FailureCode))
                {
                    FallbackAiNarrative(narrative, completion.FailureCode ?? "cancelled");
                    continue;
                }

                if (PrisonerDiplomacyMod.Settings?.EnableAiNarratives != true)
                {
                    FallbackAiNarrative(narrative, "disabled");
                    continue;
                }

                if (completion.FormalOutcome != narrative.FormalOutcome
                    || !IsAiNarrativeContextCurrent(narrative))
                {
                    FallbackAiNarrative(narrative, "stale_context");
                    continue;
                }

                if (completion.Advisory != null
                    && PrisonerDiplomacyMod.Settings?.EnableAiNegotiationAdjustments == true)
                {
                    ApplyAiNegotiationAdvisory(narrative, completion.Advisory);
                }

                narrative.GeneratedText = completion.Message;
                narrative.Status = AiNarrativeStatus.Generated;
                narrative.FailureCode = null;
                narrative.ResolvedTick = Find.TickManager?.TicksGame ?? narrative.CreatedTick;
                PresentAiNarrative(narrative);
            }
        }

        private void ApplyAiNegotiationAdvisory(
            AiNarrativeRecord narrative,
            AiNegotiationAdvisory advisory)
        {
            if (narrative == null || advisory == null || narrative.AdvisoryApplied)
            {
                return;
            }

            PrisonerDeal deal = GetDeal(narrative.DealId);
            if (deal == null || deal.State != DealState.Negotiating
                || (narrative.EventKind != AiNarrativeEventKind.PlayerDemandCountered
                    && narrative.EventKind != AiNarrativeEventKind.FinalCounter))
            {
                return;
            }

            int now = Find.TickManager?.TicksGame ?? narrative.CreatedTick;
            int reserve = GetSpendableReserve(deal.Faction, now, deal);
            int materialCap = NegotiationEconomyUtility.CalculateMaterialRewardCap(deal.Map);
            if (!AiNegotiationAdvisoryUtility.TryApply(
                deal,
                advisory,
                reserve,
                materialCap,
                out RewardDemand adjusted,
                out string summary))
            {
                return;
            }

            deal.Rewards = adjusted;
            deal.SilverAmount = adjusted.Silver;
            deal.NegotiationDemandCost = NegotiationEconomyUtility.CalculateDemandCost(
                deal.Faction,
                adjusted);
            narrative.FormalTerms = adjusted.Description().ToString();
            narrative.AdvisoryApplied = true;
            narrative.AdvisorySummary = summary;
            Log.Message("[Prisoner Diplomacy] Applied bounded AI negotiation advisory for "
                + (deal.Faction?.Name ?? "unknown faction") + ": " + summary + ".");
        }

        private bool IsAiNarrativeContextCurrent(AiNarrativeRecord narrative)
        {
            if (narrative.Faction == null || narrative.Prisoner == null
                || narrative.Prisoner.GetUniqueLoadID() != narrative.PrisonerLoadId)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(narrative.DealId))
            {
                PrisonerDeal deal = GetDeal(narrative.DealId);
                return deal != null
                    && deal.Prisoner == narrative.Prisoner
                    && deal.Faction == narrative.Faction
                    && (!narrative.HasExpectedDealState || deal.State == narrative.ExpectedDealState)
                    && (narrative.ExpectedNegotiationRound < 0
                        || deal.NegotiationRound == narrative.ExpectedNegotiationRound);
            }

            PrisonerRecord record = GetRecord(narrative.Prisoner);
            return record != null
                && record.OriginalFaction == narrative.Faction
                && GetActiveDeal(narrative.Prisoner) == null
                && (narrative.ExpectedNegotiationCount < 0
                    || record.NegotiationCount == narrative.ExpectedNegotiationCount);
        }

        private void FallbackAiNarrative(AiNarrativeRecord narrative, string failureCode)
        {
            narrative.Status = AiNarrativeStatus.Fallback;
            narrative.FailureCode = failureCode;
            narrative.ResolvedTick = Find.TickManager?.TicksGame ?? narrative.CreatedTick;
            if (PrisonerDiplomacyMod.Settings?.AiShowTechnicalErrors == true
                && failureCode != "window_closed"
                && failureCode != "superseded"
                && failureCode != "disabled"
                && failureCode != "stale_context"
                && failureCode != "smoke_test")
            {
                Messages.Message("PD_AiFallbackTechnical".Translate(failureCode), MessageTypeDefOf.NeutralEvent, false);
            }
        }

        private static void PresentAiNarrative(AiNarrativeRecord narrative)
        {
            if (narrative.EventKind == AiNarrativeEventKind.FactionOffer && Find.LetterStack != null)
            {
                ChoiceLetter_PrisonerRansomOffer letter = Find.LetterStack.LettersListForReading
                    .OfType<ChoiceLetter_PrisonerRansomOffer>()
                    .FirstOrDefault(item => item.DealId == narrative.DealId);
                if (letter != null)
                {
                    letter.ApplyAiNarrative(narrative.GeneratedText, narrative.FallbackText);
                    return;
                }
            }

            Messages.Message(
                "PD_AiNarrativeReady".Translate(narrative.Faction?.NameColored ?? "?"),
                narrative.Prisoner,
                MessageTypeDefOf.NeutralEvent,
                false);
        }

        private string FailureReason(DealState state)
        {
            switch (state)
            {
                case DealState.Expired: return "PD_ReasonExpired".Translate();
                case DealState.FailedPrisonerDead: return "PD_ReasonDead".Translate();
                case DealState.FailedEscaped: return "PD_ReasonEscaped".Translate();
                case DealState.FailedRecruited: return "PD_ReasonRecruited".Translate();
                case DealState.FailedEnslaved: return "PD_ReasonEnslaved".Translate();
                case DealState.FailedSoldOrTransferred: return "PD_ReasonSold".Translate();
                case DealState.FailedFactionInvalid: return "PD_ReasonFactionInvalid".Translate();
                case DealState.FailedHostageInvalid: return "PD_ReasonHostageInvalid".Translate();
                case DealState.Cancelled: return "PD_ReasonCancelled".Translate();
                default: return "PD_ReasonPrisonerInvalid".Translate();
            }
        }

        private void RepairLoadedData()
        {
            int loadedVersion = saveVersion;
            bool migrateBrokenSilverPayments = saveVersion < 2;
            records.RemoveAll(record => record == null
                || record.Pawn == null && string.IsNullOrEmpty(record.PawnLoadId));
            deals.RemoveAll(deal => deal == null
                || string.IsNullOrEmpty(deal.DealId) && deal.Prisoner == null && string.IsNullOrEmpty(deal.PrisonerLoadId));
            factionNegotiationMemories.RemoveAll(memory => memory == null || memory.Faction == null);
            factionStrategicStates.RemoveAll(state => state == null || state.Faction == null);
            strategicFollowups.RemoveAll(followup => followup == null
                || string.IsNullOrEmpty(followup.EventId)
                || followup.Faction == null);
            commTargets.RemoveAll(target => target == null || target.Faction == null);
            aiNarratives.RemoveAll(narrative => narrative == null || string.IsNullOrEmpty(narrative.ContextId));
            foreach (PrisonerRecord record in records)
            {
                if (record.Pawn != null)
                {
                    record.PawnLoadId = record.Pawn.GetUniqueLoadID();
                    if (saveVersion < 7)
                    {
                        record.CapturedHealthPercent = record.Pawn.health?.summaryHealth?.SummaryHealthPercent ?? 1f;
                        record.WasLifeThreatening = PrisonerTreatmentUtility.IsLifeThreatening(record.Pawn);
                        record.LastMissingPartCount = PrisonerTreatmentUtility.CountMissingParts(record.Pawn);
                        record.LastPermanentInjuryCount = PrisonerTreatmentUtility.CountPermanentInjuries(record.Pawn);
                        record.LastTreatmentCheckTick = Find.TickManager.TicksGame;
                    }
                if (saveVersion < 8)
                {
                    record.LastPlayerTreatmentTick = -1;
                }
                }
                EnsureFactionOfferSchedule(record);
            }

            foreach (FactionNegotiationMemory memory in factionNegotiationMemories)
            {
                memory.RecentEvents = memory.RecentEvents ?? new List<PrisonerMemoryEvent>();
                if (memory.MemoryUpdatedTick < 0)
                {
                    memory.MemoryUpdatedTick = Find.TickManager.TicksGame;
                }
                FactionPrisonerMemoryUtility.ClampMemory(memory);
                AiNarrativeContextUtility.EnsurePersona(memory, memory.Faction);
            }

            int strategicNow = Find.TickManager.TicksGame;
            foreach (FactionStrategicState state in factionStrategicStates)
            {
                if (state.CeasefireExpiresTick <= strategicNow)
                {
                    state.CeasefireExpiresTick = -1;
                }
                if (state.IntelExpiresTick <= strategicNow)
                {
                    state.IntelAvailable = false;
                    state.IntelExpiresTick = -1;
                }
                if (state.WarnedRaidFireTick >= 0 && state.WarnedRaidFireTick + TicksPerDay < strategicNow)
                {
                    state.ClearWarnedRaid();
                }
            }

            foreach (AiNarrativeRecord narrative in aiNarratives)
            {
                narrative.WindowContextId = null;
                if (narrative.Prisoner != null)
                {
                    narrative.PrisonerLoadId = narrative.Prisoner.GetUniqueLoadID();
                }

                if (narrative.Status == AiNarrativeStatus.Waiting)
                {
                    narrative.Status = AiNarrativeStatus.Fallback;
                    narrative.FailureCode = "load_interrupted";
                    narrative.ResolvedTick = Find.TickManager.TicksGame;
                }
                else if (narrative.Status == AiNarrativeStatus.Generated
                    && string.IsNullOrWhiteSpace(narrative.GeneratedText))
                {
                    narrative.Status = AiNarrativeStatus.Fallback;
                    narrative.FailureCode = "missing_persisted_text";
                    narrative.ResolvedTick = Find.TickManager.TicksGame;
                }
                else if (narrative.Status == AiNarrativeStatus.Generated)
                {
                    AiNarrativePrompt validationPrompt = new AiNarrativePrompt
                    {
                        transaction = AiNarrativeContextUtility.BuildTransactionContext(narrative.EventKind)
                    };
                    if (!OpenAiCompatibleNarrativeProvider.TryValidateNarrativeMessage(
                        narrative.GeneratedText,
                        validationPrompt,
                        out string persistedFailure))
                    {
                        narrative.Status = AiNarrativeStatus.Fallback;
                        narrative.GeneratedText = null;
                        narrative.FailureCode = "persisted_" + (persistedFailure ?? "invalid_message");
                        narrative.ResolvedTick = Find.TickManager.TicksGame;
                    }
                }
            }

            foreach (PrisonerDeal deal in deals)
            {
                if (deal.Rewards == null)
                {
                    deal.Rewards = new RewardDemand { Silver = deal.SilverAmount };
                }

                if (saveVersion < 8)
                {
                    deal.DeadlineExtensionCount = 0;
                    deal.LastTreatmentTickAtExtension = deal.AcceptedTick >= 0
                        ? deal.AcceptedTick - 1
                        : -1;
                }

                if (deal.LastPlayerDemand == null && deal.Origin == DealOrigin.PlayerDemand)
                {
                    deal.LastPlayerDemand = deal.Rewards.Clone();
                }

                if (saveVersion < 9 || deal.NegotiationType == FactionNegotiationType.Diplomatic && deal.Faction != null)
                {
                    deal.NegotiationType = FactionNegotiationUtility.GetType(deal.Faction);
                }

                if (deal.ReturnedHostage != null)
                {
                    deal.ReturnedHostageLoadId = deal.ReturnedHostage.GetUniqueLoadID();
                }

                if (deal.RewardIssued)
                {
                    deal.SilverRewardIssued = true;
                    deal.SupplyRewardIssued = true;
                    deal.GoodwillRewardIssued = true;
                    deal.CeasefireRewardIssued = true;
                    deal.IntelRewardIssued = true;
                    deal.SpecialRewardIssued = true;
                    deal.ReserveCharged = true;
                }

                if (deal.Prisoner != null)
                {
                    deal.PrisonerLoadId = deal.Prisoner.GetUniqueLoadID();
                }

                if (migrateBrokenSilverPayments
                    && deal.State == DealState.Completed
                    && deal.PrisonerDelivered
                    && deal.RewardIssued
                    && deal.SilverAmount > 0)
                {
                    deal.State = DealState.FulfillmentPending;
                    deal.RewardIssued = false;
                    deal.SilverRewardIssued = false;
                    deal.SupplyRewardIssued = false;
                    deal.GoodwillRewardIssued = false;
                    deal.CeasefireRewardIssued = false;
                    deal.IntelRewardIssued = false;
                    deal.SpecialRewardIssued = false;
                    deal.ReserveCharged = false;
                    deal.CompletedTick = -1;
                    Log.Warning("[Prisoner Diplomacy] Queued corrected silver payment for legacy deal " + deal.DealId + ".");
                }

                if (deal.IsActive)
                {
                    PrisonerRecord record = records.FirstOrDefault(item => item.PawnLoadId == deal.PrisonerLoadId);
                    if (record != null)
                    {
                        record.ActiveDealId = deal.DealId;
                    }
                }

                if (deal.State == DealState.FulfillmentPending && deal.RewardIssued)
                {
                    deal.State = DealState.Completed;
                    deal.CompletedTick = Math.Max(deal.CompletedTick, Find.TickManager.TicksGame);
                    ClearRecordDeal(deal);
                }
            }

            lastRepairSummary = RepairCompatibilityData(true);
            saveVersion = SaveVersion;
            if (loadedVersion < SaveVersion)
            {
                Log.Message("[Prisoner Diplomacy] Migrated save schema " + loadedVersion
                    + " to " + SaveVersion + ". 1.2 event and special-reward fields use conservative defaults.");
            }
        }

        private void TryRunCommandLineSmokeTest()
        {
            if (!commandLineSmokeTestPending || Find.Maps.Count == 0 || Find.FactionManager == null)
            {
                if (commandLineSmokeTestPending && !commandLineSmokeTestWaitLogged)
                {
                    commandLineSmokeTestWaitLogged = true;
                    Log.Message("[Prisoner Diplomacy SmokeTest] WAIT maps=" + Find.Maps.Count
                        + " factionManager=" + (Find.FactionManager != null));
                }
                return;
            }

            Map map = Find.Maps.FirstOrDefault(candidate => candidate.IsPlayerHome);
            Faction faction = Find.FactionManager.AllFactionsVisible
                .Where(PrisonerEligibilityUtility.IsNegotiatingFaction)
                .OrderByDescending(candidate => candidate.HostileTo(Faction.OfPlayer))
                .FirstOrDefault();
            if (map == null || faction == null)
            {
                if (!commandLineSmokeTestWaitLogged)
                {
                    commandLineSmokeTestWaitLogged = true;
                    Log.Message("[Prisoner Diplomacy SmokeTest] WAIT playerHome=" + (map != null)
                        + " visibleFactions=" + Find.FactionManager.AllFactionsVisible.Count());
                }
                return;
            }

            commandLineSmokeTestPending = false;
            try
            {
                PawnKindDef pawnKind = faction.def.basicMemberKind ?? PawnKindDefOf.SpaceRefugee;
                AssertSmokeTest(PrisonerEligibilityUtility.IsNegotiatingFaction(faction),
                    "selected smoke-test faction was not negotiable");
                FactionNegotiationOverride originalOverride = PrisonerDiplomacyMod.Settings?.GetOverride(faction.def.defName)
                    ?? FactionNegotiationOverride.Automatic;
                PrisonerDiplomacyMod.Settings?.SetOverride(faction.def.defName, FactionNegotiationOverride.NonNegotiating);
                AssertSmokeTest(!PrisonerEligibilityUtility.IsNegotiatingFaction(faction)
                    && FactionNegotiationUtility.GetType(faction) == FactionNegotiationType.NonNegotiating,
                    "faction negotiation override did not disable negotiation");
                PrisonerDiplomacyMod.Settings?.SetOverride(faction.def.defName, originalOverride);
                AssertSmokeTest(PrisonerEligibilityUtility.IsNegotiatingFaction(faction),
                    "faction negotiation override was not restored");
                PrisonerRecord valueProbe = new PrisonerRecord
                {
                    Pawn = PawnGenerator.GeneratePawn(pawnKind, faction),
                    PawnLoadId = "PD-Smoke-ValueProbe",
                    OriginalFaction = faction,
                    CapturedMarketValue = 400f,
                    Importance = PrisonerImportance.Regular
                };
                PrisonerDiplomacySettings tuningSettings = PrisonerDiplomacyMod.Settings;
                bool originalEnemyOffers = tuningSettings.EnableEnemyInitiatedRansoms;
                float originalFrequency = tuningSettings.OfferFrequencyMultiplier;
                float originalValueMultiplier = tuningSettings.RansomValueMultiplier;
                bool originalReserves = tuningSettings.EnableFactionReserves;
                bool originalMemory = tuningSettings.EnableFactionMemory;
                bool originalAiEnabled = tuningSettings.EnableAiNarratives;
                bool originalAiContext = tuningSettings.AiAllowExternalContext;
                string originalApiKey = tuningSettings.AiApiKey;
                PrisonerDiplomacyMessageDetail originalMessageDetail = tuningSettings.MessageDetail;
                try
                {
                    tuningSettings.RansomValueMultiplier = 1.50f;
                    AssertSmokeTest(PrisonerDiplomacyTuning.ScaleRansomValue(1000) == 1500,
                        "ransom value multiplier did not scale a deterministic value");

                    tuningSettings.OfferFrequencyMultiplier = 1f;
                    int normalFrequencyDelay = CalculateFactionContactDelayTicks(
                        "PD-Smoke-Frequency", Find.TickManager.TicksGame, PrisonerImportance.Regular);
                    tuningSettings.OfferFrequencyMultiplier = 2f;
                    int increasedFrequencyDelay = CalculateFactionContactDelayTicks(
                        "PD-Smoke-Frequency", Find.TickManager.TicksGame, PrisonerImportance.Regular);
                    AssertSmokeTest(increasedFrequencyDelay <= normalFrequencyDelay,
                        "higher offer frequency did not shorten the deterministic contact delay");

                    tuningSettings.EnableEnemyInitiatedRansoms = false;
                    valueProbe.ScheduledFactionOfferTick = Find.TickManager.TicksGame;
                    AssertSmokeTest(!ShouldCreateOffer(valueProbe, Find.TickManager.TicksGame),
                        "disabled enemy-initiated ransoms still allowed a passive offer");

                    FactionNegotiationMemory tuningMemory = GetFactionMemory(faction, true);
                    tuningMemory.Reliability = 50f;
                    tuningSettings.EnableFactionMemory = false;
                    AssertSmokeTest(Math.Abs(GetFactionMemoryMultiplier(faction, Find.TickManager.TicksGame) - 1f) < 0.0001f
                        && Math.Abs(PrisonerDiplomacyTuning.EffectiveReliability(tuningMemory)) < 0.0001f,
                        "disabled faction memory still affected negotiation or pirate risk");

                    tuningSettings.EnableFactionReserves = false;
                    tuningMemory.DiplomaticReserve = 0f;
                    AssertSmokeTest(Math.Abs(GetAvailableReserve(tuningMemory, Find.TickManager.TicksGame)
                            - NegotiationEconomyUtility.CalculateMaximumReserve(faction)) < 1f,
                        "disabled faction reserves still exposed a depleted reserve");

                    tuningSettings.MessageDetail = PrisonerDiplomacyMessageDetail.Essential;
                    AssertSmokeTest(!PrisonerDiplomacyTuning.ShouldNotifyMemory(12f)
                        && PrisonerDiplomacyTuning.ShouldNotifyMemory(25f),
                        "essential message detail did not filter minor memory changes");

                    tuningSettings.EnableAiNarratives = true;
                    tuningSettings.AiAllowExternalContext = false;
                    AssertSmokeTest(AiNarrativeService.ConfigurationIssue(tuningSettings)
                            == "external_context_disabled",
                        "AI narrative did not require explicit external-context consent");

                    tuningSettings.AiApiKey = "PD_SMOKE_SECRET_KEY";
                    AssertSmokeTest(!PrisonerDiplomacyDiagnostics.BuildReport().Contains(tuningSettings.AiApiKey),
                        "diagnostic report exposed the AI API Key");
                }
                finally
                {
                    tuningSettings.EnableEnemyInitiatedRansoms = originalEnemyOffers;
                    tuningSettings.OfferFrequencyMultiplier = originalFrequency;
                    tuningSettings.RansomValueMultiplier = originalValueMultiplier;
                    tuningSettings.EnableFactionReserves = originalReserves;
                    tuningSettings.EnableFactionMemory = originalMemory;
                    tuningSettings.EnableAiNarratives = originalAiEnabled;
                    tuningSettings.AiAllowExternalContext = originalAiContext;
                    tuningSettings.AiApiKey = originalApiKey;
                    tuningSettings.MessageDetail = originalMessageDetail;
                    ResetSmokeTestFactionMemory(faction);
                }
                int regularOffer = PrisonerValueCalculator.CalculateOffer(valueProbe);
                valueProbe.Importance = PrisonerImportance.Leader;
                int leaderOffer = PrisonerValueCalculator.CalculateOffer(valueProbe);
                if (FactionNegotiationUtility.IsTransactional(faction))
                {
                    AssertSmokeTest(leaderOffer > regularOffer,
                        "transactional faction did not value a leader above a regular member");
                    AssertSmokeTest(!NegotiationEconomyUtility.CanRequestGoodwill(faction),
                        "transactional faction incorrectly exposed goodwill rewards");
                }
                int silverBeforeSuccess = CountSilverOnMap(map);
                PrisonerDeal success = CreateSmokeTestDeal(map, faction, pawnKind, true);
                Pawn successPawn = success.Prisoner;
                GenGuest.PrisonerRelease(successPawn);
                successPawn.ExitMap(false, Rot4.North);
                int silverAfterSuccess = CountSilverOnMap(map);
                AssertSmokeTest(success.State == DealState.Completed
                    && success.PrisonerDelivered
                    && success.RewardIssued
                    && silverAfterSuccess - silverBeforeSuccess == success.SilverAmount
                    && !successPawn.Spawned
                    && successPawn.Map == null,
                    "successful deal did not deliver physical silver; expected=" + success.SilverAmount
                        + " actualDelta=" + (silverAfterSuccess - silverBeforeSuccess));
                NotifyPawnExited(successPawn, map, true);
                AssertSmokeTest(success.State == DealState.Completed
                    && success.RewardIssued
                    && CountSilverOnMap(map) == silverAfterSuccess,
                    "completed deal changed or paid again after duplicate exit notification");

                PrisonerDeal ordinaryRelease = CreateSmokeTestDeal(map, faction, pawnKind, false);
                GenGuest.PrisonerRelease(ordinaryRelease.Prisoner);
                ordinaryRelease.Prisoner.ExitMap(false, Rot4.North);
                AssertSmokeFailure(ordinaryRelease, DealState.FailedEscaped, "ordinary release");

                PrisonerRecord passiveExpiryRecord = CreateSmokeTestRecord(map, faction, pawnKind);
                PrisonerDeal passiveExpiryDeal = ForceOffer(passiveExpiryRecord.Pawn);
                passiveExpiryDeal.OfferExpiresTick = Find.TickManager.TicksGame;
                UpdateDeal(passiveExpiryDeal, Find.TickManager.TicksGame);
                AssertSmokeTest(passiveExpiryDeal.State == DealState.Expired
                    && !passiveExpiryRecord.TerminalOutcomeRecorded,
                    "unaccepted offer expiry incorrectly ended prisoner treatment tracking");

                PrisonerRecord passiveEscapeRecord = CreateSmokeTestRecord(map, faction, pawnKind);
                PrisonerDeal passiveEscapeDeal = ForceOffer(passiveEscapeRecord.Pawn);
                passiveEscapeRecord.Pawn.ExitMap(false, Rot4.North);
                FactionNegotiationMemory passiveEscapeMemory = GetFactionMemory(faction, true);
                AssertSmokeTest(passiveEscapeDeal.State == DealState.FailedEscaped
                    && passiveEscapeRecord.TerminalOutcomeRecorded
                    && passiveEscapeMemory.RecentEvents.LastOrDefault()?.ReasonKey == "PD_MemoryEventEscaped",
                    "escape before agreement was not recorded as an ordinary escape");

                PrisonerDeal untreatedExpiry = CreateSmokeTestDeal(map, faction, pawnKind, false);
                PrisonerRecord untreatedRecord = GetRecord(untreatedExpiry.Prisoner);
                AddSmokeTestTendableInjury(untreatedExpiry.Prisoner);
                untreatedExpiry.FulfillmentExpiresTick = Find.TickManager.TicksGame;
                UpdateDeal(untreatedExpiry, Find.TickManager.TicksGame);
                AssertSmokeTest(untreatedExpiry.State == DealState.Expired
                    && untreatedRecord.LastPlayerTreatmentTick < untreatedExpiry.AcceptedTick,
                    "an untreated downed prisoner incorrectly received a deadline extension");

                PrisonerDeal treatedExpiry = CreateSmokeTestDeal(map, faction, pawnKind, false);
                PrisonerRecord treatedRecord = GetRecord(treatedExpiry.Prisoner);
                AddSmokeTestTendableInjury(treatedExpiry.Prisoner);
                treatedExpiry.FulfillmentExpiresTick = Find.TickManager.TicksGame;
                NotifyPlayerMedicalTreatment(treatedExpiry.Prisoner, map.mapPawns.FreeColonistsSpawned.FirstOrDefault());
                int originalDeadline = treatedExpiry.FulfillmentExpiresTick;
                UpdateDeal(treatedExpiry, Find.TickManager.TicksGame);
                AssertSmokeTest(treatedExpiry.IsActive
                    && treatedExpiry.DeadlineExtensionCount == 1
                    && treatedExpiry.FulfillmentExpiresTick == originalDeadline + FulfillmentExtensionTicks
                    && treatedRecord.LastPlayerTreatmentTick == treatedExpiry.LastTreatmentTickAtExtension,
                    "active player treatment did not extend an injured prisoner's deadline by two days");

                treatedExpiry.FulfillmentExpiresTick = Find.TickManager.TicksGame;
                UpdateDeal(treatedExpiry, Find.TickManager.TicksGame);
                AssertSmokeTest(treatedExpiry.State == DealState.Expired
                    && treatedExpiry.DeadlineExtensionCount == 1,
                    "the same treatment event granted more than one deadline extension");

                PrisonerDeal expired = CreateSmokeTestDeal(map, faction, pawnKind, true);
                expired.FulfillmentExpiresTick = Find.TickManager.TicksGame;
                GenGuest.PrisonerRelease(expired.Prisoner);
                expired.Prisoner.ExitMap(false, Rot4.North);
                AssertSmokeFailure(expired, DealState.Expired, "expired release");

                PrisonerDeal sold = CreateSmokeTestDeal(map, faction, pawnKind, false);
                NotifyPawnSold(sold.Prisoner);
                AssertSmokeFailure(sold, DealState.FailedSoldOrTransferred, "sale or transfer");

                PrisonerDeal recruited = CreateSmokeTestDeal(map, faction, pawnKind, false);
                recruited.Prisoner.SetFaction(Faction.OfPlayer);
                UpdateDeal(recruited, Find.TickManager.TicksGame);
                AssertSmokeFailure(recruited, DealState.FailedRecruited, "recruitment");

                PrisonerDeal dead = CreateSmokeTestDeal(map, faction, pawnKind, false);
                dead.Prisoner.Kill(null, null);
                UpdateDeal(dead, Find.TickManager.TicksGame);
                AssertSmokeFailure(dead, DealState.FailedPrisonerDead, "death");

                Pawn negotiator = map.mapPawns.FreeColonistsSpawned.FirstOrDefault();
                AssertSmokeTest(negotiator != null, "no player negotiator available");
                ResetSmokeTestFactionMemory(faction);
                PrisonerRecord deterministicRecord = CreateSmokeTestRecord(map, faction, pawnKind);
                int acceptedDemand = FindDemandForOutcome(deterministicRecord, negotiator, NegotiationOutcome.Accepted);
                NegotiationResult deterministicA = PrisonerNegotiationUtility.Evaluate(deterministicRecord, negotiator, acceptedDemand);
                NegotiationResult deterministicB = PrisonerNegotiationUtility.Evaluate(deterministicRecord, negotiator, acceptedDemand);
                AssertSmokeTest(deterministicA.Seed == deterministicB.Seed
                    && deterministicA.Outcome == deterministicB.Outcome
                    && Math.Abs(deterministicA.AcceptanceChance - deterministicB.AcceptanceChance) < 0.0001f,
                    "player demand outcome was not deterministic");

                int silverBeforePlayerDemand = CountSilverOnMap(map);
                NegotiationResult acceptedResult = SubmitPlayerDemand(deterministicRecord, negotiator, acceptedDemand);
                PrisonerDeal playerDemandDeal = GetActiveDeal(deterministicRecord.Pawn);
                AssertSmokeTest(acceptedResult?.Outcome == NegotiationOutcome.Accepted
                    && playerDemandDeal != null
                    && playerDemandDeal.Origin == DealOrigin.PlayerDemand
                    && playerDemandDeal.State == DealState.AcceptedAwaitingRelease,
                    "accepted player demand did not create a player-origin deal");
                AssertSmokeTest(!ShouldCreateOffer(deterministicRecord, deterministicRecord.ScheduledFactionOfferTick),
                    "active player-origin deal did not suppress a faction offer");
                AssertSmokeTest(OrderRansomRelease(deterministicRecord.Pawn), "could not order accepted player-demand release");
                GenGuest.PrisonerRelease(deterministicRecord.Pawn);
                deterministicRecord.Pawn.ExitMap(false, Rot4.North);
                AssertSmokeTest(playerDemandDeal.State == DealState.Completed
                    && CountSilverOnMap(map) - silverBeforePlayerDemand == acceptedDemand,
                    "accepted player demand did not deliver physical silver");

                ResetSmokeTestFactionMemory(faction);
                PrisonerRecord rejectedRecord = CreateSmokeTestRecord(map, faction, pawnKind);
                int rejectedDemand = FindDemandForOutcome(rejectedRecord, negotiator, NegotiationOutcome.Rejected);
                NegotiationResult rejectedResult = SubmitPlayerDemand(rejectedRecord, negotiator, rejectedDemand);
                AssertSmokeTest(rejectedResult?.Outcome == NegotiationOutcome.Rejected
                    && GetActiveDeal(rejectedRecord.Pawn) == null,
                    "rejected player demand left an active deal");
                AssertSmokeTest(rejectedRecord.ScheduledFactionOfferTick - Find.TickManager.TicksGame >= MinimumFactionContactDelayTicks,
                    "rejected player demand did not reschedule faction contact");
                AssertSmokeTest(!CanStartPlayerNegotiation(rejectedRecord, out _),
                    "submitted player demand did not apply prisoner cooldown");

                AssertSmokeTest(!PrisonerNegotiationUtility.TryParseDemand(string.Empty, out _),
                    "empty demand should remain invalid while editing");
                AssertSmokeTest(PrisonerNegotiationUtility.TryParseDemand("550", out int parsedDemand) && parsedDemand == 550,
                    "550 demand did not parse exactly");
                AssertSmokeTest(!PrisonerNegotiationUtility.TryParseDemand("55100", out _),
                    "out-of-range demand should not be silently clamped");

                RewardDemand threeRewardDemand = new RewardDemand
                {
                    Silver = 100,
                    CeasefireDays = NegotiationEconomyUtility.MinimumCeasefireDays,
                    EarlyWarningIntel = true
                };
                AssertSmokeTest(NegotiationEconomyUtility.IsDemandValid(faction, threeRewardDemand, out _)
                    && threeRewardDemand.RewardTypeCount == NegotiationEconomyUtility.MaximumRewardTypes,
                    "three reward types should be valid in the 1.0 contract");
                AssertSmokeTest(PrisonerDiplomacyBackendApi.GetPrisonerSnapshots(map).Any(snapshot =>
                        snapshot.Pawn == rejectedRecord.Pawn
                        && snapshot.DiplomaticValue == rejectedRecord.DiplomaticValue)
                    && PrisonerDiplomacyBackendApi.GetFactionSnapshots(map).Any(snapshot => snapshot.Faction == faction),
                    "UI-independent backend snapshots did not expose the current prisoner and faction");
                NegotiationResult backendPreview = PrisonerDiplomacyBackendApi.PreviewDemand(
                    rejectedRecord.Pawn,
                    negotiator,
                    new RewardDemand { Silver = 100 },
                    out string backendPreviewReason);
                AssertSmokeTest(backendPreview != null
                    && backendPreviewReason == null
                    && backendPreview.RequestedRewards.Silver == 100,
                    "UI-independent demand preview did not preserve a legal reward");
                AssertSmokeTest(!NegotiationEconomyUtility.IsDemandValid(faction, new RewardDemand
                {
                    Silver = 100,
                    Goodwill = 1,
                    CeasefireDays = NegotiationEconomyUtility.MinimumCeasefireDays,
                    EarlyWarningIntel = true
                }, out string tooManyRewardsReason) && tooManyRewardsReason == "PD_NegotiationTooManyRewards",
                    "four reward types should be rejected");
                AssertSmokeTest(NegotiationEconomyUtility.CalculateGoodwillCost(10) == 1400,
                    "goodwill cost formula changed unexpectedly");
                AssertSmokeTest(NegotiationEconomyUtility.CalculateCeasefireCost(10) == 1200
                    && NegotiationEconomyUtility.CalculateCeasefireCost(20) == 2200
                    && NegotiationEconomyUtility.CalculateCeasefireCost(30) == 3500,
                    "ceasefire cost formula did not match the v0.7 design benchmarks");
                RewardDemand strategicDemand = new RewardDemand { CeasefireDays = 10, EarlyWarningIntel = true };
                AssertSmokeTest(NegotiationEconomyUtility.IsDemandValid(faction, strategicDemand, out _)
                    && strategicDemand.RewardTypeCount == 2
                    && NegotiationEconomyUtility.CalculateDemandCost(faction, strategicDemand) == 2100,
                    "ceasefire and intel were not valid composable reward types");
                RewardDemand scaledStrategic = NegotiationEconomyUtility.ScaleDemandToCost(
                    faction, new RewardDemand { CeasefireDays = 30 }, 1200);
                AssertSmokeTest(scaledStrategic.CeasefireDays == 10
                    && NegotiationEconomyUtility.CalculateDemandCost(faction, scaledStrategic) == 1200,
                    "ceasefire counteroffer scaling did not preserve an affordable duration");
                RewardDemand saferPirateTerms = NegotiationEconomyUtility.CreateSaferPirateTerms(
                    faction, new RewardDemand { Silver = 1000 });
                AssertSmokeTest(saferPirateTerms != null
                    && saferPirateTerms.Silver == 800
                    && NegotiationEconomyUtility.CalculateDemandCost(faction, saferPirateTerms) < 1000,
                    "pirate safer-terms countermeasure did not reduce the reward deterministically");
                AssertSmokeTest(SupplyRewardUtility.AvailableSupplies(faction).All(def => def != null && def.BaseMarketValue > 0f),
                    "available supply list contained an invalid item");

                ResetSmokeTestFactionMemory(faction);
                PrisonerDeal strategicDeal = CreateSmokeTestDeal(map, faction, pawnKind, false);
                strategicDeal.Rewards = strategicDemand.Clone();
                strategicDeal.SilverAmount = 0;
                strategicDeal.NegotiationDemandCost = NegotiationEconomyUtility.CalculateDemandCost(faction, strategicDeal.Rewards);
                AssertSmokeTest(OrderRansomRelease(strategicDeal.Prisoner),
                    "could not order strategic-reward smoke-test release");
                GenGuest.PrisonerRelease(strategicDeal.Prisoner);
                strategicDeal.Prisoner.ExitMap(false, Rot4.North);
                FactionStrategicState strategicState = GetFactionStrategicState(faction, false);
                AssertSmokeTest(strategicDeal.State == DealState.Completed
                    && strategicDeal.CeasefireRewardIssued
                    && strategicDeal.IntelRewardIssued
                    && strategicState?.CeasefireExpiresTick > Find.TickManager.TicksGame
                    && strategicState.IntelAvailable,
                    "verified delivery did not activate ceasefire and intelligence exactly once");
                AssertSmokeTest(GetPersistentCommTargetFactions(map).Contains(faction),
                    "a faction with an active strategic agreement disappeared from the comms console");
                int originalCeasefireExpiry = strategicState.CeasefireExpiresTick;
                int originalIntelExpiry = strategicState.IntelExpiresTick;
                FulfillDeal(strategicDeal);
                AssertSmokeTest(strategicState.CeasefireExpiresTick == originalCeasefireExpiry
                    && strategicState.IntelExpiresTick == originalIntelExpiry,
                    "duplicate fulfillment extended strategic rewards a second time");

                string delayedRiskText = FactionNegotiationUtility.RiskDescription(PirateDealRisk.DelayedPayment);
                string rescueRiskText = FactionNegotiationUtility.RiskDescription(PirateDealRisk.RescueRaid);
                string jailbreakRiskText = FactionNegotiationUtility.RiskDescription(PirateDealRisk.JailbreakIncitement);
                string ambushRiskText = FactionNegotiationUtility.RiskDescription(PirateDealRisk.Ambush);
                AssertSmokeTest(new[] { delayedRiskText, rescueRiskText, jailbreakRiskText, ambushRiskText }
                    .All(text => !string.IsNullOrWhiteSpace(text))
                    && new[] { delayedRiskText, rescueRiskText, jailbreakRiskText, ambushRiskText }.Distinct().Count() == 4,
                    "pirate risks were not individually disclosed by the UI contract");

                PrisonerRecord strategicRecord = GetRecord(strategicDeal.Prisoner);
                AddStrategicFollowup(StrategicFollowupKind.RetaliationRaid, strategicRecord,
                    strategicDeal.DealId, Find.TickManager.TicksGame + TicksPerDay, strategicDeal);
                StrategicFollowupEvent causalProbe = strategicFollowups.LastOrDefault();
                AssertSmokeTest(causalProbe != null
                    && causalProbe.SourcePawnLoadId == strategicDeal.PrisonerLoadId
                    && causalProbe.SourcePawnLabel == strategicDeal.Prisoner.LabelShortCap
                    && causalProbe.SourceDealId == strategicDeal.DealId,
                    "retaliation follow-up did not retain a concrete pawn and deal cause");
                strategicFollowups.Remove(causalProbe);

                IncidentParms forcedQuestRaid = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, map);
                forcedQuestRaid.faction = faction;
                forcedQuestRaid.forced = true;
                AssertSmokeTest(ShouldAllowResolvedRaid(
                        IncidentDefOf.RaidEnemy.Worker as IncidentWorker_RaidEnemy,
                        forcedQuestRaid)
                    && strategicState.IntelAvailable,
                    "quest or forced raid was incorrectly intercepted or consumed intelligence");

                Pawn breachInstigator = map.mapPawns.FreeColonistsSpawned.FirstOrDefault();
                DamageInfo breachDamage = new DamageInfo(DamageDefOf.Blunt, 1f, 0f, -1f, breachInstigator);
                float reliabilityBeforeBreach = GetFactionMemory(faction, true).Reliability;
                NotifyPlayerAttackAgainstFaction(faction, "PD smoke target", breachDamage);
                AssertSmokeTest(!IsCeasefireActive(faction)
                    && GetFactionMemory(faction, true).Reliability <= reliabilityBeforeBreach - 35f,
                    "player attack did not terminate ceasefire with a serious reliability breach");

                IncidentParms intelRaid = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, map);
                intelRaid.faction = faction;
                intelRaid.points = 500f;
                IncidentWorker_RaidEnemy raidWorker = IncidentDefOf.RaidEnemy.Worker as IncidentWorker_RaidEnemy;
                AssertSmokeTest(raidWorker != null && !ShouldAllowResolvedRaid(raidWorker, intelRaid)
                    && !strategicState.IntelAvailable
                    && strategicState.WarnedRaidFireTick > Find.TickManager.TicksGame,
                    "early-warning intelligence did not delay and consume the next eligible raid");
                strategicState.WarnedRaidFireTick = Find.TickManager.TicksGame;
                AssertSmokeTest(ShouldAllowResolvedRaid(raidWorker, intelRaid)
                    && strategicState.WarnedRaidFireTick < 0,
                    "the warned raid was intercepted more than once instead of using its bypass");
                factionStrategicStates.Remove(strategicState);

                ResetSmokeTestFactionMemory(faction);
                PrisonerRecord counterRecord = CreateSmokeTestRecord(map, faction, pawnKind);
                int counterBudget = NegotiationEconomyUtility.CalculateNegotiationBudget(counterRecord, negotiator);
                int counterDemand = Math.Min(PrisonerNegotiationUtility.MaximumDemand,
                    Math.Max(PrisonerNegotiationUtility.MinimumDemand,
                        (int)Math.Ceiling(counterBudget * 1.20f / 50f) * 50));
                NegotiationResult counterA = PrisonerNegotiationUtility.Evaluate(counterRecord, negotiator, counterDemand);
                NegotiationResult counterB = PrisonerNegotiationUtility.Evaluate(counterRecord, negotiator, counterDemand);
                AssertSmokeTest(counterA.Outcome == NegotiationOutcome.Countered
                    && counterA.CounterOffer != null
                    && counterA.Seed == counterB.Seed
                    && NegotiationEconomyUtility.CalculateDemandCost(faction, counterA.CounterOffer)
                        <= counterA.NegotiationBudget,
                    "counteroffer was not deterministic or exceeded its budget");
                RewardDemand plus50;
                RewardDemand plus100;
                RewardDemand plus250;
                AssertSmokeTest(NegotiationEconomyUtility.TryCreateCounterRevision(counterA.CounterOffer, 50, out plus50)
                    && NegotiationEconomyUtility.TryCreateCounterRevision(counterA.CounterOffer, 100, out plus100)
                    && NegotiationEconomyUtility.TryCreateCounterRevision(counterA.CounterOffer, 250, out plus250)
                    && plus50.Silver == counterA.CounterOffer.Silver + 50
                    && plus100.Silver == counterA.CounterOffer.Silver + 100
                    && plus250.Silver == counterA.CounterOffer.Silver + 250,
                    "counteroffer silver shortcuts were cumulative or calculated from the wrong base");
                AssertSmokeTest(!NegotiationEconomyUtility.TryCreateCounterRevision(new RewardDemand
                {
                    SupplyDef = SupplyRewardUtility.AvailableSupplies(faction).FirstOrDefault(),
                    SupplyCount = 1,
                    Goodwill = 1,
                    CeasefireDays = 1
                }, 50, out _),
                    "counteroffer shortcut incorrectly created a fourth reward type");
                NegotiationResult submittedCounter = SubmitPlayerDemand(counterRecord, negotiator, counterDemand);
                PrisonerDeal counterDeal = GetActiveDeal(counterRecord.Pawn);
                AssertSmokeTest(submittedCounter?.Outcome == NegotiationOutcome.Countered
                    && counterDeal?.State == DealState.Negotiating
                    && counterDeal.NegotiationRound == 1,
                    "counteroffer did not create a persisted negotiating deal");
                int firstCounterCost = NegotiationEconomyUtility.CalculateDemandCost(faction, counterDeal.Rewards);
                NegotiationResult finalCounter = RevisePlayerDemand(counterDeal, new RewardDemand { Silver = counterDemand });
                AssertSmokeTest(finalCounter?.Outcome == NegotiationOutcome.Countered
                    && counterDeal.State == DealState.Negotiating
                    && counterDeal.NegotiationRound == 2
                    && NegotiationEconomyUtility.CalculateDemandCost(faction, counterDeal.Rewards) > firstCounterCost
                    && NegotiationEconomyUtility.CalculateDemandCost(faction, counterDeal.Rewards) <= counterBudget,
                    "second player offer did not create an improved affordable final counteroffer");
                AssertSmokeTest(RevisePlayerDemand(counterDeal, new RewardDemand { Silver = counterDemand }) == null,
                    "a third player offer was incorrectly allowed");
                RejectCounterOffer(counterDeal);
                AssertSmokeTest(counterDeal.State == DealState.Rejected && GetActiveDeal(counterRecord.Pawn) == null,
                    "rejecting a counteroffer did not end negotiation");

                FactionNegotiationMemory recoveryMemory = new FactionNegotiationMemory
                {
                    Faction = faction,
                    DiplomaticReserve = 0f,
                    ReserveUpdatedTick = Find.TickManager.TicksGame
                };
                float recoveredReserve = GetAvailableReserve(
                    recoveryMemory,
                    Find.TickManager.TicksGame + NegotiationEconomyUtility.ReserveRecoveryDays * TicksPerDay);
                AssertSmokeTest(Math.Abs(recoveredReserve - NegotiationEconomyUtility.CalculateMaximumReserve(faction)) < 1f,
                    "diplomatic reserve did not recover over 45 days");

                FactionNegotiationMemory impatientMemory = new FactionNegotiationMemory { Faction = faction };
                NegotiationResult absurdResult = new NegotiationResult
                {
                    RequestedRewards = new RewardDemand { Silver = 5000 },
                    DemandCost = 5000,
                    NegotiationBudget = 1000,
                    MaterialRewardCap = 2000
                };
                ApplyRejectedNegotiation(impatientMemory, absurdResult);
                ApplyRejectedNegotiation(impatientMemory, absurdResult);
                AssertSmokeTest(impatientMemory.Impatience == 2
                    && impatientMemory.NegotiationSuspendedUntilTick > Find.TickManager.TicksGame,
                    "repeated absurd demands did not suspend negotiation");

                ThingDef supplyDef = SupplyRewardUtility.AvailableSupplies(faction).FirstOrDefault();
                AssertSmokeTest(supplyDef != null, "no safe supply reward was available");
                FactionNegotiationMemory supplyMemory = GetFactionMemory(faction, true);
                supplyMemory.DiplomaticReserve = NegotiationEconomyUtility.CalculateMaximumReserve(faction);
                supplyMemory.ReserveUpdatedTick = Find.TickManager.TicksGame;
                int supplyBefore = CountThingOnMap(map, supplyDef);
                float reserveBeforeSupply = supplyMemory.DiplomaticReserve;
                PrisonerDeal supplyDeal = CreateSmokeTestDeal(map, faction, pawnKind, false);
                supplyDeal.Rewards = new RewardDemand { SupplyDef = supplyDef, SupplyCount = 7 };
                supplyDeal.SilverAmount = 0;
                supplyDeal.PrisonerDelivered = true;
                supplyDeal.State = DealState.FulfillmentPending;
                FulfillDeal(supplyDeal);
                AssertSmokeTest(supplyDeal.State == DealState.Completed
                    && supplyDeal.SupplyRewardIssued
                    && CountThingOnMap(map, supplyDef) - supplyBefore == 7,
                    "supply reward was not physically delivered");
                int supplyAfter = CountThingOnMap(map, supplyDef);
                FulfillDeal(supplyDeal);
                AssertSmokeTest(CountThingOnMap(map, supplyDef) == supplyAfter,
                    "completed supply reward was delivered twice");
                AssertSmokeTest(supplyMemory.DiplomaticReserve < reserveBeforeSupply
                    && supplyMemory.SuccessfulDeals > 0
                    && !string.IsNullOrEmpty(supplyMemory.LastDealSummary),
                    "completed reward did not consume reserve or update history");

                PrisonerDiplomacyExtensionCatalog.DebugSpecialRewardsEnabled = true;
                PrisonerDeal specialDeal = CreateSmokeTestDeal(map, faction, pawnKind, false);
                List<PrisonerDiplomacySpecialRewardDefinition> specialOptions =
                    PrisonerDiplomacyExtensionRegistry.GetSpecialRewards(
                        specialDeal.Prisoner,
                        faction).ToList();
                PrisonerDiplomacySpecialRewardDefinition smokeReward = specialOptions
                    .FirstOrDefault(option => option.RewardId == "sample.energy_core");
                RewardDemand specialDemand = new RewardDemand();
                AssertSmokeTest(smokeReward != null
                    && PrisonerDiplomacySpecialRewardUtility.TryPopulateDemand(
                        specialDeal.Prisoner,
                        faction,
                        smokeReward.RewardId,
                        specialDemand,
                        out _)
                    && NegotiationEconomyUtility.IsDemandValid(
                        faction,
                        specialDemand,
                        out _,
                        specialDeal.Prisoner)
                    && NegotiationEconomyUtility.CalculateDemandCost(faction, specialDemand) > 0,
                    "special reward adapter did not produce a validated costed demand");
                int specialBefore = CountThingOnMap(map, specialDemand.SpecialRewardThingDef);
                specialDeal.Rewards = specialDemand;
                specialDeal.SilverAmount = 0;
                specialDeal.PrisonerDelivered = true;
                specialDeal.State = DealState.FulfillmentPending;
                FulfillDeal(specialDeal);
                AssertSmokeTest(specialDeal.State == DealState.Completed
                    && specialDeal.SpecialRewardIssued
                    && CountThingOnMap(map, specialDemand.SpecialRewardThingDef) - specialBefore
                        == specialDemand.SpecialRewardCount,
                    "special reward was not physically delivered by the core");
                int specialAfter = CountThingOnMap(map, specialDemand.SpecialRewardThingDef);
                FulfillDeal(specialDeal);
                AssertSmokeTest(CountThingOnMap(map, specialDemand.SpecialRewardThingDef) == specialAfter,
                    "completed special reward was delivered twice");
                PrisonerDiplomacyExtensionCatalog.DebugSpecialRewardsEnabled = false;

                Faction goodwillFaction = Find.FactionManager.AllFactionsVisible
                    .Where(PrisonerEligibilityUtility.IsNegotiatingFaction)
                    .FirstOrDefault(candidate => NegotiationEconomyUtility.CanRequestGoodwill(candidate)
                        && candidate.PlayerGoodwill < 100);
                if (goodwillFaction != null)
                {
                    PawnKindDef goodwillPawnKind = goodwillFaction.def.basicMemberKind ?? PawnKindDefOf.SpaceRefugee;
                    FactionNegotiationMemory goodwillMemory = GetFactionMemory(goodwillFaction, true);
                    goodwillMemory.DiplomaticReserve = NegotiationEconomyUtility.CalculateMaximumReserve(goodwillFaction);
                    goodwillMemory.ReserveUpdatedTick = Find.TickManager.TicksGame;
                    int goodwillBefore = goodwillFaction.PlayerGoodwill;
                    ThingDef goodwillSupply = SupplyRewardUtility.AvailableSupplies(goodwillFaction).First();
                    int goodwillSupplyBefore = CountThingOnMap(map, goodwillSupply);
                    PrisonerDeal mixedDeal = CreateSmokeTestDeal(map, goodwillFaction, goodwillPawnKind, false);
                    mixedDeal.Rewards = new RewardDemand { SupplyDef = goodwillSupply, SupplyCount = 3, Goodwill = 1 };
                    mixedDeal.SilverAmount = 0;
                    mixedDeal.PrisonerDelivered = true;
                    mixedDeal.State = DealState.FulfillmentPending;
                    FulfillDeal(mixedDeal);
                    AssertSmokeTest(mixedDeal.State == DealState.Completed
                        && mixedDeal.SupplyRewardIssued
                        && mixedDeal.GoodwillRewardIssued
                        && CountThingOnMap(map, goodwillSupply) - goodwillSupplyBefore == 3
                        && goodwillFaction.PlayerGoodwill == goodwillBefore + 1,
                        "mixed supply and goodwill rewards were not fulfilled");
                }

                PrisonerRecord thresholdRecord = new PrisonerRecord
                {
                    PawnLoadId = "PD-Smoke-Negotiation-Thresholds",
                    OriginalFaction = faction,
                    DiplomaticValue = 1000,
                    CapturedTick = Find.TickManager.TicksGame
                };
                NegotiationResult cappedResult = PrisonerNegotiationUtility.Evaluate(
                    thresholdRecord,
                    negotiator,
                    new RewardDemand { Silver = 550 },
                    10000,
                    500,
                    1);
                AssertSmokeTest(cappedResult.Outcome == NegotiationOutcome.Countered
                    && cappedResult.CounterOffer != null
                    && NegotiationEconomyUtility.CalculateMaterialCost(faction, cappedResult.CounterOffer) <= 500,
                    "material reward slightly above the colony cap did not produce a capped counteroffer");

                int thresholdBudget = NegotiationEconomyUtility.CalculateNegotiationBudget(thresholdRecord, negotiator);
                int moderateDemand = (int)Math.Ceiling(thresholdBudget * 1.60f / 50f) * 50;
                NegotiationResult moderateResult = PrisonerNegotiationUtility.Evaluate(
                    thresholdRecord,
                    negotiator,
                    new RewardDemand { Silver = moderateDemand },
                    10000,
                    12000,
                    1);
                AssertSmokeTest(moderateResult.Outcome == NegotiationOutcome.Countered,
                    "a moderate demand above the old rejection threshold did not receive a counteroffer");

                int absurdDemand = (int)Math.Ceiling(thresholdBudget * 1.95f / 50f) * 50;
                NegotiationResult absurdThresholdResult = PrisonerNegotiationUtility.Evaluate(
                    thresholdRecord,
                    negotiator,
                    new RewardDemand { Silver = absurdDemand },
                    10000,
                    12000,
                    1);
                AssertSmokeTest(absurdThresholdResult.Outcome == NegotiationOutcome.Rejected,
                    "a demand beyond the maximum counterable ratio was not rejected");

                ResetSmokeTestFactionMemory(faction);
                Pawn exchangeHostage = PrisonerExchangeUtility.CreateSmokeTestHostage(faction, PawnKindDefOf.SpaceRefugee);
                AssertSmokeTest(PrisonerExchangeUtility.IsHeldByFaction(faction, exchangeHostage)
                    && GetAvailableHostages(faction).Contains(exchangeHostage),
                    "kidnapped colonist tracker did not expose the exchange hostage"
                        + " trackerCount=" + (faction.kidnapped?.KidnappedPawnsListForReading.Count ?? -1)
                        + " inTracker=" + (faction.kidnapped?.KidnappedPawnsListForReading.Contains(exchangeHostage) ?? false)
                        + " faction=" + (exchangeHostage.Faction?.Name ?? "null")
                        + " homeFaction=" + (exchangeHostage.HomeFaction?.Name ?? "null")
                        + " spawned=" + exchangeHostage.Spawned
                        + " destroyed=" + exchangeHostage.Destroyed
                        + " world=" + Find.WorldPawns.Contains(exchangeHostage));
                PrisonerRecord exchangeRecord = CreateSmokeTestRecord(map, faction, pawnKind);
                int exchangeHostageCost = PrisonerExchangeUtility.CalculateHostageCost(exchangeHostage);
                int exchangeCompensation = PrisonerExchangeUtility.CalculateCompensation(exchangeRecord, exchangeHostage);
                AssertSmokeTest(exchangeHostageCost > 0
                    && exchangeCompensation == Math.Max(0, exchangeHostageCost - exchangeRecord.DiplomaticValue),
                    "exchange value gap was calculated incorrectly");
                if (exchangeCompensation > 0)
                {
                    SpawnSmokeTestThings(map, ThingDefOf.Silver, exchangeCompensation);
                }

                int silverBeforeExchange = PrisonerExchangeUtility.CountAvailableThings(map, ThingDefOf.Silver);
                AssertSmokeTest(TryCreatePrisonerExchange(
                    exchangeRecord,
                    negotiator,
                    exchangeHostage,
                    null,
                    out string exchangeReason),
                    "could not create exchange deal: " + exchangeReason);
                PrisonerDeal exchangeDeal = GetActiveDeal(exchangeRecord.Pawn);
                AssertSmokeTest(exchangeDeal != null
                    && exchangeDeal.ReturnedHostage == exchangeHostage
                    && exchangeDeal.PlayerCompensationSilver == exchangeCompensation
                    && PrisonerExchangeUtility.CountAvailableThings(map, ThingDefOf.Silver)
                        == silverBeforeExchange - exchangeCompensation,
                    "exchange did not persist or charge its exact value gap");
                AssertSmokeTest(OrderRansomRelease(exchangeRecord.Pawn),
                    "could not order prisoner exchange release");
                GenGuest.PrisonerRelease(exchangeRecord.Pawn);
                exchangeRecord.Pawn.ExitMap(false, Rot4.North);
                AssertSmokeTest(exchangeDeal.State == DealState.Completed
                    && exchangeDeal.HostageReturned
                    && exchangeDeal.RewardIssued
                    && !PrisonerExchangeUtility.IsHeldByFaction(faction, exchangeHostage)
                    && exchangeHostage.ParentHolder != null,
                    "exchange did not return exactly one tracked colonist after safe release");
                FulfillDeal(exchangeDeal);
                AssertSmokeTest(exchangeDeal.State == DealState.Completed
                    && exchangeDeal.HostageReturned,
                    "completed exchange changed after duplicate fulfillment");

                ResetSmokeTestFactionMemory(faction);
                Pawn refundHostage = PrisonerExchangeUtility.CreateSmokeTestHostage(faction, PawnKindDefOf.SpaceRefugee);
                PrisonerRecord refundRecord = CreateSmokeTestRecord(map, faction, pawnKind);
                int refundCompensation = PrisonerExchangeUtility.CalculateCompensation(refundRecord, refundHostage);
                if (refundCompensation > 0)
                {
                    SpawnSmokeTestThings(map, ThingDefOf.Silver, refundCompensation);
                }

                int silverBeforeRefundDeal = CountSilverOnMap(map);
                AssertSmokeTest(TryCreatePrisonerExchange(
                    refundRecord,
                    negotiator,
                    refundHostage,
                    null,
                    out string refundReason),
                    "could not create refundable exchange: " + refundReason);
                PrisonerDeal refundDeal = GetActiveDeal(refundRecord.Pawn);
                AssertSmokeTest(refundDeal != null && refundDeal.PlayerCompensationSilver == refundCompensation,
                    "refundable exchange did not persist compensation");
                int silverAfterRefundCharge = CountSilverOnMap(map);
                FailDeal(refundDeal, DealState.FailedPrisonerDead);
                AssertSmokeTest(refundDeal.State == DealState.FailedPrisonerDead
                    && !refundDeal.CompensationCharged
                    && CountSilverOnMap(map) - silverAfterRefundCharge == refundCompensation,
                    "failed exchange did not refund escrowed silver exactly once"
                        + " compensation=" + refundCompensation
                        + " beforeDeal=" + silverBeforeRefundDeal
                        + " afterCharge=" + silverAfterRefundCharge
                        + " afterRefund=" + CountSilverOnMap(map));
                faction.kidnapped.RemoveKidnappedPawn(refundHostage);
                if (Find.WorldPawns.Contains(refundHostage))
                {
                    Find.WorldPawns.RemoveAndDiscardPawnViaGC(refundHostage);
                }

                ResetSmokeTestFactionMemory(faction);
                Pawn supplyHostage = PrisonerExchangeUtility.CreateSmokeTestHostage(faction, PawnKindDefOf.SpaceRefugee);
                PrisonerRecord supplyExchangeRecord = CreateSmokeTestRecord(map, faction, pawnKind);
                ThingDef supplyProbe = SupplyRewardUtility.AvailableSupplies(faction)
                    .OrderByDescending(def => def.BaseMarketValue)
                    .FirstOrDefault();
                AssertSmokeTest(supplyProbe != null, "no faction supply was available for exchange testing");
                int supplyHostageCost = PrisonerExchangeUtility.CalculateHostageCost(supplyHostage);
                int targetSupplyGap = Math.Min(
                    supplyHostageCost,
                    Math.Max(1, SupplyRewardUtility.CalculateCost(faction, supplyProbe, 10)));
                supplyExchangeRecord.DiplomaticValue = Math.Max(0, supplyHostageCost - targetSupplyGap);
                int supplyGap = PrisonerExchangeUtility.CalculateCompensation(supplyExchangeRecord, supplyHostage);
                ThingDef exchangeSupply = PrisonerExchangeUtility.AvailableCompensationSupplies(
                    faction,
                    supplyGap).FirstOrDefault();
                AssertSmokeTest(exchangeSupply != null, "no exchange compensation supply was available");
                int supplyPaymentCount = PrisonerExchangeUtility.CalculateSupplyCount(faction, exchangeSupply, supplyGap);
                if (supplyPaymentCount > 0)
                {
                    SpawnSmokeTestThings(map, exchangeSupply, supplyPaymentCount);
                }

                int suppliesBeforeExchange = PrisonerExchangeUtility.CountAvailableThings(map, exchangeSupply);
                AssertSmokeTest(TryCreatePrisonerExchange(
                    supplyExchangeRecord,
                    negotiator,
                    supplyHostage,
                    exchangeSupply,
                    out string supplyExchangeReason,
                    refreshRecordValue: false),
                    "could not create supply-funded exchange: " + supplyExchangeReason);
                PrisonerDeal supplyExchangeDeal = GetActiveDeal(supplyExchangeRecord.Pawn);
                AssertSmokeTest(supplyExchangeDeal != null
                    && supplyExchangeDeal.PlayerCompensationThingDef == exchangeSupply
                    && supplyExchangeDeal.PlayerCompensationThingCount == supplyPaymentCount
                    && PrisonerExchangeUtility.CountAvailableThings(map, exchangeSupply)
                        == suppliesBeforeExchange - supplyPaymentCount,
                    "supply-funded exchange did not charge the exact compensation");
                FailDeal(supplyExchangeDeal, DealState.Cancelled);
                AssertSmokeTest(!supplyExchangeDeal.CompensationCharged
                    && CountThingOnMap(map, exchangeSupply) >= suppliesBeforeExchange,
                    "failed supply-funded exchange did not refund compensation");
                faction.kidnapped.RemoveKidnappedPawn(supplyHostage);
                if (Find.WorldPawns.Contains(supplyHostage))
                {
                    Find.WorldPawns.RemoveAndDiscardPawnViaGC(supplyHostage);
                }

                PrisonerDeal legacyDeal = new PrisonerDeal
                {
                    DealId = "PD-LEGACY-REWARD",
                    SilverAmount = 650,
                    State = DealState.Rejected,
                    CompletedTick = Find.TickManager.TicksGame
                };
                deals.Add(legacyDeal);
                RepairLoadedData();
                AssertSmokeTest(legacyDeal.Rewards != null && legacyDeal.Rewards.Silver == 650,
                    "legacy silver deal did not migrate to reward data");
                deals.Remove(legacyDeal);

                int scheduleAnchor = Find.TickManager.TicksGame;
                int regularDelayA = CalculateFactionContactDelayTicks("SmokeSchedulePawn", scheduleAnchor, PrisonerImportance.Regular);
                int regularDelayB = CalculateFactionContactDelayTicks("SmokeSchedulePawn", scheduleAnchor, PrisonerImportance.Regular);
                int leaderDelay = CalculateFactionContactDelayTicks("SmokeSchedulePawn", scheduleAnchor, PrisonerImportance.Leader);
                AssertSmokeTest(regularDelayA == regularDelayB,
                    "faction contact delay was not deterministic");
                AssertSmokeTest(regularDelayA >= MinimumFactionContactDelayTicks
                    && regularDelayA <= MaximumFactionContactDelayTicks,
                    "regular faction contact delay fell outside 3-7 days: " + regularDelayA);
                AssertSmokeTest(leaderDelay <= regularDelayA,
                    "important prisoner contact was later than comparable regular prisoner");

                PrisonerRecord scheduledRecord = CreateSmokeTestRecord(map, faction, pawnKind);
                scheduledRecord.ScheduledFactionOfferTick = scheduleAnchor;
                scheduledRecord.LastProposalTick = -1;
                AssertSmokeTest(!ShouldCreateOffer(scheduledRecord, scheduleAnchor - 1),
                    "faction offer became eligible before its scheduled tick");
                PrisonerDeal scheduledOffer = ShouldCreateOffer(scheduledRecord, scheduleAnchor)
                    ? CreateOffer(scheduledRecord, false)
                    : null;
                AssertSmokeTest(scheduledOffer != null && scheduledOffer.Origin == DealOrigin.FactionOffer,
                    "faction offer was not created at its scheduled tick");
                AssertSmokeTest(!ShouldCreateOffer(scheduledRecord, scheduleAnchor),
                    "active faction offer did not suppress a duplicate offer");
                RejectDeal(scheduledOffer.DealId);
                AssertSmokeTest(scheduledRecord.UnsolicitedOfferSuppressedUntilTick
                    >= scheduleAnchor + RejectedOfferCooldownTicks
                    && !ShouldCreateOffer(scheduledRecord,
                        scheduleAnchor + RejectedOfferCooldownTicks - 1),
                    "rejecting a faction offer did not apply the per-prisoner unsolicited-offer cooldown");
                CaptureTerminalDealHistory();
                AssertSmokeTest(dealHistory.Any(entry => entry?.DealId == scheduledOffer.DealId
                    && entry.State == DealState.Rejected),
                    "rejected faction offer was not captured in permanent transaction history");

                PrisonerRecord legacyRecord = new PrisonerRecord
                {
                    PawnLoadId = "LegacySchedulePawn",
                    OriginalFaction = faction,
                    CapturedTick = scheduleAnchor - TicksPerDay,
                    Importance = PrisonerImportance.Notable,
                    ScheduledFactionOfferTick = -1
                };
                EnsureFactionOfferSchedule(legacyRecord);
                int migratedDelay = legacyRecord.ScheduledFactionOfferTick - legacyRecord.CapturedTick;
                AssertSmokeTest(migratedDelay >= MinimumFactionContactDelayTicks
                    && migratedDelay <= MaximumFactionContactDelayTicks,
                    "legacy record migration did not create a valid 3-7 day schedule");

                ResetSmokeTestFactionMemory(faction);
                FactionNegotiationMemory neutralMemory = GetFactionMemory(faction, true);
                float neutralMultiplier = FactionPrisonerMemoryUtility.CalculateMultiplier(faction, neutralMemory);
                AssertSmokeTest(Math.Abs(neutralMultiplier - 1f) < 0.0001f,
                    "neutral faction memory multiplier was not 1: " + neutralMultiplier);

                neutralMemory.Reliability = 50f;
                neutralMemory.Treatment = 40f;
                neutralMemory.Resentment = 0f;
                float positiveMultiplier = FactionPrisonerMemoryUtility.CalculateMultiplier(faction, neutralMemory);
                AssertSmokeTest(positiveMultiplier > neutralMultiplier,
                    "positive faction memory did not improve willingness");

                neutralMemory.Reliability = -50f;
                neutralMemory.Treatment = -40f;
                neutralMemory.Resentment = 60f;
                float negativeMultiplier = FactionPrisonerMemoryUtility.CalculateMultiplier(faction, neutralMemory);
                AssertSmokeTest(negativeMultiplier < neutralMultiplier,
                    "negative faction memory did not reduce willingness");

                PrisonerRecord memoryBudgetRecord = CreateSmokeTestRecord(map, faction, pawnKind);
                int positiveMemoryBudget = NegotiationEconomyUtility.CalculateNegotiationBudget(
                    memoryBudgetRecord, negotiator, positiveMultiplier);
                int negativeMemoryBudget = NegotiationEconomyUtility.CalculateNegotiationBudget(
                    memoryBudgetRecord, negotiator, negativeMultiplier);
                AssertSmokeTest(positiveMemoryBudget > negativeMemoryBudget,
                    "positive memory did not produce a higher negotiation budget"
                        + " positive=" + positiveMemoryBudget + " negative=" + negativeMemoryBudget);

                ResetSmokeTestFactionMemory(faction);
                FactionNegotiationMemory releaseMemory = GetFactionMemory(faction, true);
                releaseMemory.Resentment = 20f;
                PrisonerRecord releaseRecord = CreateSmokeTestRecord(map, faction, pawnKind);
                RecordUnconditionalRelease(releaseRecord.Pawn);
                AssertSmokeTest(releaseMemory.Reliability > 0f
                    && releaseMemory.Treatment > 0f
                    && releaseMemory.Resentment < 20f,
                    "unconditional release did not improve all intended memory dimensions");
                AssertSmokeTest(releaseMemory.RecentEvents.LastOrDefault()?.ReasonKey
                        == "PD_MemoryEventUnconditionalRelease"
                    || releaseMemory.RecentEvents.LastOrDefault()?.ReasonKey
                        == "PD_MemoryEventReleasedAfterTreatment",
                    "unconditional release was not retained in recent event history");

                ResetSmokeTestFactionMemory(faction);
                PrisonerDeal memorySuccessDeal = CreateSmokeTestDeal(map, faction, pawnKind, false);
                memorySuccessDeal.PrisonerDelivered = true;
                memorySuccessDeal.State = DealState.FulfillmentPending;
                FulfillDeal(memorySuccessDeal);
                FactionNegotiationMemory successfulMemory = GetFactionMemory(faction, true);
                AssertSmokeTest(memorySuccessDeal.State == DealState.Completed
                    && successfulMemory.Reliability >= 10f,
                    "completed agreement did not improve reliability");

                ResetSmokeTestFactionMemory(faction);
                PrisonerDeal memoryBreachDeal = CreateSmokeTestDeal(map, faction, pawnKind, false);
                FailDeal(memoryBreachDeal, DealState.FailedEnslaved, true);
                FactionNegotiationMemory breachedMemory = GetFactionMemory(faction, true);
                AssertSmokeTest(breachedMemory.Reliability <= -20f
                    && breachedMemory.Treatment <= -10f
                    && breachedMemory.Resentment >= 30f,
                    "accepted-agreement breach did not sharply worsen faction memory");

                ResetSmokeTestFactionMemory(faction);
                FactionNegotiationMemory insultedMemory = GetFactionMemory(faction, true);
                ApplyRejectedNegotiation(insultedMemory, new NegotiationResult
                {
                    DemandCost = 2000,
                    NegotiationBudget = 500
                });
                AssertSmokeTest(insultedMemory.Resentment > 0f
                    && insultedMemory.Reliability < 0f
                    && insultedMemory.RecentEvents.Any(item => item.ReasonKey == "PD_MemoryEventAbsurdDemand"),
                    "absurd demand did not create resentment and a recorded cause");

                int decayNow = FactionPrisonerMemoryUtility.MemoryYearTicks * 2;
                FactionNegotiationMemory decayingMemory = new FactionNegotiationMemory
                {
                    Faction = faction,
                    Reliability = 20f,
                    Treatment = 40f,
                    Resentment = 50f,
                    ResentmentFloor = 15f,
                    MemoryUpdatedTick = decayNow - FactionPrisonerMemoryUtility.MemoryYearTicks
                };
                FactionPrisonerMemoryUtility.ApplyDecay(decayingMemory, decayNow);
                AssertSmokeTest(decayingMemory.Reliability < 20f
                    && decayingMemory.Treatment < 40f
                    && decayingMemory.Resentment < 50f
                    && decayingMemory.Resentment >= decayingMemory.ResentmentFloor,
                    "faction memory did not decay toward neutral while respecting its resentment floor");

                ResetSmokeTestFactionMemory(faction);
                FactionNegotiationMemory hiddenMemory = GetFactionMemory(faction, true);
                hiddenMemory.Reliability = 37f;
                hiddenMemory.Treatment = 26f;
                hiddenMemory.Resentment = 42f;
                string hiddenDescription = GetFactionMemoryPageText(faction);
                AssertSmokeTest(!hiddenDescription.Contains("37")
                    && !hiddenDescription.Contains("26")
                    && !hiddenDescription.Contains("42"),
                    "normal faction memory UI exposed exact hidden values: " + hiddenDescription);

                AssertSmokeTest(PrisonerDiplomacyBackendApi.ApiVersion == "1.2.0"
                    && PrisonerDiplomacyBackendApi.GetRegisteredExtensionIds().Contains(
                        "g1061.prisonerdiplomacy.core")
                    && PrisonerDiplomacyBackendApi.GetRegisteredExtensionIds().Contains(
                        "g1061.prisonerdiplomacy.sample"),
                    "versioned extension registry did not expose the built-in and sample add-ons");
                AssertSmokeTest(PrisonerDiplomacyBackendApi.GetEventDefinitions().Count(definition =>
                        definition != null && definition.EventId.StartsWith("core.", StringComparison.Ordinal)) == 4,
                    "core extension event definitions were not registered");
                AssertSmokeTest(TryScheduleExtensionEvent(
                        "core.neutral_trade",
                        PrisonerDiplomacyEventKind.NeutralTradeCaravan,
                        faction,
                        map,
                        memoryBudgetRecord.Pawn,
                        "PD-API-SMOKE",
                        Find.TickManager.TicksGame + TicksPerDay),
                    "extension event registry rejected a valid deterministic event");
                PrisonerDiplomacyEventRecord apiEvent = diplomacyEvents.LastOrDefault();
                AssertSmokeTest(apiEvent != null
                    && PrisonerDiplomacyBackendApi.GetEventSnapshots().Any(snapshot =>
                        snapshot.EventId == apiEvent.EventId
                        && snapshot.DefinitionId == "core.neutral_trade"),
                    "extension event snapshot API did not expose its persisted record");
                diplomacyEvents.Remove(apiEvent);

                ResetSmokeTestFactionMemory(faction);
                FactionNegotiationMemory migratedMemory = GetFactionMemory(faction, true);
                migratedMemory.MemoryUpdatedTick = -1;
                PrisonerRecord memoryLegacyRecord = new PrisonerRecord
                {
                    Pawn = memoryBudgetRecord.Pawn,
                    PawnLoadId = "PD-Smoke-Legacy-Memory",
                    OriginalFaction = faction,
                    CapturedHealthPercent = 0f,
                    LastTreatmentCheckTick = -1
                };
                records.Add(memoryLegacyRecord);
                saveVersion = 6;
                RepairLoadedData();
                AssertSmokeTest(saveVersion == SaveVersion
                    && migratedMemory.MemoryUpdatedTick == Find.TickManager.TicksGame
                    && Math.Abs(migratedMemory.Reliability) < 0.0001f
                    && Math.Abs(migratedMemory.Treatment) < 0.0001f
                    && Math.Abs(migratedMemory.Resentment) < 0.0001f
                    && memoryLegacyRecord.CapturedHealthPercent > 0f
                    && memoryLegacyRecord.LastTreatmentCheckTick == Find.TickManager.TicksGame,
                    "legacy save migration did not initialize memory and treatment baselines neutrally");
                records.Remove(memoryLegacyRecord);

                AssertSmokeTest(AiNarrativeService.TryRunSelfTest(out string aiSelfTestFailure),
                    "AI narrative provider validation failed: " + aiSelfTestFailure);
                AssertSmokeTest(ErrorTelemetryService.TryRunSelfTest(out string telemetrySelfTestFailure),
                    "error telemetry contract failed: " + telemetrySelfTestFailure);

                AiNarrativeContextUtility.EnsurePersona(migratedMemory, faction);
                string stablePersona = migratedMemory.AiPersonaSummary;
                migratedMemory.Reliability = 75f;
                AiNarrativeContextUtility.EnsurePersona(migratedMemory, faction);
                AssertSmokeTest(!string.IsNullOrWhiteSpace(stablePersona)
                    && migratedMemory.AiPersonaSummary == stablePersona,
                    "AI faction persona was not persistent and deterministic");

                AiNarrativeRecord interruptedNarrative = new AiNarrativeRecord
                {
                    ContextId = "PD-AI-SMOKE-INTERRUPTED",
                    RequestId = "PD-AI-SMOKE-REQUEST",
                    Prisoner = memoryBudgetRecord.Pawn,
                    PrisonerLoadId = memoryBudgetRecord.PawnLoadId,
                    Faction = faction,
                    Status = AiNarrativeStatus.Waiting,
                    FallbackText = "fallback",
                    CreatedTick = Find.TickManager.TicksGame
                };
                AiNarrativeRecord persistedNarrative = new AiNarrativeRecord
                {
                    ContextId = "PD-AI-SMOKE-PERSISTED",
                    RequestId = "PD-AI-SMOKE-PERSISTED-REQUEST",
                    Prisoner = memoryBudgetRecord.Pawn,
                    PrisonerLoadId = memoryBudgetRecord.PawnLoadId,
                    Faction = faction,
                    Status = AiNarrativeStatus.Generated,
                    GeneratedText = "persisted narrative",
                    FallbackText = "fallback",
                    CreatedTick = Find.TickManager.TicksGame
                };
                AiNarrativeRecord reversedPaymentNarrative = new AiNarrativeRecord
                {
                    ContextId = "PD-AI-SMOKE-REVERSED-PAYMENT",
                    RequestId = "PD-AI-SMOKE-REVERSED-PAYMENT-REQUEST",
                    Prisoner = memoryBudgetRecord.Pawn,
                    PrisonerLoadId = memoryBudgetRecord.PawnLoadId,
                    Faction = faction,
                    EventKind = AiNarrativeEventKind.PlayerDemandCountered,
                    Status = AiNarrativeStatus.Generated,
                    GeneratedText = "只要你願意向我們支付贖金，我們就接受。",
                    FallbackText = "fallback",
                    CreatedTick = Find.TickManager.TicksGame
                };
                aiNarratives.Add(interruptedNarrative);
                aiNarratives.Add(persistedNarrative);
                aiNarratives.Add(reversedPaymentNarrative);
                saveVersion = 10;
                RepairLoadedData();
                AssertSmokeTest(saveVersion == SaveVersion
                    && interruptedNarrative.Status == AiNarrativeStatus.Fallback
                    && interruptedNarrative.FailureCode == "load_interrupted",
                    "pending AI narrative did not fall back safely during load migration");
                AssertSmokeTest(persistedNarrative.Status == AiNarrativeStatus.Generated
                    && persistedNarrative.GeneratedText == "persisted narrative",
                    "adopted AI narrative was not preserved by load repair");
                AssertSmokeTest(reversedPaymentNarrative.Status == AiNarrativeStatus.Fallback
                    && reversedPaymentNarrative.GeneratedText == null
                    && reversedPaymentNarrative.FailureCode == "persisted_transaction_direction_mismatch",
                    "persisted reversed-payment AI narrative was not repaired safely");

                AiNarrativeRecord contextNarrative = new AiNarrativeRecord
                {
                    ContextId = "PD-AI-SMOKE-CONTEXT",
                    RequestId = "PD-AI-SMOKE-CONTEXT-REQUEST",
                    Prisoner = memoryBudgetRecord.Pawn,
                    PrisonerLoadId = memoryBudgetRecord.Pawn.GetUniqueLoadID(),
                    Faction = faction,
                    ExpectedNegotiationCount = memoryBudgetRecord.NegotiationCount
                };
                AssertSmokeTest(IsAiNarrativeContextCurrent(contextNarrative),
                    "current AI narrative context was rejected");
                memoryBudgetRecord.NegotiationCount++;
                AssertSmokeTest(!IsAiNarrativeContextCurrent(contextNarrative),
                    "stale AI narrative context was accepted after candidate version changed");
                memoryBudgetRecord.NegotiationCount--;
                aiNarratives.Remove(interruptedNarrative);
                aiNarratives.Remove(persistedNarrative);
                aiNarratives.Remove(reversedPaymentNarrative);

                RunCompatibilitySmokeTests(map, faction, pawnKind, memoryBudgetRecord);
                string customRaceSmoke = RunCustomRaceDebugSmokeTests(map);

                if (LanguageDatabase.activeLanguage?.loadErrors != null)
                {
                    foreach (string languageError in LanguageDatabase.activeLanguage.loadErrors)
                    {
                        Log.Message("[Prisoner Diplomacy SmokeTest] Language load error: " + languageError);
                    }
                }

                if (RimChatIntegration.IsInstalled && RimChatHarmonyPatches.IsInstalled)
                {
                    AssertSmokeTest(RimChatHarmonyPatches.TryRunSmokeTest(out string rimChatGuardFailure),
                        "RimChat ransom guard smoke test failed: " + rimChatGuardFailure);
                }

                Log.Message("[Prisoner Diplomacy SmokeTest] PASS cases=127 successfulDeal="
                    + success.DealId + " silver=" + success.SilverAmount
                    + " physicalSilverDelta=" + (silverAfterSuccess - silverBeforeSuccess)
                    + " playerDemand=" + acceptedDemand
                    + " customRaces=" + customRaceSmoke);
            }
            catch (Exception exception)
            {
                Log.Error("[Prisoner Diplomacy SmokeTest] FAIL exception: " + exception);
            }
        }

        private void RunCompatibilitySmokeTests(
            Map map,
            Faction faction,
            PawnKindDef pawnKind,
            PrisonerRecord referenceRecord)
        {
            int recordsBefore = records.Count;
            PrisonerRecord duplicateRecord = new PrisonerRecord
            {
                Pawn = referenceRecord.Pawn,
                PawnLoadId = referenceRecord.PawnLoadId,
                OriginalFaction = faction,
                CapturedTick = referenceRecord.CapturedTick,
                CapturedMarketValue = 0f,
                DiplomaticValue = 0,
                LastTreatmentCheckTick = -1,
                LastPlayerTreatmentTick = -1
            };
            records.Add(duplicateRecord);

            string mapLoadId = map.GetUniqueLoadID();
            PrisonerDeal malformedA = new PrisonerDeal
            {
                DealId = "PD-080000",
                Faction = faction,
                MapLoadId = mapLoadId,
                State = DealState.Completed,
                Rewards = new RewardDemand
                {
                    Silver = -50,
                    SupplyDef = ThingDefOf.Silver,
                    SupplyCount = -3,
                    Goodwill = -10,
                    CeasefireDays = 90
                }
            };
            PrisonerDeal malformedB = new PrisonerDeal
            {
                DealId = malformedA.DealId,
                Faction = faction,
                MapLoadId = mapLoadId,
                State = DealState.Completed,
                Rewards = new RewardDemand { Silver = 100 }
            };
            PrisonerDeal conflictA = new PrisonerDeal
            {
                DealId = "PD-080001",
                PrisonerLoadId = "PD-Compatibility-Conflict",
                Faction = faction,
                Map = map,
                MapLoadId = mapLoadId,
                State = DealState.AcceptedAwaitingRelease,
                AcceptedTick = Find.TickManager.TicksGame,
                FulfillmentExpiresTick = Find.TickManager.TicksGame + 60000,
                Rewards = new RewardDemand { Silver = 100 }
            };
            PrisonerDeal conflictB = new PrisonerDeal
            {
                DealId = "PD-080002",
                PrisonerLoadId = conflictA.PrisonerLoadId,
                Faction = faction,
                Map = map,
                MapLoadId = mapLoadId,
                State = DealState.AcceptedAwaitingRelease,
                AcceptedTick = Find.TickManager.TicksGame - 1,
                FulfillmentExpiresTick = Find.TickManager.TicksGame + 60000,
                Rewards = new RewardDemand { Silver = 100 }
            };
            deals.Add(malformedA);
            deals.Add(malformedB);
            deals.Add(conflictA);
            deals.Add(conflictB);

            CompatibilityRepairSummary summary = RepairCompatibilityData(false);
            CompatibilityRepairSummary idempotentSummary = RepairCompatibilityData(false);
            AssertSmokeTest(records.Count == recordsBefore,
                "compatibility repair did not merge duplicate prisoner records");
            AssertSmokeTest(malformedA.Map == map && malformedA.MapLoadId == mapLoadId,
                "compatibility repair did not restore the delivery map from its stable ID");
            AssertSmokeTest(malformedA.Rewards.Silver == 0
                && malformedA.Rewards.SupplyDef == null
                && malformedA.Rewards.SupplyCount == 0
                && malformedA.Rewards.CeasefireDays == 30,
                "compatibility repair did not normalize an invalid reward");
            AssertSmokeTest(malformedA.DealId != malformedB.DealId,
                "compatibility repair did not separate duplicate deal IDs");
            AssertSmokeTest(deals.Count(deal => deal.PrisonerLoadId == conflictA.PrisonerLoadId && deal.IsActive) == 1
                && deals.Count(deal => deal.PrisonerLoadId == conflictA.PrisonerLoadId && deal.State == DealState.Cancelled) == 1,
                "compatibility repair did not isolate duplicate active deals");
            AssertSmokeTest(ResolveDealMap(new PrisonerDeal { MapLoadId = mapLoadId }, false) == map,
                "multi-colony map resolver rejected a valid stable map ID");
            AssertSmokeTest(summary.Changed && summary.MergedRecords >= 1
                && summary.CancelledConflictingDeals == 1
                && summary.RepairedSequences >= 1
                && !idempotentSummary.Changed,
                "compatibility repair did not produce an actionable, idempotent repair summary");

            records.Remove(duplicateRecord);
            deals.Remove(malformedA);
            deals.Remove(malformedB);
            deals.Remove(conflictA);
            deals.Remove(conflictB);
        }

        private string RunCustomRaceDebugSmokeTests(Map map)
        {
            List<DetectedDebugRace> detected = AlienRaceDebugUtility.DetectCustomHumanlikeRaces();
            AssertSmokeTest(detected.All(entry => entry?.Race != null
                    && entry.Race != ThingDefOf.Human
                    && entry.Race.race?.Humanlike == true
                    && entry.PawnKinds.Count > 0),
                "custom-race debug catalog contained an invalid race or PawnKind");
            AssertSmokeTest(detected.Select(entry => entry.Race).Distinct().Count() == detected.Count,
                "custom-race debug catalog contained duplicate race entries");
            AssertSmokeTest(detected.All(entry => entry.PawnKinds.All(kind => kind?.race == entry.Race)),
                "custom-race debug catalog mixed PawnKinds from different races");

            ThingDef miliraRace = DefDatabase<ThingDef>.GetNamedSilentFail("Milira_Race");
            PawnKindDef miliraKind = DefDatabase<PawnKindDef>.GetNamedSilentFail("Milira_Colonist");
            if (miliraRace == null && miliraKind == null)
            {
                return detected.Count + ":milira-not-loaded";
            }

            AssertSmokeTest(miliraRace != null && miliraKind?.race == miliraRace,
                "Milira defs were only partially loaded or did not reference Milira_Race");
            DetectedDebugRace milira = detected.FirstOrDefault(entry => entry.Race == miliraRace);
            AssertSmokeTest(milira != null && milira.PawnKinds.Contains(miliraKind),
                "Milira_Race / Milira_Colonist was not present in the custom-race debug catalog");
            AssertSmokeTest(milira.PreferredPawnKind()?.defName == "Milira_Colonist",
                "Milira_Colonist was not selected as the preferred Milira PawnKind");

            Faction miliraFaction = AlienRaceDebugUtility.FindMatchingFaction(milira);
            AssertSmokeTest(miliraFaction != null
                    && AlienRaceDebugUtility.FactionSupportsRace(miliraFaction, miliraRace),
                "no active negotiating Milira faction matched Milira_Race");

            Pawn anchor = map?.mapPawns?.FreeColonistsSpawned?.FirstOrDefault();
            AssertSmokeTest(anchor != null, "no player colonist was available for the custom-race debug smoke test");
            Pawn prisoner = DebugSpawnTestPrisoner(anchor, miliraKind, miliraFaction, out string prisonerFailure);
            AssertSmokeTest(prisoner != null
                    && prisoner.def == miliraRace
                    && prisoner.kindDef == miliraKind
                    && prisoner.IsPrisonerOfColony,
                "Milira prisoner generation failed: " + prisonerFailure);
            PrisonerRecord record = GetRecord(prisoner);
            AssertSmokeTest(record?.OriginalFaction == miliraFaction,
                "Milira prisoner was not registered against the matching original faction");

            Pawn hostage = DebugGenerateTestHostage(prisoner, miliraKind, out string hostageFailure);
            AssertSmokeTest(hostage != null
                    && hostage.def == miliraRace
                    && (hostage.HomeFaction == Faction.OfPlayer || hostage.Faction == Faction.OfPlayer),
                "Milira kidnapped colonist generation failed: " + hostageFailure);
            AssertSmokeTest(miliraFaction.kidnapped?.KidnappedPawnsListForReading.Contains(hostage) == true,
                "Milira kidnapped colonist was not stored in the matching faction tracker");

            miliraFaction.kidnapped.RemoveKidnappedPawn(hostage);
            if (Find.WorldPawns.Contains(hostage))
            {
                Find.WorldPawns.RemoveAndDiscardPawnViaGC(hostage);
            }
            records.Remove(record);
            prisoner.Destroy(DestroyMode.Vanish);
            return detected.Count + ":milira-pass";
        }

        private PrisonerDeal CreateSmokeTestDeal(Map map, Faction faction, PawnKindDef pawnKind, bool orderRelease)
        {
            Pawn pawn = PawnGenerator.GeneratePawn(pawnKind, faction);
            pawn.inventory?.innerContainer.ClearAndDestroyContents();
            IntVec3 spawnCell = CellFinder.RandomSpawnCellForPawnNear(map.Center, map);
            GenSpawn.Spawn(pawn, spawnCell, map, WipeMode.Vanish);
            pawn.guest.SetGuestStatus(Faction.OfPlayer, GuestStatus.Prisoner);
            AssertSmokeTest(pawn.IsPrisonerOfColony, "generated pawn did not become a player prisoner");

            PrisonerDeal deal = ForceOffer(pawn);
            AssertSmokeTest(deal != null && AcceptDeal(deal.DealId), "could not create or accept deal");
            if (orderRelease)
            {
                AssertSmokeTest(OrderRansomRelease(pawn), "could not order ransom release");
            }

            return deal;
        }

        private PrisonerRecord CreateSmokeTestRecord(Map map, Faction faction, PawnKindDef pawnKind)
        {
            Pawn pawn = PawnGenerator.GeneratePawn(pawnKind, faction);
            pawn.inventory?.innerContainer.ClearAndDestroyContents();
            IntVec3 spawnCell = CellFinder.RandomSpawnCellForPawnNear(map.Center, map);
            GenSpawn.Spawn(pawn, spawnCell, map, WipeMode.Vanish);
            pawn.guest.SetGuestStatus(Faction.OfPlayer, GuestStatus.Prisoner);
            PrisonerRecord record = RegisterPawn(pawn);
            AssertSmokeTest(record != null, "could not register player-demand smoke-test prisoner");
            return record;
        }

        private void ResetSmokeTestFactionMemory(Faction faction)
        {
            FactionNegotiationMemory memory = GetFactionMemory(faction, true);
            memory.LastPlayerNegotiationTick = -1;
            memory.DiplomaticReserve = NegotiationEconomyUtility.CalculateMaximumReserve(faction);
            memory.ReserveUpdatedTick = Find.TickManager.TicksGame;
            memory.NegotiationSuspendedUntilTick = -1;
            memory.Impatience = 0;
            memory.Reliability = 0f;
            memory.Treatment = 0f;
            memory.Resentment = 0f;
            memory.ResentmentFloor = 0f;
            memory.MemoryUpdatedTick = Find.TickManager.TicksGame;
            memory.RecentEvents = new List<PrisonerMemoryEvent>();
        }

        private static int FindDemandForOutcome(PrisonerRecord record, Pawn negotiator, NegotiationOutcome expectedOutcome)
        {
            for (int demand = PrisonerNegotiationUtility.MinimumDemand;
                demand <= PrisonerNegotiationUtility.MaximumDemand;
                demand += 50)
            {
                if (PrisonerNegotiationUtility.Evaluate(record, negotiator, demand).Outcome == expectedOutcome)
                {
                    return demand;
                }
            }

            throw new InvalidOperationException("could not find deterministic demand outcome " + expectedOutcome);
        }

        private static void AssertSmokeFailure(PrisonerDeal deal, DealState expectedState, string caseName)
        {
            AssertSmokeTest(deal.State == expectedState && !deal.PrisonerDelivered && !deal.RewardIssued,
                caseName + " expected=" + expectedState
                + " actual=" + deal.State
                + " delivered=" + deal.PrisonerDelivered
                + " rewardIssued=" + deal.RewardIssued);
        }

        private static int CountSilverOnMap(Map map)
        {
            return CountThingOnMap(map, ThingDefOf.Silver);
        }

        private static int CountThingOnMap(Map map, ThingDef thingDef)
        {
            int spawned = map.listerThings.ThingsOfDef(thingDef).Sum(thing => thing.stackCount);
            int held = ThingOwnerUtility.GetAllThingsRecursively(map)
                .Where(thing => thing.def == thingDef)
                .Sum(thing => thing.stackCount);
            return spawned + held;
        }

        private static void SpawnSmokeTestThings(Map map, ThingDef thingDef, int amount)
        {
            if (map == null || thingDef == null || amount <= 0)
            {
                return;
            }

            int remaining = amount;
            int stackLimit = Math.Max(1, thingDef.stackLimit);
            while (remaining > 0)
            {
                Thing thing = ThingMaker.MakeThing(thingDef);
                thing.stackCount = Math.Min(remaining, stackLimit);
                if (!GenPlace.TryPlaceThing(thing, map.Center, map, ThingPlaceMode.Near))
                {
                    thing.Destroy();
                    break;
                }

                remaining -= thing.stackCount;
            }
        }

        private static void AddSmokeTestTendableInjury(Pawn pawn)
        {
            Hediff injury = HediffMaker.MakeHediff(HediffDefOf.Cut, pawn);
            injury.Severity = 1f;
            pawn.health.AddHediff(injury);
        }

        private static void AssertSmokeTest(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
