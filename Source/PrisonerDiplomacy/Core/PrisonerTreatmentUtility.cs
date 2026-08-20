using System.Linq;
using Verse;

namespace PrisonerDiplomacy
{
    public static class PrisonerTreatmentUtility
    {
        public static bool IsLifeThreatening(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null)
            {
                return false;
            }

            float health = pawn.health.summaryHealth?.SummaryHealthPercent ?? 1f;
            return health <= 0.45f
                || pawn.health.hediffSet.hediffs.Any(hediff => hediff.IsCurrentlyLifeThreatening);
        }

        public static int CountMissingParts(Pawn pawn)
        {
            return pawn?.health?.hediffSet?.hediffs
                .OfType<Hediff_MissingPart>()
                .Count() ?? 0;
        }

        public static int CountPermanentInjuries(Pawn pawn)
        {
            return pawn?.health?.hediffSet?.hediffs
                .OfType<HediffWithComps>()
                .Count(hediff => hediff.TryGetComp<HediffComp_GetsPermanent>()?.IsPermanent == true) ?? 0;
        }
    }
}
