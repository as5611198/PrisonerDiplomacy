using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace PrisonerDiplomacy
{
    public sealed partial class PrisonerDiplomacyGameComponent
    {
        public Pawn DebugSpawnTestPrisoner(Pawn anchor, out string failureKey)
        {
            List<Faction> negotiatingFactions = Find.FactionManager?.AllFactionsVisible
                .Where(candidate => candidate != null
                    && candidate != Faction.OfPlayer
                    && PrisonerEligibilityUtility.IsNegotiatingFaction(candidate))
                .OrderByDescending(candidate => candidate.HostileTo(Faction.OfPlayer))
                .ThenBy(candidate => candidate.Name)
                .ToList() ?? new List<Faction>();
            Faction faction = negotiatingFactions
                .FirstOrDefault(candidate => AlienRaceDebugUtility.FactionSupportsRace(
                    candidate,
                    ThingDefOf.Human))
                ?? negotiatingFactions.FirstOrDefault();
            if (faction == null)
            {
                failureKey = "PD_DebugNoHumanFaction";
                return null;
            }

            PawnKindDef pawnKind = faction.def?.basicMemberKind?.race == ThingDefOf.Human
                ? faction.def.basicMemberKind
                : PawnKindDefOf.SpaceRefugee;
            return DebugSpawnTestPrisoner(anchor, pawnKind, faction, out failureKey);
        }

        public Pawn DebugSpawnRoyalNobleTestPrisoner(Pawn anchor, out string failureKey)
        {
            failureKey = null;
            PawnKindDef nobleKind = DefDatabase<PawnKindDef>.GetNamedSilentFail("Empire_Royal_NobleWimp");
            Faction empire = Find.FactionManager?.AllFactionsVisible
                .Where(candidate => candidate != null
                    && candidate != Faction.OfPlayer
                    && string.Equals(candidate.def?.defName, "Empire", StringComparison.Ordinal)
                    && PrisonerEligibilityUtility.IsNegotiatingFaction(candidate))
                .FirstOrDefault();
            if (nobleKind == null || empire == null)
            {
                failureKey = "PD_DebugNoRoyaltySupport";
                return null;
            }

            Pawn pawn = DebugSpawnTestPrisoner(anchor, nobleKind, empire, out failureKey);
            if (pawn == null)
            {
                return null;
            }

            // The noble PawnKind normally receives Knight or Praetor. Use Count
            // when available so this debug pawn also exercises Core importance.
            RoyalTitleDef countTitle = DefDatabase<RoyalTitleDef>.GetNamedSilentFail("Count");
            if (pawn.royalty != null && countTitle != null)
            {
                pawn.royalty.SetTitle(empire, countTitle, false, true, false);
            }

            PrisonerRecord record = GetRecord(pawn);
            if (record != null)
            {
                record.Importance = PrisonerValueCalculator.Classify(pawn, empire);
                record.DiplomaticValue = PrisonerValueCalculator.Calculate(
                    pawn,
                    record.CapturedMarketValue,
                    record.Importance);
                ScheduleImportantPrisonerRescue(record);
            }

            return pawn;
        }

        public Pawn DebugSpawnTestPrisoner(
            Pawn anchor,
            PawnKindDef pawnKind,
            Faction faction,
            out string failureKey)
        {
            failureKey = null;
            Map map = anchor?.MapHeld;
            if (map == null)
            {
                failureKey = "PD_DebugNoMap";
                return null;
            }
            if (pawnKind?.race?.race?.Humanlike != true
                || faction == null
                || faction == Faction.OfPlayer
                || !PrisonerEligibilityUtility.IsNegotiatingFaction(faction)
                || (pawnKind.race != ThingDefOf.Human
                    && !AlienRaceDebugUtility.FactionSupportsRace(faction, pawnKind.race)))
            {
                failureKey = "PD_DebugNoMatchingRaceFaction";
                return null;
            }

            Pawn pawn = null;
            try
            {
                pawn = PawnGenerator.GeneratePawn(pawnKind, faction);
                pawn.inventory?.innerContainer.ClearAndDestroyContents();
                IntVec3 cell = CellFinder.RandomSpawnCellForPawnNear(anchor.Position, map);
                GenSpawn.Spawn(pawn, cell, map, WipeMode.Vanish);
                pawn.guest.SetGuestStatus(Faction.OfPlayer, GuestStatus.Prisoner);
                PrisonerRecord record = RegisterPawn(pawn);
                if (record == null)
                {
                    pawn.Destroy();
                    failureKey = "PD_DebugNoEligible";
                    return null;
                }

                return pawn;
            }
            catch (Exception exception)
            {
                if (pawn != null && !pawn.Destroyed)
                {
                    pawn.Destroy();
                }

                Log.Warning("[Prisoner Diplomacy Debug] Could not spawn test prisoner: " + exception);
                failureKey = "PD_DebugSpawnFailed";
                return null;
            }
        }

        public Pawn DebugGenerateTestHostage(Pawn prisoner, out string failureKey)
        {
            return DebugGenerateTestHostage(prisoner, PawnKindDefOf.SpaceRefugee, out failureKey);
        }

        public Pawn DebugGenerateTestHostage(Pawn prisoner, PawnKindDef pawnKind, out string failureKey)
        {
            failureKey = null;
            PrisonerRecord record = GetRecord(prisoner) ?? RegisterPawn(prisoner);
            if (record?.OriginalFaction?.kidnapped == null)
            {
                failureKey = "PD_DebugNoHostageFaction";
                return null;
            }
            if (pawnKind?.race?.race?.Humanlike != true)
            {
                failureKey = "PD_DebugInvalidRaceKind";
                return null;
            }

            try
            {
                return PrisonerExchangeUtility.CreateSmokeTestHostage(
                    record.OriginalFaction,
                    pawnKind);
            }
            catch (Exception exception)
            {
                Log.Warning("[Prisoner Diplomacy Debug] Could not generate test hostage: " + exception);
                failureKey = "PD_DebugSpawnFailed";
                return null;
            }
        }

        public bool DebugResetNegotiationState(Pawn pawn)
        {
            PrisonerRecord record = GetRecord(pawn) ?? RegisterPawn(pawn);
            if (record?.OriginalFaction == null)
            {
                return false;
            }

            int now = Find.TickManager.TicksGame;
            record.LastProposalTick = -1;
            record.LastPlayerNegotiationTick = -1;
            record.NegotiationCount = 0;
            record.ScheduledFactionOfferTick = now;

            FactionNegotiationMemory memory = GetFactionMemory(record.OriginalFaction, true);
            memory.LastPlayerNegotiationTick = -1;
            memory.NegotiationSuspendedUntilTick = -1;
            memory.Impatience = 0;
            return true;
        }

        public NegotiationResult DebugSubmitPlayerDemand(Pawn pawn, int silver, out string failureKey)
        {
            failureKey = null;
            PrisonerRecord record = GetRecord(pawn) ?? RegisterPawn(pawn);
            Pawn negotiator = FindDebugNegotiator(pawn);
            if (record == null)
            {
                failureKey = "PD_DebugNoEligible";
                return null;
            }
            if (negotiator == null)
            {
                failureKey = "PD_DebugNoNegotiator";
                return null;
            }

            NegotiationResult result = SubmitPlayerDemand(record, negotiator, silver);
            if (result == null)
            {
                failureKey = "PD_DebugDemandRejectedByState";
            }
            return result;
        }

        public NegotiationResult DebugForceCounterOffer(Pawn pawn, out string failureKey)
        {
            failureKey = null;
            PrisonerRecord record = GetRecord(pawn) ?? RegisterPawn(pawn);
            Pawn negotiator = FindDebugNegotiator(pawn);
            if (record == null)
            {
                failureKey = "PD_DebugNoEligible";
                return null;
            }
            if (negotiator == null)
            {
                failureKey = "PD_DebugNoNegotiator";
                return null;
            }
            if (!DebugResetNegotiationState(pawn))
            {
                failureKey = "PD_DebugDemandRejectedByState";
                return null;
            }

            int now = Find.TickManager.TicksGame;
            int reserve = GetSpendableReserve(record.OriginalFaction, now);
            int materialCap = NegotiationEconomyUtility.CalculateMaterialRewardCap(record.Pawn.MapHeld);
            float memoryMultiplier = GetFactionMemoryMultiplier(record.OriginalFaction, now);
            for (int silver = PrisonerNegotiationUtility.MinimumDemand;
                silver <= PrisonerNegotiationUtility.MaximumDemand;
                silver += 50)
            {
                NegotiationResult preview = PrisonerNegotiationUtility.Evaluate(
                    record,
                    negotiator,
                    new RewardDemand { Silver = silver },
                    reserve,
                    materialCap,
                    1,
                    memoryMultiplier);
                if (preview.Outcome == NegotiationOutcome.Countered)
                {
                    return SubmitPlayerDemand(record, negotiator, silver);
                }
            }

            failureKey = "PD_DebugCounterUnavailable";
            return null;
        }

        public NegotiationResult DebugReviseCounterOffer(Pawn pawn, int silverDelta, out string failureKey)
        {
            failureKey = null;
            PrisonerDeal deal = GetActiveDeal(pawn);
            if (deal == null || deal.State != DealState.Negotiating)
            {
                failureKey = "PD_DebugNoCounteroffer";
                return null;
            }

            RewardDemand demand = deal.LastPlayerDemand?.Clone()
                ?? new RewardDemand { Silver = deal.Rewards?.Silver ?? 0 };
            demand.Silver = Math.Max(
                PrisonerNegotiationUtility.MinimumDemand,
                Math.Min(PrisonerNegotiationUtility.MaximumDemand, demand.Silver + silverDelta));
            NegotiationResult result = RevisePlayerDemand(deal, demand);
            if (result == null)
            {
                failureKey = "PD_DebugDemandRejectedByState";
            }
            return result;
        }

        public bool DebugSetReserveEmpty(Pawn pawn)
        {
            PrisonerRecord record = GetRecord(pawn) ?? RegisterPawn(pawn);
            if (record?.OriginalFaction == null)
            {
                return false;
            }

            FactionNegotiationMemory memory = GetFactionMemory(record.OriginalFaction, true);
            memory.DiplomaticReserve = 0f;
            memory.ReserveUpdatedTick = Find.TickManager.TicksGame;
            return true;
        }

        public bool DebugSimulateSale(Pawn pawn)
        {
            PrisonerDeal deal = GetActiveDeal(pawn);
            if (deal != null)
            {
                FailDeal(deal, DealState.FailedSoldOrTransferred, true);
                return true;
            }

            NotifyPawnSold(pawn);
            return GetRecord(pawn)?.TerminalOutcomeRecorded == true;
        }

        public bool DebugSimulateEnslavement(Pawn pawn)
        {
            PrisonerDeal deal = GetActiveDeal(pawn);
            if (deal != null)
            {
                FailDeal(deal, DealState.FailedEnslaved, true);
                return true;
            }

            NotifyPawnJoinedPlayer(pawn, true);
            return GetRecord(pawn)?.TerminalOutcomeRecorded == true;
        }

        public bool DebugMarkDeliveryPending(Pawn pawn)
        {
            PrisonerDeal deal = GetActiveDeal(pawn);
            if (deal == null)
            {
                return false;
            }

            if (deal.State == DealState.Offered && !AcceptDeal(deal.DealId))
            {
                return false;
            }
            if (deal.State == DealState.AcceptedAwaitingRelease && !OrderRansomRelease(pawn))
            {
                return false;
            }
            if (deal.State != DealState.ReleaseOrdered)
            {
                return deal.State == DealState.FulfillmentPending;
            }

            deal.VanillaReleaseConfirmed = true;
            deal.PrisonerDelivered = true;
            deal.PrisonerDeliveredTick = Find.TickManager.TicksGame;
            deal.State = DealState.FulfillmentPending;
            return true;
        }

        public bool DebugOrderRelease(Pawn pawn)
        {
            return OrderRansomRelease(pawn);
        }

        public bool DebugCancelAcceptedDeal(Pawn pawn)
        {
            return CancelAcceptedDeal(pawn);
        }

        public bool DebugAddTreatmentState(Pawn pawn)
        {
            PrisonerRecord record = GetRecord(pawn) ?? RegisterPawn(pawn);
            Pawn doctor = pawn?.MapHeld?.mapPawns?.FreeColonistsSpawned
                .FirstOrDefault(candidate => candidate != null && candidate != pawn);
            if (record == null || pawn?.health == null || doctor == null)
            {
                return false;
            }

            Hediff injury = HediffMaker.MakeHediff(HediffDefOf.Cut, pawn);
            injury.Severity = 1f;
            pawn.health.AddHediff(injury);
            NotifyPlayerMedicalTreatment(pawn, doctor);
            return record.LastPlayerTreatmentTick == Find.TickManager.TicksGame;
        }

        public string BuildAiContextPreview(Pawn pawn)
        {
            PrisonerRecord record = GetRecord(pawn) ?? RegisterPawn(pawn);
            Faction faction = record?.OriginalFaction ?? pawn?.Faction;
            if (record == null || faction == null)
            {
                return null;
            }

            PrisonerDeal deal = GetActiveDeal(pawn);
            AiNarrativeRecord preview = new AiNarrativeRecord
            {
                RequestId = "pd-debug-preview",
                ContextId = "pd-debug-preview",
                DealId = deal?.DealId,
                Prisoner = pawn,
                PrisonerLoadId = pawn.GetUniqueLoadID(),
                Faction = faction,
                EventKind = deal?.State == DealState.Negotiating
                    ? AiNarrativeEventKind.PlayerDemandCountered
                    : AiNarrativeEventKind.FactionOffer,
                FormalOutcome = deal?.State.ToString() ?? "preview",
                FormalTerms = deal?.Rewards?.Description().ToString() ?? "preview only",
                CandidateVersion = 1
            };
            AiNarrativePrompt prompt = AiNarrativeContextUtility.BuildPrompt(
                preview,
                record,
                GetFactionMemory(faction, true),
                GetFactionFinancialStatus(faction));
            return AiJsonUtility.TrySerialize(prompt, out string json) ? json : null;
        }

        public bool DebugApplyAiAdvisory(
            Pawn pawn,
            string urgency,
            string concession,
            string leverageResponse,
            out string summary)
        {
            summary = null;
            PrisonerDeal deal = GetActiveDeal(pawn);
            if (deal == null || deal.State != DealState.Negotiating)
            {
                return false;
            }

            AiNegotiationAdvisory advisory = new AiNegotiationAdvisory
            {
                Urgency = urgency,
                Concession = concession,
                LeverageResponse = leverageResponse
            };
            int now = Find.TickManager?.TicksGame ?? 0;
            if (!AiNegotiationAdvisoryUtility.TryApply(
                deal,
                advisory,
                GetSpendableReserve(deal.Faction, now, deal),
                NegotiationEconomyUtility.CalculateMaterialRewardCap(deal.Map),
                out RewardDemand adjusted,
                out summary))
            {
                return false;
            }

            deal.Rewards = adjusted;
            deal.SilverAmount = adjusted.Silver;
            deal.NegotiationDemandCost = NegotiationEconomyUtility.CalculateDemandCost(
                deal.Faction,
                adjusted);
            return true;
        }

        public bool DebugIssueNextReward(Pawn pawn, out string rewardKey)
        {
            rewardKey = null;
            PrisonerDeal deal = GetActiveDeal(pawn);
            if (deal == null || !DebugMarkDeliveryPending(pawn))
            {
                return false;
            }

            Map map = ResolveDealMap(deal, true);
            RewardDemand rewards = deal.Rewards ?? new RewardDemand { Silver = deal.SilverAmount };
            if (map == null)
            {
                return false;
            }

            if (rewards.Silver > 0 && !deal.SilverRewardIssued)
            {
                DeliverThings(map, ThingDefOf.Silver, rewards.Silver);
                deal.SilverRewardIssued = true;
                rewardKey = "silver";
                return true;
            }
            if (rewards.SupplyDef != null && rewards.SupplyCount > 0 && !deal.SupplyRewardIssued)
            {
                DeliverThings(map, rewards.SupplyDef, rewards.SupplyCount);
                deal.SupplyRewardIssued = true;
                rewardKey = rewards.SupplyDef.defName;
                return true;
            }
            if (!string.IsNullOrWhiteSpace(rewards.SpecialRewardId)
                && rewards.SpecialRewardThingDef != null
                && rewards.SpecialRewardCount > 0
                && !deal.SpecialRewardIssued)
            {
                DeliverThings(map, rewards.SpecialRewardThingDef, rewards.SpecialRewardCount);
                deal.SpecialRewardIssued = true;
                rewardKey = rewards.SpecialRewardId;
                return true;
            }
            if (rewards.Goodwill > 0 && !deal.GoodwillRewardIssued)
            {
                int amount = Math.Min(rewards.Goodwill, Math.Max(0, 100 - deal.Faction.PlayerGoodwill));
                if (amount > 0 && !deal.Faction.TryAffectGoodwillWith(Faction.OfPlayer, amount, false, false, null))
                {
                    return false;
                }

                deal.GoodwillRewardIssued = true;
                rewardKey = "goodwill";
                return true;
            }
            if (rewards.CeasefireDays > 0 && !deal.CeasefireRewardIssued)
            {
                ActivateCeasefire(deal.Faction, rewards.CeasefireDays, deal.DealId,
                    deal.Prisoner?.LabelShortCap ?? deal.PrisonerLoadId);
                deal.CeasefireRewardIssued = true;
                rewardKey = "ceasefire";
                return true;
            }
            if (rewards.EarlyWarningIntel && !deal.IntelRewardIssued)
            {
                ActivateEarlyWarningIntel(deal.Faction, deal.DealId,
                    deal.Prisoner?.LabelShortCap ?? deal.PrisonerLoadId);
                deal.IntelRewardIssued = true;
                rewardKey = "intel";
                return true;
            }

            return false;
        }

        public bool DebugMakePaymentDueNow(Pawn pawn)
        {
            PrisonerDeal deal = GetActiveDeal(pawn);
            if (deal == null || !DebugMarkDeliveryPending(pawn))
            {
                return false;
            }

            deal.PaymentDueTick = Find.TickManager.TicksGame;
            FulfillDeal(deal);
            return deal.State == DealState.Completed;
        }

        private static Pawn FindDebugNegotiator(Pawn prisoner)
        {
            return prisoner?.MapHeld?.mapPawns?.FreeColonistsSpawned
                .FirstOrDefault(candidate => candidate != null && candidate != prisoner);
        }
    }
}
