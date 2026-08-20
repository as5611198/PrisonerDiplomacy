using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace PrisonerDiplomacy
{
    public enum PrisonerDiplomacyUiRegion
    {
        FactionHeader,
        PrisonerSummary,
        NegotiationBody
    }

    public sealed class PrisonerDiplomacyUiContext
    {
        public PrisonerDiplomacyFactionSnapshot Faction { get; internal set; }
        public PrisonerDiplomacyPrisonerSnapshot Prisoner { get; internal set; }
        public PrisonerDiplomacyDealSnapshot Deal { get; internal set; }
        public bool CompactLayout { get; internal set; }
    }

    public interface IPrisonerDiplomacyUiExtension
    {
        string Id { get; }
        int Order { get; }
        float GetHeight(PrisonerDiplomacyUiRegion region, PrisonerDiplomacyUiContext context, float width);
        void Draw(PrisonerDiplomacyUiRegion region, Rect rect, PrisonerDiplomacyUiContext context);
    }

    public static class PrisonerDiplomacyUiExtensionRegistry
    {
        private static readonly List<IPrisonerDiplomacyUiExtension> Extensions =
            new List<IPrisonerDiplomacyUiExtension>();

        public static bool Register(IPrisonerDiplomacyUiExtension extension)
        {
            if (extension == null || string.IsNullOrWhiteSpace(extension.Id)
                || Extensions.Any(item => string.Equals(item.Id, extension.Id, StringComparison.Ordinal)))
            {
                return false;
            }

            Extensions.Add(extension);
            Extensions.Sort((left, right) => left.Order.CompareTo(right.Order));
            return true;
        }

        public static bool Unregister(string id)
        {
            return !string.IsNullOrEmpty(id)
                && Extensions.RemoveAll(item => string.Equals(item.Id, id, StringComparison.Ordinal)) > 0;
        }

        internal static float GetHeight(
            PrisonerDiplomacyUiRegion region,
            PrisonerDiplomacyUiContext context,
            float width)
        {
            float total = 0f;
            foreach (IPrisonerDiplomacyUiExtension extension in Extensions.ToList())
            {
                try
                {
                    total += Math.Max(0f, extension.GetHeight(region, context, width));
                }
                catch (Exception exception)
                {
                    CompatibilityDiagnostics.LogErrorOnce(
                        "ui-extension-height:" + extension.Id,
                        "Disabled invalid height from a Prisoner Diplomacy UI extension.",
                        exception);
                }
            }
            return total;
        }

        internal static float Draw(
            PrisonerDiplomacyUiRegion region,
            Rect rect,
            PrisonerDiplomacyUiContext context)
        {
            float y = rect.y;
            foreach (IPrisonerDiplomacyUiExtension extension in Extensions.ToList())
            {
                try
                {
                    float height = Math.Max(0f, extension.GetHeight(region, context, rect.width));
                    if (height <= 0f)
                    {
                        continue;
                    }
                    extension.Draw(region, new Rect(rect.x, y, rect.width, height), context);
                    y += height;
                }
                catch (Exception exception)
                {
                    CompatibilityDiagnostics.LogErrorOnce(
                        "ui-extension-draw:" + extension.Id,
                        "Isolated a Prisoner Diplomacy UI extension drawing exception.",
                        exception);
                    PrisonerDiplomacyUiTheme.ResetText();
                }
            }
            return y - rect.y;
        }
    }
}
