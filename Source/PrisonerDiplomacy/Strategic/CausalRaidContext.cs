using System;
using RimWorld;

namespace PrisonerDiplomacy
{
    internal static class CausalRaidContext
    {
        private static Faction activeFaction;

        internal static bool Active => activeFaction != null;

        internal static Faction Faction => activeFaction;

        internal static Scope Enter(Faction faction)
        {
            Scope scope = new Scope(activeFaction);
            activeFaction = faction;
            return scope;
        }

        internal struct Scope : IDisposable
        {
            private readonly Faction previousFaction;

            internal Scope(Faction previousFaction)
            {
                this.previousFaction = previousFaction;
            }

            public void Dispose()
            {
                activeFaction = previousFaction;
            }
        }
    }
}
