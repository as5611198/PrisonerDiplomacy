using System;
using System.Linq;

namespace PrisonerDiplomacy
{
    internal static class AiNegotiationNoteUtility
    {
        internal const int MaximumCharacters = 240;

        private static readonly string[] ThreateningTerms =
        {
            "kill", "execute", "torture", "organ", "kidney", "cut", "threat",
            "\u6bba", "\u8655\u6c7a", "\u62f7\u554f", "\u8170\u5b50", "\u8178\u5b50", "\u5272", "\u5272\u958b",
            "\u4e0d\u7136", "\u5426\u5247", "\u8981\u662f\u4e0d", "\u4e0d\u7d66"
        };

        private static readonly string[] ConciliatoryTerms =
        {
            "please", "mercy", "ally", "alliance", "friend", "honor", "goodwill", "cooperate",
            "\u8acb", "\u8acb\u6c42", "\u6148\u60b2", "\u540c\u76df", "\u53cb\u597d", "\u53cb\u8abc", "\u8aa0\u610f", "\u539f\u8ad2",
            "\u770b\u5728", "\u653e\u4ed6\u4e00\u99ac", "\u5408\u4f5c"
        };

        private static readonly string[] UrgentTerms =
        {
            "urgent", "hurry", "immediately", "desperate", "please hurry", "\u5feb", "\u7acb\u523b",
            "\u6025", "\u8feb\u5207", "\u7121\u6cd5\u7b49", "\u6551\u4ed6", "\u6551\u5979"
        };

        private static readonly string[] RespectfulTerms =
        {
            "sir", "lord", "lady", "your grace", "honorable", "respectfully", "\u5927\u4eba",
            "\u6b8a\u69ae", "\u69ae\u8b7d", "\u656c\u610f", "\u8cb4\u65b9", "\u95a3\u4e0b"
        };

        public static string Normalize(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            string normalized = new string(raw
                .Where(character => !char.IsControl(character))
                .ToArray())
                .Trim();
            return normalized.Length <= MaximumCharacters
                ? normalized
                : normalized.Substring(0, MaximumCharacters).TrimEnd();
        }

        public static string Classify(string raw)
        {
            string normalized = Normalize(raw);
            if (normalized.Length == 0)
            {
                return "neutral";
            }

            if (ContainsAny(normalized, ThreateningTerms))
            {
                return "threatening";
            }

            if (ContainsAny(normalized, ConciliatoryTerms))
            {
                return "conciliatory";
            }

            if (ContainsAny(normalized, UrgentTerms))
            {
                return "urgent";
            }

            if (ContainsAny(normalized, RespectfulTerms))
            {
                return "respectful";
            }

            return "neutral";
        }

        private static bool ContainsAny(string value, string[] terms)
        {
            return terms.Any(term => value.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
}
