using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SM.HeadlessCensus;

/// <summary>수동 옵션 카탈로그 없이 E02 source/edge에서 witness와 comparator를 파생한다.</summary>
public static class OptionWitnessContractDeriver
{
    private static readonly string[] PromiseRelations =
    {
        BuildGrammarRelation.Produces,
        BuildGrammarRelation.Amplifies,
        BuildGrammarRelation.PaysOff,
    };

    public static IReadOnlyList<OptionWitnessContract> Derive(
        BuildGrammarTruthGraph graph,
        IEnumerable<BuildGrammarTruthSource> sources)
    {
        if (graph == null) throw new ArgumentNullException(nameof(graph));
        if (sources == null) throw new ArgumentNullException(nameof(sources));

        var sourceByOption = sources
            .Where(source => source != null
                             && source.Actionable
                             && OptionWitnessContract.IsSupportedSubjectKind(source.SubjectKind)
                             && !string.IsNullOrWhiteSpace(source.SubjectId))
            .OrderBy(source => source.SubjectKind, StringComparer.Ordinal)
            .ThenBy(source => source.SubjectId, StringComparer.Ordinal)
            .ToDictionary(
                source => OptionWitnessContract.StableOptionId(source.SubjectKind, source.SubjectId),
                source => source,
                StringComparer.Ordinal);
        var edgesByOption = graph.Edges
            .Where(edge => sourceByOption.ContainsKey(OptionWitnessContract.StableOptionId(edge.SubjectKind, edge.SubjectId)))
            .GroupBy(edge => OptionWitnessContract.StableOptionId(edge.SubjectKind, edge.SubjectId), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);

        var drafts = sourceByOption.Values.Select(source =>
        {
            var optionId = OptionWitnessContract.StableOptionId(source.SubjectKind, source.SubjectId);
            var edges = edgesByOption.TryGetValue(optionId, out var found)
                ? found
                : Array.Empty<BuildGrammarTruthEdge>();
            var promises = edges.Where(edge => PromiseRelations.Contains(edge.Relation, StringComparer.Ordinal))
                .OrderBy(edge => edge.Relation, StringComparer.Ordinal)
                .ThenBy(edge => edge.TargetKind, StringComparer.Ordinal)
                .ThenBy(edge => edge.TargetId, StringComparer.Ordinal)
                .ThenBy(edge => edge.TruthValue, StringComparer.Ordinal)
                .Select(ToPromise)
                .ToArray();
            var intended = edges.Where(edge => edge.Relation == BuildGrammarRelation.Requires)
                .Select(edge => $"{edge.TargetKind}:{edge.TargetId}")
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            var prerequisites = edges.Where(edge => edge.Relation == BuildGrammarRelation.Requires
                                                     && edge.TargetKind == "passive_node")
                .Select(edge => OptionWitnessContract.StableOptionId(BuildGrammarSubjectKind.Passive, edge.TargetId))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            var acquisition = edges.Where(edge => edge.Relation == BuildGrammarRelation.AcquiredBy)
                .Select(edge => edge.TargetId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            var explicitComparators = edges.Where(edge => edge.Relation == BuildGrammarRelation.Substitutes)
                .Select(edge => OptionWitnessContract.StableOptionId(edge.TargetKind, edge.TargetId));
            var groupedComparators = string.IsNullOrWhiteSpace(source.ComparatorGroupId)
                ? Array.Empty<string>()
                : sourceByOption.Values
                    .Where(other => !ReferenceEquals(other, source)
                                    && other.SubjectKind == source.SubjectKind
                                    && string.Equals(other.ComparatorGroupId, source.ComparatorGroupId, StringComparison.Ordinal)
                                    && string.Equals(other.BudgetBand, source.BudgetBand, StringComparison.Ordinal))
                    .Select(other => OptionWitnessContract.StableOptionId(other.SubjectKind, other.SubjectId));
            var comparators = explicitComparators.Concat(groupedComparators)
                .Where(sourceByOption.ContainsKey)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            var directions = promises.Select(value => value.ExpectedDeltaDirection).ToHashSet(StringComparer.Ordinal);
            var tradeoff = source.HasVisibleTradeoff
                           || edges.Any(edge => edge.Relation == BuildGrammarRelation.Conflicts)
                           || directions.Contains(OptionDeltaDirection.Positive)
                           && directions.Contains(OptionDeltaDirection.Negative);
            return new ContractDraft(
                source,
                optionId,
                promises,
                intended,
                prerequisites,
                acquisition,
                comparators,
                tradeoff);
        }).OrderBy(value => value.OptionId, StringComparer.Ordinal).ToArray();

        var draftById = drafts.ToDictionary(value => value.OptionId, StringComparer.Ordinal);
        return drafts.Select(draft =>
        {
            var comparatorPromises = draft.ComparatorOptionIds
                .Where(draftById.ContainsKey)
                .SelectMany(id => draftById[id].Promises)
                .ToArray();
            var uniqueUnlocks = draft.ComparatorOptionIds.Length == 0
                ? draft.Promises.Length
                : draft.Promises.Count(promise => !comparatorPromises.Any(other => SameSemantic(promise, other)));
            var structuralTrap = !draft.HasVisibleTradeoff
                                 && uniqueUnlocks == 0
                                 && draft.ComparatorOptionIds.Any(id => StructurallyDominates(draftById[id].Promises, draft.Promises));
            var structuralDominance = !draft.HasVisibleTradeoff
                                      && draft.ComparatorOptionIds.Any(id => StructurallyDominates(draft.Promises, draftById[id].Promises));
            return new OptionWitnessContract(
                draft.OptionId,
                draft.Source.SubjectKind,
                draft.Source.SubjectId,
                OptionWitnessContract.ResolveBaselineComparator(draft.Source.SubjectKind, draft.AcquisitionPaths),
                draft.Source.ComparatorGroupId ?? string.Empty,
                draft.Source.BudgetBand ?? string.Empty,
                draft.ComparatorOptionIds,
                draft.IntendedPredicates,
                draft.PrerequisiteOptionIds,
                draft.AcquisitionPaths,
                draft.Promises,
                uniqueUnlocks,
                draft.HasVisibleTradeoff,
                draft.Promises.Length > 0,
                draft.ComparatorOptionIds.Length > 0,
                structuralTrap,
                structuralDominance);
        }).ToArray();
    }

    private static OptionWitnessPromise ToPromise(BuildGrammarTruthEdge edge)
    {
        var magnitude = ResolveMagnitude(edge.TruthValue);
        var direction = double.IsNaN(magnitude)
            ? OptionDeltaDirection.Unknown
            : magnitude > 0d
                ? OptionDeltaDirection.Positive
                : magnitude < 0d
                    ? OptionDeltaDirection.Negative
                    : OptionDeltaDirection.Zero;
        return new OptionWitnessPromise(
            edge.EdgeId,
            edge.Relation,
            edge.TargetKind,
            edge.TargetId,
            edge.TruthValue ?? string.Empty,
            edge.ExpectedFeedbackWitness ?? string.Empty,
            direction,
            double.IsNaN(magnitude) ? 0d : magnitude);
    }

    private static double ResolveMagnitude(string truthValue)
    {
        if (string.IsNullOrWhiteSpace(truthValue)) return double.NaN;
        var values = truthValue.Split(';')
            .Select(value => value.Split(new[] { '=' }, 2))
            .Where(value => value.Length == 2)
            .ToDictionary(value => value[0], value => value[1], StringComparer.Ordinal);
        foreach (var key in new[]
                 {
                     "value", "magnitude", "power_flat", "physical_coefficient", "magical_coefficient",
                     "healing_coefficient", "health_coefficient",
                 })
        {
            if (values.TryGetValue(key, out var raw)
                && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                && Math.Abs(parsed) > 1e-12d)
            {
                return parsed;
            }
        }

        return values.Keys.Any(key => key is "value" or "magnitude" or "power_flat"
            or "physical_coefficient" or "magical_coefficient" or "healing_coefficient" or "health_coefficient")
            ? 0d
            : double.NaN;
    }

    private static bool StructurallyDominates(
        IReadOnlyList<OptionWitnessPromise> candidate,
        IReadOnlyList<OptionWitnessPromise> comparator)
    {
        if (candidate.Count == 0 || comparator.Count == 0) return false;
        var strict = false;
        foreach (var expected in comparator)
        {
            var matches = candidate.Where(value => SameSemantic(value, expected)).ToArray();
            if (matches.Length == 0) return false;
            var best = matches.Max(value => Math.Abs(value.DeclaredMagnitude));
            var baseline = Math.Abs(expected.DeclaredMagnitude);
            if (best + 1e-9d < baseline) return false;
            strict |= best > baseline + 1e-9d;
        }

        strict |= candidate.Any(value => !comparator.Any(other => SameSemantic(value, other)));
        return strict;
    }

    private static bool SameSemantic(OptionWitnessPromise left, OptionWitnessPromise right)
        => left.Relation == right.Relation
           && left.TargetKind == right.TargetKind
           && left.TargetId == right.TargetId;

    private sealed record ContractDraft(
        BuildGrammarTruthSource Source,
        string OptionId,
        OptionWitnessPromise[] Promises,
        string[] IntendedPredicates,
        string[] PrerequisiteOptionIds,
        string[] AcquisitionPaths,
        string[] ComparatorOptionIds,
        bool HasVisibleTradeoff);
}
