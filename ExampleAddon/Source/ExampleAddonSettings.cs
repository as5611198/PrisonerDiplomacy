using Verse;

namespace PrisonerDiplomacyExampleAddon
{
    public sealed class ExampleAddonSettings : ModSettings
    {
        public bool ShowHeaderWidget = true;
        public bool EnablePersonaExamples = true;
        public bool VerboseApiLogging;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref ShowHeaderWidget, "showHeaderWidget", true);
            Scribe_Values.Look(ref EnablePersonaExamples, "enablePersonaExamples", true);
            Scribe_Values.Look(ref VerboseApiLogging, "verboseApiLogging", false);
        }
    }
}
