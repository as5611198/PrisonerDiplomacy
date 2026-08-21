using UnityEngine;
using Verse;

namespace PrisonerDiplomacyExampleAddon
{
    /// <summary>
    /// Small local theme helper. The core theme is intentionally internal, so
    /// add-ons should own their presentation instead of depending on internals.
    /// </summary>
    internal static class ExampleAddonUi
    {
        private static readonly Color Accent = new Color(0.33f, 0.70f, 0.78f, 1f);
        private static readonly Color Border = new Color(0.28f, 0.31f, 0.32f, 1f);
        private static readonly Color Surface = new Color(0.15f, 0.18f, 0.19f, 1f);
        private static readonly Color Primary = new Color(0.10f, 0.24f, 0.26f, 1f);

        internal static bool DrawButton(Rect rect, string label, bool primary = false)
        {
            bool enabled = GUI.enabled;
            bool hovered = enabled && Mouse.IsOver(rect);
            bool pressed = hovered && Event.current != null
                && Event.current.type == EventType.MouseDown;
            Color border = primary ? Accent : Border;
            Color fill = primary ? Primary : Surface;
            if (hovered)
            {
                border = Color.Lerp(border, Color.white, 0.16f);
                fill = Color.Lerp(fill, border, 0.14f);
            }
            if (pressed)
            {
                fill = Color.Lerp(fill, Color.black, 0.18f);
            }
            if (!enabled)
            {
                border = Color.Lerp(border, Color.gray, 0.55f);
                fill = Color.Lerp(fill, Color.black, 0.35f);
            }

            Widgets.DrawBoxSolid(rect, border);
            Widgets.DrawBoxSolid(rect.ContractedBy(1f), fill);
            if (hovered)
            {
                Widgets.DrawBoxSolid(
                    new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, 2f),
                    new Color(border.r, border.g, border.b, 0.78f));
            }

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = enabled ? Color.white : new Color(0.7f, 0.7f, 0.7f, 0.62f);
            Widgets.Label(rect.ContractedBy(6f, 2f), label ?? string.Empty);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            return enabled && Widgets.ButtonInvisible(rect, true);
        }
    }
}
