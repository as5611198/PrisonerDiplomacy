using System;
using System.Linq;
using RimWorld;
using Verse;

namespace PrisonerDiplomacy
{
    internal static class AiNarrativeContextUtility
    {
        private const int PersonaVersion = 2;

        public static AiNarrativePrompt BuildPrompt(
            AiNarrativeRecord narrative,
            PrisonerRecord prisonerRecord,
            FactionNegotiationMemory memory,
            string financialCapacity)
        {
            Pawn pawn = narrative.Prisoner;
            Faction faction = narrative.Faction;
            EnsurePersona(memory, faction, pawn);
            string configuredPersona = GetConfiguredPersona(faction, pawn);
            PrisonerMemoryEvent recentEvent = memory?.RecentEvents?.LastOrDefault();
            string playerNote = AiNegotiationNoteUtility.Normalize(narrative.PlayerNote);
            return new AiNarrativePrompt
            {
                requestId = narrative.RequestId,
                contextId = narrative.ContextId,
                candidateVersion = narrative.CandidateVersion,
                targetLanguage = LanguageDatabase.activeLanguage?.folderName ?? "English",
                eventKind = narrative.EventKind.ToString(),
                formalOutcome = narrative.FormalOutcome,
                formalTerms = narrative.FormalTerms ?? string.Empty,
                playerNote = playerNote,
                playerEmotion = string.IsNullOrEmpty(playerNote)
                    ? "neutral"
                    : narrative.PlayerEmotion ?? AiNegotiationNoteUtility.Classify(playerNote),
                advisoryEnabled = PrisonerDiplomacyMod.Settings?.EnableAiNegotiationAdjustments == true
                    && narrative.DealId != null
                    && (narrative.EventKind == AiNarrativeEventKind.PlayerDemandCountered
                        || narrative.EventKind == AiNarrativeEventKind.FinalCounter),
                advisory = BuildAdvisoryContext(narrative),
                transaction = BuildTransactionContext(narrative.EventKind),
                faction = new AiNarrativeFactionContext
                {
                    name = faction?.Name ?? "Unknown faction",
                    negotiationType = FactionNegotiationUtility.GetType(faction).ToString(),
                    permanentHostility = faction?.def?.permanentEnemy == true ? "permanent enemy" : "not permanent enemy",
                    financialCapacity = financialCapacity ?? "unknown",
                    archetype = GetFactionArchetype(faction),
                    ideology = GetIdeologySignals(faction),
                    persona = !string.IsNullOrEmpty(configuredPersona)
                        ? "custom context: " + configuredPersona
                        : memory?.AiPersonaSummary ?? "restrained and pragmatic"
                },
                prisoner = new AiNarrativePrisonerContext
                {
                    name = pawn?.LabelShort ?? narrative.PrisonerLoadId ?? "Unknown prisoner",
                    identity = GetIdentity(pawn),
                    importance = prisonerRecord != null ? prisonerRecord.Importance.ToString() : "unknown",
                    health = GetHealthBand(pawn),
                    incapacitated = pawn?.Downed == true,
                    needsMedicalCare = pawn?.health?.HasHediffsNeedingTendByPlayer(true) == true,
                    combatContext = BuildCombatContext(prisonerRecord),
                    relationshipToFaction = BuildFactionRelationship(pawn, faction)
                },
                memory = new AiNarrativeMemoryContext
                {
                    reliability = MemoryBand(memory?.Reliability ?? 0f, true),
                    treatment = MemoryBand(memory?.Treatment ?? 0f, true),
                    resentment = MemoryBand(memory?.Resentment ?? 0f, false),
                    recentEvent = recentEvent == null
                        ? "none"
                        : recentEvent.ReasonKey.Translate(recentEvent.PawnLabel ?? "?").ToString(),
                    historicalGrievance = BuildHistoricalGrievance(memory)
                }
            };
        }

        private static AiNegotiationAdvisoryContext BuildAdvisoryContext(AiNarrativeRecord narrative)
        {
            bool eligible = PrisonerDiplomacyMod.Settings?.EnableAiNegotiationAdjustments == true
                && narrative?.DealId != null
                && (narrative.EventKind == AiNarrativeEventKind.PlayerDemandCountered
                    || narrative.EventKind == AiNarrativeEventKind.FinalCounter);
            return new AiNegotiationAdvisoryContext
            {
                eligible = eligible,
                currentTerms = eligible ? narrative.FormalTerms ?? string.Empty : string.Empty,
                allowedAdjustment = eligible
                    ? "bounded adjustment of the existing counteroffer, clamped by faction reserve and material limits"
                    : "none",
                signals = eligible
                    ? "urgency=critical|high|normal|low; concession=high|medium|low; leverageResponse=threatened|neutral|conciliatory"
                    : "none"
            };
        }

        internal static AiNarrativeTransactionContext BuildTransactionContext(
            AiNarrativeEventKind eventKind)
        {
            switch (eventKind)
            {
                case AiNarrativeEventKind.FactionOffer:
                case AiNarrativeEventKind.PlayerDemandAccepted:
                case AiNarrativeEventKind.PlayerDemandCountered:
                case AiNarrativeEventKind.PlayerDemandRejected:
                case AiNarrativeEventKind.FinalCounter:
                    return new AiNarrativeTransactionContext
                    {
                        transactionType = "prisoner_ransom",
                        rewardDirection = "faction_to_player",
                        playerObligation = "release the held prisoner after accepting the terms",
                        factionObligation = "provide the listed reward to the player after the verified release"
                    };
                case AiNarrativeEventKind.PiratePaymentDelayed:
                    return new AiNarrativeTransactionContext
                    {
                        transactionType = "prisoner_ransom",
                        rewardDirection = "faction_to_player",
                        playerObligation = "the prisoner has already been released",
                        factionObligation = "provide the delayed listed reward to the player"
                    };
                case AiNarrativeEventKind.DealCompleted:
                    return new AiNarrativeTransactionContext
                    {
                        transactionType = "prisoner_ransom",
                        rewardDirection = "faction_to_player",
                        playerObligation = "the prisoner was released",
                        factionObligation = "the listed reward was provided to the player"
                    };
                case AiNarrativeEventKind.ExchangeCompleted:
                    return new AiNarrativeTransactionContext
                    {
                        transactionType = "prisoner_exchange",
                        rewardDirection = "bidirectional_exchange",
                        playerObligation = "release the held prisoner and provide only the listed compensation",
                        factionObligation = "return the named hostage to the player"
                    };
                default:
                    return new AiNarrativeTransactionContext
                    {
                        transactionType = "deal_outcome",
                        rewardDirection = "no_new_transfer",
                        playerObligation = "none beyond the formal outcome",
                        factionObligation = "none beyond the formal outcome"
                    };
            }
        }

        public static void EnsurePersona(FactionNegotiationMemory memory, Faction faction, Pawn pawn = null)
        {
            if (memory == null || faction == null)
            {
                return;
            }

            FactionNegotiationType type = FactionNegotiationUtility.GetType(faction);
            string configuredPersona = GetConfiguredPersona(faction, pawn);
            if (memory.AiPersonaVersion == PersonaVersion
                && memory.AiPersonaNegotiationType == type
                && (string.IsNullOrEmpty(configuredPersona)
                    || memory.AiPersonaSummary?.IndexOf(configuredPersona, StringComparison.Ordinal) >= 0)
                && !string.IsNullOrWhiteSpace(memory.AiPersonaSummary))
            {
                return;
            }

            string archetype = GetFactionArchetype(faction);
            string core = !string.IsNullOrEmpty(configuredPersona)
                ? "Follow this faction persona context while remaining grounded in the formal terms: " + configuredPersona
                : archetype == "imperial_nobility"
                ? "arrogant, ceremonious, status-conscious, and obsessed with honor while speaking as though every concession is generous"
                : archetype == "brutal_pirate"
                    ? "aggressive, terse, slang-heavy, threatening, and ruthlessly practical about leverage and profit"
                    : archetype == "ancestral_tribe"
                        ? "rooted in ancestors, spirits, bloodline, and nature, with sincere confusion about advanced technology"
                        : type == FactionNegotiationType.Transactional
                            ? "terse, hard-edged, profit-minded, and attentive to leverage and proven reliability"
                            : type == FactionNegotiationType.Diplomatic
                                ? "formal, controlled, protective of faction members, and attentive to treatment and agreements"
                                : "distant, guarded, and unwilling to imply a broader relationship";

            string ideology = GetIdeologySignals(faction);
            if (ideology != "none known")
            {
                core += ". Treat the faction's ideology signals as lived doctrine, not as a generic description: " + ideology;
            }

            string[] traits =
            {
                "Speaks with restrained pride.",
                "Prefers direct statements over ceremony.",
                "Emphasizes collective duty over personal emotion.",
                "Uses cautious language and avoids unnecessary promises."
            };
            int hash = GenText.StableStringHash(faction.def?.defName ?? faction.Name ?? string.Empty) & int.MaxValue;
            memory.AiPersonaSummary = core + ". " + traits[hash % traits.Length];
            memory.AiPersonaVersion = PersonaVersion;
            memory.AiPersonaNegotiationType = type;
        }

        private static string GetConfiguredPersona(Faction faction, Pawn pawn)
        {
            string factionDefName = faction?.def?.defName;
            PrisonerDiplomacySettings settings = PrisonerDiplomacyMod.Settings;
            string overridePersona = settings?.GetFactionPersonaOverride(factionDefName);
            if (!string.IsNullOrEmpty(overridePersona))
            {
                return overridePersona;
            }

            string providerPersona = PrisonerDiplomacyExtensionRegistry.GetPersona(pawn, faction);
            if (!string.IsNullOrEmpty(providerPersona))
            {
                return providerPersona;
            }

            return PrisonerDiplomacySettings.NormalizePersona(settings?.AiDefaultFactionPersona);
        }

        private static string GetFactionArchetype(Faction faction)
        {
            string defName = faction?.def?.defName ?? string.Empty;
            if (defName.IndexOf("Empire", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "imperial_nobility";
            }

            if (FactionNegotiationUtility.IsTransactional(faction))
            {
                return "brutal_pirate";
            }

            if ((faction?.def?.techLevel ?? TechLevel.Neolithic) <= TechLevel.Neolithic)
            {
                return "ancestral_tribe";
            }

            return "diplomatic_faction";
        }

        private static string GetIdeologySignals(Faction faction)
        {
            Ideo primaryIdeo = faction?.ideos?.PrimaryIdeo;
            if (primaryIdeo == null)
            {
                return "none known";
            }

            var signals = primaryIdeo.PreceptsListForReading
                .Where(precept => precept != null)
                .Select(precept => precept.LabelCap.ToString())
                .Where(label => !string.IsNullOrWhiteSpace(label))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(4)
                .ToList();
            if (signals.Count == 0 && !string.IsNullOrWhiteSpace(primaryIdeo.name))
            {
                signals.Add(primaryIdeo.name);
            }

            return signals.Count == 0 ? "none known" : string.Join(", ", signals);
        }

        private static string BuildCombatContext(PrisonerRecord record)
        {
            if (record?.RecentBattleEvents == null || record.RecentBattleEvents.Count == 0)
            {
                return "no recent battle detail recorded";
            }

            return string.Join(
                " | ",
                record.RecentBattleEvents
                    .Where(item => item != null && !string.IsNullOrWhiteSpace(item.Description))
                    .OrderByDescending(item => item.Tick)
                    .Take(3)
                    .Select(item => item.Description));
        }

        private static string BuildFactionRelationship(Pawn pawn, Faction faction)
        {
            if (pawn == null || faction == null)
            {
                return "no known direct relation to the faction leadership";
            }

            if (faction.leader == pawn)
            {
                return "the prisoner is the faction leader";
            }

            Pawn leader = faction.leader;
            if (leader == null || pawn.relations == null)
            {
                return "no known direct relation to the faction leadership";
            }

            PawnRelationDef relation = PawnRelationUtility.GetMostImportantRelation(pawn, leader);
            return relation == null
                ? "no known direct relation to the faction leadership"
                : "the prisoner is the faction leader's " + relation.LabelCap.ToString()
                    + " (leader: " + leader.LabelShortCap + ")";
        }

        private static string BuildHistoricalGrievance(FactionNegotiationMemory memory)
        {
            if (memory?.RecentEvents == null)
            {
                return "none known";
            }

            string[] events = memory.RecentEvents
                .Where(item => item != null && !string.IsNullOrEmpty(item.ReasonKey))
                .OrderByDescending(item => item.Tick)
                .Take(4)
                .Select(item => item.ReasonKey.Translate(item.PawnLabel ?? "?").ToString())
                .ToArray();
            return events.Length == 0 ? "none known" : string.Join(" | ", events);
        }

        private static string GetIdentity(Pawn pawn)
        {
            string title = pawn?.story?.TitleShortCap.ToString();
            if (!string.IsNullOrWhiteSpace(title))
            {
                return title;
            }

            return pawn?.kindDef?.LabelCap.ToString() ?? "unknown";
        }

        private static string GetHealthBand(Pawn pawn)
        {
            float health = pawn?.health?.summaryHealth?.SummaryHealthPercent ?? 0f;
            if (health >= 0.85f) return "healthy";
            if (health >= 0.55f) return "injured but stable";
            if (health >= 0.25f) return "seriously injured";
            return "critical";
        }

        private static string MemoryBand(float value, bool signed)
        {
            if (signed)
            {
                if (value >= 35f) return "strongly positive";
                if (value >= 10f) return "positive";
                if (value <= -35f) return "strongly negative";
                if (value <= -10f) return "negative";
                return "neutral";
            }

            if (value >= 55f) return "severe";
            if (value >= 20f) return "lasting";
            return "low";
        }
    }
}
