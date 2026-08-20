using RimWorld;
using Verse;

namespace PrisonerDiplomacy
{
    public sealed class FactionStrategicState : IExposable
    {
        public Faction Faction;
        public int CeasefireExpiresTick = -1;
        public string CeasefireSourceDealId;
        public string CeasefireSourcePawnLabel;
        public int LastCeasefireNoticeTick = -1;
        public int LastCeasefireBreachTick = -1;
        public bool IntelAvailable;
        public int IntelExpiresTick = -1;
        public string IntelSourceDealId;
        public string IntelSourcePawnLabel;
        public bool CareCreditAvailable;
        public string CareCreditSourceDealId;
        public string CareCreditSourcePawnLabel;
        public int CareCreditGrantedTick = -1;
        public int WarnedRaidFireTick = -1;
        public IncidentDef WarnedRaidDef;
        public Map WarnedRaidMap;
        public float WarnedRaidPoints;

        public void ExposeData()
        {
            Scribe_References.Look(ref Faction, "faction");
            Scribe_Values.Look(ref CeasefireExpiresTick, "ceasefireExpiresTick", -1);
            Scribe_Values.Look(ref CeasefireSourceDealId, "ceasefireSourceDealId");
            Scribe_Values.Look(ref CeasefireSourcePawnLabel, "ceasefireSourcePawnLabel");
            Scribe_Values.Look(ref LastCeasefireNoticeTick, "lastCeasefireNoticeTick", -1);
            Scribe_Values.Look(ref LastCeasefireBreachTick, "lastCeasefireBreachTick", -1);
            Scribe_Values.Look(ref IntelAvailable, "intelAvailable");
            Scribe_Values.Look(ref IntelExpiresTick, "intelExpiresTick", -1);
            Scribe_Values.Look(ref IntelSourceDealId, "intelSourceDealId");
            Scribe_Values.Look(ref IntelSourcePawnLabel, "intelSourcePawnLabel");
            Scribe_Values.Look(ref CareCreditAvailable, "careCreditAvailable");
            Scribe_Values.Look(ref CareCreditSourceDealId, "careCreditSourceDealId");
            Scribe_Values.Look(ref CareCreditSourcePawnLabel, "careCreditSourcePawnLabel");
            Scribe_Values.Look(ref CareCreditGrantedTick, "careCreditGrantedTick", -1);
            Scribe_Values.Look(ref WarnedRaidFireTick, "warnedRaidFireTick", -1);
            Scribe_Defs.Look(ref WarnedRaidDef, "warnedRaidDef");
            Scribe_References.Look(ref WarnedRaidMap, "warnedRaidMap");
            Scribe_Values.Look(ref WarnedRaidPoints, "warnedRaidPoints");
        }

        public void ClearWarnedRaid()
        {
            WarnedRaidFireTick = -1;
            WarnedRaidDef = null;
            WarnedRaidMap = null;
            WarnedRaidPoints = 0f;
        }
    }
}
