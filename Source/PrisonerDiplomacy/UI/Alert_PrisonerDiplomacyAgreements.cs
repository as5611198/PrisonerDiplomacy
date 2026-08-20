using System.Linq;
using RimWorld;
using Verse;

namespace PrisonerDiplomacy
{
    public sealed class Alert_PrisonerDiplomacyAgreements : Alert
    {
        public Alert_PrisonerDiplomacyAgreements()
        {
            defaultPriority = AlertPriority.Medium;
        }

        public override string GetLabel()
        {
            return "PD_UiAgreementAlertLabel".Translate();
        }

        public override TaggedString GetExplanation()
        {
            return "PD_UiAgreementAlertExplanation".Translate();
        }

        public override AlertReport GetReport()
        {
            return PrisonerDiplomacyGameComponent.Current?.HasActiveAgreementStatus == true
                ? AlertReport.Active
                : AlertReport.Inactive;
        }

        protected override void OnClick()
        {
            PrisonerDiplomacyGameComponent component = PrisonerDiplomacyGameComponent.Current;
            Faction faction = component?.GetFirstActiveAgreementFaction();
            Map map = component?.Deals
                .FirstOrDefault(deal => deal?.IsActive == true && deal.Faction == faction)?.Map
                ?? Find.CurrentMap;
            if (faction == null || map == null)
            {
                return;
            }

            Pawn negotiator = map.mapPawns?.FreeColonistsSpawned?.FirstOrDefault();
            Find.WindowStack.Add(new Window_PrisonerNegotiation(
                faction,
                negotiator,
                map,
                true,
                PrisonerDiplomacyWindowTab.Agreements));
        }
    }
}
