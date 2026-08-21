using System.Linq;
using LudeonTK;
using PrisonerDiplomacy;
using RimWorld;
using UnityEngine;
using Verse;

namespace PrisonerDiplomacyExampleAddon
{
    internal static class ExampleAddonDebugActions
    {
        [DebugAction("Prisoner Diplomacy Example Add-on", "Open read-only API inspector",
            allowedGameStates = AllowedGameStates.Playing)]
        private static void OpenInspector()
        {
            ExampleAddonMod.OpenInspector();
        }

        [DebugAction("Prisoner Diplomacy Example Add-on", "Copy full public API report",
            allowedGameStates = AllowedGameStates.Playing)]
        private static void CopyReport()
        {
            string report = ExampleAddonApiReport.BuildFull(Find.CurrentMap);
            GUIUtility.systemCopyBuffer = report;
            Log.Message("[Prisoner Diplomacy Example Add-on]\n" + report);
            Messages.Message("PDX_ReportCopied".Translate(), MessageTypeDefOf.TaskCompletion, false);
        }

        [DebugAction("Prisoner Diplomacy Example Add-on", "Log selected prisoner snapshot",
            actionType = DebugActionType.ToolMapForPawns,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void LogSelectedPrisoner(Pawn pawn)
        {
            if (!PrisonerDiplomacyBackendApi.TryGetPrisonerSnapshot(
                pawn,
                pawn?.MapHeld,
                out PrisonerDiplomacyPrisonerSnapshot snapshot))
            {
                Messages.Message("PDX_NoPrisonerSnapshot".Translate(),
                    MessageTypeDefOf.RejectInput, false);
                return;
            }

            string rewards = snapshot.SpecialRewardIds == null
                ? "none"
                : string.Join(", ", snapshot.SpecialRewardIds);
            Log.Message("[Prisoner Diplomacy Example Add-on] prisoner=" + snapshot.PawnLabel
                + " faction=" + snapshot.FactionLabel
                + " value=" + snapshot.DiplomaticValue
                + " canNegotiate=" + snapshot.CanNegotiate
                + " specialRewards=" + rewards + ".");
        }

        [DebugAction("Prisoner Diplomacy Example Add-on", "Preview 250 silver for selected prisoner",
            actionType = DebugActionType.ToolMapForPawns,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void PreviewDemand(Pawn pawn)
        {
            Pawn negotiator = pawn?.MapHeld?.mapPawns?.FreeColonistsSpawned
                .Where(colonist => colonist != null && !colonist.Dead)
                .OrderByDescending(colonist =>
                    colonist.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0)
                .FirstOrDefault();
            if (negotiator == null)
            {
                Messages.Message("PDX_NoNegotiator".Translate(),
                    MessageTypeDefOf.RejectInput, false);
                return;
            }

            NegotiationResult result = PrisonerDiplomacyBackendApi.PreviewDemand(
                pawn,
                negotiator,
                new RewardDemand { Silver = 250 },
                out string reasonKey);
            if (result == null)
            {
                Messages.Message("PDX_PreviewFailed".Translate(reasonKey ?? "?"),
                    MessageTypeDefOf.RejectInput, false);
                return;
            }

            string line = "[Prisoner Diplomacy Example Add-on] preview prisoner="
                + pawn.LabelShortCap
                + " negotiator=" + negotiator.LabelShortCap
                + " outcome=" + result.Outcome
                + " assessment=" + result.Assessment
                + " fairValue=" + result.FairValue
                + " budget=" + result.NegotiationBudget
                + " reserve=" + result.ReserveAvailable
                + " chance=" + result.AcceptanceChance.ToString("P0") + ".";
            Log.Message(line);
            Messages.Message("PDX_PreviewLogged".Translate(),
                MessageTypeDefOf.NeutralEvent, false);
        }

        [DebugAction("Prisoner Diplomacy Example Add-on", "Log selected prisoner adapter context",
            actionType = DebugActionType.ToolMapForPawns,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void LogAdapterContext(Pawn pawn)
        {
            Faction faction = pawn?.HomeFaction ?? pawn?.Faction;
            int adjustment = PrisonerDiplomacyBackendApi.GetDiplomaticValueAdjustment(pawn, faction);
            string persona = PrisonerDiplomacyExtensionRegistry.GetPersona(pawn, faction);
            string rewards = string.Join(", ",
                PrisonerDiplomacyBackendApi.GetSpecialRewardOptions(pawn, faction)
                    .Select(reward => reward.RewardId));
            Log.Message("[Prisoner Diplomacy Example Add-on] pawn="
                + (pawn?.LabelShortCap ?? "?")
                + " factionDef=" + (faction?.def?.defName ?? "?")
                + " adjustment=" + adjustment
                + " rewards=" + (string.IsNullOrEmpty(rewards) ? "none" : rewards)
                + " persona=" + (string.IsNullOrEmpty(persona) ? "none" : persona) + ".");
        }
    }
}
