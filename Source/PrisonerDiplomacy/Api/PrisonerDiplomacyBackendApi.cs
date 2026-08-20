using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace PrisonerDiplomacy
{
    // Read-only data contracts for future interfaces. They deliberately expose no
    // mutation path, so a UI cannot bypass the deterministic GameComponent rules.
    public sealed class PrisonerDiplomacyPrisonerSnapshot
    {
        public Pawn Pawn { get; private set; }
        public string PawnLoadId { get; private set; }
        public string PawnLabel { get; private set; }
        public Faction OriginalFaction { get; private set; }
        public string FactionLabel { get; private set; }
        public PrisonerImportance Importance { get; private set; }
        public int DiplomaticValue { get; private set; }
        public float HealthPercent { get; private set; }
        public bool Downed { get; private set; }
        public bool LifeThreatening { get; private set; }
        public bool CanNegotiate { get; private set; }
        public string NegotiationUnavailableReason { get; private set; }
        public int EstimatedFactionContactTicks { get; private set; }
        public int NegotiationCount { get; private set; }
        public string ActiveDealId { get; private set; }
        public IReadOnlyList<string> SpecialRewardIds { get; private set; }

        internal static PrisonerDiplomacyPrisonerSnapshot Create(
            PrisonerDiplomacyGameComponent component,
            PrisonerRecord record,
            Map map)
        {
            if (component == null || record?.Pawn == null)
            {
                return null;
            }

            TaggedString reason;
            bool canNegotiate = record.Pawn.MapHeld == map
                && component.CanStartPlayerNegotiation(record, out reason);
            return new PrisonerDiplomacyPrisonerSnapshot
            {
                Pawn = record.Pawn,
                PawnLoadId = record.PawnLoadId ?? record.Pawn.GetUniqueLoadID(),
                PawnLabel = record.Pawn.LabelShortCap,
                OriginalFaction = record.OriginalFaction,
                FactionLabel = record.OriginalFaction?.Name ?? "?",
                Importance = record.Importance,
                DiplomaticValue = record.DiplomaticValue,
                HealthPercent = record.Pawn.health?.summaryHealth?.SummaryHealthPercent ?? 0f,
                Downed = record.Pawn.Downed,
                LifeThreatening = PrisonerTreatmentUtility.IsLifeThreatening(record.Pawn),
                CanNegotiate = canNegotiate,
                NegotiationUnavailableReason = canNegotiate ? null : reason.ToString(),
                EstimatedFactionContactTicks = component.GetEstimatedFactionContactTicks(record),
                NegotiationCount = record.NegotiationCount,
                ActiveDealId = record.ActiveDealId,
                SpecialRewardIds = PrisonerDiplomacyExtensionRegistry.GetSpecialRewards(
                    record.Pawn,
                    record.OriginalFaction)
                    .Select(reward => reward.RewardId)
                    .ToList()
            };
        }
    }

    public sealed class PrisonerDiplomacyDealSnapshot
    {
        public string DealId { get; private set; }
        public string PrisonerLoadId { get; private set; }
        public string PrisonerLabel { get; private set; }
        public string FactionLabel { get; private set; }
        public DealState State { get; private set; }
        public DealOrigin Origin { get; private set; }
        public string StateKey { get; private set; }
        public string RewardsDescription { get; private set; }
        public int NegotiationRound { get; private set; }
        public int OfferExpiresTick { get; private set; }
        public int FulfillmentExpiresTick { get; private set; }
        public int OfferRemainingTicks { get; private set; }
        public int FulfillmentRemainingTicks { get; private set; }
        public PirateDealRisk PirateRisk { get; private set; }
        public bool PirateRiskDisclosed { get; private set; }
        public bool PirateRiskMitigated { get; private set; }
        public string ReturnedHostageLabel { get; private set; }
        public bool IsActive { get; private set; }
        public bool CanOrderRelease { get; private set; }

        internal static PrisonerDiplomacyDealSnapshot Create(PrisonerDeal deal, int now)
        {
            if (deal == null)
            {
                return null;
            }

            return new PrisonerDiplomacyDealSnapshot
            {
                DealId = deal.DealId,
                PrisonerLoadId = deal.PrisonerLoadId,
                PrisonerLabel = deal.Prisoner?.LabelShortCap ?? deal.PrisonerLoadId ?? "?",
                FactionLabel = deal.Faction?.Name ?? "?",
                State = deal.State,
                Origin = deal.Origin,
                StateKey = deal.State.ToString(),
                RewardsDescription = deal.Rewards?.Description().ToString() ?? "",
                NegotiationRound = deal.NegotiationRound,
                OfferExpiresTick = deal.OfferExpiresTick,
                FulfillmentExpiresTick = deal.FulfillmentExpiresTick,
                OfferRemainingTicks = RemainingTicks(deal.OfferExpiresTick, now),
                FulfillmentRemainingTicks = RemainingTicks(deal.FulfillmentExpiresTick, now),
                PirateRisk = deal.PirateRisk,
                PirateRiskDisclosed = deal.PirateRiskDisclosed,
                PirateRiskMitigated = deal.PirateRiskMitigated,
                ReturnedHostageLabel = deal.ReturnedHostage?.LabelShortCap,
                IsActive = deal.IsActive,
                CanOrderRelease = deal.State == DealState.AcceptedAwaitingRelease
            };
        }

        private static int RemainingTicks(int deadline, int now)
        {
            return deadline < 0 ? -1 : Math.Max(0, deadline - now);
        }
    }

    public sealed class PrisonerDiplomacyFactionSnapshot
    {
        public Faction Faction { get; private set; }
        public string FactionDefName { get; private set; }
        public string FactionLabel { get; private set; }
        public FactionNegotiationType NegotiationType { get; private set; }
        public bool CanNegotiate { get; private set; }
        public int NegotiablePrisonerCount { get; private set; }
        public int AvailableHostageCount { get; private set; }
        public string FinancialStatus { get; private set; }
        public string MemorySummary { get; private set; }
        public string StrategicStatus { get; private set; }

        internal static PrisonerDiplomacyFactionSnapshot Create(
            PrisonerDiplomacyGameComponent component,
            Faction faction,
            Map map)
        {
            if (component == null || faction == null)
            {
                return null;
            }

            return new PrisonerDiplomacyFactionSnapshot
            {
                Faction = faction,
                FactionDefName = faction.def?.defName,
                FactionLabel = faction.Name,
                NegotiationType = FactionNegotiationUtility.GetType(faction),
                CanNegotiate = PrisonerEligibilityUtility.IsNegotiatingFaction(faction),
                NegotiablePrisonerCount = component.GetNegotiableRecords(faction, map).Count,
                AvailableHostageCount = component.GetAvailableHostages(faction).Count,
                FinancialStatus = component.GetFactionFinancialStatus(faction),
                MemorySummary = component.GetFactionMemoryDescription(faction),
                StrategicStatus = component.GetFactionStrategicStatus(faction)
            };
        }
    }

    public sealed class PrisonerDiplomacyEventSnapshot
    {
        public string EventId { get; private set; }
        public string DefinitionId { get; private set; }
        public string ExtensionId { get; private set; }
        public PrisonerDiplomacyEventKind Kind { get; private set; }
        public PrisonerDiplomacyEventState State { get; private set; }
        public string FactionLabel { get; private set; }
        public string PrisonerLabel { get; private set; }
        public string SourceDealId { get; private set; }
        public string IntermediaryFactionLabel { get; private set; }
        public int CreatedTick { get; private set; }
        public int TriggerTick { get; private set; }
        public int Stage { get; private set; }
        public int Attempts { get; private set; }
        public bool IsActive { get; private set; }

        internal static PrisonerDiplomacyEventSnapshot Create(PrisonerDiplomacyEventRecord eventRecord)
        {
            if (eventRecord == null)
            {
                return null;
            }

            return new PrisonerDiplomacyEventSnapshot
            {
                EventId = eventRecord.EventId,
                DefinitionId = eventRecord.DefinitionId,
                ExtensionId = eventRecord.ExtensionId,
                Kind = eventRecord.Kind,
                State = eventRecord.State,
                FactionLabel = eventRecord.Faction?.Name ?? "?",
                PrisonerLabel = eventRecord.Prisoner?.LabelShortCap ?? eventRecord.PrisonerLabel,
                SourceDealId = eventRecord.SourceDealId,
                IntermediaryFactionLabel = eventRecord.IntermediaryFaction?.Name,
                CreatedTick = eventRecord.CreatedTick,
                TriggerTick = eventRecord.TriggerTick,
                Stage = eventRecord.Stage,
                Attempts = eventRecord.Attempts,
                IsActive = eventRecord.IsActive
            };
        }
    }

    public static class PrisonerDiplomacyBackendApi
    {
        public const string ApiVersion = PrisonerDiplomacyApi.ApiVersion;

        public static IReadOnlyList<string> GetRegisteredExtensionIds()
        {
            return PrisonerDiplomacyExtensionRegistry.RegisteredExtensionIds;
        }

        public static IReadOnlyList<PrisonerDiplomacyEventDefinition> GetEventDefinitions()
        {
            return PrisonerDiplomacyExtensionRegistry.RegisteredEventDefinitions;
        }

        public static IReadOnlyList<PrisonerDiplomacySpecialRewardDefinition> GetSpecialRewardOptions(
            Pawn prisoner,
            Faction faction)
        {
            return PrisonerDiplomacyExtensionRegistry.GetSpecialRewards(prisoner, faction);
        }

        public static IReadOnlyList<PrisonerDiplomacyEventSnapshot> GetEventSnapshots()
        {
            PrisonerDiplomacyGameComponent component = PrisonerDiplomacyGameComponent.Current;
            if (component == null)
            {
                return new List<PrisonerDiplomacyEventSnapshot>();
            }

            return component.GetDiplomacyEvents()
                .Select(PrisonerDiplomacyEventSnapshot.Create)
                .Where(snapshot => snapshot != null)
                .ToList();
        }

        public static int GetDiplomaticValueAdjustment(Pawn prisoner, Faction faction)
        {
            return PrisonerDiplomacyExtensionRegistry.GetDiplomaticValueAdjustment(prisoner, faction);
        }

        public static IReadOnlyList<PrisonerDiplomacyPrisonerSnapshot> GetPrisonerSnapshots(Map map)
        {
            PrisonerDiplomacyGameComponent component = PrisonerDiplomacyGameComponent.Current;
            if (component == null || map == null)
            {
                return new List<PrisonerDiplomacyPrisonerSnapshot>();
            }

            return component.GetNegotiableRecords(null, map)
                .Select(record => PrisonerDiplomacyPrisonerSnapshot.Create(component, record, map))
                .Where(snapshot => snapshot != null)
                .ToList();
        }

        public static IReadOnlyList<PrisonerDiplomacyFactionSnapshot> GetFactionSnapshots(Map map)
        {
            PrisonerDiplomacyGameComponent component = PrisonerDiplomacyGameComponent.Current;
            if (component == null || map == null)
            {
                return new List<PrisonerDiplomacyFactionSnapshot>();
            }

            return component.GetNegotiableFactions(map)
                .Select(faction => PrisonerDiplomacyFactionSnapshot.Create(component, faction, map))
                .Where(snapshot => snapshot != null)
                .ToList();
        }

        public static bool TryGetPrisonerSnapshot(
            Pawn pawn,
            Map map,
            out PrisonerDiplomacyPrisonerSnapshot snapshot)
        {
            snapshot = null;
            PrisonerDiplomacyGameComponent component = PrisonerDiplomacyGameComponent.Current;
            PrisonerRecord record = component?.GetRecord(pawn);
            if (component == null || record == null || pawn?.MapHeld != map)
            {
                return false;
            }

            snapshot = PrisonerDiplomacyPrisonerSnapshot.Create(component, record, map);
            return snapshot != null;
        }

        public static bool TryGetActiveDealSnapshot(
            Pawn prisoner,
            out PrisonerDiplomacyDealSnapshot snapshot)
        {
            snapshot = null;
            PrisonerDiplomacyGameComponent component = PrisonerDiplomacyGameComponent.Current;
            PrisonerDeal deal = component?.GetActiveDeal(prisoner);
            if (deal == null)
            {
                return false;
            }

            snapshot = PrisonerDiplomacyDealSnapshot.Create(
                deal,
                Find.TickManager?.TicksGame ?? 0);
            return snapshot != null;
        }

        public static NegotiationResult PreviewDemand(
            Pawn prisoner,
            Pawn negotiator,
            RewardDemand demand,
            out string reasonKey)
        {
            reasonKey = null;
            PrisonerDiplomacyGameComponent component = PrisonerDiplomacyGameComponent.Current;
            PrisonerRecord record = component?.GetRecord(prisoner);
            if (component == null || record == null || demand == null)
            {
                reasonKey = "PD_NegotiationUnavailable";
                return null;
            }

            if (!NegotiationEconomyUtility.IsDemandValid(
                record.OriginalFaction,
                demand,
                out reasonKey,
                record.Pawn))
            {
                return null;
            }

            int now = Find.TickManager?.TicksGame ?? 0;
            int reserve = component.GetAvailableReserve(record.OriginalFaction);
            int materialCap = NegotiationEconomyUtility.CalculateMaterialRewardCap(prisoner?.MapHeld);
            float memoryMultiplier = component.GetFactionMemoryMultiplier(record.OriginalFaction, now);
            return PrisonerNegotiationUtility.Evaluate(
                record,
                negotiator,
                demand.Clone(),
                reserve,
                materialCap,
                1,
                memoryMultiplier,
                component.GetNegotiationBudgetMultiplier(record));
        }
    }
}
