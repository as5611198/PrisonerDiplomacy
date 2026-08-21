using System.Collections.Generic;
using PrisonerDiplomacy;

namespace YourAuthor.YourAddon
{
    public sealed class YourRaceFactionAdapter : IPrisonerDiplomacyRaceAdapter
    {
        public string AdapterId => "yourauthor.youraddon.adapter.dragon";
        public string ApiVersion => PrisonerDiplomacyApi.ApiVersion;

        public bool AppliesTo(PrisonerDiplomacyRaceContext context)
        {
            return context?.Prisoner != null
                && context.Faction != null
                && context.RaceDefName == "YourDragonRace";
        }

        public int GetDiplomaticValueAdjustment(PrisonerDiplomacyRaceContext context)
        {
            // Keep this deterministic and bounded. The core clamps each adapter
            // result to -1000..1000 before summing applicable adapters.
            return context?.FactionDefName == "YourDragonEmpire" ? 80 : 30;
        }

        public IEnumerable<PrisonerDiplomacySpecialRewardDefinition> GetSpecialRewards(
            PrisonerDiplomacyRaceContext context)
        {
            // The registry passes null once to build a catalog. Return every
            // reward this adapter can expose in that case.
            if (context == null || AppliesTo(context))
            {
                yield return new PrisonerDiplomacySpecialRewardDefinition(
                    "yourauthor.youraddon.reward.ember-core",
                    "YourAddon_EmberCoreLabel",
                    "YourAddon_EmberCoreDescription",
                    "YourAddon_EmberCore",
                    1);
            }
        }
    }
}
