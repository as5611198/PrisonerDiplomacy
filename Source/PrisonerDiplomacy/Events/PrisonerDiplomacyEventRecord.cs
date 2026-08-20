using RimWorld;
using RimWorld.Planet;
using Verse;

namespace PrisonerDiplomacy
{
    public enum PrisonerDiplomacyEventState
    {
        Scheduled,
        Offered,
        Active,
        Completed,
        Failed,
        Cancelled
    }

    public sealed class PrisonerDiplomacyEventRecord : IExposable
    {
        public string EventId;
        public string DefinitionId;
        public string ExtensionId;
        public PrisonerDiplomacyEventKind Kind;
        public PrisonerDiplomacyEventState State;
        public Faction Faction;
        public Faction IntermediaryFaction;
        public Map Map;
        public WorldObject NeutralTradePoint;
        public PlanetTile NeutralTradeTile;
        public bool WorldTradePointRequested;
        public Pawn Prisoner;
        public string PrisonerLoadId;
        public string PrisonerLabel;
        public string SourceDealId;
        public int CreatedTick;
        public int TriggerTick;
        public int Attempts;
        public int Stage;
        public int StageStartedTick = -1;
        public bool PlayerAccepted;
        public bool OutcomeApplied;

        public bool IsActive => State == PrisonerDiplomacyEventState.Scheduled
            || State == PrisonerDiplomacyEventState.Offered
            || State == PrisonerDiplomacyEventState.Active;

        public void ExposeData()
        {
            Scribe_Values.Look(ref EventId, "eventId");
            Scribe_Values.Look(ref DefinitionId, "definitionId");
            Scribe_Values.Look(ref ExtensionId, "extensionId");
            Scribe_Values.Look(ref Kind, "kind", PrisonerDiplomacyEventKind.NeutralTradeCaravan);
            Scribe_Values.Look(ref State, "state", PrisonerDiplomacyEventState.Scheduled);
            Scribe_References.Look(ref Faction, "faction");
            Scribe_References.Look(ref IntermediaryFaction, "intermediaryFaction");
            Scribe_References.Look(ref Map, "map");
            Scribe_References.Look(ref NeutralTradePoint, "neutralTradePoint");
            Scribe_Values.Look(ref NeutralTradeTile, "neutralTradeTile");
            Scribe_Values.Look(ref WorldTradePointRequested, "worldTradePointRequested");
            Scribe_References.Look(ref Prisoner, "prisoner", true);
            Scribe_Values.Look(ref PrisonerLoadId, "prisonerLoadId");
            Scribe_Values.Look(ref PrisonerLabel, "prisonerLabel");
            Scribe_Values.Look(ref SourceDealId, "sourceDealId");
            Scribe_Values.Look(ref CreatedTick, "createdTick");
            Scribe_Values.Look(ref TriggerTick, "triggerTick");
            Scribe_Values.Look(ref Attempts, "attempts");
            Scribe_Values.Look(ref Stage, "stage");
            Scribe_Values.Look(ref StageStartedTick, "stageStartedTick", -1);
            Scribe_Values.Look(ref PlayerAccepted, "playerAccepted");
            Scribe_Values.Look(ref OutcomeApplied, "outcomeApplied");
        }
    }
}
