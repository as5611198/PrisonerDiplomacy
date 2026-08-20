using Verse;

namespace PrisonerDiplomacy
{
    public sealed class PrisonerMemoryEvent : IExposable
    {
        public int Tick;
        public string ReasonKey;
        public string PawnLabel;

        public void ExposeData()
        {
            Scribe_Values.Look(ref Tick, "tick");
            Scribe_Values.Look(ref ReasonKey, "reasonKey");
            Scribe_Values.Look(ref PawnLabel, "pawnLabel");
        }
    }
}
