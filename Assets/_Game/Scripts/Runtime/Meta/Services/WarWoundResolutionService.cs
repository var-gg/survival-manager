using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Meta.Model;

namespace SM.Meta.Services;

/// <summary>
/// 승리 결과에서 run-scoped 전상을 선택한다. 입력 순서와 무관하게 HP 비율, hero id ordinal 순으로 판정한다.
/// </summary>
public static class WarWoundResolutionService
{
    public static WarWoundResolutionResult Resolve(
        ActiveRunState run,
        bool victory,
        IReadOnlyList<WarWoundCandidate> candidates,
        WarWoundSpec spec)
    {
        if (run == null) throw new ArgumentNullException(nameof(run));
        if (candidates == null) throw new ArgumentNullException(nameof(candidates));
        if (spec == null) throw new ArgumentNullException(nameof(spec));

        var activeWounds = (run.ActiveWoundHeroIds ?? Array.Empty<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
        if ((!victory && !spec.ApplyWoundOnLoss)
            || spec.MaxWoundsAppliedPerBattle <= 0
            || spec.MaxActiveWounds <= activeWounds.Count
            || spec.WoundStacksPerUnitMax <= 0)
        {
            return new WarWoundResolutionResult(run, Array.Empty<string>());
        }

        var deployedHeroIds = (run.BattleDeployHeroIds ?? Array.Empty<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);
        var activeWoundSet = activeWounds.ToHashSet(StringComparer.Ordinal);
        var availableSlots = Math.Max(0, spec.MaxActiveWounds - activeWounds.Count);
        var applyCount = Math.Min(spec.MaxWoundsAppliedPerBattle, availableSlots);
        var applied = candidates
            .Where(candidate => candidate != null
                                && deployedHeroIds.Contains(candidate.HeroId)
                                && !activeWoundSet.Contains(candidate.HeroId)
                                && candidate.MaxHealth > 0f)
            .Select(candidate => new
            {
                candidate.HeroId,
                Ratio = candidate.EndHealth / candidate.MaxHealth,
            })
            .Where(candidate => candidate.Ratio < spec.WoundTriggerHpRatio)
            .GroupBy(candidate => candidate.HeroId, StringComparer.Ordinal)
            .Select(group => group.OrderBy(candidate => candidate.Ratio).First())
            .OrderBy(candidate => candidate.Ratio)
            .ThenBy(candidate => candidate.HeroId, StringComparer.Ordinal)
            .Take(applyCount)
            .Select(candidate => candidate.HeroId)
            .ToArray();

        if (applied.Length == 0)
        {
            return new WarWoundResolutionResult(run, Array.Empty<string>());
        }

        activeWounds.AddRange(applied);
        var updatedRun = run with
        {
            ActiveWoundHeroIds = activeWounds
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray(),
        };
        return new WarWoundResolutionResult(updatedRun, applied);
    }

    /// <summary>사건 결과용 직접 전상. 배치 서수가 가장 낮은 미전상 전열원 한 명만 고른다.</summary>
    public static WarWoundResolutionResult InflictOrdinalFrontliner(
        ActiveRunState run,
        WarWoundSpec spec)
    {
        if (run == null) throw new ArgumentNullException(nameof(run));
        if (spec == null) throw new ArgumentNullException(nameof(spec));

        var activeWounds = NormalizeActiveWounds(run);
        if (spec.MaxActiveWounds <= activeWounds.Count || spec.WoundStacksPerUnitMax <= 0)
        {
            return new WarWoundResolutionResult(run, Array.Empty<string>());
        }

        var deployed = (run.BattleDeployHeroIds ?? Array.Empty<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);
        var target = (run.Blueprint?.DeploymentAssignments
                      ?? new Dictionary<DeploymentAnchorId, string>())
            .Where(pair => pair.Key.IsFrontRow()
                           && !string.IsNullOrWhiteSpace(pair.Value)
                           && deployed.Contains(pair.Value)
                           && !activeWounds.Contains(pair.Value, StringComparer.Ordinal))
            .OrderBy(pair => (int)pair.Key)
            .Select(pair => pair.Value)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(target))
        {
            return new WarWoundResolutionResult(run, Array.Empty<string>());
        }

        activeWounds.Add(target);
        return new WarWoundResolutionResult(
            run with { ActiveWoundHeroIds = activeWounds.OrderBy(id => id, StringComparer.Ordinal).ToArray() },
            new[] { target });
    }

    /// <summary>배치 서수가 가장 낮은 전상자 한 명을 치료한다. 배치 밖 잔여 전상은 id ordinal로 후순위 처리한다.</summary>
    public static WarWoundResolutionResult CureOrdinal(ActiveRunState run)
    {
        if (run == null) throw new ArgumentNullException(nameof(run));

        var activeWounds = NormalizeActiveWounds(run);
        if (activeWounds.Count == 0)
        {
            return new WarWoundResolutionResult(run, Array.Empty<string>());
        }

        var activeSet = activeWounds.ToHashSet(StringComparer.Ordinal);
        var target = (run.Blueprint?.DeploymentAssignments
                      ?? new Dictionary<DeploymentAnchorId, string>())
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value) && activeSet.Contains(pair.Value))
            .OrderBy(pair => (int)pair.Key)
            .Select(pair => pair.Value)
            .FirstOrDefault()
            ?? activeWounds[0];
        activeWounds.RemoveAll(id => string.Equals(id, target, StringComparison.Ordinal));
        return new WarWoundResolutionResult(
            run with { ActiveWoundHeroIds = activeWounds.ToArray() },
            new[] { target });
    }

    private static List<string> NormalizeActiveWounds(ActiveRunState run)
    {
        return (run.ActiveWoundHeroIds ?? Array.Empty<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
    }
}
