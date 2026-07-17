using System;
using System.Collections.Generic;
using System.Linq;
using SM.HeadlessCensus;

namespace SM.Editor.Validation;

/// <summary>candidate에만 E05.5 탐색기를 호출해 option/comparator의 future semantic track을 비교한다.</summary>
internal static class H100OptionContinuationProbe
{
    public static IReadOnlyList<OptionContinuationComparison> EvaluateCandidates(
        IReadOnlyList<OptionWitnessContract> contracts,
        IReadOnlyCollection<string> candidateIds)
    {
        var byId = contracts.ToDictionary(value => value.OptionId, StringComparer.Ordinal);
        var results = new List<OptionContinuationComparison>();
        foreach (var optionId in candidateIds.OrderBy(value => value, StringComparer.Ordinal))
        {
            if (!byId.TryGetValue(optionId, out var option) || option.Promises.Count == 0)
            {
                results.Add(OptionContinuationComparison.Unmeasured(optionId, "candidate", "no-promise-contract"));
                continue;
            }

            var comparator = option.ComparatorOptionIds.Select(id => byId.GetValueOrDefault(id)).FirstOrDefault(value => value != null);
            var semanticIds = option.Promises.Select(SemanticId).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            var comparatorIds = (comparator?.Promises ?? Array.Empty<OptionWitnessPromise>())
                .Select(SemanticId).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            var contract = new ConceptContract(
                semanticIds.Select(value => $"effect.ready:{value}").ToArray(),
                Array.Empty<string>(),
                option.Promises.First().ExpectedFeedbackWitness,
                option.ComparatorOptionIds,
                Array.Empty<string>(),
                Array.Empty<string>(),
                "oracle-candidate",
                Array.Empty<string>());
            var lever = ResolveLever(option.AcquisitionPaths);
            var withInput = SearchInput(contract, lever, Choice($"select:{optionId}", semanticIds));
            var withoutInput = SearchInput(
                contract,
                lever,
                comparator == null
                    ? IntentTrackChoice.NoOp($"preserve-budget:{option.BaselineComparatorId}")
                    : Choice($"select:{comparator.OptionId}", comparatorIds));
            results.Add(OptionContinuationOracle.Evaluate(optionId, "candidate-authored-offer", withInput, withoutInput));
        }

        return results;
    }

    private static IntentTrackSearchInput SearchInput(
        ConceptContract contract,
        string lever,
        IntentTrackChoice choice)
        => new(
            contract,
            IntentTrackState.Empty,
            new[] { new IntentTrackAgencyWindow(0, lever, "option-trap-candidate", 0, new[] { choice }) },
            new[] { lever },
            CommitWindowIndex: 0,
            HorizonWindowCount: 1);

    private static IntentTrackChoice Choice(string id, IReadOnlyList<string> activeEffects)
        => new(
            id,
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<IntentTrackRosterMember>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            activeEffects,
            0,
            0,
            0,
            0,
            0,
            0,
            Array.Empty<string>(),
            Array.Empty<IntentTrackTagCount>(),
            Array.Empty<string>(),
            activeEffects,
            Array.Empty<string>(),
            null,
            activeEffects,
            true);

    private static string SemanticId(OptionWitnessPromise promise)
        => $"promise:{promise.Relation}:{promise.TargetKind}:{promise.TargetId}";

    private static string ResolveLever(IEnumerable<string> paths)
    {
        var values = (paths ?? Array.Empty<string>()).ToHashSet(StringComparer.Ordinal);
        if (values.Contains(IntentTrackLeverId.LevelNode)) return IntentTrackLeverId.LevelNode;
        if (values.Contains(IntentTrackLeverId.Refit)) return IntentTrackLeverId.Refit;
        if (values.Contains(IntentTrackLeverId.Recruit)) return IntentTrackLeverId.Recruit;
        return IntentTrackLeverId.Reward;
    }
}
