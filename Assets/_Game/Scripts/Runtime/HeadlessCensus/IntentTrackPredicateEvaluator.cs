using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace SM.HeadlessCensus;

/// <summary>
/// ConceptContract identity/milestone 문자열을 명시적으로 해석한다.
/// 알 수 없는 identity 술어는 false로 삼키지 않고 예외로 올려 측정 커버리지 이격을 드러낸다.
/// </summary>
public static class IntentTrackPredicateEvaluator
{
    public const string BuildTagCountKind = "build_tag_count";
    public const string BuildTagPresenceKind = "build_tag_presence";
    public const string OwnedComponentKind = "owned_component";
    public const string EffectReadyKind = "effect_ready";
    public const string TeamRuleKind = "team_rule";
    public const string FormationKind = "formation";

    private const string ContainsTagPrefix = "build.contains_tag:";
    private const string OwnedPrefix = "owned:";
    private const string EffectReadyPrefix = "effect.ready:";
    private const string TeamRulePrefix = "build.team_rule=";
    private const string FormationPrefix = "formation.";

    public static IReadOnlyList<string> RequireSupportedIdentityPredicates(IEnumerable<string> predicates)
        => (predicates ?? Array.Empty<string>())
            .Select(RequireSupportedIdentityPredicate)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

    public static string RequireSupportedIdentityPredicate(string predicate)
    {
        if (TryParseCountIdentity(predicate, out _, out _))
        {
            return BuildTagCountKind;
        }

        if (HasNonEmptyTail(predicate, ContainsTagPrefix))
        {
            return BuildTagPresenceKind;
        }

        if (HasNonEmptyTail(predicate, OwnedPrefix))
        {
            return OwnedComponentKind;
        }

        if (HasNonEmptyTail(predicate, EffectReadyPrefix))
        {
            return EffectReadyKind;
        }

        if (HasNonEmptyTail(predicate, TeamRulePrefix))
        {
            return TeamRuleKind;
        }

        if (!string.IsNullOrWhiteSpace(predicate)
            && predicate.StartsWith(FormationPrefix, StringComparison.Ordinal))
        {
            ValidateFormationPredicate(predicate);
            return FormationKind;
        }

        throw new NotSupportedException($"Unsupported intent-track identity predicate: '{predicate ?? "<null>"}'.");
    }

    public static IntentTrackIdentityPredicateResult EvaluateIdentityPredicate(
        string predicate,
        IntentTrackState state)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));
        return new IntentTrackPredicateEvaluationCache().Evaluate(predicate, state);
    }

    public static bool SatisfiesFormationPredicate(string predicate, FormationFeatures? features)
    {
        var clauses = ParseFormationPredicate(predicate);
        if (features == null)
        {
            return false;
        }

        foreach (var clause in clauses)
        {
            if (clause.Profile != null)
            {
                if (!string.Equals(ConceptFormationProfile.Classify(features), clause.Profile, StringComparison.Ordinal))
                {
                    return false;
                }

                continue;
            }

            var actual = clause.Key switch
            {
                "formation.frontline_count" => features.FrontlineCount,
                "formation.protected_slot_count" => features.ProtectedSlotCount,
                "formation.flank_rear_exposure_score" => features.FlankRearExposureScore,
                "formation.backline_accessibility" => features.BacklineAccessibility,
                _ => throw new InvalidOperationException($"Unreachable formation key: {clause.Key}"),
            };
            if (!Compare(actual, clause.Operator!, clause.Expected))
            {
                return false;
            }
        }

        return true;
    }

    internal static bool MilestoneSatisfied(string milestone, IntentTrackState state)
    {
        if (TryParseCountMilestone(milestone, out var tag, out var required))
        {
            return TagCount(state, tag) >= required;
        }

        const string acquire = "acquire:";
        if (milestone.StartsWith(acquire, StringComparison.Ordinal))
        {
            return OwnedComponents(state).Contains(milestone.Substring(acquire.Length));
        }

        const string activate = "activate:";
        if (milestone.StartsWith(activate, StringComparison.Ordinal))
        {
            return state.ActiveEffectIds.Contains(milestone.Substring(activate.Length), StringComparer.Ordinal);
        }

        const string deployStatus = "deploy.status:";
        if (milestone.StartsWith(deployStatus, StringComparison.Ordinal))
        {
            return state.ActiveEffectIds.Contains($"status:{milestone.Substring(deployStatus.Length)}", StringComparer.Ordinal);
        }

        if (milestone.StartsWith(TeamRulePrefix, StringComparison.Ordinal))
        {
            return state.ActiveTeamRuleIds.Contains(milestone.Substring(TeamRulePrefix.Length), StringComparer.Ordinal);
        }

        return milestone.StartsWith(FormationPrefix, StringComparison.Ordinal)
               && SatisfiesFormationPredicate(milestone, state.Formation);
    }

    internal static HashSet<string> OwnedComponents(IntentTrackState state)
        => state.OwnedComponentIds
            .Concat(state.InventoryComponentIds)
            .Concat(state.SkillIds)
            .Concat(state.PassiveIds)
            .Concat(state.ActiveComponentIds)
            .ToHashSet(StringComparer.Ordinal);

    private static bool EvaluateCore(string predicate, string kind, IntentTrackState state)
    {
        return kind switch
        {
            BuildTagCountKind => TryParseCountIdentity(predicate, out var tag, out var threshold)
                                 && TagCount(state, tag) >= threshold,
            BuildTagPresenceKind => TagCount(state, predicate.Substring(ContainsTagPrefix.Length)) > 0,
            OwnedComponentKind => OwnedComponents(state).Contains(predicate.Substring(OwnedPrefix.Length)),
            EffectReadyKind => state.ActiveEffectIds.Contains(
                predicate.Substring(EffectReadyPrefix.Length), StringComparer.Ordinal),
            TeamRuleKind => state.ActiveTeamRuleIds.Contains(
                predicate.Substring(TeamRulePrefix.Length), StringComparer.Ordinal),
            FormationKind => SatisfiesFormationPredicate(predicate, state.Formation),
            _ => throw new InvalidOperationException($"Unreachable identity predicate kind: {kind}"),
        };
    }

    private static int TagCount(IntentTrackState state, string tag)
        => state.DeployedTagCounts.FirstOrDefault(value => string.Equals(value.TagId, tag, StringComparison.Ordinal))?.Count ?? 0;

    private static bool TryParseCountIdentity(string? value, out string tag, out int threshold)
    {
        tag = string.Empty;
        threshold = 0;
        const string prefix = "build.count_tag(";
        if (value == null || !value.StartsWith(prefix, StringComparison.Ordinal)) return false;
        var close = value.IndexOf(')', prefix.Length);
        if (close <= prefix.Length || !value.Substring(close + 1).StartsWith(">=", StringComparison.Ordinal)) return false;
        tag = value.Substring(prefix.Length, close - prefix.Length);
        return int.TryParse(value.Substring(close + 3), NumberStyles.Integer, CultureInfo.InvariantCulture, out threshold)
               && threshold > 0;
    }

    private static bool TryParseCountMilestone(string value, out string tag, out int required)
    {
        tag = string.Empty;
        required = 0;
        const string prefix = "build.count_tag(";
        if (!value.StartsWith(prefix, StringComparison.Ordinal)) return false;
        var close = value.IndexOf(')', prefix.Length);
        if (close < 0 || close + 1 >= value.Length || value[close + 1] != '=') return false;
        tag = value.Substring(prefix.Length, close - prefix.Length);
        return int.TryParse(value.Substring(close + 2).Split('/')[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out required);
    }

    private static bool HasNonEmptyTail(string? value, string prefix)
        => value != null
           && value.StartsWith(prefix, StringComparison.Ordinal)
           && value.Length > prefix.Length;

    private static void ValidateFormationPredicate(string predicate)
        => _ = ParseFormationPredicate(predicate);

    private static IReadOnlyList<FormationClause> ParseFormationPredicate(string predicate)
    {
        if (string.IsNullOrWhiteSpace(predicate))
        {
            throw new NotSupportedException("Intent-track formation predicate is empty.");
        }

        var values = predicate.Split(new[] { " and " }, StringSplitOptions.None);
        if (values.Length == 0 || values.Any(string.IsNullOrWhiteSpace))
        {
            throw new NotSupportedException($"Malformed intent-track formation predicate: '{predicate}'.");
        }

        var clauses = new List<FormationClause>(values.Length);
        foreach (var raw in values)
        {
            var value = raw.Trim();
            const string profilePrefix = "formation.profile=";
            if (value.StartsWith(profilePrefix, StringComparison.Ordinal))
            {
                var profile = value.Substring(profilePrefix.Length);
                if (profile is not (ConceptFormationProfile.FortifiedLine
                    or ConceptFormationProfile.ForwardSpear
                    or ConceptFormationProfile.BaitedGap
                    or ConceptFormationProfile.ScreenedBackline
                    or ConceptFormationProfile.OpenSkirmish))
                {
                    throw new NotSupportedException($"Unsupported intent-track formation profile: '{profile}'.");
                }

                clauses.Add(new FormationClause(string.Empty, null, 0d, profile));
                continue;
            }

            var key = new[]
                {
                    "formation.frontline_count",
                    "formation.protected_slot_count",
                    "formation.flank_rear_exposure_score",
                    "formation.backline_accessibility",
                }
                .FirstOrDefault(candidate => value.StartsWith(candidate, StringComparison.Ordinal));
            if (key == null)
            {
                throw new NotSupportedException($"Unsupported intent-track formation clause: '{value}'.");
            }

            var suffix = value.Substring(key.Length);
            var operation = new[] { ">=", "<=", ">", "<", "=" }
                .FirstOrDefault(candidate => suffix.StartsWith(candidate, StringComparison.Ordinal));
            if (operation == null
                || !double.TryParse(suffix.Substring(operation.Length), NumberStyles.Float, CultureInfo.InvariantCulture, out var expected))
            {
                throw new NotSupportedException($"Malformed intent-track formation comparison: '{value}'.");
            }

            clauses.Add(new FormationClause(key, operation, expected, null));
        }

        return clauses;
    }

    private static bool Compare(double actual, string operation, double expected)
        => operation switch
        {
            ">=" => actual >= expected,
            "<=" => actual <= expected,
            ">" => actual > expected,
            "<" => actual < expected,
            "=" => Math.Abs(actual - expected) <= 1e-9d,
            _ => throw new InvalidOperationException($"Unreachable formation comparison: {operation}"),
        };

    private sealed record FormationClause(string Key, string? Operator, double Expected, string? Profile);

    internal sealed class IntentTrackPredicateEvaluationCache
    {
        private readonly ConcurrentDictionary<string, Lazy<IntentTrackIdentityPredicateResult>> _results =
            new(StringComparer.Ordinal);
        private int _evaluationCount;
        private int _cacheHitCount;

        public int EvaluationCount => Volatile.Read(ref _evaluationCount);
        public int CacheHitCount => Volatile.Read(ref _cacheHitCount);

        public IntentTrackIdentityPredicateResult Evaluate(string predicate, IntentTrackState state)
        {
            var kind = RequireSupportedIdentityPredicate(predicate);
            var key = $"{predicate}\u001f{PredicateStateSignature(predicate, kind, state)}";
            var candidate = new Lazy<IntentTrackIdentityPredicateResult>(
                () =>
                {
                    Interlocked.Increment(ref _evaluationCount);
                    return new IntentTrackIdentityPredicateResult(predicate, kind, EvaluateCore(predicate, kind, state));
                },
                LazyThreadSafetyMode.ExecutionAndPublication);
            var result = _results.GetOrAdd(key, candidate);
            if (!ReferenceEquals(result, candidate))
            {
                Interlocked.Increment(ref _cacheHitCount);
            }

            return result.Value;
        }

        private static string PredicateStateSignature(string predicate, string kind, IntentTrackState state)
        {
            return kind switch
            {
                BuildTagCountKind => TryParseCountIdentity(predicate, out var countTag, out _)
                    ? TagCount(state, countTag).ToString(CultureInfo.InvariantCulture)
                    : throw new InvalidOperationException($"Unreachable count predicate: {predicate}"),
                BuildTagPresenceKind => TagCount(state, predicate.Substring(ContainsTagPrefix.Length))
                    .ToString(CultureInfo.InvariantCulture),
                OwnedComponentKind => OwnedComponents(state)
                    .Contains(predicate.Substring(OwnedPrefix.Length)) ? "1" : "0",
                EffectReadyKind => state.ActiveEffectIds.Contains(
                    predicate.Substring(EffectReadyPrefix.Length), StringComparer.Ordinal) ? "1" : "0",
                TeamRuleKind => state.ActiveTeamRuleIds.Contains(
                    predicate.Substring(TeamRulePrefix.Length), StringComparer.Ordinal) ? "1" : "0",
                FormationKind => FormationSignature(state.Formation),
                _ => throw new InvalidOperationException($"Unreachable identity predicate kind: {kind}"),
            };
        }

        private static string FormationSignature(FormationFeatures? features)
            => features == null
                ? "-"
                : string.Join(",",
                    features.FrontlineCount.ToString(CultureInfo.InvariantCulture),
                    features.ProtectedSlotCount.ToString(CultureInfo.InvariantCulture),
                    features.FlankRearExposureScore.ToString("R", CultureInfo.InvariantCulture),
                    features.BacklineAccessibility.ToString("R", CultureInfo.InvariantCulture));
    }
}
