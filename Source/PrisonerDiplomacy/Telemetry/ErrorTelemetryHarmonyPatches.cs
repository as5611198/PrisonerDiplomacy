using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace PrisonerDiplomacy.Telemetry
{
    [HarmonyPatch]
    internal static class PrisonerDiplomacyCriticalMethodTelemetryPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            Type component = typeof(PrisonerDiplomacyGameComponent);
            return new[]
            {
                AccessTools.Method(typeof(PrisonerValueCalculator), nameof(PrisonerValueCalculator.Calculate),
                    new[] { typeof(Pawn), typeof(float), typeof(PrisonerImportance) }),
                AccessTools.Method(typeof(PrisonerValueCalculator), nameof(PrisonerValueCalculator.CalculateOffer),
                    new[] { typeof(PrisonerRecord), typeof(float) }),
                AccessTools.Method(typeof(PrisonerNegotiationUtility), nameof(PrisonerNegotiationUtility.Evaluate),
                    new[]
                    {
                        typeof(PrisonerRecord), typeof(Pawn), typeof(RewardDemand), typeof(int),
                        typeof(int), typeof(int), typeof(float), typeof(float)
                    }),
                AccessTools.Method(component, nameof(PrisonerDiplomacyGameComponent.SubmitPlayerDemand),
                    new[]
                    {
                        typeof(PrisonerRecord), typeof(Pawn), typeof(RewardDemand), typeof(string), typeof(string)
                    }),
                AccessTools.Method(component, nameof(PrisonerDiplomacyGameComponent.AcceptCounterOffer),
                    new[] { typeof(PrisonerDeal) }),
                AccessTools.Method(component, nameof(PrisonerDiplomacyGameComponent.AcceptDeal),
                    new[] { typeof(string) }),
                AccessTools.Method(component, nameof(PrisonerDiplomacyGameComponent.OrderRansomRelease),
                    new[] { typeof(Pawn) }),
                AccessTools.Method(component, "FulfillDeal", new[] { typeof(PrisonerDeal) }),
                AccessTools.Method(component, nameof(PrisonerDiplomacyGameComponent.GameComponentTick)),
                AccessTools.Method(component, nameof(PrisonerDiplomacyGameComponent.GameComponentUpdate)),
                AccessTools.Method(component, nameof(PrisonerDiplomacyGameComponent.StartedNewGame)),
                AccessTools.Method(typeof(PrisonerExchangeUtility), nameof(PrisonerExchangeUtility.TryReturnHostage))
            }.Where(method => method != null);
        }

        private static Exception Finalizer(Exception __exception, MethodBase __originalMethod, object[] __args)
        {
            if (__exception == null)
            {
                return null;
            }

            PrisonerDeal deal = __args?.OfType<PrisonerDeal>().FirstOrDefault();
            Pawn pawn = __args?.OfType<Pawn>().FirstOrDefault();
            if (deal == null)
            {
                PrisonerRecord record = __args?.OfType<PrisonerRecord>().FirstOrDefault();
                pawn = pawn ?? record?.Pawn;
            }

            string operation = (__originalMethod?.DeclaringType?.FullName ?? "PrisonerDiplomacy")
                + "." + (__originalMethod?.Name ?? "unknown");
            ErrorTelemetryService.CaptureException(__exception, operation, deal, pawn);
            return __exception;
        }
    }

}
