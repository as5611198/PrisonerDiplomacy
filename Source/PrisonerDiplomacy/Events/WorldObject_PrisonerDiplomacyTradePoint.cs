using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace PrisonerDiplomacy
{
    /// <summary>
    /// Persistent world-map handoff point for a neutral prisoner exchange.
    /// The event component remains authoritative; this object only routes a
    /// Caravan arrival back into that state machine.
    /// </summary>
    public sealed class WorldObject_PrisonerDiplomacyTradePoint : WorldObject
    {
        public string EventId;

        public override string Label => "PD_WorldTradePointLabel".Translate();

        public override string GetInspectString()
        {
            PrisonerDiplomacyEventRecord record = PrisonerDiplomacyGameComponent.Current?.GetDiplomacyEvent(EventId);
            if (record == null)
            {
                return base.GetInspectString();
            }

            string baseInspectString = base.GetInspectString();
            string eventInspectString = "PD_WorldTradePointInspect".Translate(
                record.PrisonerLabel ?? record.PrisonerLoadId ?? "?",
                record.Faction?.Name ?? "?");
            return string.IsNullOrWhiteSpace(baseInspectString)
                ? eventInspectString
                : baseInspectString + "\n" + eventInspectString;
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Caravan caravan)
        {
            foreach (FloatMenuOption option in base.GetFloatMenuOptions(caravan))
            {
                yield return option;
            }

            foreach (FloatMenuOption option in CaravanArrivalAction_PrisonerDiplomacyTradePoint
                .GetFloatMenuOptions(caravan, this))
            {
                yield return option;
            }
        }

        public void Notify_CaravanArrived(Caravan caravan)
        {
            PrisonerDiplomacyGameComponent.Current?.NotifyNeutralTradePointArrived(EventId, caravan);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref EventId, "eventId");
        }
    }

    public sealed class CaravanArrivalAction_PrisonerDiplomacyTradePoint : CaravanArrivalAction
    {
        private WorldObject_PrisonerDiplomacyTradePoint tradePoint;

        public override string Label => "PD_WorldTradePointVisit".Translate(tradePoint?.Label ?? "?");

        public override string ReportString => "CaravanVisiting".Translate(tradePoint?.Label ?? "?");

        public CaravanArrivalAction_PrisonerDiplomacyTradePoint()
        {
        }

        public CaravanArrivalAction_PrisonerDiplomacyTradePoint(
            WorldObject_PrisonerDiplomacyTradePoint tradePoint)
        {
            this.tradePoint = tradePoint;
        }

        public static FloatMenuAcceptanceReport CanVisit(
            Caravan caravan,
            WorldObject_PrisonerDiplomacyTradePoint tradePoint)
        {
            return tradePoint != null
                && tradePoint.Spawned
                && caravan != null
                && caravan.Spawned;
        }

        public override FloatMenuAcceptanceReport StillValid(Caravan caravan, PlanetTile destinationTile)
        {
            FloatMenuAcceptanceReport report = base.StillValid(caravan, destinationTile);
            if (!(bool)report)
            {
                return report;
            }

            if (tradePoint == null || tradePoint.Tile != destinationTile)
            {
                return false;
            }

            return CanVisit(caravan, tradePoint);
        }

        public override void Arrived(Caravan caravan)
        {
            tradePoint?.Notify_CaravanArrived(caravan);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref tradePoint, "tradePoint");
        }

        public static IEnumerable<FloatMenuOption> GetFloatMenuOptions(
            Caravan caravan,
            WorldObject_PrisonerDiplomacyTradePoint tradePoint)
        {
            return CaravanArrivalActionUtility.GetFloatMenuOptions<
                CaravanArrivalAction_PrisonerDiplomacyTradePoint>(
                () => CanVisit(caravan, tradePoint),
                () => new CaravanArrivalAction_PrisonerDiplomacyTradePoint(tradePoint),
                "PD_WorldTradePointVisit".Translate(tradePoint?.Label ?? "?"),
                caravan,
                tradePoint.Tile,
                tradePoint);
        }
    }
}
