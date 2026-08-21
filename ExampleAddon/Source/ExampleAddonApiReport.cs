using System.Collections.Generic;
using System.Linq;
using System.Text;
using PrisonerDiplomacy;
using Verse;

namespace PrisonerDiplomacyExampleAddon
{
    internal enum InspectorTab
    {
        Overview,
        Prisoners,
        Factions,
        Events
    }

    internal static class ExampleAddonApiReport
    {
        internal static string Build(InspectorTab tab, Map map)
        {
            switch (tab)
            {
                case InspectorTab.Prisoners: return BuildPrisoners(map);
                case InspectorTab.Factions: return BuildFactions(map);
                case InspectorTab.Events: return BuildEvents();
                default: return BuildOverview();
            }
        }

        internal static string BuildFull(Map map)
        {
            return BuildOverview()
                + "\n\n=== Prisoners ===\n" + BuildPrisoners(map)
                + "\n\n=== Factions ===\n" + BuildFactions(map)
                + "\n\n=== Events ===\n" + BuildEvents();
        }

        private static string BuildOverview()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Prisoner Diplomacy Example Add-on API report");
            builder.AppendLine("Core API: " + PrisonerDiplomacyBackendApi.ApiVersion);
            builder.AppendLine("Required API: " + ExampleAddonMod.RequiredApiVersion);
            builder.AppendLine("Extension registered: " + ExampleAddonMod.ExtensionRegistered);
            builder.AppendLine("Persona registered: " + ExampleAddonMod.PersonaRegistered);
            builder.AppendLine("UI extension registered: " + ExampleAddonMod.UiExtensionRegistered);
            if (!string.IsNullOrEmpty(ExampleAddonMod.CompatibilityFailure))
            {
                builder.AppendLine("Compatibility failure: " + ExampleAddonMod.CompatibilityFailure);
            }

            AppendList(builder, "Registered extension IDs",
                PrisonerDiplomacyBackendApi.GetRegisteredExtensionIds());
            AppendList(builder, "Registered persona provider IDs",
                PrisonerDiplomacyExtensionRegistry.RegisteredPersonaProviderIds);

            builder.AppendLine();
            builder.AppendLine("Special reward catalog:");
            foreach (PrisonerDiplomacySpecialRewardDefinition reward in
                PrisonerDiplomacyExtensionRegistry.RegisteredSpecialRewardDefinitions)
            {
                builder.AppendLine("- " + reward.RewardId
                    + " | ThingDef=" + reward.RequiredThingDefName
                    + " | count=" + reward.MinimumCount
                    + " | labelKey=" + reward.LabelKey);
            }

            builder.AppendLine();
            builder.AppendLine("Event definition catalog:");
            foreach (PrisonerDiplomacyEventDefinition definition in
                PrisonerDiplomacyBackendApi.GetEventDefinitions())
            {
                builder.AppendLine("- " + definition.EventId
                    + " | kind=" + definition.Kind
                    + " | requiresPrisoner=" + definition.RequiresPrisoner);
            }
            builder.AppendLine();
            builder.AppendLine("API 1.2 note: external event definitions are metadata. "
                + "Only the core schedules and executes event state machines.");
            return builder.ToString().TrimEnd();
        }

        private static string BuildPrisoners(Map map)
        {
            if (map == null)
            {
                return "No current map.";
            }

            IReadOnlyList<PrisonerDiplomacyPrisonerSnapshot> snapshots =
                PrisonerDiplomacyBackendApi.GetPrisonerSnapshots(map);
            if (snapshots.Count == 0)
            {
                return "No Prisoner Diplomacy prisoner snapshots are available on this map.";
            }

            StringBuilder builder = new StringBuilder();
            foreach (PrisonerDiplomacyPrisonerSnapshot snapshot in snapshots)
            {
                builder.AppendLine(snapshot.PawnLabel + " [" + snapshot.PawnLoadId + "]");
                builder.AppendLine("  faction=" + snapshot.FactionLabel
                    + " importance=" + snapshot.Importance
                    + " value=" + snapshot.DiplomaticValue
                    + " health=" + snapshot.HealthPercent.ToString("P0"));
                builder.AppendLine("  canNegotiate=" + snapshot.CanNegotiate
                    + " downed=" + snapshot.Downed
                    + " lifeThreatening=" + snapshot.LifeThreatening
                    + " negotiationCount=" + snapshot.NegotiationCount);
                builder.AppendLine("  specialRewards="
                    + JoinOrNone(snapshot.SpecialRewardIds));
                if (!string.IsNullOrEmpty(snapshot.NegotiationUnavailableReason))
                {
                    builder.AppendLine("  unavailable=" + snapshot.NegotiationUnavailableReason);
                }
                if (PrisonerDiplomacyBackendApi.TryGetActiveDealSnapshot(
                    snapshot.Pawn,
                    out PrisonerDiplomacyDealSnapshot deal))
                {
                    builder.AppendLine("  activeDeal=" + deal.DealId
                        + " state=" + deal.StateKey
                        + " rewards=" + deal.RewardsDescription
                        + " canOrderRelease=" + deal.CanOrderRelease);
                }
                builder.AppendLine();
            }
            return builder.ToString().TrimEnd();
        }

        private static string BuildFactions(Map map)
        {
            if (map == null)
            {
                return "No current map.";
            }

            IReadOnlyList<PrisonerDiplomacyFactionSnapshot> snapshots =
                PrisonerDiplomacyBackendApi.GetFactionSnapshots(map);
            if (snapshots.Count == 0)
            {
                return "No negotiable faction snapshots are available on this map.";
            }

            StringBuilder builder = new StringBuilder();
            foreach (PrisonerDiplomacyFactionSnapshot snapshot in snapshots)
            {
                builder.AppendLine(snapshot.FactionLabel + " [" + snapshot.FactionDefName + "]");
                builder.AppendLine("  type=" + snapshot.NegotiationType
                    + " canNegotiate=" + snapshot.CanNegotiate
                    + " prisoners=" + snapshot.NegotiablePrisonerCount
                    + " hostages=" + snapshot.AvailableHostageCount);
                builder.AppendLine("  finance=" + snapshot.FinancialStatus);
                builder.AppendLine("  strategic=" + snapshot.StrategicStatus);
                builder.AppendLine("  memory=" + OneLine(snapshot.MemorySummary));
                builder.AppendLine();
            }
            return builder.ToString().TrimEnd();
        }

        private static string BuildEvents()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Registered definitions:");
            foreach (PrisonerDiplomacyEventDefinition definition in
                PrisonerDiplomacyBackendApi.GetEventDefinitions())
            {
                builder.AppendLine("- " + definition.EventId
                    + " | " + definition.Kind
                    + " | " + definition.LabelKey);
            }

            builder.AppendLine();
            builder.AppendLine("Persisted event snapshots:");
            IReadOnlyList<PrisonerDiplomacyEventSnapshot> snapshots =
                PrisonerDiplomacyBackendApi.GetEventSnapshots();
            if (snapshots.Count == 0)
            {
                builder.AppendLine("- none");
            }
            foreach (PrisonerDiplomacyEventSnapshot snapshot in snapshots)
            {
                builder.AppendLine("- " + snapshot.EventId
                    + " definition=" + snapshot.DefinitionId
                    + " extension=" + snapshot.ExtensionId
                    + " kind=" + snapshot.Kind
                    + " state=" + snapshot.State
                    + " stage=" + snapshot.Stage
                    + " attempts=" + snapshot.Attempts
                    + " active=" + snapshot.IsActive);
            }
            return builder.ToString().TrimEnd();
        }

        private static void AppendList(
            StringBuilder builder,
            string heading,
            IEnumerable<string> values)
        {
            builder.AppendLine();
            builder.AppendLine(heading + ":");
            List<string> materialized = values?.Where(value => !string.IsNullOrEmpty(value)).ToList()
                ?? new List<string>();
            if (materialized.Count == 0)
            {
                builder.AppendLine("- none");
                return;
            }
            foreach (string value in materialized)
            {
                builder.AppendLine("- " + value);
            }
        }

        private static string JoinOrNone(IEnumerable<string> values)
        {
            List<string> materialized = values?.Where(value => !string.IsNullOrEmpty(value)).ToList()
                ?? new List<string>();
            return materialized.Count == 0 ? "none" : string.Join(", ", materialized);
        }

        private static string OneLine(string value)
        {
            return string.IsNullOrEmpty(value)
                ? "none"
                : value.Replace("\r", " ").Replace("\n", " | ");
        }
    }
}
