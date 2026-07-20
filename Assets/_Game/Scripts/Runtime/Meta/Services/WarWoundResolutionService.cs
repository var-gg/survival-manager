using System;
using System.Collections.Generic;
using System.Linq;
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
}
