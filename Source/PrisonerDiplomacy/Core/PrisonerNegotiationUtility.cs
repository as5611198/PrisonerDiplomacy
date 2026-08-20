using System;
using RimWorld;
using Verse;

namespace PrisonerDiplomacy
{
    public static class PrisonerNegotiationUtility
    {
        public const int MinimumDemand = 100;
        public const int MaximumDemand = 12000;
        public const float MaximumCounterableRatio = 1.85f;

        public static NegotiationResult Evaluate(PrisonerRecord record, Pawn negotiator, int demand)
        {
            return Evaluate(record, negotiator, new RewardDemand { Silver = demand }, int.MaxValue, int.MaxValue, 1);
        }

        public static NegotiationResult Evaluate(
            PrisonerRecord record,
            Pawn negotiator,
            RewardDemand requestedRewards,
            int reserveAvailable,
            int materialRewardCap,
            int negotiationRound,
            float memoryMultiplier = 1f,
            float budgetMultiplier = 1f)
        {
            int fairValue = Math.Max(MinimumDemand, record?.DiplomaticValue ?? MinimumDemand);
            int socialSkill = GetSocialSkill(negotiator);
            RewardDemand demand = requestedRewards?.Clone() ?? new RewardDemand();
            int demandCost = NegotiationEconomyUtility.CalculateDemandCost(record?.OriginalFaction, demand);
            int baseBudget = NegotiationEconomyUtility.CalculateNegotiationBudget(record, negotiator, memoryMultiplier);
            int budget = Math.Max(0, Math.Min(
                (int)Math.Round(baseBudget * Math.Max(0.65f, Math.Min(1.50f, budgetMultiplier))),
                reserveAvailable));
            float ratio = demandCost / (float)Math.Max(1, budget);
            float chance = CalculateAcceptanceChance(ratio, socialSkill);
            int seed = BuildSeed(record, negotiator, demandCost, negotiationRound);
            bool exceedsMaterialCap = NegotiationEconomyUtility.CalculateMaterialCost(
                record?.OriginalFaction,
                demand) > materialRewardCap;
            NegotiationOutcome outcome;
            if (ratio > MaximumCounterableRatio)
            {
                outcome = NegotiationOutcome.Rejected;
                chance = 0f;
            }
            else if (!exceedsMaterialCap
                && (ratio <= 0.90f || (ratio <= 1.05f && Rand.ChanceSeeded(chance, seed))))
            {
                outcome = NegotiationOutcome.Accepted;
            }
            else
            {
                outcome = NegotiationOutcome.Countered;
            }

            RewardDemand counterOffer = null;
            if (outcome == NegotiationOutcome.Countered)
            {
                float counterRatio;
                if (negotiationRound >= 2)
                {
                    counterRatio = 1f;
                }
                else
                {
                    Rand.PushState(seed);
                    counterRatio = Rand.Range(0.88f, 0.98f);
                    Rand.PopState();
                }
                int nonMaterialCost = NegotiationEconomyUtility.CalculateGoodwillCost(demand.Goodwill)
                    + NegotiationEconomyUtility.CalculateCeasefireCost(demand.CeasefireDays)
                    + (demand.EarlyWarningIntel ? NegotiationEconomyUtility.EarlyWarningIntelCost : 0);
                int counterTarget = Math.Min(materialRewardCap + nonMaterialCost,
                    (int)Math.Floor(budget * counterRatio));
                counterOffer = NegotiationEconomyUtility.ScaleDemandToCost(record?.OriginalFaction, demand, counterTarget);
                counterOffer = NegotiationEconomyUtility.EnforceMaterialCap(record?.OriginalFaction, counterOffer, materialRewardCap);
                if (counterOffer.IsEmpty)
                {
                    outcome = NegotiationOutcome.Rejected;
                    counterOffer = null;
                }
            }

            return new NegotiationResult
            {
                Demand = demand.Silver,
                RequestedRewards = demand,
                CounterOffer = counterOffer,
                DemandCost = demandCost,
                NegotiationBudget = budget,
                MaterialRewardCap = materialRewardCap,
                ReserveAvailable = reserveAvailable,
                NegotiationRound = negotiationRound,
                FairValue = fairValue,
                SocialSkill = socialSkill,
                AcceptanceChance = chance,
                Assessment = AssessDemand(ratio, socialSkill),
                Outcome = outcome,
                Seed = seed
            };
        }

        public static int SuggestedDemand(PrisonerRecord record)
        {
            int fairValue = Math.Max(MinimumDemand, record?.DiplomaticValue ?? MinimumDemand);
            return Math.Max(MinimumDemand, Math.Min(MaximumDemand, (int)Math.Round(fairValue / 50f) * 50));
        }

        public static bool TryParseDemand(string input, out int demand)
        {
            return int.TryParse(input, out demand)
                && demand >= MinimumDemand
                && demand <= MaximumDemand;
        }

        public static string AssessmentLabel(DemandAssessment assessment)
        {
            switch (assessment)
            {
                case DemandAssessment.VeryFavorable: return "PD_AssessmentVeryFavorable".Translate();
                case DemandAssessment.Reasonable: return "PD_AssessmentReasonable".Translate();
                case DemandAssessment.Ambitious: return "PD_AssessmentAmbitious".Translate();
                default: return "PD_AssessmentExtreme".Translate();
            }
        }

        public static int GetSocialSkill(Pawn negotiator)
        {
            if (negotiator?.skills == null || negotiator.WorkTagIsDisabled(WorkTags.Social))
            {
                return 0;
            }

            return negotiator.skills.GetSkill(SkillDefOf.Social)?.Level ?? 0;
        }

        private static float CalculateAcceptanceChance(float ratio, int socialSkill)
        {
            float chance;
            if (ratio <= 0.75f)
            {
                chance = 0.97f;
            }
            else if (ratio <= 1f)
            {
                chance = 0.90f - (ratio - 0.75f) * 0.40f;
            }
            else if (ratio <= 1.35f)
            {
                chance = 0.80f - (ratio - 1f) * 1.15f;
            }
            else if (ratio <= 1.75f)
            {
                chance = 0.40f - (ratio - 1.35f) * 0.75f;
            }
            else
            {
                chance = 0.08f;
            }

            chance += (socialSkill - 8) * 0.0125f;
            return Math.Max(0.03f, Math.Min(0.98f, chance));
        }

        private static DemandAssessment AssessDemand(float ratio, int socialSkill)
        {
            float insight = Math.Max(0f, Math.Min(1f, socialSkill / 20f));
            float uncertainty = (1f - insight) * 0.15f;
            if (ratio <= 0.78f + uncertainty)
            {
                return DemandAssessment.VeryFavorable;
            }

            if (ratio <= 1.12f + uncertainty)
            {
                return DemandAssessment.Reasonable;
            }

            if (ratio <= 1.55f + uncertainty)
            {
                return DemandAssessment.Ambitious;
            }

            return DemandAssessment.Extreme;
        }

        private static int BuildSeed(PrisonerRecord record, Pawn negotiator, int demandCost, int negotiationRound)
        {
            int seed = GenText.StableStringHash(record?.PawnLoadId ?? "missing-prisoner");
            seed = Gen.HashCombineInt(seed, record?.CapturedTick ?? 0);
            seed = Gen.HashCombineInt(seed, negotiator != null ? GenText.StableStringHash(negotiator.GetUniqueLoadID()) : 0);
            seed = Gen.HashCombineInt(seed, demandCost);
            seed = Gen.HashCombineInt(seed, negotiationRound);
            seed = Gen.HashCombineInt(seed, record?.NegotiationCount ?? 0);
            return seed;
        }
    }
}
