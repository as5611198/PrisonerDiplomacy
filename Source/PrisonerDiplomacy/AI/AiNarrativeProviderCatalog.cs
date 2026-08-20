using System;
using System.Collections.Generic;

namespace PrisonerDiplomacy
{
    public enum AiNarrativeProviderKind
    {
        OpenAI,
        Google,
        DeepSeek,
        Grok,
        GLM,
        Alibaba,
        OpenRouter,
        CustomOpenAI
    }

    internal sealed class AiNarrativeProviderDefinition
    {
        public AiNarrativeProviderKind Kind;
        public string DisplayName;
        public string BaseUrl;
        public string ModelsUrl;
        public string DefaultModel;
        public bool UsesGoogleProtocol;
    }

    internal static class AiNarrativeProviderCatalog
    {
        private static readonly AiNarrativeProviderKind[] OrderedKinds =
        {
            AiNarrativeProviderKind.OpenAI,
            AiNarrativeProviderKind.Google,
            AiNarrativeProviderKind.DeepSeek,
            AiNarrativeProviderKind.Grok,
            AiNarrativeProviderKind.GLM,
            AiNarrativeProviderKind.Alibaba,
            AiNarrativeProviderKind.OpenRouter,
            AiNarrativeProviderKind.CustomOpenAI
        };

        private static readonly Dictionary<AiNarrativeProviderKind, AiNarrativeProviderDefinition> Definitions
            = new Dictionary<AiNarrativeProviderKind, AiNarrativeProviderDefinition>
            {
                {
                    AiNarrativeProviderKind.OpenAI,
                    new AiNarrativeProviderDefinition
                    {
                        Kind = AiNarrativeProviderKind.OpenAI,
                        DisplayName = "OpenAI",
                        BaseUrl = "https://api.openai.com/v1",
                        ModelsUrl = "https://api.openai.com/v1/models",
                        DefaultModel = "gpt-5.4"
                    }
                },
                {
                    AiNarrativeProviderKind.Google,
                    new AiNarrativeProviderDefinition
                    {
                        Kind = AiNarrativeProviderKind.Google,
                        DisplayName = "Google Gemini",
                        BaseUrl = "https://generativelanguage.googleapis.com/v1beta",
                        ModelsUrl = "https://generativelanguage.googleapis.com/v1beta/models",
                        UsesGoogleProtocol = true
                    }
                },
                {
                    AiNarrativeProviderKind.DeepSeek,
                    new AiNarrativeProviderDefinition
                    {
                        Kind = AiNarrativeProviderKind.DeepSeek,
                        DisplayName = "DeepSeek",
                        BaseUrl = "https://api.deepseek.com/v1",
                        ModelsUrl = "https://api.deepseek.com/models"
                    }
                },
                {
                    AiNarrativeProviderKind.Grok,
                    new AiNarrativeProviderDefinition
                    {
                        Kind = AiNarrativeProviderKind.Grok,
                        DisplayName = "Grok (xAI)",
                        BaseUrl = "https://api.x.ai/v1",
                        ModelsUrl = "https://api.x.ai/v1/models"
                    }
                },
                {
                    AiNarrativeProviderKind.GLM,
                    new AiNarrativeProviderDefinition
                    {
                        Kind = AiNarrativeProviderKind.GLM,
                        DisplayName = "GLM",
                        BaseUrl = "https://open.bigmodel.cn/api/paas/v4",
                        ModelsUrl = "https://open.bigmodel.cn/api/paas/v4/models"
                    }
                },
                {
                    AiNarrativeProviderKind.Alibaba,
                    new AiNarrativeProviderDefinition
                    {
                        Kind = AiNarrativeProviderKind.Alibaba,
                        DisplayName = "Alibaba DashScope",
                        BaseUrl = "https://dashscope.aliyuncs.com/compatible-mode/v1",
                        ModelsUrl = "https://dashscope.aliyuncs.com/compatible-mode/v1/models"
                    }
                },
                {
                    AiNarrativeProviderKind.OpenRouter,
                    new AiNarrativeProviderDefinition
                    {
                        Kind = AiNarrativeProviderKind.OpenRouter,
                        DisplayName = "OpenRouter",
                        BaseUrl = "https://openrouter.ai/api/v1",
                        ModelsUrl = "https://openrouter.ai/api/v1/models"
                    }
                },
                {
                    AiNarrativeProviderKind.CustomOpenAI,
                    new AiNarrativeProviderDefinition
                    {
                        Kind = AiNarrativeProviderKind.CustomOpenAI,
                        DisplayName = "Custom OpenAI-compatible"
                    }
                }
            };

        public static IEnumerable<AiNarrativeProviderKind> AllKinds
        {
            get { return OrderedKinds; }
        }

        public static AiNarrativeProviderDefinition Get(AiNarrativeProviderKind kind)
        {
            if (Definitions.TryGetValue(kind, out AiNarrativeProviderDefinition definition))
            {
                return definition;
            }

            return Definitions[AiNarrativeProviderKind.OpenAI];
        }

        public static string DisplayName(AiNarrativeProviderKind kind)
        {
            return Get(kind).DisplayName;
        }

        public static string DefaultModel(AiNarrativeProviderKind kind)
        {
            return Get(kind).DefaultModel ?? string.Empty;
        }

        public static bool UsesGoogleProtocol(AiNarrativeProviderKind kind)
        {
            return Get(kind).UsesGoogleProtocol;
        }

        public static bool RequiresApiKey(PrisonerDiplomacySettings settings)
        {
            return settings == null
                || settings.AiProvider != AiNarrativeProviderKind.CustomOpenAI
                || settings.AiEndpointRequiresKey;
        }

        public static string ResolveBaseUrl(PrisonerDiplomacySettings settings)
        {
            if (settings == null)
            {
                return string.Empty;
            }

            if (settings.AiProvider == AiNarrativeProviderKind.CustomOpenAI)
            {
                string configured = settings.AiCustomBaseUrl;
                if (string.IsNullOrWhiteSpace(configured))
                {
                    configured = settings.AiEndpoint;
                }
                return NormalizeBaseUrl(configured);
            }

            return Get(settings.AiProvider).BaseUrl;
        }

        public static string ResolveGenerationEndpoint(PrisonerDiplomacySettings settings)
        {
            string baseUrl = ResolveBaseUrl(settings);
            if (string.IsNullOrEmpty(baseUrl))
            {
                return string.Empty;
            }

            if (UsesGoogleProtocol(settings.AiProvider))
            {
                string model = NormalizeGoogleModelName(settings.AiModel);
                return string.IsNullOrEmpty(model)
                    ? baseUrl
                    : baseUrl + "/models/" + Uri.EscapeDataString(model) + ":generateContent";
            }

            return baseUrl + "/chat/completions";
        }

        public static string ResolveModelsEndpoint(PrisonerDiplomacySettings settings)
        {
            if (settings == null)
            {
                return string.Empty;
            }

            if (settings.AiProvider == AiNarrativeProviderKind.CustomOpenAI)
            {
                string baseUrl = ResolveBaseUrl(settings);
                return string.IsNullOrEmpty(baseUrl) ? string.Empty : baseUrl + "/models";
            }

            return Get(settings.AiProvider).ModelsUrl;
        }

        public static string NormalizeBaseUrl(string input)
        {
            string value = input?.Trim();
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            if (!value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                && !value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                value = "http://" + value;
            }

            if (!Uri.TryCreate(value, UriKind.Absolute, out Uri uri))
            {
                return value.TrimEnd('/');
            }

            string normalized = uri.AbsoluteUri.TrimEnd('/');
            normalized = RemoveSuffix(normalized, "/chat/completions");
            normalized = RemoveSuffix(normalized, "/models");
            return normalized.TrimEnd('/');
        }

        public static string NormalizeGoogleModelName(string model)
        {
            string value = model?.Trim() ?? string.Empty;
            return value.StartsWith("models/", StringComparison.OrdinalIgnoreCase)
                ? value.Substring("models/".Length)
                : value;
        }

        public static bool TryValidateEndpoint(string endpoint, out Uri uri, out string issue)
        {
            issue = null;
            if (!Uri.TryCreate(endpoint?.Trim(), UriKind.Absolute, out uri)
                || uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            {
                issue = "invalid_endpoint";
                return false;
            }

            if (uri.Scheme == Uri.UriSchemeHttp && !uri.IsLoopback)
            {
                issue = "insecure_endpoint";
                return false;
            }

            return true;
        }

        private static string RemoveSuffix(string value, string suffix)
        {
            return value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
                ? value.Substring(0, value.Length - suffix.Length)
                : value;
        }
    }
}
