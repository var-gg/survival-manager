using System;
using System.Collections.Generic;
using System.Linq;
using SM.Atlas.Model;

namespace SM.Atlas.Services;

public static class SigilPropagationService
{
    public const int RewardBiasHardCapPercent = 60;
    public const int ThreatPressureHardCapPercent = 45;

    public static AtlasSigilResolution Resolve(AtlasRegionDefinition region, IReadOnlyList<AtlasPlacedSigil> placements)
    {
        if (region == null)
        {
            throw new ArgumentNullException(nameof(region));
        }

        placements ??= Array.Empty<AtlasPlacedSigil>();
        var sigilsById = region.SigilPool.ToDictionary(sigil => sigil.SigilId, StringComparer.Ordinal);
        var stacks = new List<AtlasNodeModifierStack>(region.Nodes.Count);

        foreach (var node in region.Nodes.OrderBy(node => node.Hex))
        {
            var influences = new List<AtlasSigilInfluence>();
            foreach (var placement in placements.OrderBy(placement => placement.SigilId, StringComparer.Ordinal))
            {
                if (!sigilsById.TryGetValue(placement.SigilId, out var sigil))
                {
                    continue;
                }

                var distance = placement.AnchorHex.DistanceTo(node.Hex);
                if (distance > sigil.Radius)
                {
                    continue;
                }

                var falloff = ResolveFalloffPercent(distance);
                if (falloff <= 0)
                {
                    continue;
                }

                foreach (var modifier in sigil.Modifiers)
                {
                    influences.Add(new AtlasSigilInfluence(
                        sigil.SigilId,
                        sigil.DisplayName,
                        placement.AnchorHex,
                        distance,
                        distance,
                        modifier.Category,
                        modifier.Label,
                        modifier.Percent,
                        QuantizePercent(modifier.Percent, falloff)));
                }
            }

            stacks.Add(new AtlasNodeModifierStack(
                node.NodeId,
                node.Hex,
                influences
                    .OrderBy(influence => influence.SigilId, StringComparer.Ordinal)
                    .ThenBy(influence => influence.Category)
                    .ThenByDescending(influence => influence.EffectivePercent)
                    .ToArray(),
                ResolveModifiers(influences)));
        }

        return new AtlasSigilResolution(stacks);
    }

    public static int ResolveFalloffPercent(int distance)
    {
        return distance switch
        {
            <= 0 => 100,
            1 => 70,
            2 => 40,
            _ => 0,
        };
    }

    private static IReadOnlyList<AtlasResolvedModifier> ResolveModifiers(IReadOnlyList<AtlasSigilInfluence> influences)
    {
        return influences
            .GroupBy(influence => influence.Category)
            .Select(ResolveCategory)
            .Where(modifier => modifier.Percent > 0)
            .OrderByDescending(modifier => modifier.Percent)
            .ThenBy(modifier => modifier.Category)
            .Take(3)
            .ToArray();
    }

    private static AtlasResolvedModifier ResolveCategory(IGrouping<AtlasModifierCategory, AtlasSigilInfluence> group)
    {
        var ordered = group
            .OrderByDescending(influence => influence.EffectivePercent)
            .ThenBy(influence => influence.SigilId, StringComparer.Ordinal)
            .ThenBy(influence => influence.Label, StringComparer.Ordinal)
            .ToArray();

        var strongest = ordered.FirstOrDefault();
        var second = ordered.Skip(1).FirstOrDefault();
        var value = strongest?.EffectivePercent ?? 0;
        var sameCategoryCapped = second != null;
        if (second != null)
        {
            value += second.EffectivePercent / 2;
        }

        var capped = ApplyHardCap(group.Key, value);
        return new AtlasResolvedModifier(
            group.Key,
            strongest?.Label ?? group.Key.ToString(),
            capped.Percent,
            ordered,
            sameCategoryCapped,
            capped.HardCapped);
    }

    private static (int Percent, bool HardCapped) ApplyHardCap(AtlasModifierCategory category, int value)
    {
        var cap = category switch
        {
            AtlasModifierCategory.RewardBias => RewardBiasHardCapPercent,
            AtlasModifierCategory.ThreatPressure => ThreatPressureHardCapPercent,
            _ => int.MaxValue,
        };

        return value > cap ? (cap, true) : (value, false);
    }

    private static int QuantizePercent(int value, int falloffPercent)
    {
        return (value * falloffPercent) / 100;
    }
}
