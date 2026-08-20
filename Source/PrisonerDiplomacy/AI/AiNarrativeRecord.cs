using RimWorld;
using Verse;

namespace PrisonerDiplomacy
{
    public enum AiNarrativeEventKind
    {
        FactionOffer,
        PlayerDemandAccepted,
        PlayerDemandCountered,
        PlayerDemandRejected,
        FinalCounter,
        PiratePaymentDelayed,
        DealCompleted,
        ExchangeCompleted,
        DealFailed
    }

    public enum AiNarrativeStatus
    {
        Waiting,
        Generated,
        Fallback,
        Cancelled
    }

    public sealed class AiNarrativeRecord : IExposable
    {
        public string ContextId;
        public string RequestId;
        public string WindowContextId;
        public string DealId;
        public Pawn Prisoner;
        public string PrisonerLoadId;
        public Faction Faction;
        public AiNarrativeEventKind EventKind;
        public AiNarrativeStatus Status;
        public int CandidateVersion;
        public bool HasExpectedDealState;
        public DealState ExpectedDealState;
        public int ExpectedNegotiationRound = -1;
        public int ExpectedNegotiationCount = -1;
        public string FormalOutcome;
        public string FormalTerms;
        public string PlayerNote;
        public string PlayerEmotion;
        public string FallbackText;
        public string GeneratedText;
        public string FailureCode;
        public bool AdvisoryApplied;
        public string AdvisorySummary;
        public int CreatedTick;
        public int ResolvedTick = -1;

        public string DisplayText => Status == AiNarrativeStatus.Generated && !string.IsNullOrWhiteSpace(GeneratedText)
            ? GeneratedText
            : FallbackText;

        public void ExposeData()
        {
            Scribe_Values.Look(ref ContextId, "contextId");
            Scribe_Values.Look(ref RequestId, "requestId");
            Scribe_Values.Look(ref WindowContextId, "windowContextId");
            Scribe_Values.Look(ref DealId, "dealId");
            Scribe_References.Look(ref Prisoner, "prisoner", true);
            Scribe_Values.Look(ref PrisonerLoadId, "prisonerLoadId");
            Scribe_References.Look(ref Faction, "faction");
            Scribe_Values.Look(ref EventKind, "eventKind", AiNarrativeEventKind.FactionOffer);
            Scribe_Values.Look(ref Status, "status", AiNarrativeStatus.Fallback);
            Scribe_Values.Look(ref CandidateVersion, "candidateVersion");
            Scribe_Values.Look(ref HasExpectedDealState, "hasExpectedDealState");
            Scribe_Values.Look(ref ExpectedDealState, "expectedDealState", DealState.Offered);
            Scribe_Values.Look(ref ExpectedNegotiationRound, "expectedNegotiationRound", -1);
            Scribe_Values.Look(ref ExpectedNegotiationCount, "expectedNegotiationCount", -1);
            Scribe_Values.Look(ref FormalOutcome, "formalOutcome");
            Scribe_Values.Look(ref FormalTerms, "formalTerms");
            Scribe_Values.Look(ref PlayerNote, "playerNote");
            Scribe_Values.Look(ref PlayerEmotion, "playerEmotion");
            Scribe_Values.Look(ref FallbackText, "fallbackText");
            Scribe_Values.Look(ref GeneratedText, "generatedText");
            Scribe_Values.Look(ref FailureCode, "failureCode");
            Scribe_Values.Look(ref AdvisoryApplied, "advisoryApplied", false);
            Scribe_Values.Look(ref AdvisorySummary, "advisorySummary");
            Scribe_Values.Look(ref CreatedTick, "createdTick");
            Scribe_Values.Look(ref ResolvedTick, "resolvedTick", -1);
        }
    }
}
