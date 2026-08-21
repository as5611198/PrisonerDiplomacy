using System;
using PrisonerDiplomacy;

namespace PrisonerDiplomacyExampleAddon
{
    /// <summary>
    /// Persona text is untrusted narrative context. It cannot alter rewards,
    /// deadlines, Pawn state, or event outcomes.
    /// </summary>
    public sealed class ExamplePersonaProvider : IPrisonerDiplomacyPersonaProvider
    {
        public const string StableProviderId =
            "g1061.prisonerdiplomacy.exampleaddon.vanilla-personas";

        public string ProviderId => StableProviderId;
        public string ApiVersion => PrisonerDiplomacyApi.ApiVersion;

        public bool AppliesTo(PrisonerDiplomacyRaceContext context)
        {
            if (ExampleAddonMod.Settings?.EnablePersonaExamples == false)
            {
                return false;
            }

            string factionDefName = context?.FactionDefName ?? string.Empty;
            return factionDefName == "Empire"
                || factionDefName.StartsWith("Pirate", StringComparison.Ordinal)
                || factionDefName.StartsWith("Tribe", StringComparison.Ordinal);
        }

        public string GetPersona(PrisonerDiplomacyRaceContext context)
        {
            string factionDefName = context?.FactionDefName ?? string.Empty;
            if (factionDefName == "Empire")
            {
                return "aristocratic, status-conscious, ceremonially formal, protective of noble bloodlines, and unwilling to admit desperation";
            }
            if (factionDefName.StartsWith("Pirate", StringComparison.Ordinal))
            {
                return "blunt, predatory, suspicious, transactional, fond of threats and underworld slang, but attentive to concrete leverage";
            }
            if (factionDefName.StartsWith("Tribe", StringComparison.Ordinal))
            {
                return "guided by ancestors, kinship, omens, oral promises, and the natural world; wary of unfamiliar technology";
            }
            return string.Empty;
        }
    }
}
