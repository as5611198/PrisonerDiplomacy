using System.Collections.Generic;
using RimWorld;
using Verse;

namespace PrisonerDiplomacy
{
    public sealed class PrisonerRecord : IExposable
    {
        public Pawn Pawn;
        public string PawnLoadId;
        public Faction OriginalFaction;
        public int CapturedTick;
        public float CapturedMarketValue;
        public PrisonerImportance Importance;
        public int DiplomaticValue;
        public string ActiveDealId;
        public int LastProposalTick = -1;
        public int UnsolicitedOfferSuppressedUntilTick = -1;
        public int UnsolicitedOfferRejectionCount;
        public int LastPlayerNegotiationTick = -1;
        public int ScheduledFactionOfferTick = -1;
        public int NegotiationCount;
        public float CapturedHealthPercent = 1f;
        public bool WasLifeThreatening;
        public bool CriticalRecoveryRecorded;
        public int LastMissingPartCount;
        public int LastPermanentInjuryCount;
        public int LastTreatmentCheckTick = -1;
        public int LastPlayerTreatmentTick = -1;
        public int StarvationTicks;
        public bool MalnutritionRecorded;
        public bool TerminalOutcomeRecorded;
        public bool PlayerCausedPermanentHarm;
        public int LastPermanentHarmTick = -1;
        public List<PrisonerBattleEvent> RecentBattleEvents = new List<PrisonerBattleEvent>();

        public void ExposeData()
        {
            Scribe_References.Look(ref Pawn, "pawn", true);
            Scribe_Values.Look(ref PawnLoadId, "pawnLoadId");
            Scribe_References.Look(ref OriginalFaction, "originalFaction");
            Scribe_Values.Look(ref CapturedTick, "capturedTick");
            Scribe_Values.Look(ref CapturedMarketValue, "capturedMarketValue");
            Scribe_Values.Look(ref Importance, "importance", PrisonerImportance.Regular);
            Scribe_Values.Look(ref DiplomaticValue, "diplomaticValue");
            Scribe_Values.Look(ref ActiveDealId, "activeDealId");
            Scribe_Values.Look(ref LastProposalTick, "lastProposalTick", -1);
            Scribe_Values.Look(ref UnsolicitedOfferSuppressedUntilTick, "unsolicitedOfferSuppressedUntilTick", -1);
            Scribe_Values.Look(ref UnsolicitedOfferRejectionCount, "unsolicitedOfferRejectionCount");
            Scribe_Values.Look(ref LastPlayerNegotiationTick, "lastPlayerNegotiationTick", -1);
            Scribe_Values.Look(ref ScheduledFactionOfferTick, "scheduledFactionOfferTick", -1);
            Scribe_Values.Look(ref NegotiationCount, "negotiationCount");
            Scribe_Values.Look(ref CapturedHealthPercent, "capturedHealthPercent", 1f);
            Scribe_Values.Look(ref WasLifeThreatening, "wasLifeThreatening");
            Scribe_Values.Look(ref CriticalRecoveryRecorded, "criticalRecoveryRecorded");
            Scribe_Values.Look(ref LastMissingPartCount, "lastMissingPartCount");
            Scribe_Values.Look(ref LastPermanentInjuryCount, "lastPermanentInjuryCount");
            Scribe_Values.Look(ref LastTreatmentCheckTick, "lastTreatmentCheckTick", -1);
            Scribe_Values.Look(ref LastPlayerTreatmentTick, "lastPlayerTreatmentTick", -1);
            Scribe_Values.Look(ref StarvationTicks, "starvationTicks");
            Scribe_Values.Look(ref MalnutritionRecorded, "malnutritionRecorded");
            Scribe_Values.Look(ref TerminalOutcomeRecorded, "terminalOutcomeRecorded");
            Scribe_Values.Look(ref PlayerCausedPermanentHarm, "playerCausedPermanentHarm");
            Scribe_Values.Look(ref LastPermanentHarmTick, "lastPermanentHarmTick", -1);
            Scribe_Collections.Look(ref RecentBattleEvents, "recentBattleEvents", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                RecentBattleEvents = RecentBattleEvents ?? new List<PrisonerBattleEvent>();
            }
        }
    }
}
