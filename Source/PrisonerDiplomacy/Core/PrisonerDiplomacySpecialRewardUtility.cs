using System;
using System.Linq;
using RimWorld;
using Verse;

namespace PrisonerDiplomacy
{
    internal static class PrisonerDiplomacySpecialRewardUtility
    {
        public static bool TryPopulateDemand(
            Pawn prisoner,
            Faction faction,
            string rewardId,
            RewardDemand demand,
            out string reasonKey)
        {
            reasonKey = null;
            if (demand == null
                || !TryResolve(prisoner, faction, rewardId,
                    out PrisonerDiplomacySpecialRewardDefinition definition,
                    out ThingDef thingDef,
                    out reasonKey))
            {
                return false;
            }

            demand.SpecialRewardId = definition.RewardId;
            demand.SpecialRewardThingDef = thingDef;
            demand.SpecialRewardCount = definition.MinimumCount;
            return true;
        }

        public static bool ValidateSelection(
            Pawn prisoner,
            Faction faction,
            RewardDemand demand,
            out string reasonKey)
        {
            reasonKey = null;
            bool hasId = !string.IsNullOrWhiteSpace(demand?.SpecialRewardId);
            bool hasThing = demand?.SpecialRewardThingDef != null;
            bool hasCount = demand?.SpecialRewardCount > 0;
            if (!hasId && !hasThing && !hasCount)
            {
                return true;
            }
            if (!hasId || !hasThing || !hasCount)
            {
                reasonKey = "PD_NegotiationInvalidSpecialReward";
                return false;
            }

            if (!TryResolve(prisoner, faction, demand.SpecialRewardId,
                out PrisonerDiplomacySpecialRewardDefinition definition,
                out ThingDef thingDef,
                out reasonKey))
            {
                return false;
            }

            if (thingDef != demand.SpecialRewardThingDef
                || demand.SpecialRewardCount != definition.MinimumCount)
            {
                reasonKey = "PD_NegotiationInvalidSpecialReward";
                return false;
            }
            return true;
        }

        public static int CalculateCost(ThingDef thingDef, int count)
        {
            if (!IsDeliverableThing(thingDef) || count <= 0)
            {
                return 0;
            }
            return Math.Max(50,
                (int)Math.Ceiling(thingDef.BaseMarketValue * count * 1.15f / 10f) * 10);
        }

        public static string Label(RewardDemand demand)
        {
            if (demand == null || string.IsNullOrWhiteSpace(demand.SpecialRewardId))
            {
                return string.Empty;
            }

            PrisonerDiplomacySpecialRewardDefinition definition =
                PrisonerDiplomacyExtensionRegistry.RegisteredSpecialRewardDefinitions
                    .FirstOrDefault(item => item.RewardId == demand.SpecialRewardId);
            if (definition != null && !string.IsNullOrWhiteSpace(definition.LabelKey))
            {
                return definition.LabelKey.Translate();
            }
            return demand.SpecialRewardThingDef?.LabelCap ?? demand.SpecialRewardId;
        }

        private static bool TryResolve(
            Pawn prisoner,
            Faction faction,
            string rewardId,
            out PrisonerDiplomacySpecialRewardDefinition definition,
            out ThingDef thingDef,
            out string reasonKey)
        {
            definition = null;
            thingDef = null;
            reasonKey = null;
            if (prisoner == null || faction == null || string.IsNullOrWhiteSpace(rewardId))
            {
                reasonKey = "PD_NegotiationInvalidSpecialReward";
                return false;
            }

            definition = PrisonerDiplomacyExtensionRegistry.GetSpecialRewards(prisoner, faction)
                .FirstOrDefault(item => item.RewardId == rewardId);
            if (definition == null)
            {
                reasonKey = "PD_NegotiationSpecialRewardUnavailable";
                return false;
            }

            thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(definition.RequiredThingDefName);
            if (!IsDeliverableThing(thingDef)
                || definition.MinimumCount <= 0
                || definition.MinimumCount > SupplyRewardUtility.MaximumSupplyCount)
            {
                reasonKey = "PD_NegotiationInvalidSpecialReward";
                return false;
            }
            return true;
        }

        private static bool IsDeliverableThing(ThingDef thingDef)
        {
            return thingDef != null
                && thingDef.category == ThingCategory.Item
                && thingDef.stackLimit > 0
                && thingDef.BaseMarketValue > 0f;
        }
    }
}
