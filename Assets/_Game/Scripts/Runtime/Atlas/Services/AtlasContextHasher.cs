using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using SM.Atlas.Model;

namespace SM.Atlas.Services;

public static class AtlasContextHasher
{
    private const string CapRuleVersion = "math_rule_v1_normalized_influence_mass";

    public static string BuildSigilSnapshotHash(
        AtlasRegionDefinition region,
        string siteId,
        string traversalMode,
        IReadOnlyList<AtlasPlacedSigil> placedSigils,
        string cycleSalt)
    {
        if (region == null)
        {
            throw new ArgumentNullException(nameof(region));
        }

        var sigilsById = region.SigilPool.ToDictionary(sigil => sigil.SigilId, StringComparer.Ordinal);
        var builder = new StringBuilder();
        builder.Append("atlas-sigil-snapshot|")
            .Append(siteId).Append('|')
            .Append(region.RegionId).Append('|')
            .Append(traversalMode).Append('|')
            .Append(region.AnchorSlotVersion).Append('|')
            .Append(region.FootprintProfileVersion).Append('|')
            .Append(cycleSalt);

        foreach (var placement in (placedSigils ?? Array.Empty<AtlasPlacedSigil>())
                     .OrderBy(placement => placement.SigilId, StringComparer.Ordinal)
                     .ThenBy(placement => placement.AnchorId, StringComparer.Ordinal)
                     .ThenBy(placement => ResolveCategoryForSort(sigilsById, placement.SigilId))
                     .ThenBy(placement => ResolveFootprintForSort(sigilsById, placement.SigilId), StringComparer.Ordinal))
        {
            sigilsById.TryGetValue(placement.SigilId, out var sigil);
            builder.Append('|')
                .Append(placement.SigilId).Append('@')
                .Append(placement.AnchorId).Append(':')
                .Append(sigil?.SigilCategory.ToString() ?? string.Empty).Append(':')
                .Append(sigil?.FootprintProfileId ?? string.Empty);
        }

        return ComputeHash(builder.ToString());
    }

    public static string BuildNodeOverlayHash(
        string regionId,
        AtlasRegionNode node,
        string cycleSalt,
        IReadOnlyList<AtlasSigilInfluence> affectingSigils)
    {
        return BuildNodeOverlayHash(regionId, node, cycleSalt, affectingSigils, Array.Empty<AtlasResolvedModifier>());
    }

    public static string BuildNodeOverlayHash(
        string regionId,
        AtlasRegionNode node,
        string cycleSalt,
        AtlasNodeModifierStack stack)
    {
        if (stack == null)
        {
            throw new ArgumentNullException(nameof(stack));
        }

        return BuildNodeOverlayHash(regionId, node, cycleSalt, stack.Influences, stack.ResolvedModifiers);
    }

    private static string BuildNodeOverlayHash(
        string regionId,
        AtlasRegionNode node,
        string cycleSalt,
        IReadOnlyList<AtlasSigilInfluence> affectingSigils,
        IReadOnlyList<AtlasResolvedModifier> resolvedModifiers)
    {
        var builder = new StringBuilder();
        builder.Append("atlas-node-overlay|")
            .Append(regionId).Append('|')
            .Append(node.NodeId).Append('|')
            .Append(node.Hex).Append('|')
            .Append(cycleSalt).Append('|')
            .Append(CapRuleVersion);

        foreach (var sigil in SortedSigils(affectingSigils))
        {
            builder.Append('|')
                .Append(sigil.Category).Append(':')
                .Append(sigil.SigilId).Append('@')
                .Append(sigil.AnchorId).Append(':')
                .Append(sigil.FootprintProfileId).Append(':')
                .Append(sigil.FootprintShape).Append(':')
                .Append(sigil.FalloffTier).Append(':')
                .Append(sigil.RawPercent).Append(':')
                .Append(sigil.EffectivePercent);
        }

        foreach (var modifier in (resolvedModifiers ?? Array.Empty<AtlasResolvedModifier>())
                     .OrderBy(modifier => modifier.Category)
                     .ThenByDescending(modifier => modifier.Percent)
                     .ThenBy(modifier => modifier.Label, StringComparer.Ordinal))
        {
            builder.Append("|resolved:")
                .Append(modifier.Category).Append(':')
                .Append(modifier.Percent).Append(':')
                .Append(modifier.SameCategoryCapped ? '1' : '0').Append(':')
                .Append(modifier.HardCapped ? '1' : '0').Append(':')
                .Append(modifier.RiskBackedCapped ? '1' : '0');
        }

        return ComputeHash(builder.ToString());
    }

    public static string BuildBattleContextHash(
        string runId,
        string chapterId,
        string siteId,
        int nodeIndex,
        string encounterId,
        string stageCandidatePathHash,
        string nodeOverlayHash,
        string squadSnapshotId)
    {
        var input = string.Join(
            "|",
            "atlas-battle-context",
            runId,
            chapterId,
            siteId,
            nodeIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
            encounterId,
            stageCandidatePathHash,
            nodeOverlayHash,
            squadSnapshotId);

        return ComputeHash(input);
    }

    public static string BuildStageCandidatePathHash(IEnumerable<string> stageCandidateHexIds)
    {
        var input = string.Join(
            "|",
            new[] { "atlas-stage-candidate-path" }.Concat(stageCandidateHexIds ?? Array.Empty<string>()));
        return ComputeHash(input);
    }

    public static IReadOnlyList<string> SortedSigilIds(IEnumerable<AtlasSigilInfluence> affectingSigils)
    {
        return SortedSigils(affectingSigils).Select(sigil => sigil.SigilId).Distinct(StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<AtlasSigilInfluence> SortedSigils(IEnumerable<AtlasSigilInfluence> affectingSigils)
    {
        return (affectingSigils ?? Array.Empty<AtlasSigilInfluence>())
            .OrderBy(sigil => sigil.SigilId, StringComparer.Ordinal)
            .ThenBy(sigil => sigil.AnchorId, StringComparer.Ordinal)
            .ThenBy(sigil => sigil.Category)
            .ToArray();
    }

    private static AtlasModifierCategory ResolveCategoryForSort(
        IReadOnlyDictionary<string, AtlasSigilDefinition> sigilsById,
        string sigilId)
    {
        return sigilsById.TryGetValue(sigilId, out var sigil)
            ? sigil.SigilCategory
            : AtlasModifierCategory.RewardBias;
    }

    private static string ResolveFootprintForSort(
        IReadOnlyDictionary<string, AtlasSigilDefinition> sigilsById,
        string sigilId)
    {
        return sigilsById.TryGetValue(sigilId, out var sigil) ? sigil.FootprintProfileId : string.Empty;
    }

    private static string ComputeHash(string input)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
    }
}
