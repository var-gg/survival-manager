using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Core.Contracts;
using SM.Core.Stats;

namespace SM.Combat.Services;

/// <summary>
/// Mutable builders for optional battle diagnostic records. They collect only observer-facing values and never
/// participate in combat resolution, canonical telemetry, replay state, hashing, or RNG.
/// </summary>
internal sealed class TargetSelectionTrace
{
    private readonly BattleState _state;
    private readonly UnitSnapshot _actor;
    private readonly TargetRule _rule;
    private readonly TargetSelectionPurpose _purpose;
    private readonly string _skillId;
    private readonly string _currentTargetId;
    private readonly float _acquireRange;
    private readonly string _acquireRangeSource;
    private readonly Dictionary<string, MutableCandidateDiagnostic> _candidates = new(StringComparer.Ordinal);
    private string _primarySelectedTargetId = string.Empty;
    private string _finalSelectedTargetId = string.Empty;
    private bool _fallbackUsed;
    private bool _meleeNearestOverrideApplied;

    internal TargetSelectionTrace(
        BattleState state,
        UnitSnapshot actor,
        TargetRule rule,
        TargetSelectionPurpose purpose,
        string skillId,
        float acquireRange,
        string acquireRangeSource)
    {
        _state = state;
        _actor = actor;
        _rule = rule;
        _purpose = purpose;
        _skillId = skillId;
        _acquireRange = acquireRange;
        _acquireRangeSource = acquireRangeSource;
        _currentTargetId = actor.CurrentTargetId?.Value ?? string.Empty;
    }

    internal void RecordCandidateValidation(
        UnitSnapshot? target,
        TargetCandidateRejectionReason rejection,
        bool rangeRelaxation)
    {
        if (target == null)
        {
            return;
        }

        var candidate = GetOrAdd(target);
        if (rangeRelaxation)
        {
            candidate.EligibleAfterRangeRelaxation = rejection == TargetCandidateRejectionReason.None;
            return;
        }

        candidate.InitialRejection = rejection;
        candidate.EligibleAfterRangeRelaxation = rejection == TargetCandidateRejectionReason.None;
    }

    internal void RecordScore(
        UnitSnapshot target,
        float targetSwitchPenalty,
        float focusBias,
        float flankBias,
        float screenPenalty,
        float guardedPenalty,
        float focusMarkBias,
        float comboOpportunityBias,
        float total,
        bool screened)
    {
        var candidate = GetOrAdd(target);
        candidate.TargetSwitchPenalty = targetSwitchPenalty;
        candidate.FocusBias = focusBias;
        candidate.FlankBias = flankBias;
        candidate.ScreenPenalty = screenPenalty;
        candidate.GuardedPenalty = guardedPenalty;
        candidate.FocusMarkBias = focusMarkBias;
        candidate.ComboOpportunityBias = comboOpportunityBias;
        candidate.TotalTacticBias = total;
        candidate.Screened = screened;
    }

    internal void RecordScreenedSortKey(UnitSnapshot target, int key, bool screened)
    {
        var candidate = GetOrAdd(target);
        candidate.ScreenedSortKey = key;
        candidate.Screened = screened;
    }

    internal void RecordBacklineExposure(UnitSnapshot target, bool exposed)
    {
        GetOrAdd(target).BacklineExposed = exposed;
    }

    internal void RecordPrimarySelection(UnitSnapshot? target)
    {
        _primarySelectedTargetId = target?.Id.Value ?? string.Empty;
    }

    internal void RecordFallback(UnitSnapshot? target)
    {
        _fallbackUsed = true;
        if (string.IsNullOrEmpty(_primarySelectedTargetId))
        {
            _primarySelectedTargetId = target?.Id.Value ?? string.Empty;
        }
    }

    internal void RecordFinalSelection(UnitSnapshot? beforeMeleeOverride, UnitSnapshot? final)
    {
        _finalSelectedTargetId = final?.Id.Value ?? string.Empty;
        _meleeNearestOverrideApplied = beforeMeleeOverride != null
                                       && final != null
                                       && beforeMeleeOverride.Id != final.Id;
    }

    internal void Emit(BattleState state)
    {
        var candidates = _candidates.Values
            .OrderBy(candidate => candidate.Target.Id.Value, StringComparer.Ordinal)
            .Select(candidate => candidate.Build(_actor, _acquireRange, _acquireRangeSource))
            .ToArray();
        state.RecordDiagnostic(new TargetSelectionDiagnosticEvent(
            _state.StepIndex,
            _state.ElapsedSeconds,
            _actor.Id.Value,
            _actor.Definition.ArchetypeId,
            _actor.Definition.ClassId,
            _purpose,
            _skillId,
            _rule.Domain,
            _rule.PrimarySelector,
            _rule.FallbackPolicy,
            _rule.Filters,
            _rule.MaxAcquireRange,
            _actor.AttackRange,
            _acquireRange,
            _acquireRangeSource,
            _currentTargetId,
            _primarySelectedTargetId,
            _finalSelectedTargetId,
            _fallbackUsed,
            _meleeNearestOverrideApplied,
            candidates));
    }

    private MutableCandidateDiagnostic GetOrAdd(UnitSnapshot target)
    {
        if (_candidates.TryGetValue(target.Id.Value, out var candidate))
        {
            return candidate;
        }

        candidate = new MutableCandidateDiagnostic(target);
        _candidates.Add(target.Id.Value, candidate);
        return candidate;
    }

    private sealed class MutableCandidateDiagnostic
    {
        internal MutableCandidateDiagnostic(UnitSnapshot target)
        {
            Target = target;
        }

        internal UnitSnapshot Target { get; }
        internal TargetCandidateRejectionReason InitialRejection { get; set; }
        internal bool EligibleAfterRangeRelaxation { get; set; }
        internal bool BacklineExposed { get; set; }
        internal bool Screened { get; set; }
        internal int ScreenedSortKey { get; set; }
        internal float TargetSwitchPenalty { get; set; }
        internal float FocusBias { get; set; }
        internal float FlankBias { get; set; }
        internal float ScreenPenalty { get; set; }
        internal float GuardedPenalty { get; set; }
        internal float FocusMarkBias { get; set; }
        internal float ComboOpportunityBias { get; set; }
        internal float TotalTacticBias { get; set; }

        internal TargetCandidateDiagnostic Build(
            UnitSnapshot actor,
            float acquireRange,
            string acquireRangeSource)
        {
            var edgeDistance = MovementResolver.ComputeEdgeDistance(actor, Target);
            var centerPathDistance = actor.Position.DistanceTo(Target.Position);
            return new TargetCandidateDiagnostic(
                Target.Id.Value,
                Target.Definition.ArchetypeId,
                Target.Definition.ClassId,
                Target.Behavior.FormationLine,
                Target.IsAlive,
                Target.CurrentHealth,
                Target.HealthRatio,
                edgeDistance,
                centerPathDistance,
                acquireRange,
                acquireRangeSource,
                InitialRejection,
                EligibleAfterRangeRelaxation,
                BacklineExposed,
                Screened,
                ScreenedSortKey,
                TargetSwitchPenalty,
                FocusBias,
                FlankBias,
                ScreenPenalty,
                GuardedPenalty,
                FocusMarkBias,
                ComboOpportunityBias,
                TotalTacticBias,
                edgeDistance + TotalTacticBias);
        }
    }
}

internal sealed class TacticEvaluationTrace
{
    private readonly BattleState _state;
    private readonly UnitSnapshot _actor;
    private readonly string _currentTargetIdAtEntry;
    private readonly float _targetSwitchLockRemaining;
    private readonly bool _needsReevaluation;
    private StableTargetDisposition _stableTargetDisposition = StableTargetDisposition.NotEvaluated;
    private string _stableTargetId = string.Empty;
    private float _stableTargetEdgeDistance;
    private float _stableTargetAcquireLeash;

    internal TacticEvaluationTrace(BattleState state, UnitSnapshot actor)
    {
        _state = state;
        _actor = actor;
        _currentTargetIdAtEntry = actor.CurrentTargetId?.Value ?? string.Empty;
        _targetSwitchLockRemaining = actor.TargetSwitchLockRemaining;
        _needsReevaluation = actor.NeedsReevaluation;
    }

    internal void RecordStableTarget(
        StableTargetDisposition disposition,
        UnitSnapshot? target,
        float edgeDistance,
        float acquireLeash)
    {
        if (_stableTargetDisposition != StableTargetDisposition.NotEvaluated
            && disposition != StableTargetDisposition.HeldByDiveIntent)
        {
            return;
        }

        _stableTargetDisposition = disposition;
        _stableTargetId = target?.Id.Value ?? string.Empty;
        _stableTargetEdgeDistance = edgeDistance;
        _stableTargetAcquireLeash = acquireLeash;
    }

    internal void Emit(BattleState state, EvaluatedAction evaluated)
    {
        state.RecordDiagnostic(new TacticEvaluationDiagnosticEvent(
            _state.StepIndex,
            _state.ElapsedSeconds,
            _actor.Id.Value,
            _actor.Definition.ArchetypeId,
            _actor.Definition.ClassId,
            _currentTargetIdAtEntry,
            _targetSwitchLockRemaining,
            _needsReevaluation,
            _stableTargetDisposition,
            _stableTargetId,
            _stableTargetEdgeDistance,
            _stableTargetAcquireLeash,
            evaluated.ActionType,
            evaluated.Target?.Id.Value ?? string.Empty,
            evaluated.Skill?.Id ?? string.Empty,
            evaluated.Skill?.DisplacementKind ?? SkillDisplacementKind.None));
    }
}

internal sealed class DiveIntentTrace
{
    private readonly BattleState _state;
    private readonly UnitSnapshot _actor;
    private readonly float _requiredHealthRatio;
    private readonly float _meleeRangeThreshold;
    private readonly float _maxForwardDepth;
    private readonly float _maxPathDistance;
    private readonly int _nearbyEnemyLimit;
    private readonly Dictionary<string, MutableDiveCandidate> _candidates = new(StringComparer.Ordinal);
    private DiveIntentGateReason _reason = DiveIntentGateReason.DistinctTargetUnavailable;
    private bool _continuingExistingDive;
    private string _currentIntentTargetId = string.Empty;
    private string _selectedTargetId = string.Empty;
    private bool _hasSupportProxy;
    private int _nearbyEnemyCount;
    private int _activeDiverCount;
    private int _eligibleDiverCount;
    private int _selectedCommitUntilStep = -1;

    internal DiveIntentTrace(
        BattleState state,
        UnitSnapshot actor,
        float requiredHealthRatio,
        float meleeRangeThreshold,
        float maxForwardDepth,
        float maxPathDistance,
        int nearbyEnemyLimit)
    {
        _state = state;
        _actor = actor;
        _requiredHealthRatio = requiredHealthRatio;
        _meleeRangeThreshold = meleeRangeThreshold;
        _maxForwardDepth = maxForwardDepth;
        _maxPathDistance = maxPathDistance;
        _nearbyEnemyLimit = nearbyEnemyLimit;
    }

    internal void Fail(DiveIntentGateReason reason)
    {
        _reason = reason;
    }

    internal void Select(string targetId, int commitUntilStep)
    {
        _reason = DiveIntentGateReason.Selected;
        _selectedTargetId = targetId;
        _selectedCommitUntilStep = commitUntilStep;
    }

    internal void SetContinuation(string targetId)
    {
        _continuingExistingDive = true;
        _currentIntentTargetId = targetId;
    }

    internal void SetSupportProxy(bool value)
    {
        _hasSupportProxy = value;
    }

    internal void SetNearbyEnemyCount(int value)
    {
        _nearbyEnemyCount = value;
    }

    internal void SetDiveSlotCounts(int active, int eligible)
    {
        _activeDiverCount = active;
        _eligibleDiverCount = eligible;
    }

    internal void RecordCandidateShape(UnitSnapshot target, bool postureEligible, int requiredScore)
    {
        var candidate = GetOrAdd(target);
        candidate.PostureEligible = postureEligible;
        candidate.RequiredScore = requiredScore;
    }

    internal void RecordCandidateScore(
        UnitSnapshot target,
        bool postureEligible,
        bool hasFocusMark,
        bool hasFrontlineProtector,
        float forwardDepth,
        float pathDistance,
        int formationLineScore,
        int classScore,
        int lowHealthScore,
        int protectorScore,
        int focusMarkScore,
        int forwardDepthScore,
        int pathDistanceScore,
        int totalScore,
        int requiredScore)
    {
        var candidate = GetOrAdd(target);
        candidate.PostureEligible = postureEligible;
        candidate.HasFocusMark = hasFocusMark;
        candidate.HasFrontlineProtector = hasFrontlineProtector;
        candidate.ForwardDepth = forwardDepth;
        candidate.PathDistance = pathDistance;
        candidate.FormationLineScore = formationLineScore;
        candidate.ClassScore = classScore;
        candidate.LowHealthScore = lowHealthScore;
        candidate.ProtectorScore = protectorScore;
        candidate.FocusMarkScore = focusMarkScore;
        candidate.ForwardDepthScore = forwardDepthScore;
        candidate.PathDistanceScore = pathDistanceScore;
        candidate.TotalScore = totalScore;
        candidate.RequiredScore = requiredScore;
    }

    internal void Emit(BattleState state)
    {
        state.RecordDiagnostic(new DiveIntentDiagnosticEvent(
            _state.StepIndex,
            _state.ElapsedSeconds,
            _actor.Id.Value,
            _actor.Definition.ArchetypeId,
            _actor.Definition.ClassId,
            _reason,
            _continuingExistingDive,
            _currentIntentTargetId,
            _selectedTargetId,
            _state.GetPosture(_actor.Side),
            _actor.HealthRatio,
            _requiredHealthRatio,
            _actor.AttackRange,
            _meleeRangeThreshold,
            _maxForwardDepth,
            _maxPathDistance,
            _hasSupportProxy,
            _nearbyEnemyCount,
            _nearbyEnemyLimit,
            _activeDiverCount,
            _eligibleDiverCount,
            _actor.MoveSpeed,
            _actor.Stats.Get(StatKey.MoveSpeed),
            _state.FixedStepSeconds,
            _actor.CurrentCombatIntent.CommitUntilStep,
            _selectedCommitUntilStep,
            _candidates.Values
                .OrderBy(candidate => candidate.Target.Id.Value, StringComparer.Ordinal)
                .Select(candidate => candidate.Build(_actor))
                .ToArray()));
    }

    private MutableDiveCandidate GetOrAdd(UnitSnapshot target)
    {
        if (_candidates.TryGetValue(target.Id.Value, out var candidate))
        {
            return candidate;
        }

        candidate = new MutableDiveCandidate(target);
        _candidates.Add(target.Id.Value, candidate);
        return candidate;
    }

    private sealed class MutableDiveCandidate
    {
        internal MutableDiveCandidate(UnitSnapshot target)
        {
            Target = target;
        }

        internal UnitSnapshot Target { get; }
        internal bool PostureEligible { get; set; }
        internal bool HasFocusMark { get; set; }
        internal bool HasFrontlineProtector { get; set; }
        internal float ForwardDepth { get; set; }
        internal float PathDistance { get; set; }
        internal int FormationLineScore { get; set; }
        internal int ClassScore { get; set; }
        internal int LowHealthScore { get; set; }
        internal int ProtectorScore { get; set; }
        internal int FocusMarkScore { get; set; }
        internal int ForwardDepthScore { get; set; }
        internal int PathDistanceScore { get; set; }
        internal int TotalScore { get; set; }
        internal int RequiredScore { get; set; }

        internal DiveTargetCandidateDiagnostic Build(UnitSnapshot actor)
            => new(
                Target.Id.Value,
                Target.Definition.ArchetypeId,
                Target.Definition.ClassId,
                Target.Behavior.FormationLine,
                Target.IsAlive,
                PostureEligible,
                Target.HealthRatio,
                HasFocusMark,
                HasFrontlineProtector,
                ForwardDepth,
                PathDistance,
                MovementResolver.ComputeEdgeDistance(actor, Target),
                FormationLineScore,
                ClassScore,
                LowHealthScore,
                ProtectorScore,
                FocusMarkScore,
                ForwardDepthScore,
                PathDistanceScore,
                TotalScore,
                RequiredScore);
    }
}
