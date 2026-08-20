using RimWorld;
using Verse;

namespace PrisonerDiplomacy
{
    public sealed class PrisonerDealHistoryEntry : IExposable
    {
        public string DealId;
        public Faction Faction;
        public string FactionLoadId;
        public string FactionLabel;
        public string PrisonerLoadId;
        public string PrisonerLabel;
        public string ReturnedHostageLabel;
        public RewardDemand Rewards;
        public int PlayerCompensationSilver;
        public ThingDef PlayerCompensationThingDef;
        public int PlayerCompensationThingCount;
        public DealState State;
        public DealOrigin Origin;
        public FactionNegotiationType NegotiationType;
        public int CreatedTick;
        public int CompletedTick;
        public int NegotiatorSocialSkill;

        public static PrisonerDealHistoryEntry Create(PrisonerDeal deal)
        {
            if (deal == null || string.IsNullOrEmpty(deal.DealId))
            {
                return null;
            }

            return new PrisonerDealHistoryEntry
            {
                DealId = deal.DealId,
                Faction = deal.Faction,
                FactionLoadId = deal.Faction?.GetUniqueLoadID(),
                FactionLabel = deal.Faction?.Name ?? "?",
                PrisonerLoadId = deal.PrisonerLoadId,
                PrisonerLabel = deal.Prisoner?.LabelShortCap ?? deal.PrisonerLoadId ?? "?",
                ReturnedHostageLabel = deal.ReturnedHostage?.LabelShortCap,
                Rewards = deal.Rewards?.Clone(),
                PlayerCompensationSilver = deal.PlayerCompensationSilver,
                PlayerCompensationThingDef = deal.PlayerCompensationThingDef,
                PlayerCompensationThingCount = deal.PlayerCompensationThingCount,
                State = deal.State,
                Origin = deal.Origin,
                NegotiationType = deal.NegotiationType,
                CreatedTick = deal.CreatedTick,
                CompletedTick = deal.CompletedTick,
                NegotiatorSocialSkill = deal.NegotiatorSocialSkill
            };
        }

        public string RewardsDescription()
        {
            if (!string.IsNullOrEmpty(ReturnedHostageLabel))
            {
                if (PlayerCompensationThingDef != null && PlayerCompensationThingCount > 0)
                {
                    return "PD_HistoryExchangeSupplies".Translate(
                        ReturnedHostageLabel,
                        PlayerCompensationThingCount,
                        PlayerCompensationThingDef.LabelCap);
                }

                return "PD_HistoryExchangeSilver".Translate(ReturnedHostageLabel, PlayerCompensationSilver);
            }

            return Rewards?.Description().ToString() ?? "PD_RewardNone".Translate().ToString();
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref DealId, "dealId");
            Scribe_References.Look(ref Faction, "faction");
            Scribe_Values.Look(ref FactionLoadId, "factionLoadId");
            Scribe_Values.Look(ref FactionLabel, "factionLabel");
            Scribe_Values.Look(ref PrisonerLoadId, "prisonerLoadId");
            Scribe_Values.Look(ref PrisonerLabel, "prisonerLabel");
            Scribe_Values.Look(ref ReturnedHostageLabel, "returnedHostageLabel");
            Scribe_Deep.Look(ref Rewards, "rewards");
            Scribe_Values.Look(ref PlayerCompensationSilver, "playerCompensationSilver");
            Scribe_Defs.Look(ref PlayerCompensationThingDef, "playerCompensationThingDef");
            Scribe_Values.Look(ref PlayerCompensationThingCount, "playerCompensationThingCount");
            Scribe_Values.Look(ref State, "state", DealState.Rejected);
            Scribe_Values.Look(ref Origin, "origin", DealOrigin.FactionOffer);
            Scribe_Values.Look(ref NegotiationType, "negotiationType", FactionNegotiationType.Diplomatic);
            Scribe_Values.Look(ref CreatedTick, "createdTick");
            Scribe_Values.Look(ref CompletedTick, "completedTick", -1);
            Scribe_Values.Look(ref NegotiatorSocialSkill, "negotiatorSocialSkill");
        }
    }
}
