using RimWorld;
using RimWorld.Planet;
using Verse;

namespace PrisonerDiplomacy
{
    [DefOf]
    public static class PrisonerDiplomacyDefOf
    {
        public static LetterDef PD_PrisonerRansomOffer;
        public static LetterDef PD_PrisonerDiplomacyEvent;
        public static ThingDef PD_PortableDiplomacyTerminal;
        public static WorldObjectDef PD_NeutralTradePoint;

        static PrisonerDiplomacyDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(PrisonerDiplomacyDefOf));
        }
    }
}
