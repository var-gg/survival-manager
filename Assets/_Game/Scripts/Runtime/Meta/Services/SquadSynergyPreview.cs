using System;
using System.Collections.Generic;
using System.Linq;
using SM.Meta.Model;

namespace SM.Meta.Services;

/// <summary>
/// 편성 화면 표면화용 — 현재 배치된 분대의 태그를 content 시너지 카탈로그에 대조해
/// 시너지별 활성 상태(현재 수 · 도달 티어 · 다음 티어)를 산출한다.
///
/// 전투/밸런스가 소비하는 <c>content.SynergyCatalog</c> 와 동일 SoT 이며, 태그 카운팅 규칙은
/// <see cref="TeamPlanEvaluator"/> 와 일치한다(유닛별 race + class + archetype RecruitPlanTags).
/// 단 스코프가 다르다: TeamPlanEvaluator 는 로스터 전체(recruit planning), 본 헬퍼는 "지금 배치된 분대".
///
/// 순수 함수 — Unity/세션 의존 없이 태그 목록 + 카탈로그만 받는다(FastUnit 테스트 대상).
/// </summary>
public static class SquadSynergyPreview
{
    public static IReadOnlyList<SquadSynergySurface> Evaluate(
        IReadOnlyList<IReadOnlyList<string>> deployedUnitTags,
        IReadOnlyDictionary<string, SynergyTierTemplate> synergyCatalog)
    {
        if (deployedUnitTags == null || deployedUnitTags.Count == 0
            || synergyCatalog == null || synergyCatalog.Count == 0)
        {
            return Array.Empty<SquadSynergySurface>();
        }

        // 유닛별로 태그를 distinct 후 합산 — 한 유닛이 같은 태그를 중복으로 기여하지 않게.
        var tagCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var unitTags in deployedUnitTags)
        {
            if (unitTags == null)
            {
                continue;
            }

            foreach (var tag in unitTags
                         .Where(tag => !string.IsNullOrWhiteSpace(tag))
                         .Distinct(StringComparer.Ordinal))
            {
                tagCounts[tag] = tagCounts.TryGetValue(tag, out var existing) ? existing + 1 : 1;
            }
        }

        // 한 시너지(SynergyId)에 tier 템플릿이 여러 개(예: 2/4) — SynergyId 로 묶어 집계.
        var families = synergyCatalog.Values
            .Select(template => template.Rule)
            .Where(rule => rule != null && rule.Threshold > 0 && !string.IsNullOrWhiteSpace(rule.SynergyId))
            .GroupBy(rule => rule.SynergyId, StringComparer.Ordinal);

        var result = new List<SquadSynergySurface>();
        foreach (var family in families)
        {
            var countedTagId = family.First().CountedTagId;
            var current = tagCounts.TryGetValue(countedTagId, out var count) ? count : 0;
            var thresholds = family
                .Select(rule => rule.Threshold)
                .Distinct()
                .OrderBy(threshold => threshold)
                .ToList();

            var activeThreshold = 0;
            var nextThreshold = 0;
            foreach (var threshold in thresholds)
            {
                if (current >= threshold)
                {
                    activeThreshold = threshold;
                }
                else
                {
                    nextThreshold = threshold;
                    break;
                }
            }

            result.Add(new SquadSynergySurface(family.Key, countedTagId, current, activeThreshold, nextThreshold));
        }

        // 결정적 정렬: 활성 우선 → 현재 수 내림 → SynergyId.
        return result
            .OrderByDescending(surface => surface.IsActive)
            .ThenByDescending(surface => surface.CurrentCount)
            .ThenBy(surface => surface.SynergyId, StringComparer.Ordinal)
            .ToList();
    }
}

/// <summary>배치 분대의 단일 시너지 family 표면 상태. <see cref="ActiveThreshold"/> &gt; 0 이면 발현.</summary>
public sealed record SquadSynergySurface(
    string SynergyId,
    string CountedTagId,
    int CurrentCount,
    int ActiveThreshold,
    int NextThreshold)
{
    public bool IsActive => ActiveThreshold > 0;
}
