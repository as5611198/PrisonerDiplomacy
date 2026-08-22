using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using PrisonerDiplomacy.Telemetry;

namespace PrisonerDiplomacy
{
    public sealed class PrisonerDiplomacyMod : Mod
    {
        private static PrisonerDiplomacyMod instance;
        private static string loadTestSaveName;
        private Vector2 settingsScrollPosition;
        private bool showAiApiKey;
        private string aiOperationStatus;
        private bool aiOperationSucceeded;
        private bool lastEnemyInitiatedRansoms;
        private float lastOfferFrequencyMultiplier;
        private float lastRansomValueMultiplier;

        public static PrisonerDiplomacySettings Settings { get; private set; }

        public PrisonerDiplomacyMod(ModContentPack content) : base(content)
        {
            instance = this;
            Settings = GetSettings<PrisonerDiplomacySettings>();
            lastEnemyInitiatedRansoms = Settings.EnableEnemyInitiatedRansoms;
            lastOfferFrequencyMultiplier = Settings.OfferFrequencyMultiplier;
            lastRansomValueMultiplier = Settings.RansomValueMultiplier;
            ErrorTelemetryService.Initialize();
            Harmony harmony = new Harmony("g1061.prisonerdiplomacy");
            harmony.PatchAll();
            RimChatHarmonyPatches.TryInstall(harmony);
            PrisonerDiplomacyExtensionCatalog.RegisterBuiltIns();
            Log.Message("[Prisoner Diplomacy] 1.2.1 initialized. RimChat="
                + (RimChatIntegration.IsInstalled ? RimChatIntegration.Version : "not installed")
                + " status=" + RimChatIntegration.Status
                + " bridge=" + RimChatHarmonyPatches.IsInstalled + ".");

            if (GenCommandLine.TryGetCommandLineArg("pdloadtest", out loadTestSaveName))
            {
                LongEventHandler.ExecuteWhenFinished(() =>
                {
                    Log.Message("[Prisoner Diplomacy LoadTest] Loading " + loadTestSaveName + ".");
                    GameDataSaveLoader.CheckVersionAndLoadGame(loadTestSaveName);
                });
            }
        }

        public override string SettingsCategory()
        {
            return "PD_SettingsCategory".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            DrainAiProviderResults();
            RimChatIntegration.EnsureRefreshed();
            List<FactionDef> factionDefs = DefDatabase<FactionDef>.AllDefsListForReading
                .Where(def => def != null && !def.isPlayer)
                .OrderBy(def => def.LabelCap.ToString())
                .ToList();
            float contentHeight = 1610f + factionDefs.Count * 68f;
            Rect view = new Rect(0f, 0f, inRect.width - 18f, contentHeight);
            Widgets.BeginScrollView(inRect, ref settingsScrollPosition, view);

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(new Rect(0f, 0f, view.width, 1600f));
            listing.Label("PD_SettingsGameplayHeading".Translate());
            listing.CheckboxLabeled(
                "PD_SettingEnemyInitiatedRansoms".Translate(),
                ref Settings.EnableEnemyInitiatedRansoms,
                "PD_SettingEnemyInitiatedRansomsDesc".Translate());
            listing.Label("PD_SettingOfferFrequency".Translate(Settings.OfferFrequencyMultiplier.ToString("0.00")));
            Settings.OfferFrequencyMultiplier = listing.Slider(Settings.OfferFrequencyMultiplier, 0.25f, 4f);
            listing.Label("PD_SettingRansomValue".Translate(Settings.RansomValueMultiplier.ToString("0.00")));
            Settings.RansomValueMultiplier = listing.Slider(Settings.RansomValueMultiplier, 0.50f, 2f);
            listing.CheckboxLabeled(
                "PD_SettingFactionReserves".Translate(),
                ref Settings.EnableFactionReserves,
                "PD_SettingFactionReservesDesc".Translate());
            listing.CheckboxLabeled(
                "PD_SettingFactionMemory".Translate(),
                ref Settings.EnableFactionMemory,
                "PD_SettingFactionMemoryDesc".Translate());
            listing.CheckboxLabeled(
                "PD_SettingPermanentEnemyNegotiation".Translate(),
                ref Settings.AllowPermanentEnemyNegotiation,
                "PD_SettingPermanentEnemyNegotiationDesc".Translate());
            listing.CheckboxLabeled(
                "PD_SettingPirateRisks".Translate(),
                ref Settings.EnablePirateRisks,
                "PD_SettingPirateRisksDesc".Translate());
            listing.CheckboxLabeled(
                "PD_SettingStrategicConsequences".Translate(),
                ref Settings.EnableStrategicConsequences,
                "PD_SettingStrategicConsequencesDesc".Translate());
            listing.CheckboxLabeled(
                "PD_SettingCompatibilityLogging".Translate(),
                ref Settings.EnableCompatibilityLogging,
                "PD_SettingCompatibilityLoggingDesc".Translate());
            listing.CheckboxLabeled(
                "PD_SettingPerformanceLogging".Translate(),
                ref Settings.EnablePerformanceLogging,
                "PD_SettingPerformanceLoggingDesc".Translate());
            listing.CheckboxLabeled(
                "PD_SettingErrorTelemetry".Translate(),
                ref Settings.EnableErrorTelemetryPrompts,
                "PD_SettingErrorTelemetryDesc".Translate());
            bool alwaysSendTelemetryBefore = Settings.AlwaysSendErrorTelemetry;
            bool previousGuiEnabled = GUI.enabled;
            GUI.enabled = previousGuiEnabled && Settings.EnableErrorTelemetryPrompts;
            listing.CheckboxLabeled(
                "PD_SettingErrorTelemetryAlways".Translate(),
                ref Settings.AlwaysSendErrorTelemetry,
                "PD_SettingErrorTelemetryAlwaysDesc".Translate());
            GUI.enabled = previousGuiEnabled;
            if (alwaysSendTelemetryBefore && !Settings.AlwaysSendErrorTelemetry)
            {
                ErrorTelemetryService.RevokePersistentConsent();
            }
            listing.CheckboxLabeled(
                "PD_SettingReduceUiMotion".Translate(),
                ref Settings.ReduceUiMotion,
                "PD_SettingReduceUiMotionDesc".Translate());
            Rect messageDetailRect = listing.GetRect(32f);
            Widgets.Label(new Rect(messageDetailRect.x, messageDetailRect.y, 250f, messageDetailRect.height),
                "PD_SettingMessageDetail".Translate());
            if (PrisonerDiplomacyUiTheme.DrawButton(
                new Rect(messageDetailRect.x + 258f, messageDetailRect.y, messageDetailRect.width - 258f, messageDetailRect.height),
                MessageDetailLabel(Settings.MessageDetail),
                DiplomacyUiButtonStyle.Secondary))
            {
                OpenMessageDetailMenu();
            }
            Rect diagnosticButtonRect = listing.GetRect(32f);
            if (PrisonerDiplomacyUiTheme.DrawButton(
                diagnosticButtonRect,
                "PD_CopyDiagnosticReport".Translate(),
                DiplomacyUiButtonStyle.Secondary))
            {
                CopyDiagnosticReport();
            }
            listing.Gap(8f);
            listing.Label("PD_RimChatCompatibilityHeading".Translate());
            listing.Label(BuildRimChatStatusText());
            if (RimChatIntegration.EffectiveOwner != Settings.RansomSystemOwner)
            {
                listing.Label("PD_RimChatEffectiveMode".Translate(
                    RimChatIntegration.OwnerLabelKey(RimChatIntegration.EffectiveOwner).Translate()));
            }
            Rect ownerRect = listing.GetRect(32f);
            Widgets.Label(new Rect(ownerRect.x, ownerRect.y, ownerRect.width - 238f, ownerRect.height),
                "PD_RansomOwnerSetting".Translate());
            if (PrisonerDiplomacyUiTheme.DrawButton(
                new Rect(ownerRect.xMax - 230f, ownerRect.y, 230f, ownerRect.height),
                RimChatIntegration.OwnerLabelKey(Settings.RansomSystemOwner).Translate(),
                DiplomacyUiButtonStyle.Secondary))
            {
                OpenRansomOwnerMenu();
            }
            listing.Label(OwnerDescriptionKey(Settings.RansomSystemOwner).Translate());
            if (RimChatIntegration.RequiresCompatibilityWarning)
            {
                GUI.color = Color.yellow;
                listing.Label("PD_RimChatSafeIsolationWarning".Translate());
                GUI.color = Color.white;
            }

            DrawAiSettings(listing);
            listing.Gap(8f);
            listing.Label("PD_SettingFactionOverrides".Translate());
            listing.Label("PD_SettingFactionOverridesDesc".Translate());
            float factionStart = listing.CurHeight + 8f;
            listing.End();

            bool settingsChanged = lastEnemyInitiatedRansoms != Settings.EnableEnemyInitiatedRansoms
                || Math.Abs(lastOfferFrequencyMultiplier - Settings.OfferFrequencyMultiplier) > 0.0001f
                || Math.Abs(lastRansomValueMultiplier - Settings.RansomValueMultiplier) > 0.0001f;
            if (settingsChanged)
            {
                bool reenabledOffers = !lastEnemyInitiatedRansoms && Settings.EnableEnemyInitiatedRansoms;
                PrisonerDiplomacyGameComponent.Current?.RefreshFactionOfferSchedules(reenabledOffers);
                lastEnemyInitiatedRansoms = Settings.EnableEnemyInitiatedRansoms;
                lastOfferFrequencyMultiplier = Settings.OfferFrequencyMultiplier;
                lastRansomValueMultiplier = Settings.RansomValueMultiplier;
                WriteSettings();
            }

            float y = factionStart;
            foreach (FactionDef factionDef in factionDefs)
            {
                Widgets.Label(new Rect(0f, y, view.width - 230f, 32f), factionDef.LabelCap);
                FactionNegotiationOverride configured = Settings.GetOverride(factionDef.defName);
                string label = FactionOverrideLabel(configured);
                if (PrisonerDiplomacyUiTheme.DrawButton(
                    new Rect(view.width - 220f, y, 220f, 32f),
                    label,
                    DiplomacyUiButtonStyle.Secondary))
                {
                    List<FloatMenuOption> options = new List<FloatMenuOption>();
                    foreach (FactionNegotiationOverride option in System.Enum.GetValues(typeof(FactionNegotiationOverride)))
                    {
                        FactionNegotiationOverride captured = option;
                        options.Add(new FloatMenuOption(FactionOverrideLabel(captured), () =>
                        {
                            Settings.SetOverride(factionDef.defName, captured);
                            WriteSettings();
                        }));
                    }
                    Find.WindowStack.Add(new FloatMenu(options));
                }
                GUI.color = Color.grey;
                Widgets.Label(new Rect(0f, y + 34f, 150f, 28f), "PD_AiFactionPersonaOverride".Translate());
                GUI.color = Color.white;
                string persona = Settings.GetFactionPersonaOverride(factionDef.defName);
                string editedPersona = Widgets.TextField(
                    new Rect(158f, y + 34f, Math.Max(120f, view.width - 158f), 28f),
                    persona ?? string.Empty);
                editedPersona = PrisonerDiplomacySettings.NormalizePersona(editedPersona);
                if (!string.Equals(persona, editedPersona, StringComparison.Ordinal))
                {
                    Settings.SetFactionPersonaOverride(factionDef.defName, editedPersona);
                    WriteSettings();
                }
                y += 68f;
            }
            Widgets.EndScrollView();
        }

        private void DrawAiSettings(Listing_Standard listing)
        {
            listing.Gap(12f);
            listing.Label("PD_AiSettingsHeading".Translate());
            bool aiWasEnabled = Settings.EnableAiNarratives;
            listing.CheckboxLabeled(
                "PD_SettingEnableAiNarratives".Translate(),
                ref Settings.EnableAiNarratives,
                "PD_SettingEnableAiNarrativesDesc".Translate());
            listing.CheckboxLabeled(
                "PD_SettingAiAllowExternalContext".Translate(),
                ref Settings.AiAllowExternalContext,
                "PD_SettingAiAllowExternalContextDesc".Translate());
            listing.CheckboxLabeled(
                "PD_SettingEnableAiNegotiationAdjustments".Translate(),
                ref Settings.EnableAiNegotiationAdjustments,
                "PD_SettingEnableAiNegotiationAdjustmentsDesc".Translate());
            listing.Label("PD_AiPersonaHeading".Translate());
            listing.Label("PD_AiPersonaDescription".Translate());
            DrawTextSetting(
                listing,
                "PD_AiDefaultFactionPersona".Translate(),
                ref Settings.AiDefaultFactionPersona,
                false);
            if (aiWasEnabled && !Settings.EnableAiNarratives)
            {
                PrisonerDiplomacyGameComponent.Current?.DisableAiNarratives();
                AiProviderSettingsService.CancelOperations();
            }

            Rect providerRow = listing.GetRect(32f);
            float labelWidth = Math.Min(180f, providerRow.width * 0.3f);
            Widgets.Label(
                new Rect(providerRow.x, providerRow.y, labelWidth, providerRow.height),
                "PD_AiProvider".Translate());
            Rect providerButton = new Rect(
                providerRow.x + labelWidth + 8f,
                providerRow.y,
                providerRow.width - labelWidth - 8f,
                providerRow.height);
            if (PrisonerDiplomacyUiTheme.DrawButton(
                providerButton,
                AiNarrativeProviderCatalog.DisplayName(Settings.AiProvider),
                DiplomacyUiButtonStyle.Secondary))
            {
                OpenAiProviderMenu();
            }

            Rect endpointRow = listing.GetRect(32f);
            Widgets.Label(
                new Rect(endpointRow.x, endpointRow.y, labelWidth, endpointRow.height),
                "PD_AiBaseUrl".Translate());
            Rect endpointField = new Rect(
                endpointRow.x + labelWidth + 8f,
                endpointRow.y,
                endpointRow.width - labelWidth - 8f,
                endpointRow.height);
            if (Settings.AiProvider == AiNarrativeProviderKind.CustomOpenAI)
            {
                Settings.AiCustomBaseUrl = Widgets.TextField(endpointField, Settings.AiCustomBaseUrl ?? string.Empty);
            }
            else
            {
                GUI.color = Color.grey;
                Widgets.Label(endpointField, AiNarrativeProviderCatalog.ResolveBaseUrl(Settings));
                GUI.color = Color.white;
            }

            Rect keyRow = listing.GetRect(32f);
            Widgets.Label(
                new Rect(keyRow.x, keyRow.y, labelWidth, keyRow.height),
                "PD_AiApiKey".Translate());
            const float showButtonWidth = 86f;
            Rect keyField = new Rect(
                keyRow.x + labelWidth + 8f,
                keyRow.y,
                keyRow.width - labelWidth - showButtonWidth - 16f,
                keyRow.height);
            Settings.AiApiKey = showAiApiKey
                ? Widgets.TextField(keyField, Settings.AiApiKey ?? string.Empty)
                : GUI.PasswordField(keyField, Settings.AiApiKey ?? string.Empty, '*');
            if (PrisonerDiplomacyUiTheme.DrawButton(
                new Rect(keyRow.xMax - showButtonWidth, keyRow.y, showButtonWidth, keyRow.height),
                showAiApiKey ? "PD_AiHideKey".Translate() : "PD_AiShowKey".Translate(),
                DiplomacyUiButtonStyle.Secondary))
            {
                showAiApiKey = !showAiApiKey;
            }

            if (Settings.AiProvider == AiNarrativeProviderKind.CustomOpenAI)
            {
                listing.CheckboxLabeled(
                    "PD_AiEndpointRequiresKey".Translate(),
                    ref Settings.AiEndpointRequiresKey,
                    "PD_AiEndpointRequiresKeyDesc".Translate());
            }

            Rect modelRow = listing.GetRect(32f);
            Widgets.Label(
                new Rect(modelRow.x, modelRow.y, labelWidth, modelRow.height),
                "PD_AiModel".Translate());
            const float modelButtonWidth = 48f;
            Rect modelField = new Rect(
                modelRow.x + labelWidth + 8f,
                modelRow.y,
                modelRow.width - labelWidth - modelButtonWidth - 16f,
                modelRow.height);
            Settings.AiModel = Widgets.TextField(modelField, Settings.AiModel ?? string.Empty);
            if (PrisonerDiplomacyUiTheme.DrawButton(
                new Rect(modelRow.xMax - modelButtonWidth, modelRow.y, modelButtonWidth, modelRow.height),
                "v",
                DiplomacyUiButtonStyle.Secondary))
            {
                OpenAiModelMenu();
            }

            Rect actionsRow = listing.GetRect(32f);
            float actionWidth = (actionsRow.width - 8f) * 0.5f;
            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && !AiProviderSettingsService.IsFetchingModels;
            if (PrisonerDiplomacyUiTheme.DrawButton(
                new Rect(actionsRow.x, actionsRow.y, actionWidth, actionsRow.height),
                AiProviderSettingsService.IsFetchingModels
                    ? "PD_AiFetchingModels".Translate()
                    : "PD_AiFetchModels".Translate(),
                DiplomacyUiButtonStyle.Primary,
                !AiProviderSettingsService.IsFetchingModels))
            {
                AiProviderSettingsService.StartModelFetch(Settings);
            }

            GUI.enabled = previousEnabled && !AiProviderSettingsService.IsTestingConnection;
            if (PrisonerDiplomacyUiTheme.DrawButton(
                new Rect(actionsRow.x + actionWidth + 8f, actionsRow.y, actionWidth, actionsRow.height),
                AiProviderSettingsService.IsTestingConnection
                    ? "PD_AiTestingConnection".Translate()
                    : "PD_AiTestConnection".Translate(),
                DiplomacyUiButtonStyle.Primary,
                !AiProviderSettingsService.IsTestingConnection))
            {
                AiProviderSettingsService.StartConnectionTest(Settings);
            }
            GUI.enabled = previousEnabled;

            Settings.RefreshLegacyAiEndpoint();
            string resolvedEndpoint = Settings.AiEndpoint;
            Text.Font = GameFont.Tiny;
            GUI.color = Color.grey;
            listing.Label("PD_AiResolvedEndpoint".Translate(resolvedEndpoint));
            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            if (!string.IsNullOrEmpty(aiOperationStatus))
            {
                GUI.color = aiOperationSucceeded ? Color.green : Color.yellow;
                listing.Label(aiOperationStatus);
                GUI.color = Color.white;
            }

            listing.Label("PD_AiTimeout".Translate(Settings.AiTimeoutSeconds));
            Settings.AiTimeoutSeconds = Mathf.RoundToInt(listing.Slider(Settings.AiTimeoutSeconds, 3f, 60f));
            listing.CheckboxLabeled(
                "PD_AiShowTechnicalErrors".Translate(),
                ref Settings.AiShowTechnicalErrors,
                "PD_AiShowTechnicalErrorsDesc".Translate());
            GUI.color = AiNarrativeService.ConfigurationIssue(Settings) == null ? Color.green : Color.yellow;
            listing.Label(AiConfigurationStatusKey(AiNarrativeService.ConfigurationIssue(Settings)).Translate());
            GUI.color = Color.white;
            listing.Label("PD_AiPrivacyNotice".Translate());
        }

        private void OpenAiProviderMenu()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            foreach (AiNarrativeProviderKind provider in AiNarrativeProviderCatalog.AllKinds)
            {
                AiNarrativeProviderKind captured = provider;
                options.Add(new FloatMenuOption(AiNarrativeProviderCatalog.DisplayName(captured), () =>
                {
                    if (Settings.AiProvider == captured)
                    {
                        return;
                    }

                    AiProviderSettingsService.CancelOperations();
                    Settings.AiProvider = captured;
                    Settings.AiApiKey = string.Empty;
                    Settings.AiModel = AiNarrativeProviderCatalog.DefaultModel(captured);
                    Settings.AiFetchedModels.Clear();
                    Settings.RefreshLegacyAiEndpoint();
                    aiOperationStatus = null;
                    WriteSettings();
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void OpenAiModelMenu()
        {
            List<string> models = Settings.AiFetchedModels
                .Where(model => !string.IsNullOrWhiteSpace(model))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(model => model, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (models.Count == 0)
            {
                Messages.Message("PD_AiNoModels".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            Find.WindowStack.Add(new FloatMenu(models
                .Select(model => new FloatMenuOption(model, () =>
                {
                    Settings.AiModel = model;
                    Settings.RefreshLegacyAiEndpoint();
                    WriteSettings();
                }))
                .ToList()));
        }

        private void DrainAiProviderResults()
        {
            while (AiProviderSettingsService.TryApplyNextResult(Settings, out AiProviderOperationResult result))
            {
                aiOperationSucceeded = result.Success;
                if (result.Success && result.Kind == AiProviderOperationKind.ModelFetch)
                {
                    aiOperationStatus = "PD_AiFetchModelsSuccess".Translate(result.Models?.Count ?? 0);
                }
                else if (result.Success)
                {
                    aiOperationStatus = "PD_AiConnectionSuccess".Translate(
                        AiNarrativeProviderCatalog.DisplayName(Settings.AiProvider));
                    Log.Message("[Prisoner Diplomacy] AI connection test passed. provider="
                        + Settings.AiProvider + ".");
                }
                else
                {
                    aiOperationStatus = "PD_AiOperationFailed".Translate(
                        AiFailureReason(result.FailureCode));
                    if (result.Kind == AiProviderOperationKind.ConnectionTest)
                    {
                        Log.Warning("[Prisoner Diplomacy] AI connection test failed. provider="
                            + Settings.AiProvider + " code=" + SanitizedAiFailureCode(result.FailureCode) + ".");
                    }
                }

                Messages.Message(
                    aiOperationStatus,
                    result.Success ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.RejectInput,
                    false);
                WriteSettings();
            }
        }

        private static TaggedString AiFailureReason(string failureCode)
        {
            if (failureCode == "missing_api_key") return "PD_AiFailureMissingKey".Translate();
            if (failureCode == "missing_model") return "PD_AiFailureMissingModel".Translate();
            if (failureCode == "invalid_endpoint") return "PD_AiFailureInvalidEndpoint".Translate();
            if (failureCode == "insecure_endpoint") return "PD_AiFailureInsecureEndpoint".Translate();
            if (failureCode == "timeout_or_cancelled") return "PD_AiFailureTimeout".Translate();
            if (failureCode == "empty_model_list") return "PD_AiFailureEmptyModels".Translate();
            if (failureCode == "request_serialization_error") return "PD_AiFailureInternal".Translate();
            if (failureCode == "invalid_envelope_json"
                || failureCode == "invalid_model_json"
                || failureCode == "missing_content"
                || failureCode == "invalid_message"
                || failureCode == "binding_mismatch")
            {
                return "PD_AiFailureIncompatibleResponse".Translate();
            }
            if (failureCode == "invalid_response_size")
            {
                return "PD_AiFailureInvalidResponse".Translate();
            }
            if (failureCode == "http_401" || failureCode == "http_403")
            {
                return "PD_AiFailureUnauthorized".Translate();
            }
            if (failureCode == "http_429") return "PD_AiFailureRateLimit".Translate();
            if (!string.IsNullOrEmpty(failureCode)
                && failureCode.StartsWith("http_4", StringComparison.Ordinal))
            {
                return "PD_AiFailureRequestRejected".Translate();
            }
            if (!string.IsNullOrEmpty(failureCode)
                && failureCode.StartsWith("http_5", StringComparison.Ordinal))
            {
                return "PD_AiFailureServer".Translate();
            }
            return "PD_AiFailureNetwork".Translate();
        }

        private static string SanitizedAiFailureCode(string failureCode)
        {
            if (string.IsNullOrEmpty(failureCode))
            {
                return "unknown_error";
            }

            return failureCode.All(character => char.IsLetterOrDigit(character) || character == '_')
                ? failureCode
                : "unknown_error";
        }

        public static void SetAiNarrativesEnabled(bool enabled)
        {
            if (Settings == null)
            {
                return;
            }

            Settings.EnableAiNarratives = enabled;
            if (!enabled)
            {
                PrisonerDiplomacyGameComponent.Current?.DisableAiNarratives();
                AiProviderSettingsService.CancelOperations();
            }
            instance?.WriteSettings();
        }

        internal static void SaveSettingsNow()
        {
            instance?.WriteSettings();
        }

        private static void DrawTextSetting(Listing_Standard listing, string label, ref string value, bool password)
        {
            Rect rect = listing.GetRect(32f);
            float labelWidth = Math.Min(220f, rect.width * 0.36f);
            Widgets.Label(new Rect(rect.x, rect.y, labelWidth, rect.height), label);
            Rect fieldRect = new Rect(rect.x + labelWidth + 8f, rect.y, rect.width - labelWidth - 8f, rect.height);
            value = password
                ? GUI.PasswordField(fieldRect, value ?? string.Empty, '*')
                : Widgets.TextField(fieldRect, value ?? string.Empty);
        }

        private static string AiConfigurationStatusKey(string issue)
        {
            switch (issue)
            {
                case null: return "PD_AiStatusReady";
                case "disabled": return "PD_AiStatusDisabled";
                case "external_context_disabled": return "PD_AiStatusExternalContextDisabled";
                case "missing_api_key": return "PD_AiStatusMissingKey";
                case "missing_model": return "PD_AiStatusMissingModel";
                case "insecure_endpoint": return "PD_AiStatusInsecureEndpoint";
                default: return "PD_AiStatusInvalidEndpoint";
            }
        }

        private static string MessageDetailLabel(PrisonerDiplomacyMessageDetail detail)
        {
            switch (detail)
            {
                case PrisonerDiplomacyMessageDetail.Essential: return "PD_MessageDetailEssential".Translate();
                case PrisonerDiplomacyMessageDetail.Detailed: return "PD_MessageDetailDetailed".Translate();
                default: return "PD_MessageDetailStandard".Translate();
            }
        }

        private void OpenMessageDetailMenu()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            foreach (PrisonerDiplomacyMessageDetail detail in System.Enum.GetValues(typeof(PrisonerDiplomacyMessageDetail)))
            {
                PrisonerDiplomacyMessageDetail captured = detail;
                options.Add(new FloatMenuOption(MessageDetailLabel(captured), () =>
                {
                    Settings.MessageDetail = captured;
                    WriteSettings();
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static void CopyDiagnosticReport()
        {
            string report = PrisonerDiplomacyDiagnostics.BuildReport();
            GUIUtility.systemCopyBuffer = report;
            Log.Message(report);
            Messages.Message("PD_DiagnosticReportCopied".Translate(), MessageTypeDefOf.PositiveEvent, false);
        }

        private void OpenRansomOwnerMenu()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            foreach (PrisonerRansomSystemOwner owner in System.Enum.GetValues(typeof(PrisonerRansomSystemOwner)))
            {
                PrisonerRansomSystemOwner captured = owner;
                string label = RimChatIntegration.OwnerLabelKey(captured).Translate();
                if (!RimChatIntegration.IsOwnerAvailable(captured))
                {
                    options.Add(new FloatMenuOption(label + " (" + "PD_RimChatRequired".Translate() + ")", null));
                    continue;
                }

                options.Add(new FloatMenuOption(label, () =>
                {
                    Settings.RansomSystemOwner = captured;
                    WriteSettings();
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static TaggedString BuildRimChatStatusText()
        {
            if (!RimChatIntegration.IsInstalled)
            {
                return RimChatIntegration.StatusLabelKey().Translate();
            }

            return "PD_RimChatDetectedStatus".Translate(
                string.IsNullOrEmpty(RimChatIntegration.Version) ? "?" : RimChatIntegration.Version,
                RimChatIntegration.StatusLabelKey().Translate());
        }

        private static string OwnerDescriptionKey(PrisonerRansomSystemOwner owner)
        {
            switch (owner)
            {
                case PrisonerRansomSystemOwner.RimChat:
                    return "PD_RansomOwnerRimChatDesc";
                case PrisonerRansomSystemOwner.SafeIsolation:
                    return "PD_RansomOwnerSafeIsolationDesc";
                default:
                    return "PD_RansomOwnerPrisonerDiplomacyDesc";
            }
        }

        private static string FactionOverrideLabel(FactionNegotiationOverride value)
        {
            switch (value)
            {
                case FactionNegotiationOverride.NonNegotiating: return "PD_FactionTypeNonNegotiating".Translate();
                case FactionNegotiationOverride.Transactional: return "PD_FactionTypeTransactional".Translate();
                case FactionNegotiationOverride.Diplomatic: return "PD_FactionTypeDiplomatic".Translate();
                default: return "PD_FactionTypeAutomatic".Translate();
            }
        }
    }
}
