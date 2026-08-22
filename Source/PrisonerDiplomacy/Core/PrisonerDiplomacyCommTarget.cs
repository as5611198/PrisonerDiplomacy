using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace PrisonerDiplomacy
{
    public sealed class PrisonerDiplomacyCommTarget : ICommunicable, IExposable, ILoadReferenceable
    {
        public Faction Faction;
        private bool isHub;
        private string missingFactionLoadId;

        public bool IsHub => isHub;

        public PrisonerDiplomacyCommTarget()
        {
        }

        public PrisonerDiplomacyCommTarget(Faction faction)
        {
            Faction = faction;
        }

        internal static PrisonerDiplomacyCommTarget CreateHub()
        {
            return new PrisonerDiplomacyCommTarget { isHub = true };
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref Faction, "faction");
            Scribe_Values.Look(ref isHub, "isHub", false);
            Scribe_Values.Look(ref missingFactionLoadId, "missingFactionLoadId");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && Faction == null && !isHub)
            {
                EnsureMissingFactionLoadId();
            }
        }

        public string GetUniqueLoadID()
        {
            if (isHub)
            {
                return "PD_CommTarget_Hub";
            }
            if (Faction != null)
            {
                return "PD_CommTarget_" + Faction.GetUniqueLoadID();
            }

            EnsureMissingFactionLoadId();
            return "PD_CommTarget_MissingFaction_" + missingFactionLoadId;
        }

        private void EnsureMissingFactionLoadId()
        {
            if (string.IsNullOrEmpty(missingFactionLoadId))
            {
                missingFactionLoadId = Guid.NewGuid().ToString("N");
            }
        }

        public string GetCallLabel()
        {
            return isHub
                ? "PD_CommHubLabel".Translate()
                : "PD_CommTargetLabel".Translate(Faction?.Name ?? "?");
        }

        public string GetInfoText()
        {
            return isHub
                ? "PD_CommHubInfo".Translate()
                : "PD_CommTargetInfo".Translate(Faction?.Name ?? "?");
        }

        public Faction GetFaction()
        {
            return Faction;
        }

        public FloatMenuOption CommFloatMenuOption(Building_CommsConsole console, Pawn negotiator)
        {
            string label = GetCallLabel();
            if ((!isHub && Faction == null) || console == null || negotiator == null)
            {
                return new FloatMenuOption(label + " (" + "PD_NegotiationUnavailable".Translate() + ")", null);
            }

            FloatMenuOption option = new FloatMenuOption(
                label,
                () => console.GiveUseCommsJob(negotiator, this),
                isHub ? TexButton.IconBook : Faction.def?.FactionIcon,
                isHub ? PrisonerDiplomacyUiTheme.Accent : Faction.Color,
                MenuOptionPriority.InitiateSocial);
            return FloatMenuUtility.DecoratePrioritizedTask(option, negotiator, console, "ReservedBy");
        }

        public void TryOpenComms(Pawn negotiator)
        {
            Map map = negotiator?.MapHeld;
            if (map == null || (!isHub && Faction == null))
            {
                Messages.Message("PD_NegotiationUnavailable".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            if (isHub)
            {
                PrisonerDiplomacyGameComponent component = PrisonerDiplomacyGameComponent.Current;
                if (component == null || component.GetKnownNegotiationFactions(map).Count == 0)
                {
                    Messages.Message("PD_NegotiationUnavailable".Translate(), MessageTypeDefOf.RejectInput, false);
                    return;
                }

                Find.WindowStack.Add(new Window_PrisonerDiplomacyFactionBrowser(negotiator, map));
                return;
            }

            Find.WindowStack.Add(new Window_PrisonerNegotiation(Faction, negotiator, map));
        }
    }
}
