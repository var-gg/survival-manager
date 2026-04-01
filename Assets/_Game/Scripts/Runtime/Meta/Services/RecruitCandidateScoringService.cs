using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Core.Contracts;
using SM.Meta.Model;

namespace SM.Meta.Services;

public static class RecruitCandidateScoringService
{
    public static RecruitCandidateEvaluation Evaluate(
        CombatArchetypeTemplate template,
        TeamPlanProfile plan,
        ScoutDirective? directive,
        IReadOnlyList<HeroRecord> roster)
    {
        var score = new CandidatePlanScoreBreakdown();
        foreach (var tag in template.RecruitPlanTags ?? Array.Empty<string>())
        {
            if (plan.BreakpointGapsByTag.TryGetValue(tag, out var gap) && gap == 1)
            {
                score.BreakpointProgressScore = Math.Max(score.BreakpointProgressScore, 3);
            }

            if (plan.TopSynergyTagIds.Contains(tag) && !IsSoftSaturated(roster, tag))
            {
                score.NativeTagMatchScore = Math.Max(score.NativeTagMatchScore, 2);
            }

            if (plan.AugmentHookTags.Contains(tag))
            {
                score.AugmentHookScore = Math.Max(score.AugmentHookScore, 1);
            }
        }

        if (plan.NeedsFrontline && template.Behavior?.FormationLine == FormationLine.Frontline
            || plan.NeedsBackline && template.Behavior?.FormationLine == FormationLine.Backline
            || plan.NeedsSupport && string.Equals(template.RoleTag, "support", StringComparison.Ordinal))
        {
            score.RoleNeedScore = 2;
        }

        if (directive != null && RecruitmentTemplateResolver.MatchesDirective(template, directive))
        {
            score.ScoutDirectiveScore = 2;
        }

        if (IsOversaturated(roster, template))
        {
            score.OversaturationPenalty = 2;
        }

        var fit = score.Total >= 4
            ? CandidatePlanFit.OnPlan
            : score.Total >= 2
                ? CandidatePlanFit.Bridge
                : CandidatePlanFit.OffPlan;
        return new RecruitCandidateEvaluation(template.Id, fit, score, template.RecruitTier);
    }

    private static bool IsSoftSaturated(IReadOnlyList<HeroRecord> roster, string tag)
    {
        var count = roster.Count(hero =>
            string.Equals(hero.RaceId, tag, StringComparison.Ordinal)
            || string.Equals(hero.ClassId, tag, StringComparison.Ordinal));
        return count >= 3;
    }

    private static bool IsOversaturated(IReadOnlyList<HeroRecord> roster, CombatArchetypeTemplate template)
    {
        var matchingClassCount = roster.Count(hero => string.Equals(hero.ClassId, template.ClassId, StringComparison.Ordinal));
        return matchingClassCount >= 3;
    }
}
