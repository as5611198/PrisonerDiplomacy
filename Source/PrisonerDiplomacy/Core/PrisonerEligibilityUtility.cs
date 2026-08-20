using RimWorld;
using Verse;

namespace PrisonerDiplomacy
{
    public static class PrisonerEligibilityUtility
    {
        public static bool IsEligible(Pawn pawn, out Faction originalFaction)
        {
            return IsEligible(pawn, out originalFaction, out _);
        }

        public static bool IsEligible(Pawn pawn, out Faction originalFaction, out string issue)
        {
            originalFaction = pawn?.HomeFaction ?? pawn?.Faction;
            issue = null;
            if (pawn == null)
            {
                issue = "missing_pawn";
                return false;
            }
            if (pawn.Dead || pawn.Destroyed)
            {
                issue = "dead_or_destroyed";
                return false;
            }
            if (!pawn.IsPrisonerOfColony)
            {
                issue = "not_player_prisoner";
                return false;
            }

            PrisonerDiplomacyPawnCompatibilityExtension compatibility = pawn.def?.GetModExtension<PrisonerDiplomacyPawnCompatibilityExtension>()
                ?? pawn.kindDef?.GetModExtension<PrisonerDiplomacyPawnCompatibilityExtension>();
            if (compatibility?.ExcludeFromDiplomacy == true || compatibility?.TemporaryPawn == true)
            {
                issue = string.IsNullOrEmpty(compatibility.ExclusionReason)
                    ? "special_pawn"
                    : compatibility.ExclusionReason;
                return false;
            }

            if (pawn.RaceProps == null || !pawn.RaceProps.Humanlike)
            {
                issue = "non_humanlike";
                return false;
            }
            if (pawn.guest == null)
            {
                issue = "missing_guest_tracker";
                return false;
            }
            if (pawn.IsQuestLodger())
            {
                issue = "temporary_quest_lodger";
                return false;
            }
            if (pawn.kindDef == null)
            {
                issue = "missing_pawn_kind";
                return false;
            }
            string loadId;
            try
            {
                loadId = pawn.GetUniqueLoadID();
            }
            catch
            {
                loadId = null;
            }
            if (string.IsNullOrEmpty(loadId))
            {
                issue = "missing_stable_id";
                return false;
            }
            if (originalFaction == null || originalFaction == Faction.OfPlayer)
            {
                issue = "missing_or_player_faction";
                return false;
            }

            if (!IsNegotiatingFaction(originalFaction))
            {
                issue = "faction_non_negotiating";
                return false;
            }

            return true;
        }

        public static bool IsNegotiatingFaction(Faction faction)
        {
            return FactionNegotiationUtility.CanNegotiate(faction);
        }
    }
}
