using System;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace PrisonerDiplomacy
{
    internal static class RaidGenerationUtility
    {
        internal static bool HasUsablePawnGroupMaker(IncidentParms parms)
        {
            if (parms?.faction == null)
            {
                return true;
            }

            PawnGroupMakerParms groupParms = BuildPawnGroupMakerParms(parms);
            PawnGroupMaker maker;
            int seed = Gen.HashCombineInt(
                GenText.StableStringHash(parms.faction.GetUniqueLoadID()),
                (int)(parms.points * 10f));
            Rand.PushState(seed);
            try
            {
                return PawnGroupMakerUtility.TryGetRandomPawnGroupMaker(
                    groupParms,
                    out maker,
                    false);
            }
            catch (Exception exception)
            {
                CompatibilityDiagnostics.LogErrorOnce(
                    "raid-group-maker-probe:" + parms.faction.GetUniqueLoadID(),
                    "Could not inspect a faction's raid PawnGroupMakers; leaving vanilla raid handling untouched.",
                    exception);
                return true;
            }
            finally
            {
                Rand.PopState();
            }
        }

        internal static PawnGroupMakerParms BuildPawnGroupMakerParms(IncidentParms parms)
        {
            Map map = parms?.target as Map;
            Faction faction = parms?.faction;
            return new PawnGroupMakerParms
            {
                groupKind = parms?.pawnGroupKind ?? PawnGroupKindDefOf.Combat,
                tile = map != null ? map.Tile : default(PlanetTile),
                inhabitants = false,
                points = parms?.points ?? 0f,
                faction = faction,
                ideo = parms?.pawnIdeo,
                traderKind = parms?.traderKind,
                generateFightersOnly = parms?.generateFightersOnly ?? false,
                dontUseSingleUseRocketLaunchers = parms?.dontUseSingleUseRocketLaunchers ?? false,
                raidStrategy = parms?.raidStrategy,
                forceOneDowned = parms?.raidForceOneDowned ?? false,
                seed = parms?.pawnGroupMakerSeed,
                raidAgeRestriction = parms?.raidAgeRestriction,
                ignoreGroupCommonality = true
            };
        }
    }
}
