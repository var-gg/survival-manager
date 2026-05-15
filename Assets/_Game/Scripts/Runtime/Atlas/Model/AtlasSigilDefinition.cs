using System.Collections.Generic;

namespace SM.Atlas.Model;

public enum AtlasModifierCategory
{
    RewardBias = 0,
    ThreatPressure = 1,
    AffinityBoost = 2,
}

public enum AtlasFootprintShape
{
    Cluster = 0,
    Lane = 1,
    ScoutArc = 2,
}

public sealed record AtlasSigilModifier(
    AtlasModifierCategory Category,
    string Label,
    int Percent);

public sealed record AtlasSigilDefinition(
    string SigilId,
    string DisplayName,
    int Radius,
    string ColorToken,
    IReadOnlyList<AtlasSigilModifier> Modifiers,
    AtlasModifierCategory SigilCategory = AtlasModifierCategory.RewardBias,
    string FootprintProfileId = "RewardBias.Cluster.Wide",
    int PotencyTier = 1,
    string FalloffProfileId = "falloff_100_70_40");

public sealed record AtlasPlacedSigil(
    string SigilId,
    AtlasHexCoordinate AnchorHex,
    string AnchorId = "");

public sealed record AtlasFootprintCell(
    AtlasHexCoordinate Hex,
    int CellIndex,
    int FalloffPercent);

public sealed record AtlasSigilInfluence(
    string SigilId,
    string DisplayName,
    AtlasHexCoordinate AnchorHex,
    int Distance,
    int FalloffTier,
    AtlasModifierCategory Category,
    string Label,
    int RawPercent,
    int EffectivePercent,
    string AnchorId = "",
    string FootprintProfileId = "",
    AtlasFootprintShape FootprintShape = AtlasFootprintShape.Cluster);

public sealed record AtlasResolvedModifier(
    AtlasModifierCategory Category,
    string Label,
    int Percent,
    IReadOnlyList<AtlasSigilInfluence> Sources,
    bool SameCategoryCapped,
    bool HardCapped,
    bool RiskBackedCapped = false);

public sealed record AtlasNodeModifierStack(
    string NodeId,
    AtlasHexCoordinate Hex,
    IReadOnlyList<AtlasSigilInfluence> Influences,
    IReadOnlyList<AtlasResolvedModifier> ResolvedModifiers)
{
    public int RewardBiasPercent => GetPercent(AtlasModifierCategory.RewardBias);
    public int ThreatPressurePercent => GetPercent(AtlasModifierCategory.ThreatPressure);
    public int AffinityBoostPercent => GetPercent(AtlasModifierCategory.AffinityBoost);

    private int GetPercent(AtlasModifierCategory category)
    {
        foreach (var modifier in ResolvedModifiers)
        {
            if (modifier.Category == category)
            {
                return modifier.Percent;
            }
        }

        return 0;
    }
}

public sealed record AtlasSigilResolution(IReadOnlyList<AtlasNodeModifierStack> NodeStacks)
{
    public AtlasNodeModifierStack? FindNode(string nodeId)
    {
        foreach (var stack in NodeStacks)
        {
            if (string.Equals(stack.NodeId, nodeId, System.StringComparison.Ordinal))
            {
                return stack;
            }
        }

        return null;
    }
}
