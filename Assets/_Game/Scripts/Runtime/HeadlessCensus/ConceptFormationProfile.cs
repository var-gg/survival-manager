using System;

namespace SM.HeadlessCensus;

internal static class ConceptFormationProfile
{
    public const string FortifiedLine = "fortified_line";
    public const string ForwardSpear = "forward_spear";
    public const string BaitedGap = "baited_gap";
    public const string ScreenedBackline = "screened_backline";
    public const string OpenSkirmish = "open_skirmish";

    public static string Classify(FormationFeatures features)
    {
        if (features.ProtectedSlotCount >= 2 && features.BacklineAccessibility <= 0.75d)
        {
            return FortifiedLine;
        }

        if (features.FrontlineCount >= 3 && features.BacklineAccessibility >= 0.75d)
        {
            return ForwardSpear;
        }

        if (features.FlankRearExposureScore >= 4d)
        {
            return BaitedGap;
        }

        if (features.ProtectedSlotCount >= 1 && features.FrontlineCount >= 2)
        {
            return ScreenedBackline;
        }

        return OpenSkirmish;
    }

    public static string Predicate(string profile)
        => profile switch
        {
            FortifiedLine => "formation.protected_slot_count>=2 and formation.backline_accessibility<=0.75",
            ForwardSpear => "formation.frontline_count>=3 and formation.backline_accessibility>=0.75",
            BaitedGap => "formation.flank_rear_exposure_score>=4",
            ScreenedBackline => "formation.protected_slot_count>=1 and formation.frontline_count>=2",
            OpenSkirmish => "formation.profile=open_skirmish",
            _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, "Unknown concept formation profile."),
        };
}
