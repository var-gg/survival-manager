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
        => Evaluate(input, new IntentTrackPredicateEvaluator.IntentTrackPredicateEvaluationCache());

    internal static IntentTrackSearchResult Evaluate(
        IntentTrackSearchInput input,
        IntentTrackPredicateEvaluator.IntentTrackPredicateEvaluationCache predicateCache)
    {
        Validate(input);
        if (predicateCache == null) throw new ArgumentNullException(nameof(predicateCache));
        var enabled = input.EnabledLeverIds.ToHashSet(StringComparer.Ordinal);
        var windows = input.Windows
            .Where(window => window.WindowIndex >= input.CommitWindowIndex && enabled.Contains(window.LeverId))
            .OrderBy(window => window.WindowIndex)
            .ThenBy(window => window.LeverId, StringComparer.Ordinal)
            .ThenBy(window => window.SourceId, StringComparer.Ordinal)
            .Take(input.HorizonWindowCount)
            .ToArray();
        var relevance = Relevance.Create(input.Contract, input.InitialState, windows);
        windows = windows.Select(window => window with
            {
                Choices = CompactChoices(window.Choices, input.Contract, relevance),
            })
            .ToArray();
        var initialState = NormalizeState(input.InitialState, relevance);
        var initialAssessment = Assess(input.Contract, initialState, predicateCache);
        initialState = initialState with { CompletedMilestones = initialAssessment.CompletedMilestones };
        var initialNode = new SearchNode(
            initialState,
            initialAssessment,
            initialAssessment.CompletedMilestones.Count > 0 ? 0 : -1,
            initialAssessment.IdentityRealized ? 0 : -1,
            initialAssessment.IdentityRealized ? input.CommitWindowIndex - 1 : -1,
            0,
            0,
            SearchPath.Root,
            0L);
        if (initialAssessment.IdentityRealized)
        {
            return ToResult(initialNode, agencyWindowCount: 0, trackAvailable: true);
        }

        var nodes = new[] { initialNode };
        SearchNode? bestRealized = null;
        var processed = 0;
        var nextStableOrder = 1L;
        foreach (var window in windows)
        {
            processed++;
            if (string.Equals(window.LeverId, IntentTrackLeverId.Deployment, StringComparison.Ordinal))
            {
                nodes = PruneBeforeDeployment(nodes, relevance);
            }

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

                var substitutionOffered = legal.Any(choice => OffersEffectiveSubstitution(input.Contract, choice));
                var projections = legal
                    .Select(choice => new
                    {
                        Choice = choice,
                        State = Apply(node.State, choice, relevance),
                    })
                    .GroupBy(value => StateSignature(value.State, relevance), StringComparer.Ordinal)
                    .OrderBy(group => group.Key, StringComparer.Ordinal)
                    .Select(group => group.OrderBy(value => value.Choice.ChoiceId, StringComparer.Ordinal).First())
                    .Select(value =>
                    {
                        var assessment = Assess(input.Contract, value.State, predicateCache);
                        var nextState = value.State with { CompletedMilestones = assessment.CompletedMilestones };
                        return new ChoiceProjection(value.Choice, nextState, assessment);
                    })
                    .ToArray();
                var progressOffered = substitutionOffered || projections.Any(projection =>
                    projection.Assessment.IdentityScore > node.Assessment.IdentityScore
                    || projection.Assessment.CompletedMilestones.Count > node.Assessment.CompletedMilestones.Count);
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

                    var next = new SearchNode(
                        projection.State,
                        projection.Assessment,
                        firstProgress,
                        realizationTime,
                        realizationWindowIndex,
                        drought,
                        maxDrought,
                        node.Path.Append(projection.Choice.ChoiceId),
                        nextStableOrder++);
                    expanded.Add(next);
                    if (projection.Assessment.IdentityRealized
                        && (bestRealized == null || CompareRealized(next, bestRealized) < 0))
                    {
                        bestRealized = next;
                    }
                }
            }

            nodes = Prune(expanded, relevance);
            if (bestRealized != null)
            {
                return ToResult(bestRealized, processed, trackAvailable: true);
            }
        }

        var bestNearMiss = nodes
            .OrderByDescending(node => node.Assessment.IdentityScore)
            .ThenByDescending(node => node.Assessment.CompletedMilestones.Count)
            .ThenBy(node => node.MaxDrought)
            .ThenBy(node => node.StableOrder)
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
            node.Path.ToArray(),
            node.Assessment.IdentityPredicateResults);

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

        return byState.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => pair.Value)
            .ToArray();
    }

    private static SearchNode[] PruneBeforeDeployment(
        IEnumerable<SearchNode> candidates,
        Relevance relevance)
    {
        var byPersistentState = new Dictionary<string, SearchNode>(StringComparer.Ordinal);
        foreach (var candidate in candidates)
        {
            var state = candidate.State;
            var signature = string.Join("|",
                string.Join(",", state.Roster.Select(value => value.MemberId)),
                string.Join(",", state.InventoryComponentIds),
                string.Join(",", state.SkillIds),
                string.Join(",", state.PassiveIds),
                string.Join(",", state.OwnedComponentIds),
                relevance.RecruitResourceCap > 0 ? state.RecruitResource.ToString(CultureInfo.InvariantCulture) : "-",
                relevance.PassiveBudgetCap > 0 ? state.PassiveBudget.ToString(CultureInfo.InvariantCulture) : "-",
                relevance.RefitResourceCap > 0 ? state.RefitResource.ToString(CultureInfo.InvariantCulture) : "-",
                candidate.Assessment.IdentityScore.ToString(CultureInfo.InvariantCulture),
                string.Join(",", candidate.Assessment.CompletedMilestones),
                candidate.FirstProgressTime.ToString(CultureInfo.InvariantCulture),
                candidate.CurrentDrought.ToString(CultureInfo.InvariantCulture),
                candidate.MaxDrought.ToString(CultureInfo.InvariantCulture));
            if (!byPersistentState.TryGetValue(signature, out var existing)
                || candidate.StableOrder < existing.StableOrder)
            {
                byPersistentState[signature] = candidate;
            }
        }

        return byPersistentState.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => pair.Value)
            .ToArray();
    }

    private static IReadOnlyList<IntentTrackChoice> CompactChoices(
        IReadOnlyList<IntentTrackChoice> choices,
        ConceptContract contract,
        Relevance relevance)
    {
        var projectionBase = new IntentTrackState(
            Array.Empty<IntentTrackRosterMember>(),
            int.MaxValue,
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            relevance.RecruitResourceCap,
            relevance.PassiveBudgetCap,
            relevance.RefitResourceCap,
            Array.Empty<string>(),
            Array.Empty<IntentTrackTagCount>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            null,
            Array.Empty<string>());
        return (choices ?? Array.Empty<IntentTrackChoice>())
            .Where(choice => relevance.ShouldRetain(choice, contract))
            .GroupBy(choice => string.Join("|",
                    StateSignature(Apply(projectionBase, choice, relevance), relevance),
                    string.Join(",", (choice.RequiredRosterMemberIds ?? Array.Empty<string>()).OrderBy(value => value, StringComparer.Ordinal)),
                    string.Join(",", (choice.RequiredOwnedComponentIds ?? Array.Empty<string>()).OrderBy(value => value, StringComparer.Ordinal)),
                    choice.RecruitResourceDelta.ToString(CultureInfo.InvariantCulture),
                    choice.RecruitResourceCost.ToString(CultureInfo.InvariantCulture),
                    choice.PassiveBudgetDelta.ToString(CultureInfo.InvariantCulture),
                    choice.PassiveBudgetCost.ToString(CultureInfo.InvariantCulture),
                    choice.RefitResourceDelta.ToString(CultureInfo.InvariantCulture),
                    choice.RefitResourceCost.ToString(CultureInfo.InvariantCulture),
                    OffersEffectiveSubstitution(contract, choice) ? "substitution" : "direct",
                    choice.Irreversible ? "irreversible" : "reversible"),
                StringComparer.Ordinal)
            .Select(group => group.OrderBy(value => value.ChoiceId, StringComparer.Ordinal).First())
            .OrderBy(value => value.ChoiceId, StringComparer.Ordinal)
            .ToArray();
    }

    private static int ComparePathQuality(SearchNode left, SearchNode right)
    {
        var first = NormalizeTime(left.FirstProgressTime).CompareTo(NormalizeTime(right.FirstProgressTime));
        if (first != 0) return first;
        var drought = left.MaxDrought.CompareTo(right.MaxDrought);
        if (drought != 0) return drought;
        return left.StableOrder.CompareTo(right.StableOrder);
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
            state.RosterCapacity,
            state.InventoryComponentIds.Concat(choice.AddedInventoryComponentIds ?? Array.Empty<string>()).ToArray(),
            state.SkillIds.Concat(choice.AddedSkillIds ?? Array.Empty<string>()).ToArray(),
            state.PassiveIds.Concat(choice.AddedPassiveIds ?? Array.Empty<string>()).ToArray(),
            state.OwnedComponentIds.Concat(choice.AddedOwnedComponentIds ?? Array.Empty<string>()).ToArray(),
            state.RecruitResource + choice.RecruitResourceDelta - choice.RecruitResourceCost,
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
        if (state.RecruitResource < choice.RecruitResourceCost
            || state.PassiveBudget < choice.PassiveBudgetCost
            || state.RefitResource < choice.RefitResourceCost)
        {
            return false;
        }

        var rosterIds = state.Roster.Select(member => member.MemberId).ToHashSet(StringComparer.Ordinal);
        if (state.Roster.Count + (choice.AddedRosterMembers?.Count ?? 0) > state.RosterCapacity)
        {
            return false;
        }

        if (choice.RecruitResourceCost > 0
            && (choice.AddedRosterMembers?.Count ?? 0) > 0
            && choice.AddedRosterMembers.All(member => rosterIds.Contains(member.MemberId)))
        {
            return false;
        }

        if ((choice.RequiredRosterMemberIds ?? Array.Empty<string>()).Any(required => !rosterIds.Contains(required)))
        {
            return false;
        }

        var owned = IntentTrackPredicateEvaluator.OwnedComponents(state);
        if (choice.PassiveBudgetCost > 0
            && (choice.AddedPassiveIds?.Count ?? 0) > 0
            && choice.AddedPassiveIds.All(owned.Contains))
        {
            return false;
        }

        if (choice.RefitResourceCost > 0
            && (choice.AddedOwnedComponentIds?.Count ?? 0) > 0
            && choice.AddedOwnedComponentIds.All(owned.Contains))
        {
            return false;
        }

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

    private static Assessment Assess(
        ConceptContract contract,
        IntentTrackState state,
        IntentTrackPredicateEvaluator.IntentTrackPredicateEvaluationCache predicateCache)
    {
        var identityPredicates = (contract.IdentityPredicates ?? Array.Empty<string>())
            .Select(predicate => predicateCache.Evaluate(predicate, state))
            .ToArray();
        var identity = identityPredicates.Count(result => result.Satisfied);
        var milestones = (contract.ProgressMilestones ?? Array.Empty<string>())
            .Where(milestone => IntentTrackPredicateEvaluator.MilestoneSatisfied(milestone, state))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var target = contract.IdentityPredicates?.Count ?? 0;
        return new Assessment(
            identity,
            target,
            identity == target && target > 0,
            milestones,
            identityPredicates);
    }

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
            Math.Max(0, state.RosterCapacity),
            Relevant(state.InventoryComponentIds),
            Relevant(state.SkillIds),
            Relevant(state.PassiveIds),
            Relevant(state.OwnedComponentIds),
            Math.Min(relevance.RecruitResourceCap, Math.Max(0, state.RecruitResource)),
            Math.Min(relevance.PassiveBudgetCap, Math.Max(0, state.PassiveBudget)),
            Math.Min(relevance.RefitResourceCap, Math.Max(0, state.RefitResource)),
            Stable(state.DeployedMemberIds),
            (state.DeployedTagCounts ?? Array.Empty<IntentTrackTagCount>())
                .Where(value => value != null
                                && !string.IsNullOrWhiteSpace(value.TagId)
                                && value.Count > 0
                                && relevance.BuildTags.Contains(value.TagId))
                .GroupBy(value => value.TagId, StringComparer.Ordinal)
                .Select(group => new IntentTrackTagCount(group.Key, group.Max(value => value.Count)))
                .OrderBy(value => value.TagId, StringComparer.Ordinal)
                .ToArray(),
            Relevant(state.ActiveComponentIds),
            Relevant(state.ActiveEffectIds),
            Stable(state.ActiveTeamRuleIds).Where(relevance.TeamRuleIds.Contains).ToArray(),
            state.Formation,
            Stable(state.CompletedMilestones));
    }

    private static string StateSignature(IntentTrackState state, Relevance relevance)
    {
        var roster = string.Join(",", state.Roster.Select(member => member.MemberId));
        var tags = string.Join(",", state.DeployedTagCounts.Select(value => $"{value.TagId}:{value.Count.ToString(CultureInfo.InvariantCulture)}"));
        var formation = string.Join(",", relevance.FormationPredicates.Select(predicate =>
            IntentTrackPredicateEvaluator.SatisfiesFormationPredicate(predicate, state.Formation) ? "1" : "0"));
        return string.Join("|",
            roster,
            string.Join(",", state.InventoryComponentIds),
            string.Join(",", state.SkillIds),
            string.Join(",", state.PassiveIds),
            string.Join(",", state.OwnedComponentIds),
            relevance.RecruitResourceCap > 0 ? state.RecruitResource.ToString(CultureInfo.InvariantCulture) : "-",
            relevance.PassiveBudgetCap > 0 ? state.PassiveBudget.ToString(CultureInfo.InvariantCulture) : "-",
            relevance.RefitResourceCap > 0 ? state.RefitResource.ToString(CultureInfo.InvariantCulture) : "-",
            tags,
            string.Join(",", state.ActiveComponentIds),
            string.Join(",", state.ActiveEffectIds),
            string.Join(",", state.ActiveTeamRuleIds),
            formation,
            string.Join(",", state.CompletedMilestones));
    }

    private static void Validate(IntentTrackSearchInput input)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        if (input.Contract == null || input.InitialState == null) throw new ArgumentException("Track input contract/state is required.", nameof(input));
        if (input.Contract.IdentityPredicates == null || input.Contract.IdentityPredicates.Count == 0)
        {
            throw new ArgumentException("Track identity predicates are required.", nameof(input));
        }

        IntentTrackPredicateEvaluator.RequireSupportedIdentityPredicates(input.Contract.IdentityPredicates);

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
        IReadOnlyList<string> CompletedMilestones,
        IReadOnlyList<IntentTrackIdentityPredicateResult> IdentityPredicateResults);

    private sealed record SearchNode(
        IntentTrackState State,
        Assessment Assessment,
        int FirstProgressTime,
        int RealizationTime,
        int RealizationWindowIndex,
        int CurrentDrought,
        int MaxDrought,
        SearchPath Path,
        long StableOrder);

    private sealed class SearchPath
    {
        public static SearchPath Root { get; } = new(null, string.Empty, 0);

        private SearchPath(SearchPath? parent, string choiceId, int depth)
        {
            Parent = parent;
            ChoiceId = choiceId;
            Depth = depth;
        }

        private SearchPath? Parent { get; }

        private string ChoiceId { get; }

        private int Depth { get; }

        public SearchPath Append(string choiceId) => new(this, choiceId, Depth + 1);

        public IReadOnlyList<string> ToArray()
        {
            if (Depth == 0)
            {
                return Array.Empty<string>();
            }

            var values = new string[Depth];
            var cursor = this;
            for (var index = Depth - 1; index >= 0; index--)
            {
                values[index] = cursor.ChoiceId;
                cursor = cursor.Parent!;
            }

            return values;
        }
    }

    private sealed record ChoiceProjection(
        IntentTrackChoice Choice,
        IntentTrackState State,
        Assessment Assessment);

    private sealed record Relevance(
        HashSet<string> SemanticIds,
        HashSet<string> BuildTags,
        HashSet<string> TeamRuleIds,
        HashSet<string> RelevantRosterMemberIds,
        IReadOnlyList<string> FormationPredicates,
        int RecruitResourceCap,
        int PassiveBudgetCap,
        int RefitResourceCap)
    {
        public static Relevance Create(
            ConceptContract contract,
            IntentTrackState initialState,
            IReadOnlyList<IntentTrackAgencyWindow> windows)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var buildTags = new HashSet<string>(StringComparer.Ordinal);
            var teamRuleIds = new HashSet<string>(StringComparer.Ordinal);
            var formationPredicates = new HashSet<string>(StringComparer.Ordinal);
            var allChoices = windows.SelectMany(value => value.Choices ?? Array.Empty<IntentTrackChoice>())
                .ToArray();
            foreach (var value in contract.IdentityPredicates
                         .Concat(contract.ProgressMilestones))
            {
                AddSemanticTail(ids, value, "owned:");
                AddSemanticTail(ids, value, "acquire:");
                AddSemanticTail(ids, value, "effect.ready:");
                AddSemanticTail(ids, value, "activate:");
                AddBuildTag(buildTags, value);
                AddSemanticTail(teamRuleIds, value, "build.team_rule=");
                if (value.StartsWith("formation.", StringComparison.Ordinal))
                {
                    formationPredicates.Add(value);
                }

                if (value.IndexOf(':') >= 0
                    && !value.StartsWith("build.", StringComparison.Ordinal)
                    && !value.StartsWith("formation.", StringComparison.Ordinal))
                {
                    ids.Add(value);
                }
            }

            var addedPrerequisite = true;
            while (addedPrerequisite)
            {
                addedPrerequisite = false;
                foreach (var choice in allChoices.Where(value => ProvidesSemantic(value, ids)))
                {
                    foreach (var required in choice.RequiredOwnedComponentIds ?? Array.Empty<string>())
                    {
                        if (!string.IsNullOrWhiteSpace(required) && ids.Add(required))
                        {
                            addedPrerequisite = true;
                        }
                    }
                }
            }

            var formation = formationPredicates.OrderBy(value => value, StringComparer.Ordinal).ToArray();
            var relevantRosterMemberIds = allChoices
                .Where(choice => TouchesTargetState(choice, ids, buildTags, teamRuleIds, formation))
                .SelectMany(choice => choice.RequiredRosterMemberIds ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToHashSet(StringComparer.Ordinal);
            return new Relevance(
                ids,
                buildTags,
                teamRuleIds,
                relevantRosterMemberIds,
                formation,
                CalculateRecruitResourceCap(initialState, windows),
                DistinctAcquisitionResourceCap(
                    windows,
                    value => value.AddedPassiveIds,
                    value => value.PassiveBudgetCost),
                DistinctAcquisitionResourceCap(
                    windows,
                    value => value.AddedOwnedComponentIds,
                    value => value.RefitResourceCost));
        }

        public bool ShouldRetain(IntentTrackChoice choice, ConceptContract contract)
        {
            if (!choice.Irreversible
                || choice.RecruitResourceDelta > 0
                || choice.PassiveBudgetDelta > 0
                || choice.RefitResourceDelta > 0
                || OffersEffectiveSubstitution(contract, choice))
            {
                return true;
            }

            if (TouchesTargetState(choice, SemanticIds, BuildTags, TeamRuleIds, FormationPredicates))
            {
                return true;
            }

            return (choice.AddedRosterMembers ?? Array.Empty<IntentTrackRosterMember>())
                .Any(member => RelevantRosterMemberIds.Contains(member.MemberId));
        }

        private static bool ProvidesSemantic(IntentTrackChoice choice, ISet<string> semanticIds)
            => (choice.AddedInventoryComponentIds ?? Array.Empty<string>())
                   .Concat(choice.AddedSkillIds ?? Array.Empty<string>())
                   .Concat(choice.AddedPassiveIds ?? Array.Empty<string>())
                   .Concat(choice.AddedOwnedComponentIds ?? Array.Empty<string>())
                   .Concat(choice.ActiveComponentIds ?? Array.Empty<string>())
                   .Concat(choice.ActiveEffectIds ?? Array.Empty<string>())
                   .Concat((choice.AddedRosterMembers ?? Array.Empty<IntentTrackRosterMember>())
                       .SelectMany(member => (member.ComponentIds ?? Array.Empty<string>())
                           .Concat(member.EffectIds ?? Array.Empty<string>())))
                   .Any(semanticIds.Contains);

        private static bool TouchesTargetState(
            IntentTrackChoice choice,
            ISet<string> semanticIds,
            ISet<string> buildTags,
            ISet<string> teamRuleIds,
            IReadOnlyList<string> formationPredicates)
            => ProvidesSemantic(choice, semanticIds)
               || (choice.AddedRosterMembers ?? Array.Empty<IntentTrackRosterMember>())
                   .SelectMany(member => member.Tags ?? Array.Empty<string>())
                   .Any(buildTags.Contains)
               || (choice.DeployedTagCounts ?? Array.Empty<IntentTrackTagCount>())
                   .Any(value => buildTags.Contains(value.TagId))
               || (choice.ActiveTeamRuleIds ?? Array.Empty<string>()).Any(teamRuleIds.Contains)
               || (choice.Formation != null && formationPredicates.Any(predicate =>
                   IntentTrackPredicateEvaluator.SatisfiesFormationPredicate(predicate, choice.Formation)));

        private static int CalculateRecruitResourceCap(
            IntentTrackState initialState,
            IReadOnlyList<IntentTrackAgencyWindow> windows)
        {
            var availableSlots = Math.Max(0, initialState.RosterCapacity - initialState.Roster.Count);
            if (availableSlots == 0)
            {
                return 0;
            }

            return windows.SelectMany(value => value.Choices ?? Array.Empty<IntentTrackChoice>())
                .Where(value => value.RecruitResourceCost > 0
                                && (value.AddedRosterMembers?.Count ?? 0) > 0)
                .GroupBy(value => string.Join(",", value.AddedRosterMembers
                        .Select(member => member.MemberId)
                        .OrderBy(memberId => memberId, StringComparer.Ordinal)),
                    StringComparer.Ordinal)
                .Select(group => group.Max(value => value.RecruitResourceCost))
                .OrderByDescending(value => value)
                .Take(availableSlots)
                .Sum();
        }

        private static int DistinctAcquisitionResourceCap(
            IReadOnlyList<IntentTrackAgencyWindow> windows,
            Func<IntentTrackChoice, IReadOnlyList<string>> acquiredIds,
            Func<IntentTrackChoice, int> costSelector)
            => windows.SelectMany(value => value.Choices ?? Array.Empty<IntentTrackChoice>())
                .Where(value => costSelector(value) > 0 && (acquiredIds(value)?.Count ?? 0) > 0)
                .GroupBy(value => string.Join(",", acquiredIds(value)
                        .OrderBy(id => id, StringComparer.Ordinal)),
                    StringComparer.Ordinal)
                .Sum(group => group.Max(costSelector));

        private static void AddSemanticTail(ISet<string> ids, string value, string marker)
        {
            var index = value.IndexOf(marker, StringComparison.Ordinal);
            if (index >= 0)
            {
                ids.Add(value.Substring(index + marker.Length));
            }
        }

        private static void AddBuildTag(ISet<string> tags, string value)
        {
            const string presencePrefix = "build.contains_tag:";
            if (value.StartsWith(presencePrefix, StringComparison.Ordinal))
            {
                tags.Add(value.Substring(presencePrefix.Length));
                return;
            }

            const string countPrefix = "build.count_tag(";
            if (!value.StartsWith(countPrefix, StringComparison.Ordinal))
            {
                return;
            }

            var close = value.IndexOf(')', countPrefix.Length);
            if (close > countPrefix.Length)
            {
                tags.Add(value.Substring(countPrefix.Length, close - countPrefix.Length));
            }
        }
    }
}
