using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace PrisonerDiplomacy
{
    internal sealed class DetectedDebugRace
    {
        public ThingDef Race { get; }
        public ModContentPack Mod { get; }
        public List<PawnKindDef> PawnKinds { get; }

        public string DisplayLabel
        {
            get
            {
                string modName = Mod?.Name ?? Race?.modContentPack?.Name ?? "Unknown mod";
                string raceName = Race?.LabelCap.ToString();
                if (string.IsNullOrWhiteSpace(raceName))
                {
                    raceName = Race?.defName ?? "Unknown race";
                }

                return "[" + modName + "] " + raceName;
            }
        }

        public DetectedDebugRace(ThingDef race, ModContentPack mod, IEnumerable<PawnKindDef> pawnKinds)
        {
            Race = race;
            Mod = mod;
            PawnKinds = (pawnKinds ?? Enumerable.Empty<PawnKindDef>())
                .Where(kind => kind != null && kind.race == race)
                .OrderByDescending(IsBasicMemberKind)
                .ThenByDescending(IsColonistKind)
                .ThenBy(kind => kind.factionLeader)
                .ThenBy(kind => kind.defName)
                .ToList();
        }

        public PawnKindDef PreferredPawnKind(Faction faction = null)
        {
            PawnKindDef basicMember = faction?.def?.basicMemberKind;
            if (basicMember?.race == Race)
            {
                return basicMember;
            }

            return PawnKinds.FirstOrDefault();
        }

        private static bool IsBasicMemberKind(PawnKindDef pawnKind)
        {
            return pawnKind != null
                && DefDatabase<FactionDef>.AllDefsListForReading.Any(faction => faction?.basicMemberKind == pawnKind);
        }

        private static bool IsColonistKind(PawnKindDef pawnKind)
        {
            string text = ((pawnKind?.defName ?? string.Empty) + " " + (pawnKind?.label ?? string.Empty)).ToLowerInvariant();
            return text.Contains("colonist") || text.Contains("citizen") || text.Contains("common");
        }
    }

    internal static class AlienRaceDebugUtility
    {
        public static List<DetectedDebugRace> DetectCustomHumanlikeRaces()
        {
            return DefDatabase<PawnKindDef>.AllDefsListForReading
                .Where(IsCustomHumanlikePawnKind)
                .GroupBy(kind => kind.race)
                .Select(group => new DetectedDebugRace(
                    group.Key,
                    group.Key.modContentPack ?? group.FirstOrDefault()?.modContentPack,
                    group))
                .Where(entry => entry.PawnKinds.Count > 0)
                .OrderBy(entry => entry.Mod?.Name ?? string.Empty)
                .ThenBy(entry => entry.Race?.label ?? entry.Race?.defName ?? string.Empty)
                .ToList();
        }

        public static Faction FindMatchingFaction(DetectedDebugRace race)
        {
            if (race?.Race == null || Find.FactionManager == null)
            {
                return null;
            }

            return Find.FactionManager.AllFactionsVisible
                .Where(faction => faction != null
                    && faction != Faction.OfPlayer
                    && PrisonerEligibilityUtility.IsNegotiatingFaction(faction)
                    && FactionMatchesRace(faction, race))
                .OrderByDescending(faction => faction.def?.basicMemberKind?.race == race.Race)
                .ThenByDescending(faction => faction.HostileTo(Faction.OfPlayer))
                .ThenBy(faction => faction.Name)
                .FirstOrDefault();
        }

        public static bool FactionMatchesRace(Faction faction, DetectedDebugRace race)
        {
            return FactionSupportsRace(faction, race?.Race);
        }

        public static bool FactionSupportsRace(Faction faction, ThingDef race)
        {
            if (faction?.def == null || race == null)
            {
                return false;
            }

            return faction.def.basicMemberKind?.race == race
                || (faction.def.fixedLeaderKinds?.Any(kind => kind?.race == race) ?? false)
                || (faction.def.pawnGroupMakers?.Any(group =>
                    GroupOptions(group).Any(option => option?.kind?.race == race)) ?? false);
        }

        private static IEnumerable<PawnGenOption> GroupOptions(PawnGroupMaker group)
        {
            if (group == null)
            {
                return Enumerable.Empty<PawnGenOption>();
            }

            return (group.options ?? new List<PawnGenOption>())
                .Concat(group.traders ?? new List<PawnGenOption>())
                .Concat(group.carriers ?? new List<PawnGenOption>())
                .Concat(group.guards ?? new List<PawnGenOption>());
        }

        public static bool IsValidCustomHumanlikePawnKind(PawnKindDef pawnKind)
        {
            return IsCustomHumanlikePawnKind(pawnKind);
        }

        public static void LogDetectedRaces()
        {
            List<DetectedDebugRace> races = DetectCustomHumanlikeRaces();
            if (races.Count == 0)
            {
                Log.Message("[Prisoner Diplomacy Debug] No custom humanlike races with usable PawnKinds were detected.");
                return;
            }

            Log.Message("[Prisoner Diplomacy Debug] Detected custom humanlike races=" + races.Count + ":\n"
                + string.Join("\n", races.Select(entry =>
                    "- mod=" + (entry.Mod?.Name ?? "?")
                    + " packageId=" + (entry.Mod?.PackageIdPlayerFacing ?? "?")
                    + " race=" + (entry.Race?.defName ?? "?")
                    + " preferredKind=" + (entry.PreferredPawnKind()?.defName ?? "?")
                    + " pawnKinds=" + string.Join(",", entry.PawnKinds.Select(kind => kind.defName))
                    + " matchingFaction=" + (FindMatchingFaction(entry)?.Name ?? "none"))));
        }

        private static bool IsCustomHumanlikePawnKind(PawnKindDef pawnKind)
        {
            ThingDef race = pawnKind?.race;
            string packageId = race?.modContentPack?.PackageIdPlayerFacing
                ?? race?.modContentPack?.PackageId;
            if (race == null
                || race == ThingDefOf.Human
                || race.race?.Humanlike != true
                || race.modContentPack == null
                || (packageId?.StartsWith("ludeon.rimworld", StringComparison.OrdinalIgnoreCase) ?? false))
            {
                return false;
            }

            PrisonerDiplomacyPawnCompatibilityExtension compatibility =
                race.GetModExtension<PrisonerDiplomacyPawnCompatibilityExtension>()
                ?? pawnKind.GetModExtension<PrisonerDiplomacyPawnCompatibilityExtension>();
            return compatibility?.ExcludeFromDiplomacy != true
                && compatibility?.TemporaryPawn != true;
        }
    }
}
