using System.Collections.Generic;
using Verse;

namespace PrisonerDiplomacy
{
    public sealed class ChoiceLetter_PrisonerRansomOffer : ChoiceLetter
    {
        public string DealId;

        public void ApplyAiNarrative(string narrative, string formalOfferText)
        {
            Text = "PD_AiOfferLetterText".Translate(
                narrative ?? string.Empty,
                formalOfferText ?? string.Empty);
        }

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                PrisonerDiplomacyGameComponent component = PrisonerDiplomacyGameComponent.Current;
                PrisonerDeal deal = component?.GetDeal(DealId);
                if (deal == null || deal.State != DealState.Offered)
                {
                    DiaOption unavailable = new DiaOption("PD_OfferUnavailable".Translate());
                    unavailable.action = () => Find.LetterStack.RemoveLetter(this);
                    unavailable.resolveTree = true;
                    yield return unavailable;
                    yield break;
                }

                DiaOption accept = new DiaOption("PD_AcceptOffer".Translate());
                accept.action = () =>
                {
                    component.AcceptDeal(DealId);
                };
                accept.resolveTree = true;
                yield return accept;

                DiaOption reject = new DiaOption("PD_RejectOffer".Translate());
                reject.action = () =>
                {
                    component.RejectDeal(DealId);
                };
                reject.resolveTree = true;
                yield return reject;

                DiaOption muteFaction = new DiaOption("PD_RejectAndMuteFaction".Translate());
                muteFaction.action = () =>
                {
                    component.RejectAndMuteFaction(DealId);
                };
                muteFaction.resolveTree = true;
                yield return muteFaction;

                yield return Option_Postpone;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref DealId, "dealId");
        }
    }
}
