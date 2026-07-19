using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;

namespace SM.HeadlessPolicies;

internal sealed class PreviewGroundedFormationSelection
{
    public PreviewGroundedFormationSelection(
        IReadOnlyList<HeadlessPlacement> placements,
        string ruleId)
    {
        Placements = placements;
        RuleId = ruleId;
    }

    public IReadOnlyList<HeadlessPlacement> Placements { get; }
    public string RuleId { get; }
}

/// <summary>가시 counter 역할과 현재 HP band로만 formation posture를 고른다.</summary>
internal static class PreviewGroundedFormationSelector
{
    public const string StandardRule = "standard_role_rows";
    public const string ProtectedBreakerRule = "protected_breaker_crossfire";
    public const string CenterBreakRule = "center_break_crossfire";
    public const string DurableMultiEntryRule = "durable_multi_entry_rotation";
    public const string StablePriorityScreenRule = "stable_priority_screen";
    public const string BacklineDiveScreenRule = "backline_dive_screen";

    public static PreviewGroundedFormationSelection Select(
        EnemyThreatProfile profile,
        IReadOnlyList<HeadlessHeroObservation> heroes,
        IReadOnlyList<DeploymentAnchorId> anchors,
        IReadOnlyList<PreviewCounterConnection> connections)
    {
        var fallback = new PreviewGroundedFormationSelection(
            HeadlessPolicyScoring.PlaceFormation(heroes, anchors),
            StandardRule);
        if (profile.Tags.Contains(EnemyThreatTag.BacklineDive, StringComparer.Ordinal)
            && HasAllAnchors(anchors))
        {
            var diveScreen = BacklineDiveScreen(heroes, connections);
            if (diveScreen != null)
            {
                return diveScreen;
            }
        }

        if (HasSustainBackline(profile)
            && HasAllAnchors(anchors)
            && heroes.Count(HeadlessPolicyScoring.PrefersFront) <= 3
            && heroes.Count(value => !HeadlessPolicyScoring.PrefersFront(value)) <= 3)
        {
            return StablePriorityScreen(heroes);
        }

        if (!HasCompositeScreen(profile)
            || heroes.Count != 4
            || !HasAllAnchors(anchors))
        {
            return fallback;
        }

        var byId = heroes.ToDictionary(value => value.HeroId, StringComparer.Ordinal);
        var breaker = ConnectedHeroes(connections, PreviewCounterCapability.WallBreak)
            .Select(value => byId[value])
            .OrderBy(value => value.HeroId, StringComparer.Ordinal)
            .FirstOrDefault();
        var disruptor = ConnectedHeroes(connections, PreviewCounterCapability.SustainDisruption)
            .Where(value => breaker == null || value != breaker.HeroId)
            .Select(value => byId[value])
            .OrderBy(value => IsRanged(value) ? 1 : 0)
            .ThenBy(value => value.HeroId, StringComparer.Ordinal)
            .FirstOrDefault();
        var ranged = ConnectedHeroes(connections, PreviewCounterCapability.PriorityAccess)
            .Where(value => (breaker == null || value != breaker.HeroId)
                            && (disruptor == null || value != disruptor.HeroId))
            .Select(value => byId[value])
            .OrderByDescending(MaxVisibleRange)
            .ThenBy(value => value.HeroId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (breaker == null || disruptor == null || ranged == null)
        {
            return fallback;
        }

        var reserved = new HashSet<string>(new[] { breaker.HeroId, disruptor.HeroId, ranged.HeroId }, StringComparer.Ordinal);
        var secondary = heroes.Where(value => !reserved.Contains(value.HeroId))
            .OrderBy(value => value.HeroId, StringComparer.Ordinal)
            .Single();
        if (HealthRatio(breaker) >= 0.65d && HealthRatio(secondary) >= 0.65d)
        {
            return Formation(
                DurableMultiEntryRule,
                (breaker, DeploymentAnchorId.BackTop),
                (disruptor, DeploymentAnchorId.BackCenter),
                (secondary, DeploymentAnchorId.BackBottom),
                (ranged, DeploymentAnchorId.FrontCenter));
        }

        if (HealthRatio(breaker) >= 0.65d)
        {
            return Formation(
                CenterBreakRule,
                (breaker, DeploymentAnchorId.FrontCenter),
                (disruptor, DeploymentAnchorId.BackTop),
                (secondary, DeploymentAnchorId.FrontTop),
                (ranged, DeploymentAnchorId.BackBottom));
        }

        return Formation(
            ProtectedBreakerRule,
            (breaker, DeploymentAnchorId.BackCenter),
            (disruptor, DeploymentAnchorId.FrontTop),
            (secondary, DeploymentAnchorId.FrontCenter),
            (ranged, DeploymentAnchorId.BackTop));
    }

    private static PreviewGroundedFormationSelection Formation(
        string ruleId,
        params (HeadlessHeroObservation Hero, DeploymentAnchorId Anchor)[] entries)
        => new(
            entries.OrderBy(value => value.Anchor)
                .Select(value => new HeadlessPlacement(value.Anchor, value.Hero.HeroId))
                .ToArray(),
            ruleId);

    private static PreviewGroundedFormationSelection StablePriorityScreen(
        IReadOnlyList<HeadlessHeroObservation> heroes)
    {
        var front = heroes.Where(HeadlessPolicyScoring.PrefersFront)
            .OrderBy(value => string.Equals(value.RoleTag, "anchor", StringComparison.Ordinal) ? 0 : 1)
            .ThenBy(value => HasTargetRule(value, SkillTargetRule.MostExposedEnemy) ? 0 : 1)
            .ThenBy(value => HasTargetRule(value, SkillTargetRule.LowestHpEnemy) ? 0 : 1)
            .ThenBy(value => value.HeroId, StringComparer.Ordinal)
            .ToArray();
        var back = heroes.Where(value => !HeadlessPolicyScoring.PrefersFront(value))
            .OrderByDescending(MaxVisibleRange)
            .ThenBy(value => value.HeroId, StringComparer.Ordinal)
            .ToArray();
        var frontAnchors = front.Length switch
        {
            0 => Array.Empty<DeploymentAnchorId>(),
            1 => new[] { DeploymentAnchorId.FrontCenter },
            2 => new[] { DeploymentAnchorId.FrontTop, DeploymentAnchorId.FrontCenter },
            _ => new[] { DeploymentAnchorId.FrontTop, DeploymentAnchorId.FrontCenter, DeploymentAnchorId.FrontBottom },
        };
        var backAnchors = back.Length switch
        {
            0 => Array.Empty<DeploymentAnchorId>(),
            1 => new[] { DeploymentAnchorId.BackCenter },
            2 => new[] { DeploymentAnchorId.BackTop, DeploymentAnchorId.BackCenter },
            _ => new[] { DeploymentAnchorId.BackTop, DeploymentAnchorId.BackCenter, DeploymentAnchorId.BackBottom },
        };
        var placements = front.Select((hero, index) => new HeadlessPlacement(frontAnchors[index], hero.HeroId))
            .Concat(back.Select((hero, index) => new HeadlessPlacement(backAnchors[index], hero.HeroId)))
            .OrderBy(value => value.Anchor)
            .ToArray();
        return new PreviewGroundedFormationSelection(placements, StablePriorityScreenRule);
    }

    private static PreviewGroundedFormationSelection? BacklineDiveScreen(
        IReadOnlyList<HeadlessHeroObservation> heroes,
        IReadOnlyList<PreviewCounterConnection> connections)
    {
        var byId = heroes.ToDictionary(value => value.HeroId, StringComparer.Ordinal);
        var guard = ConnectedHeroes(connections, PreviewCounterCapability.Protection)
            .Where(byId.ContainsKey)
            .Select(id => byId[id])
            .Where(HeadlessPolicyScoring.PrefersFront)
            .OrderBy(value => string.Equals(value.ClassId, "vanguard", StringComparison.Ordinal) ? 0 : 1)
            .ThenByDescending(value => value.MaxHp)
            .ThenBy(value => value.HeroId, StringComparer.Ordinal)
            .FirstOrDefault();
        var protectedHero = heroes
            .Where(value => guard == null || value.HeroId != guard.HeroId)
            .Where(value => value.ClassId is "ranger" or "mystic")
            .OrderBy(value => value.PreferredAnchor is DeploymentAnchorId.BackTop or DeploymentAnchorId.BackBottom ? 0 : 1)
            .ThenBy(value => string.Equals(value.ClassId, "mystic", StringComparison.Ordinal) ? 0 : 1)
            .ThenBy(value => HealthRatio(value))
            .ThenBy(value => value.HeroId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (guard == null || protectedHero == null)
        {
            return null;
        }

        var protectedAnchor = protectedHero.PreferredAnchor is DeploymentAnchorId.BackTop or DeploymentAnchorId.BackBottom
            ? protectedHero.PreferredAnchor
            : DeploymentAnchorId.BackTop;
        var guardAnchor = protectedAnchor == DeploymentAnchorId.BackBottom
            ? DeploymentAnchorId.FrontBottom
            : DeploymentAnchorId.FrontTop;
        var placements = new List<HeadlessPlacement>
        {
            new(guardAnchor, guard.HeroId),
            new(protectedAnchor, protectedHero.HeroId),
        };
        var reserved = new HashSet<string>(new[] { guard.HeroId, protectedHero.HeroId }, StringComparer.Ordinal);
        var remainingHeroes = heroes.Where(value => !reserved.Contains(value.HeroId)).ToArray();
        var remainingAnchors = new[]
        {
            DeploymentAnchorId.FrontTop,
            DeploymentAnchorId.FrontCenter,
            DeploymentAnchorId.FrontBottom,
            DeploymentAnchorId.BackTop,
            DeploymentAnchorId.BackCenter,
            DeploymentAnchorId.BackBottom,
        }.Where(value => value != protectedAnchor && value != guardAnchor).ToArray();
        placements.AddRange(HeadlessPolicyScoring.PlaceFormation(remainingHeroes, remainingAnchors));
        return new PreviewGroundedFormationSelection(
            placements.OrderBy(value => value.Anchor).ToArray(),
            BacklineDiveScreenRule);
    }

    private static IEnumerable<string> ConnectedHeroes(
        IEnumerable<PreviewCounterConnection> connections,
        string capability)
        => connections.Where(value => value.CapabilityTag == capability)
            .Select(value => value.HeroId)
            .Distinct(StringComparer.Ordinal);

    private static bool HasCompositeScreen(EnemyThreatProfile profile)
        => profile.Tags.Contains(EnemyThreatTag.BacklineFirepower, StringComparer.Ordinal)
           && profile.Tags.Contains(EnemyThreatTag.FrontlineWall, StringComparer.Ordinal)
           && profile.Tags.Contains(EnemyThreatTag.SustainEngine, StringComparer.Ordinal);

    private static bool HasSustainBackline(EnemyThreatProfile profile)
        => profile.Tags.Contains(EnemyThreatTag.BacklineFirepower, StringComparer.Ordinal)
           && profile.Tags.Contains(EnemyThreatTag.SustainEngine, StringComparer.Ordinal)
           && !profile.Tags.Contains(EnemyThreatTag.FrontlineWall, StringComparer.Ordinal);

    private static bool HasAllAnchors(IReadOnlyList<DeploymentAnchorId> anchors)
        => Enum.GetValues(typeof(DeploymentAnchorId)).Cast<DeploymentAnchorId>()
            .All(value => anchors.Contains(value));

    private static bool IsRanged(HeadlessHeroObservation hero)
        => hero.SkillCards.Any(skill => skill.Delivery is SkillDelivery.Ranged or SkillDelivery.Projectile);

    private static bool HasTargetRule(HeadlessHeroObservation hero, SkillTargetRule targetRule)
        => hero.SkillCards.Any(skill => skill.TargetRule == targetRule);

    private static float MaxVisibleRange(HeadlessHeroObservation hero)
        => hero.SkillCards.Where(skill => skill.Kind == SkillKind.Strike)
            .Select(skill => skill.Range)
            .DefaultIfEmpty(0f)
            .Max();

    private static double HealthRatio(HeadlessHeroObservation hero)
        => hero.MaxHp <= 0 ? 0d : Math.Clamp((double)hero.CurrentHp / hero.MaxHp, 0d, 1d);
}
