namespace SM.HeadlessCensus;

public sealed record FormationFeatures(
    int FrontlineCount,
    int ProtectedSlotCount,
    int SideExposureCount,
    int RearExposureCount,
    double FlankRearExposureScore,
    double SupportDistance,
    double BacklineAccessibility)
{
    internal double[] ToMedoidVector()
        => new[]
        {
            (double)FrontlineCount,
            ProtectedSlotCount,
            FlankRearExposureScore,
            SupportDistance,
            BacklineAccessibility,
        };
}
