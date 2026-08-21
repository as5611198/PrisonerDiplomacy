using PrisonerDiplomacy;
using UnityEngine;
using Verse;

namespace PrisonerDiplomacyExampleAddon
{
    /// <summary>
    /// A compact read-only header contribution. It consumes snapshot data and
    /// deliberately provides no transaction button or mutation path.
    /// </summary>
    public sealed class ExampleHeaderUiExtension : IPrisonerDiplomacyUiExtension
    {
        public string Id => "g1061.prisonerdiplomacy.exampleaddon.header";
        public int Order => 200;

        public float GetHeight(
            PrisonerDiplomacyUiRegion region,
            PrisonerDiplomacyUiContext context,
            float width)
        {
            return ExampleAddonMod.Settings?.ShowHeaderWidget != false
                && region == PrisonerDiplomacyUiRegion.FactionHeader
                && context?.Prisoner?.Pawn != null ? 28f : 0f;
        }

        public void Draw(
            PrisonerDiplomacyUiRegion region,
            Rect rect,
            PrisonerDiplomacyUiContext context)
        {
            if (region != PrisonerDiplomacyUiRegion.FactionHeader
                || context?.Prisoner?.Pawn == null)
            {
                return;
            }

            int adjustment = PrisonerDiplomacyBackendApi.GetDiplomaticValueAdjustment(
                context.Prisoner.Pawn,
                context.Prisoner.OriginalFaction);
            int rewardCount = context.Prisoner.SpecialRewardIds?.Count ?? 0;

            Color oldColor = GUI.color;
            TextAnchor oldAnchor = Text.Anchor;
            Widgets.DrawBoxSolid(rect, new Color(0.07f, 0.11f, 0.13f, 0.96f));
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, rect.width, 1f),
                new Color(0.20f, 0.78f, 0.74f));
            GUI.color = new Color(0.78f, 0.92f, 0.90f);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(
                rect.ContractedBy(8f, 0f),
                "PDX_HeaderStatus".Translate(
                    adjustment >= 0 ? "+" + adjustment : adjustment.ToString(),
                    rewardCount));
            TooltipHandler.TipRegion(rect, "PDX_HeaderStatusTooltip".Translate());
            Text.Anchor = oldAnchor;
            GUI.color = oldColor;
        }
    }
}
