using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace PrisonerDiplomacy
{
    /// <summary>
    /// Stable extension surface for Prisoner Diplomacy 1.2.
    /// Add-ons may register metadata and adapters, but the core remains the
    /// only authority allowed to mutate prisoners, deals, payments, or events.
    /// </summary>
    public static class PrisonerDiplomacyApi
    {
        public const string ApiVersion = "1.2.0";
        public const int ApiMajorVersion = 1;
    }

    public enum PrisonerDiplomacyEventKind
    {
        NeutralTradeCaravan,
        RansomAmbushRetaliation,
        FalseSurrenderInfiltration,
        PublicWarCrimeTrial
    }

    public sealed class PrisonerDiplomacyEventDefinition
    {
        public string EventId { get; private set; }
        public string LabelKey { get; private set; }
        public string DescriptionKey { get; private set; }
        public PrisonerDiplomacyEventKind Kind { get; private set; }
        public bool RequiresPrisoner { get; private set; }

        public PrisonerDiplomacyEventDefinition(
            string eventId,
            string labelKey,
            string descriptionKey,
            PrisonerDiplomacyEventKind kind,
            bool requiresPrisoner = true)
        {
            EventId = eventId;
            LabelKey = labelKey;
            DescriptionKey = descriptionKey;
            Kind = kind;
            RequiresPrisoner = requiresPrisoner;
        }
    }

    public sealed class PrisonerDiplomacySpecialRewardDefinition
    {
        public string RewardId { get; private set; }
        public string LabelKey { get; private set; }
        public string DescriptionKey { get; private set; }
        public string RequiredThingDefName { get; private set; }
        public int MinimumCount { get; private set; }

        public PrisonerDiplomacySpecialRewardDefinition(
            string rewardId,
            string labelKey,
            string descriptionKey,
            string requiredThingDefName,
            int minimumCount = 1)
        {
            RewardId = rewardId;
            LabelKey = labelKey;
            DescriptionKey = descriptionKey;
            RequiredThingDefName = requiredThingDefName;
            MinimumCount = Math.Max(1, minimumCount);
        }
    }

    public sealed class PrisonerDiplomacyRaceContext
    {
        public Pawn Prisoner { get; private set; }
        public Faction Faction { get; private set; }
        public string RaceDefName { get; private set; }
        public string PawnKindDefName { get; private set; }
        public string FactionDefName { get; private set; }
        public TechLevel FactionTechLevel { get; private set; }

        internal static PrisonerDiplomacyRaceContext Create(Pawn prisoner, Faction faction)
        {
            return new PrisonerDiplomacyRaceContext
            {
                Prisoner = prisoner,
                Faction = faction,
                RaceDefName = prisoner?.kindDef?.race?.defName ?? prisoner?.def?.defName,
                PawnKindDefName = prisoner?.kindDef?.defName,
                FactionDefName = faction?.def?.defName,
                FactionTechLevel = faction?.def?.techLevel ?? TechLevel.Neolithic
            };
        }
    }

    public interface IPrisonerDiplomacyRaceAdapter
    {
        string AdapterId { get; }
        string ApiVersion { get; }
        bool AppliesTo(PrisonerDiplomacyRaceContext context);
        int GetDiplomaticValueAdjustment(PrisonerDiplomacyRaceContext context);
        IEnumerable<PrisonerDiplomacySpecialRewardDefinition> GetSpecialRewards(
            PrisonerDiplomacyRaceContext context);
    }

    /// <summary>
    /// Supplies a bounded, untrusted persona description for a faction or race.
    /// The core uses this only as narrative context; it cannot change gameplay.
    /// </summary>
    public interface IPrisonerDiplomacyPersonaProvider
    {
        string ProviderId { get; }
        string ApiVersion { get; }
        bool AppliesTo(PrisonerDiplomacyRaceContext context);
        string GetPersona(PrisonerDiplomacyRaceContext context);
    }

    public interface IPrisonerDiplomacyExtension
    {
        string ExtensionId { get; }
        string ApiVersion { get; }
        IEnumerable<PrisonerDiplomacyEventDefinition> GetEventDefinitions();
        IEnumerable<IPrisonerDiplomacyRaceAdapter> GetRaceAdapters();
    }

    public static class PrisonerDiplomacyExtensionRegistry
    {
        private static readonly Dictionary<string, IPrisonerDiplomacyExtension> Extensions =
            new Dictionary<string, IPrisonerDiplomacyExtension>(StringComparer.Ordinal);
        private static readonly Dictionary<string, PrisonerDiplomacyEventDefinition> EventDefinitions =
            new Dictionary<string, PrisonerDiplomacyEventDefinition>(StringComparer.Ordinal);
        private static readonly Dictionary<string, IPrisonerDiplomacyRaceAdapter> RaceAdapters =
            new Dictionary<string, IPrisonerDiplomacyRaceAdapter>(StringComparer.Ordinal);
        private static readonly Dictionary<string, IPrisonerDiplomacyPersonaProvider> PersonaProviders =
            new Dictionary<string, IPrisonerDiplomacyPersonaProvider>(StringComparer.Ordinal);

        public static IReadOnlyList<string> RegisteredExtensionIds => Extensions.Keys.OrderBy(id => id).ToList();
        public static IReadOnlyList<PrisonerDiplomacyEventDefinition> RegisteredEventDefinitions =>
            EventDefinitions.Values.OrderBy(definition => definition.EventId).ToList();
        public static IReadOnlyList<PrisonerDiplomacySpecialRewardDefinition> RegisteredSpecialRewardDefinitions =>
            RaceAdapters.Values
                .SelectMany(adapter => SafeSpecialRewards(adapter, null))
                .Where(reward => reward != null && !string.IsNullOrWhiteSpace(reward.RewardId))
                .GroupBy(reward => reward.RewardId, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(reward => reward.RewardId)
                .ToList();

        public static IReadOnlyList<string> RegisteredPersonaProviderIds =>
            PersonaProviders.Keys.OrderBy(id => id).ToList();

        public static bool Register(IPrisonerDiplomacyExtension extension)
        {
            if (extension == null
                || string.IsNullOrWhiteSpace(extension.ExtensionId)
                || !IsCompatibleVersion(extension.ApiVersion))
            {
                return false;
            }

            string extensionId = extension.ExtensionId.Trim();
            if (Extensions.ContainsKey(extensionId))
            {
                return false;
            }

            List<PrisonerDiplomacyEventDefinition> definitions = (extension.GetEventDefinitions()
                ?? Enumerable.Empty<PrisonerDiplomacyEventDefinition>())
                .Where(definition => definition != null
                    && !string.IsNullOrWhiteSpace(definition.EventId)
                    && !EventDefinitions.ContainsKey(definition.EventId))
                .ToList();
            List<IPrisonerDiplomacyRaceAdapter> adapters = (extension.GetRaceAdapters()
                ?? Enumerable.Empty<IPrisonerDiplomacyRaceAdapter>())
                .Where(adapter => adapter != null
                    && !string.IsNullOrWhiteSpace(adapter.AdapterId)
                    && !RaceAdapters.ContainsKey(adapter.AdapterId)
                    && IsCompatibleVersion(adapter.ApiVersion))
                .ToList();

            Extensions.Add(extensionId, extension);
            foreach (PrisonerDiplomacyEventDefinition definition in definitions)
            {
                EventDefinitions.Add(definition.EventId, definition);
            }
            foreach (IPrisonerDiplomacyRaceAdapter adapter in adapters)
            {
                RaceAdapters.Add(adapter.AdapterId, adapter);
            }
            return true;
        }

        public static bool RegisterPersonaProvider(IPrisonerDiplomacyPersonaProvider provider)
        {
            if (provider == null
                || string.IsNullOrWhiteSpace(provider.ProviderId)
                || !IsCompatibleVersion(provider.ApiVersion))
            {
                return false;
            }

            string providerId = provider.ProviderId.Trim();
            if (PersonaProviders.ContainsKey(providerId))
            {
                return false;
            }

            PersonaProviders.Add(providerId, provider);
            return true;
        }

        public static IReadOnlyList<PrisonerDiplomacySpecialRewardDefinition> GetSpecialRewards(
            Pawn prisoner,
            Faction faction)
        {
            PrisonerDiplomacyRaceContext context = PrisonerDiplomacyRaceContext.Create(prisoner, faction);
            return RaceAdapters.Values
                .Where(adapter => AppliesSafely(adapter, context))
                .SelectMany(adapter => SafeSpecialRewards(adapter, context))
                .Where(reward => reward != null && !string.IsNullOrWhiteSpace(reward.RewardId))
                .GroupBy(reward => reward.RewardId, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(reward => reward.RewardId)
                .ToList();
        }

        public static int GetDiplomaticValueAdjustment(Pawn prisoner, Faction faction)
        {
            PrisonerDiplomacyRaceContext context = PrisonerDiplomacyRaceContext.Create(prisoner, faction);
            return RaceAdapters.Values
                .Where(adapter => AppliesSafely(adapter, context))
                .Sum(adapter => ClampAdjustment(adapter.GetDiplomaticValueAdjustment(context)));
        }

        public static string GetPersona(Pawn prisoner, Faction faction)
        {
            PrisonerDiplomacyRaceContext context = PrisonerDiplomacyRaceContext.Create(prisoner, faction);
            foreach (IPrisonerDiplomacyPersonaProvider provider in PersonaProviders.Values
                .OrderBy(item => item.ProviderId, StringComparer.Ordinal))
            {
                try
                {
                    if (!provider.AppliesTo(context))
                    {
                        continue;
                    }

                    string persona = PrisonerDiplomacySettings.NormalizePersona(provider.GetPersona(context));
                    if (!string.IsNullOrEmpty(persona))
                    {
                        return persona;
                    }
                }
                catch (Exception exception)
                {
                    Log.Warning("[Prisoner Diplomacy] Ignored persona provider "
                        + provider.ProviderId + " after failure: " + exception.Message);
                }
            }

            return string.Empty;
        }

        private static bool AppliesSafely(
            IPrisonerDiplomacyRaceAdapter adapter,
            PrisonerDiplomacyRaceContext context)
        {
            try
            {
                return adapter.AppliesTo(context);
            }
            catch (Exception exception)
            {
                Log.Warning("[Prisoner Diplomacy] Ignored extension adapter "
                    + adapter.AdapterId + " after AppliesTo failed: " + exception.Message);
                return false;
            }
        }

        private static IEnumerable<PrisonerDiplomacySpecialRewardDefinition> SafeSpecialRewards(
            IPrisonerDiplomacyRaceAdapter adapter,
            PrisonerDiplomacyRaceContext context)
        {
            try
            {
                return adapter.GetSpecialRewards(context)
                    ?? Enumerable.Empty<PrisonerDiplomacySpecialRewardDefinition>();
            }
            catch (Exception exception)
            {
                Log.Warning("[Prisoner Diplomacy] Ignored extension adapter "
                    + adapter.AdapterId + " after GetSpecialRewards failed: " + exception.Message);
                return Enumerable.Empty<PrisonerDiplomacySpecialRewardDefinition>();
            }
        }

        private static bool IsCompatibleVersion(string version)
        {
            if (!Version.TryParse(version, out Version parsed)
                || !Version.TryParse(PrisonerDiplomacyApi.ApiVersion, out Version current))
            {
                return false;
            }
            return parsed.Major == current.Major && parsed <= current;
        }

        private static int ClampAdjustment(int adjustment)
        {
            return Math.Max(-1000, Math.Min(1000, adjustment));
        }
    }
}
