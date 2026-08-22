using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace PrisonerDiplomacy
{
    internal enum AiProviderOperationKind
    {
        ModelFetch,
        ConnectionTest
    }

    internal sealed class AiProviderOperationResult
    {
        public AiProviderOperationKind Kind;
        public bool Success;
        public string FailureCode;
        public List<string> Models;
    }

    internal static class AiProviderSettingsService
    {
        private const int MaximumModelListCharacters = 2097152;
        private static readonly ConcurrentQueue<AiProviderOperationResult> Results
            = new ConcurrentQueue<AiProviderOperationResult>();
        private static readonly object OperationLock = new object();
        private static CancellationTokenSource modelFetchCancellation;
        private static CancellationTokenSource connectionTestCancellation;
        private static int modelFetchGeneration;
        private static int connectionTestGeneration;
        private static volatile bool isFetchingModels;
        private static volatile bool isTestingConnection;

        public static bool IsFetchingModels
        {
            get { return isFetchingModels; }
        }

        public static bool IsTestingConnection
        {
            get { return isTestingConnection; }
        }

        public static void StartModelFetch(PrisonerDiplomacySettings settings)
        {
            if (settings == null || IsFetchingModels)
            {
                return;
            }

            AiNarrativeProviderConfig config = AiNarrativeService.SnapshotConfig(settings);
            if (!ValidateLookupConfiguration(config, true, out string issue))
            {
                Results.Enqueue(Failed(AiProviderOperationKind.ModelFetch, issue));
                return;
            }

            CancellationTokenSource cancellation = new CancellationTokenSource();
            cancellation.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, Math.Min(60, config.TimeoutSeconds))));
            int generation;
            lock (OperationLock)
            {
                modelFetchCancellation?.Cancel();
                modelFetchCancellation?.Dispose();
                modelFetchCancellation = cancellation;
                generation = ++modelFetchGeneration;
                isFetchingModels = true;
            }

            Task.Run(async () =>
            {
                AiProviderOperationResult result;
                try
                {
                    result = await FetchModelsAsync(config, cancellation.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    result = Failed(AiProviderOperationKind.ModelFetch, "timeout_or_cancelled");
                }
                catch
                {
                    result = Failed(AiProviderOperationKind.ModelFetch, "network_error");
                }

                lock (OperationLock)
                {
                    if (generation != modelFetchGeneration)
                    {
                        cancellation.Dispose();
                        return;
                    }

                    isFetchingModels = false;
                    modelFetchCancellation = null;
                }

                cancellation.Dispose();
                Results.Enqueue(result);
            });
        }

        public static void StartConnectionTest(PrisonerDiplomacySettings settings)
        {
            if (settings == null || IsTestingConnection)
            {
                return;
            }

            string configurationIssue = AiNarrativeService.ConfigurationIssue(settings, ignoreEnabled: true);
            if (configurationIssue != null)
            {
                Results.Enqueue(Failed(AiProviderOperationKind.ConnectionTest, configurationIssue));
                return;
            }

            AiNarrativeProviderConfig config = AiNarrativeService.SnapshotConfig(settings);
            CancellationTokenSource cancellation = new CancellationTokenSource();
            cancellation.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, Math.Min(60, config.TimeoutSeconds))));
            int generation;
            lock (OperationLock)
            {
                connectionTestCancellation?.Cancel();
                connectionTestCancellation?.Dispose();
                connectionTestCancellation = cancellation;
                generation = ++connectionTestGeneration;
                isTestingConnection = true;
            }

            Task.Run(async () =>
            {
                string failureCode;
                try
                {
                    failureCode = await AiNarrativeService.TestConfigurationAsync(config, cancellation.Token)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    failureCode = "timeout_or_cancelled";
                }
                catch
                {
                    failureCode = "network_error";
                }

                lock (OperationLock)
                {
                    if (generation != connectionTestGeneration)
                    {
                        cancellation.Dispose();
                        return;
                    }

                    isTestingConnection = false;
                    connectionTestCancellation = null;
                }

                cancellation.Dispose();
                Results.Enqueue(new AiProviderOperationResult
                {
                    Kind = AiProviderOperationKind.ConnectionTest,
                    Success = string.IsNullOrEmpty(failureCode),
                    FailureCode = failureCode
                });
            });
        }

        public static bool TryApplyNextResult(
            PrisonerDiplomacySettings settings,
            out AiProviderOperationResult result)
        {
            if (!Results.TryDequeue(out result))
            {
                return false;
            }

            if (result.Success
                && result.Kind == AiProviderOperationKind.ModelFetch
                && result.Models != null
                && settings != null)
            {
                settings.AiFetchedModels = result.Models;
                if (string.IsNullOrWhiteSpace(settings.AiModel) && result.Models.Count > 0)
                {
                    settings.AiModel = result.Models[0];
                    settings.RefreshLegacyAiEndpoint();
                }
            }

            return true;
        }

        public static void CancelOperations()
        {
            lock (OperationLock)
            {
                modelFetchGeneration++;
                connectionTestGeneration++;
                modelFetchCancellation?.Cancel();
                connectionTestCancellation?.Cancel();
                modelFetchCancellation?.Dispose();
                connectionTestCancellation?.Dispose();
                modelFetchCancellation = null;
                connectionTestCancellation = null;
                isFetchingModels = false;
                isTestingConnection = false;
            }

            while (Results.TryDequeue(out _))
            {
            }
        }

        internal static bool TryRunSelfTest(out string failure)
        {
            failure = null;
            string openAiResponse = "{\"data\":[{\"id\":\"model-b\"},{\"id\":\"model-a\"}]}";
            List<string> openAiModels = ParseOpenAiModelList(openAiResponse);
            if (openAiModels.Count != 2 || openAiModels[0] != "model-a" || openAiModels[1] != "model-b")
            {
                failure = "OpenAI-compatible model list was not parsed: count="
                    + openAiModels.Count + " values=" + string.Join(",", openAiModels);
                return false;
            }

            string googleResponse = "{\"models\":["
                + "{\"name\":\"models/gemini-test\",\"supportedGenerationMethods\":[\"generateContent\"]},"
                + "{\"name\":\"models/embed-test\",\"supportedGenerationMethods\":[\"embedContent\"]}]}";
            List<string> googleModels = ParseGoogleModelList(googleResponse);
            if (googleModels.Count != 1 || googleModels[0] != "gemini-test")
            {
                failure = "Google model list was not filtered: count="
                    + googleModels.Count + " values=" + string.Join(",", googleModels);
                return false;
            }

            return true;
        }

        private static bool ValidateLookupConfiguration(
            AiNarrativeProviderConfig config,
            bool useModelsEndpoint,
            out string issue)
        {
            issue = null;
            string endpoint = useModelsEndpoint ? config.ModelsEndpoint : config.Endpoint;
            if (!AiNarrativeProviderCatalog.TryValidateEndpoint(endpoint, out _, out issue))
            {
                return false;
            }

            if (config.RequireApiKey && string.IsNullOrWhiteSpace(config.ApiKey))
            {
                issue = "missing_api_key";
                return false;
            }

            return true;
        }

        private static async Task<AiProviderOperationResult> FetchModelsAsync(
            AiNarrativeProviderConfig config,
            CancellationToken cancellationToken)
        {
            string endpoint = config.ModelsEndpoint;
            if (config.Provider == AiNarrativeProviderKind.Google && !string.IsNullOrWhiteSpace(config.ApiKey))
            {
                endpoint += (endpoint.Contains("?") ? "&" : "?")
                    + "key=" + Uri.EscapeDataString(config.ApiKey);
            }

            using (HttpClient client = new HttpClient())
            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, endpoint))
            {
                client.Timeout = Timeout.InfiniteTimeSpan;
                request.Headers.UserAgent.ParseAdd("PrisonerDiplomacy/1.2.1");
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                if (config.Provider != AiNarrativeProviderKind.Google && !string.IsNullOrWhiteSpace(config.ApiKey))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);
                }

                using (HttpResponseMessage response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        return Failed(AiProviderOperationKind.ModelFetch, "http_" + (int)response.StatusCode);
                    }

                    if (response.Content.Headers.ContentLength.HasValue
                        && response.Content.Headers.ContentLength.Value > MaximumModelListCharacters)
                    {
                        return Failed(AiProviderOperationKind.ModelFetch, "invalid_response_size");
                    }

                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (string.IsNullOrWhiteSpace(body) || body.Length > MaximumModelListCharacters)
                    {
                        return Failed(AiProviderOperationKind.ModelFetch, "invalid_response_size");
                    }

                    List<string> models = config.Provider == AiNarrativeProviderKind.Google
                        ? ParseGoogleModelList(body)
                        : ParseOpenAiModelList(body);
                    if (models.Count == 0)
                    {
                        return Failed(AiProviderOperationKind.ModelFetch, "empty_model_list");
                    }

                    return new AiProviderOperationResult
                    {
                        Kind = AiProviderOperationKind.ModelFetch,
                        Success = true,
                        Models = models
                    };
                }
            }
        }

        private static List<string> ParseOpenAiModelList(string json)
        {
            if (!AiJsonUtility.TryDeserialize(json, out OpenAiModelListEnvelope envelope))
            {
                return new List<string>();
            }

            return (envelope?.data ?? Array.Empty<OpenAiModelItem>())
                .Select(item => item?.id?.Trim())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<string> ParseGoogleModelList(string json)
        {
            if (!AiJsonUtility.TryDeserialize(json, out GoogleModelListEnvelope envelope))
            {
                return new List<string>();
            }

            return (envelope?.models ?? Array.Empty<GoogleModelListItem>())
                .Where(item => item?.supportedGenerationMethods != null
                    && item.supportedGenerationMethods.Any(method => method == "generateContent"))
                .Select(item => AiNarrativeProviderCatalog.NormalizeGoogleModelName(item.name))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static AiProviderOperationResult Failed(AiProviderOperationKind kind, string failureCode)
        {
            return new AiProviderOperationResult
            {
                Kind = kind,
                FailureCode = failureCode ?? "unknown_error"
            };
        }
    }

    [Serializable]
    [DataContract]
    internal sealed class OpenAiModelListEnvelope
    {
        [DataMember(Name = "data")]
        public OpenAiModelItem[] data = Array.Empty<OpenAiModelItem>();
    }

    [Serializable]
    [DataContract]
    internal sealed class OpenAiModelItem
    {
        [DataMember(Name = "id")]
        public string id = string.Empty;
    }

    [Serializable]
    [DataContract]
    internal sealed class GoogleModelListEnvelope
    {
        [DataMember(Name = "models")]
        public GoogleModelListItem[] models = Array.Empty<GoogleModelListItem>();
    }

    [Serializable]
    [DataContract]
    internal sealed class GoogleModelListItem
    {
        [DataMember(Name = "name")]
        public string name = string.Empty;
        [DataMember(Name = "supportedGenerationMethods")]
        public string[] supportedGenerationMethods = Array.Empty<string>();
    }
}
