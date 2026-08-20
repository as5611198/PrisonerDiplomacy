using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace PrisonerDiplomacy
{
    public sealed partial class PrisonerDiplomacyGameComponent
    {
        private const int ExtensionEventRetryTicks = 15000;
        private const int NeutralExchangeStageTicks = 2500;
        private const int EventRetentionTicks = 60 * TicksPerDay;

        private enum DiplomacyEventExecutionResult
        {
            Completed,
            Deferred,
            Retry,
            TerminalFailure
        }

        public PrisonerDiplomacyEventRecord GetDiplomacyEvent(string eventId)
        {
            return diplomacyEvents.FirstOrDefault(item => item?.EventId == eventId);
        }

        public IReadOnlyList<PrisonerDiplomacyEventRecord> GetDiplomacyEvents()
        {
            return diplomacyEvents
                .Where(item => item != null)
                .OrderByDescending(item => item.CreatedTick)
                .ToList();
        }

        public bool AcceptDiplomacyEvent(string eventId)
        {
            PrisonerDiplomacyEventRecord eventRecord = GetDiplomacyEvent(eventId);
            if (eventRecord == null
                || eventRecord.State != PrisonerDiplomacyEventState.Offered
                || !ValidateDiplomacyEvent(eventRecord))
            {
                return false;
            }

            eventRecord.PlayerAccepted = true;
            eventRecord.State = PrisonerDiplomacyEventState.Active;
            eventRecord.TriggerTick = Find.TickManager.TicksGame;
            TryExecuteDiplomacyEvent(eventRecord, Find.TickManager.TicksGame);
            return true;
        }

        public bool RejectDiplomacyEvent(string eventId)
        {
            PrisonerDiplomacyEventRecord eventRecord = GetDiplomacyEvent(eventId);
            if (eventRecord == null || eventRecord.State != PrisonerDiplomacyEventState.Offered)
            {
                return false;
            }

            eventRecord.State = PrisonerDiplomacyEventState.Cancelled;
            eventRecord.OutcomeApplied = true;
            CleanupNeutralTradePoint(eventRecord);
            return true;
        }

        private void UpdateExtensionEvents(int now)
        {
            if (diplomacyEvents == null)
            {
                diplomacyEvents = new List<PrisonerDiplomacyEventRecord>();
            }

            foreach (PrisonerDiplomacyEventRecord eventRecord in diplomacyEvents
                .Where(item => item?.IsActive == true && item.TriggerTick <= now)
                .ToList())
            {
                try
                {
                    if (!ValidateDiplomacyEvent(eventRecord))
                    {
                        eventRecord.State = PrisonerDiplomacyEventState.Cancelled;
                        eventRecord.OutcomeApplied = true;
                        CleanupNeutralTradePoint(eventRecord);
                        continue;
                    }

                    if (eventRecord.State == PrisonerDiplomacyEventState.Scheduled
                        && EventRequiresChoice(eventRecord.Kind))
                    {
                        OfferDiplomacyEvent(eventRecord);
                        continue;
                    }

                    TryExecuteDiplomacyEvent(eventRecord, now);
                }
                catch (Exception exception)
                {
                    RetryDiplomacyEvent(eventRecord, now);
                    CompatibilityDiagnostics.LogErrorOnce(
                        "extension-event:" + (eventRecord.EventId ?? "missing"),
                        "Deferred a diplomacy extension event after an exception.",
                        exception);
                }
            }

            diplomacyEvents.RemoveAll(item => item != null
                && !item.IsActive
                && item.CreatedTick >= 0
                && now - item.CreatedTick > EventRetentionTicks);
        }

        private static bool EventRequiresChoice(PrisonerDiplomacyEventKind kind)
        {
            return kind == PrisonerDiplomacyEventKind.NeutralTradeCaravan
                || kind == PrisonerDiplomacyEventKind.PublicWarCrimeTrial;
        }

        private bool ValidateDiplomacyEvent(PrisonerDiplomacyEventRecord eventRecord)
        {
            if (eventRecord?.Faction == null || eventRecord.Faction.defeated)
            {
                return false;
            }

            if (eventRecord.Kind == PrisonerDiplomacyEventKind.RansomAmbushRetaliation)
            {
                return true;
            }

            if (eventRecord.Kind == PrisonerDiplomacyEventKind.NeutralTradeCaravan
                && eventRecord.Stage >= 2)
            {
                PrisonerDeal neutralDeal = GetDeal(eventRecord.SourceDealId);
                return neutralDeal != null
                    && (neutralDeal.IsActive || neutralDeal.State == DealState.Completed);
            }

            if (eventRecord.Kind == PrisonerDiplomacyEventKind.NeutralTradeCaravan
                && eventRecord.Stage == 1
                && eventRecord.WorldTradePointRequested)
            {
                PrisonerDeal worldDeal = GetDeal(eventRecord.SourceDealId);
                Pawn worldPrisoner = worldDeal?.Prisoner ?? eventRecord.Prisoner;
                return worldDeal != null
                    && worldDeal.IsActive
                    && eventRecord.NeutralTradePoint != null
                    && eventRecord.NeutralTradePoint.Spawned
                    && worldPrisoner != null
                    && !worldPrisoner.Dead
                    && !worldPrisoner.Destroyed
                    && (worldPrisoner.IsPrisonerOfColony && worldPrisoner.MapHeld != null
                        || Find.WorldObjects?.Caravans.Any(caravan => caravan != null
                            && caravan.PawnsListForReading.Contains(worldPrisoner)) == true);
            }

            Pawn prisoner = eventRecord.Prisoner;
            return prisoner != null
                && !prisoner.Dead
                && !prisoner.Destroyed
                && prisoner.IsPrisonerOfColony
                && prisoner.MapHeld != null;
        }

        private void OfferDiplomacyEvent(PrisonerDiplomacyEventRecord eventRecord)
        {
            if (Find.LetterStack == null || GenCommandLine.CommandLineArgPassed("pdsmoketest"))
            {
                eventRecord.PlayerAccepted = true;
                eventRecord.State = PrisonerDiplomacyEventState.Active;
                eventRecord.TriggerTick = Find.TickManager.TicksGame;
                return;
            }

            TaggedString label;
            TaggedString text;
            if (eventRecord.Kind == PrisonerDiplomacyEventKind.PublicWarCrimeTrial)
            {
                label = "PD_EventTrialLabel".Translate();
                text = "PD_EventTrialText".Translate(eventRecord.PrisonerLabel ?? "?");
            }
            else
            {
                label = "PD_EventNeutralTradeLabel".Translate();
                text = "PD_EventNeutralTradeText".Translate(
                    eventRecord.Faction.NameColored,
                    eventRecord.PrisonerLabel ?? "?");
            }

            ChoiceLetter_PrisonerDiplomacyEvent letter =
                (ChoiceLetter_PrisonerDiplomacyEvent)LetterMaker.MakeLetter(
                    label,
                    text,
                    PrisonerDiplomacyDefOf.PD_PrisonerDiplomacyEvent,
                    new LookTargets(eventRecord.Prisoner),
                    eventRecord.Faction);
            letter.EventId = eventRecord.EventId;
            Find.LetterStack.ReceiveLetter(letter);
            eventRecord.State = PrisonerDiplomacyEventState.Offered;
        }

        private void TryExecuteDiplomacyEvent(PrisonerDiplomacyEventRecord eventRecord, int now)
        {
            DiplomacyEventExecutionResult result;
            switch (eventRecord.Kind)
            {
                case PrisonerDiplomacyEventKind.NeutralTradeCaravan:
                    result = ExecuteNeutralTradeExchange(eventRecord, now);
                    break;
                case PrisonerDiplomacyEventKind.RansomAmbushRetaliation:
                    result = ExecuteRansomAmbushRetaliation(eventRecord)
                        ? DiplomacyEventExecutionResult.Completed
                        : DiplomacyEventExecutionResult.Retry;
                    break;
                case PrisonerDiplomacyEventKind.FalseSurrenderInfiltration:
                    result = ExecuteFalseSurrenderInfiltration(eventRecord)
                        ? DiplomacyEventExecutionResult.Completed
                        : DiplomacyEventExecutionResult.Retry;
                    break;
                case PrisonerDiplomacyEventKind.PublicWarCrimeTrial:
                    result = ExecutePublicWarCrimeTrial(eventRecord)
                        ? DiplomacyEventExecutionResult.Completed
                        : DiplomacyEventExecutionResult.Retry;
                    break;
                default:
                    result = DiplomacyEventExecutionResult.TerminalFailure;
                    break;
            }

            if (result == DiplomacyEventExecutionResult.Completed)
            {
                eventRecord.State = PrisonerDiplomacyEventState.Completed;
                eventRecord.OutcomeApplied = true;
                CleanupNeutralTradePoint(eventRecord);
                return;
            }

            if (result == DiplomacyEventExecutionResult.Deferred)
            {
                return;
            }

            if (result == DiplomacyEventExecutionResult.TerminalFailure)
            {
                eventRecord.State = PrisonerDiplomacyEventState.Failed;
                eventRecord.OutcomeApplied = true;
                CleanupNeutralTradePoint(eventRecord);
                return;
            }

            RetryDiplomacyEvent(eventRecord, now);
        }

        private DiplomacyEventExecutionResult ExecuteNeutralTradeExchange(
            PrisonerDiplomacyEventRecord eventRecord,
            int now)
        {
            PrisonerDeal deal = GetDeal(eventRecord.SourceDealId);
            if (deal == null
                || deal.Prisoner != eventRecord.Prisoner)
            {
                return DiplomacyEventExecutionResult.TerminalFailure;
            }

            Map map = ResolveExtensionEventMap(eventRecord);
            if (map == null)
            {
                return DiplomacyEventExecutionResult.Retry;
            }

            if (eventRecord.Stage <= 0)
            {
                if (deal.State != DealState.AcceptedAwaitingRelease
                    || !TryCreateNeutralTradePoint(eventRecord))
                {
                    // A pre-1.2 save can contain the old map-scoped caravan
                    // stage. Keep that path recoverable while all new events
                    // use the persistent world object.
                    if (eventRecord.WorldTradePointRequested
                        || !TrySpawnNeutralTradeCaravan(map, out Faction intermediaryFaction))
                    {
                        return DiplomacyEventExecutionResult.Retry;
                    }

                    eventRecord.IntermediaryFaction = intermediaryFaction;
                    eventRecord.Stage = 1;
                    eventRecord.StageStartedTick = now;
                    eventRecord.Attempts = 0;
                    eventRecord.TriggerTick = now + NeutralExchangeStageTicks;
                    Messages.Message(
                        "PD_EventNeutralTradeCaravanArrived".Translate(
                            intermediaryFaction?.NameColored ?? "PD_EventNeutralMediator".Translate()),
                        MessageTypeDefOf.PositiveEvent,
                        false);
                    return DiplomacyEventExecutionResult.Deferred;
                }

                eventRecord.Stage = 1;
                eventRecord.StageStartedTick = now;
                eventRecord.Attempts = 0;
                eventRecord.TriggerTick = now + NeutralExchangeStageTicks;
                Messages.Message(
                    "PD_EventNeutralTradePointCreated".Translate(
                        eventRecord.PrisonerLabel ?? eventRecord.PrisonerLoadId ?? "?",
                        eventRecord.NeutralTradeTile.ToString()),
                    MessageTypeDefOf.PositiveEvent,
                    false);
                return DiplomacyEventExecutionResult.Deferred;
            }

            if (eventRecord.Stage == 1)
            {
                if (eventRecord.WorldTradePointRequested)
                {
                    if (eventRecord.NeutralTradePoint == null
                        || !eventRecord.NeutralTradePoint.Spawned)
                    {
                        if (!TryCreateNeutralTradePoint(eventRecord))
                        {
                            return DiplomacyEventExecutionResult.Retry;
                        }
                    }

                    // Arrival is driven by CaravanArrivalAction. Do not
                    // progress this stage merely because time elapsed.
                    eventRecord.TriggerTick = now + NeutralExchangeStageTicks;
                    return DiplomacyEventExecutionResult.Deferred;
                }

                if (deal.State != DealState.AcceptedAwaitingRelease)
                {
                    return deal.State == DealState.ReleaseOrdered
                        ? AdvanceNeutralReleaseStage(eventRecord, now)
                        : DiplomacyEventExecutionResult.TerminalFailure;
                }

                if (!HasNeutralTradeCaravan(map, eventRecord.IntermediaryFaction))
                {
                    return DiplomacyEventExecutionResult.Retry;
                }

                if (!OrderRansomRelease(deal.Prisoner))
                {
                    return DiplomacyEventExecutionResult.Retry;
                }

                Messages.Message(
                    "PD_EventNeutralTradeReleaseOrdered".Translate(
                        deal.Prisoner?.LabelShortCap ?? deal.PrisonerLoadId),
                    deal.Prisoner,
                    MessageTypeDefOf.NeutralEvent,
                    false);
                return AdvanceNeutralReleaseStage(eventRecord, now);
            }

            if (deal.State == DealState.Completed || deal.PrisonerDelivered)
            {
                Find.LetterStack?.ReceiveLetter(
                    "PD_EventNeutralTradeCompletedLabel".Translate(),
                    "PD_EventNeutralTradeCompletedText".Translate(
                        deal.Faction?.NameColored ?? "?",
                        deal.Prisoner?.LabelShortCap ?? deal.PrisonerLoadId),
                    LetterDefOf.PositiveEvent,
                    new LookTargets(map.Parent),
                    deal.Faction);
                return DiplomacyEventExecutionResult.Completed;
            }

            if (deal.State == DealState.ReleaseOrdered
                || deal.State == DealState.FulfillmentPending)
            {
                eventRecord.TriggerTick = now + NeutralExchangeStageTicks;
                return DiplomacyEventExecutionResult.Deferred;
            }

            return DiplomacyEventExecutionResult.TerminalFailure;
        }

        private bool TryCreateNeutralTradePoint(PrisonerDiplomacyEventRecord eventRecord)
        {
            if (eventRecord == null)
            {
                return false;
            }

            if (eventRecord.NeutralTradePoint != null
                && eventRecord.NeutralTradePoint.Spawned)
            {
                eventRecord.WorldTradePointRequested = true;
                eventRecord.NeutralTradeTile = eventRecord.NeutralTradePoint.Tile;
                return true;
            }

            eventRecord.WorldTradePointRequested = true;
            if (PrisonerDiplomacyDefOf.PD_NeutralTradePoint == null
                || Find.WorldObjects == null
                || !TileFinder.TryFindNewSiteTile(
                    out PlanetTile tile,
                    5,
                    12,
                    false,
                    tileFinderMode: TileFinderMode.Random,
                    exitOnFirstTileFound: false))
            {
                eventRecord.WorldTradePointRequested = false;
                return false;
            }

            WorldObject_PrisonerDiplomacyTradePoint point =
                (WorldObject_PrisonerDiplomacyTradePoint)WorldObjectMaker.MakeWorldObject(
                    PrisonerDiplomacyDefOf.PD_NeutralTradePoint);
            point.EventId = eventRecord.EventId;
            point.Tile = tile;
            Find.WorldObjects.Add(point);
            eventRecord.NeutralTradePoint = point;
            eventRecord.NeutralTradeTile = tile;
            return point.Spawned;
        }

        private static void CleanupNeutralTradePoint(PrisonerDiplomacyEventRecord eventRecord)
        {
            if (eventRecord?.NeutralTradePoint != null)
            {
                if (eventRecord.NeutralTradePoint.Spawned)
                {
                    eventRecord.NeutralTradePoint.Destroy();
                }

                eventRecord.NeutralTradePoint = null;
            }
        }

        public bool NotifyNeutralTradePointArrived(string eventId, Caravan caravan)
        {
            PrisonerDiplomacyEventRecord eventRecord = GetDiplomacyEvent(eventId);
            PrisonerDeal deal = eventRecord == null ? null : GetDeal(eventRecord.SourceDealId);
            Pawn prisoner = deal?.Prisoner ?? eventRecord?.Prisoner;
            if (eventRecord == null
                || deal == null
                || eventRecord.Kind != PrisonerDiplomacyEventKind.NeutralTradeCaravan
                || !eventRecord.IsActive
                || !eventRecord.WorldTradePointRequested
                || eventRecord.NeutralTradePoint == null
                || !eventRecord.NeutralTradePoint.Spawned
                || eventRecord.Stage != 1
                || deal.State != DealState.AcceptedAwaitingRelease
                || prisoner == null
                || !prisoner.IsPrisonerOfColony
                || !caravan.PawnsListForReading.Contains(prisoner))
            {
                Messages.Message(
                    "PD_DebugNeutralTradeArrivalRejected".Translate(),
                    caravan,
                    MessageTypeDefOf.RejectInput,
                    false);
                return false;
            }

            if (deal.PrisonerDelivered || deal.State == DealState.FulfillmentPending
                || deal.State == DealState.Completed)
            {
                CleanupNeutralTradePoint(eventRecord);
                return false;
            }

            int now = Find.TickManager.TicksGame;
            caravan.RemovePawn(prisoner);
            if (prisoner.Faction != deal.Faction)
            {
                prisoner.SetFaction(deal.Faction);
            }
            // Caravan pawns are normally already tracked by WorldPawns. Only
            // add an untracked pawn; PassToWorld logs an error for duplicates.
            if (!Find.WorldPawns.Contains(prisoner))
            {
                Find.WorldPawns.PassToWorld(prisoner, PawnDiscardDecideMode.KeepForever);
            }
            bool destroyEmptyCaravan = caravan.PawnsListForReading.Count == 0;
            deal.VanillaReleaseConfirmed = true;
            deal.PrisonerDelivered = true;
            deal.PrisonerDeliveredTick = now;
            deal.State = DealState.FulfillmentPending;
            eventRecord.Stage = 2;
            eventRecord.StageStartedTick = now;
            eventRecord.TriggerTick = now;
            eventRecord.Attempts = 0;
            eventRecord.NeutralTradePoint.Destroy();
            eventRecord.NeutralTradePoint = null;
            FulfillDeal(deal);
            TryExecuteDiplomacyEvent(eventRecord, now);
            Messages.Message(
                "PD_EventNeutralTradePointArrived".Translate(
                    prisoner.LabelShortCap,
                    deal.Faction?.NameColored ?? "?"),
                caravan,
                MessageTypeDefOf.PositiveEvent,
                false);
            if (destroyEmptyCaravan && caravan.Spawned)
            {
                caravan.Destroy();
            }
            return true;
        }

        private static DiplomacyEventExecutionResult AdvanceNeutralReleaseStage(
            PrisonerDiplomacyEventRecord eventRecord,
            int now)
        {
            eventRecord.Stage = 2;
            eventRecord.StageStartedTick = now;
            eventRecord.Attempts = 0;
            eventRecord.TriggerTick = now + NeutralExchangeStageTicks;
            return DiplomacyEventExecutionResult.Deferred;
        }

        private static bool TrySpawnNeutralTradeCaravan(Map map, out Faction intermediaryFaction)
        {
            intermediaryFaction = null;
            if (map == null || IncidentDefOf.TraderCaravanArrival == null)
            {
                return false;
            }

            List<Lord> existingLords = map.lordManager?.lords?.ToList() ?? new List<Lord>();
            IncidentParms parms = StorytellerUtility.DefaultParmsNow(
                IncidentCategoryDefOf.Misc,
                map);
            parms.points = Math.Max(200f, StorytellerUtility.DefaultThreatPointsNow(map) * 0.20f);
            parms.forced = true;
            parms.bypassStorytellerSettings = true;
            parms.sendLetter = false;
            if (!IncidentDefOf.TraderCaravanArrival.Worker.TryExecute(parms))
            {
                return false;
            }

            Lord newCaravan = map.lordManager?.lords?
                .FirstOrDefault(lord => lord != null
                    && !existingLords.Contains(lord)
                    && IsTradeCaravanLord(lord));
            intermediaryFaction = newCaravan?.faction;
            return true;
        }

        private static bool HasNeutralTradeCaravan(Map map, Faction intermediaryFaction)
        {
            return map?.lordManager?.lords?.Any(lord => lord != null
                && IsTradeCaravanLord(lord)
                && (intermediaryFaction == null || lord.faction == intermediaryFaction)) == true;
        }

        private static bool IsTradeCaravanLord(Lord lord)
        {
            return lord?.LordJob != null
                && lord.LordJob.GetType().Name == "LordJob_TradeWithColony"
                && lord.faction != null
                && !lord.faction.HostileTo(Faction.OfPlayer);
        }

        private bool ExecuteRansomAmbushRetaliation(PrisonerDiplomacyEventRecord eventRecord)
        {
            Map map = ResolveExtensionEventMap(eventRecord);
            if (map == null)
            {
                return false;
            }

            return TryExecuteCausalRaid(
                eventRecord.Faction,
                map,
                1.10f,
                "PD_EventAmbushLabel".Translate(),
                "PD_EventAmbushText".Translate(
                    eventRecord.Faction.NameColored,
                    eventRecord.PrisonerLabel
                        ?? eventRecord.Prisoner?.LabelShortCap
                        ?? eventRecord.PrisonerLoadId
                        ?? "PD_UnknownPrisoner".Translate(),
                    eventRecord.SourceDealId
                        ?? eventRecord.EventId
                        ?? "PD_UnknownDeal".Translate()),
                350f);
        }

        private bool ExecuteFalseSurrenderInfiltration(PrisonerDiplomacyEventRecord eventRecord)
        {
            return ExecuteFalseSurrenderInfiltration(eventRecord, null);
        }

        private bool ExecuteFalseSurrenderInfiltration(
            PrisonerDiplomacyEventRecord eventRecord,
            bool? forcedPrisonBreak)
        {
            Pawn prisoner = eventRecord.Prisoner;
            if (prisoner == null || !prisoner.IsPrisonerOfColony)
            {
                return false;
            }

            int seed = Gen.HashCombineInt(
                GenText.StableStringHash(eventRecord.EventId ?? string.Empty),
                eventRecord.CreatedTick);
            bool prisonBreak = forcedPrisonBreak
                ?? (PrisonBreakUtility.CanParticipateInPrisonBreak(prisoner)
                    && Rand.ChanceSeeded(0.65f, seed));
            if (prisonBreak && !PrisonBreakUtility.CanParticipateInPrisonBreak(prisoner))
            {
                return false;
            }
            if (prisonBreak)
            {
                PrisonBreakUtility.StartPrisonBreak(prisoner);
            }
            else
            {
                ApplyMemoryChange(
                    eventRecord.Faction,
                    -5f,
                    0f,
                    15f,
                    "PD_MemoryEventInfiltration",
                    eventRecord.PrisonerLabel ?? "?",
                    true);
            }

            Find.LetterStack?.ReceiveLetter(
                "PD_EventInfiltrationLabel".Translate(),
                "PD_EventInfiltrationText".Translate(
                    eventRecord.Faction.NameColored,
                    eventRecord.PrisonerLabel ?? "?"),
                LetterDefOf.ThreatSmall,
                new LookTargets(prisoner),
                eventRecord.Faction);
            return true;
        }

        private bool ExecutePublicWarCrimeTrial(PrisonerDiplomacyEventRecord eventRecord)
        {
            Pawn prisoner = eventRecord.Prisoner;
            PrisonerRecord record = GetRecord(prisoner);
            if (record == null || record.Importance < PrisonerImportance.Leader)
            {
                return false;
            }

            foreach (Faction ally in Find.FactionManager.AllFactionsVisible.Where(faction =>
                faction != null
                && faction != Faction.OfPlayer
                && faction != eventRecord.Faction
                && !faction.defeated
                && faction.PlayerRelationKind == FactionRelationKind.Ally
                && faction.CanChangeGoodwillFor(Faction.OfPlayer, 8)))
            {
                ally.TryAffectGoodwillWith(Faction.OfPlayer, 8, false, false, null);
            }

            prisoner.Kill(null, null);
            // A public execution is a direct diplomatic insult to the prisoner's
            // original faction. Keep this separate from the mod's qualitative
            // memory so vanilla goodwill reflects the visible consequence too.
            eventRecord.Faction.TryAffectGoodwillWith(Faction.OfPlayer, -40, false, false, null);
            ApplyMemoryChange(
                eventRecord.Faction,
                -40f,
                -20f,
                50f,
                "PD_MemoryEventExecution",
                eventRecord.PrisonerLabel ?? "?",
                true);
            AddStrategicFollowup(
                StrategicFollowupKind.RetaliationRaid,
                record,
                eventRecord.SourceDealId ?? eventRecord.EventId,
                Find.TickManager.TicksGame + TicksPerDay);
            Find.LetterStack?.ReceiveLetter(
                "PD_EventTrialLabel".Translate(),
                "PD_EventTrialText".Translate(eventRecord.PrisonerLabel ?? "?"),
                LetterDefOf.NegativeEvent,
                new LookTargets(prisoner),
                eventRecord.Faction);
            return true;
        }

        private Map ResolveExtensionEventMap(PrisonerDiplomacyEventRecord eventRecord)
        {
            if (eventRecord?.Map != null && Find.Maps.Contains(eventRecord.Map))
            {
                return eventRecord.Map;
            }
            return Find.Maps.FirstOrDefault(candidate => candidate?.IsPlayerHome == true);
        }

        private static void RetryDiplomacyEvent(PrisonerDiplomacyEventRecord eventRecord, int now)
        {
            eventRecord.Attempts++;
            if (eventRecord.Attempts >= 4)
            {
                eventRecord.State = PrisonerDiplomacyEventState.Failed;
                eventRecord.OutcomeApplied = true;
            }
            else
            {
                eventRecord.TriggerTick = now + ExtensionEventRetryTicks;
            }
        }

        private void TryScheduleAcceptedDealEvents(PrisonerDeal deal)
        {
            if (deal?.Prisoner == null
                || PrisonerDiplomacyMod.Settings?.EnableStrategicConsequences == false
                || GenCommandLine.CommandLineArgPassed("pdsmoketest"))
            {
                return;
            }

            PrisonerRecord record = GetRecord(deal.Prisoner);
            int seed = Gen.HashCombineInt(GenText.StableStringHash(deal.DealId ?? string.Empty), 7919);
            if (record?.Importance >= PrisonerImportance.Notable && Rand.ChanceSeeded(0.18f, seed))
            {
                TryScheduleExtensionEvent(
                    "core.neutral_trade",
                    PrisonerDiplomacyEventKind.NeutralTradeCaravan,
                    deal.Faction,
                    deal.Map,
                    deal.Prisoner,
                    deal.DealId,
                    Find.TickManager.TicksGame + TicksPerDay);
            }

            bool highSkill = deal.Prisoner.skills?.skills?.Any(skill => skill?.Level >= 12) == true;
            if (highSkill && FactionNegotiationUtility.IsTransactional(deal.Faction)
                && Rand.ChanceSeeded(0.12f, Gen.HashCombineInt(seed, 7927)))
            {
                TryScheduleExtensionEvent(
                    "core.false_surrender",
                    PrisonerDiplomacyEventKind.FalseSurrenderInfiltration,
                    deal.Faction,
                    deal.Map,
                    deal.Prisoner,
                    deal.DealId,
                    Find.TickManager.TicksGame + 90000);
            }

            if (record?.Importance >= PrisonerImportance.Leader
                && deal.Faction.HostileTo(Faction.OfPlayer)
                && Rand.ChanceSeeded(0.28f, Gen.HashCombineInt(seed, 7933)))
            {
                TryScheduleExtensionEvent(
                    "core.public_trial",
                    PrisonerDiplomacyEventKind.PublicWarCrimeTrial,
                    deal.Faction,
                    deal.Map,
                    deal.Prisoner,
                    deal.DealId,
                    Find.TickManager.TicksGame + 30000);
            }
        }

        private void ScheduleRansomAmbushRetaliationEvent(PrisonerDeal deal)
        {
            if (deal?.Faction == null
                || deal.PirateRisk != PirateDealRisk.Ambush
                || deal.PirateRiskMitigated
                || PrisonerDiplomacyMod.Settings?.EnableStrategicConsequences == false)
            {
                return;
            }

            TryScheduleExtensionEvent(
                "core.ransom_ambush",
                PrisonerDiplomacyEventKind.RansomAmbushRetaliation,
                deal.Faction,
                deal.Map,
                deal.Prisoner,
                deal.DealId,
                Find.TickManager.TicksGame
                    + FactionNegotiationUtility.CalculateRiskEventDelayTicks(
                        deal.DealId,
                        deal.PirateRisk) / 2);
        }

        public bool DebugForceDiplomacyEvent(
            Pawn pawn,
            PrisonerDiplomacyEventKind kind,
            out PrisonerDiplomacyEventRecord eventRecord,
            out string failureKey)
        {
            eventRecord = null;
            failureKey = null;
            PrisonerRecord record = GetRecord(pawn) ?? RegisterPawn(pawn);
            if (record?.OriginalFaction == null || pawn?.MapHeld == null)
            {
                failureKey = "PD_DebugNoEligible";
                return false;
            }

            PrisonerDiplomacyEventRecord existing = diplomacyEvents
                .Where(item => item?.IsActive == true
                    && item.Kind == kind
                    && item.PrisonerLoadId == record.PawnLoadId)
                .OrderByDescending(item => item.CreatedTick)
                .FirstOrDefault();
            if (existing != null)
            {
                eventRecord = existing;
                failureKey = "PD_DebugEventAlreadyActive";
                return false;
            }

            PrisonerDeal deal = GetActiveDeal(pawn);
            if (kind == PrisonerDiplomacyEventKind.NeutralTradeCaravan)
            {
                if (deal == null)
                {
                    deal = ForceOffer(pawn);
                }
                if (deal?.State == DealState.Offered)
                {
                    AcceptDeal(deal.DealId);
                }
                if (deal?.State == DealState.Negotiating)
                {
                    AcceptCounterOffer(deal);
                }

                // Accepting a deal may deterministically schedule the same neutral
                // handoff through the normal gameplay hook. Treat that event as the
                // requested debug result instead of attempting to create a duplicate.
                existing = diplomacyEvents
                    .Where(item => item?.IsActive == true
                        && item.Kind == kind
                        && item.PrisonerLoadId == record.PawnLoadId)
                    .OrderByDescending(item => item.CreatedTick)
                    .FirstOrDefault();
                if (existing != null)
                {
                    eventRecord = existing;
                    return true;
                }

                if (deal?.State != DealState.AcceptedAwaitingRelease)
                {
                    failureKey = "PD_DebugEventNeedsAcceptedDeal";
                    return false;
                }
            }

            if (kind == PrisonerDiplomacyEventKind.RansomAmbushRetaliation
                && deal == null)
            {
                // Keep the debug event causal even when the selected test
                // prisoner has no prior ransom deal. The forced offer is only
                // a source record; the debug action still executes the raid
                // immediately and does not release the prisoner.
                deal = ForceOffer(pawn);
            }

            if (kind == PrisonerDiplomacyEventKind.PublicWarCrimeTrial)
            {
                record.Importance = PrisonerImportance.Leader;
            }

            string definitionId = DefinitionIdForEventKind(kind);
            if (!TryScheduleExtensionEvent(
                definitionId,
                kind,
                record.OriginalFaction,
                pawn.MapHeld,
                pawn,
                deal?.DealId,
                Find.TickManager.TicksGame))
            {
                failureKey = "PD_DebugEventScheduleFailed";
                return false;
            }

            eventRecord = diplomacyEvents.LastOrDefault(item => item?.DefinitionId == definitionId
                && item.PrisonerLoadId == record.PawnLoadId);
            if (eventRecord == null)
            {
                failureKey = "PD_DebugEventScheduleFailed";
                return false;
            }

            if (EventRequiresChoice(kind))
            {
                OfferDiplomacyEvent(eventRecord);
            }
            else
            {
                eventRecord.State = PrisonerDiplomacyEventState.Active;
                TryExecuteDiplomacyEvent(eventRecord, Find.TickManager.TicksGame);
            }
            return true;
        }

        public bool DebugForceNeutralWorldTradePoint(
            Pawn pawn,
            out PrisonerDiplomacyEventRecord eventRecord,
            out string failureKey)
        {
            eventRecord = null;
            failureKey = null;
            if (!DebugForceDiplomacyEvent(
                pawn,
                PrisonerDiplomacyEventKind.NeutralTradeCaravan,
                out eventRecord,
                out failureKey))
            {
                return false;
            }

            if (eventRecord.State == PrisonerDiplomacyEventState.Offered)
            {
                if (!AcceptDiplomacyEvent(eventRecord.EventId))
                {
                    failureKey = "PD_DebugEventScheduleFailed";
                    return false;
                }
            }
            else if (eventRecord.State == PrisonerDiplomacyEventState.Scheduled)
            {
                eventRecord.State = PrisonerDiplomacyEventState.Active;
                eventRecord.PlayerAccepted = true;
                eventRecord.TriggerTick = Find.TickManager.TicksGame;
                TryExecuteDiplomacyEvent(eventRecord, Find.TickManager.TicksGame);
            }
            else if (eventRecord.State == PrisonerDiplomacyEventState.Active
                && eventRecord.Stage <= 0)
            {
                TryExecuteDiplomacyEvent(eventRecord, Find.TickManager.TicksGame);
            }

            return eventRecord.WorldTradePointRequested
                && eventRecord.NeutralTradePoint != null
                && eventRecord.NeutralTradePoint.Spawned;
        }

        public bool DebugForceFalseSurrenderOutcome(
            Pawn pawn,
            bool forcePrisonBreak,
            out PrisonerDiplomacyEventRecord eventRecord,
            out string failureKey)
        {
            eventRecord = null;
            failureKey = null;
            PrisonerRecord record = GetRecord(pawn) ?? RegisterPawn(pawn);
            if (record?.OriginalFaction == null
                || pawn?.MapHeld == null
                || !pawn.IsPrisonerOfColony)
            {
                failureKey = "PD_DebugNoEligible";
                return false;
            }

            if (diplomacyEvents.Any(item => item?.IsActive == true
                && item.Kind == PrisonerDiplomacyEventKind.FalseSurrenderInfiltration
                && item.PrisonerLoadId == record.PawnLoadId))
            {
                failureKey = "PD_DebugEventAlreadyActive";
                return false;
            }

            string definitionId = DefinitionIdForEventKind(
                PrisonerDiplomacyEventKind.FalseSurrenderInfiltration);
            if (!TryScheduleExtensionEvent(
                definitionId,
                PrisonerDiplomacyEventKind.FalseSurrenderInfiltration,
                record.OriginalFaction,
                pawn.MapHeld,
                pawn,
                GetActiveDeal(pawn)?.DealId,
                Find.TickManager.TicksGame))
            {
                failureKey = "PD_DebugEventScheduleFailed";
                return false;
            }

            eventRecord = diplomacyEvents.LastOrDefault(item => item?.DefinitionId == definitionId
                && item.PrisonerLoadId == record.PawnLoadId);
            if (eventRecord == null)
            {
                failureKey = "PD_DebugEventScheduleFailed";
                return false;
            }

            eventRecord.State = PrisonerDiplomacyEventState.Active;
            eventRecord.TriggerTick = Find.TickManager.TicksGame;
            if (!ExecuteFalseSurrenderInfiltration(eventRecord, forcePrisonBreak))
            {
                RetryDiplomacyEvent(eventRecord, Find.TickManager.TicksGame);
                failureKey = "PD_DebugEventScheduleFailed";
                return false;
            }

            eventRecord.State = PrisonerDiplomacyEventState.Completed;
            eventRecord.OutcomeApplied = true;
            return true;
        }

        public Caravan DebugSendPrisonerToNeutralWorldTradePoint(
            Pawn pawn,
            out string failureKey)
        {
            failureKey = null;
            PrisonerDiplomacyEventRecord eventRecord = diplomacyEvents
                .Where(item => item?.IsActive == true
                    && item.Kind == PrisonerDiplomacyEventKind.NeutralTradeCaravan
                    && item.PrisonerLoadId == pawn?.GetUniqueLoadID()
                    && item.WorldTradePointRequested
                    && item.NeutralTradePoint != null
                    && item.NeutralTradePoint.Spawned)
                .OrderByDescending(item => item.CreatedTick)
                .FirstOrDefault();
            PrisonerDeal deal = eventRecord == null ? null : GetDeal(eventRecord.SourceDealId);
            if (eventRecord == null || deal == null || deal.State != DealState.AcceptedAwaitingRelease)
            {
                failureKey = "PD_DebugNoActiveEvent";
                return null;
            }
            if (pawn == null || pawn.MapHeld == null || !pawn.IsPrisonerOfColony)
            {
                failureKey = "PD_DebugNoMap";
                return null;
            }

            string pawnLoadId = pawn.GetUniqueLoadID();
            pendingCaravanTransferExits.Add(pawnLoadId);
            try
            {
                Map map = pawn.MapHeld;
                PlanetTile destination = eventRecord.NeutralTradePoint.Tile;
                Caravan caravan = CaravanExitMapUtility.ExitMapAndCreateCaravan(
                    new[] { pawn },
                    Faction.OfPlayer,
                    map.Tile,
                    destination,
                    destination,
                    false);
                if (caravan == null)
                {
                    failureKey = "PD_DebugWorldTradeCaravanFailed";
                    return null;
                }

                caravan.pather.StartPath(destination, new CaravanArrivalAction_PrisonerDiplomacyTradePoint(
                    eventRecord.NeutralTradePoint as WorldObject_PrisonerDiplomacyTradePoint), true);
                return caravan;
            }
            catch (Exception exception)
            {
                Log.Warning("[Prisoner Diplomacy Debug] Could not send prisoner to world trade point: " + exception);
                failureKey = "PD_DebugWorldTradeCaravanFailed";
                return null;
            }
            finally
            {
                // NotifyPawnExited removes this during a successful transfer.
                // Clear it here when caravan creation fails before the callback.
                pendingCaravanTransferExits.Remove(pawnLoadId);
            }
        }

        public bool DebugCompleteNeutralWorldTradePoint(WorldObject worldObject)
        {
            WorldObject_PrisonerDiplomacyTradePoint point =
                worldObject as WorldObject_PrisonerDiplomacyTradePoint;
            if (point == null || !point.Spawned)
            {
                return false;
            }

            PrisonerDiplomacyEventRecord eventRecord = GetDiplomacyEvent(point.EventId);
            PrisonerDeal deal = eventRecord == null ? null : GetDeal(eventRecord.SourceDealId);
            Caravan caravan = Find.WorldObjects?.Caravans
                .FirstOrDefault(candidate => candidate != null
                    && candidate.Tile == point.Tile
                    && deal?.Prisoner != null
                    && candidate.PawnsListForReading.Contains(deal.Prisoner));
            return caravan != null && NotifyNeutralTradePointArrived(point.EventId, caravan);
        }

        public bool DebugCompleteFirstNeutralWorldTradePoint()
        {
            foreach (WorldObject_PrisonerDiplomacyTradePoint point in Find.WorldObjects?.AllWorldObjects
                .OfType<WorldObject_PrisonerDiplomacyTradePoint>()
                .Where(candidate => candidate != null && candidate.Spawned)
                .ToList() ?? new List<WorldObject_PrisonerDiplomacyTradePoint>())
            {
                if (DebugCompleteNeutralWorldTradePoint(point))
                {
                    return true;
                }
            }

            return false;
        }

        public void DebugLogNeutralWorldTradePoints()
        {
            List<WorldObject_PrisonerDiplomacyTradePoint> points = Find.WorldObjects?.AllWorldObjects
                .OfType<WorldObject_PrisonerDiplomacyTradePoint>()
                .ToList() ?? new List<WorldObject_PrisonerDiplomacyTradePoint>();
            Log.Message("[Prisoner Diplomacy Debug] world trade points=" + points.Count + ".");
            foreach (WorldObject_PrisonerDiplomacyTradePoint point in points)
            {
                PrisonerDiplomacyEventRecord eventRecord = GetDiplomacyEvent(point.EventId);
                PrisonerDeal deal = eventRecord == null ? null : GetDeal(eventRecord.SourceDealId);
                List<Caravan> caravansAtPoint = Find.WorldObjects?.Caravans
                    .Where(caravan => caravan != null && caravan.Tile == point.Tile)
                    .ToList() ?? new List<Caravan>();
                bool targetPresent = deal?.Prisoner != null
                    && caravansAtPoint.Any(caravan => caravan.PawnsListForReading.Contains(deal.Prisoner));
                Log.Message("[Prisoner Diplomacy Debug] worldPoint event=" + (point.EventId ?? "?")
                    + " tile=" + point.Tile
                    + " spawned=" + point.Spawned
                    + " state=" + (eventRecord?.State.ToString() ?? "missing")
                    + " stage=" + (eventRecord?.Stage ?? -1)
                    + " attempts=" + (eventRecord?.Attempts ?? -1)
                    + " caravansAtPoint=" + caravansAtPoint.Count
                    + " targetPresent=" + targetPresent
                    + " dealState=" + (deal?.State.ToString() ?? "missing")
                    + " prisonerDelivered=" + (deal?.PrisonerDelivered == true)
                    + " rewardIssued=" + (deal?.RewardIssued == true)
                    + " prisoner=" + (eventRecord?.PrisonerLabel ?? "?") + ".");
            }
        }

        public bool DebugAdvanceDiplomacyEvent(Pawn pawn, out PrisonerDiplomacyEventRecord eventRecord)
        {
            string pawnId = pawn?.GetUniqueLoadID();
            eventRecord = diplomacyEvents
                .Where(item => item?.IsActive == true && item.PrisonerLoadId == pawnId)
                .OrderByDescending(item => item.CreatedTick)
                .FirstOrDefault();
            if (eventRecord == null)
            {
                return false;
            }

            int now = Find.TickManager.TicksGame;
            if (eventRecord.State == PrisonerDiplomacyEventState.Offered)
            {
                return AcceptDiplomacyEvent(eventRecord.EventId);
            }
            if (eventRecord.State == PrisonerDiplomacyEventState.Scheduled
                && EventRequiresChoice(eventRecord.Kind))
            {
                OfferDiplomacyEvent(eventRecord);
                return true;
            }

            eventRecord.TriggerTick = now;
            TryExecuteDiplomacyEvent(eventRecord, now);
            return true;
        }

        public int DebugCancelDiplomacyEvents(Pawn pawn)
        {
            string pawnId = pawn?.GetUniqueLoadID();
            int cancelled = 0;
            foreach (PrisonerDiplomacyEventRecord eventRecord in diplomacyEvents
                .Where(item => item?.IsActive == true && item.PrisonerLoadId == pawnId))
            {
                eventRecord.State = PrisonerDiplomacyEventState.Cancelled;
                eventRecord.OutcomeApplied = true;
                CleanupNeutralTradePoint(eventRecord);
                cancelled++;
            }
            return cancelled;
        }

        public void DebugLogDiplomacyEvents()
        {
            Log.Message("[Prisoner Diplomacy Debug] Extension events=" + diplomacyEvents.Count + ".");
            foreach (PrisonerDiplomacyEventRecord eventRecord in diplomacyEvents
                .Where(item => item != null)
                .OrderByDescending(item => item.CreatedTick))
            {
                Log.Message("[Prisoner Diplomacy Debug] event=" + eventRecord.EventId
                    + " definition=" + eventRecord.DefinitionId
                    + " kind=" + eventRecord.Kind
                    + " state=" + eventRecord.State
                    + " stage=" + eventRecord.Stage
                    + " attempts=" + eventRecord.Attempts
                    + " tile=" + (eventRecord.NeutralTradePoint?.Tile.ToString()
                        ?? eventRecord.NeutralTradeTile.ToString())
                    + " worldPoint=" + (eventRecord.NeutralTradePoint?.Spawned == true)
                    + " faction=" + (eventRecord.Faction?.Name ?? "?")
                    + " intermediary=" + (eventRecord.IntermediaryFaction?.Name ?? "?")
                    + " prisoner=" + (eventRecord.PrisonerLabel ?? eventRecord.PrisonerLoadId ?? "?")
                    + " deal=" + (eventRecord.SourceDealId ?? "?") + ".");
            }
        }

        private static string DefinitionIdForEventKind(PrisonerDiplomacyEventKind kind)
        {
            switch (kind)
            {
                case PrisonerDiplomacyEventKind.NeutralTradeCaravan: return "core.neutral_trade";
                case PrisonerDiplomacyEventKind.RansomAmbushRetaliation: return "core.ransom_ambush";
                case PrisonerDiplomacyEventKind.FalseSurrenderInfiltration: return "core.false_surrender";
                case PrisonerDiplomacyEventKind.PublicWarCrimeTrial: return "core.public_trial";
                default: return "core.unknown";
            }
        }
    }
}
