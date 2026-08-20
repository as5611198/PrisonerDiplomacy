using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace PrisonerDiplomacy
{
    public sealed partial class PrisonerDiplomacyGameComponent
    {
        private const int IntelDurationTicks = 60 * TicksPerDay;
        private const int MinimumIntelWarningTicks = 15000;
        private const int MaximumIntelWarningTicks = 30000;
        private const int StrategicRetryTicks = 15000;
        private const float CareCreditBudgetMultiplier = 1.10f;

        private static bool IsCareCreditEligible(Faction faction)
        {
            return faction != null
                && faction != Faction.OfPlayer
                && !faction.defeated
                && faction.def?.permanentEnemy == true
                && PrisonerEligibilityUtility.IsNegotiatingFaction(faction)
                && FactionNegotiationUtility.GetType(faction) != FactionNegotiationType.NonNegotiating;
        }

        private FactionStrategicState GetFactionStrategicState(Faction faction, bool create)
        {
            if (faction == null || faction == Faction.OfPlayer)
            {
                return null;
            }

            FactionStrategicState state = factionStrategicStates.FirstOrDefault(item => item.Faction == faction);
            if (state == null && create)
            {
                state = new FactionStrategicState { Faction = faction };
                factionStrategicStates.Add(state);
            }
            return state;
        }

        private void ActivateCeasefire(Faction faction, int days, string dealId, string pawnLabel)
        {
            if (faction == null || days <= 0)
            {
                return;
            }

            FactionStrategicState state = GetFactionStrategicState(faction, true);
            int now = Find.TickManager.TicksGame;
            int anchor = Math.Max(now, state.CeasefireExpiresTick);
            state.CeasefireExpiresTick = anchor + days * TicksPerDay;
            state.CeasefireSourceDealId = dealId;
            state.CeasefireSourcePawnLabel = pawnLabel;
        }

        private void ActivateEarlyWarningIntel(Faction faction, string dealId, string pawnLabel)
        {
            if (faction == null)
            {
                return;
            }

            FactionStrategicState state = GetFactionStrategicState(faction, true);
            state.IntelAvailable = true;
            state.IntelExpiresTick = Find.TickManager.TicksGame + IntelDurationTicks;
            state.IntelSourceDealId = dealId;
            state.IntelSourcePawnLabel = pawnLabel;
        }

        public bool IsCeasefireActive(Faction faction)
        {
            FactionStrategicState state = GetFactionStrategicState(faction, false);
            return state != null && state.CeasefireExpiresTick > Find.TickManager.TicksGame;
        }

        private static bool HasActiveStrategicStatus(FactionStrategicState state, int now)
        {
            return state?.Faction != null
                && (state.CeasefireExpiresTick > now
                    || state.IntelAvailable && state.IntelExpiresTick > now
                    || state.WarnedRaidFireTick > now
                    || state.CareCreditAvailable);
        }

        internal bool HasCareCredit(Faction faction)
        {
            return IsCareCreditEligible(faction)
                && GetFactionStrategicState(faction, false)?.CareCreditAvailable == true;
        }

        internal float GetNegotiationBudgetMultiplier(PrisonerRecord record, PrisonerDeal deal = null)
        {
            if (deal?.CareCreditApplied == true)
            {
                return CareCreditBudgetMultiplier;
            }

            return deal == null && HasCareCredit(record?.OriginalFaction)
                ? CareCreditBudgetMultiplier
                : 1f;
        }

        private bool GrantCareCredit(Faction faction, string sourcePawnLabel, string sourceDealId, bool notify)
        {
            if (!IsCareCreditEligible(faction))
            {
                return false;
            }

            FactionStrategicState state = GetFactionStrategicState(faction, true);
            if (!state.CareCreditAvailable)
            {
                state.CareCreditAvailable = true;
                state.CareCreditSourceDealId = sourceDealId;
                state.CareCreditSourcePawnLabel = sourcePawnLabel;
                state.CareCreditGrantedTick = Find.TickManager.TicksGame;
            }

            if (notify && !GenCommandLine.CommandLineArgPassed("pdsmoketest"))
            {
                Find.LetterStack.ReceiveLetter(
                    "PD_CareCreditLabel".Translate(sourcePawnLabel ?? "?"),
                    "PD_CareCreditGrantedText".Translate(
                        faction.NameColored,
                        sourcePawnLabel ?? "?",
                        sourceDealId ?? "?"),
                    LetterDefOf.PositiveEvent,
                    LookTargets.Invalid,
                    faction);
            }

            return true;
        }

        private bool ConsumeCareCredit(Faction faction)
        {
            FactionStrategicState state = GetFactionStrategicState(faction, false);
            if (state == null || !state.CareCreditAvailable)
            {
                return false;
            }

            state.CareCreditAvailable = false;
            return true;
        }

        internal bool ShouldAllowResolvedRaid(
            IncidentWorker_RaidEnemy worker,
            IncidentParms parms,
            bool forceCeasefireNotice = false)
        {
            if (!IsControlledProactiveRaid(parms))
            {
                return true;
            }

            Faction faction = parms.faction;
            Map map = parms.target as Map;
            FactionStrategicState state = GetFactionStrategicState(faction, false);
            if (state == null)
            {
                return true;
            }

            int now = Find.TickManager.TicksGame;
            if (state.CeasefireExpiresTick > now)
            {
                state.ClearWarnedRaid();
                if (forceCeasefireNotice
                    || state.LastCeasefireNoticeTick < 0
                    || now - state.LastCeasefireNoticeTick >= TicksPerDay)
                {
                    state.LastCeasefireNoticeTick = now;
                    Messages.Message("PD_CeasefireRaidBlocked".Translate(
                        faction.NameColored,
                        (state.CeasefireExpiresTick - now).ToStringTicksToPeriod()),
                        MessageTypeDefOf.NeutralEvent,
                        false);
                }
                return false;
            }

            if (state.WarnedRaidFireTick >= 0
                && now >= state.WarnedRaidFireTick
                && now <= state.WarnedRaidFireTick + TicksPerDay
                && state.WarnedRaidDef == worker.def
                && state.WarnedRaidMap == map)
            {
                state.ClearWarnedRaid();
                return true;
            }

            if (!state.IntelAvailable || state.IntelExpiresTick <= now)
            {
                return true;
            }

            int seed = Gen.HashCombineInt(GenText.StableStringHash(faction.GetUniqueLoadID()), now / ScanIntervalTicks);
            Rand.PushState(seed);
            int warningTicks = Rand.RangeInclusive(MinimumIntelWarningTicks, MaximumIntelWarningTicks);
            Rand.PopState();
            int fireTick = now + warningTicks;
            IncidentParms delayedParms = parms.ShallowCopy();
            if (!Find.Storyteller.incidentQueue.Add(worker.def, fireTick, delayedParms, StrategicRetryTicks))
            {
                return true;
            }

            state.IntelAvailable = false;
            state.IntelExpiresTick = -1;
            state.WarnedRaidFireTick = fireTick;
            state.WarnedRaidDef = worker.def;
            state.WarnedRaidMap = map;
            state.WarnedRaidPoints = parms.points;
            Find.LetterStack.ReceiveLetter(
                "PD_IntelWarningLabel".Translate(faction.NameColored),
                "PD_IntelWarningText".Translate(
                    faction.NameColored,
                    DescribeThreatBand(parms),
                    DescribeRaidStyle(parms, faction),
                    DescribeRaidDirection(parms, map),
                    warningTicks.ToStringTicksToPeriod(),
                    state.IntelSourcePawnLabel ?? "?",
                    state.IntelSourceDealId ?? "?"),
                LetterDefOf.ThreatSmall,
                map != null ? new LookTargets(map.Parent) : LookTargets.Invalid,
                faction);
            return false;
        }

        private static bool IsControlledProactiveRaid(IncidentParms parms)
        {
            Map map = parms?.target as Map;
            return parms?.faction != null
                && map?.IsPlayerHome == true
                && !parms.forced
                && parms.quest == null
                && string.IsNullOrEmpty(parms.questTag);
        }

        private static string DescribeThreatBand(IncidentParms parms)
        {
            float baseline = parms?.target == null ? 0f : StorytellerUtility.DefaultThreatPointsNow(parms.target);
            float points = parms?.points > 0f ? parms.points : baseline;
            float ratio = baseline <= 0f ? 1f : points / baseline;
            if (ratio < 0.75f)
            {
                return "PD_IntelThreatLow".Translate();
            }
            if (ratio > 1.25f)
            {
                return "PD_IntelThreatHigh".Translate();
            }
            return "PD_IntelThreatMedium".Translate();
        }

        private static string DescribeRaidStyle(IncidentParms parms, Faction faction)
        {
            if (parms?.raidStrategy != null)
            {
                return parms.raidStrategy.LabelCap;
            }

            TechLevel techLevel = faction?.def?.techLevel ?? TechLevel.Neolithic;
            if (techLevel <= TechLevel.Neolithic)
            {
                return "PD_IntelStyleTribal".Translate();
            }
            if (techLevel >= TechLevel.Spacer)
            {
                return "PD_IntelStyleAdvanced".Translate();
            }
            return "PD_IntelStyleIndustrial".Translate();
        }

        private static string DescribeRaidDirection(IncidentParms parms, Map map)
        {
            if (map == null || parms == null || !parms.spawnCenter.IsValid)
            {
                return "PD_IntelDirectionUnknown".Translate();
            }

            IntVec3 offset = parms.spawnCenter - map.Center;
            if (Math.Abs(offset.x) > Math.Abs(offset.z))
            {
                return offset.x >= 0 ? "PD_IntelDirectionEast".Translate() : "PD_IntelDirectionWest".Translate();
            }
            return offset.z >= 0 ? "PD_IntelDirectionNorth".Translate() : "PD_IntelDirectionSouth".Translate();
        }

        public void NotifyPlayerAttackAgainstFaction(Faction faction, string targetLabel, DamageInfo damageInfo)
        {
            if (faction == null
                || faction == Faction.OfPlayer
                || damageInfo.Instigator?.Faction != Faction.OfPlayer)
            {
                return;
            }

            FactionStrategicState state = GetFactionStrategicState(faction, false);
            int now = Find.TickManager.TicksGame;
            if (state == null || state.CeasefireExpiresTick <= now
                || state.LastCeasefireBreachTick >= 0
                    && now - state.LastCeasefireBreachTick < ScanIntervalTicks)
            {
                return;
            }

            string sourceDeal = state.CeasefireSourceDealId ?? "?";
            string sourcePawn = state.CeasefireSourcePawnLabel ?? "?";
            state.CeasefireExpiresTick = -1;
            state.LastCeasefireBreachTick = now;
            ApplyMemoryChange(faction, -35f, 0f, 30f,
                "PD_MemoryEventCeasefireBroken", targetLabel ?? faction.Name, true);
            Find.LetterStack.ReceiveLetter(
                "PD_CeasefireBrokenLabel".Translate(faction.NameColored),
                "PD_CeasefireBrokenText".Translate(
                    faction.NameColored,
                    targetLabel ?? "?",
                    sourcePawn,
                    sourceDeal),
                LetterDefOf.NegativeEvent,
                damageInfo.Instigator != null ? new LookTargets(damageInfo.Instigator) : LookTargets.Invalid,
                faction);
        }

        public bool TrySecurePirateDeal(Pawn pawn)
        {
            PrisonerDeal deal = GetActiveDeal(pawn);
            if (deal == null
                || deal.State != DealState.AcceptedAwaitingRelease
                || deal.PirateRisk == PirateDealRisk.None)
            {
                return false;
            }

            RewardDemand safer = NegotiationEconomyUtility.CreateSaferPirateTerms(deal.Faction, deal.Rewards);
            if (safer == null)
            {
                Messages.Message("PD_SecurePirateDealUnavailable".Translate(), pawn,
                    MessageTypeDefOf.RejectInput, false);
                return false;
            }

            TaggedString oldTerms = deal.Rewards.Description();
            deal.Rewards = safer;
            deal.SilverAmount = safer.Silver;
            deal.NegotiationDemandCost = NegotiationEconomyUtility.CalculateDemandCost(deal.Faction, safer);
            deal.PirateRisk = PirateDealRisk.None;
            deal.PirateRiskMitigated = true;
            deal.PirateRiskEventTick = -1;
            Messages.Message("PD_SecurePirateDealSucceeded".Translate(oldTerms, safer.Description()), pawn,
                MessageTypeDefOf.PositiveEvent, false);
            return true;
        }

        private void UpdatePirateRiskEvent(PrisonerDeal deal, int now)
        {
            if (deal == null
                || deal.PirateRiskMitigated
                || deal.PirateRiskEventTriggered
                || (deal.PirateRisk != PirateDealRisk.RescueRaid
                    && deal.PirateRisk != PirateDealRisk.JailbreakIncitement)
                || (deal.State != DealState.AcceptedAwaitingRelease && deal.State != DealState.ReleaseOrdered))
            {
                return;
            }

            if (deal.PirateRiskEventTick < 0)
            {
                int anchor = deal.AcceptedTick >= 0 ? deal.AcceptedTick : now;
                deal.PirateRiskEventTick = anchor
                    + FactionNegotiationUtility.CalculateRiskEventDelayTicks(deal.DealId, deal.PirateRisk);
            }
            if (now < deal.PirateRiskEventTick)
            {
                return;
            }

            bool triggered = deal.PirateRisk == PirateDealRisk.RescueRaid
                ? TryExecuteRescueOperation(
                    deal.Faction,
                    deal.Map,
                    deal.Prisoner,
                    0.65f,
                    "PD_PirateRescueRaidLabel".Translate(deal.Prisoner?.LabelShortCap ?? deal.PrisonerLoadId),
                    "PD_PirateRescueRaidText".Translate(
                        deal.Faction?.NameColored ?? "?",
                        deal.Prisoner?.LabelShortCap ?? deal.PrisonerLoadId,
                        deal.DealId))
                : TryTriggerPirateJailbreak(deal);
            if (triggered)
            {
                deal.PirateRiskEventTriggered = true;
                return;
            }

            deal.PirateRiskEventAttempts++;
            if (deal.PirateRiskEventAttempts >= 4)
            {
                deal.PirateRiskEventTriggered = true;
            }
            else
            {
                deal.PirateRiskEventTick = now + StrategicRetryTicks;
            }
        }

        private bool TryTriggerPirateJailbreak(PrisonerDeal deal)
        {
            Pawn pawn = deal?.Prisoner;
            if (pawn == null || !pawn.IsPrisonerOfColony || !PrisonBreakUtility.CanParticipateInPrisonBreak(pawn))
            {
                return false;
            }

            Find.LetterStack.ReceiveLetter(
                "PD_PirateJailbreakLabel".Translate(pawn.LabelShortCap),
                "PD_PirateJailbreakText".Translate(
                    deal.Faction?.NameColored ?? "?",
                    pawn.LabelShortCap,
                    deal.DealId),
                LetterDefOf.ThreatSmall,
                new LookTargets(pawn),
                deal.Faction);
            PrisonBreakUtility.StartPrisonBreak(pawn);
            return true;
        }

        private void UpdateStrategicConsequences(int now)
        {
            foreach (FactionStrategicState state in factionStrategicStates)
            {
                if (state.CeasefireExpiresTick >= 0 && state.CeasefireExpiresTick <= now)
                {
                    state.CeasefireExpiresTick = -1;
                    Messages.Message("PD_CeasefireExpired".Translate(state.Faction?.NameColored ?? "?"),
                        MessageTypeDefOf.NeutralEvent, false);
                }
                if (state.IntelAvailable && state.IntelExpiresTick >= 0 && state.IntelExpiresTick <= now)
                {
                    state.IntelAvailable = false;
                    state.IntelExpiresTick = -1;
                    Messages.Message("PD_IntelExpired".Translate(state.Faction?.NameColored ?? "?"),
                        MessageTypeDefOf.NeutralEvent, false);
                }
                if (state.WarnedRaidFireTick >= 0 && state.WarnedRaidFireTick + TicksPerDay < now)
                {
                    state.ClearWarnedRaid();
                }
            }

            if (PrisonerDiplomacyMod.Settings?.EnableStrategicConsequences == false)
            {
                return;
            }

            foreach (StrategicFollowupEvent followup in strategicFollowups
                .Where(item => !item.Triggered && item.TriggerTick <= now)
                .ToList())
            {
                try
                {
                    TryExecuteFollowup(followup, now);
                }
                catch (Exception exception)
                {
                    followup.Attempts++;
                    followup.TriggerTick = now + StrategicRetryTicks;
                    CompatibilityDiagnostics.LogErrorOnce(
                        "followup:" + (followup.EventId ?? "missing"),
                        "Deferred a strategic follow-up after a compatibility exception.",
                        exception);
                }
            }
            strategicFollowups.RemoveAll(item => item.Triggered);
        }

        private void TryExecuteFollowup(StrategicFollowupEvent followup, int now)
        {
            Map map = followup.Map != null && Find.Maps.Contains(followup.Map)
                ? followup.Map
                : Find.Maps.FirstOrDefault(candidate => candidate.IsPlayerHome);
            if (map == null || followup.Faction == null || followup.Faction.defeated)
            {
                RetryOrFinishFollowup(followup, now);
                return;
            }

            if (followup.Kind == StrategicFollowupKind.RescueRaid)
            {
                Pawn sourcePawn = followup.SourcePawn;
                PrisonerRecord sourceRecord = sourcePawn == null ? null : GetRecord(sourcePawn);
                if (sourcePawn == null
                    || sourcePawn.Dead
                    || !sourcePawn.IsPrisonerOfColony
                    || sourceRecord?.TerminalOutcomeRecorded == true)
                {
                    followup.Triggered = true;
                    return;
                }
                if (IsCeasefireActive(followup.Faction))
                {
                    FactionStrategicState state = GetFactionStrategicState(followup.Faction, false);
                    followup.TriggerTick = Math.Max(now + StrategicRetryTicks,
                        (state?.CeasefireExpiresTick ?? now) + StrategicRetryTicks);
                    return;
                }
            }

            bool succeeded;
            switch (followup.Kind)
            {
                case StrategicFollowupKind.PositiveGift:
                    if (IsCareCreditEligible(followup.Faction))
                    {
                        succeeded = GrantCareCredit(
                            followup.Faction,
                            followup.SourcePawnLabel,
                            followup.SourceDealId,
                            true);
                        break;
                    }

                    int amount = PositiveGiftAmount(followup);
                    IntVec3 cell = DeliverThings(map, ThingDefOf.Silver, amount);
                    Find.LetterStack.ReceiveLetter(
                        "PD_PositiveReturnLabel".Translate(followup.SourcePawnLabel ?? "?"),
                        "PD_PositiveReturnText".Translate(
                            followup.Faction.NameColored,
                            followup.SourcePawnLabel ?? "?",
                            followup.SourceDealId ?? "?",
                            amount),
                        LetterDefOf.PositiveEvent,
                        new LookTargets(new TargetInfo(cell, map)),
                        followup.Faction);
                    succeeded = true;
                    break;
                case StrategicFollowupKind.RescueRaid:
                    succeeded = TryExecuteRescueOperation(
                        followup.Faction,
                        map,
                        followup.SourcePawn,
                        0.75f,
                        "PD_RescueRaidLabel".Translate(followup.SourcePawnLabel ?? "?"),
                        "PD_RescueRaidText".Translate(
                            followup.Faction.NameColored,
                            followup.SourcePawnLabel ?? "?"));
                    break;
                case StrategicFollowupKind.RetaliationRaid:
                    succeeded = TryExecuteCausalRaid(
                        followup.Faction,
                        map,
                        1.05f,
                        "PD_RetaliationRaidLabel".Translate(followup.SourcePawnLabel ?? "?"),
                        "PD_RetaliationRaidText".Translate(
                            followup.Faction.NameColored,
                            followup.SourcePawnLabel ?? "?",
                            followup.SourceDealId ?? "?"),
                        RetaliationMinimumPoints(followup));
                    break;
                default:
                    succeeded = TryExecuteCausalRaid(
                        followup.Faction,
                        map,
                        0.85f,
                        "PD_PirateAmbushLabel".Translate(followup.SourcePawnLabel ?? "?"),
                        "PD_PirateAmbushText".Translate(
                            followup.Faction.NameColored,
                            followup.SourcePawnLabel ?? "?",
                            followup.SourceDealId ?? "?"));
                    break;
            }

            if (succeeded)
            {
                followup.Triggered = true;
            }
            else
            {
                RetryOrFinishFollowup(followup, now);
            }
        }

        private static int PositiveGiftAmount(StrategicFollowupEvent followup)
        {
            int seed = GenText.StableStringHash(followup.EventId ?? string.Empty);
            return 100 + (Math.Abs(seed) % 6) * 50;
        }

        private static void RetryOrFinishFollowup(StrategicFollowupEvent followup, int now)
        {
            followup.Attempts++;
            if (followup.Attempts >= 4)
            {
                followup.Triggered = true;
            }
            else
            {
                followup.TriggerTick = now + StrategicRetryTicks;
            }
        }

        private static bool TryExecuteCausalRaid(
            Faction faction,
            Map map,
            float pointsFactor,
            TaggedString letterLabel,
            TaggedString letterText,
            float minimumPoints = 200f)
        {
            if (faction == null
                || map == null
                || faction.defeated
                || !faction.HostileTo(Faction.OfPlayer))
            {
                return false;
            }

            IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, map);
            parms.faction = faction;
            parms.points = Math.Max(minimumPoints, StorytellerUtility.DefaultThreatPointsNow(map) * pointsFactor);
            parms.forced = true;
            parms.bypassStorytellerSettings = true;
            parms.sendLetter = true;
            parms.customLetterLabel = letterLabel.ToString();
            parms.customLetterText = letterText.ToString();
            parms.customLetterDef = LetterDefOf.ThreatBig;
            using (CausalRaidContext.Enter(faction))
            {
                return IncidentDefOf.RaidEnemy.Worker.TryExecute(parms);
            }
        }

        private bool TryExecuteRescueOperation(
            Faction faction,
            Map map,
            Pawn sourcePawn,
            float pointsFactor,
            TaggedString letterLabel,
            TaggedString letterText)
        {
            if (faction == null
                || map == null
                || sourcePawn == null
                || sourcePawn.Dead
                || sourcePawn.Destroyed
                || !sourcePawn.IsPrisonerOfColony
                || sourcePawn.MapHeld != map
                || faction.defeated)
            {
                return false;
            }

            // Rescue is a targeted extraction, not a normal assault. Cancel an
            // accepted deal without treating the extraction as player betrayal,
            // then let the original faction escort its prisoner off the map.
            PrisonerDeal activeDeal = GetActiveDeal(sourcePawn);
            if (activeDeal != null && activeDeal.IsActive)
            {
                activeDeal.FailureNotified = true;
                FailDeal(activeDeal, DealState.Cancelled, false, false);
            }

            List<Pawn> escorts = GenerateRescueEscorts(faction, map, pointsFactor);
            SpawnRescueEscorts(faction, map, escorts);

            GenGuest.PrisonerRelease(sourcePawn);
            sourcePawn.ExitMap(false, Rot4.North);
            Find.LetterStack.ReceiveLetter(
                letterLabel,
                letterText,
                LetterDefOf.ThreatBig,
                new LookTargets(map.Parent),
                faction);
            return true;
        }

        private static List<Pawn> GenerateRescueEscorts(Faction faction, Map map, float pointsFactor)
        {
            List<Pawn> escorts = new List<Pawn>();
            IncidentParms escortParms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, map);
            escortParms.faction = faction;
            escortParms.points = Math.Max(200f, StorytellerUtility.DefaultThreatPointsNow(map) * pointsFactor);
            escortParms.pawnGroupKind = PawnGroupKindDefOf.Combat;
            escortParms.raidStrategy = RaidStrategyDefOf.ImmediateAttack;
            if (!RaidGenerationUtility.HasUsablePawnGroupMaker(escortParms))
            {
                return escorts;
            }

            try
            {
                escorts.AddRange(PawnGroupMakerUtility.GeneratePawns(
                    RaidGenerationUtility.BuildPawnGroupMakerParms(escortParms), false).Take(6));
            }
            catch (Exception exception)
            {
                CompatibilityDiagnostics.LogErrorOnce(
                    "rescue-escort-generation:" + faction.GetUniqueLoadID(),
                    "Could not generate a non-destructive rescue escort.",
                    exception);
            }
            return escorts;
        }

        private static void SpawnRescueEscorts(Faction faction, Map map, List<Pawn> escorts)
        {
            if (escorts == null || escorts.Count == 0)
            {
                return;
            }

            if (!CellFinder.TryFindRandomEdgeCellWith(
                cell => cell.Standable(map),
                map,
                CellFinder.EdgeRoadChance_Hostile,
                out IntVec3 edgeCell))
            {
                foreach (Pawn pawn in escorts.Where(item => item != null && !item.Destroyed))
                {
                    pawn.Destroy();
                }
                return;
            }

            List<Pawn> spawned = new List<Pawn>();
            foreach (Pawn pawn in escorts)
            {
                if (pawn == null || pawn.Destroyed
                    || !CellFinder.TryFindRandomSpawnCellForPawnNear(edgeCell, map, out IntVec3 spawnCell))
                {
                    pawn?.Destroy();
                    continue;
                }

                GenSpawn.Spawn(pawn, spawnCell, map, WipeMode.Vanish);
                spawned.Add(pawn);
            }

            if (spawned.Count > 0)
            {
                LordMaker.MakeNewLord(
                    faction,
                    new LordJob_ExitMapBest(LocomotionUrgency.Jog, false, true),
                    map,
                    spawned);
            }
        }

        private float RetaliationMinimumPoints(StrategicFollowupEvent followup)
        {
            PrisonerImportance importance = GetRecord(followup?.SourcePawn)?.Importance
                ?? PrisonerImportance.Regular;
            if (importance >= PrisonerImportance.Leader)
            {
                return 600f;
            }
            if (importance >= PrisonerImportance.Core)
            {
                return 450f;
            }
            return 300f;
        }

        private void ScheduleImportantPrisonerRescue(PrisonerRecord record)
        {
            if (record == null
                || record.Importance < PrisonerImportance.Core
                || PrisonerDiplomacyMod.Settings?.EnableStrategicConsequences == false
                || GenCommandLine.CommandLineArgPassed("pdsmoketest"))
            {
                return;
            }

            int seed = Gen.HashCombineInt(GenText.StableStringHash(record.PawnLoadId ?? string.Empty), record.CapturedTick);
            float chance = record.Importance == PrisonerImportance.Leader ? 0.85f : 0.55f;
            if (!Rand.ChanceSeeded(chance, seed))
            {
                return;
            }

            Rand.PushState(Gen.HashCombineInt(seed, 6203));
            int delay = Rand.RangeInclusive(3 * TicksPerDay, 6 * TicksPerDay);
            Rand.PopState();
            AddStrategicFollowup(StrategicFollowupKind.RescueRaid, record, null,
                record.CapturedTick + delay);
        }

        private int CancelPendingRescueFollowups(PrisonerRecord record)
        {
            if (record == null || string.IsNullOrEmpty(record.PawnLoadId))
            {
                return 0;
            }

            int cancelled = 0;
            foreach (StrategicFollowupEvent followup in strategicFollowups.Where(item =>
                !item.Triggered
                && item.Kind == StrategicFollowupKind.RescueRaid
                && item.SourcePawnLoadId == record.PawnLoadId))
            {
                followup.Triggered = true;
                cancelled++;
            }
            return cancelled;
        }

        private void ScheduleImportantDeathRetaliation(PrisonerRecord record, bool agreementBroken)
        {
            if (record?.OriginalFaction == null
                || PrisonerDiplomacyMod.Settings?.EnableStrategicConsequences == false
                || GenCommandLine.CommandLineArgPassed("pdsmoketest"))
            {
                return;
            }

            PrisonerDeal sourceDeal = deals
                .Where(item => item.PrisonerLoadId == record.PawnLoadId)
                .OrderByDescending(item => item.CreatedTick)
                .FirstOrDefault();
            string dealId = sourceDeal?.DealId;
            string pawnLabel = record.Pawn?.LabelShortCap ?? record.PawnLoadId;
            Find.LetterStack.ReceiveLetter(
                "PD_ImportantDeathWarningLabel".Translate(pawnLabel),
                "PD_ImportantDeathWarningText".Translate(
                    record.OriginalFaction.NameColored,
                    pawnLabel,
                    dealId ?? "?",
                    agreementBroken ? "PD_DeathWarningAgreementBroken".Translate() : "PD_DeathWarningPlayerResponsible".Translate()),
                LetterDefOf.NegativeEvent,
                record.Pawn != null ? new LookTargets(record.Pawn) : LookTargets.Invalid,
                record.OriginalFaction);

            int seed = Gen.HashCombineInt(GenText.StableStringHash(record.PawnLoadId ?? string.Empty), Find.TickManager.TicksGame);
            Rand.PushState(seed);
            int delay = Rand.RangeInclusive(90000, 180000);
            Rand.PopState();
            AddStrategicFollowup(StrategicFollowupKind.RetaliationRaid, record, dealId,
                Find.TickManager.TicksGame + delay);
        }

        private void SchedulePositiveReturn(PrisonerDeal deal, PrisonerRecord record)
        {
            if (record == null
                || !record.CriticalRecoveryRecorded
                || PrisonerDiplomacyMod.Settings?.EnableStrategicConsequences == false
                || GenCommandLine.CommandLineArgPassed("pdsmoketest"))
            {
                return;
            }

            string dealId = deal?.DealId;
            if (IsCareCreditEligible(record.OriginalFaction))
            {
                GrantCareCredit(
                    record.OriginalFaction,
                    record.Pawn?.LabelShortCap,
                    dealId,
                    true);
                return;
            }

            if (strategicFollowups.Any(item => item.Kind == StrategicFollowupKind.PositiveGift
                && item.SourcePawnLoadId == record.PawnLoadId
                && item.SourceDealId == dealId))
            {
                return;
            }

            int seed = Gen.HashCombineInt(GenText.StableStringHash(record.PawnLoadId ?? string.Empty), deal?.CompletedTick ?? Find.TickManager.TicksGame);
            if (!Rand.ChanceSeeded(0.70f, seed))
            {
                return;
            }
            Rand.PushState(Gen.HashCombineInt(seed, 6229));
            int delay = Rand.RangeInclusive(6 * TicksPerDay, 14 * TicksPerDay);
            Rand.PopState();
            AddStrategicFollowup(StrategicFollowupKind.PositiveGift, record, dealId,
                Find.TickManager.TicksGame + delay);
        }

        public bool DebugForcePositiveReturnGift(Pawn pawn)
        {
            return DebugForcePositiveReturnGift(pawn, out _);
        }

        public bool DebugForcePositiveReturnGift(Pawn pawn, out bool careCredit)
        {
            careCredit = false;
            PrisonerRecord record = GetRecord(pawn) ?? RegisterPawn(pawn);
            if (record?.OriginalFaction == null || record.OriginalFaction.defeated)
            {
                return false;
            }

            string dealId = deals
                .Where(item => item.PrisonerLoadId == record.PawnLoadId)
                .OrderByDescending(item => item.CreatedTick)
                .Select(item => item.DealId)
                .FirstOrDefault();
            int now = Find.TickManager.TicksGame;
            if (IsCareCreditEligible(record.OriginalFaction))
            {
                careCredit = true;
                return GrantCareCredit(
                    record.OriginalFaction,
                    record.Pawn?.LabelShortCap,
                    dealId ?? "DEBUG",
                    false);
            }

            AddStrategicFollowup(StrategicFollowupKind.PositiveGift, record, dealId, now);
            StrategicFollowupEvent followup = strategicFollowups.LastOrDefault(item =>
                !item.Triggered
                && item.Kind == StrategicFollowupKind.PositiveGift
                && item.SourcePawnLoadId == record.PawnLoadId
                && item.SourceDealId == dealId);
            if (followup == null)
            {
                return false;
            }

            TryExecuteFollowup(followup, now);
            strategicFollowups.RemoveAll(item => item.Triggered);
            return followup.Triggered;
        }

        private void SchedulePirateAmbush(PrisonerDeal deal)
        {
            if (deal == null
                || deal.PirateRisk != PirateDealRisk.Ambush
                || deal.PirateRiskMitigated
                || PrisonerDiplomacyMod.Settings?.EnableStrategicConsequences == false
                || GenCommandLine.CommandLineArgPassed("pdsmoketest"))
            {
                return;
            }

            ScheduleRansomAmbushRetaliationEvent(deal);
        }

        private void AddStrategicFollowup(
            StrategicFollowupKind kind,
            PrisonerRecord record,
            string dealId,
            int triggerTick,
            PrisonerDeal deal = null)
        {
            Faction faction = record?.OriginalFaction ?? deal?.Faction;
            if (faction == null)
            {
                return;
            }

            string pawnLoadId = record?.PawnLoadId ?? deal?.PrisonerLoadId;
            string pawnLabel = record?.Pawn?.LabelShortCap
                ?? deal?.Prisoner?.LabelShortCap
                ?? pawnLoadId;
            if (strategicFollowups.Any(item => !item.Triggered
                && item.Kind == kind
                && item.SourcePawnLoadId == pawnLoadId
                && item.SourceDealId == dealId))
            {
                return;
            }

            strategicFollowups.Add(new StrategicFollowupEvent
            {
                EventId = "PD-EVT-" + nextStrategicEventSequence++.ToString("D6"),
                Kind = kind,
                Faction = faction,
                Map = deal?.Map ?? record?.Pawn?.MapHeld ?? Find.Maps.FirstOrDefault(map => map.IsPlayerHome),
                SourcePawn = record?.Pawn ?? deal?.Prisoner,
                SourcePawnLoadId = pawnLoadId,
                SourcePawnLabel = pawnLabel,
                SourceDealId = dealId,
                TriggerTick = triggerTick
            });
        }

        public string GetFactionStrategicStatus(Faction faction)
        {
            FactionStrategicState state = GetFactionStrategicState(faction, false);
            if (state == null)
            {
                return string.Empty;
            }

            int now = Find.TickManager.TicksGame;
            List<string> parts = new List<string>();
            if (state.CeasefireExpiresTick > now)
            {
                parts.Add("PD_StrategicStatusCeasefire".Translate(
                    (state.CeasefireExpiresTick - now).ToStringTicksToPeriod(),
                    state.CeasefireSourcePawnLabel ?? "?",
                    state.CeasefireSourceDealId ?? "?"));
            }
            if (state.IntelAvailable && state.IntelExpiresTick > now)
            {
                parts.Add("PD_StrategicStatusIntel".Translate(
                    (state.IntelExpiresTick - now).ToStringTicksToPeriod(),
                    state.IntelSourcePawnLabel ?? "?",
                    state.IntelSourceDealId ?? "?"));
            }
            if (state.WarnedRaidFireTick > now)
            {
                parts.Add("PD_StrategicStatusWarnedRaid".Translate(
                    (state.WarnedRaidFireTick - now).ToStringTicksToPeriod()));
            }
            if (state.CareCreditAvailable)
            {
                parts.Add("PD_StrategicStatusCareCredit".Translate(
                    state.CareCreditSourcePawnLabel ?? "?",
                    state.CareCreditSourceDealId ?? "?"));
            }
            return parts.Count == 0
                ? string.Empty
                : "PD_StrategicStatus".Translate(string.Join("\n", parts)).ToString();
        }

        public bool DebugGrantCeasefire(Pawn pawn)
        {
            PrisonerRecord record = GetRecord(pawn) ?? RegisterPawn(pawn);
            if (record?.OriginalFaction == null)
            {
                return false;
            }
            ActivateCeasefire(record.OriginalFaction, 10, "DEBUG", pawn.LabelShortCap);
            Messages.Message("PD_DebugCeasefireGranted".Translate(record.OriginalFaction.NameColored),
                MessageTypeDefOf.PositiveEvent, false);
            return true;
        }

        public bool DebugLogStrategicState(Pawn pawn)
        {
            PrisonerRecord record = GetRecord(pawn) ?? RegisterPawn(pawn);
            Faction faction = record?.OriginalFaction;
            if (faction == null)
            {
                return false;
            }

            FactionStrategicState state = GetFactionStrategicState(faction, false);
            int now = Find.TickManager.TicksGame;
            Log.Message(
                "[Prisoner Diplomacy Debug] strategic faction=" + faction.Name
                + " loadId=" + faction.GetUniqueLoadID()
                + " now=" + now
                + " ceasefireExpires=" + (state?.CeasefireExpiresTick ?? -1)
                + " ceasefireRemaining=" + (state != null && state.CeasefireExpiresTick > now
                    ? (state.CeasefireExpiresTick - now).ToStringTicksToPeriod()
                    : "none")
                + " lastCeasefireNotice=" + (state?.LastCeasefireNoticeTick ?? -1)
                + " lastCeasefireBreach=" + (state?.LastCeasefireBreachTick ?? -1)
                + " intelExpires=" + (state?.IntelExpiresTick ?? -1)
                + " warnedRaidFire=" + (state?.WarnedRaidFireTick ?? -1));
            return true;
        }

        public bool DebugGrantIntel(Pawn pawn)
        {
            PrisonerRecord record = GetRecord(pawn) ?? RegisterPawn(pawn);
            if (record?.OriginalFaction == null)
            {
                return false;
            }
            ActivateEarlyWarningIntel(record.OriginalFaction, "DEBUG", pawn.LabelShortCap);
            Messages.Message("PD_DebugIntelGranted".Translate(record.OriginalFaction.NameColored),
                MessageTypeDefOf.PositiveEvent, false);
            return true;
        }

        public bool DebugTriggerEligibleRaid(Pawn pawn)
        {
            PrisonerRecord record = GetRecord(pawn) ?? RegisterPawn(pawn);
            Map map = pawn?.MapHeld ?? Find.Maps.FirstOrDefault(candidate => candidate.IsPlayerHome);
            if (record?.OriginalFaction == null || map == null)
            {
                return false;
            }

            IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, map);
            parms.faction = record.OriginalFaction;
            parms.points = Math.Max(200f, StorytellerUtility.DefaultThreatPointsNow(map) * 0.35f);
            parms.forced = false;
            parms.sendLetter = true;

            IncidentWorker_RaidEnemy worker = IncidentDefOf.RaidEnemy.Worker as IncidentWorker_RaidEnemy;
            if (worker == null)
            {
                return false;
            }

            // Probe the requested faction before vanilla raid resolution can substitute
            // another hostile faction. This makes the debug action deterministic for
            // ceasefire testing, including factions that currently support the player.
            if (IsCeasefireActive(record.OriginalFaction)
                && !ShouldAllowResolvedRaid(worker, parms, true))
            {
                return false;
            }

            if (!RaidGenerationUtility.HasUsablePawnGroupMaker(parms))
            {
                Messages.Message("PD_DebugRaidNoPawnGroupMaker".Translate(
                    record.OriginalFaction.NameColored),
                    MessageTypeDefOf.NeutralEvent,
                    false);
                return false;
            }

            if (!record.OriginalFaction.HostileTo(Faction.OfPlayer))
            {
                Messages.Message("PD_DebugRaidFactionNotHostile".Translate(
                    record.OriginalFaction.NameColored),
                    MessageTypeDefOf.NeutralEvent,
                    false);
                return false;
            }

            return worker.TryExecute(parms);
        }
    }
}
