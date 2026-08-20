using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace PrisonerDiplomacy
{
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    internal static class Pawn_GetGizmos_Patch
    {
        private static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
        {
            if (__result != null)
            {
                foreach (Gizmo gizmo in __result)
                {
                    if (gizmo != null)
                    {
                        yield return gizmo;
                    }
                }
            }

            PrisonerDiplomacyGameComponent component = PrisonerDiplomacyGameComponent.Current;
            PrisonerDeal deal = component?.GetActiveDeal(__instance);
            PrisonerRecord record = component?.GetRecord(__instance);
            if (record != null && __instance.IsPrisonerOfColony)
            {
                yield return new Command_Action
                {
                    defaultLabel = "PD_PrisonerStatus".Translate(),
                    defaultDesc = "PD_PrisonerStatusDesc".Translate(),
                    icon = TexCommand.OpenLinkedQuestTex,
                    action = () => Find.WindowStack.Add(new Dialog_MessageBox(PrisonerDiplomacyUIUtility.BuildPrisonerStatus(record, deal)))
                };
            }

            if (component != null && __instance.IsColonist && __instance.MapHeld != null
                && component.HasPortableDiplomacyTerminal(__instance)
                && component.GetKnownNegotiationFactions(__instance.MapHeld).Count > 0)
            {
                yield return new Command_Action
                {
                    defaultLabel = "PD_UiPortableTerminal".Translate(),
                    defaultDesc = "PD_UiPortableTerminalDesc".Translate(),
                    icon = TexCommand.OpenLinkedQuestTex,
                    action = () => Find.WindowStack.Add(
                        new Window_PrisonerDiplomacyFactionBrowser(__instance, __instance.MapHeld))
                };
            }

            if (deal == null || (deal.State != DealState.AcceptedAwaitingRelease && deal.State != DealState.ReleaseOrdered))
            {
                yield break;
            }

            if (deal.State == DealState.ReleaseOrdered)
            {
                Command_Action ordered = new Command_Action
                {
                    defaultLabel = "PD_ReleaseAlreadyOrdered".Translate(),
                    defaultDesc = "PD_ReleaseAlreadyOrderedDesc".Translate(),
                    icon = TexCommand.ReleaseAnimals
                };
                ordered.Disable("PD_ReleaseAlreadyOrderedDesc".Translate());
                yield return ordered;
                yield break;
            }

            Command_Action command = new Command_Action
            {
                defaultLabel = deal.ReturnedHostage != null
                    ? "PD_ReleaseForExchange".Translate()
                    : "PD_ReleaseForRansom".Translate(),
                defaultDesc = deal.ReturnedHostage != null
                    ? "PD_ReleaseForExchangeDesc".Translate(
                        __instance.LabelShortCap,
                        deal.ReturnedHostage.LabelShortCap)
                    : "PD_ReleaseForRansomDesc".Translate(__instance.LabelShortCap),
                icon = TexCommand.ReleaseAnimals,
                action = () =>
                {
                    if (deal.PirateRisk != PirateDealRisk.None)
                    {
                        Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                            "PD_PirateReleaseConfirmation".Translate(
                                deal.Faction?.NameColored ?? "?",
                                deal.Rewards?.Description() ?? "PD_RewardNone".Translate(),
                                FactionNegotiationUtility.RiskDescription(deal.PirateRisk)),
                            () => component.OrderRansomRelease(__instance),
                            false,
                            null,
                            WindowLayer.Dialog));
                    }
                    else
                    {
                        component.OrderRansomRelease(__instance);
                    }
                }
            };
            if (__instance.MapHeld == null)
            {
                command.Disable("PD_NoSafeMap".Translate());
            }
            yield return command;

            if (deal.PirateRisk != PirateDealRisk.None && deal.State == DealState.AcceptedAwaitingRelease)
            {
                yield return new Command_Action
                {
                    defaultLabel = "PD_SecurePirateDeal".Translate(),
                    defaultDesc = "PD_SecurePirateDealDesc".Translate(),
                    icon = TexCommand.ForbidOff,
                    action = () => Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        "PD_SecurePirateDealConfirmation".Translate(
                            deal.Rewards?.Description() ?? "PD_RewardNone".Translate()),
                        () => component.TrySecurePirateDeal(__instance),
                        false,
                        null,
                        WindowLayer.Dialog))
                };
                yield return new Command_Action
                {
                    defaultLabel = "PD_CancelRiskyDeal".Translate(),
                    defaultDesc = "PD_CancelRiskyDealDesc".Translate(),
                    icon = TexCommand.ClearPrioritizedWork,
                    action = () => Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        "PD_CancelRiskyDealConfirmation".Translate(__instance.LabelShortCap),
                        () => component.CancelAcceptedDeal(__instance),
                        true,
                        null,
                        WindowLayer.Dialog))
                };
            }
        }
    }

    [HarmonyPatch(typeof(IncidentWorker_Raid), "TryGenerateRaidInfo")]
    internal static class IncidentWorker_Raid_TryGenerateRaidInfo_Patch
    {
        private static bool Prefix(
            IncidentParms parms,
            ref System.Collections.Generic.List<Pawn> pawns,
            ref bool __result)
        {
            if (parms?.faction == null
                || RaidGenerationUtility.HasUsablePawnGroupMaker(parms))
            {
                return true;
            }

            // Some custom factions define diplomacy and pawn kinds but no Combat
            // PawnGroupMaker. Stop before vanilla's iterator emits a red error.
            pawns = new System.Collections.Generic.List<Pawn>();
            __result = false;
            Log.Message(
                "[Prisoner Diplomacy] Skipped raid generation for faction "
                + parms.faction.Name
                + " because no usable Combat PawnGroupMaker was available.");
            return false;
        }
    }

    [HarmonyPatch(typeof(IncidentWorker_RaidEnemy), "TryResolveRaidFaction")]
    internal static class IncidentWorker_RaidEnemy_TryResolveRaidFaction_Patch
    {
        private static bool Prefix(IncidentParms parms, ref bool __result)
        {
            if (!CausalRaidContext.Active)
            {
                return true;
            }

            // Causal events must never fall through to vanilla's random faction
            // fallback. That fallback is correct for ordinary raids, but it can
            // silently turn a rescue or retaliation into another faction's raid.
            Faction faction = CausalRaidContext.Faction;
            if (parms?.faction != faction
                || faction == null
                || faction.defeated
                || !faction.HostileTo(Faction.OfPlayer)
                || !RaidGenerationUtility.HasUsablePawnGroupMaker(parms))
            {
                __result = false;
                return false;
            }

            __result = true;
            return false;
        }

        private static void Postfix(IncidentWorker_RaidEnemy __instance, IncidentParms parms, ref bool __result)
        {
            if (__result && PrisonerDiplomacyGameComponent.Current?.ShouldAllowResolvedRaid(__instance, parms) == false)
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(Faction), nameof(Faction.Notify_MemberTookDamage))]
    internal static class Faction_Notify_MemberTookDamage_Patch
    {
        private static void Postfix(Faction __instance, Pawn member, DamageInfo dinfo)
        {
            PrisonerDiplomacyGameComponent.Current?.NotifyPlayerAttackAgainstFaction(
                __instance,
                member?.LabelShortCap,
                dinfo);
        }
    }

    [HarmonyPatch(typeof(Faction), nameof(Faction.Notify_BuildingTookDamage))]
    internal static class Faction_Notify_BuildingTookDamage_Patch
    {
        private static void Postfix(Faction __instance, Building building, DamageInfo dinfo)
        {
            PrisonerDiplomacyGameComponent.Current?.NotifyPlayerAttackAgainstFaction(
                __instance,
                building?.LabelCap,
                dinfo);
        }
    }

    [HarmonyPatch(typeof(Building_CommsConsole), "GetCommTargets")]
    internal static class Building_CommsConsole_GetCommTargets_Patch
    {
        private static IEnumerable<ICommunicable> Postfix(IEnumerable<ICommunicable> __result, Building_CommsConsole __instance)
        {
            if (__result != null)
            {
                foreach (ICommunicable target in __result)
                {
                    if (target != null)
                    {
                        yield return target;
                    }
                }
            }

            PrisonerDiplomacyGameComponent component = PrisonerDiplomacyGameComponent.Current;
            if (component == null || __instance.Map == null)
            {
                yield break;
            }

            foreach (PrisonerDiplomacyCommTarget target in component.GetCommTargets(__instance.Map))
            {
                yield return target;
            }
        }
    }

    [HarmonyPatch(typeof(GenGuest), nameof(GenGuest.PrisonerRelease))]
    internal static class GenGuest_PrisonerRelease_Patch
    {
        private static void Postfix(Pawn __0)
        {
            PrisonerDiplomacyGameComponent.Current?.NotifyVanillaRelease(__0);
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.ExitMap))]
    internal static class Pawn_ExitMap_Patch
    {
        private struct ExitState
        {
            public Map Map;
            public bool WasVanillaReleased;
        }

        private static void Prefix(Pawn __instance, out ExitState __state)
        {
            __state = new ExitState
            {
                Map = __instance.Map,
                WasVanillaReleased = __instance.guest != null && __instance.guest.Released
            };
        }

        private static void Postfix(Pawn __instance, ExitState __state)
        {
            PrisonerDiplomacyGameComponent.Current?.NotifyPawnExited(__instance, __state.Map, __state.WasVanillaReleased);
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.PreTraded))]
    internal static class Pawn_PreTraded_Patch
    {
        private static void Prefix(Pawn __instance, TradeAction action)
        {
            if (action == TradeAction.PlayerSells)
            {
                PrisonerDiplomacyGameComponent.Current?.NotifyPawnSold(__instance);
            }
        }
    }

    [HarmonyPatch(typeof(Recipe_RemoveBodyPart), "ApplyOnPawn")]
    internal static class Recipe_RemoveBodyPart_ApplyOnPawn_Patch
    {
        private static void Postfix(Pawn pawn)
        {
            PrisonerDiplomacyGameComponent.Current?.NotifyBodyPartRemoved(pawn);
        }
    }

    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.PostApplyDamage))]
    internal static class Pawn_HealthTracker_PostApplyDamage_Patch
    {
        private static void Prefix(Pawn_HealthTracker __instance, out int __state)
        {
            Pawn pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
            __state = PrisonerTreatmentUtility.CountMissingParts(pawn);
        }

        private static void Postfix(Pawn_HealthTracker __instance, DamageInfo dinfo, int __state)
        {
            Pawn pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
            PrisonerDiplomacyGameComponent.Current?.NotifyPrisonerTookDamage(pawn, dinfo);
            PrisonerDiplomacyGameComponent.Current?.NotifyPlayerCausedPermanentHarm(pawn, __state, dinfo);
        }
    }

    [HarmonyPatch(typeof(TendUtility), nameof(TendUtility.DoTend), new[] { typeof(Pawn), typeof(Pawn), typeof(Medicine) })]
    internal static class TendUtility_DoTend_Patch
    {
        private static void Postfix(Pawn doctor, Pawn patient)
        {
            PrisonerDiplomacyGameComponent.Current?.NotifyPlayerMedicalTreatment(patient, doctor);
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    internal static class Pawn_Kill_Patch
    {
        private static void Prefix(Pawn __instance, DamageInfo? dinfo)
        {
            PrisonerDiplomacyGameComponent.Current?.NotifyPawnKilled(__instance, dinfo);
        }
    }

    [HarmonyPatch(typeof(Pawn_GuestTracker), nameof(Pawn_GuestTracker.Notify_PawnRecruited))]
    internal static class Pawn_GuestTracker_Notify_PawnRecruited_Patch
    {
        private static void Prefix(Pawn_GuestTracker __instance)
        {
            Pawn pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
            PrisonerDiplomacyGameComponent.Current?.NotifyPawnJoinedPlayer(pawn, false);
        }
    }

    [HarmonyPatch(typeof(Pawn_GuestTracker), nameof(Pawn_GuestTracker.SetGuestStatus))]
    internal static class Pawn_GuestTracker_SetGuestStatus_Patch
    {
        private static void Prefix(Pawn_GuestTracker __instance, Faction newHost, GuestStatus guestStatus)
        {
            if (newHost == Faction.OfPlayer && guestStatus == GuestStatus.Slave)
            {
                Pawn pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
                PrisonerDiplomacyGameComponent.Current?.NotifyPawnJoinedPlayer(pawn, true);
            }
        }
    }

    [HarmonyPatch(typeof(FactionUIUtility), nameof(FactionUIUtility.DrawRelatedFactionInfo))]
    internal static class FactionUIUtility_DrawRelatedFactionInfo_Patch
    {
        private static void Postfix(Rect rect, Faction faction, ref float curY)
        {
            string text = PrisonerDiplomacyGameComponent.Current?.GetFactionMemoryPageText(faction);
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            float height = Text.CalcHeight(text, rect.width);
            curY += 8f;
            Widgets.Label(new Rect(rect.x, curY, rect.width, height), text);
            curY += height;
        }
    }
}
