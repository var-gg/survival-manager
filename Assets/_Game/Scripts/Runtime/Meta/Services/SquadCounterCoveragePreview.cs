using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Meta.Model;

namespace SM.Meta.Services;

/// <summary>
/// 편성 화면 표면화용 — 배치된 분대의 아키타입 governance 를 모아 팀 카운터 커버리지(이 분대의 "대응 능력")를
/// 산출하고 강함/취약 차원으로 분류한다.
///
/// 집계는 전투/콘텐츠 거버넌스가 쓰는 <see cref="CounterCoverageAggregationService"/> 와 동일 SoT.
/// governance 열거 규칙은 <see cref="TeamPlanEvaluator"/> 와 일치(아키타입 + 스킬 + 시그니처/플렉스 패시브 + 기동 반응).
/// 순수 — Unity 의존 없이 템플릿/리포트만 받는다(FastUnit 대상). 스코프는 "지금 배치된 분대".
/// </summary>
public static class SquadCounterCoveragePreview
{
    /// <summary>8개 카운터 도구 차원 — 표시/분류 순서(<see cref="TeamCounterCoverageReport"/> 키와 일치).</summary>
    public static readonly IReadOnlyList<string> Dimensions = new[]
    {
        "ArmorShred", "Exposure", "GuardBreakMultiHit", "TrackingArea",
        "TenacityStability", "AntiHealShatter", "InterceptPeel", "CleaveWaveclear",
    };

    public static TeamCounterCoverageReport Evaluate(IEnumerable<CombatArchetypeTemplate> deployedTemplates)
    {
        if (deployedTemplates == null)
        {
            return new TeamCounterCoverageReport();
        }

        return CounterCoverageAggregationService.AggregateSummaries(
            deployedTemplates.Where(template => template != null).SelectMany(EnumerateGovernance));
    }

    /// <summary>리포트를 강함(Standard 이상)·취약(None)으로 분류. <see cref="Dimensions"/> 순서 유지.</summary>
    public static (IReadOnlyList<string> Strong, IReadOnlyList<string> Gaps) Classify(TeamCounterCoverageReport report)
    {
        var strong = new List<string>();
        var gaps = new List<string>();
        if (report == null)
        {
            return (strong, gaps);
        }

        foreach (var dimension in Dimensions)
        {
            var level = report.GetLevel(dimension);
            if (level >= CounterCoverageLevelValue.Standard)
            {
                strong.Add(dimension);
            }
            else if (level == CounterCoverageLevelValue.None)
            {
                gaps.Add(dimension);
            }
        }

        return (strong, gaps);
    }

    private static IEnumerable<ContentGovernanceSummary?> EnumerateGovernance(CombatArchetypeTemplate template)
    {
        yield return template.Governance;

        foreach (var skill in template.Skills ?? Array.Empty<BattleSkillSpec>())
        {
            yield return skill.Governance;
        }

        yield return template.SignaturePassive?.Governance;
        yield return template.FlexPassive?.Governance;
        yield return template.MobilityReaction?.Governance;
    }
}
