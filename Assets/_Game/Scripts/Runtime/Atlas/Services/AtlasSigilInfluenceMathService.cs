using System;
using System.Collections.Generic;
using System.Linq;
using SM.Atlas.Model;

namespace SM.Atlas.Services;

public static class AtlasSigilInfluenceMathService
{
    public static AtlasSigilMathResolution Resolve(
        AtlasRegionDefinition region,
        IReadOnlyList<AtlasPlacedSigil> placements,
        AtlasTraversalMode mode,
        int currentStageIndex,
        AtlasSigilMathConfig? config = null)
    {
        if (region == null)
        {
            throw new ArgumentNullException(nameof(region));
        }

        placements ??= Array.Empty<AtlasPlacedSigil>();
        config ??= AtlasSigilMathConfig.CreateDefault();

        var evaluations = placements
            .OrderBy(placement => placement.SigilId, StringComparer.Ordinal)
            .ThenBy(placement => placement.AnchorId, StringComparer.Ordinal)
            .Select(placement => Evaluate(region, placement, mode, currentStageIndex, config))
            .Where(evaluation => evaluation != null)
            .Select(evaluation => evaluation!)
            .ToArray();

        return new AtlasSigilMathResolution(
            evaluations,
            ResolveNodeEffects(region, mode, config, evaluations));
    }

    public static AtlasSigilMathEvaluation? Evaluate(
        AtlasRegionDefinition region,
        AtlasPlacedSigil placement,
        AtlasTraversalMode mode,
        int currentStageIndex,
        AtlasSigilMathConfig? config = null)
    {
        if (region == null)
        {
            throw new ArgumentNullException(nameof(region));
        }

        config ??= AtlasSigilMathConfig.CreateDefault();
        var sigil = region.SigilPool.FirstOrDefault(candidate => string.Equals(candidate.SigilId, placement.SigilId, StringComparison.Ordinal));
        if (sigil == null)
        {
            return null;
        }

        var anchorSlot = ResolveAnchorSlot(region, placement);
        if (anchorSlot == null)
        {
            return null;
        }

        var profile = ResolveProfile(config, sigil);
        var categoryParameters = config.CategoryParameters[profile.Category];
        var cells = BuildProfileCells(region, anchorSlot, profile, mode, currentStageIndex, config);
        var influenceMassRaw = cells.Sum(cell => cell.MassContribution);
        var influenceMass = Math.Max(influenceMassRaw, profile.MassFloor);
        var normalizedMassUnclamped = influenceMass / ResolveModeMassRef(config, mode);
        var normalizedMass = Clamp(normalizedMassUnclamped, config.MinNormalizedMass, config.MaxNormalizedMass);
        var massMultiplier = Clamp(
            Math.Pow(normalizedMass, -categoryParameters.MassExponent),
            categoryParameters.MinMassMultiplier,
            categoryParameters.MaxMassMultiplier);
        var effectivePotency = ResolveTierBudget(sigil.PotencyTier) *
                               profile.ProfileScalar *
                               profile.FrictionBonus *
                               massMultiplier;
        var cellsWithLocalContribution = cells
            .Select(cell => cell with { LocalContribution = cell.HasEffect ? effectivePotency * cell.Falloff : 0.0 })
            .ToArray();

        return new AtlasSigilMathEvaluation(
            sigil.SigilId,
            anchorSlot.AnchorId,
            profile.ProfileId,
            profile.Category,
            ResolveTierBudget(sigil.PotencyTier),
            influenceMassRaw,
            influenceMass,
            normalizedMass,
            massMultiplier,
            !NearlyEqual(normalizedMassUnclamped, normalizedMass),
            effectivePotency,
            cellsWithLocalContribution);
    }

    public static AtlasSigilMathSimulationReport BuildSimulationReport(
        AtlasRegionDefinition region,
        AtlasTraversalMode mode,
        int currentStageIndex,
        AtlasSigilMathConfig? config = null)
    {
        if (region == null)
        {
            throw new ArgumentNullException(nameof(region));
        }

        config ??= AtlasSigilMathConfig.CreateDefault();
        var placements = region.SigilPool
            .SelectMany(sigil => region.SigilAnchorSlots.Select(anchor => new AtlasPlacedSigil(sigil.SigilId, ResolveAnchorHex(region, anchor), anchor.AnchorId)))
            .ToArray();
        var resolution = Resolve(region, placements, mode, currentStageIndex, config);
        var metrics = resolution.Evaluations
            .GroupBy(evaluation => evaluation.ProfileId, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group =>
            {
                var samples = group.ToArray();
                return new AtlasProfileSimulationMetric(
                    group.Key,
                    samples.Length,
                    samples.Count(sample => sample.NormalizedMassClamped) / (double)samples.Length,
                    samples.Average(sample => sample.NormalizedMass),
                    samples.Average(sample => sample.EffectivePotency),
                    samples.Average(sample => sample.Cells.Count(cell => cell.HasEffect)),
                    samples.Average(sample => sample.Cells.Count(cell => !cell.HasEffect)));
            })
            .ToArray();

        return new AtlasSigilMathSimulationReport(metrics, resolution.NodeEffects);
    }

    private static IReadOnlyList<AtlasNodeSigilMathEffect> ResolveNodeEffects(
        AtlasRegionDefinition region,
        AtlasTraversalMode mode,
        AtlasSigilMathConfig config,
        IReadOnlyList<AtlasSigilMathEvaluation> evaluations)
    {
        var rawEffects = evaluations
            .SelectMany(evaluation => evaluation.Cells
                .Where(cell => cell.HasEffect)
                .Select(cell => (cell.NodeId, evaluation.Category, Percent: ConvertContributionToPercent(config, evaluation.Category, cell.LocalContribution))))
            .Where(item => item.Percent > 0.0)
            .GroupBy(item => (item.NodeId, item.Category))
            .Select(group => ResolveStackedEffect(region, mode, config, group.Key.NodeId, group.Key.Category, group.Select(item => item.Percent).ToArray()))
            .ToArray();

        var threatByNode = rawEffects
            .Where(effect => effect.Category == AtlasModifierCategory.ThreatPressure)
            .ToDictionary(effect => effect.NodeId, effect => effect.Percent, StringComparer.Ordinal);

        return rawEffects
            .Select(effect => effect.Category == AtlasModifierCategory.RewardBias
                ? ApplyRiskBackedRewardCap(region, mode, config, effect, threatByNode.TryGetValue(effect.NodeId, out var threat) ? threat : 0.0)
                : effect)
            .OrderBy(effect => effect.NodeId, StringComparer.Ordinal)
            .ThenBy(effect => effect.Category)
            .ToArray();
    }

    private static AtlasNodeSigilMathEffect ResolveStackedEffect(
        AtlasRegionDefinition region,
        AtlasTraversalMode mode,
        AtlasSigilMathConfig config,
        string nodeId,
        AtlasModifierCategory category,
        IReadOnlyList<double> percents)
    {
        var parameters = config.CategoryParameters[category];
        var ordered = percents.OrderByDescending(value => value).ToArray();
        var stacked = 0.0;
        for (var i = 0; i < ordered.Length; i++)
        {
            var multiplier = i < parameters.StackMultipliers.Count ? parameters.StackMultipliers[i] : 0.0;
            stacked += ordered[i] * multiplier;
        }

        var cap = ResolveCategoryCap(category, mode);
        var capped = Math.Min(stacked, cap);
        return new AtlasNodeSigilMathEffect(
            nodeId,
            category,
            stacked,
            capped,
            ordered.Length,
            ordered.Length > 1,
            capped < stacked,
            RiskBackedCapped: false);
    }

    private static AtlasNodeSigilMathEffect ApplyRiskBackedRewardCap(
        AtlasRegionDefinition region,
        AtlasTraversalMode mode,
        AtlasSigilMathConfig config,
        AtlasNodeSigilMathEffect rewardEffect,
        double threatPercent)
    {
        var node = region.Nodes.FirstOrDefault(candidate => string.Equals(candidate.NodeId, rewardEffect.NodeId, StringComparison.Ordinal));
        var allowed = Math.Min(
            ResolveCategoryCap(AtlasModifierCategory.RewardBias, mode),
            config.SafeRewardCapPercent + (config.RiskCoupling * threatPercent) + ResolveNativeDangerCredit(node));
        var capped = Math.Min(rewardEffect.Percent, allowed);

        return rewardEffect with
        {
            Percent = capped,
            RiskBackedCapped = capped < rewardEffect.Percent,
            HardCapped = rewardEffect.HardCapped || capped < rewardEffect.Percent,
        };
    }

    private static IReadOnlyList<AtlasSigilMathCell> BuildProfileCells(
        AtlasRegionDefinition region,
        SigilAnchorSlot anchorSlot,
        AtlasSigilMathProfile profile,
        AtlasTraversalMode mode,
        int currentStageIndex,
        AtlasSigilMathConfig config)
    {
        var anchorHex = ResolveAnchorHex(region, anchorSlot);
        var offsets = ResolveProfileHexes(anchorHex, anchorSlot.OrientationToCore, profile);
        var cells = new List<AtlasSigilMathCell>(profile.Falloff.Count);
        for (var index = 0; index < profile.Falloff.Count; index++)
        {
            var hex = offsets[index];
            var node = region.Nodes.FirstOrDefault(candidate => candidate.Hex.Equals(hex));
            var role = ResolveCellRole(region, node, currentStageIndex, config);
            var importance = config.CellImportance[role];
            var mass = ResolveCellImportanceMass(config, mode, role);
            var hasEffect = node != null &&
                            node.Kind != AtlasNodeKind.SigilAnchor &&
                            role != AtlasCellRole.Visited &&
                            role != AtlasCellRole.OffBoard &&
                            role != AtlasCellRole.DeadOrIrrelevant;
            var falloff = profile.Falloff[index];

            cells.Add(new AtlasSigilMathCell(
                hex,
                node?.NodeId ?? string.Empty,
                index,
                falloff,
                role,
                importance,
                mass,
                hasEffect,
                mass * falloff,
                LocalContribution: 0.0));
        }

        return cells;
    }

    private static AtlasCellRole ResolveCellRole(
        AtlasRegionDefinition region,
        AtlasRegionNode? node,
        int currentStageIndex,
        AtlasSigilMathConfig config)
    {
        if (node == null)
        {
            return AtlasCellRole.OffBoard;
        }

        if (node.Kind == AtlasNodeKind.SigilAnchor)
        {
            return AtlasCellRole.Entry;
        }

        var role = ResolveNodeKindRole(node.Kind);
        var stageCandidate = region.StageCandidates.FirstOrDefault(candidate => string.Equals(candidate.HexId, node.NodeId, StringComparison.Ordinal));
        if (stageCandidate != null)
        {
            var stageRole = ResolveStageRole(stageCandidate.SiteStageIndex, currentStageIndex);
            if (ResolveImportance(config, stageRole) >= ResolveImportance(config, role))
            {
                role = stageRole;
            }
        }

        if (role != AtlasCellRole.Boss && IsAdjacentToKind(region, node.Hex, AtlasNodeKind.Boss))
        {
            role = MaxRole(config, role, AtlasCellRole.BossAdjacent);
        }

        if (role != AtlasCellRole.Extract && IsAdjacentToKind(region, node.Hex, AtlasNodeKind.Extract))
        {
            role = MaxRole(config, role, AtlasCellRole.ExtractAdjacent);
        }

        return role;
    }

    private static AtlasCellRole ResolveNodeKindRole(AtlasNodeKind kind)
    {
        return kind switch
        {
            AtlasNodeKind.Boss => AtlasCellRole.Boss,
            AtlasNodeKind.Extract => AtlasCellRole.Extract,
            AtlasNodeKind.Elite => AtlasCellRole.Elite,
            AtlasNodeKind.Event or AtlasNodeKind.Echo or AtlasNodeKind.ScoutVantage => AtlasCellRole.Clue,
            AtlasNodeKind.Cache or AtlasNodeKind.Reward => AtlasCellRole.DetourHighValue,
            AtlasNodeKind.Normal or AtlasNodeKind.Skirmish => AtlasCellRole.CombatStandard,
            AtlasNodeKind.SigilAnchor => AtlasCellRole.Entry,
            _ => AtlasCellRole.DeadOrIrrelevant,
        };
    }

    private static AtlasCellRole ResolveStageRole(int stageIndex, int currentStageIndex)
    {
        if (stageIndex < currentStageIndex)
        {
            return AtlasCellRole.Visited;
        }

        if (stageIndex == currentStageIndex)
        {
            return AtlasCellRole.StageCandidateNow;
        }

        if (stageIndex == currentStageIndex + 1)
        {
            return AtlasCellRole.StageCandidateFuture1;
        }

        return AtlasCellRole.StageCandidateFuture2Plus;
    }

    private static AtlasCellRole MaxRole(AtlasSigilMathConfig config, AtlasCellRole a, AtlasCellRole b)
    {
        return ResolveImportance(config, a) >= ResolveImportance(config, b) ? a : b;
    }

    private static double ResolveImportance(AtlasSigilMathConfig config, AtlasCellRole role)
    {
        return config.CellImportance[role];
    }

    private static double ResolveCellImportanceMass(AtlasSigilMathConfig config, AtlasTraversalMode mode, AtlasCellRole role)
    {
        if (role == AtlasCellRole.OffBoard)
        {
            return config.EdgeVoidMass;
        }

        if (role == AtlasCellRole.Visited)
        {
            return config.VisitedMassFloor;
        }

        var floor = mode == AtlasTraversalMode.EndlessRegion ? config.EndlessCellMassFloor : config.StoryCellMassFloor;
        return Math.Max(config.CellImportance[role], floor);
    }

    private static IReadOnlyList<AtlasHexCoordinate> ResolveProfileHexes(
        AtlasHexCoordinate anchor,
        AtlasHexDirection orientation,
        AtlasSigilMathProfile profile)
    {
        var left = Rotate(orientation, -1);
        var right = Rotate(orientation, 1);

        return profile.ProfileId switch
        {
            "RewardBias.Cluster.Dense" => new[]
            {
                anchor,
                Offset(anchor, orientation),
                Offset(anchor, left),
            },
            "RewardBias.Cluster.Wide" => new[]
            {
                anchor,
                Offset(anchor, orientation),
                Offset(anchor, left),
                Offset(anchor, right),
                Offset(Offset(anchor, orientation), left),
                Offset(Offset(anchor, orientation), right),
            },
            "ThreatPressure.Lane.Hard" => BuildLane(anchor, orientation, 3),
            "ThreatPressure.Lane.Long" => BuildLane(anchor, orientation, 5),
            "AffinityBoost.ScoutArc.Deep" => new[]
            {
                Offset(anchor, orientation),
                Offset(Offset(anchor, orientation), orientation),
                Offset(Offset(Offset(anchor, orientation), orientation), orientation),
                Offset(Offset(anchor, orientation), left),
            },
            "AffinityBoost.ScoutArc.Wide" => new[]
            {
                Offset(anchor, orientation),
                Offset(anchor, left),
                Offset(anchor, right),
                Offset(Offset(anchor, orientation), left),
                Offset(Offset(anchor, orientation), right),
            },
            _ => new[] { anchor },
        };
    }

    private static IReadOnlyList<AtlasHexCoordinate> BuildLane(AtlasHexCoordinate anchor, AtlasHexDirection orientation, int count)
    {
        var cells = new List<AtlasHexCoordinate>(count);
        var cursor = anchor;
        for (var i = 0; i < count; i++)
        {
            cursor = Offset(cursor, orientation);
            cells.Add(cursor);
        }

        return cells;
    }

    private static AtlasSigilMathProfile ResolveProfile(AtlasSigilMathConfig config, AtlasSigilDefinition sigil)
    {
        if (config.Profiles.TryGetValue(sigil.FootprintProfileId, out var profile))
        {
            return profile;
        }

        var fallback = sigil.SigilCategory switch
        {
            AtlasModifierCategory.ThreatPressure => "ThreatPressure.Lane.Long",
            AtlasModifierCategory.AffinityBoost => "AffinityBoost.ScoutArc.Deep",
            _ => "RewardBias.Cluster.Wide",
        };
        return config.Profiles[fallback];
    }

    private static SigilAnchorSlot? ResolveAnchorSlot(AtlasRegionDefinition region, AtlasPlacedSigil placement)
    {
        if (!string.IsNullOrWhiteSpace(placement.AnchorId))
        {
            var byId = region.SigilAnchorSlots.FirstOrDefault(slot => string.Equals(slot.AnchorId, placement.AnchorId, StringComparison.Ordinal));
            if (byId != null)
            {
                return byId;
            }
        }

        return region.SigilAnchorSlots.FirstOrDefault(slot => ResolveAnchorHex(region, slot).Equals(placement.AnchorHex));
    }

    private static AtlasHexCoordinate ResolveAnchorHex(AtlasRegionDefinition region, SigilAnchorSlot anchorSlot)
    {
        return region.Nodes.First(node => string.Equals(node.NodeId, anchorSlot.HexId, StringComparison.Ordinal)).Hex;
    }

    private static double ConvertContributionToPercent(AtlasSigilMathConfig config, AtlasModifierCategory category, double contribution)
    {
        return contribution * config.CategoryParameters[category].ContributionToPercentScale;
    }

    private static double ResolveCategoryCap(AtlasModifierCategory category, AtlasTraversalMode mode)
    {
        return category switch
        {
            AtlasModifierCategory.RewardBias => mode switch
            {
                AtlasTraversalMode.StoryFirstClear => 30.0,
                AtlasTraversalMode.EndlessRegion => 45.0,
                _ => 40.0,
            },
            AtlasModifierCategory.ThreatPressure => mode switch
            {
                AtlasTraversalMode.StoryFirstClear => 35.0,
                AtlasTraversalMode.EndlessRegion => 55.0,
                _ => 45.0,
            },
            _ => 35.0,
        };
    }

    private static double ResolveNativeDangerCredit(AtlasRegionNode? node)
    {
        return node?.Kind switch
        {
            AtlasNodeKind.Boss or AtlasNodeKind.Elite => 10.0,
            AtlasNodeKind.Skirmish => 5.0,
            _ => 0.0,
        };
    }

    private static double ResolveTierBudget(int potencyTier)
    {
        return 100.0 + (Math.Max(1, potencyTier) - 1) * 15.0;
    }

    private static double ResolveModeMassRef(AtlasSigilMathConfig config, AtlasTraversalMode mode)
    {
        return mode == AtlasTraversalMode.EndlessRegion ? config.EndlessModeMassRef : config.StoryModeMassRef;
    }

    private static bool IsAdjacentToKind(AtlasRegionDefinition region, AtlasHexCoordinate hex, AtlasNodeKind kind)
    {
        return region.Nodes.Any(node => node.Kind == kind && node.Hex.DistanceTo(hex) == 1);
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

    private static double Clamp(double value, double min, double max)
    {
        return Math.Min(Math.Max(value, min), max);
    }

    private static bool NearlyEqual(double a, double b)
    {
        return Math.Abs(a - b) < 0.000001;
    }
}
