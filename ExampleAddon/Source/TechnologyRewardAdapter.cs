using System;
using System.Collections.Generic;
using PrisonerDiplomacy;
using RimWorld;

namespace PrisonerDiplomacyExampleAddon
{
    /// <summary>
    /// One context adapter can filter on race, PawnKind, faction Def, or faction
    /// technology. Results must be deterministic, bounded, and side-effect free.
    /// </summary>
    public sealed class TechnologyRewardAdapter : IPrisonerDiplomacyRaceAdapter
    {
        public const string StableAdapterId =
            "g1061.prisonerdiplomacy.exampleaddon.technology-rewards";
        public const string SealRewardId =
            "g1061.prisonerdiplomacy.exampleaddon.diplomatic-seal";
        public const string LedgerRewardId =
            "g1061.prisonerdiplomacy.exampleaddon.encrypted-ledger";

        public string AdapterId => StableAdapterId;
        public string ApiVersion => PrisonerDiplomacyApi.ApiVersion;

        public bool AppliesTo(PrisonerDiplomacyRaceContext context)
        {
            return context?.Prisoner != null && context.Faction != null;
        }

        public int GetDiplomaticValueAdjustment(PrisonerDiplomacyRaceContext context)
        {
            if (context == null)
            {
                return 0;
            }

            int adjustment = string.Equals(
                context.RaceDefName,
                "Human",
                StringComparison.Ordinal) ? 10 : 0;
            if (string.Equals(context.FactionDefName, "Empire", StringComparison.Ordinal))
            {
                adjustment += 25;
            }
            return adjustment;
        }

        public IEnumerable<PrisonerDiplomacySpecialRewardDefinition> GetSpecialRewards(
            PrisonerDiplomacyRaceContext context)
        {
            // A null context is used by the public registry to build its catalog.
            if (context == null)
            {
                yield return CreateSealReward();
                yield return CreateLedgerReward();
                yield break;
            }

            if (context.FactionTechLevel <= TechLevel.Medieval)
            {
                yield return CreateSealReward();
            }
            else
            {
                yield return CreateLedgerReward();
            }
        }

        private static PrisonerDiplomacySpecialRewardDefinition CreateSealReward()
        {
            return new PrisonerDiplomacySpecialRewardDefinition(
                SealRewardId,
                "PDX_RewardSealLabel",
                "PDX_RewardSealDescription",
                "PDX_DiplomaticSeal",
                2);
        }

        private static PrisonerDiplomacySpecialRewardDefinition CreateLedgerReward()
        {
            return new PrisonerDiplomacySpecialRewardDefinition(
                LedgerRewardId,
                "PDX_RewardLedgerLabel",
                "PDX_RewardLedgerDescription",
                "PDX_EncryptedDiplomaticLedger",
                1);
        }
    }
}
