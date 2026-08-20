using System.Collections.Generic;

namespace PrisonerDiplomacy
{
    // This is intentionally tiny. Its reward adapter stays inert until the
    // developer-only toggle is enabled, so it can also exercise fulfillment.
    public sealed class PrisonerDiplomacySampleAddon : IPrisonerDiplomacyExtension
    {
        public string ExtensionId => "g1061.prisonerdiplomacy.sample";
        public string ApiVersion => PrisonerDiplomacyApi.ApiVersion;

        public IEnumerable<PrisonerDiplomacyEventDefinition> GetEventDefinitions()
        {
            yield return new PrisonerDiplomacyEventDefinition(
                "sample.neutral_trade",
                "PD_SampleAddonEventLabel",
                "PD_SampleAddonEventDescription",
                PrisonerDiplomacyEventKind.NeutralTradeCaravan);
        }

        public IEnumerable<IPrisonerDiplomacyRaceAdapter> GetRaceAdapters()
        {
            yield return new SampleRaceAdapter();
        }

        private sealed class SampleRaceAdapter : IPrisonerDiplomacyRaceAdapter
        {
            public string AdapterId => "g1061.prisonerdiplomacy.sample.race";
            public string ApiVersion => PrisonerDiplomacyApi.ApiVersion;

            public bool AppliesTo(PrisonerDiplomacyRaceContext context)
            {
                return PrisonerDiplomacyExtensionCatalog.DebugSpecialRewardsEnabled
                    && context?.Prisoner != null;
            }

            public int GetDiplomaticValueAdjustment(PrisonerDiplomacyRaceContext context)
            {
                return 15;
            }

            public IEnumerable<PrisonerDiplomacySpecialRewardDefinition> GetSpecialRewards(
                PrisonerDiplomacyRaceContext context)
            {
                yield return new PrisonerDiplomacySpecialRewardDefinition(
                    "sample.energy_core",
                    "PD_SampleAddonRewardLabel",
                    "PD_SampleAddonRewardDescription",
                    "ComponentIndustrial",
                    3);
            }
        }
    }

    internal static class PrisonerDiplomacyExtensionCatalog
    {
        internal static bool DebugSpecialRewardsEnabled { get; set; }

        public static void RegisterBuiltIns()
        {
            PrisonerDiplomacyExtensionRegistry.Register(new PrisonerDiplomacyCoreExtension());
            PrisonerDiplomacyExtensionRegistry.Register(new PrisonerDiplomacySampleAddon());
        }
    }

    internal sealed class PrisonerDiplomacyCoreExtension : IPrisonerDiplomacyExtension
    {
        public string ExtensionId => "g1061.prisonerdiplomacy.core";
        public string ApiVersion => PrisonerDiplomacyApi.ApiVersion;

        public IEnumerable<PrisonerDiplomacyEventDefinition> GetEventDefinitions()
        {
            yield return new PrisonerDiplomacyEventDefinition(
                "core.neutral_trade",
                "PD_EventNeutralTradeLabel",
                "PD_EventNeutralTradeText",
                PrisonerDiplomacyEventKind.NeutralTradeCaravan);
            yield return new PrisonerDiplomacyEventDefinition(
                "core.false_surrender",
                "PD_EventInfiltrationLabel",
                "PD_EventInfiltrationText",
                PrisonerDiplomacyEventKind.FalseSurrenderInfiltration);
            yield return new PrisonerDiplomacyEventDefinition(
                "core.public_trial",
                "PD_EventTrialLabel",
                "PD_EventTrialText",
                PrisonerDiplomacyEventKind.PublicWarCrimeTrial);
            yield return new PrisonerDiplomacyEventDefinition(
                "core.ransom_ambush",
                "PD_EventAmbushLabel",
                "PD_EventAmbushText",
                PrisonerDiplomacyEventKind.RansomAmbushRetaliation);
        }

        public IEnumerable<IPrisonerDiplomacyRaceAdapter> GetRaceAdapters()
        {
            yield break;
        }
    }
}
