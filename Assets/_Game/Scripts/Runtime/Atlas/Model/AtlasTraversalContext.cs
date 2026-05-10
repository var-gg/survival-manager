namespace SM.Atlas.Model;

public enum TraversalMode
{
    CampaignFirstClear = 0,
    CampaignResume = 1,
    Revisit = 2,
    EndlessRegion = 3,
}

public enum AtlasWeaknessContractPolicy
{
    Disabled = 0,
    LimitedPlaceholder = 1,
    FullPlaceholder = 2,
}

public sealed record AtlasTraversalContext(
    TraversalMode Mode,
    int ActiveSigilCap,
    AtlasWeaknessContractPolicy WeaknessContractPolicy)
{
    public static AtlasTraversalContext CampaignFirstClear { get; } = ForMode(TraversalMode.CampaignFirstClear);

    public static AtlasTraversalContext ForMode(TraversalMode mode)
    {
        return mode switch
        {
            TraversalMode.EndlessRegion => new AtlasTraversalContext(mode, 3, AtlasWeaknessContractPolicy.FullPlaceholder),
            TraversalMode.Revisit => new AtlasTraversalContext(mode, 2, AtlasWeaknessContractPolicy.LimitedPlaceholder),
            TraversalMode.CampaignResume => new AtlasTraversalContext(mode, 2, AtlasWeaknessContractPolicy.Disabled),
            _ => new AtlasTraversalContext(TraversalMode.CampaignFirstClear, 2, AtlasWeaknessContractPolicy.Disabled),
        };
    }
}
