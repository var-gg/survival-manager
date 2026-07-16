using System;
using System.Collections.Generic;
using System.Linq;

namespace SM.HeadlessMetrics;

/// <summary>규칙 id별 원시 발현 횟수. 직렬화 전에 id ordinal 순서로 정규화한다.</summary>
public sealed record MetricCount(string Id, int Count)
{
    public static IReadOnlyList<MetricCount> Normalize(IEnumerable<MetricCount>? source)
    {
        return (source ?? Array.Empty<MetricCount>())
            .Where(item => item != null && !string.IsNullOrWhiteSpace(item.Id) && item.Count != 0)
            .GroupBy(item => item.Id, StringComparer.Ordinal)
            .Select(group => new MetricCount(group.Key, group.Sum(item => item.Count)))
            .OrderBy(item => item.Id, StringComparer.Ordinal)
            .ToArray();
    }
}
