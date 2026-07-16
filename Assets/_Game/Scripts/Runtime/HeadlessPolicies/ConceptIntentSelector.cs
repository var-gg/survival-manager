using System;
using System.Collections.Generic;
using System.Linq;

namespace SM.HeadlessPolicies;

internal sealed class ConceptDeploymentSelection
{
    public ConceptDeploymentSelection(
        IReadOnlyList<HeadlessHeroObservation> heroes,
        IReadOnlyList<HeadlessPlacement> placements,
        string reason,
        int progressScore,
        IReadOnlyList<string> completedMilestones,
        bool milestoneAdvanced,
        bool meaningfulProgress)
    {
        Heroes = heroes;
        Placements = placements;
        Reason = reason;
        ProgressScore = progressScore;
        CompletedMilestones = completedMilestones;
        MilestoneAdvanced = milestoneAdvanced;
        MeaningfulProgress = meaningfulProgress;
    }

    public IReadOnlyList<HeadlessHeroObservation> Heroes { get; }
    public IReadOnlyList<HeadlessPlacement> Placements { get; }
    public string Reason { get; }
    public int ProgressScore { get; }
    public IReadOnlyList<string> CompletedMilestones { get; }
    public bool MilestoneAdvanced { get; }
    public bool MeaningfulProgress { get; }
}

internal sealed class ConceptRewardSelection
{
    public ConceptRewardSelection(
        HeadlessRewardOption option,
        string reason,
        IReadOnlyList<string> completedMilestones,
        bool milestoneAdvanced,
        bool scarceResourceInvested,
        bool meaningfulProgress)
    {
        Option = option;
        Reason = reason;
        CompletedMilestones = completedMilestones;
        MilestoneAdvanced = milestoneAdvanced;
        ScarceResourceInvested = scarceResourceInvested;
        MeaningfulProgress = meaningfulProgress;
    }

    public HeadlessRewardOption Option { get; }
    public string Reason { get; }
    public IReadOnlyList<string> CompletedMilestones { get; }
    public bool MilestoneAdvanced { get; }
    public bool ScarceResourceInvested { get; }
    public bool MeaningfulProgress { get; }
}

/// <summary>가중합 없이 명시적 우선순위와 stable ID tie-break로 배치·보상을 선택한다.</summary>
internal static class ConceptIntentSelector
{
    public static ConceptDeploymentSelection SelectDeployment(
        HeadlessConceptIntent intent,
        IntentState state,
        HeadlessPolicyObservation observation)
    {
        var annihilationRisk = ConceptIntentPredicateMatcher.IsAnnihilationRisk(observation);
        var candidates = HeadlessPolicyScoring.EnumerateCombinations(observation.Roster, observation.DeployCapacity)
            .Select(heroes =>
            {
                var placements = HeadlessPolicyScoring.PlaceFormation(heroes, observation.Anchors);
                return new DeploymentCandidate(
                    heroes,
                    placements,
                    ConceptIntentPredicateMatcher.IdentityProgress(intent, heroes, observation, placements),
                    ConceptIntentPredicateMatcher.CompletedMilestones(intent, heroes, observation, placements),
                    ConceptIntentPredicateMatcher.SubstitutionMatches(intent, heroes),
                    ConceptIntentPredicateMatcher.CounterSafety(heroes, observation.EnemyPreview),
                    heroes.Count(hero => hero.IsDeployed),
                    HeadlessPolicyScoring.HeroSignature(heroes),
                    HeadlessPolicyScoring.PlacementSignature(placements));
            })
            .ToArray();

        IOrderedEnumerable<DeploymentCandidate> ordered;
        if (annihilationRisk)
        {
            ordered = candidates
                .OrderByDescending(value => value.CounterSafety)
                .ThenByDescending(value => value.ProgressScore)
                .ThenByDescending(value => value.CompletedMilestones.Count)
                .ThenByDescending(value => value.SubstitutionMatches);
        }
        else
        {
            ordered = candidates
                .OrderByDescending(value => value.ProgressScore)
                .ThenByDescending(value => value.CompletedMilestones.Count)
                .ThenByDescending(value => value.SubstitutionMatches)
                .ThenByDescending(value => value.CurrentDeploymentRetention);
        }

        var selected = ordered
            .ThenBy(value => value.HeroSignature, StringComparer.Ordinal)
            .ThenBy(value => value.PlacementSignature, StringComparer.Ordinal)
            .First();
        var newlyCompleted = selected.CompletedMilestones
            .Except(state.CompletedMilestones, StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var milestoneAdvanced = newlyCompleted.Length > 0;
        var identityAdvanced = selected.ProgressScore > state.ProgressScore;
        var maximumPrimary = candidates.Max(value => value.ProgressScore);
        var reason = annihilationRisk
            ? IntentDecisionReason.CounterAdapt
            : maximumPrimary == 0 && selected.SubstitutionMatches > 0
                ? IntentDecisionReason.Substitute
                : milestoneAdvanced || identityAdvanced
                    ? IntentDecisionReason.Advance
                    : NoProgressReason(state);
        var meaningfulProgress = milestoneAdvanced
                                 || identityAdvanced
                                 || string.Equals(reason, IntentDecisionReason.Substitute, StringComparison.Ordinal)
                                 || string.Equals(reason, IntentDecisionReason.CounterAdapt, StringComparison.Ordinal);
        return new ConceptDeploymentSelection(
            selected.Heroes,
            selected.Placements,
            reason,
            selected.ProgressScore,
            selected.CompletedMilestones,
            milestoneAdvanced,
            meaningfulProgress);
    }

    public static ConceptRewardSelection SelectReward(
        HeadlessConceptIntent intent,
        IntentState state,
        HeadlessPolicyObservation observation)
    {
        var annihilationRisk = ConceptIntentPredicateMatcher.IsAnnihilationRisk(observation);
        var candidates = observation.RewardOptions
            .Select(option => new RewardCandidate(
                option,
                ConceptIntentPredicateMatcher.RewardPrimaryMatches(intent, option),
                ConceptIntentPredicateMatcher.RewardSubstitutionMatches(intent, option),
                ConceptIntentPredicateMatcher.RewardCounterMatches(intent, option)))
            .OrderByDescending(value => annihilationRisk ? value.CounterMatches : 0)
            .ThenByDescending(value => value.PrimaryMatches)
            .ThenByDescending(value => value.SubstitutionMatches)
            .ThenBy(value => value.Option.Index)
            .ToArray();
        var selected = candidates[0];
        var rewardMilestones = ConceptIntentPredicateMatcher.RewardCompletedMilestones(intent, selected.Option);
        var completed = state.CompletedMilestones
            .Concat(rewardMilestones)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var milestoneAdvanced = completed.Length > state.CompletedMilestones.Count;
        var counterAdapted = annihilationRisk && selected.CounterMatches > 0;
        var reason = counterAdapted
            ? IntentDecisionReason.CounterAdapt
            : selected.PrimaryMatches > 0
                ? IntentDecisionReason.Advance
                : selected.SubstitutionMatches > 0
                    ? IntentDecisionReason.Substitute
                    : NoProgressReason(state);
        var scarceResourceInvested = milestoneAdvanced
                                     && selected.Option.Kind is HeadlessRewardKind.Item
                                         or HeadlessRewardKind.TemporaryAugment
                                         or HeadlessRewardKind.PermanentAugmentSlot;
        return new ConceptRewardSelection(
            selected.Option,
            reason,
            completed,
            milestoneAdvanced,
            scarceResourceInvested,
            milestoneAdvanced
            || string.Equals(reason, IntentDecisionReason.Substitute, StringComparison.Ordinal)
            || string.Equals(reason, IntentDecisionReason.CounterAdapt, StringComparison.Ordinal));
    }

    public static string NoProgressReason(IntentState state)
    {
        if (string.Equals(state.Status, IntentStatus.Abandoned, StringComparison.Ordinal)
            || string.Equals(state.Status, IntentStatus.Pivoted, StringComparison.Ordinal)
            && state.ConsecutiveNoProgressDecisions >= 1)
        {
            return IntentDecisionReason.Abandon;
        }

        return state.ConsecutiveNoProgressDecisions >= 1
            ? IntentDecisionReason.Pivot
            : IntentDecisionReason.Keep;
    }

    private sealed class DeploymentCandidate
    {
        public DeploymentCandidate(
            IReadOnlyList<HeadlessHeroObservation> heroes,
            IReadOnlyList<HeadlessPlacement> placements,
            int progressScore,
            IReadOnlyList<string> completedMilestones,
            int substitutionMatches,
            int counterSafety,
            int currentDeploymentRetention,
            string heroSignature,
            string placementSignature)
        {
            Heroes = heroes;
            Placements = placements;
            ProgressScore = progressScore;
            CompletedMilestones = completedMilestones;
            SubstitutionMatches = substitutionMatches;
            CounterSafety = counterSafety;
            CurrentDeploymentRetention = currentDeploymentRetention;
            HeroSignature = heroSignature;
            PlacementSignature = placementSignature;
        }

        public IReadOnlyList<HeadlessHeroObservation> Heroes { get; }
        public IReadOnlyList<HeadlessPlacement> Placements { get; }
        public int ProgressScore { get; }
        public IReadOnlyList<string> CompletedMilestones { get; }
        public int SubstitutionMatches { get; }
        public int CounterSafety { get; }
        public int CurrentDeploymentRetention { get; }
        public string HeroSignature { get; }
        public string PlacementSignature { get; }
    }

    private sealed class RewardCandidate
    {
        public RewardCandidate(
            HeadlessRewardOption option,
            int primaryMatches,
            int substitutionMatches,
            int counterMatches)
        {
            Option = option;
            PrimaryMatches = primaryMatches;
            SubstitutionMatches = substitutionMatches;
            CounterMatches = counterMatches;
        }

        public HeadlessRewardOption Option { get; }
        public int PrimaryMatches { get; }
        public int SubstitutionMatches { get; }
        public int CounterMatches { get; }
    }
}
