using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

namespace PrisonerDiplomacy
{
    internal static class PrisonerDiplomacyDiagnostics
    {
        private static readonly string[] PriorityPackageIds =
        {
            "yancy.rimchat",
            "erdelf.HumanoidAlienRaces",
            "Orion.Hospitality",
            "CETeam.CombatExtended",
            "oskarpotocki.vanillafactionsexpanded.core",
            "oskarpotocki.vfe.insectoid",
            "ludeon.rimworld.royalty",
            "ludeon.rimworld.ideology",
            "ludeon.rimworld.biotech",
            "ludeon.rimworld.anomaly",
            "ludeon.rimworld.odyssey"
        };

        public static string BuildReport()
        {
            StringBuilder builder = new StringBuilder();
            PrisonerDiplomacyGameComponent component = PrisonerDiplomacyGameComponent.Current;
            PrisonerDiplomacySettings settings = PrisonerDiplomacyMod.Settings;

            builder.AppendLine("Prisoner Diplomacy diagnostic report");
            builder.AppendLine("modVersion=1.2.1");
            builder.AppendLine("apiVersion=" + PrisonerDiplomacyBackendApi.ApiVersion);
            builder.AppendLine("gameVersion=" + SafeGameVersion());
            builder.AppendLine("saveSchema=" + (component?.SaveSchemaVersion.ToString() ?? "no-game"));
            builder.AppendLine("records=" + (component?.Records.Count ?? 0));
            builder.AppendLine("deals=" + (component?.Deals.Count ?? 0));
            builder.AppendLine("activeDeals=" + (component?.Deals.Count(deal => deal != null && deal.IsActive) ?? 0));
            builder.AppendLine("maps=" + (Find.Maps?.Count ?? 0));
            builder.AppendLine("playerMaps=" + (Find.Maps?.Count(map => map != null && map.IsPlayerHome) ?? 0));
            builder.AppendLine("rimChatInstalled=" + RimChatIntegration.IsInstalled);
            builder.AppendLine("rimChatVersion=" + (RimChatIntegration.Version ?? ""));
            builder.AppendLine("rimChatStatus=" + RimChatIntegration.Status);
            builder.AppendLine("rimChatBridge=" + RimChatHarmonyPatches.IsInstalled);
            builder.AppendLine("settings.enemyInitiatedRansoms=" + (settings?.EnableEnemyInitiatedRansoms ?? true));
            builder.AppendLine("settings.offerFrequencyMultiplier=" + (settings?.OfferFrequencyMultiplier ?? 1f).ToString("0.00"));
            builder.AppendLine("settings.ransomValueMultiplier=" + (settings?.RansomValueMultiplier ?? 1f).ToString("0.00"));
            builder.AppendLine("settings.factionReserves=" + (settings?.EnableFactionReserves ?? true));
            builder.AppendLine("settings.factionMemory=" + (settings?.EnableFactionMemory ?? true));
            builder.AppendLine("settings.pirateRisks=" + (settings?.EnablePirateRisks ?? true));
            builder.AppendLine("settings.strategicConsequences=" + (settings?.EnableStrategicConsequences ?? true));
            builder.AppendLine("settings.aiEnabled=" + (settings?.EnableAiNarratives ?? false));
            builder.AppendLine("settings.aiExternalContext=" + (settings?.AiAllowExternalContext ?? false));
            builder.AppendLine("settings.aiNegotiationAdjustments=" + (settings?.EnableAiNegotiationAdjustments ?? false));
            builder.AppendLine("settings.aiPersonaOverrides=" + (settings?.FactionPersonaOverrides?.Count ?? 0));
            builder.AppendLine("activePriorityMods=" + string.Join(",", ActivePriorityMods()));

            if (component?.LastRepairSummary != null)
            {
                builder.AppendLine("lastRepair=" + component.LastRepairSummary.ToLogString());
            }

            builder.AppendLine("dealStates=" + string.Join(",", (component?.Deals ?? Enumerable.Empty<PrisonerDeal>())
                .Where(deal => deal != null)
                .GroupBy(deal => deal.State)
                .OrderBy(group => group.Key.ToString(), StringComparer.Ordinal)
                .Select(group => group.Key + ":" + group.Count())));
            return builder.ToString();
        }

        private static IEnumerable<string> ActivePriorityMods()
        {
            try
            {
                return LoadedModManager.RunningModsListForReading
                    .Where(mod => mod?.PackageId != null
                        && PriorityPackageIds.Any(id => string.Equals(id, mod.PackageId, StringComparison.OrdinalIgnoreCase)))
                    .Select(mod => mod.PackageId + "=" + (mod.Name ?? "?"))
                    .OrderBy(item => item, StringComparer.Ordinal)
                    .ToList();
            }
            catch
            {
                return new[] { "unavailable" };
            }
        }

        private static string SafeGameVersion()
        {
            try
            {
                return VersionControl.CurrentVersionString ?? "unknown";
            }
            catch
            {
                return "unknown";
            }
        }
    }
}
