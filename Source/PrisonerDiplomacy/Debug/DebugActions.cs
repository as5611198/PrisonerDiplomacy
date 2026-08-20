using System;
using System.Collections.Generic;
using LudeonTK;
using PrisonerDiplomacy.Telemetry;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace PrisonerDiplomacy
{
    internal static class DebugActions
    {
        [DebugAction("Prisoner Diplomacy", "Force ransom offer", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ForceRansomOffer(Pawn pawn)
        {
            PrisonerDeal deal = PrisonerDiplomacyGameComponent.Current?.ForceOffer(pawn);
            if (deal == null)
            {
                Messages.Message("PD_DebugNoEligible".Translate(), MessageTypeDefOf.RejectInput, false);
            }
        }

        [DebugAction("Prisoner Diplomacy", "Spawn vanilla human test prisoner near selected Pawn", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SpawnTestPrisoner(Pawn anchor)
        {
            PrisonerDiplomacyGameComponent component = PrisonerDiplomacyGameComponent.Current;
            if (component == null)
            {
                return;
            }

            Pawn pawn = component.DebugSpawnTestPrisoner(anchor, out string failureKey);
            Messages.Message(
                pawn != null
                    ? "PD_DebugPrisonerSpawned".Translate(pawn.LabelShortCap, pawn.Faction?.Name ?? "?")
                    : TranslateFailure(failureKey),
                pawn != null ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.RejectInput,
                false);
        }

        [DebugAction("Prisoner Diplomacy", "Spawn royal noble test prisoner near selected Pawn", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SpawnRoyalNobleTestPrisoner(Pawn anchor)
        {
            PrisonerDiplomacyGameComponent component = PrisonerDiplomacyGameComponent.Current;
            if (component == null)
            {
                return;
            }

            Pawn pawn = component.DebugSpawnRoyalNobleTestPrisoner(anchor, out string failureKey);
            Messages.Message(
                pawn != null
                    ? "PD_DebugRoyalPrisonerSpawned".Translate(
                        pawn.LabelShortCap,
                        pawn.Faction?.Name ?? "?",
                        PrisonerValueCalculator.ImportanceLabel(
                            component.GetRecord(pawn)?.Importance ?? PrisonerImportance.Regular))
                    : TranslateFailure(failureKey),
                pawn != null ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.RejectInput,
                false);
        }

        [DebugAction("Prisoner Diplomacy", "Generate test kidnapped colonist for selected Pawn", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void GenerateTestHostage(Pawn prisoner)
        {
            PrisonerDiplomacyGameComponent component = PrisonerDiplomacyGameComponent.Current;
            if (component == null)
            {
                return;
            }

            Pawn hostage = component.DebugGenerateTestHostage(prisoner, out string failureKey);
            Messages.Message(
                hostage != null
                    ? "PD_DebugHostageGenerated".Translate(hostage.LabelShortCap, prisoner?.Faction?.Name ?? "?")
                    : TranslateFailure(failureKey),
                hostage != null ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.RejectInput,
                false);
        }

        [DebugAction("Prisoner Diplomacy", "Spawn custom-race test prisoner", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static List<DebugActionNode> SpawnCustomRaceTestPrisoner()
        {
            List<DebugActionNode> nodes = new List<DebugActionNode>();
            foreach (DetectedDebugRace race in AlienRaceDebugUtility.DetectCustomHumanlikeRaces())
            {
                DetectedDebugRace selectedRace = race;
                nodes.Add(new DebugActionNode(
                    selectedRace.DisplayLabel,
                    DebugActionType.ToolMapForPawns,
                    null,
                    delegate(Pawn anchor) { SpawnCustomRaceTestPrisoner(anchor, selectedRace); }));
            }
            return nodes;
        }

        [DebugAction("Prisoner Diplomacy", "Generate custom-race kidnapped colonist", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static List<DebugActionNode> GenerateCustomRaceTestHostage()
        {
            List<DebugActionNode> nodes = new List<DebugActionNode>();
            foreach (DetectedDebugRace race in AlienRaceDebugUtility.DetectCustomHumanlikeRaces())
            {
                DetectedDebugRace selectedRace = race;
                nodes.Add(new DebugActionNode(
                    selectedRace.DisplayLabel,
                    DebugActionType.ToolMapForPawns,
                    null,
                    delegate(Pawn prisoner) { GenerateCustomRaceTestHostage(prisoner, selectedRace); }));
            }
            return nodes;
        }

        [DebugAction("Prisoner Diplomacy", "Log detected custom races", allowedGameStates = AllowedGameStates.Playing)]
        private static void LogDetectedCustomRaces()
        {
            AlienRaceDebugUtility.LogDetectedRaces();
            Messages.Message("PD_DebugCustomRacesLogged".Translate(), MessageTypeDefOf.NeutralEvent, false);
        }

        [DebugAction("Prisoner Diplomacy", "Reset selected Pawn negotiation cooldowns", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ResetNegotiationState(Pawn pawn)
        {
            Messages.Message(
                PrisonerDiplomacyGameComponent.Current?.DebugResetNegotiationState(pawn) == true
                    ? "PD_DebugCooldownReset".Translate()
                    : "PD_DebugNoEligible".Translate(),
                MessageTypeDefOf.NeutralEvent,
                false);
        }

        [DebugAction("Prisoner Diplomacy", "Submit 100 silver demand", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Submit100SilverDemand(Pawn pawn)
        {
            SubmitSilverDemand(pawn, 100);
        }

        [DebugAction("Prisoner Diplomacy", "Submit 250 silver demand", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Submit250SilverDemand(Pawn pawn)
        {
            SubmitSilverDemand(pawn, 250);
        }

        [DebugAction("Prisoner Diplomacy", "Submit 500 silver demand", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Submit500SilverDemand(Pawn pawn)
        {
            SubmitSilverDemand(pawn, 500);
        }

        [DebugAction("Prisoner Diplomacy", "Submit 1000 silver demand", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Submit1000SilverDemand(Pawn pawn)
        {
            SubmitSilverDemand(pawn, 1000);
        }

        [DebugAction("Prisoner Diplomacy", "Submit 2000 silver demand", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Submit2000SilverDemand(Pawn pawn)
        {
            SubmitSilverDemand(pawn, 2000);
        }

        [DebugAction("Prisoner Diplomacy", "Force selected Pawn into a counteroffer", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ForceCounterOffer(Pawn pawn)
        {
            PrisonerDiplomacyGameComponent component = PrisonerDiplomacyGameComponent.Current;
            if (component == null)
            {
                return;
            }

            NegotiationResult result = component.DebugForceCounterOffer(pawn, out string failureKey);
            ShowNegotiationResult(pawn, result, failureKey);
        }

        [DebugAction("Prisoner Diplomacy", "Apply debug AI advisory: urgent and threatened", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ApplyUrgentAiAdvisory(Pawn pawn)
        {
            ApplyAiAdvisory(pawn, "critical", "high", "threatened");
        }

        [DebugAction("Prisoner Diplomacy", "Apply debug AI advisory: conciliatory", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ApplyConciliatoryAiAdvisory(Pawn pawn)
        {
            ApplyAiAdvisory(pawn, "low", "low", "conciliatory");
        }

        private static void ApplyAiAdvisory(
            Pawn pawn,
            string urgency,
            string concession,
            string leverageResponse)
        {
            string summary = null;
            bool applied = PrisonerDiplomacyGameComponent.Current?.DebugApplyAiAdvisory(
                pawn,
                urgency,
                concession,
                leverageResponse,
                out summary) == true;
            Messages.Message(
                applied
                    ? "PD_DebugAdvisoryApplied".Translate(summary)
                    : "PD_DebugAdvisoryNoCounteroffer".Translate(),
                applied ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.RejectInput,
                false);
        }

        [DebugAction("Prisoner Diplomacy", "Revise counteroffer +50 silver", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ReviseCounterOfferPlus50(Pawn pawn)
        {
            ReviseCounterOffer(pawn, 50);
        }

        [DebugAction("Prisoner Diplomacy", "Revise counteroffer +100 silver", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ReviseCounterOfferPlus100(Pawn pawn)
        {
            ReviseCounterOffer(pawn, 100);
        }

        [DebugAction("Prisoner Diplomacy", "Revise counteroffer +250 silver", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ReviseCounterOfferPlus250(Pawn pawn)
        {
            ReviseCounterOffer(pawn, 250);
        }

        [DebugAction("Prisoner Diplomacy", "Empty selected faction reserve", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void EmptyFactionReserve(Pawn pawn)
        {
            Messages.Message(
                PrisonerDiplomacyGameComponent.Current?.DebugSetReserveEmpty(pawn) == true
                    ? "PD_DebugReserveEmptied".Translate()
                    : "PD_DebugNoEligible".Translate(),
                MessageTypeDefOf.NeutralEvent,
                false);
        }

        [DebugAction("Prisoner Diplomacy", "Log records and deals", allowedGameStates = AllowedGameStates.Playing)]
        private static void LogRecordsAndDeals()
        {
            PrisonerDiplomacyGameComponent.Current?.LogSummary();
        }

        [DebugAction("Prisoner Diplomacy", "Log selected Pawn valuation", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void LogSelectedPawnValuation(Pawn pawn)
        {
            PrisonerDiplomacyGameComponent.Current?.LogPawnDiagnostics(pawn);
        }

        [DebugAction("Prisoner Diplomacy", "Log selected Pawn AI context preview", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void LogSelectedPawnAiContext(Pawn pawn)
        {
            string preview = PrisonerDiplomacyGameComponent.Current?.BuildAiContextPreview(pawn);
            if (string.IsNullOrEmpty(preview))
            {
                Messages.Message("PD_DebugNoEligible".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            Log.Message("[Prisoner Diplomacy AI Context Preview] " + preview);
            GUIUtility.systemCopyBuffer = preview;
            Messages.Message("PD_DebugAiContextCopied".Translate(), MessageTypeDefOf.PositiveEvent, false);
        }

        [DebugAction("Prisoner Diplomacy", "Accept selected ransom or counteroffer", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void AcceptSelectedDeal(Pawn pawn)
        {
            Messages.Message(
                PrisonerDiplomacyGameComponent.Current?.DebugAcceptSelectedDeal(pawn) == true
                    ? "PD_DebugDealAccepted".Translate()
                    : "PD_DebugNoActiveDeal".Translate(),
                MessageTypeDefOf.NeutralEvent,
                false);
        }

        [DebugAction("Prisoner Diplomacy", "Reject selected ransom or counteroffer", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void RejectSelectedDeal(Pawn pawn)
        {
            Messages.Message(
                PrisonerDiplomacyGameComponent.Current?.DebugRejectSelectedDeal(pawn) == true
                    ? "PD_DebugDealRejected".Translate()
                    : "PD_DebugNoActiveDeal".Translate(),
                MessageTypeDefOf.NeutralEvent,
                false);
        }

        [DebugAction("Prisoner Diplomacy", "Expire selected deal", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ExpireSelectedDeal(Pawn pawn)
        {
            Messages.Message(
                PrisonerDiplomacyGameComponent.Current?.DebugExpireSelectedDeal(pawn) == true
                    ? "PD_DebugDealExpired".Translate()
                    : "PD_DebugNoActiveDeal".Translate(),
                MessageTypeDefOf.NeutralEvent,
                false);
        }

        [DebugAction("Prisoner Diplomacy", "Simulate selected prisoner death failure", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SimulateDeathFailure(Pawn pawn)
        {
            SimulateFailure(pawn, DealState.FailedPrisonerDead);
        }

        [DebugAction("Prisoner Diplomacy", "Simulate selected prisoner escape failure", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SimulateEscapeFailure(Pawn pawn)
        {
            SimulateFailure(pawn, DealState.FailedEscaped);
        }

        [DebugAction("Prisoner Diplomacy", "Simulate selected prisoner recruitment failure", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SimulateRecruitmentFailure(Pawn pawn)
        {
            SimulateFailure(pawn, DealState.FailedRecruited);
        }

        [DebugAction("Prisoner Diplomacy", "Adjust selected faction memory +10 treatment", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void AdjustSelectedMemory(Pawn pawn)
        {
            Messages.Message(
                PrisonerDiplomacyGameComponent.Current?.DebugAdjustMemory(pawn, 0f, 10f, 0f) == true
                    ? "PD_DebugMemoryAdjusted".Translate()
                    : "PD_DebugNoEligible".Translate(),
                MessageTypeDefOf.NeutralEvent,
                false);
        }

        [DebugAction("Prisoner Diplomacy", "Adjust selected faction reliability +10", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void AdjustSelectedReliability(Pawn pawn)
        {
            Messages.Message(
                PrisonerDiplomacyGameComponent.Current?.DebugAdjustMemory(pawn, 10f, 0f, 0f) == true
                    ? "PD_DebugMemoryAdjusted".Translate()
                    : "PD_DebugNoEligible".Translate(),
                MessageTypeDefOf.NeutralEvent,
                false);
        }

        [DebugAction("Prisoner Diplomacy", "Adjust selected faction resentment +10", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void AdjustSelectedResentment(Pawn pawn)
        {
            Messages.Message(
                PrisonerDiplomacyGameComponent.Current?.DebugAdjustMemory(pawn, 0f, 0f, 10f) == true
                    ? "PD_DebugMemoryAdjusted".Translate()
                    : "PD_DebugNoEligible".Translate(),
                MessageTypeDefOf.NeutralEvent,
                false);
        }

        [DebugAction("Prisoner Diplomacy", "Refill selected faction reserve", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void RefillSelectedReserve(Pawn pawn)
        {
            Messages.Message(
                PrisonerDiplomacyGameComponent.Current?.DebugRefillReserve(pawn) == true
                    ? "PD_DebugReserveRefilled".Translate()
                    : "PD_DebugNoEligible".Translate(),
                MessageTypeDefOf.NeutralEvent,
                false);
        }

        [DebugAction("Prisoner Diplomacy", "Copy diagnostic report", allowedGameStates = AllowedGameStates.Playing)]
        private static void CopyDiagnosticReport()
        {
            GUIUtility.systemCopyBuffer = PrisonerDiplomacyDiagnostics.BuildReport();
            Messages.Message("PD_DiagnosticReportCopied".Translate(), MessageTypeDefOf.PositiveEvent, false);
        }

        [DebugAction("Prisoner Diplomacy", "Log compatibility report", allowedGameStates = AllowedGameStates.Playing)]
        private static void LogCompatibilityReport()
        {
            PrisonerDiplomacyGameComponent.Current?.LogCompatibilityReport();
            Messages.Message("PD_DebugCompatibilityLogged".Translate(), MessageTypeDefOf.NeutralEvent, false);
        }

        [DebugAction("Prisoner Diplomacy", "Repair invalid saved data", allowedGameStates = AllowedGameStates.Playing)]
        private static void RepairInvalidSavedData()
        {
            CompatibilityRepairSummary summary = PrisonerDiplomacyGameComponent.Current?.RunCompatibilityRepair();
            if (summary == null)
            {
                return;
            }

            Messages.Message(
                summary.Changed
                    ? "PD_DebugRepairApplied".Translate(summary.ToLogString())
                    : "PD_DebugRepairNotNeeded".Translate(),
                summary.Changed ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NeutralEvent,
                false);
        }

        [DebugAction("Prisoner Diplomacy", "Test RimChat ransom guard", allowedGameStates = AllowedGameStates.Playing)]
        private static void TestRimChatRansomGuard()
        {
            if (!RimChatHarmonyPatches.TryRunSmokeTest(out string failure))
            {
                Messages.Message("RimChat guard test failed: " + failure, MessageTypeDefOf.RejectInput, false);
                return;
            }

            Messages.Message("RimChat ransom guard rejected the test action.", MessageTypeDefOf.PositiveEvent, false);
        }

        [DebugAction("Prisoner Diplomacy", "Telemetry: test consent prompt", allowedGameStates = AllowedGameStates.Playing)]
        private static void TestErrorTelemetryConsent()
        {
            if (!ErrorTelemetryService.IsUploadConfigured
                || PrisonerDiplomacyMod.Settings?.EnableErrorTelemetryPrompts != true)
            {
                Messages.Message(
                    "Error telemetry receiver or consent prompts are disabled.",
                    MessageTypeDefOf.RejectInput,
                    false);
                return;
            }

            try
            {
                throw new InvalidOperationException("Synthetic Prisoner Diplomacy telemetry connectivity test.");
            }
            catch (Exception exception)
            {
                ErrorTelemetryService.CaptureException(
                    exception,
                    "DebugActions.TestErrorTelemetryConsent",
                    source: "debug_action");
                // Debug actions can run while the debug menu is being torn down;
                // drain immediately so the consent window is visible in the same UI turn.
                ErrorTelemetryService.DrainMainThread();
            }
        }

        [DebugAction("Prisoner Diplomacy", "Complete selected ransom delivery", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void CompleteSelectedDelivery(Pawn pawn)
        {
            PrisonerDiplomacyGameComponent component = PrisonerDiplomacyGameComponent.Current;
            PrisonerDeal deal = component?.GetActiveDeal(pawn);
            if (deal == null)
            {
                Messages.Message("PD_DebugNoEligible".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            component.DebugCompleteDelivery(pawn);
        }

        [DebugAction("Prisoner Diplomacy", "Order selected ransom release only", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void OrderReleaseOnly(Pawn pawn)
        {
            Messages.Message(
                PrisonerDiplomacyGameComponent.Current?.DebugOrderRelease(pawn) == true
                    ? "PD_DebugReleaseOrdered".Translate()
                    : "PD_DebugNoActiveDeal".Translate(),
                MessageTypeDefOf.NeutralEvent,
                false);
        }

        [DebugAction("Prisoner Diplomacy", "Add tendable injury and record player treatment", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void AddTreatmentState(Pawn pawn)
        {
            Messages.Message(
                PrisonerDiplomacyGameComponent.Current?.DebugAddTreatmentState(pawn) == true
                    ? "PD_DebugTreatmentRecorded".Translate()
                    : "PD_DebugNoEligible".Translate(),
                MessageTypeDefOf.NeutralEvent,
                false);
        }

        [DebugAction("Prisoner Diplomacy", "Cancel selected accepted deal", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void CancelAcceptedDeal(Pawn pawn)
        {
            Messages.Message(
                PrisonerDiplomacyGameComponent.Current?.DebugCancelAcceptedDeal(pawn) == true
                    ? "PD_DebugDealCancelled".Translate()
                    : "PD_DebugNoActiveDeal".Translate(),
                MessageTypeDefOf.NeutralEvent,
                false);
        }

        [DebugAction("Prisoner Diplomacy", "Mark selected deal delivered, payment pending", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void MarkDeliveryPending(Pawn pawn)
        {
            Messages.Message(
                PrisonerDiplomacyGameComponent.Current?.DebugMarkDeliveryPending(pawn) == true
                    ? "PD_DebugDeliveryPending".Translate()
                    : "PD_DebugNoActiveDeal".Translate(),
                MessageTypeDefOf.NeutralEvent,
                false);
        }

        [DebugAction("Prisoner Diplomacy", "Issue only the next pending reward", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void IssueNextReward(Pawn pawn)
        {
            PrisonerDiplomacyGameComponent component = PrisonerDiplomacyGameComponent.Current;
            if (component == null)
            {
                return;
            }

            bool issued = component.DebugIssueNextReward(pawn, out string rewardKey);
            Messages.Message(
                issued
                    ? "PD_DebugPartialReward".Translate(rewardKey)
                    : "PD_DebugNoRewardPending".Translate(),
                issued ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.RejectInput,
                false);
        }

        [DebugAction("Prisoner Diplomacy", "Make selected pirate payment due now", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void MakePaymentDueNow(Pawn pawn)
        {
            Messages.Message(
                PrisonerDiplomacyGameComponent.Current?.DebugMakePaymentDueNow(pawn) == true
                    ? "PD_DebugPaymentCompleted".Translate()
                    : "PD_DebugNoPendingPayment".Translate(),
                MessageTypeDefOf.NeutralEvent,
                false);
        }

        [DebugAction("Prisoner Diplomacy", "Simulate selected prisoner sale or transfer", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SimulateSale(Pawn pawn)
        {
            Messages.Message(
                PrisonerDiplomacyGameComponent.Current?.DebugSimulateSale(pawn) == true
                    ? "PD_DebugSaleFailed".Translate()
                    : "PD_DebugNoActiveDeal".Translate(),
                MessageTypeDefOf.NeutralEvent,
                false);
        }

        [DebugAction("Prisoner Diplomacy", "Simulate selected prisoner enslavement", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SimulateEnslavement(Pawn pawn)
        {
            Messages.Message(
                PrisonerDiplomacyGameComponent.Current?.DebugSimulateEnslavement(pawn) == true
                    ? "PD_DebugEnslavementFailed".Translate()
                    : "PD_DebugNoActiveDeal".Translate(),
                MessageTypeDefOf.NeutralEvent,
                false);
        }

        [DebugAction("Prisoner Diplomacy", "Grant 10-day ceasefire", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void GrantCeasefire(Pawn pawn)
        {
            if (PrisonerDiplomacyGameComponent.Current?.DebugGrantCeasefire(pawn) != true)
            {
                Messages.Message("PD_DebugNoEligible".Translate(), MessageTypeDefOf.RejectInput, false);
            }
        }

        [DebugAction("Prisoner Diplomacy", "Log selected faction strategic state", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void LogSelectedFactionStrategicState(Pawn pawn)
        {
            Messages.Message(
                PrisonerDiplomacyGameComponent.Current?.DebugLogStrategicState(pawn) == true
                    ? "PD_DebugStrategicStateLogged".Translate()
                    : "PD_DebugNoEligible".Translate(),
                MessageTypeDefOf.NeutralEvent,
                false);
        }

        [DebugAction("Prisoner Diplomacy", "Grant early-warning intel", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void GrantIntel(Pawn pawn)
        {
            if (PrisonerDiplomacyGameComponent.Current?.DebugGrantIntel(pawn) != true)
            {
                Messages.Message("PD_DebugNoEligible".Translate(), MessageTypeDefOf.RejectInput, false);
            }
        }

        [DebugAction("Prisoner Diplomacy", "Force selected faction positive return gift now", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ForcePositiveReturnGift(Pawn pawn)
        {
            bool careCredit = false;
            bool forced = PrisonerDiplomacyGameComponent.Current?.DebugForcePositiveReturnGift(pawn, out careCredit) == true;
            Messages.Message(
                forced
                    ? (careCredit
                        ? "PD_DebugCareCreditGranted".Translate()
                        : "PD_DebugPositiveGiftForced".Translate())
                    : "PD_DebugNoEligible".Translate(),
                forced ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.RejectInput,
                false);
        }

        [DebugAction("Prisoner Diplomacy", "Trigger eligible test raid", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void TriggerEligibleRaid(Pawn pawn)
        {
            if (PrisonerDiplomacyGameComponent.Current?.DebugTriggerEligibleRaid(pawn) != true)
            {
                Messages.Message("PD_DebugRaidSuppressed".Translate(), MessageTypeDefOf.NeutralEvent, false);
            }
        }

        [DebugAction("Prisoner Diplomacy", "Event: force neutral exchange proposal", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ForceNeutralExchangeEvent(Pawn pawn)
        {
            ForceDiplomacyEvent(pawn, PrisonerDiplomacyEventKind.NeutralTradeCaravan);
        }

        [DebugAction("Prisoner Diplomacy", "Event: force neutral world trade point", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ForceNeutralWorldTradePoint(Pawn pawn)
        {
            PrisonerDiplomacyGameComponent component = PrisonerDiplomacyGameComponent.Current;
            PrisonerDiplomacyEventRecord eventRecord = null;
            string failureKey = null;
            bool forced = component != null && component.DebugForceNeutralWorldTradePoint(
                pawn,
                out eventRecord,
                out failureKey);
            Messages.Message(
                forced
                    ? "PD_DebugWorldTradePointForced".Translate(
                        eventRecord?.EventId ?? "?",
                        eventRecord?.NeutralTradeTile.ToString() ?? "?",
                        eventRecord?.PrisonerLabel ?? "?")
                    : TranslateFailure(failureKey),
                forced ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.RejectInput,
                false);
        }

        [DebugAction("Prisoner Diplomacy", "Event: send selected prisoner to world trade point", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SendPrisonerToNeutralWorldTradePoint(Pawn pawn)
        {
            string failureKey = null;
            Caravan caravan = PrisonerDiplomacyGameComponent.Current?.DebugSendPrisonerToNeutralWorldTradePoint(
                pawn,
                out failureKey);
            Messages.Message(
                caravan != null
                    ? "PD_DebugWorldTradeCaravanSent".Translate(
                        pawn?.LabelShortCap ?? "?",
                        caravan.Tile.ToString())
                    : TranslateFailure(failureKey),
                caravan != null ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.RejectInput,
                false);
        }

        [DebugAction("Prisoner Diplomacy", "Event: force false surrender now", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ForceFalseSurrenderEvent(Pawn pawn)
        {
            ForceDiplomacyEvent(pawn, PrisonerDiplomacyEventKind.FalseSurrenderInfiltration);
        }

        [DebugAction("Prisoner Diplomacy", "Event: force false surrender warning now", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ForceFalseSurrenderWarningEvent(Pawn pawn)
        {
            ForceFalseSurrenderOutcome(pawn, false);
        }

        [DebugAction("Prisoner Diplomacy", "Event: force false surrender jailbreak now", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ForceFalseSurrenderJailbreakEvent(Pawn pawn)
        {
            ForceFalseSurrenderOutcome(pawn, true);
        }

        [DebugAction("Prisoner Diplomacy", "Event: force public trial proposal", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ForcePublicTrialEvent(Pawn pawn)
        {
            ForceDiplomacyEvent(pawn, PrisonerDiplomacyEventKind.PublicWarCrimeTrial);
        }

        [DebugAction("Prisoner Diplomacy", "Event: force ransom ambush now", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ForceRansomAmbushEvent(Pawn pawn)
        {
            ForceDiplomacyEvent(pawn, PrisonerDiplomacyEventKind.RansomAmbushRetaliation);
        }

        [DebugAction("Prisoner Diplomacy", "Event: advance selected Pawn event", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void AdvanceDiplomacyEvent(Pawn pawn)
        {
            PrisonerDiplomacyEventRecord eventRecord = null;
            PrisonerDiplomacyGameComponent component = PrisonerDiplomacyGameComponent.Current;
            bool advanced = component != null
                && component.DebugAdvanceDiplomacyEvent(pawn, out eventRecord);
            Messages.Message(
                advanced
                    ? "PD_DebugEventAdvanced".Translate(
                        eventRecord?.Kind.ToString() ?? "?",
                        eventRecord?.State.ToString() ?? "?",
                        eventRecord?.Stage ?? 0)
                    : "PD_DebugNoActiveEvent".Translate(),
                advanced ? MessageTypeDefOf.NeutralEvent : MessageTypeDefOf.RejectInput,
                false);
        }

        [DebugAction("Prisoner Diplomacy", "Event: cancel selected Pawn events", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void CancelDiplomacyEvents(Pawn pawn)
        {
            int cancelled = PrisonerDiplomacyGameComponent.Current?.DebugCancelDiplomacyEvents(pawn) ?? 0;
            Messages.Message(
                cancelled > 0
                    ? "PD_DebugEventsCancelled".Translate(cancelled)
                    : "PD_DebugNoActiveEvent".Translate(),
                cancelled > 0 ? MessageTypeDefOf.NeutralEvent : MessageTypeDefOf.RejectInput,
                false);
        }

        [DebugAction("Prisoner Diplomacy", "Event: log all event states", allowedGameStates = AllowedGameStates.Playing)]
        private static void LogDiplomacyEvents()
        {
            PrisonerDiplomacyGameComponent.Current?.DebugLogDiplomacyEvents();
            Messages.Message("PD_DebugEventsLogged".Translate(), MessageTypeDefOf.NeutralEvent, false);
        }

        [DebugAction("Prisoner Diplomacy", "Event: log neutral world trade points", allowedGameStates = AllowedGameStates.Playing)]
        private static void LogNeutralWorldTradePoints()
        {
            PrisonerDiplomacyGameComponent.Current?.DebugLogNeutralWorldTradePoints();
            Messages.Message("PD_DebugWorldTradePointsLogged".Translate(), MessageTypeDefOf.NeutralEvent, false);
        }

        [DebugAction("Prisoner Diplomacy", "Event: complete first ready world trade point", allowedGameStates = AllowedGameStates.Playing)]
        private static void CompleteNeutralWorldTradePoint()
        {
            bool completed = PrisonerDiplomacyGameComponent.Current?.DebugCompleteFirstNeutralWorldTradePoint() == true;
            Messages.Message(
                completed
                    ? "PD_DebugWorldTradePointCompleted".Translate()
                    : "PD_DebugWorldTradePointNeedsCaravan".Translate(),
                completed ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.RejectInput,
                false);
        }

        [DebugAction("Prisoner Diplomacy", "Rewards: toggle debug special reward", allowedGameStates = AllowedGameStates.Playing)]
        private static void ToggleDebugSpecialReward()
        {
            PrisonerDiplomacyExtensionCatalog.DebugSpecialRewardsEnabled =
                !PrisonerDiplomacyExtensionCatalog.DebugSpecialRewardsEnabled;
            Messages.Message(
                "PD_DebugSpecialRewardToggled".Translate(
                    PrisonerDiplomacyExtensionCatalog.DebugSpecialRewardsEnabled
                        ? "PD_UiEnabled".Translate()
                        : "PD_UiDisabled".Translate()),
                MessageTypeDefOf.NeutralEvent,
                false);
        }

        private static void SimulateFailure(Pawn pawn, DealState state)
        {
            Messages.Message(
                PrisonerDiplomacyGameComponent.Current?.DebugFailSelectedDeal(pawn, state) == true
                    ? "PD_DebugDealFailed".Translate(state.ToString())
                    : "PD_DebugNoActiveDeal".Translate(),
                MessageTypeDefOf.NeutralEvent,
                false);
        }

        private static void SpawnCustomRaceTestPrisoner(Pawn anchor, DetectedDebugRace race)
        {
            PrisonerDiplomacyGameComponent component = PrisonerDiplomacyGameComponent.Current;
            Faction faction = AlienRaceDebugUtility.FindMatchingFaction(race);
            PawnKindDef pawnKind = race?.PreferredPawnKind(faction);
            string failureKey = null;
            Pawn pawn = component?.DebugSpawnTestPrisoner(anchor, pawnKind, faction, out failureKey);
            Messages.Message(
                pawn != null
                    ? "PD_DebugCustomRacePrisonerSpawned".Translate(
                        pawn.LabelShortCap,
                        pawn.Faction?.Name ?? "?",
                        race?.Race?.LabelCap ?? "?")
                    : TranslateFailure(failureKey),
                pawn != null ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.RejectInput,
                false);
        }

        private static void GenerateCustomRaceTestHostage(Pawn prisoner, DetectedDebugRace race)
        {
            PrisonerDiplomacyGameComponent component = PrisonerDiplomacyGameComponent.Current;
            PawnKindDef pawnKind = race?.PreferredPawnKind();
            string failureKey = null;
            Pawn hostage = component?.DebugGenerateTestHostage(prisoner, pawnKind, out failureKey);
            Messages.Message(
                hostage != null
                    ? "PD_DebugCustomRaceHostageGenerated".Translate(
                        hostage.LabelShortCap,
                        prisoner?.Faction?.Name ?? "?",
                        race?.Race?.LabelCap ?? "?")
                    : TranslateFailure(failureKey),
                hostage != null ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.RejectInput,
                false);
        }

        private static void SubmitSilverDemand(Pawn pawn, int silver)
        {
            PrisonerDiplomacyGameComponent component = PrisonerDiplomacyGameComponent.Current;
            if (component == null)
            {
                return;
            }

            NegotiationResult result = component.DebugSubmitPlayerDemand(pawn, silver, out string failureKey);
            ShowNegotiationResult(pawn, result, failureKey);
        }

        private static void ReviseCounterOffer(Pawn pawn, int silverDelta)
        {
            PrisonerDiplomacyGameComponent component = PrisonerDiplomacyGameComponent.Current;
            if (component == null)
            {
                return;
            }

            NegotiationResult result = component.DebugReviseCounterOffer(pawn, silverDelta, out string failureKey);
            ShowNegotiationResult(pawn, result, failureKey);
        }

        private static void ForceDiplomacyEvent(Pawn pawn, PrisonerDiplomacyEventKind kind)
        {
            PrisonerDiplomacyGameComponent component = PrisonerDiplomacyGameComponent.Current;
            PrisonerDiplomacyEventRecord eventRecord = null;
            string failureKey = null;
            bool forced = component != null && component.DebugForceDiplomacyEvent(
                pawn,
                kind,
                out eventRecord,
                out failureKey);
            Messages.Message(
                forced
                    ? "PD_DebugEventForced".Translate(
                        eventRecord?.Kind.ToString() ?? kind.ToString(),
                        eventRecord?.State.ToString() ?? "?")
                    : TranslateFailure(failureKey),
                forced ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.RejectInput,
                false);
        }

        private static void ForceFalseSurrenderOutcome(Pawn pawn, bool forcePrisonBreak)
        {
            PrisonerDiplomacyGameComponent component = PrisonerDiplomacyGameComponent.Current;
            PrisonerDiplomacyEventRecord eventRecord = null;
            string failureKey = null;
            bool forced = component != null && component.DebugForceFalseSurrenderOutcome(
                pawn,
                forcePrisonBreak,
                out eventRecord,
                out failureKey);
            Messages.Message(
                forced
                    ? "PD_DebugEventForced".Translate(
                        eventRecord?.Kind.ToString() ?? PrisonerDiplomacyEventKind.FalseSurrenderInfiltration.ToString(),
                        eventRecord?.State.ToString() ?? "?")
                    : TranslateFailure(failureKey),
                forced ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.RejectInput,
                false);
        }

        private static void ShowNegotiationResult(Pawn pawn, NegotiationResult result, string failureKey)
        {
            if (result == null)
            {
                Messages.Message(TranslateFailure(failureKey), MessageTypeDefOf.RejectInput, false);
                return;
            }

            string requested = result.RequestedRewards?.Description().ToString() ?? "?";
            string counter = result.CounterOffer?.Description().ToString() ?? "-";
            Messages.Message(
                "PD_DebugDemandResult".Translate(
                    result.Outcome.ToString(),
                    requested,
                    counter,
                    result.AcceptanceChance.ToStringPercent()),
                pawn,
                MessageTypeDefOf.NeutralEvent,
                false);
        }

        private static TaggedString TranslateFailure(string failureKey)
        {
            return string.IsNullOrEmpty(failureKey)
                ? "PD_DebugNoEligible".Translate()
                : failureKey.Translate();
        }
    }
}
