using RimWorld;
using UnityEngine;
using Verse;

namespace PrisonerDiplomacyExampleAddon
{
    public sealed class Window_ExampleApiInspector : Window
    {
        private InspectorTab activeTab;
        private Vector2 scrollPosition;

        public override Vector2 InitialSize => new Vector2(920f, 700f);

        public Window_ExampleApiInspector()
        {
            doCloseX = true;
            doCloseButton = false;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = true;
            forcePause = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width - 180f, 34f),
                "PDX_InspectorTitle".Translate());
            Text.Font = GameFont.Small;

            Rect copyRect = new Rect(inRect.xMax - 170f, inRect.y, 170f, 30f);
            if (ExampleAddonUi.DrawButton(copyRect, "PDX_CopyReport".Translate(), true))
            {
                GUIUtility.systemCopyBuffer = ExampleAddonApiReport.BuildFull(Find.CurrentMap);
                Messages.Message("PDX_ReportCopied".Translate(), MessageTypeDefOf.TaskCompletion, false);
            }

            float tabsY = inRect.y + 42f;
            float tabWidth = (inRect.width - 18f) / 4f;
            DrawTab(new Rect(inRect.x, tabsY, tabWidth, 30f), InspectorTab.Overview,
                "PDX_TabOverview".Translate());
            DrawTab(new Rect(inRect.x + tabWidth + 6f, tabsY, tabWidth, 30f),
                InspectorTab.Prisoners, "PDX_TabPrisoners".Translate());
            DrawTab(new Rect(inRect.x + (tabWidth + 6f) * 2f, tabsY, tabWidth, 30f),
                InspectorTab.Factions, "PDX_TabFactions".Translate());
            DrawTab(new Rect(inRect.x + (tabWidth + 6f) * 3f, tabsY, tabWidth, 30f),
                InspectorTab.Events, "PDX_TabEvents".Translate());

            Rect viewport = new Rect(inRect.x, tabsY + 40f, inRect.width, inRect.height - tabsY - 40f);
            string report = ExampleAddonApiReport.Build(activeTab, Find.CurrentMap);
            float contentWidth = viewport.width - 20f;
            float contentHeight = Mathf.Max(viewport.height, Text.CalcHeight(report, contentWidth) + 24f);
            Rect viewRect = new Rect(0f, 0f, contentWidth, contentHeight);
            Widgets.DrawMenuSection(viewport);
            Widgets.BeginScrollView(viewport.ContractedBy(6f), ref scrollPosition, viewRect);
            Widgets.Label(new Rect(8f, 8f, contentWidth - 16f, contentHeight - 16f), report);
            Widgets.EndScrollView();
        }

        private void DrawTab(Rect rect, InspectorTab tab, string label)
        {
            Color oldColor = GUI.color;
            if (activeTab == tab)
            {
                GUI.color = new Color(0.42f, 0.95f, 0.90f);
            }
            if (ExampleAddonUi.DrawButton(rect, label, activeTab == tab))
            {
                activeTab = tab;
                scrollPosition = Vector2.zero;
            }
            GUI.color = oldColor;
        }
    }
}
