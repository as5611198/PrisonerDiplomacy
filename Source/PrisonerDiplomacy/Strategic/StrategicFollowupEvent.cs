using RimWorld;
using Verse;

namespace PrisonerDiplomacy
{
    public sealed class StrategicFollowupEvent : IExposable
    {
        public string EventId;
        public StrategicFollowupKind Kind;
        public Faction Faction;
        public Map Map;
        public Pawn SourcePawn;
        public string SourcePawnLoadId;
        public string SourcePawnLabel;
        public string SourceDealId;
        public int TriggerTick;
        public int Attempts;
        public bool Triggered;

        public void ExposeData()
        {
            Scribe_Values.Look(ref EventId, "eventId");
            Scribe_Values.Look(ref Kind, "kind", StrategicFollowupKind.PositiveGift);
            Scribe_References.Look(ref Faction, "faction");
            Scribe_References.Look(ref Map, "map");
            Scribe_References.Look(ref SourcePawn, "sourcePawn", true);
            Scribe_Values.Look(ref SourcePawnLoadId, "sourcePawnLoadId");
            Scribe_Values.Look(ref SourcePawnLabel, "sourcePawnLabel");
            Scribe_Values.Look(ref SourceDealId, "sourceDealId");
            Scribe_Values.Look(ref TriggerTick, "triggerTick");
            Scribe_Values.Look(ref Attempts, "attempts");
            Scribe_Values.Look(ref Triggered, "triggered");
        }
    }
}
