using System;
using RimWorld;

namespace PrisonerDiplomacy
{
    public enum PrisonerDiplomacyMessageDetail
    {
        Essential,
        Standard,
        Detailed
    }

    internal static class PrisonerDiplomacyTuning
    {
        public static bool EnemyInitiatedRansomsEnabled =>
            PrisonerDiplomacyMod.Settings?.EnableEnemyInitiatedRansoms != false;

        public static float OfferFrequencyMultiplier => Clamp(
            PrisonerDiplomacyMod.Settings?.OfferFrequencyMultiplier ?? 1f,
            0.25f,
            4f,
            1f);

        public static float RansomValueMultiplier => Clamp(
            PrisonerDiplomacyMod.Settings?.RansomValueMultiplier ?? 1f,
            0.50f,
            2f,
            1f);

        public static bool FactionReservesEnabled =>
            PrisonerDiplomacyMod.Settings?.EnableFactionReserves != false;

        public static bool FactionMemoryEnabled =>
            PrisonerDiplomacyMod.Settings?.EnableFactionMemory != false;

        public static PrisonerDiplomacyMessageDetail MessageDetail =>
            PrisonerDiplomacyMod.Settings?.MessageDetail ?? PrisonerDiplomacyMessageDetail.Standard;

        public static int ScaleRansomValue(int value)
        {
            if (value <= 0)
            {
                return 0;
            }

            int scaled = (int)Math.Round(value * RansomValueMultiplier / 50f) * 50;
            return Math.Max(PrisonerNegotiationUtility.MinimumDemand, scaled);
        }

        public static float EffectiveReliability(FactionNegotiationMemory memory)
        {
            return FactionMemoryEnabled ? memory?.Reliability ?? 0f : 0f;
        }

        public static bool ShouldNotifyMemory(float magnitude)
        {
            switch (MessageDetail)
            {
                case PrisonerDiplomacyMessageDetail.Essential:
                    return magnitude >= 20f;
                case PrisonerDiplomacyMessageDetail.Detailed:
                    return magnitude >= 5f;
                default:
                    return magnitude >= 12f;
            }
        }

        private static float Clamp(float value, float minimum, float maximum, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return fallback;
            }

            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }
}
