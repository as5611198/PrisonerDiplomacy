using System;
using RimWorld;
using Verse;

namespace PrisonerDiplomacy
{
    public static class FactionNegotiationUtility
    {
        public static FactionNegotiationType GetType(Faction faction)
        {
            if (faction?.def == null || faction == Faction.OfPlayer || faction.defeated || faction.Hidden)
            {
                return FactionNegotiationType.NonNegotiating;
            }

            PrisonerDiplomacyFactionCompatibilityExtension compatibility =
                faction.def.GetModExtension<PrisonerDiplomacyFactionCompatibilityExtension>();
            if (compatibility != null
                && compatibility.NegotiationOverride != FactionNegotiationOverride.Automatic)
            {
                switch (compatibility.NegotiationOverride)
                {
                    case FactionNegotiationOverride.NonNegotiating: return FactionNegotiationType.NonNegotiating;
                    case FactionNegotiationOverride.Transactional: return FactionNegotiationType.Transactional;
                    default: return FactionNegotiationType.Diplomatic;
                }
            }

            FactionNegotiationOverride configured = PrisonerDiplomacyMod.Settings?.GetOverride(faction.def.defName)
                ?? FactionNegotiationOverride.Automatic;
            if (configured != FactionNegotiationOverride.Automatic)
            {
                switch (configured)
                {
                    case FactionNegotiationOverride.NonNegotiating: return FactionNegotiationType.NonNegotiating;
                    case FactionNegotiationOverride.Transactional: return FactionNegotiationType.Transactional;
                    default: return FactionNegotiationType.Diplomatic;
                }
            }

            if (!faction.def.humanlikeFaction)
            {
                return FactionNegotiationType.NonNegotiating;
            }

            if (faction.def.permanentEnemy)
            {
                return PrisonerDiplomacyMod.Settings?.AllowPermanentEnemyNegotiation == false
                    ? FactionNegotiationType.NonNegotiating
                    : FactionNegotiationType.Transactional;
            }

            return FactionNegotiationType.Diplomatic;
        }

        public static bool CanNegotiate(Faction faction)
        {
            return GetType(faction) != FactionNegotiationType.NonNegotiating;
        }

        public static bool IsTransactional(Faction faction)
        {
            return GetType(faction) == FactionNegotiationType.Transactional;
        }

        public static string TypeLabel(Faction faction)
        {
            switch (GetType(faction))
            {
                case FactionNegotiationType.Transactional: return "PD_FactionTypeTransactional".Translate();
                case FactionNegotiationType.Diplomatic: return "PD_FactionTypeDiplomatic".Translate();
                default: return "PD_FactionTypeNonNegotiating".Translate();
            }
        }

        public static float PirateIdentityWillingness(PrisonerImportance importance)
        {
            float identityMultiplier;
            switch (importance)
            {
                case PrisonerImportance.Specialist: identityMultiplier = 1.15f; break;
                case PrisonerImportance.Notable: identityMultiplier = 1.30f; break;
                case PrisonerImportance.Core: identityMultiplier = 1.75f; break;
                case PrisonerImportance.Leader: identityMultiplier = 2.80f; break;
                default: identityMultiplier = 1f; break;
            }

            return Math.Max(0.55f, Math.Min(1.05f, 0.55f + 0.22f * (identityMultiplier - 1f)));
        }

        public static PirateDealRisk DetermineRisk(
            Faction faction,
            string dealId,
            int socialSkill,
            float reliability)
        {
            if (!IsTransactional(faction)
                || PrisonerDiplomacyMod.Settings?.EnablePirateRisks == false
                || GenCommandLine.CommandLineArgPassed("pdsmoketest"))
            {
                return PirateDealRisk.None;
            }

            float chance = 0.32f - socialSkill * 0.008f - Math.Max(0f, reliability) * 0.002f;
            chance = Math.Max(0.08f, Math.Min(0.32f, chance));
            int seed = Gen.HashCombineInt(GenText.StableStringHash(dealId ?? string.Empty), 6013);
            if (!Rand.ChanceSeeded(chance, seed))
            {
                return PirateDealRisk.None;
            }

            switch (Math.Abs(Gen.HashCombineInt(seed, 6073)) % 4)
            {
                case 1: return PirateDealRisk.RescueRaid;
                case 2: return PirateDealRisk.JailbreakIncitement;
                case 3: return PirateDealRisk.Ambush;
                default: return PirateDealRisk.DelayedPayment;
            }
        }

        public static int CalculatePaymentDelayTicks(string dealId)
        {
            Rand.PushState(Gen.HashCombineInt(GenText.StableStringHash(dealId ?? string.Empty), 6029));
            int delay = Rand.RangeInclusive(30000, 90000);
            Rand.PopState();
            return delay;
        }

        public static int CalculateRiskEventDelayTicks(string dealId, PirateDealRisk risk)
        {
            int seed = Gen.HashCombineInt(GenText.StableStringHash(dealId ?? string.Empty), 6113 + (int)risk * 17);
            Rand.PushState(seed);
            int delay = Rand.RangeInclusive(30000, 90000);
            Rand.PopState();
            return delay;
        }

        public static string RiskDescription(PirateDealRisk risk)
        {
            switch (risk)
            {
                case PirateDealRisk.DelayedPayment: return "PD_PirateRiskDelayed".Translate();
                case PirateDealRisk.RescueRaid: return "PD_PirateRiskRescue".Translate();
                case PirateDealRisk.JailbreakIncitement: return "PD_PirateRiskJailbreak".Translate();
                case PirateDealRisk.Ambush: return "PD_PirateRiskAmbush".Translate();
                default: return "PD_PirateRiskNone".Translate();
            }
        }
    }
}
