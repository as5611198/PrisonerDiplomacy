using PrisonerDiplomacy;

namespace YourAuthor.YourAddon
{
    public sealed class YourPersonaProvider : IPrisonerDiplomacyPersonaProvider
    {
        public string ProviderId => "yourauthor.youraddon.persona.dragon";
        public string ApiVersion => PrisonerDiplomacyApi.ApiVersion;

        public bool AppliesTo(PrisonerDiplomacyRaceContext context)
        {
            return context?.RaceDefName == "YourDragonRace";
        }

        public string GetPersona(PrisonerDiplomacyRaceContext context)
        {
            // Narrative only. It cannot alter terms, chances, deadlines, or
            // transaction state. Keep it short and free of prompt instructions.
            return "proud, possessive, formal, status-conscious, protective of clan honor, and reluctant to reveal urgency";
        }
    }
}
