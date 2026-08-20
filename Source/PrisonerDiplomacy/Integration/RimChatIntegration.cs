using System;
using RimWorld;
using Verse;

namespace PrisonerDiplomacy
{
    public enum PrisonerRansomSystemOwner
    {
        PrisonerDiplomacy,
        RimChat,
        SafeIsolation
    }

    public enum RimChatCompatibilityStatus
    {
        NotInstalled,
        DetectedUnverified,
        CompatibleVersionBridge,
        IncompatibleVersion
    }

    public static class RimChatIntegration
    {
        public const string PackageId = "yancy.rimchat";
        private const string SupportedPrivateBridgeVersion = "1.5.12";
        private static bool refreshed;
        private static ModMetaData activeMod;
        private static RimChatCompatibilityStatus status = RimChatCompatibilityStatus.NotInstalled;
        private static string version;

        public static bool IsInstalled => status != RimChatCompatibilityStatus.NotInstalled;
        public static ModMetaData ActiveMod => activeMod;
        public static RimChatCompatibilityStatus Status => status;
        public static string Version => version ?? string.Empty;
        public static bool HasVerifiedBridge => status == RimChatCompatibilityStatus.CompatibleVersionBridge;

        public static void Refresh()
        {
            activeMod = null;
            version = string.Empty;
            status = RimChatCompatibilityStatus.NotInstalled;
            refreshed = true;

            try
            {
                activeMod = ModLister.GetActiveModWithIdentifier(PackageId);
                if (activeMod == null)
                {
                    return;
                }

                version = activeMod.ModVersion ?? string.Empty;
                // The bridge is intentionally limited to an exact known build. A matching
                // executor signature is still required before any RimChat ransom Action is touched.
                status = string.Equals(version, SupportedPrivateBridgeVersion, StringComparison.OrdinalIgnoreCase)
                    ? RimChatCompatibilityStatus.CompatibleVersionBridge
                    : RimChatCompatibilityStatus.IncompatibleVersion;
            }
            catch (Exception exception)
            {
                status = RimChatCompatibilityStatus.DetectedUnverified;
                Log.Warning("[Prisoner Diplomacy] RimChat detection failed; using safe compatibility behavior: " + exception.Message);
            }
        }

        public static void EnsureRefreshed()
        {
            if (!refreshed)
            {
                Refresh();
            }
        }

        public static string StatusLabelKey()
        {
            EnsureRefreshed();
            switch (status)
            {
                case RimChatCompatibilityStatus.NotInstalled:
                    return "PD_RimChatStatusNotInstalled";
                case RimChatCompatibilityStatus.CompatibleVersionBridge:
                    return "PD_RimChatStatusCompatible";
                case RimChatCompatibilityStatus.IncompatibleVersion:
                    return "PD_RimChatStatusIncompatible";
                default:
                    return "PD_RimChatStatusUnverified";
            }
        }

        public static bool IsPawnReservedByVerifiedExternalDeal(Pawn pawn)
        {
            // Deliberately returns false until a stable public adapter is available. In the
            // unverified state the game component uses safe isolation instead of guessing.
            return false;
        }

        public static bool IsOwnerAvailable(PrisonerRansomSystemOwner owner)
        {
            EnsureRefreshed();
            return owner != PrisonerRansomSystemOwner.RimChat || IsInstalled;
        }

        public static PrisonerRansomSystemOwner EffectiveOwner
        {
            get
            {
                EnsureRefreshed();
                PrisonerRansomSystemOwner configured = PrisonerDiplomacyMod.Settings?.RansomSystemOwner
                    ?? PrisonerRansomSystemOwner.PrisonerDiplomacy;
                if (IsInstalled && !HasVerifiedBridge)
                {
                    return PrisonerRansomSystemOwner.SafeIsolation;
                }
                return configured == PrisonerRansomSystemOwner.RimChat && !IsInstalled
                    ? PrisonerRansomSystemOwner.PrisonerDiplomacy
                    : configured;
            }
        }

        public static bool AllowsNewPrisonerDiplomacyDeals =>
            EffectiveOwner != PrisonerRansomSystemOwner.RimChat;

        public static bool RequiresCompatibilityWarning =>
            IsInstalled && (!HasVerifiedBridge || EffectiveOwner == PrisonerRansomSystemOwner.SafeIsolation);

        public static void MarkBridgeUnavailable()
        {
            if (IsInstalled)
            {
                status = RimChatCompatibilityStatus.IncompatibleVersion;
            }
        }

        public static string OwnerLabelKey(PrisonerRansomSystemOwner owner)
        {
            switch (owner)
            {
                case PrisonerRansomSystemOwner.RimChat:
                    return "PD_RansomOwnerRimChat";
                case PrisonerRansomSystemOwner.SafeIsolation:
                    return "PD_RansomOwnerSafeIsolation";
                default:
                    return "PD_RansomOwnerPrisonerDiplomacy";
            }
        }

    }
}
