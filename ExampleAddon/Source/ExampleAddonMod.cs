using System;
using UnityEngine;
using Verse;

namespace PrisonerDiplomacyExampleAddon
{
    public sealed class ExampleAddonMod : Mod
    {
        internal const string RequiredApiVersion = "1.2.0";

        internal static ExampleAddonSettings Settings { get; private set; }
        internal static bool ExtensionRegistered { get; private set; }
        internal static bool PersonaRegistered { get; private set; }
        internal static bool UiExtensionRegistered { get; private set; }
        internal static string CompatibilityFailure { get; private set; }

        public ExampleAddonMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<ExampleAddonSettings>();
            if (!IsCompatibleApi(out string failure))
            {
                CompatibilityFailure = failure;
                Log.Error("[Prisoner Diplomacy Example Add-on] " + failure);
                return;
            }

            ExtensionRegistered = PrisonerDiplomacy.PrisonerDiplomacyExtensionRegistry.Register(
                new ExampleDiplomacyExtension());
            PersonaRegistered = PrisonerDiplomacy.PrisonerDiplomacyExtensionRegistry.RegisterPersonaProvider(
                new ExamplePersonaProvider());
            UiExtensionRegistered = PrisonerDiplomacy.PrisonerDiplomacyUiExtensionRegistry.Register(
                new ExampleHeaderUiExtension());

            Log.Message("[Prisoner Diplomacy Example Add-on] 1.0.0 initialized against API "
                + PrisonerDiplomacy.PrisonerDiplomacyBackendApi.ApiVersion
                + ". extension=" + ExtensionRegistered
                + " persona=" + PersonaRegistered
                + " ui=" + UiExtensionRegistered + ".");
        }

        public override string SettingsCategory()
        {
            return "PDX_SettingsCategory".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.Label("PDX_SettingsIntro".Translate());
            listing.GapLine();
            listing.CheckboxLabeled(
                "PDX_ShowHeaderWidget".Translate(),
                ref Settings.ShowHeaderWidget,
                "PDX_ShowHeaderWidgetDesc".Translate());
            listing.CheckboxLabeled(
                "PDX_EnablePersonas".Translate(),
                ref Settings.EnablePersonaExamples,
                "PDX_EnablePersonasDesc".Translate());
            listing.CheckboxLabeled(
                "PDX_VerboseLogging".Translate(),
                ref Settings.VerboseApiLogging,
                "PDX_VerboseLoggingDesc".Translate());
            listing.Gap(12f);
            Rect buttonRect = listing.GetRect(34f);
            if (ExampleAddonUi.DrawButton(buttonRect, "PDX_OpenInspector".Translate(), true))
            {
                OpenInspector();
            }
            listing.End();
        }

        internal static void OpenInspector()
        {
            if (Settings?.VerboseApiLogging == true)
            {
                Log.Message("[Prisoner Diplomacy Example Add-on]\n"
                    + ExampleAddonApiReport.BuildFull(Find.CurrentMap));
            }
            Find.WindowStack.Add(new Window_ExampleApiInspector());
        }

        private static bool IsCompatibleApi(out string failure)
        {
            failure = null;
            if (!Version.TryParse(RequiredApiVersion, out Version required)
                || !Version.TryParse(
                    PrisonerDiplomacy.PrisonerDiplomacyBackendApi.ApiVersion,
                    out Version current))
            {
                failure = "Could not parse the required or installed Prisoner Diplomacy API version.";
                return false;
            }

            if (current.Major != required.Major || current < required)
            {
                failure = "Requires Prisoner Diplomacy API " + RequiredApiVersion
                    + " with the same major version; installed API is " + current + ".";
                return false;
            }
            return true;
        }
    }
}
