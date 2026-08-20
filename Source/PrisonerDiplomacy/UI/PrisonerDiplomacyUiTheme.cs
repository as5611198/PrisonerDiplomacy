using System;
using UnityEngine;
using Verse;

namespace PrisonerDiplomacy
{
    internal enum DiplomacyUiButtonStyle
    {
        Primary,
        Secondary,
        Danger
    }

    internal enum DiplomacyUiTone
    {
        Neutral,
        Positive,
        Warning,
        Danger,
        Accent
    }

    internal static class PrisonerDiplomacyUiTheme
    {
        public const float Gap = 10f;
        public const float PanelPadding = 12f;
        public const float ControlHeight = 34f;
        public const float ActionHeight = 42f;

        public static readonly Color Canvas = new Color(0.075f, 0.085f, 0.095f, 0.98f);
        public static readonly Color Panel = new Color(0.105f, 0.12f, 0.13f, 0.98f);
        public static readonly Color PanelRaised = new Color(0.135f, 0.15f, 0.16f, 0.98f);
        public static readonly Color PanelSelected = new Color(0.16f, 0.205f, 0.21f, 1f);
        public static readonly Color Border = new Color(0.28f, 0.31f, 0.32f, 1f);
        public static readonly Color TextMuted = new Color(0.68f, 0.71f, 0.72f, 1f);
        public static readonly Color Positive = new Color(0.38f, 0.78f, 0.64f, 1f);
        public static readonly Color Warning = new Color(0.95f, 0.72f, 0.28f, 1f);
        public static readonly Color Danger = new Color(0.90f, 0.35f, 0.32f, 1f);
        public static readonly Color Accent = new Color(0.33f, 0.70f, 0.78f, 1f);

        private static readonly Color PrimaryButton = new Color(0.10f, 0.24f, 0.26f, 1f);
        private static readonly Color SecondaryButton = new Color(0.15f, 0.18f, 0.19f, 1f);
        private static readonly Color DangerButton = new Color(0.25f, 0.11f, 0.12f, 1f);

        public static Color Tone(DiplomacyUiTone tone)
        {
            switch (tone)
            {
                case DiplomacyUiTone.Positive: return Positive;
                case DiplomacyUiTone.Warning: return Warning;
                case DiplomacyUiTone.Danger: return Danger;
                case DiplomacyUiTone.Accent: return Accent;
                default: return TextMuted;
            }
        }

        public static void DrawPanel(Rect rect, bool raised = false, bool selected = false)
        {
            Widgets.DrawBoxSolid(rect, selected ? PanelSelected : raised ? PanelRaised : Panel);
            GUI.color = selected ? Accent : Border;
            Widgets.DrawBox(rect);
            GUI.color = Color.white;
        }

        public static bool DrawButton(
            Rect rect,
            string label,
            DiplomacyUiButtonStyle style = DiplomacyUiButtonStyle.Secondary,
            bool enabled = true,
            bool selected = false)
        {
            bool canInteract = enabled && GUI.enabled;
            bool hovered = canInteract && Mouse.IsOver(rect);
            bool active = hovered
                && Event.current != null
                && Event.current.type == EventType.MouseDown;
            Color borderColor;
            Color baseColor = ButtonColor(style, out borderColor);
            if (selected)
            {
                borderColor = Accent;
                baseColor = Color.Lerp(baseColor, Accent, 0.18f);
            }
            if (hovered)
            {
                baseColor = Color.Lerp(baseColor, borderColor, 0.16f);
                borderColor = Color.Lerp(borderColor, Color.white, 0.16f);
            }
            if (active)
            {
                baseColor = Color.Lerp(baseColor, Color.black, 0.16f);
            }
            if (!canInteract)
            {
                baseColor = Color.Lerp(baseColor, Canvas, 0.42f);
                borderColor = Color.Lerp(borderColor, Border, 0.45f);
            }

            DrawButtonSurface(rect, baseColor, borderColor, hovered, active, canInteract);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = canInteract ? Color.white : new Color(TextMuted.r, TextMuted.g, TextMuted.b, 0.58f);
            Widgets.Label(rect.ContractedBy(6f, 2f), label ?? string.Empty);
            ResetText();

            return canInteract && Widgets.ButtonInvisible(rect, true);
        }

        private static Color ButtonColor(DiplomacyUiButtonStyle style, out Color borderColor)
        {
            switch (style)
            {
                case DiplomacyUiButtonStyle.Primary:
                    borderColor = Accent;
                    return PrimaryButton;
                case DiplomacyUiButtonStyle.Danger:
                    borderColor = Danger;
                    return DangerButton;
                default:
                    borderColor = Border;
                    return SecondaryButton;
            }
        }

        private static void DrawButtonSurface(
            Rect rect,
            Color baseColor,
            Color borderColor,
            bool hovered,
            bool active,
            bool canInteract)
        {
            Rect inner = rect.ContractedBy(1f);
            Widgets.DrawBoxSolid(rect, new Color(borderColor.r, borderColor.g, borderColor.b,
                canInteract ? hovered ? 0.96f : 0.78f : 0.42f));
            Widgets.DrawBoxSolid(inner, baseColor);

            float topHeight = Mathf.Max(1f, inner.height * 0.46f);
            float bottomY = inner.y + topHeight;
            Color top = Color.Lerp(baseColor, Color.white, active ? 0.06f : hovered ? 0.10f : 0.045f);
            Color bottom = Color.Lerp(baseColor, Color.black, active ? 0.20f : 0.12f);
            Widgets.DrawBoxSolid(new Rect(inner.x, inner.y, inner.width, topHeight), top);
            Widgets.DrawBoxSolid(new Rect(inner.x, bottomY, inner.width, Mathf.Max(1f, inner.yMax - bottomY)), bottom);
            if (hovered && canInteract)
            {
                Widgets.DrawBoxSolid(new Rect(inner.x, inner.y, inner.width, 2f),
                    new Color(borderColor.r, borderColor.g, borderColor.b, active ? 0.95f : 0.72f));
            }
        }

        public static void DrawSectionHeading(Rect rect, string label, string trailing = null)
        {
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = TextMuted;
            Widgets.Label(rect, label);
            if (!string.IsNullOrEmpty(trailing))
            {
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(rect, trailing);
            }
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }

        public static void DrawBadge(Rect rect, string text, DiplomacyUiTone tone)
        {
            Color color = Tone(tone);
            Widgets.DrawBoxSolid(rect, new Color(color.r * 0.22f, color.g * 0.22f, color.b * 0.22f, 0.96f));
            GUI.color = new Color(color.r, color.g, color.b, 0.85f);
            Widgets.DrawBox(rect);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = color;
            Widgets.Label(rect.ContractedBy(4f, 0f), text);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }

        public static void DrawMetric(Rect rect, string label, string value, DiplomacyUiTone tone = DiplomacyUiTone.Neutral)
        {
            Widgets.DrawBoxSolid(rect, PanelRaised);
            Text.Font = GameFont.Tiny;
            GUI.color = TextMuted;
            Widgets.Label(new Rect(rect.x + 10f, rect.y + 6f, rect.width - 20f, 18f), label);
            Text.Font = GameFont.Small;
            GUI.color = tone == DiplomacyUiTone.Neutral ? Color.white : Tone(tone);
            Widgets.Label(new Rect(rect.x + 10f, rect.y + 24f, rect.width - 20f, rect.height - 28f), value);
            GUI.color = Color.white;
        }

        public static void DrawNotice(Rect rect, DiplomacyUiTone tone)
        {
            Color color = Tone(tone);
            Widgets.DrawBoxSolid(rect, new Color(
                PanelRaised.r + color.r * 0.06f,
                PanelRaised.g + color.g * 0.06f,
                PanelRaised.b + color.b * 0.06f,
                0.98f));
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, 3f, rect.height), color);
            GUI.color = new Color(color.r, color.g, color.b, 0.72f);
            Widgets.DrawBox(rect);
            GUI.color = Color.white;
        }

        public static void DrawProgress(Rect rect, float value, DiplomacyUiTone tone)
        {
            value = Mathf.Clamp01(value);
            Widgets.DrawBoxSolid(rect, new Color(0.04f, 0.05f, 0.055f, 1f));
            Color color = Tone(tone);
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, rect.width * value, rect.height), color);
        }

        public static void DrawSignal(Rect rect, bool animated)
        {
            float phase = animated ? Time.realtimeSinceStartup * 2.2f : 0f;
            float barWidth = 3f;
            for (int index = 0; index < 4; index++)
            {
                float pulse = animated ? 0.62f + 0.38f * Mathf.Sin(phase - index * 0.55f) : 0.85f;
                float height = 5f + index * 4f;
                GUI.color = new Color(Accent.r, Accent.g, Accent.b, Mathf.Clamp01(pulse));
                Widgets.DrawBoxSolid(new Rect(
                    rect.x + index * (barWidth + 3f),
                    rect.yMax - height,
                    barWidth,
                    height), GUI.color);
            }
            GUI.color = Color.white;
        }

        public static float FadeSince(float startedAt, bool animated, float duration = 0.18f)
        {
            return animated ? Mathf.Clamp01((Time.realtimeSinceStartup - startedAt) / Math.Max(0.01f, duration)) : 1f;
        }

        public static void ResetText()
        {
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }
    }
}
