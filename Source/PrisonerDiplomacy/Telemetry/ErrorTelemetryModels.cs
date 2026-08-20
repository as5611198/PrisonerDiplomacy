using System.Collections.Generic;
using System.Runtime.Serialization;

namespace PrisonerDiplomacy.Telemetry
{
    [DataContract]
    internal sealed class ErrorTelemetryPayload
    {
        [DataMember(Name = "schema_version", Order = 1)]
        public int SchemaVersion = 1;

        [DataMember(Name = "event_id", Order = 2)]
        public string EventId;

        [DataMember(Name = "error_hash", Order = 3)]
        public string ErrorHash;

        [DataMember(Name = "hash_algorithm", Order = 4)]
        public string HashAlgorithm = "sha256";

        [DataMember(Name = "captured_at_utc", Order = 5)]
        public string CapturedAtUtc;

        [DataMember(Name = "source", Order = 6)]
        public string Source;

        [DataMember(Name = "trust_level", Order = 7)]
        public string TrustLevel;

        [DataMember(Name = "operation", Order = 8)]
        public string Operation;

        [DataMember(Name = "mod_version", Order = 9)]
        public string ModVersion;

        [DataMember(Name = "game_version", Order = 10)]
        public string GameVersion;

        [DataMember(Name = "exception_type", Order = 11)]
        public string ExceptionType;

        [DataMember(Name = "message", Order = 12)]
        public string Message;

        [DataMember(Name = "stack_trace", Order = 13)]
        public string StackTrace;

        [DataMember(Name = "deal_context", Order = 14, EmitDefaultValue = false)]
        public ErrorTelemetryDealContext DealContext;

        [DataMember(Name = "active_mod_list", Order = 15)]
        public List<ErrorTelemetryModEntry> ActiveModList = new List<ErrorTelemetryModEntry>();
    }

    [DataContract]
    internal sealed class ErrorTelemetryDealContext
    {
        [DataMember(Name = "deal_id", Order = 1, EmitDefaultValue = false)]
        public string DealId;

        [DataMember(Name = "state", Order = 2, EmitDefaultValue = false)]
        public string State;

        [DataMember(Name = "origin", Order = 3, EmitDefaultValue = false)]
        public string Origin;

        [DataMember(Name = "negotiation_round", Order = 4)]
        public int NegotiationRound;

        [DataMember(Name = "prisoner_delivered", Order = 5)]
        public bool PrisonerDelivered;

        [DataMember(Name = "reward_issued", Order = 6)]
        public bool RewardIssued;
    }

    [DataContract]
    internal sealed class ErrorTelemetryModEntry
    {
        [DataMember(Name = "package_id", Order = 1)]
        public string PackageId;

        [DataMember(Name = "version", Order = 2)]
        public string Version;
    }

    internal sealed class PendingTelemetryError
    {
        public string Source;
        public string TrustLevel;
        public string Operation;
        public string ExceptionType;
        public string Message;
        public string StackTrace;
        public ErrorTelemetryDealContext DealContext;
    }
}
