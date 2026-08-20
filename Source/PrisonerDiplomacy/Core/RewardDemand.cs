using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace PrisonerDiplomacy
{
    public sealed class RewardDemand : IExposable
    {
        public int Silver;
        public ThingDef SupplyDef;
        public int SupplyCount;
        public int Goodwill;
        public int CeasefireDays;
        public bool EarlyWarningIntel;
        public string SpecialRewardId;
        public ThingDef SpecialRewardThingDef;
        public int SpecialRewardCount;

        public int RewardTypeCount => (Silver > 0 ? 1 : 0)
            + (SupplyDef != null && SupplyCount > 0 ? 1 : 0)
            + (Goodwill > 0 ? 1 : 0)
            + (CeasefireDays > 0 ? 1 : 0)
            + (EarlyWarningIntel ? 1 : 0)
            + (!string.IsNullOrWhiteSpace(SpecialRewardId)
                && SpecialRewardThingDef != null
                && SpecialRewardCount > 0 ? 1 : 0);

        public bool IsEmpty => RewardTypeCount == 0;

        public RewardDemand Clone()
        {
            return new RewardDemand
            {
                Silver = Silver,
                SupplyDef = SupplyDef,
                SupplyCount = SupplyCount,
                Goodwill = Goodwill,
                CeasefireDays = CeasefireDays,
                EarlyWarningIntel = EarlyWarningIntel,
                SpecialRewardId = SpecialRewardId,
                SpecialRewardThingDef = SpecialRewardThingDef,
                SpecialRewardCount = SpecialRewardCount
            };
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref Silver, "silver");
            Scribe_Defs.Look(ref SupplyDef, "supplyDef");
            Scribe_Values.Look(ref SupplyCount, "supplyCount");
            Scribe_Values.Look(ref Goodwill, "goodwill");
            Scribe_Values.Look(ref CeasefireDays, "ceasefireDays");
            Scribe_Values.Look(ref EarlyWarningIntel, "earlyWarningIntel");
            Scribe_Values.Look(ref SpecialRewardId, "specialRewardId");
            Scribe_Defs.Look(ref SpecialRewardThingDef, "specialRewardThingDef");
            Scribe_Values.Look(ref SpecialRewardCount, "specialRewardCount");
        }

        public TaggedString Description()
        {
            List<string> parts = new List<string>();
            if (Silver > 0)
            {
                parts.Add("PD_RewardSilver".Translate(Silver));
            }

            if (SupplyDef != null && SupplyCount > 0)
            {
                parts.Add("PD_RewardSupplies".Translate(SupplyCount, SupplyDef.LabelCap));
            }

            if (Goodwill > 0)
            {
                parts.Add("PD_RewardGoodwill".Translate(Goodwill));
            }

            if (CeasefireDays > 0)
            {
                parts.Add("PD_RewardCeasefire".Translate(CeasefireDays));
            }

            if (EarlyWarningIntel)
            {
                parts.Add("PD_RewardEarlyWarningIntel".Translate());
            }

            if (!string.IsNullOrWhiteSpace(SpecialRewardId)
                && SpecialRewardThingDef != null
                && SpecialRewardCount > 0)
            {
                parts.Add("PD_RewardSpecial".Translate(
                    SpecialRewardCount,
                    PrisonerDiplomacySpecialRewardUtility.Label(this)));
            }

            if (parts.Count == 0)
            {
                return "PD_RewardNone".Translate();
            }

            return string.Join(" + ", parts);
        }
    }
}
