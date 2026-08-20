using System.Collections.Generic;
using RimWorld;
using Verse;

namespace PrisonerDiplomacy
{
    public sealed class FactionNegotiationMemory : IExposable
    {
        public Faction Faction;
        public int LastPlayerNegotiationTick = -1;
        public float DiplomaticReserve = -1f;
        public int ReserveUpdatedTick = -1;
        public int Impatience;
        public int NegotiationSuspendedUntilTick = -1;
        public int UnsolicitedOffersSuppressedUntilTick = -1;
        public int UnsolicitedOfferRejectionCount;
        public int SuccessfulDeals;
        public int RejectedNegotiations;
        public string LastDealSummary;
        public float Reliability;
        public float Treatment;
        public float Resentment;
        public float ResentmentFloor;
        public int MemoryUpdatedTick = -1;
        public string AiPersonaSummary;
        public int AiPersonaVersion;
        public FactionNegotiationType AiPersonaNegotiationType;
        public List<PrisonerMemoryEvent> RecentEvents = new List<PrisonerMemoryEvent>();

        public void ExposeData()
        {
            Scribe_References.Look(ref Faction, "faction");
            Scribe_Values.Look(ref LastPlayerNegotiationTick, "lastPlayerNegotiationTick", -1);
            Scribe_Values.Look(ref DiplomaticReserve, "diplomaticReserve", -1f);
            Scribe_Values.Look(ref ReserveUpdatedTick, "reserveUpdatedTick", -1);
            Scribe_Values.Look(ref Impatience, "impatience");
            Scribe_Values.Look(ref NegotiationSuspendedUntilTick, "negotiationSuspendedUntilTick", -1);
            Scribe_Values.Look(ref UnsolicitedOffersSuppressedUntilTick, "unsolicitedOffersSuppressedUntilTick", -1);
            Scribe_Values.Look(ref UnsolicitedOfferRejectionCount, "unsolicitedOfferRejectionCount");
            Scribe_Values.Look(ref SuccessfulDeals, "successfulDeals");
            Scribe_Values.Look(ref RejectedNegotiations, "rejectedNegotiations");
            Scribe_Values.Look(ref LastDealSummary, "lastDealSummary");
            Scribe_Values.Look(ref Reliability, "reliability");
            Scribe_Values.Look(ref Treatment, "treatment");
            Scribe_Values.Look(ref Resentment, "resentment");
            Scribe_Values.Look(ref ResentmentFloor, "resentmentFloor");
            Scribe_Values.Look(ref MemoryUpdatedTick, "memoryUpdatedTick", -1);
            Scribe_Values.Look(ref AiPersonaSummary, "aiPersonaSummary");
            Scribe_Values.Look(ref AiPersonaVersion, "aiPersonaVersion");
            Scribe_Values.Look(ref AiPersonaNegotiationType, "aiPersonaNegotiationType", FactionNegotiationType.Diplomatic);
            Scribe_Collections.Look(ref RecentEvents, "recentPrisonerEvents", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                RecentEvents = RecentEvents ?? new List<PrisonerMemoryEvent>();
            }
        }
    }
}
