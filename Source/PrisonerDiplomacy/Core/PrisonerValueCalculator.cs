using System;
using RimWorld;
using Verse;

namespace PrisonerDiplomacy
{
    public static class PrisonerValueCalculator
    {
        public static PrisonerImportance Classify(Pawn pawn, Faction originalFaction)
        {
            if (originalFaction != null && originalFaction.leader == pawn)
            {
                return PrisonerImportance.Leader;
            }

            if (pawn.royalty != null && pawn.royalty.AllTitlesForReading.Count > 0)
            {
                int maxSeniority = 0;
                foreach (RoyalTitle title in pawn.royalty.AllTitlesForReading)
                {
                    maxSeniority = Math.Max(maxSeniority, title.def.seniority);
                }

                if (maxSeniority >= 600)
                {
                    return PrisonerImportance.Core;
                }

                return PrisonerImportance.Notable;
            }

            if (IsKeySpecialist(pawn))
            {
                return PrisonerImportance.Specialist;
            }

            return PrisonerImportance.Regular;
        }

        public static int Calculate(Pawn pawn, float capturedMarketValue, PrisonerImportance importance)
        {
            float currentMarketValue = Math.Max(0f, pawn.MarketValue);
            float cappedCapturedValue = Math.Max(400f, capturedMarketValue * 1.15f);
            float marketValue = Math.Min(currentMarketValue, cappedCapturedValue);
            float basis = 350f + 18f * (float)Math.Sqrt(Math.Max(400f, Math.Min(10000f, marketValue)));
            float identityMultiplier = GetIdentityMultiplier(importance);
            float healthMultiplier = GetHealthMultiplier(pawn);
            float value = Math.Max(250f, Math.Min(12000f, basis * identityMultiplier * healthMultiplier));
            return Math.Max(250, (int)(Math.Round(value / 50f) * 50f));
        }

        public static int CalculateOffer(PrisonerRecord record, float memoryMultiplier = 1f)
        {
            float willingness = FactionNegotiationUtility.IsTransactional(record.OriginalFaction)
                ? FactionNegotiationUtility.PirateIdentityWillingness(record.Importance)
                : 0.84f;
            int seed = Gen.HashCombineInt(GenText.StableStringHash(record.PawnLoadId), record.CapturedTick);
            Rand.PushState(seed);
            float variation = Rand.Range(0.94f, 1.06f);
            Rand.PopState();
            int result = (int)(record.DiplomaticValue * willingness * variation
                * Math.Max(0.65f, Math.Min(1.25f, memoryMultiplier)));
            return Math.Max(
                PrisonerNegotiationUtility.MinimumDemand,
                PrisonerDiplomacyTuning.ScaleRansomValue((int)(Math.Round(result / 50f) * 50f)));
        }

        public static string ImportanceLabel(PrisonerImportance importance)
        {
            switch (importance)
            {
                case PrisonerImportance.Specialist: return "PD_ClassSpecialist".Translate();
                case PrisonerImportance.Notable: return "PD_ClassNotable".Translate();
                case PrisonerImportance.Core: return "PD_ClassCore".Translate();
                case PrisonerImportance.Leader: return "PD_ClassLeader".Translate();
                default: return "PD_ClassRegular".Translate();
            }
        }

        private static bool IsKeySpecialist(Pawn pawn)
        {
            if (pawn.skills == null)
            {
                return false;
            }

            int skillsAtTwelve = 0;
            foreach (SkillRecord skill in pawn.skills.skills)
            {
                if (skill.TotallyDisabled)
                {
                    continue;
                }

                if (skill.Level >= 16)
                {
                    return true;
                }

                if (skill.Level >= 12)
                {
                    skillsAtTwelve++;
                }
            }

            return skillsAtTwelve >= 2;
        }

        private static float GetIdentityMultiplier(PrisonerImportance importance)
        {
            switch (importance)
            {
                case PrisonerImportance.Specialist: return 1.15f;
                case PrisonerImportance.Notable: return 1.30f;
                case PrisonerImportance.Core: return 1.75f;
                case PrisonerImportance.Leader: return 2.80f;
                default: return 1f;
            }
        }

        private static float GetHealthMultiplier(Pawn pawn)
        {
            float healthPercent = pawn.health?.summaryHealth?.SummaryHealthPercent ?? 1f;
            return Math.Max(0.60f, Math.Min(1f, 0.60f + 0.40f * healthPercent));
        }
    }
}
