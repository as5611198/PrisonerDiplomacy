using System;
using RimWorld;
using Verse;

namespace PrisonerDiplomacy
{
    internal static class AiNegotiationAdvisoryUtility
    {
        public static bool TryApply(
            PrisonerDeal deal,
            AiNegotiationAdvisory advisory,
            int reserveAvailable,
            int materialCap,
            out RewardDemand adjusted,
            out string summary)
        {
            adjusted = null;
            summary = null;
            if (deal == null || deal.State != DealState.Negotiating || deal.Rewards == null || advisory == null)
            {
                return false;
            }

            float urgency = SignalValue(advisory.Urgency,
                new[] { "critical", "high", "normal", "low" },
                new[] { 0.08f, 0.04f, 0f, -0.03f });
            float concession = SignalValue(advisory.Concession,
                new[] { "high", "medium", "low" },
                new[] { 0.08f, 0.03f, -0.03f });
            float leverage = SignalValue(advisory.LeverageResponse,
                new[] { "threatened", "neutral", "conciliatory" },
                new[] { 0.04f, 0f, -0.04f });
            float modifier = Math.Max(-0.10f, Math.Min(0.20f, urgency + concession + leverage));
            int currentCost = NegotiationEconomyUtility.CalculateDemandCost(deal.Faction, deal.Rewards);
            if (currentCost <= 0)
            {
                return false;
            }

            int targetCost = Math.Max(1, (int)Math.Round(currentCost * (1f + modifier) / 50f) * 50);
            targetCost = Math.Min(targetCost, Math.Max(0, reserveAvailable));
            adjusted = NegotiationEconomyUtility.ScaleDemandToCost(deal.Faction, deal.Rewards, targetCost);
            adjusted = NegotiationEconomyUtility.EnforceMaterialCap(deal.Faction, adjusted, materialCap);
            if (adjusted == null
                || adjusted.IsEmpty
                || !NegotiationEconomyUtility.IsDemandValid(
                    deal.Faction,
                    adjusted,
                    out _,
                    deal.Prisoner))
            {
                adjusted = null;
                return false;
            }

            int adjustedCost = NegotiationEconomyUtility.CalculateDemandCost(deal.Faction, adjusted);
            if (adjustedCost == currentCost)
            {
                adjusted = null;
                return false;
            }

            summary = string.Format(
                "urgency={0}; concession={1}; leverage={2}; cost={3}->{4}",
                advisory.Urgency ?? "none",
                advisory.Concession ?? "none",
                advisory.LeverageResponse ?? "none",
                currentCost,
                adjustedCost);
            return true;
        }

        private static float SignalValue(string value, string[] names, float[] values)
        {
            string normalized = value?.Trim().ToLowerInvariant();
            for (int index = 0; index < names.Length; index++)
            {
                if (string.Equals(normalized, names[index], StringComparison.Ordinal))
                {
                    return values[index];
                }
            }

            return 0f;
        }
    }
}
