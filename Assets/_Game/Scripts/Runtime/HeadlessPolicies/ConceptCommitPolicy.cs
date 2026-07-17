using System;
using System.Collections.Generic;
using System.Linq;

namespace SM.HeadlessPolicies;

/// <summary>
/// 순간 점수보다 선언된 build intent의 정체성·milestone·pivot 조건을 우선하는 결정적 정책.
/// coverage intent는 순수 DTO constructor injection을 허용하고, null이면 가시 fact만으로 discovery intent를 만든다.
/// </summary>
public sealed class ConceptCommitPolicy : IHeadlessPolicy, IHeadlessRosterPolicy
{
    public const string PolicyId = "concept-commit-v1";
    public const string PreviewGroundedPolicyId = "concept-preview-grounded-v1";

    private readonly HeadlessConceptIntent _injectedIntent;
    private readonly string _policyId;
    private readonly bool _usesPreviewGroundedSelection;
    private readonly List<HeadlessIntentDecision> _decisionTrace = new();
    private HeadlessConceptIntent _intent;
    private IntentState _state;

    public ConceptCommitPolicy(HeadlessConceptIntent coverageIntent = null)
        : this(coverageIntent, PolicyId, usesPreviewGroundedSelection: false)
    {
    }

    private ConceptCommitPolicy(
        HeadlessConceptIntent coverageIntent,
        string policyId,
        bool usesPreviewGroundedSelection)
    {
        _injectedIntent = coverageIntent;
        _policyId = policyId;
        _usesPreviewGroundedSelection = usesPreviewGroundedSelection;
    }

    public static ConceptCommitPolicy CreatePreviewGrounded(HeadlessConceptIntent coverageIntent = null)
        => new(coverageIntent, PreviewGroundedPolicyId, usesPreviewGroundedSelection: true);

    public string Id => _policyId;
    public HeadlessConceptIntent CurrentIntent => _intent;
    public IntentState CurrentState => _state;
    public IReadOnlyList<HeadlessIntentDecision> DecisionTrace => _decisionTrace;
    public PreviewGroundedDecisionTrace LastPreviewDecision { get; private set; }

    public HeadlessIntentDecision LastIntentDecision
        => _decisionTrace.Count == 0
            ? throw new InvalidOperationException("Concept policy has not made a decision yet.")
            : _decisionTrace[_decisionTrace.Count - 1];

    public HeadlessDeploymentDecision DecideDeployment(HeadlessPolicyObservation observation)
    {
        HeadlessPolicyGuard.ValidateObservation(observation);
        EnsureIntentState(observation);
        var evidence = HeadlessPolicyEvidence.ForDeployment(
            observation,
            usesDecisionSeed: false,
            usesCampaignContext: false);

        // 반드시 action 선택 전에 hypothesis를 선언한다. payoff 관측 index는 이 정책 표면에 없으므로 -1이다.
        var hypothesis = DeclareHypothesis(evidence);
        var previewSelection = _usesPreviewGroundedSelection
            ? PreviewGroundedConceptSelector.Select(_intent, _state, observation)
            : null;
        var selection = previewSelection?.Deployment
                        ?? ConceptIntentSelector.SelectDeployment(_intent, _state, observation);
        if (previewSelection != null)
        {
            LastPreviewDecision = previewSelection.Trace;
            evidence = HeadlessPolicyEvidence.ForSignals(observation, previewSelection.EvidenceSignalKeys);
        }

        var action = HeadlessPolicyScoring.PlacementSignature(selection.Placements);
        var detail = previewSelection == null
            ? $"progress={selection.ProgressScore};milestones={selection.CompletedMilestones.Count}"
            : PreviewRationale(selection, previewSelection.Trace);
        var rationale = Rationale(selection.Reason, detail);
        RecordDecision(
            "deployment",
            action,
            selection.Reason,
            selection.MilestoneAdvanced,
            scarceResourceInvested: false,
            selection.MeaningfulProgress,
            selection.ProgressScore,
            selection.CompletedMilestones,
            hypothesis);

        return new HeadlessDeploymentDecision(
            selection.Placements,
            rationale,
            HeadlessPolicyScoring.EvaluateDeployment(observation, selection.Heroes, selection.Placements),
            evidence);
    }

    public HeadlessRewardDecision DecideReward(HeadlessPolicyObservation observation)
    {
        HeadlessPolicyGuard.ValidateObservation(observation);
        EnsureIntentState(observation);
        var evidence = HeadlessPolicyEvidence.ForReward(
            observation,
            usesDecisionSeed: false,
            usesCampaignContext: false,
            usesRoster: true);
        var hypothesis = DeclareHypothesis(evidence);
        if (observation.RewardOptions.Count == 0)
        {
            var reason = ConceptIntentSelector.NoProgressReason(_state);
            RecordDecision(
                "reward",
                "option:-1",
                reason,
                milestoneAdvanced: false,
                scarceResourceInvested: false,
                meaningfulProgress: false,
                _state.ProgressScore,
                _state.CompletedMilestones,
                hypothesis);
            return new HeadlessRewardDecision(
                -1,
                Rationale(reason, "reward_options=0"),
                0d,
                evidence);
        }

        var selection = ConceptIntentSelector.SelectReward(_intent, _state, observation);
        var action = $"option:{selection.Option.Index}";
        var rationale = Rationale(
            selection.Reason,
            $"payload={selection.Option.PayloadId};milestones={selection.CompletedMilestones.Count}");
        RecordDecision(
            "reward",
            action,
            selection.Reason,
            selection.MilestoneAdvanced,
            selection.ScarceResourceInvested,
            selection.MeaningfulProgress,
            _state.ProgressScore,
            selection.CompletedMilestones,
            hypothesis);
        return new HeadlessRewardDecision(
            selection.Option.Index,
            rationale,
            ConceptIntentPredicateMatcher.RewardPrimaryMatches(_intent, selection.Option),
            evidence);
    }

    public HeadlessRecruitDecision DecideRecruit(HeadlessRosterPolicyObservation observation)
    {
        HeadlessRosterPolicyGuard.ValidateObservation(observation);
        RequireIntentState();
        var selection = ConceptRosterDecisionSelector.SelectRecruit(
            _intent,
            _state,
            observation,
            detail => detail);
        HeadlessRosterPolicyGuard.ValidateRecruitDecision(observation, selection.Decision);
        var reason = selection.Decision.IsNoOp
            ? ConceptIntentSelector.NoProgressReason(_state)
            : IntentDecisionReason.Advance;
        var decision = selection.Decision.WithRationale(Rationale(reason, selection.Decision.Rationale));
        RecordDecision(
            "recruit",
            $"offer:{decision.OfferIndex}",
            reason,
            selection.MilestoneAdvanced,
            scarceResourceInvested: !decision.IsNoOp,
            meaningfulProgress: !decision.IsNoOp,
            selection.ProgressScore,
            selection.CompletedMilestones,
            DeclareHypothesis(decision.EvidenceFactIds));
        return decision;
    }

    public HeadlessPassiveDecision DecidePassiveAllocation(HeadlessRosterPolicyObservation observation)
    {
        HeadlessRosterPolicyGuard.ValidateObservation(observation);
        RequireIntentState();
        var selection = ConceptRosterDecisionSelector.SelectPassive(
            _intent,
            _state,
            observation,
            detail => detail);
        HeadlessRosterPolicyGuard.ValidatePassiveDecision(observation, selection.Decision);
        var reason = selection.Decision.IsNoOp
            ? ConceptIntentSelector.NoProgressReason(_state)
            : IntentDecisionReason.Advance;
        var decision = selection.Decision.WithOutcome(
            Rationale(reason, selection.Decision.Rationale),
            selection.Decision.EstimatedValue,
            selection.Decision.EvidenceFactIds);
        RecordDecision(
            "level_node",
            decision.IsNoOp ? "node:none" : $"node:{decision.HeroId}:{decision.NodeId}",
            reason,
            selection.MilestoneAdvanced,
            scarceResourceInvested: !decision.IsNoOp,
            meaningfulProgress: !decision.IsNoOp,
            selection.ProgressScore,
            selection.CompletedMilestones,
            DeclareHypothesis(decision.EvidenceFactIds));
        return decision;
    }

    public HeadlessRefitDecision DecideRefit(HeadlessRosterPolicyObservation observation)
    {
        HeadlessRosterPolicyGuard.ValidateObservation(observation);
        RequireIntentState();
        var selection = ConceptRosterDecisionSelector.SelectRefit(
            _intent,
            observation,
            detail => detail);
        HeadlessRosterPolicyGuard.ValidateRefitDecision(observation, selection.Decision);
        var reason = selection.Decision.IsNoOp
            ? ConceptIntentSelector.NoProgressReason(_state)
            : IntentDecisionReason.Advance;
        var decision = selection.Decision.WithRationale(Rationale(reason, selection.Decision.Rationale));
        RecordDecision(
            "refit",
            decision.IsNoOp ? "refit:none" : $"refit:{decision.ItemInstanceId}:{decision.AffixSlotIndex}",
            reason,
            milestoneAdvanced: false,
            scarceResourceInvested: !decision.IsNoOp,
            meaningfulProgress: !decision.IsNoOp,
            _state.ProgressScore,
            _state.CompletedMilestones,
            DeclareHypothesis(decision.EvidenceFactIds));
        return decision;
    }

    private void EnsureIntentState(HeadlessPolicyObservation observation)
    {
        if (_state != null)
        {
            return;
        }

        _intent = _injectedIntent ?? ConceptIntentDiscovery.Form(observation);
        ValidateIntent(_intent);
        var deployed = observation.Roster.Where(hero => hero.IsDeployed).ToArray();
        var baselinePlacements = Array.Empty<HeadlessPlacement>();
        var completed = ConceptIntentPredicateMatcher.CompletedMilestones(
            _intent,
            deployed,
            observation,
            baselinePlacements);
        var progress = ConceptIntentPredicateMatcher.IdentityProgress(
            _intent,
            deployed,
            observation,
            baselinePlacements);
        _state = new IntentState(
            _intent.IntentId,
            _intent.SourceLane,
            nextDecisionIndex: 0,
            declaredAtDecisionIndex: 0,
            commitDecisionIndex: -1,
            status: IntentStatus.Active,
            consecutiveNoProgressDecisions: 0,
            progressScore: progress,
            completedMilestones: completed,
            hypothesis: EmptyHypothesis());
    }

    private void RequireIntentState()
    {
        if (_state == null || _intent == null)
        {
            throw new InvalidOperationException(
                "Concept roster decisions require the campaign deployment observation to declare intent first.");
        }
    }

    private BuildHypothesis DeclareHypothesis(IReadOnlyList<string> evidence)
    {
        var nextPlan = _intent.ProgressMilestones
            .Except(_state.CompletedMilestones, StringComparer.Ordinal)
            .Take(3)
            .ToArray();
        if (nextPlan.Length == 0)
        {
            nextPlan = new[] { $"preserve:{_intent.IdentityPredicates[0]}" };
        }

        var confidence = Math.Min(0.9d, 0.6d + (_state.CompletedMilestones.Count * 0.05d));
        return new BuildHypothesis(
            claimedEdge: $"{_intent.IdentityPredicates[0]}->{_intent.PayoffWitnessId}",
            evidenceRefs: evidence.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            confidence: confidence,
            openQuestion: $"verify:{nextPlan[0]}",
            expectedPayoff: _intent.PayoffWitnessId,
            nextAcquisitionPlan: nextPlan,
            falsificationSignal: _intent.PivotConditions[0],
            declaredAtDecisionIndex: _state.NextDecisionIndex,
            payoffObservedAtDecisionIndex: -1);
    }

    private void RecordDecision(
        string decisionKind,
        string action,
        string reason,
        bool milestoneAdvanced,
        bool scarceResourceInvested,
        bool meaningfulProgress,
        int progressScore,
        IReadOnlyList<string> completedMilestones,
        BuildHypothesis hypothesis)
    {
        IntentDecisionReason.RequireKnown(reason);
        var assessment = IntentCommitEvaluator.Assess(
            hypothesis,
            milestoneAdvanced,
            scarceResourceInvested,
            _intent.PivotConditions);
        var isCommit = !_state.IsCommitted && assessment.Eligible;
        var commitIndex = isCommit ? _state.NextDecisionIndex : _state.CommitDecisionIndex;
        var status = reason switch
        {
            IntentDecisionReason.Pivot => IntentStatus.Pivoted,
            IntentDecisionReason.Abandon => IntentStatus.Abandoned,
            _ => _state.Status,
        };
        var nextState = new IntentState(
            _intent.IntentId,
            _intent.SourceLane,
            nextDecisionIndex: _state.NextDecisionIndex + 1,
            declaredAtDecisionIndex: _state.DeclaredAtDecisionIndex,
            commitDecisionIndex: commitIndex,
            status: status,
            consecutiveNoProgressDecisions: meaningfulProgress
                ? 0
                : _state.ConsecutiveNoProgressDecisions + 1,
            progressScore: Math.Max(_state.ProgressScore, progressScore),
            completedMilestones: completedMilestones
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            hypothesis: hypothesis);
        var trace = new HeadlessIntentDecision(
            _state.NextDecisionIndex,
            decisionKind,
            action,
            reason,
            milestoneAdvanced,
            scarceResourceInvested,
            isCommit,
            assessment,
            hypothesis,
            nextState);
        _decisionTrace.Add(trace);
        _state = nextState;
    }

    private string Rationale(string reason, string detail)
        => $"intent_reason={reason};intent={_intent.IntentId};lane={_intent.SourceLane};{detail}";

    private static string PreviewRationale(
        ConceptDeploymentSelection selection,
        PreviewGroundedDecisionTrace trace)
        => $"progress={selection.ProgressScore};milestones={selection.CompletedMilestones.Count}"
           + $";threats={string.Join(",", trace.ThreatTags)}"
           + $";counter_links={trace.CounterConnections.Count}"
           + $";formation_rule={trace.FormationRule}"
           + $";identity_preserved={trace.CoreIdentityPreserved.ToString().ToLowerInvariant()}"
           + $";replacements={trace.ReplacementCount}"
           + $";full_reset={trace.IsFullReset.ToString().ToLowerInvariant()}";

    private static void ValidateIntent(HeadlessConceptIntent intent)
    {
        if (intent == null
            || string.IsNullOrWhiteSpace(intent.IntentId)
            || string.IsNullOrWhiteSpace(intent.SourceLane)
            || intent.IdentityPredicates.Count == 0
            || intent.ProgressMilestones.Count == 0
            || string.IsNullOrWhiteSpace(intent.PayoffWitnessId)
            || intent.PivotConditions.Count == 0)
        {
            throw new InvalidOperationException(
                "Concept intent must include id, lane, identity, progress milestones, expected payoff and pivot conditions.");
        }
    }

    private static BuildHypothesis EmptyHypothesis()
        => new(
            string.Empty,
            Array.Empty<string>(),
            0d,
            string.Empty,
            string.Empty,
            Array.Empty<string>(),
            string.Empty,
            -1,
            -1);
}
