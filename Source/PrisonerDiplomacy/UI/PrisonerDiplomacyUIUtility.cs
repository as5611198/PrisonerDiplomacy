using System;
using RimWorld;
using Verse;

namespace PrisonerDiplomacy
{
    public static class PrisonerDiplomacyUIUtility
    {
        public static TaggedString BuildPrisonerStatus(PrisonerRecord record, PrisonerDeal deal)
        {
            if (record?.Pawn == null)
            {
                return "PD_NegotiationUnavailable".Translate();
            }

            string state;
            if (deal == null)
            {
                state = "PD_StatusNoDeal".Translate();
            }
            else
            {
                state = DealStateLabel(deal.State);
                if (deal.ReturnedHostage != null)
                {
                    state = "PD_StatusExchange".Translate(state, deal.ReturnedHostage.LabelShortCap);
                }
            }

            string releaseNote = string.Empty;
            if (deal?.State == DealState.ReleaseOrdered && record.Pawn.Downed)
            {
                releaseNote = "\n\n" + "PD_StatusDownedRelease".Translate();
            }

            string factionTypeNote = "\n" + "PD_StatusFactionType".Translate(
                FactionNegotiationUtility.TypeLabel(record.OriginalFaction));
            string riskNote = string.Empty;
            if (deal?.PirateRisk != null && deal.PirateRisk != PirateDealRisk.None)
            {
                riskNote = deal.PirateRisk == PirateDealRisk.DelayedPayment
                    && deal.State == DealState.FulfillmentPending
                    && deal.PaymentDueTick > Find.TickManager.TicksGame
                    ? "\n" + "PD_StatusPiratePaymentPending".Translate(
                        (deal.PaymentDueTick - Find.TickManager.TicksGame).ToStringTicksToPeriod())
                    : "\n" + "PD_StatusPirateRisk".Translate(
                        FactionNegotiationUtility.RiskDescription(deal.PirateRisk));
            }

            string contactNote = string.Empty;
            if (deal == null)
            {
                int remainingTicks = PrisonerDiplomacyGameComponent.Current?.GetEstimatedFactionContactTicks(record) ?? -1;
                if (remainingTicks > 0)
                {
                    contactNote = "\n" + "PD_StatusEstimatedContact".Translate(remainingTicks.ToStringTicksToPeriod());
                }
                else if (remainingTicks == 0)
                {
                    contactNote = "\n" + "PD_StatusContactImminent".Translate();
                }
            }

            return "PD_PrisonerStatusText".Translate(
                record.Pawn.LabelShortCap,
                record.OriginalFaction?.NameColored ?? "?",
                PrisonerValueCalculator.ImportanceLabel(record.Importance),
                record.DiplomaticValue,
                state) + factionTypeNote + contactNote + riskNote + releaseNote;
        }

        public static string DealStateLabel(DealState state)
        {
            switch (state)
            {
                case DealState.Offered: return "PD_StatusOffered".Translate();
                case DealState.Negotiating: return "PD_StatusNegotiating".Translate();
                case DealState.AcceptedAwaitingRelease: return "PD_StatusAccepted".Translate();
                case DealState.ReleaseOrdered: return "PD_StatusReleaseOrdered".Translate();
                case DealState.FulfillmentPending: return "PD_StatusPendingPayment".Translate();
                case DealState.Completed: return "PD_StatusCompleted".Translate();
                case DealState.Rejected: return "PD_StatusRejected".Translate();
                case DealState.Expired: return "PD_StatusExpired".Translate();
                default: return "PD_StatusCancelled".Translate();
            }
        }
    }
}
