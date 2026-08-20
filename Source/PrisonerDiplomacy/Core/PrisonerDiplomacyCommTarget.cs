using System;
using RimWorld;
using Verse;

namespace PrisonerDiplomacy
{
    public sealed class PrisonerDiplomacyCommTarget : ICommunicable, IExposable, ILoadReferenceable
    {
        public Faction Faction;
        private string missingFactionLoadId;

        public PrisonerDiplomacyCommTarget()
        {
        }

        public PrisonerDiplomacyCommTarget(Faction faction)
        {
            Faction = faction;
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref Faction, "faction");
            Scribe_Values.Look(ref missingFactionLoadId, "missingFactionLoadId");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && Faction == null)
            {
                EnsureMissingFactionLoadId();
            }
        }

        public string GetUniqueLoadID()
        {
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
            return "PD_CommTargetLabel".Translate(Faction?.Name ?? "?");
        }

        public string GetInfoText()
        {
            return "PD_CommTargetInfo".Translate(Faction?.Name ?? "?");
        }

        public Faction GetFaction()
        {
            return Faction;
        }

        public FloatMenuOption CommFloatMenuOption(Building_CommsConsole console, Pawn negotiator)
        {
            string label = GetCallLabel();
            if (Faction == null || console == null || negotiator == null)
            {
                return new FloatMenuOption(label + " (" + "PD_NegotiationUnavailable".Translate() + ")", null);
            }

            FloatMenuOption option = new FloatMenuOption(
                label,
                () => console.GiveUseCommsJob(negotiator, this),
                Faction.def?.FactionIcon,
                Faction.Color,
                MenuOptionPriority.InitiateSocial);
            return FloatMenuUtility.DecoratePrioritizedTask(option, negotiator, console, "ReservedBy");
        }

        public void TryOpenComms(Pawn negotiator)
        {
            Map map = negotiator?.MapHeld;
            if (Faction == null || map == null)
            {
                Messages.Message("PD_NegotiationUnavailable".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            Find.WindowStack.Add(new Window_PrisonerNegotiation(Faction, negotiator, map));
        }
    }
}
