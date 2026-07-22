using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;

internal sealed record DiveFailureObservation(
    string ReferenceSquadId,
    int BattleSeed,
    string DiverId,
    string DiverArchetypeId,
    string Outcome,
    string RetargetCause,
    string SwitchedTargetId,
    string SwitchedTargetArchetypeId,
    string SwitchedTargetRole,
    string KillerArchetypeId,
    string KillerRole,
    double? ElapsedSeconds,
    double? RemainingDistance,
    double? RemainingTimeBudgetSeconds,
    double? TimeToFirstBacklineContactSeconds,
    bool HasEligibleBackline,
    bool SelectorEverProducedBackline,
    bool DiveIntentEverSelectedBackline,
    bool ReachedActionRange,
    bool InRangeGateOpened,
    string Detail);

/// <summary>
/// Read-only witness for a single authored diver. It observes the resolver callback and existing telemetry;
/// it never calls target selection, writes telemetry, or mutates battle state.
/// </summary>
internal sealed class DiveFailureBattleObserver
{
    internal const string DiedEnRoute = "died_en_route";
    internal const string RetargetedAway = "retargeted_away_from_backline";
    internal const string NeverSelected = "never_selected_backline";
    internal const string InRangeNeverOpened = "reached_range_inrange_gate_never_opened";
    internal const string BattleEndedFirst = "battle_ended_first";
    internal const string Success = "reached_backline_successfully";

    private readonly BattleState _state;
    private readonly string _referenceSquadId;
    private readonly string _diverId;
    private readonly HashSet<string> _backlineIds;
    private int _telemetryCursor;
    private string _previousActualTargetId = string.Empty;
    private CombatIntentType _previousIntentType = CombatIntentType.None;
    private string _lastSelectorTargetId = string.Empty;
    private bool _selectorEverProducedBackline;
    private bool _diveIntentEverSelectedBackline;
    private bool _actualTargetEverBackline;
    private bool _reachedActionRange;
    private bool _inRangeGateOpened;
    private double? _firstContactSeconds;
    private double? _deathSeconds;
    private string _killerArchetypeId = string.Empty;
    private string _killerRole = string.Empty;
    private string _retargetCause = string.Empty;
    private string _switchedTargetId = string.Empty;
    private string _switchedTargetArchetypeId = string.Empty;
    private string _switchedTargetRole = string.Empty;
    private string _retargetDetail = string.Empty;

    internal DiveFailureBattleObserver(BattleState state, string referenceSquadId, string diverId)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _referenceSquadId = referenceSquadId;
        _diverId = diverId;
        _backlineIds = state.Allies
            .Where(IsEligibleDiveBackline)
            .Select(unit => unit.Id.Value)
            .ToHashSet(StringComparer.Ordinal);
        _telemetryCursor = state.TelemetryEvents.Count;
    }

    internal void ObserveStep(BattleSimulationStep step)
    {
        ObserveNewTelemetry();
        var diver = _state.FindUnitById(_diverId);
        if (diver == null)
        {
            return;
        }

        var actualTarget = _state.FindUnit(diver.CurrentTargetId);
        var actualTargetId = actualTarget?.Id.Value ?? string.Empty;
        var intent = diver.CurrentCombatIntent;
        if (intent.Type == CombatIntentType.Dive
            && intent.TargetId is { } intentTargetId
            && _backlineIds.Contains(intentTargetId.Value))
        {
            _diveIntentEverSelectedBackline = true;
        }

        if (_backlineIds.Contains(actualTargetId))
        {
            _actualTargetEverBackline = true;
            if (actualTarget != null && MovementResolver.IsInActionRange(diver, actualTarget, diver.AttackRange))
            {
                _reachedActionRange = true;
            }
        }

        if (string.IsNullOrEmpty(_retargetCause)
            && _backlineIds.Contains(_previousActualTargetId)
            && !string.IsNullOrEmpty(actualTargetId)
            && !_backlineIds.Contains(actualTargetId))
        {
            CaptureRetarget(diver, actualTarget, _previousActualTargetId, intent.Type);
        }

        ObserveDeath(step);
        _previousActualTargetId = actualTargetId;
        _previousIntentType = intent.Type;
    }

    internal DiveFailureObservation Complete()
    {
        ObserveNewTelemetry();
        var diver = _state.FindUnitById(_diverId)
                     ?? throw new InvalidOperationException($"Dive witness diver '{_diverId}' disappeared from battle state.");
        var outcome = ResolveOutcome();
        var remainingDistance = ResolveRemainingDistance(diver);
        var remainingTime = Math.Max(
            0d,
            (BattleSimulator.DefaultMaxSteps * BattleSimulator.DefaultFixedStepSeconds) - _state.ElapsedSeconds);
        var elapsed = outcome switch
        {
            Success => _firstContactSeconds,
            DiedEnRoute => _deathSeconds,
            _ => (double?)_state.ElapsedSeconds,
        };
        var detail = outcome switch
        {
            Success => "Positive damage from the diver reached an eligible player backline ranger/mystic.",
            DiedEnRoute => $"The diver died after selecting a backline objective; killer={RoleLabel(_killerArchetypeId, _killerRole)}.",
            RetargetedAway => _retargetDetail,
            NeverSelected => _deathSeconds.HasValue
                ? "No selector, Dive intent, or actual target ever selected an eligible backline ranger/mystic before the diver died."
                : _backlineIds.Count == 0
                    ? "The reference squad contains no eligible backline ranger/mystic for a Dive attempt."
                    : "No selector, Dive intent, or actual target ever selected an eligible backline ranger/mystic before battle end.",
            InRangeNeverOpened => "The diver entered basic-attack range of an eligible backline ranger/mystic, but no attack or skill start opened for that target.",
            _ => "The battle ended after a backline objective was selected but before positive-damage contact.",
        };

        return new DiveFailureObservation(
            _referenceSquadId,
            _state.Seed,
            _diverId,
            diver.Definition.ArchetypeId,
            outcome,
            _retargetCause,
            _switchedTargetId,
            _switchedTargetArchetypeId,
            _switchedTargetRole,
            _killerArchetypeId,
            _killerRole,
            elapsed,
            remainingDistance,
            remainingTime,
            _firstContactSeconds,
            _backlineIds.Count > 0,
            _selectorEverProducedBackline,
            _diveIntentEverSelectedBackline,
            _reachedActionRange,
            _inRangeGateOpened,
            detail);
    }

    private void ObserveNewTelemetry()
    {
        var telemetry = _state.TelemetryEvents;
        while (_telemetryCursor < telemetry.Count)
        {
            var record = telemetry[_telemetryCursor++];
            if (!string.Equals(record.Actor?.UnitInstanceId, _diverId, StringComparison.Ordinal))
            {
                continue;
            }

            var targetId = record.Target?.UnitInstanceId ?? string.Empty;
            if (record.EventKind is TelemetryEventKind.TargetAcquired or TelemetryEventKind.TargetSwitched)
            {
                _lastSelectorTargetId = targetId;
                _selectorEverProducedBackline |= _backlineIds.Contains(targetId);
            }

            if (record.EventKind is TelemetryEventKind.BasicAttackStarted or TelemetryEventKind.SkillCastStarted
                && _backlineIds.Contains(targetId))
            {
                _inRangeGateOpened = true;
            }

            if (!_firstContactSeconds.HasValue
                && record.EventKind == TelemetryEventKind.DamageApplied
                && record.ValueA > 0f
                && _backlineIds.Contains(targetId))
            {
                _firstContactSeconds = record.TimeSeconds;
            }
        }
    }

    private void ObserveDeath(BattleSimulationStep step)
    {
        if (_deathSeconds.HasValue)
        {
            return;
        }

        foreach (var battleEvent in step.Events.Where(value => value.EventKind == BattleEventKind.Kill))
        {
            var victimId = battleEvent.KillPayload?.ActualVictim.Value ?? battleEvent.TargetId?.Value;
            if (!string.Equals(victimId, _diverId, StringComparison.Ordinal))
            {
                continue;
            }

            _deathSeconds = step.TimeSeconds;
            var killerId = battleEvent.KillPayload?.ActualKiller.Value ?? battleEvent.ActorId.Value;
            var killer = _state.FindUnitById(killerId);
            _killerArchetypeId = killer?.Definition.ArchetypeId ?? "unknown";
            _killerRole = killer?.Definition.RoleTag ?? killer?.Definition.ClassId ?? "unknown";
            return;
        }
    }

    private void CaptureRetarget(
        UnitSnapshot diver,
        UnitSnapshot? nextTarget,
        string priorBacklineTargetId,
        CombatIntentType currentIntentType)
    {
        _switchedTargetId = nextTarget?.Id.Value ?? string.Empty;
        _switchedTargetArchetypeId = nextTarget?.Definition.ArchetypeId ?? string.Empty;
        _switchedTargetRole = nextTarget?.Definition.RoleTag ?? nextTarget?.Definition.ClassId ?? string.Empty;
        var priorTarget = _state.FindUnitById(priorBacklineTargetId);
        var screened = priorTarget is { IsAlive: true }
                       && BattleFormationConsequence.IsScreenedBacklineFrom(_state, diver, priorTarget);

        if (_previousIntentType == CombatIntentType.Dive && currentIntentType != CombatIntentType.Dive)
        {
            _retargetCause = "threat_score_change";
        }
        else if (diver.CurrentCombatIntent.TargetId is { } intentTarget
                 && string.Equals(intentTarget.Value, _switchedTargetId, StringComparison.Ordinal)
                 && currentIntentType is not CombatIntentType.Engage and not CombatIntentType.None)
        {
            _retargetCause = "intent_override";
        }
        else if (screened && string.Equals(_lastSelectorTargetId, _switchedTargetId, StringComparison.Ordinal))
        {
            _retargetCause = "screening_sort_key";
        }
        else if (diver.TargetSwitchLockRemaining > 0f || !diver.NeedsReevaluation)
        {
            _retargetCause = "stable_target_hold";
        }
        else
        {
            _retargetCause = "threat_score_change";
        }

        _retargetDetail = $"Switched to {RoleLabel(_switchedTargetArchetypeId, _switchedTargetRole)}; "
                          + $"cause={_retargetCause}, previous_intent={_previousIntentType}, "
                          + $"current_intent={currentIntentType}, prior_backline_screened={screened}, "
                          + $"selector_target={_lastSelectorTargetId}, switch_lock={diver.TargetSwitchLockRemaining:0.###}.";
    }

    private string ResolveOutcome()
    {
        if (_firstContactSeconds.HasValue)
        {
            return Success;
        }

        if (!string.IsNullOrEmpty(_retargetCause))
        {
            return RetargetedAway;
        }

        if (_deathSeconds.HasValue && (_diveIntentEverSelectedBackline || _actualTargetEverBackline))
        {
            return DiedEnRoute;
        }

        if (_reachedActionRange && !_inRangeGateOpened)
        {
            return InRangeNeverOpened;
        }

        if (!_selectorEverProducedBackline && !_diveIntentEverSelectedBackline && !_actualTargetEverBackline)
        {
            return NeverSelected;
        }

        return BattleEndedFirst;
    }

    private double? ResolveRemainingDistance(UnitSnapshot diver)
    {
        var targets = _backlineIds
            .Select(_state.FindUnitById)
            .Where(target => target != null)
            .Cast<UnitSnapshot>()
            .ToArray();
        return targets.Length == 0
            ? null
            : targets.Min(target => (double)MovementResolver.ComputeEdgeDistance(diver, target));
    }

    private static string RoleLabel(string archetypeId, string role)
        => string.IsNullOrWhiteSpace(archetypeId) && string.IsNullOrWhiteSpace(role)
            ? "unknown"
            : $"{archetypeId}/{role}";

    private static bool IsEligibleDiveBackline(UnitSnapshot unit)
        => unit.Behavior.FormationLine == FormationLine.Backline
           && unit.Definition.ClassId is "ranger" or "mystic";
}
