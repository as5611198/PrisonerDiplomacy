using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace PrisonerDiplomacy
{
    public sealed class FactionNegotiationOverrideEntry : IExposable
    {
        public string FactionDefName;
        public FactionNegotiationOverride Override;

        public void ExposeData()
        {
            Scribe_Values.Look(ref FactionDefName, "factionDefName");
            Scribe_Values.Look(ref Override, "override", FactionNegotiationOverride.Automatic);
        }
    }

    public sealed class FactionAiPersonaOverrideEntry : IExposable
    {
        public string FactionDefName;
        public string Persona;

        public void ExposeData()
        {
            Scribe_Values.Look(ref FactionDefName, "factionDefName");
            Scribe_Values.Look(ref Persona, "persona");
        }
    }

    public sealed class PrisonerDiplomacySettings : ModSettings
    {
        public bool EnableEnemyInitiatedRansoms = true;
        public float OfferFrequencyMultiplier = 1f;
        public float RansomValueMultiplier = 1f;
        public bool EnableFactionReserves = true;
        public bool EnableFactionMemory = true;
        public bool AllowPermanentEnemyNegotiation = true;
        public bool EnablePirateRisks = true;
        public bool EnableStrategicConsequences = true;
        public bool EnableCompatibilityLogging = true;
        public bool EnablePerformanceLogging;
        public bool EnableErrorTelemetryPrompts = true;
        public bool AlwaysSendErrorTelemetry;
        public bool ReduceUiMotion;
        public PrisonerDiplomacyMessageDetail MessageDetail = PrisonerDiplomacyMessageDetail.Standard;
        public PrisonerRansomSystemOwner RansomSystemOwner = PrisonerRansomSystemOwner.PrisonerDiplomacy;
        public bool EnableAiNarratives;
        public bool AiAllowExternalContext;
        public AiNarrativeProviderKind AiProvider = AiNarrativeProviderKind.OpenAI;
        public string AiCustomBaseUrl = string.Empty;
        public string AiEndpoint = "https://api.openai.com/v1/chat/completions";
        public string AiModel = "gpt-5.4";
        public string AiApiKey = string.Empty;
        public bool AiEndpointRequiresKey = true;
        public int AiTimeoutSeconds = 20;
        public bool AiShowTechnicalErrors;
        public bool EnableAiNegotiationAdjustments;
        public string AiDefaultFactionPersona = string.Empty;
        public List<string> AiFetchedModels = new List<string>();
        public List<FactionNegotiationOverrideEntry> FactionOverrides = new List<FactionNegotiationOverrideEntry>();
        public List<FactionAiPersonaOverrideEntry> FactionPersonaOverrides = new List<FactionAiPersonaOverrideEntry>();

        public override void ExposeData()
        {
            Scribe_Values.Look(ref EnableEnemyInitiatedRansoms, "enableEnemyInitiatedRansoms", true);
            Scribe_Values.Look(ref OfferFrequencyMultiplier, "offerFrequencyMultiplier", 1f);
            Scribe_Values.Look(ref RansomValueMultiplier, "ransomValueMultiplier", 1f);
            Scribe_Values.Look(ref EnableFactionReserves, "enableFactionReserves", true);
            Scribe_Values.Look(ref EnableFactionMemory, "enableFactionMemory", true);
            Scribe_Values.Look(ref AllowPermanentEnemyNegotiation, "allowPermanentEnemyNegotiation", true);
            Scribe_Values.Look(ref EnablePirateRisks, "enablePirateRisks", true);
            Scribe_Values.Look(ref EnableStrategicConsequences, "enableStrategicConsequences", true);
            Scribe_Values.Look(ref EnableCompatibilityLogging, "enableCompatibilityLogging", true);
            Scribe_Values.Look(ref EnablePerformanceLogging, "enablePerformanceLogging", false);
            Scribe_Values.Look(ref EnableErrorTelemetryPrompts, "enableErrorTelemetryPrompts", true);
            Scribe_Values.Look(ref AlwaysSendErrorTelemetry, "alwaysSendErrorTelemetry", false);
            Scribe_Values.Look(ref ReduceUiMotion, "reduceUiMotion", false);
            Scribe_Values.Look(ref MessageDetail, "messageDetail", PrisonerDiplomacyMessageDetail.Standard);
            Scribe_Values.Look(ref RansomSystemOwner, "ransomSystemOwner", PrisonerRansomSystemOwner.PrisonerDiplomacy);
            Scribe_Values.Look(ref EnableAiNarratives, "enableAiNarratives", false);
            Scribe_Values.Look(ref AiAllowExternalContext, "aiAllowExternalContext", false);
            Scribe_Values.Look(ref AiProvider, "aiProvider", AiNarrativeProviderKind.OpenAI);
            Scribe_Values.Look(ref AiCustomBaseUrl, "aiCustomBaseUrl", string.Empty);
            Scribe_Values.Look(ref AiEndpoint, "aiEndpoint", "https://api.openai.com/v1/chat/completions");
            Scribe_Values.Look(ref AiModel, "aiModel", "gpt-5.4");
            Scribe_Values.Look(ref AiApiKey, "aiApiKey", string.Empty);
            Scribe_Values.Look(ref AiEndpointRequiresKey, "aiEndpointRequiresKey", true);
            Scribe_Values.Look(ref AiTimeoutSeconds, "aiTimeoutSeconds", 20);
            Scribe_Values.Look(ref AiShowTechnicalErrors, "aiShowTechnicalErrors", false);
            Scribe_Values.Look(ref EnableAiNegotiationAdjustments, "enableAiNegotiationAdjustments", false);
            Scribe_Values.Look(ref AiDefaultFactionPersona, "aiDefaultFactionPersona", string.Empty);
            Scribe_Collections.Look(ref AiFetchedModels, "aiFetchedModels", LookMode.Value);
            Scribe_Collections.Look(ref FactionOverrides, "factionOverrides", LookMode.Deep);
            Scribe_Collections.Look(ref FactionPersonaOverrides, "factionPersonaOverrides", LookMode.Deep);
            FactionOverrides = FactionOverrides ?? new List<FactionNegotiationOverrideEntry>();
            FactionPersonaOverrides = FactionPersonaOverrides ?? new List<FactionAiPersonaOverrideEntry>();
            AiFetchedModels = AiFetchedModels ?? new List<string>();
            AiCustomBaseUrl = AiCustomBaseUrl ?? string.Empty;
            AiEndpoint = AiEndpoint ?? string.Empty;
            AiModel = AiModel ?? string.Empty;
            AiApiKey = AiApiKey ?? string.Empty;
            AiDefaultFactionPersona = NormalizePersona(AiDefaultFactionPersona);
            FactionPersonaOverrides = FactionPersonaOverrides
                .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.FactionDefName))
                .Select(entry =>
                {
                    entry.FactionDefName = entry.FactionDefName.Trim();
                    entry.Persona = NormalizePersona(entry.Persona);
                    return entry;
                })
                .Where(entry => !string.IsNullOrEmpty(entry.Persona))
                .GroupBy(entry => entry.FactionDefName, System.StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
            OfferFrequencyMultiplier = Mathf.Clamp(OfferFrequencyMultiplier, 0.25f, 4f);
            RansomValueMultiplier = Mathf.Clamp(RansomValueMultiplier, 0.50f, 2f);
            AiTimeoutSeconds = Mathf.Clamp(AiTimeoutSeconds, 3, 60);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                MigrateLegacyAiConfiguration();
            }
        }

        public void RefreshLegacyAiEndpoint()
        {
            AiEndpoint = AiNarrativeProviderCatalog.ResolveGenerationEndpoint(this);
        }

        internal void MigrateLegacyAiConfiguration()
        {
            const string legacyOpenAiEndpoint = "https://api.openai.com/v1/chat/completions";
            if (AiProvider == AiNarrativeProviderKind.OpenAI
                && string.IsNullOrWhiteSpace(AiCustomBaseUrl)
                && !string.IsNullOrWhiteSpace(AiEndpoint)
                && !string.Equals(AiEndpoint.TrimEnd('/'), legacyOpenAiEndpoint, System.StringComparison.OrdinalIgnoreCase))
            {
                AiProvider = AiNarrativeProviderKind.CustomOpenAI;
                AiCustomBaseUrl = AiNarrativeProviderCatalog.NormalizeBaseUrl(AiEndpoint);
            }

            if (AiProvider == AiNarrativeProviderKind.CustomOpenAI && string.IsNullOrWhiteSpace(AiCustomBaseUrl))
            {
                AiCustomBaseUrl = AiNarrativeProviderCatalog.NormalizeBaseUrl(AiEndpoint);
            }

            AiFetchedModels = AiFetchedModels
                .Where(model => !string.IsNullOrWhiteSpace(model))
                .Select(model => model.Trim())
                .Distinct(System.StringComparer.OrdinalIgnoreCase)
                .OrderBy(model => model, System.StringComparer.OrdinalIgnoreCase)
                .ToList();
            RefreshLegacyAiEndpoint();
        }

        public FactionNegotiationOverride GetOverride(string factionDefName)
        {
            return FactionOverrides.FirstOrDefault(entry => entry.FactionDefName == factionDefName)?.Override
                ?? FactionNegotiationOverride.Automatic;
        }

        public void SetOverride(string factionDefName, FactionNegotiationOverride value)
        {
            FactionNegotiationOverrideEntry entry = FactionOverrides.FirstOrDefault(item => item.FactionDefName == factionDefName);
            if (value == FactionNegotiationOverride.Automatic)
            {
                if (entry != null)
                {
                    FactionOverrides.Remove(entry);
                }
                return;
            }

            if (entry == null)
            {
                entry = new FactionNegotiationOverrideEntry { FactionDefName = factionDefName };
                FactionOverrides.Add(entry);
            }
            entry.Override = value;
        }

        public string GetFactionPersonaOverride(string factionDefName)
        {
            if (string.IsNullOrWhiteSpace(factionDefName))
            {
                return string.Empty;
            }

            return FactionPersonaOverrides
                .FirstOrDefault(entry => entry != null
                    && string.Equals(entry.FactionDefName, factionDefName.Trim(), System.StringComparison.OrdinalIgnoreCase))
                ?.Persona ?? string.Empty;
        }

        public void SetFactionPersonaOverride(string factionDefName, string persona)
        {
            if (string.IsNullOrWhiteSpace(factionDefName))
            {
                return;
            }

            string normalized = NormalizePersona(persona);
            FactionAiPersonaOverrideEntry entry = FactionPersonaOverrides.FirstOrDefault(item => item != null
                && string.Equals(item.FactionDefName, factionDefName.Trim(), System.StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(normalized))
            {
                if (entry != null)
                {
                    FactionPersonaOverrides.Remove(entry);
                }
                return;
            }

            if (entry == null)
            {
                entry = new FactionAiPersonaOverrideEntry { FactionDefName = factionDefName.Trim() };
                FactionPersonaOverrides.Add(entry);
            }
            entry.Persona = normalized;
        }

        internal static string NormalizePersona(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string filtered = new string(value
                .Where(character => !char.IsControl(character))
                .ToArray())
                .Trim();
            return filtered.Length <= 500 ? filtered : filtered.Substring(0, 500).TrimEnd();
        }
    }
}
