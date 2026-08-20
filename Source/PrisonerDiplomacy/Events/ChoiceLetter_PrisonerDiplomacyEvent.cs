using System.Collections.Generic;
using Verse;

namespace PrisonerDiplomacy
{
    public sealed class ChoiceLetter_PrisonerDiplomacyEvent : ChoiceLetter
    {
        public string EventId;

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                PrisonerDiplomacyGameComponent component = PrisonerDiplomacyGameComponent.Current;
                PrisonerDiplomacyEventRecord eventRecord = component?.GetDiplomacyEvent(EventId);
                if (eventRecord == null || !eventRecord.IsActive)
                {
                    DiaOption unavailable = new DiaOption("PD_EventUnavailable".Translate());
                    unavailable.action = () => Find.LetterStack.RemoveLetter(this);
                    unavailable.resolveTree = true;
                    yield return unavailable;
                    yield break;
                }

                DiaOption accept = new DiaOption("PD_EventAccept".Translate());
                accept.action = () =>
                {
                    component.AcceptDiplomacyEvent(EventId);
                    Find.LetterStack.RemoveLetter(this);
                };
                accept.resolveTree = true;
                yield return accept;

                DiaOption reject = new DiaOption("PD_EventReject".Translate());
                reject.action = () =>
                {
                    component.RejectDiplomacyEvent(EventId);
                    Find.LetterStack.RemoveLetter(this);
                };
                reject.resolveTree = true;
                yield return reject;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref EventId, "eventId");
        }
    }
}
