using System;
using RimWorld;
using Verse;

namespace PrisonerDiplomacy
{
    public static class FactionPrisonerMemoryUtility
    {
        public const float MinimumMemory = -100f;
        public const float MaximumMemory = 100f;
        public const int MemoryYearTicks = 3600000;

        public static float CalculateMultiplier(Faction faction, FactionNegotiationMemory memory)
        {
            if (memory == null)
            {
                return 1f;
            }

            float resentment = Math.Max(0f, memory.Resentment);
            if (FactionNegotiationUtility.IsTransactional(faction))
            {
                return Clamp(1f + 0.0025f * memory.Reliability - 0.0005f * resentment, 0.70f, 1.25f);
            }

            return Clamp(1f
                + 0.0020f * memory.Reliability
                + 0.0010f * memory.Treatment
                - 0.0010f * resentment,
                0.65f,
                1.25f);
        }

        public static void ApplyDecay(FactionNegotiationMemory memory, int now)
        {
            if (memory == null)
            {
                return;
            }

            if (memory.MemoryUpdatedTick < 0)
            {
                memory.MemoryUpdatedTick = now;
                return;
            }

            int elapsed = Math.Max(0, now - memory.MemoryUpdatedTick);
            if (elapsed <= 0)
            {
                return;
            }

            float years = elapsed / (float)MemoryYearTicks;
            memory.Reliability = MoveTowardsZero(memory.Reliability, 2f * years);
            memory.Treatment = MoveTowardsZero(memory.Treatment, 8f * years);
            memory.ResentmentFloor = Math.Max(0f, memory.ResentmentFloor - 2f * years);
            float resentmentDecay = FactionNegotiationUtility.IsTransactional(memory.Faction) ? 6f : 10f;
            memory.Resentment = MoveTowardsZero(memory.Resentment, resentmentDecay * years);
            memory.Resentment = Math.Max(memory.ResentmentFloor, memory.Resentment);

            ClampMemory(memory);
            memory.MemoryUpdatedTick = now;
        }

        public static void ClampMemory(FactionNegotiationMemory memory)
        {
            memory.Reliability = Clamp(memory.Reliability, MinimumMemory, MaximumMemory);
            memory.Treatment = Clamp(memory.Treatment, MinimumMemory, MaximumMemory);
            memory.Resentment = Clamp(memory.Resentment, MinimumMemory, MaximumMemory);
            memory.ResentmentFloor = Clamp(memory.ResentmentFloor, 0f, MaximumMemory);
        }

        public static string Describe(FactionNegotiationMemory memory)
        {
            if (memory == null)
            {
                return "PD_MemoryNeutral".Translate();
            }

            string reliability = memory.Reliability >= 35f
                ? "PD_ReliabilityTrusted".Translate()
                : memory.Reliability <= -35f
                    ? "PD_ReliabilityDistrusted".Translate()
                    : memory.Reliability <= -12f
                        ? "PD_ReliabilityDoubtful".Translate()
                        : "PD_ReliabilityNeutral".Translate();
            string treatment = memory.Treatment >= 30f
                ? "PD_TreatmentHonorable".Translate()
                : memory.Treatment <= -30f
                    ? "PD_TreatmentCruel".Translate()
                    : memory.Treatment <= -10f
                        ? "PD_TreatmentConcerned".Translate()
                        : "PD_TreatmentNeutral".Translate();
            string resentment = memory.Resentment >= 50f
                ? "PD_ResentmentSevere".Translate()
                : memory.Resentment >= 20f
                    ? "PD_ResentmentLasting".Translate()
                    : "PD_ResentmentLow".Translate();
            return "PD_MemorySummary".Translate(reliability, treatment, resentment);
        }

        private static float MoveTowardsZero(float value, float amount)
        {
            if (value > 0f)
            {
                return Math.Max(0f, value - amount);
            }

            return Math.Min(0f, value + amount);
        }

        private static float Clamp(float value, float min, float max)
        {
            return Math.Max(min, Math.Min(max, value));
        }
    }
}
