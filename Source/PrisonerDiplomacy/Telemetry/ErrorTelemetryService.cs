using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using RimWorld;
using Verse;

namespace PrisonerDiplomacy.Telemetry
{
    internal static class ErrorTelemetryService
    {
        // Filled when the Cloudflare receiver is deployed. An empty endpoint keeps
        // the Workshop build completely offline without changing capture behavior.
        internal const string ProductionReportEndpoint = "";

        private const int MaximumPendingErrors = 32;
        private const int MaximumReportsPerSession = 20;
        private const int MaximumMessageCharacters = 4096;
        private const int MaximumStackCharacters = 32768;
        private const int MaximumMods = 512;
        private const int UploadAttempts = 3;

        private static readonly ConcurrentQueue<PendingTelemetryError> PendingErrors =
            new ConcurrentQueue<PendingTelemetryError>();
        private static readonly ConcurrentQueue<ErrorTelemetryPayload> PendingUploads =
            new ConcurrentQueue<ErrorTelemetryPayload>();
        private static readonly ConcurrentQueue<string> StatusMessages =
            new ConcurrentQueue<string>();
        private static readonly Queue<ErrorTelemetryPayload> PendingConsent =
            new Queue<ErrorTelemetryPayload>();
        private static readonly HashSet<string> SeenHashes =
            new HashSet<string>(StringComparer.Ordinal);
        private static readonly HttpClient HttpClient = CreateHttpClient();
        private static readonly Regex SourcePathPattern = new Regex(
            @"\s+in\s+[^\r\n]+:line\s+(\d+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly Regex WindowsPathPattern = new Regex(
            @"(?i)\b[A-Z]:\\[^\r\n\t]+",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex UnixHomePattern = new Regex(
            @"(?i)(?:/home|/Users)/[^/\s]+(?:/[^\r\n\t ]*)?",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex SecretPattern = new Regex(
            @"(?i)\b(api[_ -]?key|authorization|bearer|access[_ -]?token|secret)\b\s*[:=]?\s*[^\s,;]+",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static int mainThreadId;
        private static int pendingErrorCount;
        private static int uploadWorkerActive;
        private static int reportsQueued;
        private static int uploadFailureLogged;
        private static bool sessionConsent;
        private static bool consentDialogOpen;
        private static Uri configuredEndpoint;

        internal static bool IsUploadConfigured => TryGetReportEndpoint(out _);

        internal static void Initialize()
        {
            mainThreadId = Thread.CurrentThread.ManagedThreadId;
            configuredEndpoint = ResolveReportEndpoint();
        }

        internal static void CaptureException(
            Exception exception,
            string operation,
            PrisonerDeal deal = null,
            Pawn pawn = null,
            string source = "transaction_sentinel")
        {
            if (exception == null)
            {
                return;
            }

            Enqueue(new PendingTelemetryError
            {
                Source = source,
                TrustLevel = "high",
                Operation = NormalizeOperation(operation),
                ExceptionType = exception.GetType().FullName ?? exception.GetType().Name,
                Message = exception.Message ?? string.Empty,
                StackTrace = exception.StackTrace ?? string.Empty,
                DealContext = Thread.CurrentThread.ManagedThreadId == mainThreadId
                    ? SnapshotDeal(deal ?? ResolveDeal(pawn), operation)
                    : null
            });
        }

        internal static void DrainMainThread()
        {
            if (mainThreadId == 0)
            {
                mainThreadId = Thread.CurrentThread.ManagedThreadId;
            }
            if (Thread.CurrentThread.ManagedThreadId != mainThreadId)
            {
                return;
            }

            while (StatusMessages.TryDequeue(out string status))
            {
                Log.Warning("[Prisoner Diplomacy] " + status);
            }

            bool canPrompt = PrisonerDiplomacyMod.Settings?.EnableErrorTelemetryPrompts == true
                && TryGetReportEndpoint(out _);
            while (PendingErrors.TryDequeue(out PendingTelemetryError pending))
            {
                Interlocked.Decrement(ref pendingErrorCount);
                if (!canPrompt || reportsQueued >= MaximumReportsPerSession)
                {
                    continue;
                }

                ErrorTelemetryPayload payload = BuildPayload(pending);
                if (payload == null || !SeenHashes.Add(payload.ErrorHash))
                {
                    continue;
                }

                if (sessionConsent)
                {
                    QueueUpload(payload);
                }
                else
                {
                    PendingConsent.Enqueue(payload);
                }
            }

            TryShowConsentDialog();
        }

        private static void Enqueue(PendingTelemetryError pending)
        {
            if (pending == null || Interlocked.Increment(ref pendingErrorCount) > MaximumPendingErrors)
            {
                Interlocked.Decrement(ref pendingErrorCount);
                return;
            }

            PendingErrors.Enqueue(pending);
        }

        private static void TryShowConsentDialog()
        {
            if (consentDialogOpen || sessionConsent || PendingConsent.Count == 0
                || Find.WindowStack == null)
            {
                return;
            }

            consentDialogOpen = true;
            Find.WindowStack.Add(new Dialog_ErrorTelemetryConsent(HandleConsentDecision));
        }

        private static void HandleConsentDecision(ErrorTelemetryConsentDecision decision)
        {
            consentDialogOpen = false;
            if (PendingConsent.Count == 0)
            {
                return;
            }

            ErrorTelemetryPayload current = PendingConsent.Dequeue();
            if (decision == ErrorTelemetryConsentDecision.AllowSession)
            {
                sessionConsent = true;
                QueueUpload(current);
                while (PendingConsent.Count > 0 && reportsQueued < MaximumReportsPerSession)
                {
                    QueueUpload(PendingConsent.Dequeue());
                }
            }
            else if (decision == ErrorTelemetryConsentDecision.AllowOnce)
            {
                QueueUpload(current);
            }

            TryShowConsentDialog();
        }

        private static ErrorTelemetryPayload BuildPayload(PendingTelemetryError pending)
        {
            string exceptionType = Sanitize(pending.ExceptionType, 256);
            string operation = Sanitize(pending.Operation, 256);
            string message = Sanitize(pending.Message, MaximumMessageCharacters);
            string stack = SanitizeStack(pending.StackTrace);
            string frame = FirstRelevantModFrame(stack) ?? FirstStackLine(stack);
            string hash = ComputeHash(exceptionType + "\n" + operation + "\n" + frame);
            if (string.IsNullOrEmpty(hash))
            {
                return null;
            }

            return new ErrorTelemetryPayload
            {
                EventId = Guid.NewGuid().ToString("N"),
                ErrorHash = hash,
                CapturedAtUtc = DateTime.UtcNow.ToString("o"),
                Source = Sanitize(pending.Source, 64),
                TrustLevel = Sanitize(pending.TrustLevel, 16),
                Operation = operation,
                ModVersion = ModVersion(),
                GameVersion = GameVersion(),
                ExceptionType = exceptionType,
                Message = message,
                StackTrace = stack,
                DealContext = pending.DealContext,
                ActiveModList = SnapshotActiveMods()
            };
        }

        private static ErrorTelemetryDealContext SnapshotDeal(PrisonerDeal deal, string operation)
        {
            if (deal == null)
            {
                return string.IsNullOrEmpty(operation)
                    ? null
                    : new ErrorTelemetryDealContext { State = Sanitize(operation, 128) };
            }

            return new ErrorTelemetryDealContext
            {
                DealId = Sanitize(deal.DealId, 64),
                State = deal.State.ToString(),
                Origin = deal.Origin.ToString(),
                NegotiationRound = deal.NegotiationRound,
                PrisonerDelivered = deal.PrisonerDelivered,
                RewardIssued = deal.RewardIssued
            };
        }

        private static PrisonerDeal ResolveDeal(Pawn pawn)
        {
            try
            {
                return pawn == null ? null : PrisonerDiplomacyGameComponent.Current?.GetActiveDeal(pawn);
            }
            catch
            {
                return null;
            }
        }

        private static List<ErrorTelemetryModEntry> SnapshotActiveMods()
        {
            try
            {
                return LoadedModManager.RunningModsListForReading
                    .Where(mod => mod != null && !string.IsNullOrWhiteSpace(mod.PackageId))
                    .OrderBy(mod => mod.PackageId, StringComparer.OrdinalIgnoreCase)
                    .Take(MaximumMods)
                    .Select(mod => new ErrorTelemetryModEntry
                    {
                        PackageId = Sanitize(mod.PackageId, 160),
                        Version = Sanitize(mod.ModMetaData?.ModVersion ?? string.Empty, 64)
                    })
                    .ToList();
            }
            catch
            {
                return new List<ErrorTelemetryModEntry>();
            }
        }

        private static void QueueUpload(ErrorTelemetryPayload payload)
        {
            if (payload == null || reportsQueued >= MaximumReportsPerSession
                || !TryGetReportEndpoint(out _))
            {
                return;
            }

            reportsQueued++;
            PendingUploads.Enqueue(payload);
            StartUploadWorker();
        }

        private static void StartUploadWorker()
        {
            if (Interlocked.CompareExchange(ref uploadWorkerActive, 1, 0) != 0)
            {
                return;
            }

            Task.Run(async () =>
            {
                try
                {
                    while (PendingUploads.TryDequeue(out ErrorTelemetryPayload payload))
                    {
                        await UploadAsync(payload).ConfigureAwait(false);
                    }
                }
                finally
                {
                    Interlocked.Exchange(ref uploadWorkerActive, 0);
                    if (!PendingUploads.IsEmpty)
                    {
                        StartUploadWorker();
                    }
                }
            });
        }

        private static async Task UploadAsync(ErrorTelemetryPayload payload)
        {
            if (!TryGetReportEndpoint(out Uri endpoint)
                || !AiJsonUtility.TrySerialize(payload, out string json))
            {
                QueueUploadFailureStatus();
                return;
            }

            for (int attempt = 0; attempt < UploadAttempts; attempt++)
            {
                try
                {
                    using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, endpoint))
                    {
                        request.Headers.TryAddWithoutValidation("X-PD-Telemetry-Schema", "1");
                        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                        using (HttpResponseMessage response = await HttpClient.SendAsync(request).ConfigureAwait(false))
                        {
                            if (response.IsSuccessStatusCode)
                            {
                                return;
                            }

                            int status = (int)response.StatusCode;
                            if (status < 500 && response.StatusCode != HttpStatusCode.RequestTimeout
                                && (int)response.StatusCode != 429)
                            {
                                break;
                            }
                        }
                    }
                }
                catch (Exception exception) when (
                    exception is HttpRequestException
                    || exception is TaskCanceledException
                    || exception is OperationCanceledException)
                {
                    // Network failures never affect game state; retry below.
                }

                if (attempt + 1 < UploadAttempts)
                {
                    await Task.Delay(attempt == 0 ? 1000 : 3000).ConfigureAwait(false);
                }
            }

            QueueUploadFailureStatus();
        }

        private static void QueueUploadFailureStatus()
        {
            if (Interlocked.Exchange(ref uploadFailureLogged, 1) == 0)
            {
                StatusMessages.Enqueue("Anonymous error report upload failed; gameplay was not affected.");
            }
        }

        private static HttpClient CreateHttpClient()
        {
            HttpClient client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "PrisonerDiplomacy/1.2");
            return client;
        }

        private static bool TryGetReportEndpoint(out Uri endpoint)
        {
            endpoint = configuredEndpoint;
            return endpoint != null;
        }

        private static Uri ResolveReportEndpoint()
        {
            string configured = ProductionReportEndpoint;
            if (GenCommandLine.TryGetCommandLineArg("pdtelemetryendpoint", out string overrideEndpoint))
            {
                configured = overrideEndpoint;
            }
            if (!Uri.TryCreate(configured, UriKind.Absolute, out Uri candidate))
            {
                return null;
            }

            bool localHttp = candidate.Scheme == Uri.UriSchemeHttp
                && (candidate.IsLoopback || string.Equals(candidate.Host, "localhost", StringComparison.OrdinalIgnoreCase));
            if (candidate.Scheme != Uri.UriSchemeHttps && !localHttp)
            {
                return null;
            }

            return candidate;
        }

        private static string SanitizeStack(string value)
        {
            string sanitized = Sanitize(value, MaximumStackCharacters);
            sanitized = SourcePathPattern.Replace(sanitized, " in <redacted>:line $1");
            return Truncate(sanitized, MaximumStackCharacters);
        }

        internal static string Sanitize(string value, int maximumCharacters)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            string sanitized = value.Replace(Environment.UserName, "<user>")
                .Replace(Environment.MachineName, "<machine>");
            sanitized = SecretPattern.Replace(sanitized, "$1=<redacted>");
            sanitized = SourcePathPattern.Replace(sanitized, " in <redacted>:line $1");
            sanitized = WindowsPathPattern.Replace(sanitized, "<redacted-path>");
            sanitized = UnixHomePattern.Replace(sanitized, "<redacted-path>");
            return Truncate(sanitized, maximumCharacters);
        }

        private static string NormalizeOperation(string operation)
        {
            return string.IsNullOrWhiteSpace(operation) ? "unknown" : operation.Trim();
        }

        private static string FirstRelevantModFrame(string stack)
        {
            if (string.IsNullOrWhiteSpace(stack))
            {
                return null;
            }

            return stack.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .FirstOrDefault(line => line.IndexOf("PrisonerDiplomacy.", StringComparison.Ordinal) >= 0
                    && line.IndexOf("PrisonerDiplomacy.Telemetry.", StringComparison.Ordinal) < 0);
        }

        private static string FirstStackLine(string stack)
        {
            return stack?.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .FirstOrDefault() ?? string.Empty;
        }

        private static string ComputeHash(string value)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
                StringBuilder builder = new StringBuilder(bytes.Length * 2);
                foreach (byte item in bytes)
                {
                    builder.Append(item.ToString("x2"));
                }
                return builder.ToString();
            }
        }

        private static string ModVersion()
        {
            Assembly assembly = typeof(ErrorTelemetryService).Assembly;
            AssemblyFileVersionAttribute fileVersion = assembly
                .GetCustomAttributes(typeof(AssemblyFileVersionAttribute), false)
                .OfType<AssemblyFileVersionAttribute>()
                .FirstOrDefault();
            return fileVersion?.Version ?? assembly.GetName().Version?.ToString() ?? "unknown";
        }

        private static string GameVersion()
        {
            try
            {
                return VersionControl.CurrentVersionString ?? "unknown";
            }
            catch
            {
                return "unknown";
            }
        }

        private static string Truncate(string value, int maximumCharacters)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maximumCharacters)
            {
                return value ?? string.Empty;
            }
            return value.Substring(0, maximumCharacters);
        }

        internal static bool TryRunSelfTest(out string failure)
        {
            failure = null;
            string sanitized = Sanitize(
                @"C:\Users\TelemetryTester\save api_key=super-secret /home/tester/config",
                1024);
            if (sanitized.IndexOf("TelemetryTester", StringComparison.OrdinalIgnoreCase) >= 0
                || sanitized.IndexOf("super-secret", StringComparison.OrdinalIgnoreCase) >= 0
                || sanitized.IndexOf("/home/tester", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                failure = "sanitization_failed";
                return false;
            }

            string first = ComputeHash("type\noperation\nframe");
            string second = ComputeHash("type\noperation\nframe");
            if (first.Length != 64 || first != second)
            {
                failure = "hash_contract_failed";
                return false;
            }

            ErrorTelemetryPayload payload = new ErrorTelemetryPayload
            {
                EventId = Guid.Empty.ToString("N"),
                ErrorHash = first,
                CapturedAtUtc = DateTime.UtcNow.ToString("o"),
                Source = "self_test",
                TrustLevel = "high",
                Operation = "self_test",
                ModVersion = ModVersion(),
                GameVersion = "test",
                ExceptionType = "System.Exception",
                Message = "test",
                StackTrace = "at PrisonerDiplomacy.SelfTest.Run()"
            };
            if (!AiJsonUtility.TrySerialize(payload, out string json)
                || json.IndexOf("\"error_hash\"", StringComparison.Ordinal) < 0
                || json.IndexOf("\"active_mod_list\"", StringComparison.Ordinal) < 0)
            {
                failure = "payload_serialization_failed";
                return false;
            }

            if (FirstRelevantModFrame(
                    "at PrisonerDiplomacy.Telemetry.ErrorTelemetryService.Capture()\n"
                    + "at PrisonerDiplomacy.PrisonerValueCalculator.Calculate()")
                ?.IndexOf("PrisonerValueCalculator", StringComparison.Ordinal) < 0)
            {
                failure = "stack_filter_failed";
                return false;
            }

            return true;
        }
    }
}
