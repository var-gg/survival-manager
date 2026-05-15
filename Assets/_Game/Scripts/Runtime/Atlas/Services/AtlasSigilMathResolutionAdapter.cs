using System;
using System.Collections.Generic;
using System.Linq;
using SM.Atlas.Model;

namespace SM.Atlas.Services;

public static class AtlasSigilMathResolutionAdapter
{
    public static AtlasSigilResolution ToModifierResolution(
        AtlasRegionDefinition region,
        AtlasSigilMathResolution mathResolution,
        AtlasSigilMathConfig? config = null)
    {
        if (region == null)
        {
            throw new ArgumentNullException(nameof(region));
        }

        if (mathResolution == null)
        {
            throw new ArgumentNullException(nameof(mathResolution));
        }

        config ??= AtlasSigilMathConfig.CreateDefault();
        var sigilsById = region.SigilPool.ToDictionary(sigil => sigil.SigilId, StringComparer.Ordinal);
        var effectsByNodeCategory = mathResolution.NodeEffects.ToDictionary(
            effect => (effect.NodeId, effect.Category),
            effect => effect);
        var stacks = region.Nodes
            .OrderBy(node => node.Hex)
            .Select(node =>
            {
                var influences = BuildInfluences(region, sigilsById, mathResolution, config, node).ToArray();
                return new AtlasNodeModifierStack(
                    node.NodeId,
                    node.Hex,
                    influences,
                    BuildResolvedModifiers(influences, effectsByNodeCategory, node.NodeId));
            })
            .ToArray();

        return new AtlasSigilResolution(stacks);
    }

    private static IEnumerable<AtlasSigilInfluence> BuildInfluences(
        AtlasRegionDefinition region,
        IReadOnlyDictionary<string, AtlasSigilDefinition> sigilsById,
        AtlasSigilMathResolution mathResolution,
        AtlasSigilMathConfig config,
        AtlasRegionNode node)
    {
        foreach (var evaluation in mathResolution.Evaluations)
        {
            if (!sigilsById.TryGetValue(evaluation.SigilId, out var sigil))
            {
                continue;
            }

            var anchorHex = ResolveAnchorHex(region, evaluation.AnchorId);
            var label = sigil.Modifiers.FirstOrDefault(modifier => modifier.Category == evaluation.Category)?.Label
                        ?? evaluation.Category.ToString();
            var contributionScale = config.CategoryParameters[evaluation.Category].ContributionToPercentScale;

            foreach (var cell in evaluation.Cells.Where(cell =>
                         cell.HasEffect
                         && string.Equals(cell.NodeId, node.NodeId, StringComparison.Ordinal)
                         && cell.LocalContribution > 0.0))
            {
                var localPercent = RoundPercent(cell.LocalContribution * contributionScale);
                if (localPercent <= 0)
                {
                    continue;
                }

                yield return new AtlasSigilInfluence(
                    evaluation.SigilId,
                    sigil.DisplayName,
                    anchorHex,
                    anchorHex.DistanceTo(cell.Hex),
                    RoundPercent(cell.Falloff * 100.0),
                    evaluation.Category,
                    label,
                    localPercent,
                    localPercent,
                    evaluation.AnchorId,
                    evaluation.ProfileId,
                    ResolveFootprintShape(evaluation.ProfileId));
            }
        }
    }

    private static IReadOnlyList<AtlasResolvedModifier> BuildResolvedModifiers(
        IReadOnlyList<AtlasSigilInfluence> influences,
        IReadOnlyDictionary<(string NodeId, AtlasModifierCategory Category), AtlasNodeSigilMathEffect> effectsByNodeCategory,
        string nodeId)
    {
        return influences
            .GroupBy(influence => influence.Category)
            .Select(group =>
            {
                var orderedSources = group
                    .OrderByDescending(influence => influence.EffectivePercent)
                    .ThenBy(influence => influence.SigilId, StringComparer.Ordinal)
                    .ThenBy(influence => influence.AnchorId, StringComparer.Ordinal)
                    .ToArray();

                if (!effectsByNodeCategory.TryGetValue((nodeId, group.Key), out var effect))
                {
                    return null;
                }

                return new AtlasResolvedModifier(
                    group.Key,
                    orderedSources.FirstOrDefault()?.Label ?? group.Key.ToString(),
                    RoundPercent(effect.Percent),
                    orderedSources,
                    effect.SameCategoryDiminished,
                    effect.HardCapped,
                    effect.RiskBackedCapped);
            })
            .Where(modifier => modifier is { Percent: > 0 })
            .Select(modifier => modifier!)
            .OrderByDescending(modifier => modifier.Percent)
            .ThenBy(modifier => modifier.Category)
            .Take(3)
            .ToArray();
    }

    private static AtlasHexCoordinate ResolveAnchorHex(AtlasRegionDefinition region, string anchorId)
    {
        var slot = region.SigilAnchorSlots.FirstOrDefault(anchor => string.Equals(anchor.AnchorId, anchorId, StringComparison.Ordinal));
        if (slot == null)
        {
            return default;
        }

        return region.Nodes.FirstOrDefault(node => string.Equals(node.NodeId, slot.HexId, StringComparison.Ordinal))?.Hex ?? default;
    }

    private static AtlasFootprintShape ResolveFootprintShape(string profileId)
    {
        if (profileId.Contains(".Lane.", StringComparison.Ordinal))
        {
            return AtlasFootprintShape.Lane;
        }

        if (profileId.Contains(".ScoutArc.", StringComparison.Ordinal))
        {
            return AtlasFootprintShape.ScoutArc;
        }

        return AtlasFootprintShape.Cluster;
    }

    private static int RoundPercent(double value)
    {
        return (int)Math.Round(value, MidpointRounding.AwayFromZero);
    }
}
