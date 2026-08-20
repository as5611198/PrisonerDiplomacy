using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace PrisonerDiplomacy
{
    public static class PrisonerExchangeUtility
    {
        public static IReadOnlyList<Pawn> AvailableHostages(Faction faction)
        {
            if (faction?.kidnapped == null)
            {
                return new List<Pawn>();
            }

            return faction.kidnapped.KidnappedPawnsListForReading
                .Where(IsValidHostage)
                .OrderByDescending(CalculateHostageCost)
                .ThenBy(pawn => pawn.LabelShort)
                .ToList();
        }

        public static bool IsHeldByFaction(Faction faction, Pawn hostage)
        {
            return faction?.kidnapped != null
                && IsValidHostage(hostage)
                && faction.kidnapped.KidnappedPawnsListForReading.Contains(hostage);
        }

        public static int CalculateHostageCost(Pawn hostage)
        {
            if (!IsValidHostage(hostage))
            {
                return 0;
            }

            PrisonerImportance importance = PrisonerValueCalculator.Classify(hostage, Faction.OfPlayer);
            int value = PrisonerValueCalculator.Calculate(hostage, Math.Max(400f, hostage.MarketValue), importance);
            return RoundTo50((int)Math.Ceiling(value * 1.25f));
        }

        public static int CalculateCompensation(PrisonerRecord offeredPrisoner, Pawn hostage)
        {
            return Math.Max(0, CalculateHostageCost(hostage) - Math.Max(0, offeredPrisoner?.DiplomaticValue ?? 0));
        }

        public static int CalculateSupplyCount(Faction faction, ThingDef thingDef, int compensation)
        {
            if (compensation <= 0)
            {
                return 0;
            }

            int unitCost = Math.Max(1, SupplyRewardUtility.CalculateCost(faction, thingDef, 1));
            return Math.Max(1, (int)Math.Ceiling(compensation / (float)unitCost));
        }

        public static IReadOnlyList<ThingDef> AvailableCompensationSupplies(Faction faction, int compensation)
        {
            if (compensation <= 0)
            {
                return new List<ThingDef>();
            }

            return SupplyRewardUtility.AvailableSupplies(faction)
                .Where(def => CalculateSupplyCount(faction, def, compensation)
                    <= SupplyRewardUtility.MaximumSupplyCount)
                .ToList();
        }

        public static bool TryChargeSilver(Map map, int amount)
        {
            return TryChargeThings(map, ThingDefOf.Silver, amount);
        }

        public static bool TryRefundSilver(Map map, int amount, out IntVec3 dropCell)
        {
            dropCell = IntVec3.Invalid;
            if (amount <= 0)
            {
                return true;
            }

            if (map == null || !Find.Maps.Contains(map))
            {
                return false;
            }

            List<Thing> stacks = new List<Thing>();
            int remaining = amount;
            while (remaining > 0)
            {
                Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
                silver.stackCount = Math.Min(remaining, ThingDefOf.Silver.stackLimit);
                stacks.Add(silver);
                remaining -= silver.stackCount;
            }

            dropCell = DropCellFinder.TradeDropSpot(map);
            DropPodUtility.DropThingsNear(dropCell, map, stacks, forbid: false, faction: null);
            return true;
        }

        public static bool HasTradeableThings(Map map, ThingDef thingDef, int amount)
        {
            return amount <= 0 || GetTradeableThings(map, thingDef).Sum(thing => thing.stackCount) >= amount;
        }

        public static bool TryChargeThings(Map map, ThingDef thingDef, int amount)
        {
            if (amount <= 0)
            {
                return true;
            }

            List<Thing> things = GetTradeableThings(map, thingDef);
            if (things.Sum(thing => thing.stackCount) < amount)
            {
                return false;
            }

            int remaining = amount;
            foreach (Thing thing in things)
            {
                int take = Math.Min(remaining, thing.stackCount);
                if (take == thing.stackCount)
                {
                    thing.Destroy();
                }
                else
                {
                    Thing split = thing.SplitOff(take);
                    split.Destroy();
                }

                remaining -= take;
                if (remaining <= 0)
                {
                    break;
                }
            }

            return remaining == 0;
        }

        public static bool TryRefundThings(Map map, ThingDef thingDef, int amount, out IntVec3 dropCell)
        {
            dropCell = IntVec3.Invalid;
            if (amount <= 0)
            {
                return true;
            }

            if (map == null || thingDef == null || !Find.Maps.Contains(map))
            {
                return false;
            }

            List<Thing> stacks = new List<Thing>();
            int remaining = amount;
            while (remaining > 0)
            {
                Thing stack = ThingMaker.MakeThing(thingDef);
                stack.stackCount = Math.Min(remaining, thingDef.stackLimit);
                stacks.Add(stack);
                remaining -= stack.stackCount;
            }

            dropCell = DropCellFinder.TradeDropSpot(map);
            DropPodUtility.DropThingsNear(dropCell, map, stacks, forbid: false, faction: null);
            return true;
        }

        public static bool TryReturnHostage(Faction faction, Pawn hostage, Map map, out IntVec3 arrivalCell)
        {
            arrivalCell = IntVec3.Invalid;
            if (!IsHeldByFaction(faction, hostage) || map == null || !Find.Maps.Contains(map))
            {
                return false;
            }

            faction.kidnapped.RemoveKidnappedPawn(hostage);
            if (Find.WorldPawns.Contains(hostage))
            {
                Find.WorldPawns.RemovePawn(hostage);
            }
            if (hostage.Faction != Faction.OfPlayer)
            {
                hostage.SetFaction(Faction.OfPlayer);
            }

            arrivalCell = DropCellFinder.TradeDropSpot(map);
            ActiveTransporterInfo podInfo = new ActiveTransporterInfo();
            if (!podInfo.GetDirectlyHeldThings().TryAdd(hostage))
            {
                return false;
            }
            DropPodUtility.MakeDropPodAt(arrivalCell, map, podInfo, null);
            return true;
        }

        private static bool IsValidHostage(Pawn pawn)
        {
            return pawn != null
                && !pawn.Dead
                && !pawn.Destroyed
                && (pawn.HomeFaction == Faction.OfPlayer || pawn.Faction == Faction.OfPlayer)
                && !pawn.Spawned;
        }

        internal static Pawn CreateSmokeTestHostage(Faction faction, PawnKindDef pawnKind)
        {
            Pawn hostage = PawnGenerator.GeneratePawn(pawnKind, Faction.OfPlayer);
            hostage.ForceSetStateToUnspawned();
            faction.kidnapped.Kidnap(hostage, null);
            if (!Find.WorldPawns.Contains(hostage))
            {
                Find.WorldPawns.PassToWorld(hostage, PawnDiscardDecideMode.KeepForever);
            }
            return hostage;
        }

        private static List<Thing> GetTradeableThings(Map map, ThingDef thingDef)
        {
            if (map == null || thingDef == null)
            {
                return new List<Thing>();
            }

            return map.listerThings.ThingsOfDef(thingDef)
                .Where(thing => thing.Spawned
                    && !thing.IsForbidden(Faction.OfPlayer))
                .OrderBy(thing => thing.stackCount)
                .ToList();
        }

        internal static int CountAvailableThings(Map map, ThingDef thingDef)
        {
            return GetTradeableThings(map, thingDef).Sum(thing => thing.stackCount);
        }

        private static int RoundTo50(int value)
        {
            return Math.Max(0, (int)Math.Ceiling(value / 50f) * 50);
        }
    }
}
