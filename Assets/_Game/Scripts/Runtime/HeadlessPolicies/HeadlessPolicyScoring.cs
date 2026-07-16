using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;

namespace SM.HeadlessPolicies;

/// <summary>여섯 정책이 공유하는 공개 정보 기반 조합·배치·가치 추정 규칙.</summary>
internal static class HeadlessPolicyScoring
{
    private static readonly HashSet<string> FrontClasses = new(StringComparer.Ordinal)
    {
        "vanguard", "duelist",
    };

    public static IReadOnlyList<HeadlessHeroObservation> GreedyHeroes(HeadlessPolicyObservation observation)
        => observation.Roster.Take(observation.DeployCapacity).ToArray();

    public static IReadOnlyList<HeadlessPlacement> PlaceGreedy(
        IReadOnlyList<HeadlessHeroObservation> heroes,
        IReadOnlyList<DeploymentAnchorId> anchors)
    {
        var front = new Queue<DeploymentAnchorId>(anchors.Where(anchor => anchor.IsFrontRow()));
        var back = new Queue<DeploymentAnchorId>(anchors.Where(anchor => !anchor.IsFrontRow()));
        var placements = new List<HeadlessPlacement>(heroes.Count);
        foreach (var hero in heroes)
        {
            var prefersFront = PrefersFront(hero);
            var primary = prefersFront ? front : back;
            var fallback = prefersFront ? back : front;
            if (primary.Count > 0)
            {
                placements.Add(new HeadlessPlacement(primary.Dequeue(), hero.HeroId));
            }
            else if (fallback.Count > 0)
            {
                placements.Add(new HeadlessPlacement(fallback.Dequeue(), hero.HeroId));
            }
        }

        return placements;
    }

    public static IReadOnlyList<HeadlessPlacement> PlaceFormation(
        IReadOnlyList<HeadlessHeroObservation> heroes,
        IReadOnlyList<DeploymentAnchorId> anchors)
    {
        var remaining = anchors
            .OrderBy(AnchorRowRank)
            .ThenBy(AnchorLaneRank)
            .ToList();
        var orderedHeroes = heroes
            .OrderBy(hero => PrefersFront(hero) ? 0 : 1)
            .ThenBy(hero => IsSupport(hero) ? 0 : 1)
            .ThenByDescending(ReadinessScore)
            .ThenBy(hero => hero.HeroId, StringComparer.Ordinal)
            .ToArray();
        var placements = new List<HeadlessPlacement>(heroes.Count);
        foreach (var hero in orderedHeroes)
        {
            var prefersFront = PrefersFront(hero);
            var candidate = remaining
                .Where(anchor => anchor.IsFrontRow() == prefersFront)
                .OrderBy(anchor => FormationAnchorScore(hero, anchor))
                .ThenBy(anchor => anchor)
                .FirstOrDefault();
            if (!remaining.Contains(candidate))
            {
                candidate = remaining[0];
            }

            placements.Add(new HeadlessPlacement(candidate, hero.HeroId));
            remaining.Remove(candidate);
        }

        return placements;
    }

    public static IReadOnlyList<HeadlessHeroObservation> SelectBestCombination(
        HeadlessPolicyObservation observation,
        Func<IReadOnlyList<HeadlessHeroObservation>, double> score)
    {
        IReadOnlyList<HeadlessHeroObservation> best = null;
        var bestScore = double.NegativeInfinity;
        var bestSignature = string.Empty;
        foreach (var candidate in EnumerateCombinations(observation.Roster, observation.DeployCapacity))
        {
            var value = score(candidate);
            var signature = HeroSignature(candidate);
            if (value > bestScore || (Math.Abs(value - bestScore) < 0.000001d
                                      && string.CompareOrdinal(signature, bestSignature) < 0))
            {
                best = candidate;
                bestScore = value;
                bestSignature = signature;
            }
        }

        return best ?? GreedyHeroes(observation);
    }

    public static IEnumerable<IReadOnlyList<HeadlessHeroObservation>> EnumerateCombinations(
        IReadOnlyList<HeadlessHeroObservation> heroes,
        int count)
    {
        var buffer = new HeadlessHeroObservation[count];
        return Enumerate(0, 0);

        IEnumerable<IReadOnlyList<HeadlessHeroObservation>> Enumerate(int sourceIndex, int targetIndex)
        {
            if (targetIndex == count)
            {
                yield return buffer.ToArray();
                yield break;
            }

            var remainingNeeded = count - targetIndex;
            for (var index = sourceIndex; index <= heroes.Count - remainingNeeded; index++)
            {
                buffer[targetIndex] = heroes[index];
                foreach (var candidate in Enumerate(index + 1, targetIndex + 1))
                {
                    yield return candidate;
                }
            }
        }
    }

    public static IEnumerable<IReadOnlyList<HeadlessPlacement>> EnumeratePlacements(
        IReadOnlyList<HeadlessHeroObservation> heroes,
        IReadOnlyList<DeploymentAnchorId> anchors)
    {
        var used = new bool[anchors.Count];
        var buffer = new HeadlessPlacement[heroes.Count];
        return Enumerate(0);

        IEnumerable<IReadOnlyList<HeadlessPlacement>> Enumerate(int heroIndex)
        {
            if (heroIndex == heroes.Count)
            {
                yield return buffer.ToArray();
                yield break;
            }

            for (var anchorIndex = 0; anchorIndex < anchors.Count; anchorIndex++)
            {
                if (used[anchorIndex])
                {
                    continue;
                }

                used[anchorIndex] = true;
                buffer[heroIndex] = new HeadlessPlacement(anchors[anchorIndex], heroes[heroIndex].HeroId);
                foreach (var candidate in Enumerate(heroIndex + 1))
                {
                    yield return candidate;
                }

                used[anchorIndex] = false;
            }
        }
    }

    public static double EvaluateDeployment(
        HeadlessPolicyObservation observation,
        IReadOnlyList<HeadlessHeroObservation> heroes,
        IReadOnlyList<HeadlessPlacement> placements)
    {
        return heroes.Sum(ReadinessScore)
               + DoctrineScore(heroes)
               + FormationScore(heroes, placements)
               + CounterScore(observation.EnemyPreview, heroes, placements);
    }

    public static double DoctrineScore(IReadOnlyList<HeadlessHeroObservation> heroes)
    {
        var raceCounts = heroes.GroupBy(hero => hero.RaceId, StringComparer.Ordinal).Select(group => group.Count()).ToArray();
        var classCounts = heroes.GroupBy(hero => hero.ClassId, StringComparer.Ordinal).Select(group => group.Count()).ToArray();
        var maxRace = raceCounts.DefaultIfEmpty(0).Max();
        var maxClass = classCounts.DefaultIfEmpty(0).Max();
        var score = raceCounts.Where(count => count >= 2).Sum(count => count * 4d)
                    + classCounts.Where(count => count >= 2).Sum(count => count * 4d);
        if (maxRace >= 4)
        {
            score += 68d;
        }

        if (maxClass >= 3)
        {
            score += 62d;
        }

        return score;
    }

    public static double FormationSelectionScore(IReadOnlyList<HeadlessHeroObservation> heroes)
    {
        var distinctClasses = heroes.Select(hero => hero.ClassId).Distinct(StringComparer.Ordinal).Count();
        var frontCount = heroes.Count(PrefersFront);
        var backCount = heroes.Count(hero => !PrefersFront(hero));
        var score = distinctClasses * 10d;
        score += Math.Min(frontCount, 2) * 8d + Math.Min(backCount, 2) * 8d;
        score += heroes.Any(IsSupport) ? 18d : 0d;
        score += heroes.Any(hero => string.Equals(hero.ClassId, "ranger", StringComparison.Ordinal)) ? 10d : 0d;
        score -= Math.Abs(frontCount - backCount) * 5d;
        return score + heroes.Sum(ReadinessScore) * 0.2d;
    }

    public static double CounterSelectionScore(
        HeadlessEnemyPreview preview,
        IReadOnlyList<HeadlessHeroObservation> heroes)
        => heroes.Sum(hero => HeroCounterScore(preview, hero)) + FormationSelectionScore(heroes) * 0.5d;

    public static double ReadinessScore(HeadlessHeroObservation hero)
    {
        var healthRatio = hero.MaxHp > 0 ? (double)hero.CurrentHp / hero.MaxHp : 1d;
        return (hero.Level * 2d) + (hero.EquippedItemCount * 4d) + (healthRatio * 8d);
    }

    public static double RewardScore(
        HeadlessPolicyObservation observation,
        HeadlessRewardOption option,
        bool doctrineBias,
        bool formationBias,
        bool counterBias)
    {
        var score = option.Kind switch
        {
            HeadlessRewardKind.PermanentAugmentSlot => 100d + (option.PermanentSlotAmount * 20d),
            HeadlessRewardKind.Item => 48d,
            HeadlessRewardKind.TemporaryAugment => 44d,
            HeadlessRewardKind.Gold => option.GoldAmount * 1.5d,
            HeadlessRewardKind.Echo => option.EchoAmount * 1.2d,
            _ => 0d,
        };

        if (doctrineBias)
        {
            var deployed = observation.Roster.Where(hero => hero.IsDeployed).ToArray();
            if (deployed.Length == 0)
            {
                deployed = observation.Roster.Take(observation.DeployCapacity).ToArray();
            }

            if (PayloadMatchesRoster(option.PayloadId, deployed))
            {
                score += 24d;
            }
        }

        if (formationBias && ContainsAny(option.PayloadId, "heal", "guard", "shield", "support", "rescue", "barrier"))
        {
            score += 20d;
        }

        if (counterBias && ContainsAny(option.PayloadId, "pierce", "dive", "control", "cleanse", "range", "sunder"))
        {
            score += 16d;
        }

        return score;
    }

    public static string HeroSignature(IEnumerable<HeadlessHeroObservation> heroes)
        => string.Join("|", heroes.Select(hero => hero.HeroId).OrderBy(id => id, StringComparer.Ordinal));

    public static string PlacementSignature(IEnumerable<HeadlessPlacement> placements)
        => string.Join("|", placements
            .OrderBy(value => value.Anchor)
            .Select(value => $"{(int)value.Anchor}:{value.HeroId}"));

    public static bool PrefersFront(HeadlessHeroObservation hero)
        => FrontClasses.Contains(hero.ClassId)
           || string.Equals(hero.RoleTag, "anchor", StringComparison.Ordinal)
           || string.Equals(hero.RoleTag, "bruiser", StringComparison.Ordinal);

    private static double FormationScore(
        IReadOnlyList<HeadlessHeroObservation> heroes,
        IReadOnlyList<HeadlessPlacement> placements)
    {
        var heroById = heroes.ToDictionary(hero => hero.HeroId, StringComparer.Ordinal);
        var score = FormationSelectionScore(heroes);
        foreach (var placement in placements)
        {
            var hero = heroById[placement.HeroId];
            score += placement.Anchor.IsFrontRow() == PrefersFront(hero) ? 7d : -8d;
            score += placement.Anchor == hero.PreferredAnchor ? 2d : 0d;
        }

        var supportPlacements = placements
            .Where(value => IsSupport(heroById[value.HeroId]) && value.Anchor.IsBackRow())
            .ToArray();
        foreach (var support in supportPlacements)
        {
            if (placements.Any(value => value.Anchor.IsFrontRow()
                                        && value.Anchor.LaneIndex() == support.Anchor.LaneIndex()
                                        && PrefersFront(heroById[value.HeroId])))
            {
                score += 12d;
            }
        }

        return score;
    }

    private static double CounterScore(
        HeadlessEnemyPreview preview,
        IReadOnlyList<HeadlessHeroObservation> heroes,
        IReadOnlyList<HeadlessPlacement> placements)
    {
        if (!preview.IsAvailable)
        {
            return 0d;
        }

        var score = heroes.Sum(hero => HeroCounterScore(preview, hero));
        var heroById = heroes.ToDictionary(hero => hero.HeroId, StringComparer.Ordinal);
        foreach (var placement in placements)
        {
            var hero = heroById[placement.HeroId];
            if (string.Equals(hero.ClassId, "duelist", StringComparison.Ordinal)
                && preview.Units.Any(enemy => enemy.PreferredAnchor.IsBackRow()
                                              && enemy.PreferredAnchor.LaneIndex() == placement.Anchor.LaneIndex()))
            {
                score += 5d;
            }
        }

        return score;
    }

    private static double HeroCounterScore(HeadlessEnemyPreview preview, HeadlessHeroObservation hero)
    {
        if (!preview.IsAvailable)
        {
            return 0d;
        }

        return preview.Units.Sum(enemy => (hero.ClassId, enemy.ClassId) switch
        {
            ("mystic", "vanguard") => 7d,
            ("vanguard", "duelist") => 7d,
            ("duelist", "ranger") => 8d,
            ("ranger", "mystic") => 8d,
            _ => 0d,
        });
    }

    private static bool IsSupport(HeadlessHeroObservation hero)
        => string.Equals(hero.ClassId, "mystic", StringComparison.Ordinal)
           || ContainsAny(hero.RoleTag, "support", "healer", "peeler", "controller");

    private static int FormationAnchorScore(HeadlessHeroObservation hero, DeploymentAnchorId anchor)
    {
        var score = AnchorLaneRank(anchor) * 4;
        if (anchor == hero.PreferredAnchor)
        {
            score -= 2;
        }

        if (IsSupport(hero) && anchor is DeploymentAnchorId.BackCenter)
        {
            score -= 4;
        }

        return score;
    }

    private static int AnchorRowRank(DeploymentAnchorId anchor) => anchor.IsFrontRow() ? 0 : 1;

    private static int AnchorLaneRank(DeploymentAnchorId anchor)
        => anchor switch
        {
            DeploymentAnchorId.FrontCenter or DeploymentAnchorId.BackCenter => 0,
            DeploymentAnchorId.FrontTop or DeploymentAnchorId.BackTop => 1,
            _ => 2,
        };

    private static bool PayloadMatchesRoster(string payloadId, IReadOnlyList<HeadlessHeroObservation> heroes)
    {
        if (string.IsNullOrWhiteSpace(payloadId))
        {
            return false;
        }

        return heroes.Any(hero => ContainsAny(payloadId, hero.RaceId, hero.ClassId, hero.ArchetypeId));
    }

    private static bool ContainsAny(string value, params string[] tokens)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return tokens.Any(token => !string.IsNullOrWhiteSpace(token)
                                   && value.Contains(token, StringComparison.OrdinalIgnoreCase));
    }
}
