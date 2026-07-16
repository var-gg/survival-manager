using System;
using System.Collections.Generic;
using System.Linq;
using SM.HeadlessCensus;
using SM.HeadlessMetrics;

namespace SM.Editor.Validation;

/// <summary>E03 계약 subject와 E02 위반 subject를 exact id로 조인한다.</summary>
internal static class H100IntentTrackSurfaceJoin
{
    public static bool HasRelevantGap(
        ConceptContract contract,
        IEnumerable<string> recipeComponentIds,
        IEnumerable<InformationSurfaceGap> gaps)
    {
        if (contract == null) throw new ArgumentNullException(nameof(contract));
        var subjects = contract.IdentityPredicates
            .Concat(contract.ProgressMilestones)
            .Concat(contract.AllowedSubstitutions)
            .Concat(recipeComponentIds ?? Array.Empty<string>())
            .SelectMany(Subjects)
            .ToHashSet(StringComparer.Ordinal);
        return (gaps ?? Array.Empty<InformationSurfaceGap>())
            .Any(gap => gap != null && subjects.Contains(gap.SubjectId));
    }

    private static IEnumerable<string> Subjects(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) yield break;
        foreach (var token in value.Split(
                     new[] { ':', '(', ')', '>', '<', '=', '/', ';', ',', ' ', '+', '|' },
                     StringSplitOptions.RemoveEmptyEntries))
        {
            var normalized = token.Trim();
            var at = normalized.IndexOf('@');
            if (at > 0) normalized = normalized.Substring(0, at);
            if (normalized.StartsWith("affix_", StringComparison.Ordinal)
                || normalized.StartsWith("augment_", StringComparison.Ordinal)
                || normalized.StartsWith("item_", StringComparison.Ordinal)
                || normalized.StartsWith("passive_", StringComparison.Ordinal)
                || normalized.StartsWith("skill_", StringComparison.Ordinal)
                || normalized.StartsWith("synergy_", StringComparison.Ordinal)
                || normalized.StartsWith("rule.", StringComparison.Ordinal))
            {
                yield return normalized;
            }
        }
    }
}
