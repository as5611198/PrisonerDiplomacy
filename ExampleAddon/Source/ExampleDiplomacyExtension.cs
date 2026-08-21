using System.Collections.Generic;
using PrisonerDiplomacy;

namespace PrisonerDiplomacyExampleAddon
{
    /// <summary>
    /// Registers stable metadata once. The core stores and executes every deal;
    /// this object never mutates a Pawn, Thing, faction, event, or save record.
    /// </summary>
    public sealed class ExampleDiplomacyExtension : IPrisonerDiplomacyExtension
    {
        public const string StableExtensionId = "g1061.prisonerdiplomacy.exampleaddon";

        public string ExtensionId => StableExtensionId;
        public string ApiVersion => PrisonerDiplomacyApi.ApiVersion;

        public IEnumerable<PrisonerDiplomacyEventDefinition> GetEventDefinitions()
        {
            // API 1.2 event definitions are discoverable catalog metadata. The
            // public API intentionally does not expose the core event scheduler.
            yield return new PrisonerDiplomacyEventDefinition(
                "example.neutral_exchange",
                "PDX_EventNeutralLabel",
                "PDX_EventNeutralDescription",
                PrisonerDiplomacyEventKind.NeutralTradeCaravan);
            yield return new PrisonerDiplomacyEventDefinition(
                "example.false_surrender",
                "PDX_EventInfiltrationLabel",
                "PDX_EventInfiltrationDescription",
                PrisonerDiplomacyEventKind.FalseSurrenderInfiltration);
            yield return new PrisonerDiplomacyEventDefinition(
                "example.public_trial",
                "PDX_EventTrialLabel",
                "PDX_EventTrialDescription",
                PrisonerDiplomacyEventKind.PublicWarCrimeTrial);
            yield return new PrisonerDiplomacyEventDefinition(
                "example.ransom_ambush",
                "PDX_EventAmbushLabel",
                "PDX_EventAmbushDescription",
                PrisonerDiplomacyEventKind.RansomAmbushRetaliation);
        }

        public IEnumerable<IPrisonerDiplomacyRaceAdapter> GetRaceAdapters()
        {
            yield return new TechnologyRewardAdapter();
        }
    }
}
