using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SM.HeadlessCensus;

/// <summary>
/// 정책에 비노출인 실제 offer sequence를 identity predicate 도달 목적으로만 탐색한다.
/// 관련 component만 상태 signature에 남기고 milestone-oriented memoization으로 유한 분기를 줄인다.
/// </summary>
public static class IntentTrackEvaluator
{
    public const int StarvationDroughtThreshold = 4;

    public static IntentTrackSearchResult Evaluate(IntentTrackSearchInput input)
    {
        Validate(input);
        var relevance = Relevance.Create(input.Contract);
        var initialState = NormalizeState(input.InitialState, relevance);
        var initialAssessment = Assess(input.Contract, initialState);
        initialState = initialState with { CompletedMilestones = initialAssessment.CompletedMilestones };
        var initialNode = new SearchNode(
            initialState,
            initialAssessment,
            initialAssessment.CompletedMilestones.Count > 0 ? 0 : -1,
            initialAssessment.IdentityRealized ? 0 : -1,
            initialAssessment.IdentityRealized ? input.CommitWindowIndex - 1 : -1,
            0,
            0,
            Array.Empty<string>());
        if (initialAssessment.IdentityRealized)
        {
            return ToResult(initialNode, agencyWindowCount: 0, trackAvailable: true);
        }

        var enabled = input.EnabledLeverIds.ToHashSet(StringComparer.Ordinal);
        var windows = input.Windows
            .Where(window => window.WindowIndex >= input.CommitWindowIndex && enabled.Contains(window.LeverId))
            .OrderBy(window => window.WindowIndex)
            .ThenBy(window => window.LeverId, StringComparer.Ordinal)
            .ThenBy(window => window.SourceId, StringComparer.Ordinal)
            .Take(input.HorizonWindowCount)
            .ToArray();
        var nodes = new[] { initialNode };
        SearchNode? bestRealized = null;
        var processed = 0;
        foreach (var window in windows)
        {
            processed++;
            var expanded = new List<SearchNode>();
            foreach (var node in nodes)
            {
                var legal = (window.Choices ?? Array.Empty<IntentTrackChoice>())
                    .Where(choice => IsLegal(node.State, choice))
                    .OrderBy(choice => choice.ChoiceId, StringComparer.Ordinal)
                    .ToArray();
                if (legal.Length == 0)
                {
                    legal = new[] { IntentTrackChoice.NoOp($"unavailable:{window.WindowIndex.ToString(CultureInfo.InvariantCulture)}") };
                }

                var projections = legal.Select(choice =>
                {
                    var nextState = Apply(node.State, choice, relevance);
                    var assessment = Assess(input.Contract, nextState);
                    nextState = nextState with { CompletedMilestones = assessment.CompletedMilestones };
                    return new ChoiceProjection(choice, nextState, assessment);
                }).ToArray();
                var progressOffered = projections.Any(projection =>
                    projection.Assessment.IdentityScore > node.Assessment.IdentityScore
                    || projection.Assessment.CompletedMilestones.Count > node.Assessment.CompletedMilestones.Count
                    || OffersEffectiveSubstitution(input.Contract, projection.Choice));
                var drought = progressOffered ? 0 : node.CurrentDrought + 1;
                var maxDrought = Math.Max(node.MaxDrought, drought);
                foreach (var projection in projections)
                {
                    var time = Math.Max(0, window.WindowIndex - input.CommitWindowIndex);
                    var firstProgress = node.FirstProgressTime;
                    if (firstProgress < 0
                        && projection.Assessment.CompletedMilestones.Count > initialAssessment.CompletedMilestones.Count)
                    {
                        firstProgress = time;
                    }

                    var realizationTime = node.RealizationTime;
                    var realizationWindowIndex = node.RealizationWindowIndex;
                    if (realizationTime < 0 && projection.Assessment.IdentityRealized)
                    {
                        realizationTime = time;
                        realizationWindowIndex = window.WindowIndex;
                    }

                    var path = node.ChoicePath.Append(projection.Choice.ChoiceId).ToArray();
                    var next = new SearchNode(
                        projection.State,
                        projection.Assessment,
                        firstProgress,
                        realizationTime,
                        realizationWindowIndex,
                        drought,
                        maxDrought,
                        path);
                    expanded.Add(next);
                    if (projection.Assessment.IdentityRealized
                        && (bestRealized == null || CompareRealized(next, bestRealized) < 0))
                    {
                        bestRealized = next;
                    }
                }
            }

            nodes = Prune(expanded, relevance);
        }

        if (bestRealized != null)
        {
            return ToResult(bestRealized, processed, trackAvailable: true);
        }

        var bestNearMiss = nodes
            .OrderByDescending(node => node.Assessment.IdentityScore)
            .ThenByDescending(node => node.Assessment.CompletedMilestones.Count)
            .ThenBy(node => node.MaxDrought)
            .ThenBy(node => string.Join("|", node.ChoicePath), StringComparer.Ordinal)
            .FirstOrDefault() ?? initialNode;
        return ToResult(bestNearMiss, processed, trackAvailable: false);
    }

    private static IntentTrackSearchResult ToResult(SearchNode node, int agencyWindowCount, bool trackAvailable)
        => new(
            IntentTrackSearchResult.CurrentEvaluatorVersion,
            trackAvailable,
            node.FirstProgressTime,
            node.RealizationTime,
            node.RealizationWindowIndex,
            agencyWindowCount,
            node.MaxDrought,
            node.MaxDrought >= StarvationDroughtThreshold || !trackAvailable,
            node.Assessment.TargetIdentityScore,
            node.Assessment.IdentityScore,
            node.ChoicePath);

    private static SearchNode[] Prune(IEnumerable<SearchNode> candidates, Relevance relevance)
    {
        var byState = new Dictionary<string, SearchNode>(StringComparer.Ordinal);
        foreach (var candidate in candidates)
        {
            var signature = StateSignature(candidate.State, relevance);
            if (!byState.TryGetValue(signature, out var existing) || ComparePathQuality(candidate, existing) < 0)
            {
                byState[signature] = candidate;
            }
        }

        return byState.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Value).ToArray();
    }

    private static int ComparePathQuality(SearchNode left, SearchNode right)
    {
        var first = NormalizeTime(left.FirstProgressTime).CompareTo(NormalizeTime(right.FirstProgressTime));
        if (first != 0) return first;
        var drought = left.MaxDrought.CompareTo(right.MaxDrought);
        if (drought != 0) return drought;
        return string.CompareOrdinal(string.Join("|", left.ChoicePath), string.Join("|", right.ChoicePath));
    }

    private static int CompareRealized(SearchNode left, SearchNode right)
    {
        var realization = NormalizeTime(left.RealizationTime).CompareTo(NormalizeTime(right.RealizationTime));
        if (realization != 0) return realization;
        return ComparePathQuality(left, right);
    }

    private static int NormalizeTime(int value) => value < 0 ? int.MaxValue : value;

    private static IntentTrackState Apply(IntentTrackState state, IntentTrackChoice choice, Relevance relevance)
    {
        var roster = state.Roster.Concat(choice.AddedRosterMembers ?? Array.Empty<IntentTrackRosterMember>())
            .Where(member => member != null && !string.IsNullOrWhiteSpace(member.MemberId))
            .GroupBy(member => member.MemberId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(member => member.MemberId, StringComparer.Ordinal)
            .ToArray();
        var hasDeployment = (choice.DeployedMemberIds?.Count ?? 0) > 0;
        return NormalizeState(new IntentTrackState(
            roster,
            state.InventoryComponentIds.Concat(choice.AddedInventoryComponentIds ?? Array.Empty<string>()).ToArray(),
            state.SkillIds.Concat(choice.AddedSkillIds ?? Array.Empty<string>()).ToArray(),
            state.PassiveIds.Concat(choice.AddedPassiveIds ?? Array.Empty<string>()).ToArray(),
            state.OwnedComponentIds.Concat(choice.AddedOwnedComponentIds ?? Array.Empty<string>()).ToArray(),
            state.PassiveBudget + choice.PassiveBudgetDelta - choice.PassiveBudgetCost,
            state.RefitResource + choice.RefitResourceDelta - choice.RefitResourceCost,
            hasDeployment ? choice.DeployedMemberIds : state.DeployedMemberIds,
            hasDeployment ? choice.DeployedTagCounts : state.DeployedTagCounts,
            hasDeployment ? choice.ActiveComponentIds : state.ActiveComponentIds,
            hasDeployment
                ? choice.ActiveEffectIds
                : state.ActiveEffectIds.Concat(choice.ActiveEffectIds ?? Array.Empty<string>()).ToArray(),
            hasDeployment ? choice.ActiveTeamRuleIds : state.ActiveTeamRuleIds,
            hasDeployment ? choice.Formation : state.Formation,
            state.CompletedMilestones), relevance);
    }

    private static bool IsLegal(IntentTrackState state, IntentTrackChoice choice)
    {
        if (state.PassiveBudget < choice.PassiveBudgetCost || state.RefitResource < choice.RefitResourceCost)
        {
            return false;
        }

        var rosterIds = state.Roster.Select(member => member.MemberId).ToHashSet(StringComparer.Ordinal);
        if ((choice.RequiredRosterMemberIds ?? Array.Empty<string>()).Any(required => !rosterIds.Contains(required)))
        {
            return false;
        }

        var owned = OwnedComponents(state);
        return (choice.RequiredOwnedComponentIds ?? Array.Empty<string>()).All(owned.Contains);
    }

    private static bool OffersEffectiveSubstitution(ConceptContract contract, IntentTrackChoice choice)
    {
        var offered = (choice.OfferedSemanticIds ?? Array.Empty<string>())
            .Concat(choice.ActiveComponentIds ?? Array.Empty<string>())
            .Concat(choice.AddedOwnedComponentIds ?? Array.Empty<string>())
            .ToHashSet(StringComparer.Ordinal);
        return (contract.AllowedSubstitutions ?? Array.Empty<string>()).Any(offered.Contains);
    }

    private static Assessment Assess(ConceptContract contract, IntentTrackState state)
    {
        var identity = (contract.IdentityPredicates ?? Array.Empty<string>())
            .Count(predicate => PredicateSatisfied(predicate, state));
        var milestones = (contract.ProgressMilestones ?? Array.Empty<string>())
            .Where(milestone => MilestoneSatisfied(milestone, state))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var target = contract.IdentityPredicates?.Count ?? 0;
        return new Assessment(identity, target, identity == target && target > 0, milestones);
    }

    private static bool PredicateSatisfied(string predicate, IntentTrackState state)
    {
        if (TryParseCountIdentity(predicate, out var tag, out var threshold))
        {
            return TagCount(state, tag) >= threshold;
        }

        const string containsTag = "build.contains_tag:";
        if (predicate.StartsWith(containsTag, StringComparison.Ordinal))
        {
            return TagCount(state, predicate.Substring(containsTag.Length)) > 0;
        }

        const string owned = "owned:";
        if (predicate.StartsWith(owned, StringComparison.Ordinal))
        {
            return OwnedComponents(state).Contains(predicate.Substring(owned.Length));
        }

        const string effectReady = "effect.ready:";
        if (predicate.StartsWith(effectReady, StringComparison.Ordinal))
        {
            return state.ActiveEffectIds.Contains(predicate.Substring(effectReady.Length), StringComparer.Ordinal);
        }

        const string teamRule = "build.team_rule=";
        if (predicate.StartsWith(teamRule, StringComparison.Ordinal))
        {
            return state.ActiveTeamRuleIds.Contains(predicate.Substring(teamRule.Length), StringComparer.Ordinal);
        }

        return predicate.StartsWith("formation.", StringComparison.Ordinal)
               && SatisfiesFormationPredicate(predicate, state.Formation);
    }

    private static bool MilestoneSatisfied(string milestone, IntentTrackState state)
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

        if (milestone.StartsWith("build.team_rule=", StringComparison.Ordinal))
        {
            return state.ActiveTeamRuleIds.Contains(milestone.Substring("build.team_rule=".Length), StringComparer.Ordinal);
        }

        return milestone.StartsWith("formation.", StringComparison.Ordinal)
               && SatisfiesFormationPredicate(milestone, state.Formation);
    }

    public static bool SatisfiesFormationPredicate(string predicate, FormationFeatures? features)
    {
        if (features == null)
        {
            return false;
        }

        foreach (var clause in predicate.Split(new[] { " and " }, StringSplitOptions.RemoveEmptyEntries))
        {
            var value = clause.Trim();
            if (TryCompare(value, "formation.frontline_count", features.FrontlineCount)
                || TryCompare(value, "formation.protected_slot_count", features.ProtectedSlotCount)
                || TryCompare(value, "formation.flank_rear_exposure_score", features.FlankRearExposureScore)
                || TryCompare(value, "formation.backline_accessibility", features.BacklineAccessibility))
            {
                continue;
            }

            const string profile = "formation.profile=";
            if (value.StartsWith(profile, StringComparison.Ordinal)
                && string.Equals(
                    ConceptFormationProfile.Classify(features),
                    value.Substring(profile.Length),
                    StringComparison.Ordinal))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private static bool TryCompare(string expression, string key, double actual)
    {
        if (!expression.StartsWith(key, StringComparison.Ordinal))
        {
            return false;
        }

        var suffix = expression.Substring(key.Length);
        var operators = new[] { ">=", "<=", ">", "<", "=" };
        var operation = operators.FirstOrDefault(value => suffix.StartsWith(value, StringComparison.Ordinal));
        if (operation == null
            || !double.TryParse(suffix.Substring(operation.Length), NumberStyles.Float, CultureInfo.InvariantCulture, out var expected))
        {
            return false;
        }

        return operation switch
        {
            ">=" => actual >= expected,
            "<=" => actual <= expected,
            ">" => actual > expected,
            "<" => actual < expected,
            "=" => Math.Abs(actual - expected) <= 1e-9d,
            _ => false,
        };
    }

    private static int TagCount(IntentTrackState state, string tag)
        => state.DeployedTagCounts.FirstOrDefault(value => string.Equals(value.TagId, tag, StringComparison.Ordinal))?.Count ?? 0;

    private static HashSet<string> OwnedComponents(IntentTrackState state)
        => state.OwnedComponentIds
            .Concat(state.InventoryComponentIds)
            .Concat(state.SkillIds)
            .Concat(state.PassiveIds)
            .Concat(state.ActiveComponentIds)
            .ToHashSet(StringComparer.Ordinal);

    private static IntentTrackState NormalizeState(IntentTrackState state, Relevance relevance)
    {
        static string[] Stable(IEnumerable<string>? values)
            => (values ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

        string[] Relevant(IEnumerable<string>? values)
            => Stable(values).Where(relevance.SemanticIds.Contains).ToArray();

        return new IntentTrackState(
            (state.Roster ?? Array.Empty<IntentTrackRosterMember>())
                .Where(value => value != null && !string.IsNullOrWhiteSpace(value.MemberId))
                .GroupBy(value => value.MemberId, StringComparer.Ordinal)
                .Select(group => group.First() with
                {
                    Tags = Stable(group.First().Tags),
                    ComponentIds = Relevant(group.First().ComponentIds),
                    EffectIds = Relevant(group.First().EffectIds),
                })
                .OrderBy(value => value.MemberId, StringComparer.Ordinal)
                .ToArray(),
            Relevant(state.InventoryComponentIds),
            Relevant(state.SkillIds),
            Relevant(state.PassiveIds),
            Relevant(state.OwnedComponentIds),
            Math.Max(0, state.PassiveBudget),
            Math.Max(0, state.RefitResource),
            Stable(state.DeployedMemberIds),
            (state.DeployedTagCounts ?? Array.Empty<IntentTrackTagCount>())
                .Where(value => value != null && !string.IsNullOrWhiteSpace(value.TagId) && value.Count > 0)
                .GroupBy(value => value.TagId, StringComparer.Ordinal)
                .Select(group => new IntentTrackTagCount(group.Key, group.Max(value => value.Count)))
                .OrderBy(value => value.TagId, StringComparer.Ordinal)
                .ToArray(),
            Relevant(state.ActiveComponentIds),
            Relevant(state.ActiveEffectIds),
            Stable(state.ActiveTeamRuleIds),
            state.Formation,
            Stable(state.CompletedMilestones));
    }

    private static string StateSignature(IntentTrackState state, Relevance relevance)
    {
        var roster = string.Join(",", state.Roster.Select(member => member.MemberId));
        var tags = string.Join(",", state.DeployedTagCounts.Select(value => $"{value.TagId}:{value.Count.ToString(CultureInfo.InvariantCulture)}"));
        var formation = state.Formation == null
            ? "-"
            : string.Join(",",
                state.Formation.FrontlineCount.ToString(CultureInfo.InvariantCulture),
                state.Formation.ProtectedSlotCount.ToString(CultureInfo.InvariantCulture),
                state.Formation.FlankRearExposureScore.ToString("R", CultureInfo.InvariantCulture),
                state.Formation.BacklineAccessibility.ToString("R", CultureInfo.InvariantCulture));
        return string.Join("|",
            roster,
            string.Join(",", state.InventoryComponentIds),
            string.Join(",", state.SkillIds),
            string.Join(",", state.PassiveIds),
            string.Join(",", state.OwnedComponentIds),
            state.PassiveBudget.ToString(CultureInfo.InvariantCulture),
            state.RefitResource.ToString(CultureInfo.InvariantCulture),
            string.Join(",", state.DeployedMemberIds),
            tags,
            string.Join(",", state.ActiveComponentIds),
            string.Join(",", state.ActiveEffectIds),
            string.Join(",", state.ActiveTeamRuleIds),
            formation,
            string.Join(",", state.CompletedMilestones));
    }

    private static bool TryParseCountIdentity(string value, out string tag, out int threshold)
    {
        tag = string.Empty;
        threshold = 0;
        const string prefix = "build.count_tag(";
        if (!value.StartsWith(prefix, StringComparison.Ordinal)) return false;
        var close = value.IndexOf(')', prefix.Length);
        if (close < 0 || !value.Substring(close + 1).StartsWith(">=", StringComparison.Ordinal)) return false;
        tag = value.Substring(prefix.Length, close - prefix.Length);
        return int.TryParse(value.Substring(close + 3), NumberStyles.Integer, CultureInfo.InvariantCulture, out threshold);
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

    private static void Validate(IntentTrackSearchInput input)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        if (input.Contract == null || input.InitialState == null) throw new ArgumentException("Track input contract/state is required.", nameof(input));
        if (input.Contract.IdentityPredicates == null || input.Contract.IdentityPredicates.Count == 0)
        {
            throw new ArgumentException("Track identity predicates are required.", nameof(input));
        }

        if (input.CommitWindowIndex < 0 || input.HorizonWindowCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(input), "Commit index and horizon must be non-negative.");
        }

        var duplicate = (input.Windows ?? Array.Empty<IntentTrackAgencyWindow>())
            .GroupBy(value => value.WindowIndex)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate != null)
        {
            throw new ArgumentException($"Duplicate agency window index: {duplicate.Key}", nameof(input));
        }
    }

    private sealed record Assessment(
        int IdentityScore,
        int TargetIdentityScore,
        bool IdentityRealized,
        IReadOnlyList<string> CompletedMilestones);

    private sealed record SearchNode(
        IntentTrackState State,
        Assessment Assessment,
        int FirstProgressTime,
        int RealizationTime,
        int RealizationWindowIndex,
        int CurrentDrought,
        int MaxDrought,
        IReadOnlyList<string> ChoicePath);

    private sealed record ChoiceProjection(
        IntentTrackChoice Choice,
        IntentTrackState State,
        Assessment Assessment);

    private sealed record Relevance(HashSet<string> SemanticIds)
    {
        public static Relevance Create(ConceptContract contract)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in contract.IdentityPredicates
                         .Concat(contract.ProgressMilestones)
                         .Concat(contract.AllowedSubstitutions)
                         .Concat(contract.CounterAffordances))
            {
                AddSemanticTail(ids, value, "owned:");
                AddSemanticTail(ids, value, "acquire:");
                AddSemanticTail(ids, value, "effect.ready:");
                AddSemanticTail(ids, value, "activate:");
                if (value.IndexOf(':') >= 0
                    && !value.StartsWith("build.", StringComparison.Ordinal)
                    && !value.StartsWith("formation.", StringComparison.Ordinal))
                {
                    ids.Add(value);
                }
            }

            return new Relevance(ids);
        }

        private static void AddSemanticTail(ISet<string> ids, string value, string marker)
        {
            var index = value.IndexOf(marker, StringComparison.Ordinal);
            if (index >= 0)
            {
                ids.Add(value.Substring(index + marker.Length));
            }
        }
    }
}
