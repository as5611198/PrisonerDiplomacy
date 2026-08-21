using System;
using System.Collections.Generic;
using PrisonerDiplomacy;
using Verse;

namespace YourAuthor.YourAddon
{
    public sealed class YourAddonMod : Mod
    {
        private static readonly Version MinimumApi = new Version(1, 2, 0);

        public YourAddonMod(ModContentPack content) : base(content)
        {
            if (!Version.TryParse(PrisonerDiplomacyBackendApi.ApiVersion, out Version current)
                || current.Major != MinimumApi.Major
                || current < MinimumApi)
            {
                Log.Error("[Your Add-on] Requires Prisoner Diplomacy API 1.2.x; installed API is "
                    + PrisonerDiplomacyBackendApi.ApiVersion + ".");
                return;
            }

            bool registered = PrisonerDiplomacyExtensionRegistry.Register(
                new YourDiplomacyExtension());
            Log.Message("[Your Add-on] Prisoner Diplomacy extension registered="
                + registered + ".");
        }
    }

    public sealed class YourDiplomacyExtension : IPrisonerDiplomacyExtension
    {
        public string ExtensionId => "yourauthor.youraddon.prisoner-diplomacy";
        public string ApiVersion => PrisonerDiplomacyApi.ApiVersion;

        public IEnumerable<PrisonerDiplomacyEventDefinition> GetEventDefinitions()
        {
            // API 1.2 definitions are discoverable metadata only. Registration
            // does not schedule or execute a core event.
            yield return new PrisonerDiplomacyEventDefinition(
                "yourauthor.youraddon.event.exchange",
                "YourAddon_ExchangeLabel",
                "YourAddon_ExchangeDescription",
                PrisonerDiplomacyEventKind.NeutralTradeCaravan);
        }

        public IEnumerable<IPrisonerDiplomacyRaceAdapter> GetRaceAdapters()
        {
            // Remove this yield when the add-on does not need an adapter.
            yield return new YourRaceFactionAdapter();
        }
    }
}
