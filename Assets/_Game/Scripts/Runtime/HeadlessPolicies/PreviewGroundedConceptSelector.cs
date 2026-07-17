using System;
using System.Collections.Generic;
using System.Linq;

namespace SM.HeadlessPolicies;

public sealed class PreviewGroundedDecisionTrace
{
    public PreviewGroundedDecisionTrace(
        IReadOnlyList<string> threatTags,
        IReadOnlyList<PreviewCounterConnection> counterConnections,
        bool identityPreservingCandidateAvailable,
        bool coreIdentityPreserved,
        bool roleViable,
        string formationRule,
        int replacementCount,
        int previousDeploymentCount,
        IReadOnlyList<string> selectedHeroIds,
        string reason)
    {
        ThreatTags = threatTags ?? Array.Empty<string>();
        CounterConnections = counterConnections ?? Array.Empty<PreviewCounterConnection>();
        IdentityPreservingCandidateAvailable = identityPreservingCandidateAvailable;
        CoreIdentityPreserved = coreIdentityPreserved;
        RoleViable = roleViable;
        FormationRule = formationRule;
        ReplacementCount = replacementCount;
        PreviousDeploymentCount = previousDeploymentCount;
        SelectedHeroIds = selectedHeroIds ?? Array.Empty<string>();
        Reason = reason;
    }

    public IReadOnlyList<string> ThreatTags { get; }
    public IReadOnlyList<PreviewCounterConnection> CounterConnections { get; }
    public bool IdentityPreservingCandidateAvailable { get; }
    public bool CoreIdentityPreserved { get; }
    public bool RoleViable { get; }
    public string FormationRule { get; }
    public int ReplacementCount { get; }
    public int PreviousDeploymentCount { get; }
    public IReadOnlyList<string> SelectedHeroIds { get; }
    public string Reason { get; }
    public bool IsFullReset => PreviousDeploymentCount > 0 && ReplacementCount == PreviousDeploymentCount;
}

internal sealed class PreviewGroundedSelection
{
    public PreviewGroundedSelection(
        ConceptDeploymentSelection deployment,
        PreviewGroundedDecisionTrace trace,
        IReadOnlyList<string> evidenceSignalKeys)
    {
        Deployment = deployment;
        Trace = trace;
        EvidenceSignalKeys = evidenceSignalKeys;
    }

    public ConceptDeploymentSelection Deployment { get; }
    public PreviewGroundedDecisionTrace Trace { get; }
    public IReadOnlyList<string> EvidenceSignalKeys { get; }
}

/// <summary>
/// 가중합 없이 identity, counter, role viability, milestone, switching cost, stable ID 순으로 선택한다.
/// </summary>
internal static class PreviewGroundedConceptSelector
{
    public static PreviewGroundedSelection Select(
        HeadlessConceptIntent intent,
        IntentState state,
        HeadlessPolicyObservation observation)
    {
        var threatProfile = EnemyThreatProfileParser.Parse(
            EnemyThreatObservation.FromVisiblePreview(observation.EnemyPreview));
        var previousHeroes = observation.Roster.Where(value => value.IsDeployed).ToArray();
        var previousProgress = ConceptIntentPredicateMatcher.IdentityProgress(
            intent,
            previousHeroes,
            observation,
            Array.Empty<HeadlessPlacement>());
        var candidates = HeadlessPolicyScoring.EnumerateCombinations(observation.Roster, observation.DeployCapacity)
            .Select(heroes => BuildCandidate(intent, state, observation, threatProfile, previousHeroes, previousProgress, heroes))
            .ToArray();
        if (candidates.Length == 0)
        {
            throw new InvalidOperationException("Preview-grounded policy found no legal deployment combination.");
        }

        var identityPreservingAvailable = candidates.Any(value => value.CoreIdentityPreserved);
        var identityScope = identityPreservingAvailable
            ? candidates.Where(value => value.CoreIdentityPreserved).ToArray()
            : candidates;
        var viableScope = identityScope.Any(value => value.RoleViable)
            ? identityScope.Where(value => value.RoleViable).ToArray()
            : identityScope;
        var selected = viableScope
            .OrderByDescending(value => value.SupportedThreatCount)
            .ThenByDescending(value => value.CounterRelationStrength)
            .ThenByDescending(value => value.PreservedMilestoneCount)
            .ThenBy(value => value.ReplacementCount)
            .ThenBy(value => value.AbandonedInvestment)
            .ThenBy(value => value.HeroSignature, StringComparer.Ordinal)
            .ThenBy(value => value.PlacementSignature, StringComparer.Ordinal)
            .First();

        var newlyCompleted = selected.CompletedMilestones
            .Except(state.CompletedMilestones, StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var milestoneAdvanced = newlyCompleted.Length > 0;
        var identityAdvanced = selected.ProgressScore > state.ProgressScore;
        var reason = !identityPreservingAvailable
            ? IntentDecisionReason.Pivot
            : selected.CounterConnections.Count > 0
                ? IntentDecisionReason.CounterAdapt
                : selected.SubstitutionMatches > 0 && selected.ProgressScore == 0
                    ? IntentDecisionReason.Substitute
                    : milestoneAdvanced || identityAdvanced
                        ? IntentDecisionReason.Advance
                        : IntentDecisionReason.Keep;
        PreviewGroundedEvidenceGuard.RequireSupported(reason, selected.CounterConnections);

        var meaningfulProgress = milestoneAdvanced
                                 || identityAdvanced
                                 || string.Equals(reason, IntentDecisionReason.Substitute, StringComparison.Ordinal)
                                 || string.Equals(reason, IntentDecisionReason.CounterAdapt, StringComparison.Ordinal);
        var deployment = new ConceptDeploymentSelection(
            selected.Heroes,
            selected.Placements,
            reason,
            selected.ProgressScore,
            selected.CompletedMilestones,
            milestoneAdvanced,
            meaningfulProgress);
        var trace = new PreviewGroundedDecisionTrace(
            threatProfile.Tags,
            selected.CounterConnections,
            identityPreservingAvailable,
            selected.CoreIdentityPreserved,
            selected.RoleViable,
            selected.FormationRule,
            selected.ReplacementCount,
            previousHeroes.Length,
            selected.Heroes.Select(value => value.HeroId).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            reason);
        var evidenceSignals = selected.CounterConnections
            .SelectMany(value => new[] { value.ThreatEvidenceSignalKey, value.HeroEvidenceSignalKey })
            .Append(HeadlessPolicyEvidence.DeploymentSurfaceSignal)
            .Append(HeadlessPolicyEvidence.RosterSurfaceSignal)
            .Append(HeadlessPolicyEvidence.EnemyPreviewSignal)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        return new PreviewGroundedSelection(deployment, trace, evidenceSignals);
    }

    private static Candidate BuildCandidate(
        HeadlessConceptIntent intent,
        IntentState state,
        HeadlessPolicyObservation observation,
        EnemyThreatProfile profile,
        IReadOnlyList<HeadlessHeroObservation> previousHeroes,
        int previousProgress,
        IReadOnlyList<HeadlessHeroObservation> heroes)
    {
        var selectedIds = new HashSet<string>(heroes.Select(value => value.HeroId), StringComparer.Ordinal);
        var removed = previousHeroes.Where(value => !selectedIds.Contains(value.HeroId)).ToArray();
        var connections = PreviewGroundedCounterRules.Connect(profile, heroes);
        var formation = PreviewGroundedFormationSelector.Select(
            profile,
            heroes,
            observation.Anchors,
            connections);
        var placements = formation.Placements;
        var progress = ConceptIntentPredicateMatcher.IdentityProgress(intent, heroes, observation, placements);
        var completedMilestones = ConceptIntentPredicateMatcher.CompletedMilestones(
            intent,
            heroes,
            observation,
            placements);
        return new Candidate(
            heroes,
            placements,
            progress,
            completedMilestones,
            completedMilestones.Intersect(state.CompletedMilestones, StringComparer.Ordinal).Count(),
            ConceptIntentPredicateMatcher.SubstitutionMatches(intent, heroes),
            progress >= previousProgress,
            IsRoleViable(heroes),
            connections.Select(value => value.ThreatTag).Distinct(StringComparer.Ordinal).Count(),
            CounterRelationStrength(profile, connections),
            connections,
            removed.Length,
            removed.Sum(value => Math.Max(0, value.Level) + Math.Max(0, value.EquippedItemCount)),
            HeadlessPolicyScoring.HeroSignature(heroes),
            HeadlessPolicyScoring.PlacementSignature(placements),
            formation.RuleId);
    }

    private static int CounterRelationStrength(
        EnemyThreatProfile profile,
        IReadOnlyList<PreviewCounterConnection> connections)
    {
        var hasWall = profile.Tags.Contains(EnemyThreatTag.FrontlineWall, StringComparer.Ordinal);
        return profile.Findings.Sum(finding =>
        {
            var connectedHeroCount = connections
                .Where(value => value.ThreatTag == finding.Tag)
                .Select(value => value.HeroId)
                .Distinct(StringComparer.Ordinal)
                .Count();
            var usefulLimit = finding.Tag == EnemyThreatTag.BacklineFirepower
                ? Math.Max(1, finding.EvidenceSignalKeys.Count + (hasWall ? 1 : 0))
                : 1;
            return Math.Min(usefulLimit, connectedHeroCount);
        });
    }

    private static bool IsRoleViable(IReadOnlyList<HeadlessHeroObservation> heroes)
    {
        var hasProtection = heroes.Any(hero => HeadlessPolicyScoring.PrefersFront(hero)
                                               || hero.SkillCards.Any(skill =>
                                                   skill.Kind is SM.Combat.Model.SkillKind.Heal or SM.Combat.Model.SkillKind.Shield
                                                   || skill.AppliedStatuses.Any(status => ContainsAny(
                                                       status.StatusId,
                                                       "guarded",
                                                       "barrier",
                                                       "shield"))));
        var hasDamage = heroes.Any(hero => hero.SkillCards.Any(skill =>
            skill.Kind == SM.Combat.Model.SkillKind.Strike
            && skill.DamageType != SM.Combat.Model.DamageType.Healing));
        return hasProtection && hasDamage;
    }

    private static bool ContainsAny(string value, params string[] tokens)
        => !string.IsNullOrWhiteSpace(value)
           && tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));

    private sealed class Candidate
    {
        public Candidate(
            IReadOnlyList<HeadlessHeroObservation> heroes,
            IReadOnlyList<HeadlessPlacement> placements,
            int progressScore,
            IReadOnlyList<string> completedMilestones,
            int preservedMilestoneCount,
            int substitutionMatches,
            bool coreIdentityPreserved,
            bool roleViable,
            int supportedThreatCount,
            int counterRelationStrength,
            IReadOnlyList<PreviewCounterConnection> counterConnections,
            int replacementCount,
            int abandonedInvestment,
            string heroSignature,
            string placementSignature,
            string formationRule)
        {
            Heroes = heroes;
            Placements = placements;
            ProgressScore = progressScore;
            CompletedMilestones = completedMilestones;
            PreservedMilestoneCount = preservedMilestoneCount;
            SubstitutionMatches = substitutionMatches;
            CoreIdentityPreserved = coreIdentityPreserved;
            RoleViable = roleViable;
            SupportedThreatCount = supportedThreatCount;
            CounterRelationStrength = counterRelationStrength;
            CounterConnections = counterConnections;
            ReplacementCount = replacementCount;
            AbandonedInvestment = abandonedInvestment;
            HeroSignature = heroSignature;
            PlacementSignature = placementSignature;
            FormationRule = formationRule;
        }

        public IReadOnlyList<HeadlessHeroObservation> Heroes { get; }
        public IReadOnlyList<HeadlessPlacement> Placements { get; }
        public int ProgressScore { get; }
        public IReadOnlyList<string> CompletedMilestones { get; }
        public int PreservedMilestoneCount { get; }
        public int SubstitutionMatches { get; }
        public bool CoreIdentityPreserved { get; }
        public bool RoleViable { get; }
        public int SupportedThreatCount { get; }
        public int CounterRelationStrength { get; }
        public IReadOnlyList<PreviewCounterConnection> CounterConnections { get; }
        public int ReplacementCount { get; }
        public int AbandonedInvestment { get; }
        public string HeroSignature { get; }
        public string PlacementSignature { get; }
        public string FormationRule { get; }
    }
}
