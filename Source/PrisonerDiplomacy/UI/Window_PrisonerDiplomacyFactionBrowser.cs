using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace PrisonerDiplomacy
{
    public sealed class Window_PrisonerDiplomacyFactionBrowser : Window
    {
        private readonly Pawn negotiator;
        private readonly Map map;
        private Vector2 scrollPosition;

        public override Vector2 InitialSize => new Vector2(
            Mathf.Min(760f, Math.Max(560f, UI.screenWidth - 80f)),
            Mathf.Min(620f, Math.Max(420f, UI.screenHeight - 100f)));

        public Window_PrisonerDiplomacyFactionBrowser(Pawn negotiator, Map map)
        {
            this.negotiator = negotiator;
            this.map = map;
            doCloseX = true;
            closeOnCancel = true;
            absorbInputAroundWindow = true;
            forcePause = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            PrisonerDiplomacyGameComponent component = PrisonerDiplomacyGameComponent.Current;
            List<Faction> factions = component?.GetKnownNegotiationFactions(map).ToList()
                ?? new List<Faction>();
            Widgets.DrawBoxSolid(inRect, PrisonerDiplomacyUiTheme.Canvas);
            PrisonerDiplomacyUiTheme.DrawPanel(new Rect(inRect.x, inRect.y, inRect.width, 58f), true);
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x + 14f, inRect.y + 10f, inRect.width - 28f, 28f),
                "PD_UiFactionContactsTitle".Translate());
            Text.Font = GameFont.Small;

            Rect viewport = new Rect(inRect.x + 8f, inRect.y + 68f, inRect.width - 16f,
                inRect.height - 116f);
            float rowHeight = 70f;
            float contentHeight = Math.Max(viewport.height, factions.Count * rowHeight);
            Rect view = new Rect(0f, 0f, viewport.width - 18f, contentHeight);
            Widgets.BeginScrollView(viewport, ref scrollPosition, view);
            if (factions.Count == 0)
            {
                GUI.color = PrisonerDiplomacyUiTheme.TextMuted;
                Widgets.Label(new Rect(12f, 18f, view.width - 24f, 32f),
                    "PD_UiFactionContactsEmpty".Translate());
                GUI.color = Color.white;
            }

            float y = 0f;
            foreach (Faction faction in factions)
            {
                DrawFactionRow(new Rect(0f, y, view.width, rowHeight - 6f), faction, component);
                y += rowHeight;
            }
            Widgets.EndScrollView();

            if (PrisonerDiplomacyUiTheme.DrawButton(
                new Rect(inRect.xMax - 112f, inRect.yMax - 40f, 112f, 34f),
                "CloseButton".Translate(),
                DiplomacyUiButtonStyle.Secondary))
            {
                Close();
            }
            PrisonerDiplomacyUiTheme.ResetText();
        }

        private void DrawFactionRow(Rect rect, Faction faction, PrisonerDiplomacyGameComponent component)
        {
            if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
            }
            PrisonerDiplomacyUiTheme.DrawPanel(rect, false);
            Texture2D icon = faction?.def?.FactionIcon;
            Rect iconRect = new Rect(rect.x + 10f, rect.y + 11f, 42f, 42f);
            if (icon != null)
            {
                GUI.color = faction.Color;
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit, true);
                GUI.color = Color.white;
            }
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(rect.x + 62f, rect.y + 8f, rect.width - 74f, 24f), faction.NameColored);
            int cases = component?.GetNegotiableRecords(faction, map).Count ?? 0;
            int history = component?.GetDealHistory(faction).Count ?? 0;
            Text.Font = GameFont.Tiny;
            GUI.color = PrisonerDiplomacyUiTheme.TextMuted;
            Widgets.Label(new Rect(rect.x + 62f, rect.y + 32f, rect.width - 74f, 26f),
                "PD_UiFactionContactSummary".Translate(
                    FactionNegotiationUtility.TypeLabel(faction), cases, history));
            GUI.color = Color.white;
            if (Widgets.ButtonInvisible(rect))
            {
                Find.WindowStack.Add(new Window_PrisonerNegotiation(faction, negotiator, map));
                Close();
            }
        }
    }
}
