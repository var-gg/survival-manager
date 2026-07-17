using System;
using System.Collections.Generic;
using System.Linq;

namespace SM.HeadlessCensus;

public static class IntentTrackVariantAvailabilityKind
{
    public const string V1Track = "v1_track";
    public const string LeverPending = "lever_pending";
    public const string TrueUnavailable = "true_unavailable";
}

public sealed record IntentTrackVariantSearchInput(
    string VariantId,
    ConceptContract Contract);

public sealed record IntentTrackAnchorSearchInput(
    string AnchorId,
    IReadOnlyList<IntentTrackVariantSearchInput> Variants,
    IntentTrackState InitialState,
    IReadOnlyList<IntentTrackAgencyWindow> Windows,
    IReadOnlyList<string> EnabledLeverIds,
    int CommitWindowIndex,
    int HorizonWindowCount);

public sealed record IntentTrackVariantSearchResult(
    string VariantId,
    string AvailabilityTier,
    string AvailabilityKind,
    IReadOnlyList<string> PendingLeverIds,
    IntentTrackSearchResult Search);

/// <summary>anchor의 모든 E03 variant를 OR로 평가한 결과와 variant별 원인 분해.</summary>
public sealed record IntentTrackAnchorSearchResult(
    string AnchorId,
    bool TrackAvailable,
    string SelectedVariantId,
    IntentTrackSearchResult SelectedSearch,
    int LeverPendingVariantCount,
    int TrueUnavailableVariantCount,
    int PredicateEvaluationCount,
    int PredicateCacheHitCount,
    IReadOnlyList<IntentTrackVariantSearchResult> VariantResults);

/// <summary>
/// 동일 offer/state를 anchor variant 전체에 적용한다. identity 술어 cache는 variant search가 공유하므로
/// 같은 배치·보상 상태의 같은 술어를 다시 판정하지 않는다.
/// </summary>
public static class IntentTrackAnchorEvaluator
{
    public static IntentTrackAnchorSearchResult Evaluate(IntentTrackAnchorSearchInput input)
    {
        Validate(input);
        var cache = new IntentTrackPredicateEvaluator.IntentTrackPredicateEvaluationCache();
        var variants = input.Variants
            .OrderBy(value => value.VariantId, StringComparer.Ordinal)
            .Select(variant =>
            {
                var search = IntentTrackEvaluator.Evaluate(new IntentTrackSearchInput(
                    variant.Contract,
                    input.InitialState,
                    input.Windows,
                    input.EnabledLeverIds,
                    input.CommitWindowIndex,
                    input.HorizonWindowCount), cache);
                var pending = search.TrackAvailable
                    ? Array.Empty<string>()
                    : ResolvePendingLevers(variant.Contract, search, input.EnabledLeverIds);
                var kind = search.TrackAvailable
                    ? IntentTrackVariantAvailabilityKind.V1Track
                    : pending.Length > 0
                        ? IntentTrackVariantAvailabilityKind.LeverPending
                        : IntentTrackVariantAvailabilityKind.TrueUnavailable;
                return new IntentTrackVariantSearchResult(
                    variant.VariantId,
                    variant.Contract.AvailabilityTier,
                    kind,
                    pending,
                    search);
            })
            .ToArray();

        var selected = variants.Where(value => value.Search.TrackAvailable)
                           .OrderBy(value => NormalizeTime(value.Search.RealizationTime))
                           .ThenBy(value => NormalizeTime(value.Search.FirstProgressTime))
                           .ThenBy(value => value.VariantId, StringComparer.Ordinal)
                           .FirstOrDefault()
                       ?? variants.OrderByDescending(value => IdentityCompletion(value.Search))
                           .ThenByDescending(value => value.Search.FinalIdentityPredicateCount)
                           .ThenBy(value => value.Search.MaxAgencyDrought)
                           .ThenBy(value => value.VariantId, StringComparer.Ordinal)
                           .First();
        return new IntentTrackAnchorSearchResult(
            input.AnchorId,
            variants.Any(value => value.Search.TrackAvailable),
            selected.VariantId,
            selected.Search,
            variants.Count(value => value.AvailabilityKind == IntentTrackVariantAvailabilityKind.LeverPending),
            variants.Count(value => value.AvailabilityKind == IntentTrackVariantAvailabilityKind.TrueUnavailable),
            cache.EvaluationCount,
            cache.CacheHitCount,
            variants);
    }

    private static string[] ResolvePendingLevers(
        ConceptContract contract,
        IntentTrackSearchResult search,
        IReadOnlyList<string> enabledLeverIds)
    {
        var enabled = (enabledLeverIds ?? Array.Empty<string>()).ToHashSet(StringComparer.Ordinal);
        var declared = (contract.PivotConditions ?? Array.Empty<string>())
            .Where(value => value.StartsWith("acquisition_path_unavailable:", StringComparison.Ordinal))
            .Select(value => value.Substring("acquisition_path_unavailable:".Length))
            .ToHashSet(StringComparer.Ordinal);
        var pending = new HashSet<string>(StringComparer.Ordinal);
        foreach (var result in search.IdentityPredicateResults.Where(value => !value.Satisfied))
        {
            if (result.Predicate.StartsWith("owned:passive:", StringComparison.Ordinal))
            {
                AddIfClosed(pending, enabled, IntentTrackLeverId.LevelNode);
            }
            else if (result.Predicate.StartsWith("owned:affix:", StringComparison.Ordinal))
            {
                AddIfClosed(pending, enabled, IntentTrackLeverId.Refit);
            }
            else if (result.Predicate.StartsWith("owned:skill:", StringComparison.Ordinal))
            {
                AddDeclaredClosed(pending, enabled, declared,
                    IntentTrackLeverId.Recruit, IntentTrackLeverId.LevelNode, IntentTrackLeverId.Reward);
            }
            else if (result.Predicate.StartsWith("owned:synergy:", StringComparison.Ordinal)
                     || result.PredicateKind is IntentTrackPredicateEvaluator.BuildTagCountKind
                         or IntentTrackPredicateEvaluator.BuildTagPresenceKind
                         or IntentTrackPredicateEvaluator.TeamRuleKind)
            {
                AddIfClosed(pending, enabled, IntentTrackLeverId.Recruit);
            }
            else if (result.PredicateKind == IntentTrackPredicateEvaluator.EffectReadyKind)
            {
                AddDeclaredClosed(pending, enabled, declared,
                    IntentTrackLeverId.Recruit, IntentTrackLeverId.LevelNode, IntentTrackLeverId.Refit, IntentTrackLeverId.Reward);
            }
        }

        return pending.OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    private static void AddDeclaredClosed(
        ISet<string> pending,
        ISet<string> enabled,
        ISet<string> declared,
        params string[] levers)
    {
        foreach (var lever in levers.Where(declared.Contains))
        {
            AddIfClosed(pending, enabled, lever);
        }
    }

    private static void AddIfClosed(ISet<string> pending, ISet<string> enabled, string lever)
    {
        if (!enabled.Contains(lever))
        {
            pending.Add(lever);
        }
    }

    private static double IdentityCompletion(IntentTrackSearchResult search)
        => search.TargetIdentityPredicateCount <= 0
            ? 0d
            : (double)search.FinalIdentityPredicateCount / search.TargetIdentityPredicateCount;

    private static int NormalizeTime(int value) => value < 0 ? int.MaxValue : value;

    private static void Validate(IntentTrackAnchorSearchInput input)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        if (string.IsNullOrWhiteSpace(input.AnchorId)) throw new ArgumentException("Anchor id is required.", nameof(input));
        if (input.InitialState == null) throw new ArgumentException("Anchor initial state is required.", nameof(input));
        if (input.Variants == null || input.Variants.Count == 0)
        {
            throw new ArgumentException("Anchor variants are required.", nameof(input));
        }

        if (input.Variants.Any(value => value == null
                                        || string.IsNullOrWhiteSpace(value.VariantId)
                                        || value.Contract == null))
        {
            throw new ArgumentException("Anchor variant id/contract is required.", nameof(input));
        }

        var duplicate = input.Variants.GroupBy(value => value.VariantId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate != null)
        {
            throw new ArgumentException($"Duplicate anchor variant id: {duplicate.Key}", nameof(input));
        }
    }
}
