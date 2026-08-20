using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace PrisonerDiplomacy
{
    internal static class RimChatHarmonyPatches
    {
        private const string ActionTypeName = "RimChat.AI.AIAction";
        private const string ExecutorTypeName = "RimChat.AI.AIActionExecutor";
        private const string ResultTypeName = "RimChat.AI.ActionResult";
        private static bool installed;

        public static bool IsInstalled => installed;

        public static bool TryRunSmokeTest(out string failure)
        {
            failure = null;
            if (!installed)
            {
                failure = "RimChat bridge is not installed";
                return false;
            }

            try
            {
                Type actionType = AccessTools.TypeByName(ActionTypeName);
                Type executorType = AccessTools.TypeByName(ExecutorTypeName);
                Type resultType = AccessTools.TypeByName(ResultTypeName);
                object action = Activator.CreateInstance(actionType);
                AccessTools.Property(actionType, "ActionType").SetValue(action, "pay_prisoner_ransom", null);
                AccessTools.Property(actionType, "Parameters").SetValue(
                    action,
                    new Dictionary<string, object>
                    {
                        { "target_pawn_load_id", "PD-Smoke-NoSuchPawn" },
                        { "offer_silver", 100 }
                    },
                    null);
                object executor = Activator.CreateInstance(executorType, Faction.OfPlayer, false);
                MethodInfo executeAction = AccessTools.Method(executorType, "ExecuteAction", new[] { actionType });
                object result = executeAction.Invoke(executor, new[] { action });
                bool success = (bool)AccessTools.Property(resultType, "IsSuccess").GetValue(result, null);
                if (success || result == null)
                {
                    failure = "RimChat ransom action was not rejected";
                    return false;
                }
                return true;
            }
            catch (Exception exception)
            {
                failure = exception.GetBaseException().Message;
                return false;
            }
        }

        public static void TryInstall(Harmony harmony)
        {
            installed = false;
            RimChatIntegration.Refresh();
            if (!RimChatIntegration.IsInstalled)
            {
                return;
            }

            try
            {
                Type executorType = AccessTools.TypeByName(ExecutorTypeName);
                Type actionType = AccessTools.TypeByName(ActionTypeName);
                Type resultType = AccessTools.TypeByName(ResultTypeName);
                MethodInfo executeAction = executorType == null || actionType == null
                    ? null
                    : AccessTools.Method(executorType, "ExecuteAction", new[] { actionType });
                MethodInfo resultFailure = resultType == null
                    ? null
                    : AccessTools.Method(resultType, "Failure", new[] { typeof(string) });
                if (executeAction == null || resultFailure == null)
                {
                    RimChatIntegration.MarkBridgeUnavailable();
                    Log.Warning("[Prisoner Diplomacy] RimChat 1.5.12 bridge signature mismatch; using safe isolation.");
                    return;
                }

                harmony.Patch(executeAction, prefix: new HarmonyMethod(
                    typeof(RimChatHarmonyPatches), nameof(PrefixExecuteAction)));
                installed = true;
                Log.Message("[Prisoner Diplomacy] RimChat ransom conflict bridge enabled for version "
                    + RimChatIntegration.Version + ". mode=" + RimChatIntegration.EffectiveOwner + ".");
            }
            catch (Exception exception)
            {
                RimChatIntegration.MarkBridgeUnavailable();
                Log.Warning("[Prisoner Diplomacy] RimChat bridge could not be installed; using safe isolation: " + exception);
            }
        }

        private static bool PrefixExecuteAction(object action, MethodBase __originalMethod, ref object __result)
        {
            try
            {
                if (!IsRansomAction(action))
                {
                    return true;
                }

                PrisonerDiplomacySettings settings = PrisonerDiplomacyMod.Settings;
                PrisonerRansomSystemOwner owner = RimChatIntegration.EffectiveOwner;
                Pawn target = TryResolveTargetPawn(action);
                bool hasPrisonerDiplomacyDeal = target != null
                    && PrisonerDiplomacyGameComponent.Current?.GetActiveDeal(target) != null;

                if (owner == PrisonerRansomSystemOwner.RimChat && !hasPrisonerDiplomacyDeal)
                {
                    return true;
                }

                string messageKey = hasPrisonerDiplomacyDeal
                    ? "PD_RimChatBlockedActiveDeal"
                    : "PD_RimChatBlockedOwnedByPD";
                __result = CreateFailureResult(__originalMethod, messageKey.Translate());
                return __result == null;
            }
            catch (Exception exception)
            {
                Log.Warning("[Prisoner Diplomacy] RimChat ransom guard failed open to avoid breaking unrelated dialogue: " + exception.Message);
                return true;
            }
        }

        private static bool IsRansomAction(object action)
        {
            string actionType = AccessTools.Property(action?.GetType(), "ActionType")?.GetValue(action, null) as string;
            return string.Equals(actionType, "pay_prisoner_ransom", StringComparison.OrdinalIgnoreCase);
        }

        private static Pawn TryResolveTargetPawn(object action)
        {
            object value = GetParameter(action, "target_pawn_load_id");
            if (value == null)
            {
                return null;
            }

            string raw = value.ToString();
            if (string.IsNullOrEmpty(raw))
            {
                return null;
            }

            IEnumerable<Pawn> pawns = Find.Maps
                .Where(map => map?.mapPawns != null)
                .SelectMany(map => map.mapPawns.AllPawnsSpawned)
                .Where(pawn => pawn != null);
            return pawns.FirstOrDefault(pawn =>
                string.Equals(pawn.thingIDNumber.ToString(), raw, StringComparison.OrdinalIgnoreCase)
                || string.Equals(pawn.GetUniqueLoadID(), raw, StringComparison.OrdinalIgnoreCase));
        }

        private static object GetParameter(object action, string key)
        {
            object parameters = AccessTools.Property(action?.GetType(), "Parameters")?.GetValue(action, null);
            if (!(parameters is IDictionary dictionary))
            {
                return null;
            }

            foreach (DictionaryEntry entry in dictionary)
            {
                if (string.Equals(entry.Key?.ToString(), key, StringComparison.OrdinalIgnoreCase))
                {
                    return entry.Value;
                }
            }
            return null;
        }

        private static object CreateFailureResult(MethodBase originalMethod, string message)
        {
            Type resultType = (originalMethod as MethodInfo)?.ReturnType;
            MethodInfo failure = resultType == null
                ? null
                : AccessTools.Method(resultType, "Failure", new[] { typeof(string) });
            return failure?.Invoke(null, new object[] { message });
        }
    }
}
