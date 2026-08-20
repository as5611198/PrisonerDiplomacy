using Verse;

namespace PrisonerDiplomacy
{
    public sealed class PrisonerBattleEvent : IExposable
    {
        public int Tick;
        public string Description;

        public void ExposeData()
        {
            Scribe_Values.Look(ref Tick, "tick");
            Scribe_Values.Look(ref Description, "description");
        }
    }
}
