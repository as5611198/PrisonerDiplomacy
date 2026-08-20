using RimWorld;
using Verse;

namespace PrisonerDiplomacy
{
    public sealed class PrisonerDeal : IExposable
    {
        public string DealId;
        public Pawn Prisoner;
        public string PrisonerLoadId;
        public Faction Faction;
        public Map Map;
        public string MapLoadId;
        public int SilverAmount;
        public RewardDemand Rewards;
        public RewardDemand LastPlayerDemand;
        public Pawn ReturnedHostage;
        public string ReturnedHostageLoadId;
        public int PlayerCompensationSilver;
        public ThingDef PlayerCompensationThingDef;
        public int PlayerCompensationThingCount;
        public bool CompensationCharged;
        public bool HostageReturned;
        public int NegotiationRound;
        public int NegotiationBudget;
        public int NegotiationDemandCost;
        public bool CareCreditApplied;
        public bool SilverRewardIssued;
        public bool SupplyRewardIssued;
        public bool GoodwillRewardIssued;
        public bool CeasefireRewardIssued;
        public bool IntelRewardIssued;
        public bool SpecialRewardIssued;
        public bool ReserveCharged;
        public int CreatedTick;
        public int OfferExpiresTick;
        public int FulfillmentExpiresTick;
        public int DeadlineExtensionCount;
        public int LastTreatmentTickAtExtension = -1;
        public int AcceptedTick = -1;
        public int ReleaseOrderedTick = -1;
        public int PrisonerDeliveredTick = -1;
        public int CompletedTick = -1;
        public DealState State;
        public bool VanillaReleaseConfirmed;
        public bool PrisonerDelivered;
        public bool RewardIssued;
        public bool FailureNotified;
        public DealOrigin Origin;
        public Pawn Negotiator;
        public int NegotiatorSocialSkill;
        public NegotiationOutcome NegotiationOutcome;
        public int NegotiationSeed;
        public FactionNegotiationType NegotiationType;
        public PirateDealRisk PirateRisk;
        public bool PirateRiskDisclosed;
        public int PaymentDueTick = -1;
        public int PirateRiskEventTick = -1;
        public int PirateRiskEventAttempts;
        public bool PirateRiskEventTriggered;
        public bool PirateRiskMitigated;

        public bool IsActive => State == DealState.Offered
            || State == DealState.Negotiating
            || State == DealState.AcceptedAwaitingRelease
            || State == DealState.ReleaseOrdered
            || State == DealState.FulfillmentPending;

        public void ExposeData()
        {
            Scribe_Values.Look(ref DealId, "dealId");
            Scribe_References.Look(ref Prisoner, "prisoner", true);
            Scribe_Values.Look(ref PrisonerLoadId, "prisonerLoadId");
            Scribe_References.Look(ref Faction, "faction");
            Scribe_References.Look(ref Map, "map");
            Scribe_Values.Look(ref MapLoadId, "mapLoadId");
            Scribe_Values.Look(ref SilverAmount, "silverAmount");
            Scribe_Deep.Look(ref Rewards, "rewards");
            Scribe_Deep.Look(ref LastPlayerDemand, "lastPlayerDemand");
            Scribe_References.Look(ref ReturnedHostage, "returnedHostage", true);
            Scribe_Values.Look(ref ReturnedHostageLoadId, "returnedHostageLoadId");
            Scribe_Values.Look(ref PlayerCompensationSilver, "playerCompensationSilver");
            Scribe_Defs.Look(ref PlayerCompensationThingDef, "playerCompensationThingDef");
            Scribe_Values.Look(ref PlayerCompensationThingCount, "playerCompensationThingCount");
            Scribe_Values.Look(ref CompensationCharged, "compensationCharged");
            Scribe_Values.Look(ref HostageReturned, "hostageReturned");
            Scribe_Values.Look(ref NegotiationRound, "negotiationRound");
            Scribe_Values.Look(ref NegotiationBudget, "negotiationBudget");
            Scribe_Values.Look(ref NegotiationDemandCost, "negotiationDemandCost");
            Scribe_Values.Look(ref CareCreditApplied, "careCreditApplied");
            Scribe_Values.Look(ref SilverRewardIssued, "silverRewardIssued");
            Scribe_Values.Look(ref SupplyRewardIssued, "supplyRewardIssued");
            Scribe_Values.Look(ref GoodwillRewardIssued, "goodwillRewardIssued");
            Scribe_Values.Look(ref CeasefireRewardIssued, "ceasefireRewardIssued");
            Scribe_Values.Look(ref IntelRewardIssued, "intelRewardIssued");
            Scribe_Values.Look(ref SpecialRewardIssued, "specialRewardIssued");
            Scribe_Values.Look(ref ReserveCharged, "reserveCharged");
            Scribe_Values.Look(ref CreatedTick, "createdTick");
            Scribe_Values.Look(ref OfferExpiresTick, "offerExpiresTick");
            Scribe_Values.Look(ref FulfillmentExpiresTick, "fulfillmentExpiresTick");
            Scribe_Values.Look(ref DeadlineExtensionCount, "deadlineExtensionCount");
            Scribe_Values.Look(ref LastTreatmentTickAtExtension, "lastTreatmentTickAtExtension", -1);
            Scribe_Values.Look(ref AcceptedTick, "acceptedTick", -1);
            Scribe_Values.Look(ref ReleaseOrderedTick, "releaseOrderedTick", -1);
            Scribe_Values.Look(ref PrisonerDeliveredTick, "prisonerDeliveredTick", -1);
            Scribe_Values.Look(ref CompletedTick, "completedTick", -1);
            Scribe_Values.Look(ref State, "state", DealState.Offered);
            Scribe_Values.Look(ref VanillaReleaseConfirmed, "vanillaReleaseConfirmed");
            Scribe_Values.Look(ref PrisonerDelivered, "prisonerDelivered");
            Scribe_Values.Look(ref RewardIssued, "rewardIssued");
            Scribe_Values.Look(ref FailureNotified, "failureNotified");
            Scribe_Values.Look(ref Origin, "origin", DealOrigin.FactionOffer);
            Scribe_References.Look(ref Negotiator, "negotiator", true);
            Scribe_Values.Look(ref NegotiatorSocialSkill, "negotiatorSocialSkill");
            Scribe_Values.Look(ref NegotiationOutcome, "negotiationOutcome", NegotiationOutcome.Accepted);
            Scribe_Values.Look(ref NegotiationSeed, "negotiationSeed");
            Scribe_Values.Look(ref NegotiationType, "negotiationType", FactionNegotiationType.Diplomatic);
            Scribe_Values.Look(ref PirateRisk, "pirateRisk", PirateDealRisk.None);
            Scribe_Values.Look(ref PirateRiskDisclosed, "pirateRiskDisclosed");
            Scribe_Values.Look(ref PaymentDueTick, "paymentDueTick", -1);
            Scribe_Values.Look(ref PirateRiskEventTick, "pirateRiskEventTick", -1);
            Scribe_Values.Look(ref PirateRiskEventAttempts, "pirateRiskEventAttempts");
            Scribe_Values.Look(ref PirateRiskEventTriggered, "pirateRiskEventTriggered");
            Scribe_Values.Look(ref PirateRiskMitigated, "pirateRiskMitigated");
        }
    }
}
