namespace PrisonerDiplomacy
{
    public enum DealState
    {
        Offered,
        AcceptedAwaitingRelease,
        ReleaseOrdered,
        FulfillmentPending,
        Completed,
        Rejected,
        Expired,
        Cancelled,
        FailedPrisonerDead,
        FailedEscaped,
        FailedRecruited,
        FailedEnslaved,
        FailedSoldOrTransferred,
        FailedFactionInvalid,
        Negotiating,
        FailedHostageInvalid
    }

    public enum PrisonerImportance
    {
        Regular,
        Specialist,
        Notable,
        Core,
        Leader
    }

    public enum DealOrigin
    {
        FactionOffer,
        PlayerDemand
    }

    public enum NegotiationOutcome
    {
        Accepted,
        Rejected,
        Countered
    }

    public enum DemandAssessment
    {
        VeryFavorable,
        Reasonable,
        Ambitious,
        Extreme
    }

    public enum NegotiationMode
    {
        Ransom,
        PrisonerExchange
    }

    public enum FactionNegotiationType
    {
        NonNegotiating,
        Transactional,
        Diplomatic
    }

    public enum PirateDealRisk
    {
        None,
        DelayedPayment,
        RescueRaid,
        JailbreakIncitement,
        Ambush
    }

    public enum StrategicFollowupKind
    {
        PositiveGift,
        RescueRaid,
        RetaliationRaid,
        PirateAmbush
    }

    public enum FactionNegotiationOverride
    {
        Automatic,
        NonNegotiating,
        Transactional,
        Diplomatic
    }
}
