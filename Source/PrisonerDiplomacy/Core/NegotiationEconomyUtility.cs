using System;
using System.Linq;
using RimWorld;
using Verse;

namespace PrisonerDiplomacy
{
    public static class NegotiationEconomyUtility
    {
        public const int MaximumGoodwill = 20;
        public const int MaximumRewardTypes = 3;
        public const int MinimumCeasefireDays = 5;
        public const int MaximumCeasefireDays = 30;
        public const int EarlyWarningIntelCost = 900;
        public const int ReserveRecoveryDays = 45;

        public static int CalculateDemandCost(Faction faction, RewardDemand demand)
        {
            if (demand == null)
            {
                return 0;
            }

            return Math.Max(0, demand.Silver)
                + SupplyRewardUtility.CalculateCost(faction, demand.SupplyDef, demand.SupplyCount)
                + PrisonerDiplomacySpecialRewardUtility.CalculateCost(
                    demand.SpecialRewardThingDef,
                    demand.SpecialRewardCount)
                + CalculateGoodwillCost(demand.Goodwill)
                + CalculateCeasefireCost(demand.CeasefireDays)
                + (demand.EarlyWarningIntel ? EarlyWarningIntelCost : 0);
        }

        public static int CalculateMaterialCost(Faction faction, RewardDemand demand)
        {
            if (demand == null)
            {
                return 0;
            }

            return Math.Max(0, demand.Silver)
                + SupplyRewardUtility.CalculateCost(faction, demand.SupplyDef, demand.SupplyCount)
                + PrisonerDiplomacySpecialRewardUtility.CalculateCost(
                    demand.SpecialRewardThingDef,
                    demand.SpecialRewardCount);
        }

        public static int CalculateGoodwillCost(int goodwill)
        {
            int value = Math.Max(0, Math.Min(MaximumGoodwill, goodwill));
            return 60 * value + 8 * value * value;
        }

        public static int CalculateCeasefireCost(int days)
        {
            int value = Math.Max(0, Math.Min(MaximumCeasefireDays, days));
            return value <= 0 ? 0 : (int)Math.Round(500f + 55f * value + 1.5f * value * value);
        }

        public static int CalculateNegotiationBudget(PrisonerRecord record, Pawn negotiator, float memoryMultiplier = 1f)
        {
            int baseValue = Math.Max(PrisonerNegotiationUtility.MinimumDemand, record?.DiplomaticValue ?? 0);
            Faction faction = record?.OriginalFaction;
            float willingness = FactionNegotiationUtility.IsTransactional(faction)
                ? FactionNegotiationUtility.PirateIdentityWillingness(record?.Importance ?? PrisonerImportance.Regular)
                : 1f;
            float socialMultiplier = Math.Max(0.90f, Math.Min(1.18f,
                0.90f + 0.014f * PrisonerNegotiationUtility.GetSocialSkill(negotiator)));
            float goodwillMultiplier = FactionNegotiationUtility.IsTransactional(faction)
                ? 1f
                : Math.Max(0.90f, Math.Min(1.05f, 1f + (faction?.PlayerGoodwill ?? 0) / 1000f));
            return Math.Max(PrisonerNegotiationUtility.MinimumDemand,
                (int)Math.Round(baseValue * PrisonerDiplomacyTuning.RansomValueMultiplier
                    * willingness * socialMultiplier * goodwillMultiplier
                    * Math.Max(0.65f, Math.Min(1.25f, memoryMultiplier)) / 50f) * 50);
        }

        public static int CalculateMaterialRewardCap(Map map)
        {
            float wealth = map?.PlayerWealthForStoryteller ?? 0f;
            float daysPassed = Find.TickManager?.TicksGame / 60000f ?? 0f;
            return (int)Math.Round(Math.Max(2000f, Math.Min(12000f, 1500f + 0.03f * wealth + 12f * daysPassed)) / 50f) * 50;
        }

        public static float CalculateMaximumReserve(Faction faction)
        {
            int settlementCount = Find.WorldObjects?.Settlements.Count(settlement => settlement.Faction == faction) ?? 0;
            float techMultiplier;
            switch (faction?.def?.techLevel ?? TechLevel.Neolithic)
            {
                case TechLevel.Medieval:
                    techMultiplier = 0.90f;
                    break;
                case TechLevel.Industrial:
                    techMultiplier = 1f;
                    break;
                case TechLevel.Spacer:
                case TechLevel.Ultra:
                case TechLevel.Archotech:
                    techMultiplier = 1.25f;
                    break;
                default:
                    techMultiplier = 0.75f;
                    break;
            }

            return Math.Max(2000f, Math.Min(15000f, 1000f + 1000f * Math.Max(1, settlementCount) * techMultiplier));
        }

        public static bool CanRequestGoodwill(Faction faction)
        {
            return faction != null
                && faction != Faction.OfPlayer
                && faction.CanEverGiveGoodwillRewards
                && FactionNegotiationUtility.GetType(faction) == FactionNegotiationType.Diplomatic;
        }

        public static bool IsDemandValid(
            Faction faction,
            RewardDemand demand,
            out string reasonKey,
            Pawn prisoner = null)
        {
            reasonKey = null;
            if (demand == null || demand.IsEmpty)
            {
                reasonKey = "PD_NegotiationNoRewards";
                return false;
            }

            if (demand.RewardTypeCount > MaximumRewardTypes)
            {
                reasonKey = "PD_NegotiationTooManyRewards";
                return false;
            }

            if ((demand.CeasefireDays > 0 || demand.EarlyWarningIntel)
                && PrisonerDiplomacyMod.Settings?.EnableStrategicConsequences == false)
            {
                reasonKey = "PD_NegotiationStrategicDisabled";
                return false;
            }

            if (demand.Silver < 0 || demand.Silver > PrisonerNegotiationUtility.MaximumDemand
                || demand.SupplyCount < 0 || demand.SupplyCount > SupplyRewardUtility.MaximumSupplyCount
                || demand.Goodwill < 0 || demand.Goodwill > MaximumGoodwill
                || demand.CeasefireDays < 0 || demand.CeasefireDays > MaximumCeasefireDays
                || demand.CeasefireDays > 0 && demand.CeasefireDays < MinimumCeasefireDays)
            {
                reasonKey = "PD_NegotiationInvalidRewards";
                return false;
            }

            if ((demand.SupplyDef == null) != (demand.SupplyCount <= 0)
                || (demand.SupplyDef != null && !SupplyRewardUtility.IsAvailable(faction, demand.SupplyDef)))
            {
                reasonKey = "PD_NegotiationInvalidSupplies";
                return false;
            }

            if (!PrisonerDiplomacySpecialRewardUtility.ValidateSelection(
                prisoner,
                faction,
                demand,
                out reasonKey))
            {
                return false;
            }

            if (demand.Goodwill > 0 && !CanRequestGoodwill(faction))
            {
                reasonKey = "PD_NegotiationGoodwillUnavailable";
                return false;
            }

            if (demand.Goodwill > 0 && demand.Goodwill > Math.Max(0, 100 - faction.PlayerGoodwill))
            {
                reasonKey = "PD_NegotiationGoodwillTooHigh";
                return false;
            }

            return true;
        }

        public static RewardDemand ScaleDemandToCost(Faction faction, RewardDemand requested, int targetCost)
        {
            RewardDemand result = requested?.Clone() ?? new RewardDemand();
            int originalCost = CalculateDemandCost(faction, result);
            if (originalCost <= targetCost || originalCost <= 0)
            {
                return result;
            }

            float scale = Math.Max(0f, Math.Min(1f, targetCost / (float)originalCost));
            result.Silver = RoundTo50((int)Math.Floor(result.Silver * scale));
            result.SupplyCount = Math.Max(0, (int)Math.Floor(result.SupplyCount * scale));
            result.Goodwill = Math.Max(0, (int)Math.Floor(result.Goodwill * scale));
            if (result.CeasefireDays > 0)
            {
                result.CeasefireDays = Math.Max(MinimumCeasefireDays,
                    (int)Math.Floor(result.CeasefireDays * scale));
            }
            Normalize(result);

            while (CalculateDemandCost(faction, result) > targetCost)
            {
                if (result.Silver >= 50)
                {
                    result.Silver -= 50;
                }
                else if (result.SupplyCount > 0)
                {
                    result.SupplyCount--;
                }
                else if (result.Goodwill > 0)
                {
                    result.Goodwill--;
                }
                else if (result.CeasefireDays > MinimumCeasefireDays)
                {
                    result.CeasefireDays--;
                }
                else if (!string.IsNullOrWhiteSpace(result.SpecialRewardId))
                {
                    ClearSpecialReward(result);
                }
                else if (result.EarlyWarningIntel)
                {
                    result.EarlyWarningIntel = false;
                }
                else if (result.CeasefireDays > 0)
                {
                    result.CeasefireDays = 0;
                }
                else
                {
                    break;
                }

                Normalize(result);
            }

            return result;
        }

        public static RewardDemand EnforceMaterialCap(Faction faction, RewardDemand requested, int materialCap)
        {
            RewardDemand result = requested?.Clone() ?? new RewardDemand();
            while (CalculateMaterialCost(faction, result) > materialCap)
            {
                if (result.Silver >= 50)
                {
                    result.Silver -= 50;
                }
                else if (result.SupplyCount > 0)
                {
                    result.SupplyCount--;
                }
                else if (!string.IsNullOrWhiteSpace(result.SpecialRewardId))
                {
                    ClearSpecialReward(result);
                }
                else
                {
                    break;
                }

                Normalize(result);
            }

            return result;
        }

        public static bool TryCreateCounterRevision(
            RewardDemand counterOffer,
            int additionalSilver,
            out RewardDemand revision)
        {
            revision = counterOffer?.Clone();
            if (revision == null || revision.IsEmpty || additionalSilver < 0)
            {
                return false;
            }

            if (additionalSilver == 0)
            {
                return true;
            }

            if (revision.Silver <= 0 && revision.RewardTypeCount >= MaximumRewardTypes)
            {
                revision = null;
                return false;
            }

            int revisedSilver = revision.Silver + additionalSilver;
            if (revisedSilver > PrisonerNegotiationUtility.MaximumDemand)
            {
                revision = null;
                return false;
            }

            revision.Silver = revisedSilver;
            return true;
        }

        private static int RoundTo50(int value)
        {
            return Math.Max(0, (int)Math.Round(value / 50f) * 50);
        }

        private static void Normalize(RewardDemand demand)
        {
            if (demand.SupplyCount <= 0)
            {
                demand.SupplyCount = 0;
                demand.SupplyDef = null;
            }

            if (demand.CeasefireDays > 0 && demand.CeasefireDays < MinimumCeasefireDays)
            {
                demand.CeasefireDays = 0;
            }

            if (string.IsNullOrWhiteSpace(demand.SpecialRewardId)
                || demand.SpecialRewardThingDef == null
                || demand.SpecialRewardCount <= 0)
            {
                ClearSpecialReward(demand);
            }
        }

        private static void ClearSpecialReward(RewardDemand demand)
        {
            demand.SpecialRewardId = null;
            demand.SpecialRewardThingDef = null;
            demand.SpecialRewardCount = 0;
        }

        public static RewardDemand CreateSaferPirateTerms(Faction faction, RewardDemand current)
        {
            int currentCost = CalculateDemandCost(faction, current);
            if (currentCost <= 0)
            {
                return null;
            }

            RewardDemand safer = ScaleDemandToCost(faction, current, (int)Math.Floor(currentCost * 0.80f));
            return safer != null && !safer.IsEmpty && CalculateDemandCost(faction, safer) < currentCost
                ? safer
                : null;
        }
    }
}
