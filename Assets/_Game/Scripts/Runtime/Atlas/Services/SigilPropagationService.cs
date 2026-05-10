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
        return Resolve(region, placements, AtlasTraversalContext.CampaignFirstClear);
    }

    public static AtlasSigilResolution Resolve(
        AtlasRegionDefinition region,
        IReadOnlyList<AtlasPlacedSigil> placements,
        AtlasTraversalContext traversalContext)
    {
        if (region == null)
        {
            throw new ArgumentNullException(nameof(region));
        }

        placements ??= Array.Empty<AtlasPlacedSigil>();
        ValidateActiveSigilCap(placements, traversalContext ?? AtlasTraversalContext.CampaignFirstClear);
        var sigilsById = region.SigilPool.ToDictionary(sigil => sigil.SigilId, StringComparer.Ordinal);
        var nodesByHex = region.Nodes.ToDictionary(node => node.Hex);
        var anchorSlotsById = region.SigilAnchorSlots.ToDictionary(slot => slot.AnchorId, StringComparer.Ordinal);
        var anchorSlotsByHex = region.SigilAnchorSlots
            .Select(slot => (Slot: slot, Node: region.Nodes.FirstOrDefault(node => string.Equals(node.NodeId, slot.HexId, StringComparison.Ordinal))))
            .Where(pair => pair.Node != null)
            .ToDictionary(pair => pair.Node!.Hex, pair => pair.Slot);
        var stacks = new List<AtlasNodeModifierStack>(region.Nodes.Count);

        foreach (var node in region.Nodes.OrderBy(node => node.Hex))
        {
            var influences = new List<AtlasSigilInfluence>();
            foreach (var placement in placements
                         .OrderBy(placement => placement.SigilId, StringComparer.Ordinal)
                         .ThenBy(placement => placement.AnchorId, StringComparer.Ordinal))
            {
                if (!sigilsById.TryGetValue(placement.SigilId, out var sigil))
                {
                    continue;
                }

                var slot = ResolveAnchorSlot(placement, anchorSlotsById, anchorSlotsByHex);
                if (slot == null)
                {
                    continue;
                }

                var footprint = ResolveFootprint(region, slot, sigil);
                var cell = footprint.FirstOrDefault(candidate => candidate.Hex.Equals(node.Hex));
                if (cell == null || cell.FalloffPercent <= 0)
                {
                    continue;
                }

                foreach (var modifier in sigil.Modifiers)
                {
                    influences.Add(new AtlasSigilInfluence(
                        sigil.SigilId,
                        sigil.DisplayName,
                        placement.AnchorHex,
                        placement.AnchorHex.DistanceTo(node.Hex),
                        cell.FalloffPercent,
                        modifier.Category,
                        modifier.Label,
                        modifier.Percent,
                        QuantizePercent(modifier.Percent, cell.FalloffPercent),
                        slot.AnchorId,
                        sigil.FootprintProfileId,
                        ResolveFootprintShape(sigil)));
                }
            }

            stacks.Add(new AtlasNodeModifierStack(
                node.NodeId,
                node.Hex,
                influences
                    .OrderBy(influence => influence.Category)
                    .ThenBy(influence => influence.SigilId, StringComparer.Ordinal)
                    .ThenBy(influence => influence.AnchorId, StringComparer.Ordinal)
                    .ThenByDescending(influence => influence.EffectivePercent)
                    .ToArray(),
                ResolveModifiers(influences)));
        }

        return new AtlasSigilResolution(stacks);
    }

    public static void ValidateActiveSigilCap(
        IReadOnlyList<AtlasPlacedSigil> placements,
        AtlasTraversalContext traversalContext)
    {
        placements ??= Array.Empty<AtlasPlacedSigil>();
        traversalContext ??= AtlasTraversalContext.CampaignFirstClear;
        if (placements.Count <= traversalContext.ActiveSigilCap)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Traversal mode '{traversalContext.Mode}' allows {traversalContext.ActiveSigilCap} active sigils, but {placements.Count} were provided.");
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

    public static IReadOnlyList<AtlasFootprintCell> ResolveFootprint(
        AtlasRegionDefinition region,
        SigilAnchorSlot anchorSlot,
        AtlasSigilDefinition sigil)
    {
        if (region == null)
        {
            throw new ArgumentNullException(nameof(region));
        }

        if (anchorSlot == null)
        {
            throw new ArgumentNullException(nameof(anchorSlot));
        }

        if (sigil == null)
        {
            throw new ArgumentNullException(nameof(sigil));
        }

        var anchorNode = region.Nodes.First(node => string.Equals(node.NodeId, anchorSlot.HexId, StringComparison.Ordinal));
        var regionHexes = region.Nodes.Select(node => node.Hex).ToHashSet();
        var cells = ResolveFootprintShape(sigil) switch
        {
            AtlasFootprintShape.Lane => BuildLane(anchorNode.Hex, anchorSlot.OrientationToCore, regionHexes, sigil.Radius),
            AtlasFootprintShape.ScoutArc => BuildScoutArc(anchorNode.Hex, anchorSlot.OrientationToCore, regionHexes, sigil.Radius),
            _ => BuildCluster(anchorNode.Hex, regionHexes, sigil.Radius, sigil.FootprintProfileId),
        };

        return cells
            .GroupBy(cell => cell.Hex)
            .Select(group => group.OrderBy(cell => cell.CellIndex).First())
            .OrderBy(cell => cell.CellIndex)
            .ThenBy(cell => cell.Hex)
            .ToArray();
    }

    public static AtlasFootprintShape ResolveFootprintShape(AtlasSigilDefinition sigil)
    {
        return sigil.SigilCategory switch
        {
            AtlasModifierCategory.ThreatPressure => AtlasFootprintShape.Lane,
            AtlasModifierCategory.AffinityBoost => AtlasFootprintShape.ScoutArc,
            _ => AtlasFootprintShape.Cluster,
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

    private static SigilAnchorSlot? ResolveAnchorSlot(
        AtlasPlacedSigil placement,
        IReadOnlyDictionary<string, SigilAnchorSlot> byId,
        IReadOnlyDictionary<AtlasHexCoordinate, SigilAnchorSlot> byHex)
    {
        if (!string.IsNullOrWhiteSpace(placement.AnchorId) && byId.TryGetValue(placement.AnchorId, out var byAnchorId))
        {
            return byAnchorId;
        }

        return byHex.TryGetValue(placement.AnchorHex, out var byAnchorHex) ? byAnchorHex : null;
    }

    private static IReadOnlyList<AtlasFootprintCell> BuildCluster(
        AtlasHexCoordinate anchor,
        HashSet<AtlasHexCoordinate> regionHexes,
        int rangeTier,
        string footprintProfileId)
    {
        var cells = new List<AtlasFootprintCell> { new(anchor, 0, ResolveFalloffPercent(0)) };
        var directions = OrderedDirections().ToArray();
        var isWide = footprintProfileId.Contains(".Wide", StringComparison.Ordinal);
        var adjacentDirections = rangeTier >= 2 && isWide ? directions.Take(4) : directions;
        foreach (var direction in adjacentDirections)
        {
            AddCell(cells, regionHexes, Offset(anchor, direction), 1);
        }

        if (rangeTier >= 2)
        {
            foreach (var direction in directions.Take(isWide ? 2 : 0))
            {
                AddCell(cells, regionHexes, Offset(Offset(anchor, direction), direction), 2);
            }
        }

        return cells.Take(7).ToArray();
    }

    private static IReadOnlyList<AtlasFootprintCell> BuildLane(
        AtlasHexCoordinate anchor,
        AtlasHexDirection orientation,
        HashSet<AtlasHexCoordinate> regionHexes,
        int rangeTier)
    {
        var cells = new List<AtlasFootprintCell>();
        var cursor = anchor;
        var maxSteps = Math.Clamp(rangeTier + 1, 3, 5);
        for (var step = 1; step <= maxSteps; step++)
        {
            cursor = Offset(cursor, orientation);
            AddCell(cells, regionHexes, cursor, Math.Min(step - 1, 2));
        }

        return cells;
    }

    private static IReadOnlyList<AtlasFootprintCell> BuildScoutArc(
        AtlasHexCoordinate anchor,
        AtlasHexDirection orientation,
        HashSet<AtlasHexCoordinate> regionHexes,
        int rangeTier)
    {
        var left = Rotate(orientation, -1);
        var right = Rotate(orientation, 1);
        var cells = new List<AtlasFootprintCell>();
        AddCell(cells, regionHexes, Offset(anchor, orientation), 0);
        AddCell(cells, regionHexes, Offset(anchor, left), 1);
        AddCell(cells, regionHexes, Offset(anchor, right), 1);
        AddCell(cells, regionHexes, Offset(Offset(anchor, orientation), orientation), 2);
        if (rangeTier >= 2)
        {
            AddCell(cells, regionHexes, Offset(Offset(anchor, orientation), left), 2);
            AddCell(cells, regionHexes, Offset(Offset(anchor, orientation), right), 2);
        }

        return cells;
    }

    private static void AddCell(
        ICollection<AtlasFootprintCell> cells,
        HashSet<AtlasHexCoordinate> regionHexes,
        AtlasHexCoordinate hex,
        int cellIndex)
    {
        if (!regionHexes.Contains(hex))
        {
            return;
        }

        cells.Add(new AtlasFootprintCell(hex, cellIndex, ResolveFalloffPercent(cellIndex)));
    }

    private static AtlasHexCoordinate Offset(AtlasHexCoordinate hex, AtlasHexDirection direction)
    {
        return direction switch
        {
            AtlasHexDirection.East => new AtlasHexCoordinate(hex.Q + 1, hex.R),
            AtlasHexDirection.NorthEast => new AtlasHexCoordinate(hex.Q + 1, hex.R - 1),
            AtlasHexDirection.NorthWest => new AtlasHexCoordinate(hex.Q, hex.R - 1),
            AtlasHexDirection.West => new AtlasHexCoordinate(hex.Q - 1, hex.R),
            AtlasHexDirection.SouthWest => new AtlasHexCoordinate(hex.Q - 1, hex.R + 1),
            _ => new AtlasHexCoordinate(hex.Q, hex.R + 1),
        };
    }

    private static AtlasHexDirection Rotate(AtlasHexDirection direction, int steps)
    {
        var count = Enum.GetValues(typeof(AtlasHexDirection)).Length;
        var value = ((int)direction + steps) % count;
        return (AtlasHexDirection)(value < 0 ? value + count : value);
    }

    private static IEnumerable<AtlasHexDirection> OrderedDirections()
    {
        yield return AtlasHexDirection.East;
        yield return AtlasHexDirection.NorthEast;
        yield return AtlasHexDirection.NorthWest;
        yield return AtlasHexDirection.West;
        yield return AtlasHexDirection.SouthWest;
        yield return AtlasHexDirection.SouthEast;
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
