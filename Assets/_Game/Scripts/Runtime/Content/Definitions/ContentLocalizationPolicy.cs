namespace SM.Content.Definitions;

public enum ContentLocalizationPhaseValue
{
    PhaseA = 0,
    PhaseB = 1,
    PhaseC = 2,
}

public static class ContentLocalizationPolicy
{
    public const ContentLocalizationPhaseValue CurrentPhase = ContentLocalizationPhaseValue.PhaseB;

    public static bool TreatsMissingLocalizationAsError =>
        CurrentPhase is ContentLocalizationPhaseValue.PhaseB or ContentLocalizationPhaseValue.PhaseC;

    public static bool TreatsLegacyTextAsError =>
        CurrentPhase is ContentLocalizationPhaseValue.PhaseB or ContentLocalizationPhaseValue.PhaseC;

    public static bool AllowsRuntimeFallback(bool isDevelopmentContext)
    {
        return CurrentPhase switch
        {
            ContentLocalizationPhaseValue.PhaseA => true,
            ContentLocalizationPhaseValue.PhaseB => isDevelopmentContext,
            _ => false,
        };
    }
}
