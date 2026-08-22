using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace PrisonerDiplomacy
{
    // Optional contract for race/faction mods. The extension is deliberately small so
    // compatibility does not require a hard assembly dependency on another mod.
    public sealed class PrisonerDiplomacyPawnCompatibilityExtension : DefModExtension
    {
        public bool ExcludeFromDiplomacy;
        public bool TemporaryPawn;
        public string ExclusionReason;
    }

    public sealed class PrisonerDiplomacyFactionCompatibilityExtension : DefModExtension
    {
        public FactionNegotiationOverride NegotiationOverride = FactionNegotiationOverride.Automatic;
    }

    public sealed class CompatibilityRepairSummary
    {
        public int RemovedRecords;
        public int MergedRecords;
        public int RemovedDeals;
        public int CancelledConflictingDeals;
        public int ReassignedMaps;
        public int NormalizedRewards;
        public int RemovedMemories;
        public int RemovedStrategicStates;
        public int RemovedFollowups;
        public int RemovedDiplomacyEvents;
        public int RemovedCommTargets;
        public int RemovedNarratives;
        public int RebuiltLinks;
        public int RepairedSequences;

        public bool Changed => RemovedRecords > 0
            || MergedRecords > 0
            || RemovedDeals > 0
            || CancelledConflictingDeals > 0
            || ReassignedMaps > 0
            || NormalizedRewards > 0
            || RemovedMemories > 0
            || RemovedStrategicStates > 0
            || RemovedFollowups > 0
            || RemovedDiplomacyEvents > 0
            || RemovedCommTargets > 0
            || RemovedNarratives > 0
            || RebuiltLinks > 0
            || RepairedSequences > 0;

        public string ToLogString()
        {
            return "recordsRemoved=" + RemovedRecords
                + " recordsMerged=" + MergedRecords
                + " dealsRemoved=" + RemovedDeals
                + " dealsCancelled=" + CancelledConflictingDeals
                + " mapsReassigned=" + ReassignedMaps
                + " rewardsNormalized=" + NormalizedRewards
                + " memoriesRemoved=" + RemovedMemories
                + " strategicRemoved=" + RemovedStrategicStates
                + " followupsRemoved=" + RemovedFollowups
                + " diplomacyEventsRemoved=" + RemovedDiplomacyEvents
                + " commTargetsRemoved=" + RemovedCommTargets
                + " narrativesRemoved=" + RemovedNarratives
                + " linksRebuilt=" + RebuiltLinks
                + " sequencesRepaired=" + RepairedSequences;
        }
    }

    internal static class CompatibilityDiagnostics
    {
        private const string LogPrefix = "[Prisoner Diplomacy Compatibility] ";
        private static readonly HashSet<string> LoggedIssues = new HashSet<string>(StringComparer.Ordinal);
        private static readonly string[] PriorityPackageIds =
        {
            "yancy.rimchat",
            "erdelf.HumanoidAlienRaces",
            "Orion.Hospitality",
            "ceteam.combatextended",
            "oskarpotocki.vanillafactionsexpanded.core",
            "oskarpotocki.vfe.insectoid",
            "ludeon.rimworld.royalty",
            "ludeon.rimworld.ideology",
            "ludeon.rimworld.biotech",
            "ludeon.rimworld.anomaly",
            "ludeon.rimworld.odyssey"
        };

        public static void Reset()
        {
            LoggedIssues.Clear();
        }

        public static bool Enabled => PrisonerDiplomacyMod.Settings?.EnableCompatibilityLogging != false;

        public static void LogIssueOnce(string key, string message)
        {
            if (!Enabled || string.IsNullOrEmpty(key) || !LoggedIssues.Add(key))
            {
                return;
            }

            Log.Warning(LogPrefix + message);
        }

        public static void LogErrorOnce(string key, string message, Exception exception = null)
        {
            if (!Enabled || string.IsNullOrEmpty(key) || !LoggedIssues.Add(key))
            {
                return;
            }

            Log.Error(LogPrefix + message + (exception == null ? string.Empty : " " + exception));
        }

        public static void LogPawnExcluded(Pawn pawn, string reason)
        {
            if (!Enabled || pawn == null || string.IsNullOrEmpty(reason))
            {
                return;
            }

            string id = SafePawnId(pawn);
            string key = "pawn:" + id + ":" + reason;
            LogIssueOnce(key, "Excluded pawn=" + (pawn.LabelShortCap ?? "?")
                + " id=" + id + " def=" + (pawn.def?.defName ?? "?")
                + " reason=" + reason + ".");
        }

        public static string SafePawnId(Pawn pawn)
        {
            if (pawn == null)
            {
                return "null";
            }

            try
            {
                return pawn.GetUniqueLoadID() ?? "missing";
            }
            catch
            {
                return "unreadable";
            }
        }

        public static void LogCompatibilityReport(
            IReadOnlyCollection<PrisonerRecord> records,
            IReadOnlyCollection<PrisonerDeal> deals)
        {
            if (!Enabled)
            {
                Log.Message(LogPrefix + "Detailed compatibility logging is disabled.");
                return;
            }

            List<string> activeMods = new List<string>();
            try
            {
                foreach (ModContentPack mod in LoadedModManager.RunningModsListForReading)
                {
                    if (mod == null || mod.PackageId == null)
                    {
                        continue;
                    }

                    if (PriorityPackageIds.Any(id => string.Equals(id, mod.PackageId, StringComparison.OrdinalIgnoreCase)))
                    {
                        activeMods.Add(mod.PackageId + "=" + (mod.Name ?? "?"));
                    }
                }
            }
            catch (Exception exception)
            {
                LogErrorOnce("active-mods", "Could not enumerate active mods.", exception);
            }

            int mapCount = Find.Maps?.Count ?? 0;
            int playerMapCount = Find.Maps?.Count(map => map != null && map.IsPlayerHome) ?? 0;
            int prisonerCount = 0;
            if (Find.Maps != null)
            {
                foreach (Map map in Find.Maps)
                {
                    try
                    {
                        prisonerCount += map?.mapPawns?.PrisonersOfColonySpawned?.Count() ?? 0;
                    }
                    catch (Exception exception)
                    {
                        LogErrorOnce("map-prisoner-count", "Could not count prisoners on a map.", exception);
                    }
                }
            }

            Log.Message(LogPrefix + "report version=1.2.1 maps=" + mapCount
                + " playerMaps=" + playerMapCount
                + " spawnedPrisoners=" + prisonerCount
                + " records=" + (records?.Count ?? 0)
                + " deals=" + (deals?.Count ?? 0)
                + " rimChat=" + (RimChatIntegration.IsInstalled ? RimChatIntegration.Version : "not-installed")
                + " priorityMods=" + (activeMods.Count == 0 ? "none" : string.Join(",", activeMods)) + ".");
        }

        public static void RecordScanDuration(long elapsedMilliseconds, int mapCount, int recordCount, int dealCount)
        {
            if (PrisonerDiplomacyMod.Settings?.EnablePerformanceLogging != true || elapsedMilliseconds < 100)
            {
                return;
            }

            LogIssueOnce(
                "slow-scan:" + (Find.TickManager?.TicksGame ?? 0) / 120000,
                "Slow scan elapsedMs=" + elapsedMilliseconds
                    + " maps=" + mapCount
                    + " records=" + recordCount
                    + " deals=" + dealCount + ".");
        }
    }
}
