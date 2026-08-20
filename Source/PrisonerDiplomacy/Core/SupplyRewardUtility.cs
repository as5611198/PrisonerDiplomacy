using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace PrisonerDiplomacy
{
    public static class SupplyRewardUtility
    {
        public const int MaximumSupplyCount = 500;

        public static IReadOnlyList<ThingDef> AvailableSupplies(Faction faction)
        {
            List<ThingDef> results = new List<ThingDef>();
            Add(results, ThingDefOf.MedicineHerbal);
            AddNamed(results, "Pemmican");
            AddNamed(results, "MealSimple");
            AddNamed(results, "Kibble");

            TechLevel techLevel = faction?.def?.techLevel ?? TechLevel.Neolithic;
            if (techLevel >= TechLevel.Industrial)
            {
                Add(results, ThingDefOf.MedicineIndustrial);
                Add(results, ThingDefOf.ComponentIndustrial);
                AddNamed(results, "MealFine");
                Add(results, ThingDefOf.MealSurvivalPack);
                AddNamed(results, "Chemfuel");
                AddNamed(results, "Steel");
                AddNamed(results, "Cloth");
            }

            if (techLevel >= TechLevel.Spacer)
            {
                Add(results, ThingDefOf.MedicineUltratech);
                Add(results, ThingDefOf.ComponentSpacer);
                AddNamed(results, "MealLavish");
                AddNamed(results, "Plasteel");
                AddNamed(results, "Uranium");
                AddNamed(results, "Synthread");
                AddNamed(results, "Hyperweave");
            }

            return results;
        }

        public static bool IsAvailable(Faction faction, ThingDef thingDef)
        {
            return thingDef != null && AvailableSupplies(faction).Contains(thingDef);
        }

        public static int CalculateCost(Faction faction, ThingDef thingDef, int count)
        {
            if (!IsAvailable(faction, thingDef) || count <= 0)
            {
                return 0;
            }

            float difficulty = SupplyDifficulty(faction, thingDef);
            return Math.Max(1, (int)Math.Ceiling(thingDef.BaseMarketValue * count * difficulty * 1.15f));
        }

        private static float SupplyDifficulty(Faction faction, ThingDef thingDef)
        {
            TechLevel factionTech = faction?.def?.techLevel ?? TechLevel.Neolithic;
            if (thingDef == ThingDefOf.MedicineHerbal
                || thingDef.defName == "Pemmican"
                || thingDef.defName == "MealSimple"
                || thingDef.defName == "Kibble")
            {
                return factionTech <= TechLevel.Neolithic ? 0.85f : 1f;
            }

            if (thingDef.techLevel > factionTech)
            {
                return 1.40f;
            }

            if (thingDef == ThingDefOf.ComponentIndustrial
                || thingDef == ThingDefOf.MealSurvivalPack
                || thingDef.defName == "MealFine"
                || thingDef.defName == "Chemfuel")
            {
                return 0.85f;
            }

            return 1f;
        }

        private static void Add(List<ThingDef> list, ThingDef thingDef)
        {
            if (thingDef != null && !list.Contains(thingDef))
            {
                list.Add(thingDef);
            }
        }

        private static void AddNamed(List<ThingDef> list, string defName)
        {
            Add(list, DefDatabase<ThingDef>.GetNamedSilentFail(defName));
        }
    }
}
