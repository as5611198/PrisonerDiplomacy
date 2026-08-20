using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RimWorld;
using UnityEngine;
using Verse;

namespace PrisonerDiplomacy
{
    public sealed class Window_PrisonerNegotiation : Window
    {
        private const float RowHeight = 82f;
        private const float RewardRowHeight = 30f;
        private const float RewardRowGap = 4f;
        private const float RewardHeadingHeight = 22f;
        private const float BaseRewardEditorHeight = 248f;
        private const float NegotiationNoteHeight = 76f;
        private static readonly Regex SilverInputRegex = new Regex("^[0-9]{0,5}$");
        private static readonly Regex CountInputRegex = new Regex("^[0-9]{0,3}$");
        private static readonly Regex GoodwillInputRegex = new Regex("^[0-9]{0,2}$");
        private static readonly Regex CeasefireInputRegex = new Regex("^[0-9]{0,2}$");
        private readonly Faction faction;
        private readonly Pawn negotiator;
        private readonly Map map;
        private readonly bool readOnly;
        private readonly string aiWindowContextId = Guid.NewGuid().ToString("N");
        private Vector2 scrollPosition;
        private Vector2 detailScrollPosition;
        private Vector2 historyScrollPosition;
        private Vector2 eventScrollPosition;
        private PrisonerRecord selectedRecord;
        private PrisonerDiplomacyWindowTab activeTab;
        private NegotiationMode negotiationMode;
        private Pawn selectedHostage;
        private ThingDef exchangeCompensationThingDef;
        private bool requestSilver = true;
        private bool requestSupplies;
        private bool requestGoodwill;
        private bool requestCeasefire;
        private bool requestEarlyWarningIntel;
        private bool requestSpecialReward;
        private string silverBuffer;
        private string supplyCountBuffer = "10";
        private string goodwillBuffer = "5";
        private string ceasefireBuffer = "10";
        private string negotiationNoteBuffer = string.Empty;
        private ThingDef selectedSupply;
        private PrisonerDiplomacySpecialRewardDefinition selectedSpecialReward;
        private AiNarrativeStatus? lastNarrativeStatus;
        private float narrativeTransitionStartedAt;
        public override Vector2 InitialSize
        {
            get
            {
                float width = Mathf.Min(1120f, Math.Max(680f, UI.screenWidth - 36f));
                float height = Mathf.Min(780f, Math.Max(520f, UI.screenHeight - 48f));
                return new Vector2(width, height);
            }
        }

        public Window_PrisonerNegotiation(Faction faction, Pawn negotiator, Map map)
            : this(faction, negotiator, map, false, PrisonerDiplomacyWindowTab.Cases)
        {
        }

        public Window_PrisonerNegotiation(
            Faction faction,
            Pawn negotiator,
            Map map,
            bool readOnly,
            PrisonerDiplomacyWindowTab initialTab)
        {
            this.faction = faction;
            this.negotiator = negotiator;
            this.map = map;
            this.readOnly = readOnly;
            activeTab = initialTab;
            doCloseX = true;
            closeOnCancel = true;
            absorbInputAroundWindow = true;
            forcePause = true;
            PrisonerDiplomacyGameComponent.Current?.RefreshNegotiationRecords();
            selectedSupply = SupplyRewardUtility.AvailableSupplies(faction).FirstOrDefault();
        }

        public override void DoWindowContents(Rect inRect)
        {
            DrawDiplomacyDashboard(inRect);
        }

        private void DrawDiplomacyDashboard(Rect inRect)
        {
            PrisonerDiplomacyGameComponent component = PrisonerDiplomacyGameComponent.Current;
            List<PrisonerRecord> records = component?.GetNegotiableRecords(faction, map).ToList()
                ?? new List<PrisonerRecord>();
            PrisonerDiplomacyUiTheme.ResetText();
            Widgets.DrawBoxSolid(inRect, PrisonerDiplomacyUiTheme.Canvas);

            const float headerHeight = 88f;
            const float actionHeight = 48f;
            Rect headerRect = new Rect(inRect.x, inRect.y, inRect.width, headerHeight);
            PrisonerDiplomacyUiTheme.DrawPanel(headerRect, true);
            PrisonerDiplomacyUiContext uiContext = CreateUiContext(component);
            float headerExtensionHeight = PrisonerDiplomacyUiExtensionRegistry.GetHeight(
                PrisonerDiplomacyUiRegion.FactionHeader, uiContext, Math.Max(120f, inRect.width - 24f));
            if (headerExtensionHeight > 0f)
            {
                headerRect.height += headerExtensionHeight;
                PrisonerDiplomacyUiTheme.DrawPanel(headerRect, true);
            }
            DrawDashboardHeader(headerRect, component, records.Count, uiContext, headerExtensionHeight);

            float bodyY = headerRect.yMax + PrisonerDiplomacyUiTheme.Gap;
            const float tabHeight = 30f;
            Rect tabRect = new Rect(inRect.x, bodyY, inRect.width, tabHeight);
            DrawTabBar(tabRect);
            bodyY = tabRect.yMax + PrisonerDiplomacyUiTheme.Gap;
            float bodyHeight = Math.Max(120f, inRect.yMax - bodyY - actionHeight - PrisonerDiplomacyUiTheme.Gap);
            if (activeTab == PrisonerDiplomacyWindowTab.Cases)
            {
                float listWidth = Mathf.Clamp(inRect.width * 0.30f, 230f, 330f);
                Rect listRect = new Rect(inRect.x, bodyY, listWidth, bodyHeight);
                Rect workspaceRect = new Rect(listRect.xMax + PrisonerDiplomacyUiTheme.Gap, bodyY,
                    inRect.xMax - listRect.xMax - PrisonerDiplomacyUiTheme.Gap, bodyHeight);
                DrawDashboardPrisonerList(listRect, records, component);
                DrawDashboardWorkspace(workspaceRect, component);
            }
            else if (activeTab == PrisonerDiplomacyWindowTab.Agreements)
            {
                DrawAgreementTab(new Rect(inRect.x, bodyY, inRect.width, bodyHeight), component);
            }
            else if (activeTab == PrisonerDiplomacyWindowTab.History)
            {
                DrawHistoryTab(new Rect(inRect.x, bodyY, inRect.width, bodyHeight), component);
            }
            else
            {
                DrawEventHistoryTab(new Rect(inRect.x, bodyY, inRect.width, bodyHeight), component);
            }

            Rect actionRect = new Rect(inRect.x, inRect.yMax - actionHeight, inRect.width, actionHeight);
            DrawActionBar(actionRect, component, records.Count);
            PrisonerDiplomacyUiTheme.ResetText();
        }

        private void DrawTabBar(Rect rect)
        {
            PrisonerDiplomacyUiTheme.DrawPanel(rect, true);
            float width = (rect.width - 14f) / 4f;
            DrawTabButton(new Rect(rect.x + 4f, rect.y + 3f, width, rect.height - 6f),
                "PD_UiCases".Translate().ToString(), PrisonerDiplomacyWindowTab.Cases);
            DrawTabButton(new Rect(rect.x + 4f + width + 2f, rect.y + 3f, width, rect.height - 6f),
                "PD_UiAgreementsTab".Translate().ToString(), PrisonerDiplomacyWindowTab.Agreements);
            DrawTabButton(new Rect(rect.x + 4f + (width + 2f) * 2f, rect.y + 3f, width, rect.height - 6f),
                "PD_UiHistoryTab".Translate().ToString(), PrisonerDiplomacyWindowTab.History);
            DrawTabButton(new Rect(rect.x + 4f + (width + 2f) * 3f, rect.y + 3f, width, rect.height - 6f),
                "PD_UiEventsTab".Translate().ToString(), PrisonerDiplomacyWindowTab.Events);
        }

        private void DrawTabButton(Rect rect, string label, PrisonerDiplomacyWindowTab tab)
        {
            bool selected = activeTab == tab;
            if (PrisonerDiplomacyUiTheme.DrawButton(
                rect,
                label,
                DiplomacyUiButtonStyle.Secondary,
                true,
                selected))
            {
                activeTab = tab;
                selectedRecord = null;
                detailScrollPosition = Vector2.zero;
                historyScrollPosition = Vector2.zero;
                eventScrollPosition = Vector2.zero;
            }
        }

        private void DrawDashboardHeader(
            Rect rect,
            PrisonerDiplomacyGameComponent component,
            int recordCount,
            PrisonerDiplomacyUiContext uiContext,
            float extensionHeight)
        {
            float metricWidth = Mathf.Clamp((rect.width - 340f) / 2f, 130f, 180f);
            float metricX = rect.xMax - metricWidth * 2f - 14f;
            float headingWidth = Math.Max(180f, metricX - rect.x - 24f);
            Rect identityRect = new Rect(rect.x + 10f, rect.y + 7f, headingWidth, 74f);
            float identityTextWidth = Math.Min(340f, Math.Max(120f, headingWidth - 54f));
            Texture2D factionIcon = faction?.def?.FactionIcon;
            Rect iconRect = new Rect(identityRect.x, identityRect.y + 8f, 42f, 42f);
            if (factionIcon != null)
            {
                GUI.color = faction?.Color ?? Color.white;
                GUI.DrawTexture(iconRect, factionIcon, ScaleMode.ScaleToFit, true);
                GUI.color = Color.white;
            }
            else
            {
                Widgets.DrawBoxSolid(iconRect, faction?.Color ?? PrisonerDiplomacyUiTheme.Border);
            }
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(identityRect.x + 54f, identityRect.y, identityTextWidth, 30f),
                "PD_NegotiationTitle".Translate(faction?.NameColored ?? "?"));
            Text.Font = GameFont.Small;
            GUI.color = PrisonerDiplomacyUiTheme.TextMuted;
            string negotiatorSummary = "PD_NegotiatorSummary".Translate(
                negotiator?.LabelShortCap ?? "?",
                PrisonerNegotiationUtility.GetSocialSkill(negotiator))
                + "  |  " + "PD_UiCases".Translate() + ": " + recordCount;
            Widgets.Label(new Rect(identityRect.x + 54f, identityRect.y + 34f, identityTextWidth, 22f), negotiatorSummary);
            GUI.color = Color.white;
            string factionTooltip = component?.GetFactionHistorySummary(faction) ?? string.Empty;
            if (!string.IsNullOrEmpty(factionTooltip))
            {
                TooltipHandler.TipRegion(
                    new Rect(identityRect.x, identityRect.y, identityTextWidth + 54f, identityRect.height),
                    factionTooltip);
            }

            float agreementX = identityRect.x + identityTextWidth + 66f;
            float agreementWidth = metricX - agreementX - 10f;
            if (agreementWidth >= 180f)
            {
                DrawHeaderAgreementSummary(
                    new Rect(agreementX, rect.y + 12f, agreementWidth, 64f),
                    component);
            }

            PrisonerDiplomacyUiTheme.DrawMetric(
                new Rect(metricX, rect.y + 17f, metricWidth - 6f, 54f),
                "PD_UiFactionType".Translate(), FactionNegotiationUtility.TypeLabel(faction),
                FactionNegotiationUtility.CanNegotiate(faction) ? DiplomacyUiTone.Accent : DiplomacyUiTone.Danger);
            PrisonerDiplomacyUiTheme.DrawMetric(
                new Rect(metricX + metricWidth, rect.y + 17f, metricWidth - 6f, 54f),
                "PD_UiSignal".Translate(),
                component?.GetFactionFinancialStatus(faction) ?? "?", DiplomacyUiTone.Neutral);
            if (extensionHeight > 0f)
            {
                PrisonerDiplomacyUiExtensionRegistry.Draw(
                    PrisonerDiplomacyUiRegion.FactionHeader,
                    new Rect(rect.x + 12f, rect.y + 88f, rect.width - 24f, extensionHeight),
                    uiContext);
            }
        }

        private void DrawHeaderAgreementSummary(Rect rect, PrisonerDiplomacyGameComponent component)
        {
            string strategicStatus = component?.GetFactionStrategicStatus(faction) ?? string.Empty;
            bool hasActiveAgreement = !string.IsNullOrEmpty(strategicStatus);
            PrisonerDiplomacyUiTheme.DrawNotice(
                rect,
                hasActiveAgreement ? DiplomacyUiTone.Positive : DiplomacyUiTone.Neutral);
            Text.Font = GameFont.Tiny;
            GUI.color = PrisonerDiplomacyUiTheme.TextMuted;
            Widgets.Label(new Rect(rect.x + 10f, rect.y + 5f, rect.width - 20f, 16f),
                "PD_UiAgreementSummary".Translate());
            GUI.color = hasActiveAgreement ? Color.white : PrisonerDiplomacyUiTheme.TextMuted;
            string summary = hasActiveAgreement
                ? CompactStrategicStatus(strategicStatus)
                : "PD_UiNoActiveAgreements".Translate().ToString();
            Widgets.Label(new Rect(rect.x + 10f, rect.y + 22f, rect.width - 20f, rect.height - 27f), summary);
            if (hasActiveAgreement)
            {
                TooltipHandler.TipRegion(rect, strategicStatus);
            }
            PrisonerDiplomacyUiTheme.ResetText();
        }

        private static string CompactStrategicStatus(string strategicStatus)
        {
            if (string.IsNullOrEmpty(strategicStatus))
            {
                return string.Empty;
            }

            int firstLineBreak = strategicStatus.IndexOf('\n');
            string details = firstLineBreak >= 0
                ? strategicStatus.Substring(firstLineBreak + 1)
                : strategicStatus;
            return details.Replace("\n", "  |  ");
        }

        private void DrawDashboardPrisonerList(
            Rect rect,
            List<PrisonerRecord> records,
            PrisonerDiplomacyGameComponent component)
        {
            PrisonerDiplomacyUiTheme.DrawPanel(rect);
            PrisonerDiplomacyUiTheme.DrawSectionHeading(
                new Rect(rect.x + 12f, rect.y + 9f, rect.width - 24f, 24f),
                "PD_UiPrisonerList".Translate(), records.Count.ToString());
            Rect viewport = new Rect(rect.x + 6f, rect.y + 38f, rect.width - 12f, rect.height - 44f);
            float contentHeight = Math.Max(viewport.height - 2f, records.Count * RowHeight);
            Rect view = new Rect(0f, 0f, viewport.width - 18f, contentHeight);
            Widgets.BeginScrollView(viewport, ref scrollPosition, view);
            float y = 0f;
            foreach (PrisonerRecord record in records)
            {
                DrawDashboardPrisonerRow(new Rect(0f, y, view.width, RowHeight - 5f), record, component);
                y += RowHeight;
            }
            if (records.Count == 0)
            {
                GUI.color = PrisonerDiplomacyUiTheme.TextMuted;
                Widgets.Label(new Rect(8f, 12f, view.width - 16f, 44f), "PD_UiNoCases".Translate());
                GUI.color = Color.white;
            }
            Widgets.EndScrollView();
        }

        private void DrawDashboardPrisonerRow(Rect rect, PrisonerRecord record, PrisonerDiplomacyGameComponent component)
        {
            PrisonerDeal activeDeal = component?.GetActiveDeal(record.Pawn);
            TaggedString reason = TaggedString.Empty;
            bool available = activeDeal?.State == DealState.Negotiating
                || component != null && component.CanStartPlayerNegotiation(record, out reason);
            if (activeDeal?.State == DealState.Negotiating)
            {
                reason = "PD_NegotiationCounterPending".Translate();
            }
            bool selected = selectedRecord == record;
            PrisonerDiplomacyUiTheme.DrawPanel(rect, false, selected);
            if (Mouse.IsOver(rect) && !selected)
            {
                Widgets.DrawHighlight(rect);
            }
            GUI.color = available ? Color.white : PrisonerDiplomacyUiTheme.TextMuted;
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(rect.x + 10f, rect.y + 6f, rect.width - 20f, 22f), record.Pawn.LabelShortCap);
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(rect.x + 10f, rect.y + 27f, rect.width - 108f, 17f),
                PrisonerValueCalculator.ImportanceLabel(record.Importance));
            GUI.color = PrisonerDiplomacyUiTheme.Accent;
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(new Rect(rect.x + rect.width - 100f, rect.y + 27f, 90f, 17f),
                "PD_ValueCompact".Translate(record.DiplomaticValue));
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            float health = record.Pawn.health?.summaryHealth?.SummaryHealthPercent ?? 0f;
            DiplomacyUiTone healthTone = health >= 0.65f
                ? DiplomacyUiTone.Positive
                : health >= 0.35f ? DiplomacyUiTone.Warning : DiplomacyUiTone.Danger;
            Text.Font = GameFont.Tiny;
            GUI.color = PrisonerDiplomacyUiTheme.TextMuted;
            Widgets.Label(new Rect(rect.x + 10f, rect.y + 45f, 112f, 16f),
                "PD_UiHealth".Translate(health.ToStringPercent()));
            GUI.color = Color.white;
            if (activeDeal != null)
            {
                string stateLabel = PrisonerDiplomacyUIUtility.DealStateLabel(activeDeal.State);
                Text.Font = GameFont.Tiny;
                float stateWidth = Mathf.Clamp(Text.CalcSize(stateLabel).x + 18f, 88f, rect.width - 20f);
                PrisonerDiplomacyUiTheme.DrawBadge(
                    new Rect(rect.xMax - stateWidth - 10f, rect.y + 44f, stateWidth, 18f),
                    stateLabel,
                    activeDeal.State == DealState.Negotiating ? DiplomacyUiTone.Warning : DiplomacyUiTone.Accent);
            }
            else
            {
                PrisonerDiplomacyUiTheme.DrawProgress(
                    new Rect(rect.x + 10f, rect.yMax - 8f, rect.width - 20f, 3f),
                    health,
                    healthTone);
            }
            if (Widgets.ButtonInvisible(rect))
            {
                SelectRecord(record, activeDeal);
            }
            if (!available || activeDeal?.State == DealState.Negotiating)
            {
                TooltipHandler.TipRegion(rect, reason);
            }
        }

        private void DrawDashboardWorkspace(Rect rect, PrisonerDiplomacyGameComponent component)
        {
            PrisonerDiplomacyUiTheme.DrawPanel(rect);
            PrisonerDiplomacyUiTheme.DrawSectionHeading(
                new Rect(rect.x + 12f, rect.y + 9f, rect.width - 24f, 24f),
                "PD_UiWorkspace".Translate(),
                selectedRecord?.Pawn?.LabelShortCap);
            Rect viewport = new Rect(rect.x + 6f, rect.y + 38f, rect.width - 12f, rect.height - 44f);
            // Reserve the scroll view's vertical scrollbar width up front so a
            // content-width rounding difference cannot create a horizontal bar.
            float contentWidth = Math.Max(160f, viewport.width - 18f);
            float detailHeight = GetDetailContentHeight(component, Math.Max(120f, contentWidth - 16f));
            PrisonerDiplomacyUiContext extensionContext = CreateUiContext(component);
            float prisonerSummaryHeight = PrisonerDiplomacyUiExtensionRegistry.GetHeight(
                PrisonerDiplomacyUiRegion.PrisonerSummary, extensionContext, contentWidth);
            float extensionHeight = PrisonerDiplomacyUiExtensionRegistry.GetHeight(
                PrisonerDiplomacyUiRegion.NegotiationBody, extensionContext, contentWidth);
            float contentHeight = detailHeight + prisonerSummaryHeight + extensionHeight + 20f;
            Rect view = new Rect(0f, 0f, contentWidth, Math.Max(viewport.height, contentHeight));
            Widgets.BeginScrollView(viewport, ref detailScrollPosition, view);
            float summaryY = 6f;
            if (prisonerSummaryHeight > 0f)
            {
                PrisonerDiplomacyUiExtensionRegistry.Draw(
                    PrisonerDiplomacyUiRegion.PrisonerSummary,
                    new Rect(8f, summaryY, contentWidth - 16f, prisonerSummaryHeight),
                    extensionContext);
                summaryY += prisonerSummaryHeight + 8f;
            }
            DrawDetails(new Rect(8f, summaryY, contentWidth - 16f, contentHeight - summaryY), component);
            if (extensionHeight > 0f)
            {
                PrisonerDiplomacyUiExtensionRegistry.Draw(
                    PrisonerDiplomacyUiRegion.NegotiationBody,
                    new Rect(8f, summaryY + detailHeight + 8f, contentWidth - 16f, extensionHeight),
                    extensionContext);
            }
            Widgets.EndScrollView();
        }

        private void DrawAgreementTab(Rect rect, PrisonerDiplomacyGameComponent component)
        {
            PrisonerDiplomacyUiTheme.DrawPanel(rect);
            Rect content = rect.ContractedBy(12f);
            DrawFactionOverview(content, component);
        }

        private void DrawHistoryTab(Rect rect, PrisonerDiplomacyGameComponent component)
        {
            PrisonerDiplomacyUiTheme.DrawPanel(rect);
            PrisonerDiplomacyUiTheme.DrawSectionHeading(
                new Rect(rect.x + 12f, rect.y + 9f, rect.width - 24f, 26f),
                "PD_UiHistoryTitle".Translate(),
                faction?.Name ?? "?");
            List<PrisonerDealHistoryEntry> entries = component?.GetDealHistory(faction).ToList()
                ?? new List<PrisonerDealHistoryEntry>();
            Rect viewport = new Rect(rect.x + 8f, rect.y + 42f, rect.width - 16f, rect.height - 50f);
            float rowHeight = 70f;
            float contentHeight = Math.Max(viewport.height, entries.Count * rowHeight);
            Rect view = new Rect(0f, 0f, viewport.width - 18f, contentHeight);
            Widgets.BeginScrollView(viewport, ref historyScrollPosition, view);
            if (entries.Count == 0)
            {
                GUI.color = PrisonerDiplomacyUiTheme.TextMuted;
                Widgets.Label(new Rect(12f, 16f, view.width - 24f, 32f),
                    "PD_UiHistoryEmpty".Translate());
                GUI.color = Color.white;
            }
            float y = 0f;
            foreach (PrisonerDealHistoryEntry entry in entries)
            {
                DrawHistoryRow(new Rect(0f, y, view.width, rowHeight - 6f), entry);
                y += rowHeight;
            }
            Widgets.EndScrollView();
        }

        private static void DrawHistoryRow(Rect rect, PrisonerDealHistoryEntry entry)
        {
            PrisonerDiplomacyUiTheme.DrawPanel(rect, false);
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(rect.x + 10f, rect.y + 7f, rect.width - 20f, 22f),
                entry?.PrisonerLabel ?? "?");
            Text.Font = GameFont.Tiny;
            string state = entry == null ? "?" : PrisonerDiplomacyUIUtility.DealStateLabel(entry.State);
            string origin = entry?.Origin == DealOrigin.FactionOffer
                ? "PD_HistoryFactionOffer".Translate().ToString()
                : "PD_HistoryPlayerDemand".Translate().ToString();
            string when = entry == null || entry.CompletedTick < 0
                ? "PD_HistoryUnknownDate".Translate().ToString()
                : "PD_HistoryAgo".Translate(Math.Max(0, Find.TickManager.TicksGame - entry.CompletedTick)
                    .ToStringTicksToPeriod()).ToString();
            GUI.color = PrisonerDiplomacyUiTheme.TextMuted;
            Widgets.Label(new Rect(rect.x + 10f, rect.y + 29f, rect.width - 20f, 16f),
                "PD_UiHistoryMeta".Translate(state, origin, when));
            GUI.color = PrisonerDiplomacyUiTheme.Accent;
            Widgets.Label(new Rect(rect.x + 10f, rect.y + 46f, rect.width - 20f, 18f),
                entry?.RewardsDescription() ?? "PD_RewardNone".Translate());
            PrisonerDiplomacyUiTheme.ResetText();
        }

        private void DrawEventHistoryTab(Rect rect, PrisonerDiplomacyGameComponent component)
        {
            PrisonerDiplomacyUiTheme.DrawPanel(rect);
            List<PrisonerDiplomacyEventRecord> events = component?.GetDiplomacyEvents()
                .Where(item => item?.Faction == faction)
                .OrderByDescending(item => item.CreatedTick)
                .ToList() ?? new List<PrisonerDiplomacyEventRecord>();
            PrisonerDiplomacyUiTheme.DrawSectionHeading(
                new Rect(rect.x + 12f, rect.y + 9f, rect.width - 24f, 26f),
                "PD_UiEventsTitle".Translate(),
                events.Count.ToString());

            Rect viewport = new Rect(rect.x + 8f, rect.y + 42f, rect.width - 16f, rect.height - 50f);
            const float rowHeight = 82f;
            float contentHeight = Math.Max(viewport.height, events.Count * rowHeight);
            Rect view = new Rect(0f, 0f, viewport.width - 18f, contentHeight);
            Widgets.BeginScrollView(viewport, ref eventScrollPosition, view);
            if (events.Count == 0)
            {
                GUI.color = PrisonerDiplomacyUiTheme.TextMuted;
                Widgets.Label(new Rect(12f, 16f, view.width - 24f, 32f),
                    "PD_UiEventsEmpty".Translate());
                GUI.color = Color.white;
            }

            float y = 0f;
            foreach (PrisonerDiplomacyEventRecord eventRecord in events)
            {
                DrawEventHistoryRow(new Rect(0f, y, view.width, rowHeight - 6f), eventRecord);
                y += rowHeight;
            }
            Widgets.EndScrollView();
        }

        private static void DrawEventHistoryRow(Rect rect, PrisonerDiplomacyEventRecord eventRecord)
        {
            PrisonerDiplomacyUiTheme.DrawPanel(rect, false);
            PrisonerDiplomacyEventDefinition definition =
                PrisonerDiplomacyExtensionRegistry.RegisteredEventDefinitions
                    .FirstOrDefault(item => item.EventId == eventRecord.DefinitionId);
            string label = definition == null || string.IsNullOrWhiteSpace(definition.LabelKey)
                ? eventRecord.Kind.ToString()
                : definition.LabelKey.Translate().ToString();
            string state = EventStateLabel(eventRecord.State);
            DiplomacyUiTone tone = EventStateTone(eventRecord.State);

            Text.Font = GameFont.Small;
            GUI.color = PrisonerDiplomacyUiTheme.Tone(tone);
            Widgets.Label(new Rect(rect.x + 10f, rect.y + 7f, rect.width - 170f, 22f), label);
            Text.Font = GameFont.Tiny;
            PrisonerDiplomacyUiTheme.DrawBadge(
                new Rect(rect.xMax - 150f, rect.y + 7f, 140f, 20f),
                state,
                tone);

            int now = Find.TickManager?.TicksGame ?? 0;
            string when = eventRecord.IsActive && eventRecord.TriggerTick > now
                ? "PD_UiEventDueIn".Translate((eventRecord.TriggerTick - now).ToStringTicksToPeriod())
                : "PD_HistoryAgo".Translate(Math.Max(0, now - eventRecord.CreatedTick).ToStringTicksToPeriod());
            GUI.color = PrisonerDiplomacyUiTheme.TextMuted;
            Widgets.Label(new Rect(rect.x + 10f, rect.y + 31f, rect.width - 20f, 18f),
                "PD_UiEventMeta".Translate(
                    eventRecord.PrisonerLabel ?? "?",
                    eventRecord.SourceDealId ?? "-",
                    when));
            Widgets.Label(new Rect(rect.x + 10f, rect.y + 51f, rect.width - 20f, 18f),
                "PD_UiEventTechnical".Translate(
                    eventRecord.Stage,
                    eventRecord.Attempts,
                    eventRecord.IntermediaryFaction?.Name ?? "-"));
            PrisonerDiplomacyUiTheme.ResetText();
        }

        private static string EventStateLabel(PrisonerDiplomacyEventState state)
        {
            return ("PD_UiEventState" + state).Translate();
        }

        private static DiplomacyUiTone EventStateTone(PrisonerDiplomacyEventState state)
        {
            switch (state)
            {
                case PrisonerDiplomacyEventState.Completed: return DiplomacyUiTone.Positive;
                case PrisonerDiplomacyEventState.Offered:
                case PrisonerDiplomacyEventState.Active: return DiplomacyUiTone.Accent;
                case PrisonerDiplomacyEventState.Scheduled: return DiplomacyUiTone.Warning;
                case PrisonerDiplomacyEventState.Failed: return DiplomacyUiTone.Danger;
                default: return DiplomacyUiTone.Neutral;
            }
        }

        private PrisonerDiplomacyUiContext CreateUiContext(PrisonerDiplomacyGameComponent component)
        {
            PrisonerDiplomacyUiContext context = new PrisonerDiplomacyUiContext
            {
                CompactLayout = InitialSize.x < 900f
            };
            if (component == null)
            {
                return context;
            }
            context.Faction = PrisonerDiplomacyFactionSnapshot.Create(component, faction, map);
            if (selectedRecord != null)
            {
                context.Prisoner = PrisonerDiplomacyPrisonerSnapshot.Create(component, selectedRecord, map);
                PrisonerDeal deal = component.GetActiveDeal(selectedRecord.Pawn);
                context.Deal = PrisonerDiplomacyDealSnapshot.Create(deal, Find.TickManager?.TicksGame ?? 0);
            }
            return context;
        }

        private void DrawActionBar(Rect rect, PrisonerDiplomacyGameComponent component, int recordCount)
        {
            PrisonerDiplomacyUiTheme.DrawPanel(rect, true);
            PrisonerDeal activeDeal = selectedRecord == null ? null : component?.GetActiveDeal(selectedRecord.Pawn);
            string status = selectedRecord == null
                ? (recordCount > 0
                    ? "PD_SelectPrisoner".Translate().ToString()
                    : "PD_UiAgreementReviewStatus".Translate().ToString())
                : PrisonerDiplomacyUIUtility.BuildPrisonerStatus(
                    selectedRecord, activeDeal).ToString().Split('\n')[0];
            const float closeWidth = 104f;
            const float buttonGap = 6f;
            List<string> actionLabels = new List<string>();
            List<bool> actionEnabled = new List<bool>();
            List<Action> actionCallbacks = new List<Action>();
            List<DiplomacyUiButtonStyle> actionStyles = new List<DiplomacyUiButtonStyle>();
            if (!readOnly && selectedRecord != null && component != null)
            {
                TaggedString unavailableReason = TaggedString.Empty;
                bool available = activeDeal?.State == DealState.Negotiating
                    || component.CanStartPlayerNegotiation(selectedRecord, out unavailableReason);
                if (activeDeal?.State == DealState.Negotiating)
                {
                    actionLabels.Add("PD_RejectCounterOffer".Translate());
                    actionEnabled.Add(true);
                    actionCallbacks.Add(() =>
                    {
                        component.RejectCounterOffer(activeDeal);
                        Close();
                    });
                    actionStyles.Add(DiplomacyUiButtonStyle.Danger);

                    if (activeDeal.NegotiationRound < 2)
                    {
                        RewardDemand revisedDemand = CreateRewardDemand(out string revisedValidationKey);
                        actionLabels.Add("PD_SubmitRevisedDemand".Translate());
                        actionEnabled.Add(revisedValidationKey == null);
                        actionCallbacks.Add(() => TrySubmitCurrentDemand(component, activeDeal));
                        actionStyles.Add(DiplomacyUiButtonStyle.Primary);
                    }

                    actionLabels.Add("PD_AcceptCounterOffer".Translate());
                    actionEnabled.Add(true);
                    actionCallbacks.Add(() => TryAcceptCounterOffer(component, activeDeal));
                    actionStyles.Add(DiplomacyUiButtonStyle.Primary);
                }
                else if (available && negotiationMode == NegotiationMode.PrisonerExchange)
                {
                    List<Pawn> hostages = component.GetAvailableHostages(faction).ToList();
                    if (hostages.Count > 0)
                    {
                        actionLabels.Add(GetExchangeConfirmationLabel(hostages));
                        actionEnabled.Add(selectedHostage != null && hostages.Contains(selectedHostage));
                        actionCallbacks.Add(() => TrySubmitExchange(component));
                        actionStyles.Add(DiplomacyUiButtonStyle.Primary);
                    }
                }
                else if (available)
                {
                    CreateRewardDemand(out string validationKey);
                    actionLabels.Add(activeDeal == null
                        ? "PD_SubmitRewardDemand".Translate()
                        : "PD_SubmitRevisedDemand".Translate());
                    actionEnabled.Add(validationKey == null);
                    actionCallbacks.Add(() => TrySubmitCurrentDemand(component, activeDeal));
                    actionStyles.Add(DiplomacyUiButtonStyle.Primary);
                }
            }

            List<float> actionWidths = CalculateActionWidths(
                rect.width,
                closeWidth,
                buttonGap,
                actionLabels);
            float controlsWidth = closeWidth + buttonGap;
            for (int index = 0; index < actionWidths.Count; index++)
            {
                controlsWidth += actionWidths[index] + buttonGap;
            }
            GUI.color = PrisonerDiplomacyUiTheme.TextMuted;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(rect.x + 12f, rect.y + 5f, Math.Max(80f, rect.width - controlsWidth - 24f), 38f),
                "PD_UiStatus".Translate(status));
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            float buttonY = rect.y + 6f;
            float actionsX = rect.xMax - closeWidth - buttonGap;
            for (int index = actionLabels.Count - 1; index >= 0; index--)
            {
                actionsX -= actionWidths[index];
                Rect actionRect = new Rect(actionsX, buttonY, actionWidths[index], 36f);
                if (PrisonerDiplomacyUiTheme.DrawButton(
                    actionRect,
                    actionLabels[index],
                    actionStyles[index],
                    actionEnabled[index]))
                {
                    actionCallbacks[index]();
                    return;
                }
                if (Text.CalcSize(actionLabels[index]).x + 18f > actionRect.width)
                {
                    TooltipHandler.TipRegion(actionRect, actionLabels[index]);
                }
                actionsX -= buttonGap;
            }
            Rect closeRect = new Rect(rect.xMax - closeWidth, buttonY, closeWidth, 36f);
            if (PrisonerDiplomacyUiTheme.DrawButton(
                closeRect,
                "CloseButton".Translate(),
                DiplomacyUiButtonStyle.Secondary))
            {
                Close();
            }
        }

        private static List<float> CalculateActionWidths(
            float totalWidth,
            float closeWidth,
            float gap,
            List<string> labels)
        {
            List<float> widths = labels
                .Select(label => Mathf.Clamp(Text.CalcSize(label).x + 24f, 104f, 220f))
                .ToList();
            if (widths.Count == 0)
            {
                return widths;
            }

            const float minimumStatusWidth = 96f;
            float available = Math.Max(104f * widths.Count,
                totalWidth - closeWidth - gap * (widths.Count + 1) - minimumStatusWidth - 24f);
            float preferred = widths.Sum();
            if (preferred <= available)
            {
                return widths;
            }

            float scale = available / preferred;
            for (int index = 0; index < widths.Count; index++)
            {
                widths[index] = Math.Max(104f, widths[index] * scale);
            }
            return widths;
        }

        private bool TryAcceptCounterOffer(
            PrisonerDiplomacyGameComponent component,
            PrisonerDeal activeDeal)
        {
            if (component?.AcceptCounterOffer(activeDeal) != true)
            {
                return false;
            }

            detailScrollPosition = Vector2.zero;
            return true;
        }

        private bool TrySubmitCurrentDemand(PrisonerDiplomacyGameComponent component, PrisonerDeal activeDeal)
        {
            RewardDemand demand = CreateRewardDemand(out string validationKey);
            if (validationKey != null)
            {
                return false;
            }
            NegotiationResult result = activeDeal == null
                ? component.SubmitPlayerDemand(selectedRecord, negotiator, demand, aiWindowContextId, negotiationNoteBuffer)
                : component.RevisePlayerDemand(activeDeal, demand, aiWindowContextId, negotiationNoteBuffer);
            if (result == null)
            {
                return false;
            }
            if (result.Outcome == NegotiationOutcome.Countered)
            {
                PrisonerDeal updatedDeal = component.GetActiveDeal(selectedRecord.Pawn);
                ApplyDemandToEditor(updatedDeal?.Rewards ?? result.CounterOffer);
                detailScrollPosition = Vector2.zero;
            }
            else
            {
                // Keep the workspace open so the AI narrative can render its
                // waiting and generated/fallback states in the same window.
                detailScrollPosition = Vector2.zero;
            }
            return true;
        }

        private string GetExchangeConfirmationLabel(List<Pawn> hostages)
        {
            Pawn hostage = selectedHostage != null && hostages.Contains(selectedHostage)
                ? selectedHostage
                : hostages.FirstOrDefault();
            if (hostage == null)
            {
                return "PD_ExchangeConfirm".Translate("?");
            }
            return "PD_ExchangeConfirm".Translate(GetExchangeCompensationDescription(hostage));
        }

        private string GetExchangeCompensationDescription(Pawn hostage)
        {
            int compensation = PrisonerExchangeUtility.CalculateCompensation(selectedRecord, hostage);
            if (compensation <= 0)
            {
                return "0";
            }
            int supplyCount = exchangeCompensationThingDef == null
                ? 0
                : PrisonerExchangeUtility.CalculateSupplyCount(faction, exchangeCompensationThingDef, compensation);
            return exchangeCompensationThingDef == null
                ? "PD_ExchangeCompensationSilver".Translate(compensation).ToString()
                : "PD_ExchangeCompensationSupplies".Translate(supplyCount, exchangeCompensationThingDef.LabelCap).ToString();
        }

        private bool TrySubmitExchange(PrisonerDiplomacyGameComponent component)
        {
            List<Pawn> hostages = component.GetAvailableHostages(faction).ToList();
            if (selectedHostage == null || !hostages.Contains(selectedHostage))
            {
                selectedHostage = hostages.FirstOrDefault();
            }
            if (selectedHostage == null)
            {
                return false;
            }
            if (component.TryCreatePrisonerExchange(
                selectedRecord,
                negotiator,
                selectedHostage,
                exchangeCompensationThingDef,
                out string reasonKey))
            {
                Close();
                return true;
            }
            if (!string.IsNullOrEmpty(reasonKey))
            {
                Messages.Message(reasonKey.Translate(), MessageTypeDefOf.RejectInput, false);
            }
            return false;
        }

        // Kept as a compatibility reference while the dashboard owns the live layout.
        private void DoLegacyWindowContents(Rect inRect)
        {
            PrisonerDiplomacyGameComponent component = PrisonerDiplomacyGameComponent.Current;
            List<PrisonerRecord> records = component?.GetNegotiableRecords(faction, map).ToList() ?? new List<PrisonerRecord>();

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 36f), "PD_NegotiationTitle".Translate(faction?.NameColored ?? "?"));
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(inRect.x, inRect.y + 38f, inRect.width, 44f), "PD_NegotiatorSummary".Translate(
                negotiator?.LabelShortCap ?? "?",
                PrisonerNegotiationUtility.GetSocialSkill(negotiator)));

            Rect contentRect = new Rect(inRect.x, inRect.y + 88f, inRect.width, inRect.height - 136f);
            Rect listRect = new Rect(contentRect.x, contentRect.y, 340f, contentRect.height);
            Widgets.DrawMenuSection(listRect);
            Rect viewRect = new Rect(0f, 0f, listRect.width - 18f, Math.Max(listRect.height, records.Count * RowHeight));
            Widgets.BeginScrollView(listRect.ContractedBy(6f), ref scrollPosition, viewRect);
            float y = 0f;
            foreach (PrisonerRecord record in records)
            {
                DrawPrisonerRow(new Rect(0f, y, viewRect.width, RowHeight - 4f), record, component);
                y += RowHeight;
            }
            Widgets.EndScrollView();

            Rect detailRect = new Rect(listRect.xMax + 12f, listRect.y, inRect.xMax - listRect.xMax - 12f, listRect.height);
            Widgets.DrawMenuSection(detailRect);
            Rect detailOuter = detailRect.ContractedBy(8f);
            float detailContentWidth = Math.Max(100f, detailOuter.width - 18f);
            float detailContentHeight = GetDetailContentHeight(component, detailContentWidth);
            Rect detailView = new Rect(0f, 0f, detailContentWidth,
                Math.Max(detailOuter.height, detailContentHeight));
            Widgets.BeginScrollView(detailOuter, ref detailScrollPosition, detailView);
            DrawDetails(detailView, component);
            Widgets.EndScrollView();

            if (PrisonerDiplomacyUiTheme.DrawButton(
                new Rect(inRect.xMax - 150f, inRect.yMax - 40f, 150f, 40f),
                "CloseButton".Translate(),
                DiplomacyUiButtonStyle.Secondary))
            {
                Close();
            }
        }

        public override void PreClose()
        {
            PrisonerDiplomacyGameComponent.Current?.CancelAiNarrativesForWindow(aiWindowContextId);
            base.PreClose();
        }

        private void DrawPrisonerRow(Rect rect, PrisonerRecord record, PrisonerDiplomacyGameComponent component)
        {
            if (selectedRecord == record)
            {
                Widgets.DrawHighlightSelected(rect);
            }
            else if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
            }

            PrisonerDeal activeDeal = component.GetActiveDeal(record.Pawn);
            TaggedString reason;
            bool available = activeDeal?.State == DealState.Negotiating
                || component.CanStartPlayerNegotiation(record, out reason);
            if (activeDeal?.State == DealState.Negotiating)
            {
                reason = "PD_NegotiationCounterPending".Translate();
            }

            string line = record.Pawn.LabelShortCap + "\n" + PrisonerValueCalculator.ImportanceLabel(record.Importance)
                + " | " + "PD_ValueCompact".Translate(record.DiplomaticValue);
            GUI.color = available ? Color.white : Color.gray;
            Widgets.Label(rect.ContractedBy(8f), line);
            GUI.color = Color.white;
            if (Widgets.ButtonInvisible(rect))
            {
                SelectRecord(record, activeDeal);
            }

            if (!available || activeDeal?.State == DealState.Negotiating)
            {
                TooltipHandler.TipRegion(rect, reason);
            }
        }

        private void DrawDetails(Rect rect, PrisonerDiplomacyGameComponent component)
        {
            if (selectedRecord?.Pawn == null)
            {
                DrawWorkspaceEmptyState(rect, component);
                return;
            }

            PrisonerDeal activeDeal = component.GetActiveDeal(selectedRecord.Pawn);
            bool negotiating = activeDeal?.State == DealState.Negotiating;
            TaggedString unavailableReason = TaggedString.Empty;
            bool available = negotiating || component.CanStartPlayerNegotiation(selectedRecord, out unavailableReason);
            float y = rect.y;

            AiNarrativeRecord aiNarrative = activeDeal == null
                ? component.GetLatestAiNarrative(selectedRecord.Pawn)
                : component.GetLatestAiNarrative(activeDeal.DealId);
            if (PrisonerDiplomacyMod.Settings?.EnableAiNarratives == true && aiNarrative != null)
            {
                float aiPanelHeight = GetAiNarrativePanelHeight(rect.width, aiNarrative);
                DrawAiNarrativePanel(new Rect(rect.x, y, rect.width, aiPanelHeight), aiNarrative);
                y += aiPanelHeight + 8f;
            }

            if (activeDeal != null)
            {
                float dealProgressHeight = GetDealProgressHeight(rect.width, activeDeal);
                DrawDealProgress(new Rect(rect.x, y, rect.width, dealProgressHeight), activeDeal);
                y += dealProgressHeight + 8f;

                if (activeDeal.PirateRisk != PirateDealRisk.None)
                {
                    float pirateRiskHeight = GetPirateRiskHeight(rect.width, activeDeal);
                    DrawPirateRisk(new Rect(rect.x, y, rect.width, pirateRiskHeight), activeDeal);
                    y += pirateRiskHeight + 8f;
                }
            }

            if (negotiating)
            {
                float counterPanelHeight = GetCounterOfferPanelHeight(rect.width, activeDeal);
                DrawCounterOfferPanel(new Rect(rect.x, y, rect.width, counterPanelHeight), activeDeal);
                y += counterPanelHeight + 6f;

                if (activeDeal.NegotiationRound >= 2)
                {
                    TaggedString finalCounterText = "PD_FinalCounterOnly".Translate();
                    float finalCounterHeight = Math.Max(48f, Text.CalcHeight(finalCounterText, rect.width - 20f) + 16f);
                    Rect finalCounterRect = new Rect(rect.x, y, rect.width, finalCounterHeight);
                    PrisonerDiplomacyUiTheme.DrawNotice(finalCounterRect, DiplomacyUiTone.Danger);
                    Widgets.Label(finalCounterRect.ContractedBy(10f, 8f), finalCounterText);
                    return;
                }

                TaggedString revisionHint = "PD_CounterRevisionHint".Translate();
                float revisionHintHeight = Math.Max(24f, Text.CalcHeight(revisionHint, rect.width));
                Widgets.Label(new Rect(rect.x, y, rect.width, revisionHintHeight), revisionHint);
                y += revisionHintHeight + 4f;
                DrawCounterOfferShortcuts(new Rect(rect.x, y, rect.width, 32f), activeDeal.Rewards);
                y += 38f;
            }

            List<Pawn> hostages = component.GetAvailableHostages(faction).ToList();
            if (!negotiating && hostages.Count > 0)
            {
                DrawNegotiationModeSelector(new Rect(rect.x, y, rect.width, 34f));
                y += 42f;
                if (negotiationMode == NegotiationMode.PrisonerExchange)
                {
                    float exchangeHeight = GetExchangeEditorContentHeight(
                        rect.width,
                        hostages,
                        available,
                        unavailableReason);
                    DrawExchangeEditor(
                        new Rect(rect.x, y, rect.width, exchangeHeight),
                        hostages,
                        available,
                        unavailableReason);
                    return;
                }
            }

            float rewardEditorHeight = GetRewardEditorHeight();
            RewardDemand demand = DrawRewardEditor(
                new Rect(rect.x, y, rect.width, rewardEditorHeight),
                out string validationKey);
            y += rewardEditorHeight + 4f;
            if (AiNoteVisible())
            {
                DrawNegotiationNote(new Rect(rect.x, y, rect.width, NegotiationNoteHeight));
                y += NegotiationNoteHeight + 4f;
            }
            int round = negotiating ? activeDeal.NegotiationRound + 1 : 1;
            NegotiationResult preview = validationKey == null
                ? PrisonerNegotiationUtility.Evaluate(
                    selectedRecord,
                    negotiator,
                    demand,
                    component.GetAvailableReserve(faction),
                    NegotiationEconomyUtility.CalculateMaterialRewardCap(map),
                    round,
                    component.GetFactionMemoryMultiplier(faction, Find.TickManager.TicksGame),
                    component.GetNegotiationBudgetMultiplier(selectedRecord, activeDeal))
                : null;

            if (preview != null)
            {
                TaggedString assessment = "PD_AssessmentRewardsText".Translate(
                    PrisonerNegotiationUtility.AssessmentLabel(preview.Assessment),
                    PreviewPrecisionText(preview),
                    demand.Description(),
                    preview.MaterialRewardCap);
                float assessmentHeight = GetAssessmentPanelHeight(assessment, rect.width);
                Rect assessmentRect = new Rect(rect.x, y, rect.width, assessmentHeight);
                PrisonerDiplomacyUiTheme.DrawNotice(assessmentRect, AssessmentTone(preview.Assessment));
                Widgets.Label(assessmentRect.ContractedBy(10f, 8f), assessment);
                y += assessmentHeight + 4f;
            }
            else
            {
                TaggedString validation = (validationKey ?? "PD_NegotiationInvalidRewards").Translate();
                float validationHeight = Math.Max(64f, Text.CalcHeight(validation, rect.width - 20f) + 24f);
                Rect validationRect = new Rect(rect.x, y, rect.width, validationHeight);
                PrisonerDiplomacyUiTheme.DrawNotice(validationRect, DiplomacyUiTone.Danger);
                Widgets.Label(validationRect.ContractedBy(10f, 8f), validation);
                y += validationHeight + 4f;
            }

            if (!available)
            {
                float unavailableHeight = Math.Max(48f, Text.CalcHeight(unavailableReason, rect.width - 20f) + 16f);
                Rect unavailableRect = new Rect(rect.x, y, rect.width, unavailableHeight);
                PrisonerDiplomacyUiTheme.DrawNotice(unavailableRect, DiplomacyUiTone.Warning);
                Widgets.Label(unavailableRect.ContractedBy(10f, 8f), unavailableReason);
                return;
            }

        }

        private static void DrawWorkspaceEmptyState(
            Rect rect,
            PrisonerDiplomacyGameComponent component)
        {
            PrisonerDiplomacyUiTheme.DrawNotice(rect, DiplomacyUiTone.Neutral);
            Text.Font = GameFont.Small;
            GUI.color = PrisonerDiplomacyUiTheme.TextMuted;
            string message = component == null
                ? "PD_NegotiationUnavailable".Translate().ToString()
                : "PD_SelectPrisoner".Translate().ToString();
            Widgets.Label(
                new Rect(rect.x + 14f, rect.y + 22f, rect.width - 28f, rect.height - 36f),
                message);
            PrisonerDiplomacyUiTheme.ResetText();
        }

        private void DrawFactionOverview(Rect rect, PrisonerDiplomacyGameComponent component)
        {
            string strategicStatus = component?.GetFactionStrategicStatus(faction) ?? string.Empty;
            string agreementText = string.IsNullOrEmpty(strategicStatus)
                ? "PD_UiNoActiveAgreements".Translate().ToString()
                : CompactStrategicStatus(strategicStatus);
            float agreementHeight = GetOverviewPanelHeight(agreementText, rect.width);
            DrawOverviewPanel(
                new Rect(rect.x, rect.y, rect.width, agreementHeight),
                "PD_UiAgreementSummary".Translate(),
                agreementText,
                string.IsNullOrEmpty(strategicStatus) ? DiplomacyUiTone.Neutral : DiplomacyUiTone.Positive);
            if (!string.IsNullOrEmpty(strategicStatus))
            {
                TooltipHandler.TipRegion(
                    new Rect(rect.x, rect.y, rect.width, agreementHeight),
                    strategicStatus);
            }

            string standing = component?.GetFactionMemoryDescription(faction)
                ?? "PD_MemoryNeutral".Translate().ToString();
            float standingHeight = GetOverviewPanelHeight(standing, rect.width);
            DrawOverviewPanel(
                new Rect(rect.x, rect.y + agreementHeight + 10f, rect.width, standingHeight),
                "PD_UiFactionStanding".Translate(),
                standing,
                DiplomacyUiTone.Accent);
        }

        private static void DrawOverviewPanel(
            Rect rect,
            string heading,
            string body,
            DiplomacyUiTone tone)
        {
            PrisonerDiplomacyUiTheme.DrawNotice(rect, tone);
            PrisonerDiplomacyUiTheme.DrawSectionHeading(
                new Rect(rect.x + 10f, rect.y + 5f, rect.width - 20f, 18f),
                heading);
            Text.Font = GameFont.Small;
            GUI.color = tone == DiplomacyUiTone.Neutral
                ? PrisonerDiplomacyUiTheme.TextMuted
                : Color.white;
            Widgets.Label(new Rect(rect.x + 10f, rect.y + 27f, rect.width - 20f, rect.height - 34f), body);
            PrisonerDiplomacyUiTheme.ResetText();
        }

        private float GetFactionOverviewContentHeight(
            PrisonerDiplomacyGameComponent component,
            float width)
        {
            string strategicStatus = component?.GetFactionStrategicStatus(faction) ?? string.Empty;
            string agreementText = string.IsNullOrEmpty(strategicStatus)
                ? "PD_UiNoActiveAgreements".Translate().ToString()
                : CompactStrategicStatus(strategicStatus);
            string standing = component?.GetFactionMemoryDescription(faction)
                ?? "PD_MemoryNeutral".Translate().ToString();
            return GetOverviewPanelHeight(agreementText, width)
                + 10f
                + GetOverviewPanelHeight(standing, width)
                + 8f;
        }

        private static float GetOverviewPanelHeight(string text, float width)
        {
            return Math.Max(82f, Text.CalcHeight(text ?? string.Empty, Math.Max(120f, width - 20f)) + 42f);
        }

        private void DrawNegotiationModeSelector(Rect rect)
        {
            float width = (rect.width - 6f) / 2f;
            if (PrisonerDiplomacyUiTheme.DrawButton(
                new Rect(rect.x, rect.y, width, rect.height),
                "PD_ModeRansom".Translate(),
                DiplomacyUiButtonStyle.Secondary,
                true,
                negotiationMode == NegotiationMode.Ransom))
            {
                negotiationMode = NegotiationMode.Ransom;
                detailScrollPosition = Vector2.zero;
            }

            if (PrisonerDiplomacyUiTheme.DrawButton(
                new Rect(rect.x + width + 6f, rect.y, width, rect.height),
                "PD_ModeExchange".Translate(),
                DiplomacyUiButtonStyle.Secondary,
                true,
                negotiationMode == NegotiationMode.PrisonerExchange))
            {
                negotiationMode = NegotiationMode.PrisonerExchange;
                detailScrollPosition = Vector2.zero;
            }
        }

        private void DrawExchangeEditor(
            Rect rect,
            List<Pawn> hostages,
            bool available,
            TaggedString unavailableReason)
        {
            if (selectedHostage == null || !hostages.Contains(selectedHostage))
            {
                selectedHostage = hostages.FirstOrDefault();
            }

            float y = rect.y;
            Widgets.Label(new Rect(rect.x, y, rect.width, 24f), "PD_ExchangeHostageLabel".Translate());
            y += 28f;
            if (PrisonerDiplomacyUiTheme.DrawButton(
                new Rect(rect.x, y, rect.width, 34f),
                selectedHostage?.LabelShortCap ?? "PD_ExchangeSelectHostage".Translate(),
                DiplomacyUiButtonStyle.Secondary))
            {
                Find.WindowStack.Add(new FloatMenu(hostages.Select(hostage => new FloatMenuOption(
                    "PD_ExchangeHostageOption".Translate(
                        hostage.LabelShortCap,
                        PrisonerExchangeUtility.CalculateHostageCost(hostage)),
                    () =>
                    {
                        selectedHostage = hostage;
                        exchangeCompensationThingDef = null;
                    })).ToList()));
            }
            y += 42f;

            if (selectedHostage == null)
            {
                Widgets.Label(new Rect(rect.x, y, rect.width, 48f), "PD_ExchangeNoHostages".Translate());
                return;
            }

            int hostageCost = PrisonerExchangeUtility.CalculateHostageCost(selectedHostage);
            int compensation = PrisonerExchangeUtility.CalculateCompensation(selectedRecord, selectedHostage);
            if (compensation <= 0)
            {
                exchangeCompensationThingDef = null;
            }
            List<ThingDef> compensationSupplies = PrisonerExchangeUtility.AvailableCompensationSupplies(
                faction,
                compensation).ToList();
            if (exchangeCompensationThingDef != null && !compensationSupplies.Contains(exchangeCompensationThingDef))
            {
                exchangeCompensationThingDef = null;
            }
            if (compensation > 0)
            {
                Widgets.Label(new Rect(rect.x, y, 130f, 32f), "PD_ExchangePaymentMethod".Translate());
                string paymentLabel = exchangeCompensationThingDef == null
                    ? "PD_ExchangePaySilver".Translate()
                    : exchangeCompensationThingDef.LabelCap;
                if (PrisonerDiplomacyUiTheme.DrawButton(
                    new Rect(rect.x + 138f, y, rect.width - 138f, 32f),
                    paymentLabel,
                    DiplomacyUiButtonStyle.Secondary))
                {
                    List<FloatMenuOption> options = new List<FloatMenuOption>
                    {
                        new FloatMenuOption("PD_ExchangePaySilver".Translate(), () => exchangeCompensationThingDef = null)
                    };
                    options.AddRange(compensationSupplies.Select(def =>
                        new FloatMenuOption(def.LabelCap, () => exchangeCompensationThingDef = def)));
                    Find.WindowStack.Add(new FloatMenu(options));
                }
                y += 40f;
            }

            int compensationThingCount = exchangeCompensationThingDef == null
                ? 0
                : PrisonerExchangeUtility.CalculateSupplyCount(faction, exchangeCompensationThingDef, compensation);
            TaggedString compensationDescription = exchangeCompensationThingDef == null
                ? "PD_ExchangeCompensationSilver".Translate(compensation)
                : "PD_ExchangeCompensationSupplies".Translate(
                    compensationThingCount,
                    exchangeCompensationThingDef.LabelCap);
            float exchangeSummaryHeight = GetExchangeSummaryHeight(
                rect.width,
                selectedRecord.Pawn.LabelShortCap,
                selectedRecord.DiplomaticValue,
                selectedHostage.LabelShortCap,
                hostageCost,
                compensationDescription);
            DrawExchangeSummary(
                new Rect(rect.x, y, rect.width, exchangeSummaryHeight),
                selectedRecord.Pawn.LabelShortCap,
                selectedRecord.DiplomaticValue,
                selectedHostage.LabelShortCap,
                hostageCost,
                compensationDescription);
            y += exchangeSummaryHeight + 8f;

            if (!available)
            {
                float unavailableHeight = Math.Max(48f, Text.CalcHeight(unavailableReason, rect.width - 20f) + 16f);
                Rect unavailableRect = new Rect(rect.x, y, rect.width, unavailableHeight);
                PrisonerDiplomacyUiTheme.DrawNotice(unavailableRect, DiplomacyUiTone.Warning);
                Widgets.Label(unavailableRect.ContractedBy(10f, 8f), unavailableReason);
                return;
            }

        }

        private float GetDetailContentHeight(PrisonerDiplomacyGameComponent component, float width)
        {
            if (selectedRecord?.Pawn == null)
            {
                // The faction agreement is already represented in the header and
                // the Agreements tab. Keep the empty workspace compact so it
                // cannot introduce a scrollbar before a case is selected.
                return 96f;
            }

            PrisonerDeal activeDeal = component?.GetActiveDeal(selectedRecord.Pawn);
            float headerHeight = 0f;
            if (activeDeal != null)
            {
                headerHeight += GetDealProgressHeight(width, activeDeal) + 8f;
                if (activeDeal.PirateRisk != PirateDealRisk.None)
                {
                    headerHeight += GetPirateRiskHeight(width, activeDeal) + 8f;
                }
            }
            AiNarrativeRecord aiNarrative = activeDeal == null
                ? component.GetLatestAiNarrative(selectedRecord.Pawn)
                : component.GetLatestAiNarrative(activeDeal.DealId);
            if (PrisonerDiplomacyMod.Settings?.EnableAiNarratives == true && aiNarrative != null)
            {
                headerHeight += GetAiNarrativePanelHeight(width, aiNarrative) + 8f;
            }
            bool negotiating = activeDeal?.State == DealState.Negotiating;
            TaggedString unavailableReason = TaggedString.Empty;
            bool available = negotiating || component.CanStartPlayerNegotiation(selectedRecord, out unavailableReason);
            const float bottomPadding = 16f;

            if (negotiating)
            {
                float counterHeight = GetCounterOfferPanelHeight(width, activeDeal);
                float contentHeight = counterHeight + 6f;
                if (activeDeal.NegotiationRound >= 2)
                {
                    TaggedString finalCounterText = "PD_FinalCounterOnly".Translate();
                    return headerHeight + contentHeight
                        + Math.Max(48f, Text.CalcHeight(finalCounterText, width - 20f) + 16f)
                        + bottomPadding;
                }

                TaggedString revisionHint = "PD_CounterRevisionHint".Translate();
                contentHeight += Math.Max(24f, Text.CalcHeight(revisionHint, width)) + 4f;
                contentHeight += 38f;
                contentHeight += GetRewardSectionHeight(component, width, true, activeDeal, available, unavailableReason);
                return headerHeight + contentHeight + bottomPadding;
            }

            List<Pawn> hostages = component.GetAvailableHostages(faction).ToList();
            float modeHeight = hostages.Count > 0 ? 42f : 0f;
            if (hostages.Count > 0 && negotiationMode == NegotiationMode.PrisonerExchange)
            {
                return headerHeight + modeHeight
                    + GetExchangeEditorContentHeight(width, hostages, available, unavailableReason)
                    + bottomPadding;
            }

            return headerHeight + modeHeight
                + GetRewardSectionHeight(component, width, false, activeDeal, available, unavailableReason)
                + bottomPadding;
        }

        private static float GetDealProgressHeight(float width, PrisonerDeal deal)
        {
            TaggedString rewards = deal?.Rewards?.Description() ?? TaggedString.Empty;
            TaggedString terms = "PD_UiAgreedTerms".Translate(rewards);
            float rewardHeight = Math.Max(22f, Text.CalcHeight(terms, Math.Max(120f, width - 24f)));
            return 58f + rewardHeight + (HasDealDeadline(deal) ? 37f : 8f);
        }

        private static void DrawDealProgress(Rect rect, PrisonerDeal deal)
        {
            PrisonerDiplomacyUiTheme.DrawPanel(rect, true);
            PrisonerDiplomacyUiTheme.DrawSectionHeading(
                new Rect(rect.x + 12f, rect.y + 6f, rect.width - 24f, 18f),
                "PD_UiDealProgress".Translate(),
                deal.DealId);
            string stateLabel = PrisonerDiplomacyUIUtility.DealStateLabel(deal.State);
            float stateWidth = Mathf.Clamp(Text.CalcSize(stateLabel).x + 18f, 92f, 180f);
            PrisonerDiplomacyUiTheme.DrawBadge(
                new Rect(rect.x + 12f, rect.y + 27f, stateWidth, 20f),
                stateLabel,
                DealStateTone(deal.State));

            TaggedString rewards = deal.Rewards?.Description() ?? TaggedString.Empty;
            TaggedString terms = "PD_UiAgreedTerms".Translate(rewards);
            float rewardHeight = Math.Max(22f, Text.CalcHeight(terms, rect.width - 24f));
            Text.Font = GameFont.Tiny;
            GUI.color = PrisonerDiplomacyUiTheme.TextMuted;
            Widgets.Label(new Rect(rect.x + 12f, rect.y + 53f, rect.width - 24f, rewardHeight),
                terms);
            GUI.color = Color.white;

            if (TryGetDealDeadline(deal, out TaggedString deadlineText, out float remainingFraction))
            {
                float deadlineY = rect.y + 57f + rewardHeight;
                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(rect.x + 12f, deadlineY, rect.width - 24f, 18f), deadlineText);
                DiplomacyUiTone deadlineTone = remainingFraction > 0.45f
                    ? DiplomacyUiTone.Accent
                    : remainingFraction > 0.18f ? DiplomacyUiTone.Warning : DiplomacyUiTone.Danger;
                PrisonerDiplomacyUiTheme.DrawProgress(
                    new Rect(rect.x + 12f, deadlineY + 22f, rect.width - 24f, 5f),
                    remainingFraction,
                    deadlineTone);
            }
            Text.Font = GameFont.Small;
        }

        private static float GetPirateRiskHeight(float width, PrisonerDeal deal)
        {
            TaggedString description = FactionNegotiationUtility.RiskDescription(deal.PirateRisk);
            return 48f + Math.Max(38f, Text.CalcHeight(description, Math.Max(120f, width - 24f)));
        }

        private static void DrawPirateRisk(Rect rect, PrisonerDeal deal)
        {
            DiplomacyUiTone tone = deal.PirateRiskMitigated
                ? DiplomacyUiTone.Positive
                : DiplomacyUiTone.Danger;
            PrisonerDiplomacyUiTheme.DrawNotice(rect, tone);
            PrisonerDiplomacyUiTheme.DrawSectionHeading(
                new Rect(rect.x + 12f, rect.y + 7f, rect.width - 24f, 18f),
                "PD_UiPirateRisk".Translate(),
                deal.PirateRiskMitigated
                    ? "PD_UiPirateRiskMitigated".Translate()
                    : "PD_UiPirateRiskDisclosed".Translate());
            Text.Font = GameFont.Small;
            GUI.color = PrisonerDiplomacyUiTheme.Tone(tone);
            Widgets.Label(new Rect(rect.x + 12f, rect.y + 31f, rect.width - 24f, rect.height - 39f),
                FactionNegotiationUtility.RiskDescription(deal.PirateRisk));
            GUI.color = Color.white;
        }

        private static bool HasDealDeadline(PrisonerDeal deal)
        {
            if (deal == null)
            {
                return false;
            }
            return deal.State == DealState.Offered
                || deal.State == DealState.Negotiating
                || deal.State == DealState.AcceptedAwaitingRelease
                || deal.State == DealState.ReleaseOrdered
                || deal.State == DealState.FulfillmentPending;
        }

        private static bool TryGetDealDeadline(
            PrisonerDeal deal,
            out TaggedString deadlineText,
            out float remainingFraction)
        {
            deadlineText = TaggedString.Empty;
            remainingFraction = 0f;
            if (!HasDealDeadline(deal))
            {
                return false;
            }

            int now = Find.TickManager?.TicksGame ?? 0;
            int deadline;
            int anchor;
            if (deal.State == DealState.Offered || deal.State == DealState.Negotiating)
            {
                deadline = deal.OfferExpiresTick;
                anchor = deal.CreatedTick;
                deadlineText = "PD_UiOfferDeadline".Translate(
                    Math.Max(0, deadline - now).ToStringTicksToPeriod());
            }
            else if (deal.State == DealState.FulfillmentPending && deal.PaymentDueTick > now)
            {
                deadline = deal.PaymentDueTick;
                anchor = deal.PrisonerDeliveredTick >= 0 ? deal.PrisonerDeliveredTick : deal.AcceptedTick;
                deadlineText = "PD_UiPaymentDeadline".Translate(
                    Math.Max(0, deadline - now).ToStringTicksToPeriod());
            }
            else
            {
                deadline = deal.FulfillmentExpiresTick;
                anchor = deal.AcceptedTick >= 0 ? deal.AcceptedTick : deal.CreatedTick;
                deadlineText = "PD_UiFulfillmentDeadline".Translate(
                    Math.Max(0, deadline - now).ToStringTicksToPeriod(),
                    deal.DeadlineExtensionCount);
            }

            int totalTicks = Math.Max(1, deadline - Math.Max(0, anchor));
            remainingFraction = Mathf.Clamp01((deadline - now) / (float)totalTicks);
            return true;
        }

        private static DiplomacyUiTone DealStateTone(DealState state)
        {
            switch (state)
            {
                case DealState.AcceptedAwaitingRelease:
                case DealState.Completed:
                    return DiplomacyUiTone.Positive;
                case DealState.Negotiating:
                case DealState.FulfillmentPending:
                    return DiplomacyUiTone.Warning;
                case DealState.ReleaseOrdered:
                case DealState.Offered:
                    return DiplomacyUiTone.Accent;
                case DealState.Rejected:
                case DealState.Expired:
                    return DiplomacyUiTone.Danger;
                default:
                    return DiplomacyUiTone.Neutral;
            }
        }

        private static float GetAiNarrativePanelHeight(float width, AiNarrativeRecord narrative)
        {
            TaggedString status = AiNarrativeStatusText(narrative);
            float textHeight = Math.Max(40f, Text.CalcHeight(status, Math.Max(100f, width - 20f)));
            return textHeight + 66f;
        }

        private void DrawAiNarrativePanel(Rect rect, AiNarrativeRecord narrative)
        {
            bool reduceMotion = PrisonerDiplomacyMod.Settings?.ReduceUiMotion == true;
            if (lastNarrativeStatus != narrative.Status)
            {
                lastNarrativeStatus = narrative.Status;
                narrativeTransitionStartedAt = Time.realtimeSinceStartup;
            }
            float fade = PrisonerDiplomacyUiTheme.FadeSince(
                narrativeTransitionStartedAt, !reduceMotion, 0.22f);
            PrisonerDiplomacyUiTheme.DrawPanel(rect, true);
            GUI.color = new Color(1f, 1f, 1f, fade);
            PrisonerDiplomacyUiTheme.DrawSignal(
                new Rect(rect.x + 10f, rect.y + 9f, 28f, 22f),
                !reduceMotion && narrative.Status == AiNarrativeStatus.Waiting);
            Text.Font = GameFont.Tiny;
            GUI.color = narrative.Status == AiNarrativeStatus.Generated
                ? PrisonerDiplomacyUiTheme.Positive
                : PrisonerDiplomacyUiTheme.Warning;
            Widgets.Label(new Rect(rect.x + 46f, rect.y + 8f, rect.width - 166f, 22f),
                "PD_AiNarrativeHeading".Translate());
            GUI.color = Color.white;
            if (PrisonerDiplomacyUiTheme.DrawButton(
                new Rect(rect.xMax - 112f, rect.y + 6f, 102f, 25f),
                "PD_AiDisableNow".Translate(),
                DiplomacyUiButtonStyle.Secondary))
            {
                PrisonerDiplomacyMod.SetAiNarrativesEnabled(false);
            }

            Text.Font = GameFont.Small;
            GUI.color = new Color(1f, 1f, 1f, fade);
            Widgets.Label(new Rect(rect.x + 10f, rect.y + 34f, rect.width - 20f, rect.height - 42f),
                AiNarrativeStatusText(narrative));
            GUI.color = Color.white;
        }

        private static TaggedString AiNarrativeStatusText(AiNarrativeRecord narrative)
        {
            switch (narrative.Status)
            {
                case AiNarrativeStatus.Waiting:
                    return "PD_AiNarrativeWaiting".Translate();
                case AiNarrativeStatus.Generated:
                    TaggedString generated = "PD_AiNarrativeGenerated".Translate(narrative.GeneratedText ?? string.Empty);
                    return narrative.AdvisoryApplied && !string.IsNullOrEmpty(narrative.AdvisorySummary)
                        ? generated.ToString() + "\n" + "PD_AiAdvisoryApplied".Translate(narrative.AdvisorySummary)
                        : generated;
                default:
                    return "PD_AiNarrativeFallback".Translate();
            }
        }

        private float GetRewardSectionHeight(
            PrisonerDiplomacyGameComponent component,
            float width,
            bool negotiating,
            PrisonerDeal activeDeal,
            bool available,
            TaggedString unavailableReason)
        {
            RewardDemand demand = CreateRewardDemand(out string validationKey);
            int round = negotiating ? activeDeal.NegotiationRound + 1 : 1;
            NegotiationResult preview = validationKey == null
                ? PrisonerNegotiationUtility.Evaluate(
                    selectedRecord,
                    negotiator,
                    demand,
                    component.GetAvailableReserve(faction),
                    NegotiationEconomyUtility.CalculateMaterialRewardCap(map),
                    round,
                    component.GetFactionMemoryMultiplier(faction, Find.TickManager.TicksGame),
                    component.GetNegotiationBudgetMultiplier(selectedRecord, activeDeal))
                : null;

            float messageHeight;
            if (preview != null)
            {
                TaggedString assessment = "PD_AssessmentRewardsText".Translate(
                    PrisonerNegotiationUtility.AssessmentLabel(preview.Assessment),
                    PreviewPrecisionText(preview),
                    demand.Description(),
                    preview.MaterialRewardCap);
                messageHeight = GetAssessmentPanelHeight(assessment, width);
            }
            else
            {
                TaggedString validation = (validationKey ?? "PD_NegotiationInvalidRewards").Translate();
                messageHeight = Math.Max(64f, Text.CalcHeight(validation, width - 20f) + 24f);
            }

            float terminalHeight = available
                ? 0f
                : Math.Max(48f, Text.CalcHeight(unavailableReason, width - 20f) + 16f);
            float noteHeight = AiNoteVisible() ? NegotiationNoteHeight + 4f : 0f;
            return GetRewardEditorHeight() + 4f + noteHeight + messageHeight + 4f + terminalHeight;
        }

        private static DiplomacyUiTone AssessmentTone(DemandAssessment assessment)
        {
            switch (assessment)
            {
                case DemandAssessment.VeryFavorable:
                    return DiplomacyUiTone.Positive;
                case DemandAssessment.Reasonable:
                    return DiplomacyUiTone.Accent;
                case DemandAssessment.Ambitious:
                    return DiplomacyUiTone.Warning;
                default:
                    return DiplomacyUiTone.Danger;
            }
        }

        private static float GetAssessmentPanelHeight(TaggedString assessment, float width)
        {
            return Math.Max(102f, Text.CalcHeight(assessment, Math.Max(120f, width - 20f)) + 28f);
        }

        private bool AiNoteVisible()
        {
            return PrisonerDiplomacyMod.Settings?.EnableAiNarratives == true;
        }

        private void DrawNegotiationNote(Rect rect)
        {
            PrisonerDiplomacyUiTheme.DrawNotice(rect, DiplomacyUiTone.Neutral);
            Text.Font = GameFont.Tiny;
            GUI.color = PrisonerDiplomacyUiTheme.TextMuted;
            Widgets.Label(
                new Rect(rect.x + 10f, rect.y + 6f, rect.width - 20f, 18f),
                "PD_AiPlayerNoteLabel".Translate());
            GUI.color = Color.white;
            negotiationNoteBuffer = Widgets.TextArea(
                new Rect(rect.x + 10f, rect.y + 27f, rect.width - 20f, rect.height - 35f),
                negotiationNoteBuffer ?? string.Empty,
                false);
            negotiationNoteBuffer = AiNegotiationNoteUtility.Normalize(negotiationNoteBuffer);
            Text.Font = GameFont.Small;
        }

        private float GetExchangeEditorContentHeight(
            float width,
            List<Pawn> hostages,
            bool available,
            TaggedString unavailableReason)
        {
            Pawn hostage = selectedHostage != null && hostages.Contains(selectedHostage)
                ? selectedHostage
                : hostages.FirstOrDefault();
            float contentHeight = 70f;
            if (hostage == null)
            {
                return contentHeight + Math.Max(48f, Text.CalcHeight("PD_ExchangeNoHostages".Translate(), width));
            }

            int compensation = PrisonerExchangeUtility.CalculateCompensation(selectedRecord, hostage);
            if (compensation > 0)
            {
                contentHeight += 40f;
            }

            ThingDef compensationThingDef = compensation > 0 ? exchangeCompensationThingDef : null;
            List<ThingDef> compensationSupplies = PrisonerExchangeUtility.AvailableCompensationSupplies(
                faction,
                compensation).ToList();
            if (compensationThingDef != null && !compensationSupplies.Contains(compensationThingDef))
            {
                compensationThingDef = null;
            }

            int compensationThingCount = compensationThingDef == null
                ? 0
                : PrisonerExchangeUtility.CalculateSupplyCount(faction, compensationThingDef, compensation);
            TaggedString compensationDescription = compensationThingDef == null
                ? "PD_ExchangeCompensationSilver".Translate(compensation)
                : "PD_ExchangeCompensationSupplies".Translate(
                    compensationThingCount,
                    compensationThingDef.LabelCap);
            contentHeight += GetExchangeSummaryHeight(
                width,
                selectedRecord.Pawn.LabelShortCap,
                selectedRecord.DiplomaticValue,
                hostage.LabelShortCap,
                PrisonerExchangeUtility.CalculateHostageCost(hostage),
                compensationDescription) + 8f;
            contentHeight += available
                ? 0f
                : Math.Max(48f, Text.CalcHeight(unavailableReason, width - 20f) + 16f);
            return contentHeight;
        }

        private static float GetExchangeSummaryHeight(
            float width,
            string prisonerLabel,
            int prisonerValue,
            string hostageLabel,
            int hostageCost,
            TaggedString compensationDescription)
        {
            float cellWidth = width < 460f ? width : (width - 8f) / 2f;
            TaggedString giveText = "PD_UiExchangeGiveText".Translate(prisonerLabel, prisonerValue);
            TaggedString receiveText = "PD_UiExchangeReceiveText".Translate(hostageLabel, hostageCost);
            float giveHeight = Math.Max(48f, Text.CalcHeight(giveText, cellWidth - 20f) + 28f);
            float receiveHeight = Math.Max(48f, Text.CalcHeight(receiveText, cellWidth - 20f) + 28f);
            float comparisonHeight = width < 460f
                ? giveHeight + receiveHeight + 6f
                : Math.Max(giveHeight, receiveHeight);
            TaggedString balance = "PD_UiExchangeBalance".Translate(compensationDescription);
            float balanceHeight = Math.Max(40f, Text.CalcHeight(balance, width - 20f) + 16f);
            return comparisonHeight + 6f + balanceHeight;
        }

        private static void DrawExchangeSummary(
            Rect rect,
            string prisonerLabel,
            int prisonerValue,
            string hostageLabel,
            int hostageCost,
            TaggedString compensationDescription)
        {
            TaggedString giveText = "PD_UiExchangeGiveText".Translate(prisonerLabel, prisonerValue);
            TaggedString receiveText = "PD_UiExchangeReceiveText".Translate(hostageLabel, hostageCost);
            float cellWidth = rect.width < 460f ? rect.width : (rect.width - 8f) / 2f;
            float giveHeight = Math.Max(48f, Text.CalcHeight(giveText, cellWidth - 20f) + 28f);
            float receiveHeight = Math.Max(48f, Text.CalcHeight(receiveText, cellWidth - 20f) + 28f);
            float comparisonHeight = rect.width < 460f
                ? giveHeight + receiveHeight + 6f
                : Math.Max(giveHeight, receiveHeight);

            if (rect.width < 460f)
            {
                DrawExchangeSide(
                    new Rect(rect.x, rect.y, rect.width, giveHeight),
                    "PD_UiExchangeGive".Translate(),
                    giveText,
                    DiplomacyUiTone.Warning);
                DrawExchangeSide(
                    new Rect(rect.x, rect.y + giveHeight + 6f, rect.width, receiveHeight),
                    "PD_UiExchangeReceive".Translate(),
                    receiveText,
                    DiplomacyUiTone.Positive);
            }
            else
            {
                DrawExchangeSide(
                    new Rect(rect.x, rect.y, cellWidth, comparisonHeight),
                    "PD_UiExchangeGive".Translate(),
                    giveText,
                    DiplomacyUiTone.Warning);
                DrawExchangeSide(
                    new Rect(rect.x + cellWidth + 8f, rect.y, cellWidth, comparisonHeight),
                    "PD_UiExchangeReceive".Translate(),
                    receiveText,
                    DiplomacyUiTone.Positive);
            }

            TaggedString balance = "PD_UiExchangeBalance".Translate(compensationDescription);
            float balanceHeight = Math.Max(40f, Text.CalcHeight(balance, rect.width - 20f) + 16f);
            Rect balanceRect = new Rect(rect.x, rect.y + comparisonHeight + 6f, rect.width, balanceHeight);
            PrisonerDiplomacyUiTheme.DrawNotice(balanceRect, DiplomacyUiTone.Accent);
            Widgets.Label(balanceRect.ContractedBy(10f, 8f), balance);
        }

        private static void DrawExchangeSide(
            Rect rect,
            string heading,
            TaggedString body,
            DiplomacyUiTone tone)
        {
            PrisonerDiplomacyUiTheme.DrawPanel(rect, true);
            PrisonerDiplomacyUiTheme.DrawSectionHeading(
                new Rect(rect.x + 10f, rect.y + 5f, rect.width - 20f, 18f),
                heading);
            GUI.color = PrisonerDiplomacyUiTheme.Tone(tone);
            Widgets.Label(new Rect(rect.x + 10f, rect.y + 25f, rect.width - 20f, rect.height - 31f), body);
            GUI.color = Color.white;
        }

        private static void DrawCounterOfferPanel(Rect rect, PrisonerDeal activeDeal)
        {
            TaggedString rewardDescription = activeDeal.Rewards.Description();
            float rewardHeight = Text.CalcHeight(rewardDescription, rect.width - 20f);
            PrisonerDiplomacyUiTheme.DrawNotice(rect, DiplomacyUiTone.Warning);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = PrisonerDiplomacyUiTheme.Warning;
            Widgets.Label(new Rect(rect.x + 10f, rect.y + 6f, rect.width - 120f, 24f), "PD_CounterOfferHeading".Translate());
            Text.Anchor = TextAnchor.UpperRight;
            Widgets.Label(new Rect(rect.xMax - 110f, rect.y + 6f, 100f, 24f),
                "PD_CounterOfferRound".Translate(activeDeal.NegotiationRound));
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            Widgets.Label(new Rect(rect.x + 10f, rect.y + 30f, rect.width - 20f, rewardHeight),
                rewardDescription);
        }

        private static float GetCounterOfferPanelHeight(float width, PrisonerDeal activeDeal)
        {
            return Math.Max(64f, 40f + Text.CalcHeight(activeDeal.Rewards.Description(), width - 20f));
        }

        private void DrawCounterOfferShortcuts(Rect rect, RewardDemand counterOffer)
        {
            const float spacing = 6f;
            float buttonWidth = (rect.width - spacing * 3f) / 4f;
            int[] increments = { 0, 50, 100, 250 };
            for (int index = 0; index < increments.Length; index++)
            {
                int increment = increments[index];
                bool enabled = NegotiationEconomyUtility.TryCreateCounterRevision(counterOffer, increment, out RewardDemand revision);
                Rect buttonRect = new Rect(rect.x + index * (buttonWidth + spacing), rect.y, buttonWidth, rect.height);
                string label = increment == 0
                    ? "PD_UseFactionOffer".Translate()
                    : "PD_AddSilverToCounter".Translate(increment);
                if (PrisonerDiplomacyUiTheme.DrawButton(
                    buttonRect,
                    label,
                    DiplomacyUiButtonStyle.Secondary,
                    enabled))
                {
                    ApplyDemandToEditor(revision);
                }

                if (!enabled && increment > 0)
                {
                    TooltipHandler.TipRegion(buttonRect, "PD_CounterShortcutUnavailable".Translate());
                }
            }
        }

        private RewardDemand DrawRewardEditor(Rect rect, out string validationKey)
        {
            float y = rect.y;
            DrawRewardGroupHeading(
                new Rect(rect.x, y, rect.width, RewardHeadingHeight),
                "PD_UiMaterialRewards".Translate(),
                DiplomacyUiTone.Accent);
            y += RewardHeadingHeight + RewardRowGap;
            DrawCheckboxField(new Rect(rect.x, y, rect.width, RewardRowHeight), "PD_DemandSilver".Translate(), ref requestSilver,
                ref silverBuffer, SilverInputRegex, 5);
            y += RewardRowHeight + RewardRowGap;

            Widgets.CheckboxLabeled(new Rect(rect.x, y, 150f, RewardRowHeight), SupplyDemandLabel(), ref requestSupplies);
            Rect supplyInput = new Rect(rect.x + 158f, y, Math.Max(80f, rect.width - 158f), RewardRowHeight);
            GUI.color = requestSupplies ? Color.white : Color.gray;
            float supplyButtonWidth = requestSupplies
                ? Math.Max(80f, supplyInput.width - 78f - RewardRowGap)
                : supplyInput.width;
            Rect supplyButton = new Rect(supplyInput.x, supplyInput.y, supplyButtonWidth, supplyInput.height);
            if (PrisonerDiplomacyUiTheme.DrawButton(
                supplyButton,
                selectedSupply?.LabelCap ?? "PD_SelectSupplies".Translate(),
                DiplomacyUiButtonStyle.Secondary,
                requestSupplies))
            {
                List<FloatMenuOption> options = SupplyRewardUtility.AvailableSupplies(faction)
                    .Select(def => new FloatMenuOption(def.LabelCap, () => selectedSupply = def))
                    .ToList();
                Find.WindowStack.Add(new FloatMenu(options));
            }
            if (requestSupplies)
            {
                supplyCountBuffer = Widgets.TextField(
                    new Rect(supplyInput.xMax - 78f, y, 78f, RewardRowHeight),
                    supplyCountBuffer ?? string.Empty,
                    3,
                    CountInputRegex);
            }
            string supplyValueTooltip = SupplyValueTooltip();
            if (!string.IsNullOrWhiteSpace(supplyValueTooltip))
            {
                TooltipHandler.TipRegion(supplyInput, supplyValueTooltip);
            }
            GUI.color = Color.white;
            y += RewardRowHeight + RewardRowGap;

            List<PrisonerDiplomacySpecialRewardDefinition> specialRewards = GetSpecialRewardOptions();
            if (specialRewards.Count > 0)
            {
                if (selectedSpecialReward == null || !specialRewards.Any(item =>
                    item.RewardId == selectedSpecialReward.RewardId))
                {
                    selectedSpecialReward = specialRewards.FirstOrDefault();
                }

                Widgets.CheckboxLabeled(
                    new Rect(rect.x, y, 150f, RewardRowHeight),
                    SpecialRewardDemandLabel(),
                    ref requestSpecialReward);
                Rect specialButton = new Rect(
                    rect.x + 158f,
                    y,
                    Math.Max(80f, rect.width - 158f),
                    RewardRowHeight);
                GUI.color = requestSpecialReward ? Color.white : Color.gray;
                if (PrisonerDiplomacyUiTheme.DrawButton(
                    specialButton,
                    SpecialRewardLabel(selectedSpecialReward),
                    DiplomacyUiButtonStyle.Secondary,
                    requestSpecialReward))
                {
                    Find.WindowStack.Add(new FloatMenu(specialRewards
                        .Select(definition => new FloatMenuOption(
                            SpecialRewardLabel(definition),
                            () => selectedSpecialReward = definition))
                        .ToList()));
                }
                string specialValueTooltip = SpecialRewardValueTooltip();
                string combinedSpecialTooltip = selectedSpecialReward != null
                    && !string.IsNullOrWhiteSpace(selectedSpecialReward.DescriptionKey)
                    ? selectedSpecialReward.DescriptionKey.Translate().ToString()
                    : string.Empty;
                if (!string.IsNullOrWhiteSpace(specialValueTooltip))
                {
                    if (!string.IsNullOrWhiteSpace(combinedSpecialTooltip))
                    {
                        combinedSpecialTooltip += "\n\n";
                    }
                    combinedSpecialTooltip += specialValueTooltip;
                }
                if (!string.IsNullOrWhiteSpace(combinedSpecialTooltip))
                {
                    TooltipHandler.TipRegion(specialButton, combinedSpecialTooltip);
                }
                GUI.color = Color.white;
                y += RewardRowHeight + RewardRowGap;
            }

            DrawRewardGroupHeading(
                new Rect(rect.x, y, rect.width, RewardHeadingHeight),
                "PD_UiDiplomaticRewards".Translate(),
                DiplomacyUiTone.Positive);
            y += RewardHeadingHeight + RewardRowGap;
            DrawCheckboxField(new Rect(rect.x, y, rect.width, RewardRowHeight), "PD_DemandGoodwill".Translate(), ref requestGoodwill,
                ref goodwillBuffer, GoodwillInputRegex, 2);
            y += RewardRowHeight + RewardRowGap;

            DrawRewardGroupHeading(
                new Rect(rect.x, y, rect.width, RewardHeadingHeight),
                "PD_UiStrategicRewards".Translate(),
                DiplomacyUiTone.Warning);
            y += RewardHeadingHeight + RewardRowGap;
            DrawCheckboxField(new Rect(rect.x, y, rect.width, RewardRowHeight), "PD_DemandCeasefire".Translate(), ref requestCeasefire,
                ref ceasefireBuffer, CeasefireInputRegex, 2);
            y += RewardRowHeight + RewardRowGap;

            DrawToggleField(
                new Rect(rect.x, y, rect.width, RewardRowHeight),
                "PD_DemandEarlyWarningIntel".Translate(),
                ref requestEarlyWarningIntel);

            return CreateRewardDemand(out validationKey);
        }

        private RewardDemand CreateRewardDemand(out string validationKey)
        {
            RewardDemand demand = new RewardDemand();
            validationKey = null;
            if (requestSilver && (!int.TryParse(silverBuffer, out demand.Silver) || demand.Silver <= 0))
            {
                validationKey = "PD_InvalidSilverReward";
            }

            if (requestSupplies)
            {
                demand.SupplyDef = selectedSupply;
                if (selectedSupply == null || !int.TryParse(supplyCountBuffer, out demand.SupplyCount) || demand.SupplyCount <= 0)
                {
                    validationKey = "PD_InvalidSupplyReward";
                }
            }

            if (requestGoodwill && (!int.TryParse(goodwillBuffer, out demand.Goodwill) || demand.Goodwill <= 0))
            {
                validationKey = "PD_InvalidGoodwillReward";
            }

            if (requestCeasefire
                && (!int.TryParse(ceasefireBuffer, out demand.CeasefireDays)
                    || demand.CeasefireDays < NegotiationEconomyUtility.MinimumCeasefireDays
                    || demand.CeasefireDays > NegotiationEconomyUtility.MaximumCeasefireDays))
            {
                validationKey = "PD_InvalidCeasefireReward";
            }

            demand.EarlyWarningIntel = requestEarlyWarningIntel;

            if (requestSpecialReward
                && !PrisonerDiplomacySpecialRewardUtility.TryPopulateDemand(
                    selectedRecord?.Pawn,
                    faction,
                    selectedSpecialReward?.RewardId,
                    demand,
                    out string specialRewardReason))
            {
                validationKey = specialRewardReason ?? "PD_NegotiationInvalidSpecialReward";
            }

            if (validationKey == null && !NegotiationEconomyUtility.IsDemandValid(
                faction,
                demand,
                out validationKey,
                selectedRecord?.Pawn))
            {
                return demand;
            }

            return demand;
        }

        private static void DrawRewardGroupHeading(Rect rect, string label, DiplomacyUiTone tone)
        {
            Color color = PrisonerDiplomacyUiTheme.Tone(tone);
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.y + 4f, 3f, rect.height - 8f), color);
            Text.Font = GameFont.Tiny;
            GUI.color = color;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(rect.x + 9f, rect.y, rect.width - 9f, rect.height), label);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        private static void DrawCheckboxField(Rect rect, string label, ref bool enabled, ref string buffer, Regex regex, int maxLength)
        {
            Widgets.CheckboxLabeled(new Rect(rect.x, rect.y, 150f, rect.height), label, ref enabled);
            GUI.color = enabled ? Color.white : Color.gray;
            buffer = Widgets.TextField(
                new Rect(rect.x + 158f, rect.y, Math.Max(80f, rect.width - 158f), rect.height),
                enabled ? buffer ?? string.Empty : string.Empty,
                maxLength,
                regex);
            GUI.color = Color.white;
        }

        private static void DrawToggleField(Rect rect, string label, ref bool enabled)
        {
            Widgets.CheckboxLabeled(new Rect(rect.x, rect.y, 150f, rect.height), label, ref enabled);
            Rect toggleRect = new Rect(rect.x + 158f, rect.y, Math.Max(80f, rect.width - 158f), rect.height);
            string toggleLabel = enabled
                ? "PD_UiEnabled".Translate()
                : "PD_UiDisabled".Translate();
            if (PrisonerDiplomacyUiTheme.DrawButton(
                toggleRect,
                toggleLabel,
                DiplomacyUiButtonStyle.Secondary,
                true,
                enabled))
            {
                enabled = !enabled;
            }
        }

        private void SelectRecord(PrisonerRecord record, PrisonerDeal activeDeal)
        {
            selectedRecord = record;
            detailScrollPosition = Vector2.zero;
            lastNarrativeStatus = null;
            narrativeTransitionStartedAt = Time.realtimeSinceStartup;
            negotiationNoteBuffer = string.Empty;
            negotiationMode = NegotiationMode.Ransom;
            selectedHostage = null;
            exchangeCompensationThingDef = null;
            selectedSpecialReward = GetSpecialRewardOptions().FirstOrDefault();
            RewardDemand source = activeDeal?.State == DealState.Negotiating
                ? activeDeal.Rewards
                : new RewardDemand { Silver = PrisonerNegotiationUtility.SuggestedDemand(record) };
            source = source ?? new RewardDemand { Silver = PrisonerNegotiationUtility.SuggestedDemand(record) };
            ApplyDemandToEditor(source);
        }

        private void ApplyDemandToEditor(RewardDemand source)
        {
            requestSilver = source.Silver > 0;
            requestSupplies = source.SupplyDef != null && source.SupplyCount > 0;
            requestGoodwill = source.Goodwill > 0;
            requestCeasefire = source.CeasefireDays > 0;
            requestEarlyWarningIntel = source.EarlyWarningIntel;
            requestSpecialReward = !string.IsNullOrWhiteSpace(source.SpecialRewardId);
            silverBuffer = source.Silver > 0 ? source.Silver.ToString() : string.Empty;
            selectedSupply = source.SupplyDef ?? SupplyRewardUtility.AvailableSupplies(faction).FirstOrDefault();
            supplyCountBuffer = source.SupplyCount > 0 ? source.SupplyCount.ToString() : "10";
            goodwillBuffer = source.Goodwill > 0 ? source.Goodwill.ToString() : "5";
            ceasefireBuffer = source.CeasefireDays > 0 ? source.CeasefireDays.ToString() : "10";
            selectedSpecialReward = GetSpecialRewardOptions()
                .FirstOrDefault(item => item.RewardId == source.SpecialRewardId)
                ?? selectedSpecialReward;
        }

        private float GetRewardEditorHeight()
        {
            return BaseRewardEditorHeight
                + (GetSpecialRewardOptions().Count > 0 ? RewardRowHeight + RewardRowGap : 0f);
        }

        private List<PrisonerDiplomacySpecialRewardDefinition> GetSpecialRewardOptions()
        {
            if (selectedRecord?.Pawn == null)
            {
                return new List<PrisonerDiplomacySpecialRewardDefinition>();
            }
            return PrisonerDiplomacyExtensionRegistry.GetSpecialRewards(
                selectedRecord.Pawn,
                faction).ToList();
        }

        private string SupplyDemandLabel()
        {
            string label = "PD_DemandSupplies".Translate().ToString();
            int unitValue;
            int totalValue;
            if (!TryGetSupplyValues(out unitValue, out totalValue))
            {
                return label;
            }

            int displayedValue = totalValue > 0 ? totalValue : unitValue;
            return label + " (" + "PD_ValueCompact".Translate(displayedValue).ToString() + ")";
        }

        private string SupplyValueTooltip()
        {
            int unitValue;
            int totalValue;
            if (!TryGetSupplyValues(out unitValue, out totalValue))
            {
                return string.Empty;
            }

            string tooltip = "PD_RewardUnitValue".Translate(unitValue).ToString();
            if (totalValue > 0)
            {
                tooltip += "\n" + "PD_RewardTotalValue".Translate(totalValue).ToString();
            }
            return tooltip;
        }

        private bool TryGetSupplyValues(out int unitValue, out int totalValue)
        {
            unitValue = 0;
            totalValue = 0;
            if (selectedSupply == null)
            {
                return false;
            }

            unitValue = SupplyRewardUtility.CalculateCost(faction, selectedSupply, 1);
            if (unitValue <= 0)
            {
                return false;
            }

            if (int.TryParse(supplyCountBuffer, out int count) && count > 0)
            {
                totalValue = SupplyRewardUtility.CalculateCost(faction, selectedSupply, count);
            }
            return true;
        }

        private string SpecialRewardDemandLabel()
        {
            string label = "PD_DemandSpecialReward".Translate().ToString();
            int unitValue;
            int totalValue;
            if (!TryGetSpecialRewardValues(out unitValue, out totalValue) || totalValue <= 0)
            {
                return label;
            }

            return label + " (" + "PD_ValueCompact".Translate(totalValue).ToString() + ")";
        }

        private string SpecialRewardValueTooltip()
        {
            int unitValue;
            int totalValue;
            if (!TryGetSpecialRewardValues(out unitValue, out totalValue))
            {
                return string.Empty;
            }

            return "PD_RewardUnitValue".Translate(unitValue).ToString()
                + "\n" + "PD_RewardTotalValue".Translate(totalValue).ToString();
        }

        private bool TryGetSpecialRewardValues(out int unitValue, out int totalValue)
        {
            unitValue = 0;
            totalValue = 0;
            if (selectedSpecialReward == null)
            {
                return false;
            }

            ThingDef thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(
                selectedSpecialReward.RequiredThingDefName);
            unitValue = PrisonerDiplomacySpecialRewardUtility.CalculateCost(thingDef, 1);
            totalValue = PrisonerDiplomacySpecialRewardUtility.CalculateCost(
                thingDef,
                selectedSpecialReward.MinimumCount);
            return unitValue > 0 && totalValue > 0;
        }

        private static string SpecialRewardLabel(PrisonerDiplomacySpecialRewardDefinition definition)
        {
            if (definition == null)
            {
                return "PD_SelectSpecialReward".Translate();
            }
            string label = string.IsNullOrWhiteSpace(definition.LabelKey)
                ? definition.RewardId
                : definition.LabelKey.Translate().ToString();
            return "PD_SpecialRewardOption".Translate(definition.MinimumCount, label);
        }

        private static string PreviewPrecisionText(NegotiationResult result)
        {
            if (result.SocialSkill >= 16)
            {
                return "PD_PreviewExact".Translate(result.AcceptanceChance.ToStringPercent());
            }

            if (result.SocialSkill >= 8)
            {
                int rounded = Math.Max(5, Math.Min(95, (int)Math.Round(result.AcceptanceChance * 10f) * 10));
                return "PD_PreviewApproximate".Translate(rounded);
            }

            return "PD_PreviewUncertain".Translate();
        }
    }
}
