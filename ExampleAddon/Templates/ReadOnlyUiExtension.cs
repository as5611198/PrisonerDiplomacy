using PrisonerDiplomacy;
using UnityEngine;
using Verse;

namespace YourAuthor.YourAddon
{
    public sealed class YourReadOnlyUiExtension : IPrisonerDiplomacyUiExtension
    {
        public string Id => "yourauthor.youraddon.ui.header";
        public int Order => 200;

        public float GetHeight(
            PrisonerDiplomacyUiRegion region,
            PrisonerDiplomacyUiContext context,
            float width)
        {
            return region == PrisonerDiplomacyUiRegion.FactionHeader
                && context?.Prisoner?.Pawn != null ? 28f : 0f;
        }

        public void Draw(
            PrisonerDiplomacyUiRegion region,
            Rect rect,
            PrisonerDiplomacyUiContext context)
        {
            if (region != PrisonerDiplomacyUiRegion.FactionHeader)
            {
                return;
            }

            Color oldColor = GUI.color;
            TextAnchor oldAnchor = Text.Anchor;
            GameFont oldFont = Text.Font;
            Widgets.DrawBoxSolid(rect, new Color(0.08f, 0.11f, 0.12f, 0.96f));
            GUI.color = new Color(0.72f, 0.90f, 0.88f);
            Text.Anchor = TextAnchor.MiddleLeft;
            Text.Font = GameFont.Small;
            Widgets.Label(rect.ContractedBy(8f, 0f), "YourAddon_HeaderStatus".Translate());
            Text.Font = oldFont;
            Text.Anchor = oldAnchor;
            GUI.color = oldColor;
        }
    }
}
