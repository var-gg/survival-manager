using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;
using SM.Core.Stats;

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
    double? RemainingCenterPath,
    double? RemainingTimeBudgetSeconds,
    double? TimeToFirstBacklineContactSeconds,
    bool HasEligibleBackline,
    bool SelectorEverProducedBackline,
    bool DiveIntentEverSelectedBackline,
    bool ReachedActionRange,
    bool InRangeGateOpened,
    double TerminalElapsedSeconds,
    double? TerminalRemainingDistance,
    double? TerminalRemainingCenterPath,
    DiveAttemptObservation? DiveAttempt,
    DiveSwitchObservation? Switch,
    IReadOnlyList<DiverApproachSample> ApproachTimeline,
    IReadOnlyList<DiverDamageObservation> DamageDuringApproach,
    string Detail);

internal sealed record DiveAttemptObservation(
    int StepIndex,
    double ElapsedSeconds,
    string TargetId,
    double HealthRatio,
    double MoveSpeed,
    double BaseMoveSpeed,
    double FixedStepSeconds,
    int CommitUntilStep,
    int CommitSteps,
    double? InitialEdgeDistance,
    double? InitialCenterPath,
    DiveIntentDiagnosticEvent RawObservation);

internal sealed record DiveSwitchObservation(
    int StepIndex,
    double ElapsedSeconds,
    string PriorTargetId,
    CombatIntentType PreviousObservedIntent,
    CombatIntentType IntentAtSwitch,
    double? RemainingEdgeDistance,
    double? RemainingCenterPath,
    string Attribution,
    bool AttributionObserved,
    bool GeometryVetoObserved,
    bool CommitExpiryObserved,
    bool ScreeningSortKeyObservedDecisive,
    TargetSelectionDiagnosticEvent? SelectorObservation,
    TargetCandidateDiagnostic? WinningCandidate,
    TargetCandidateDiagnostic? LosingCandidate,
    IntentOverrideDiagnosticEvent? IntentObservation,
    DiveHardAbortDiagnosticEvent? HardAbortObservation,
    DiveIntentDiagnosticEvent? LastDiveSelectionObservation,
    IntentOverrideDiagnosticEvent? IntentExitObservation,
    IReadOnlyList<DiveIntentDiagnosticEvent> ContinuationVetoObservations);

internal sealed record DiverApproachSample(
    int StepIndex,
    double ElapsedSeconds,
    double CurrentHealth,
    double MaxHealth,
    double HealthRatio,
    CombatIntentType Intent,
    string IntentTargetId,
    int CommitUntilStep,
    string ActualTargetId,
    CombatActionState ActionState,
    double MoveSpeed,
    double BaseMoveSpeed,
    bool Rooted,
    IReadOnlyList<string> StatusIds,
    double ActorX,
    double ActorY,
    double? TargetX,
    double? TargetY,
    double? EdgeDistance,
    double? CenterPathDistance);

internal sealed record DiverDamageObservation(
    double ElapsedSeconds,
    double Amount,
    string SourceUnitId,
    string SourceArchetypeId,
    string SourceRole);

/// <summary>
/// Read-only witness for a single authored diver. It observes the resolver callback, canonical telemetry, and the
/// injected diagnostic journal; it never calls target selection, writes telemetry, or mutates battle state.
/// Attribution is emitted only when the journal contains the complete observed chain. Otherwise it is unattributed.
/// </summary>
internal sealed class DiveFailureBattleObserver
{
    internal const string DiedEnRoute = "died_en_route";
    internal const string RetargetedAway = "retargeted_away_from_backline";
    internal const string NeverSelected = "never_selected_backline";
    internal const string InRangeNeverOpened = "reached_range_inrange_gate_never_opened";
    internal const string BattleEndedFirst = "battle_ended_first";
    internal const string Success = "reached_backline_successfully";
    internal const string HpHardAbortRetarget = "dive_hp_hard_abort_then_baseline_retarget";
    internal const string ContinuationGeometryRetarget = "continuation_geometry_veto_until_commit_expiry_then_baseline_retarget";
    internal const string ScreeningSortKey = "screening_sort_key";
    internal const string Unattributed = "unattributed";

    private readonly BattleState _state;
    private readonly CounterplayInstrumentationObserver _diagnostics;
    private readonly string _referenceSquadId;
    private readonly string _diverId;
    private readonly HashSet<string> _backlineIds;
    private readonly List<DiverApproachSample> _approachTimeline = new();
    private readonly List<DiverDamageObservation> _diverDamage = new();
    private int _telemetryCursor;
    private string _previousActualTargetId = string.Empty;
    private CombatIntentType _previousIntentType = CombatIntentType.None;
    private bool _selectorEverProducedBackline;
    private bool _diveIntentEverSelectedBackline;
    private bool _actualTargetEverBackline;
    private bool _reachedActionRange;
    private bool _inRangeGateOpened;
    private bool _approachClosed;
    private double? _firstContactSeconds;
    private string _firstContactTargetId = string.Empty;
    private DiveEventMeasurement? _contactMeasurement;
    private DiveEventMeasurement? _rangeMeasurement;
    private double? _deathSeconds;
    private DiveEventMeasurement? _deathMeasurement;
    private string _killerArchetypeId = string.Empty;
    private string _killerRole = string.Empty;
    private string _retargetCause = string.Empty;
    private string _switchedTargetId = string.Empty;
    private string _switchedTargetArchetypeId = string.Empty;
    private string _switchedTargetRole = string.Empty;
    private string _retargetDetail = string.Empty;
    private DiveAttemptObservation? _diveAttempt;
    private DiveSwitchObservation? _switch;

    internal DiveFailureBattleObserver(
        BattleState state,
        CounterplayInstrumentationObserver diagnostics,
        string referenceSquadId,
        string diverId)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
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

        ObserveDiveAttemptStart();
        if (!_approachClosed && _diveAttempt != null)
        {
            _approachTimeline.Add(BuildApproachSample(diver));
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
                _rangeMeasurement ??= Measure(diver, actualTarget, step.TimeSeconds);
            }
        }

        if (_firstContactSeconds.HasValue && _contactMeasurement == null)
        {
            _contactMeasurement = Measure(
                diver,
                _state.FindUnitById(_firstContactTargetId),
                _firstContactSeconds.Value);
            _approachClosed = true;
        }

        if (_switch == null
            && _backlineIds.Contains(_previousActualTargetId)
            && !string.IsNullOrEmpty(actualTargetId)
            && !_backlineIds.Contains(actualTargetId))
        {
            CaptureRetarget(diver, actualTarget, _previousActualTargetId, intent.Type, step);
            _approachClosed = true;
        }

        ObserveDeath(step, diver);
        _previousActualTargetId = actualTargetId;
        _previousIntentType = intent.Type;
    }

    internal DiveFailureObservation Complete()
    {
        ObserveNewTelemetry();
        var diver = _state.FindUnitById(_diverId)
                     ?? throw new InvalidOperationException($"Dive witness diver '{_diverId}' disappeared from battle state.");
        ObserveDiveAttemptStart();
        var outcome = ResolveOutcome();
        var terminal = MeasureNearestBackline(diver, _state.ElapsedSeconds);
        var eventMeasurement = ResolveEventMeasurement(outcome, terminal);
        var elapsed = eventMeasurement?.ElapsedSeconds;
        double? remainingTime = elapsed.HasValue
            ? Math.Max(0d, (BattleSimulator.DefaultMaxSteps * BattleSimulator.DefaultFixedStepSeconds) - elapsed.Value)
            : null;
        var approachEnd = _switch?.ElapsedSeconds
                          ?? _deathSeconds
                          ?? _firstContactSeconds
                          ?? _state.ElapsedSeconds;
        var approachDamage = _diveAttempt == null
            ? Array.Empty<DiverDamageObservation>()
            : _diverDamage
                .Where(value => value.ElapsedSeconds >= _diveAttempt.ElapsedSeconds
                                && value.ElapsedSeconds <= approachEnd)
                .ToArray();
        var detail = outcome switch
        {
            Success => "Observed positive diver damage to an eligible player backline ranger/mystic.",
            DiedEnRoute => $"Observed diver death after a backline objective was selected; killer={RoleLabel(_killerArchetypeId, _killerRole)}.",
            RetargetedAway => _retargetDetail,
            NeverSelected => _deathSeconds.HasValue
                ? "No selector, Dive intent, or actual target observed an eligible backline ranger/mystic before the diver died."
                : _backlineIds.Count == 0
                    ? "The reference squad contains no eligible backline ranger/mystic for a Dive attempt."
                    : "No selector, Dive intent, or actual target observed an eligible backline ranger/mystic before battle end.",
            InRangeNeverOpened => "Observed entry into basic-attack range of an eligible backline target, but no attack or skill start opened for it.",
            _ => "The battle ended after a backline objective was observed but before positive-damage contact.",
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
            eventMeasurement?.EdgeDistance,
            eventMeasurement?.CenterPathDistance,
            remainingTime,
            _firstContactSeconds,
            _backlineIds.Count > 0,
            _selectorEverProducedBackline,
            _diveIntentEverSelectedBackline,
            _reachedActionRange,
            _inRangeGateOpened,
            _state.ElapsedSeconds,
            terminal?.EdgeDistance,
            terminal?.CenterPathDistance,
            _diveAttempt,
            _switch,
            _approachTimeline.ToArray(),
            approachDamage,
            detail);
    }

    private void ObserveDiveAttemptStart()
    {
        if (_diveAttempt != null)
        {
            return;
        }

        var selected = _diagnostics.DiveIntentEvaluations
            .FirstOrDefault(value => value.Reason == DiveIntentGateReason.Selected
                                     && _backlineIds.Contains(value.SelectedTargetId));
        if (selected == null)
        {
            return;
        }

        var candidate = selected.Candidates.FirstOrDefault(value =>
            string.Equals(value.TargetId, selected.SelectedTargetId, StringComparison.Ordinal));
        _diveAttempt = new DiveAttemptObservation(
            selected.StepIndex,
            selected.TimeSeconds,
            selected.SelectedTargetId,
            selected.ActorHealthRatio,
            selected.ActorMoveSpeed,
            selected.ActorBaseMoveSpeed,
            selected.FixedStepSeconds,
            selected.SelectedCommitUntilStep,
            selected.SelectedCommitUntilStep < selected.StepIndex
                ? 0
                : selected.SelectedCommitUntilStep - selected.StepIndex,
            candidate?.EdgeDistance,
            candidate?.PathDistance,
            selected);
    }

    private DiverApproachSample BuildApproachSample(UnitSnapshot diver)
    {
        var targetId = diver.CurrentCombatIntent.TargetId?.Value ?? _diveAttempt?.TargetId ?? string.Empty;
        var target = _state.FindUnitById(targetId);
        return new DiverApproachSample(
            _state.StepIndex,
            _state.ElapsedSeconds,
            diver.CurrentHealth,
            diver.MaxHealth,
            diver.HealthRatio,
            diver.CurrentCombatIntent.Type,
            diver.CurrentCombatIntent.TargetId?.Value ?? string.Empty,
            diver.CurrentCombatIntent.CommitUntilStep,
            diver.CurrentTargetId?.Value ?? string.Empty,
            diver.ActionState,
            diver.MoveSpeed,
            diver.Stats.Get(StatKey.MoveSpeed),
            diver.IsRooted,
            diver.Statuses.Select(value => value.StatusId).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            diver.Position.X,
            diver.Position.Y,
            target?.Position.X,
            target?.Position.Y,
            target == null ? null : MovementResolver.ComputeEdgeDistance(diver, target),
            target == null ? null : diver.Position.DistanceTo(target.Position));
    }

    private void ObserveNewTelemetry()
    {
        var telemetry = _state.TelemetryEvents;
        while (_telemetryCursor < telemetry.Count)
        {
            var record = telemetry[_telemetryCursor++];
            var targetId = record.Target?.UnitInstanceId ?? string.Empty;
            if (record.EventKind == TelemetryEventKind.DamageApplied
                && record.ValueA > 0f
                && string.Equals(targetId, _diverId, StringComparison.Ordinal))
            {
                var sourceId = record.Actor?.UnitInstanceId ?? string.Empty;
                var source = _state.FindUnitById(sourceId);
                _diverDamage.Add(new DiverDamageObservation(
                    record.TimeSeconds,
                    record.ValueA,
                    sourceId,
                    source?.Definition.ArchetypeId ?? record.Actor?.UnitBlueprintId ?? "unknown",
                    source?.Definition.RoleTag ?? source?.Definition.ClassId ?? "unknown"));
            }

            if (!string.Equals(record.Actor?.UnitInstanceId, _diverId, StringComparison.Ordinal))
            {
                continue;
            }

            if (record.EventKind is TelemetryEventKind.TargetAcquired or TelemetryEventKind.TargetSwitched)
            {
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
                _firstContactTargetId = targetId;
            }
        }
    }

    private void ObserveDeath(BattleSimulationStep step, UnitSnapshot diver)
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
            _deathMeasurement = MeasureNearestBackline(diver, step.TimeSeconds);
            var killerId = battleEvent.KillPayload?.ActualKiller.Value ?? battleEvent.ActorId.Value;
            var killer = _state.FindUnitById(killerId);
            _killerArchetypeId = killer?.Definition.ArchetypeId ?? "unknown";
            _killerRole = killer?.Definition.RoleTag ?? killer?.Definition.ClassId ?? "unknown";
            _approachClosed = true;
            return;
        }
    }

    private void CaptureRetarget(
        UnitSnapshot diver,
        UnitSnapshot? nextTarget,
        string priorBacklineTargetId,
        CombatIntentType currentIntentType,
        BattleSimulationStep step)
    {
        _switchedTargetId = nextTarget?.Id.Value ?? string.Empty;
        _switchedTargetArchetypeId = nextTarget?.Definition.ArchetypeId ?? string.Empty;
        _switchedTargetRole = nextTarget?.Definition.RoleTag ?? nextTarget?.Definition.ClassId ?? string.Empty;
        var selector = _diagnostics.TargetSelections
            .Where(value => value.StepIndex <= step.StepIndex
                            && string.Equals(value.CurrentTargetId, priorBacklineTargetId, StringComparison.Ordinal)
                            && string.Equals(value.FinalSelectedTargetId, _switchedTargetId, StringComparison.Ordinal))
            .OrderBy(value => value.StepIndex)
            .ThenBy(value => value.TimeSeconds)
            .LastOrDefault();
        var winningCandidate = selector?.Candidates.FirstOrDefault(value =>
            string.Equals(value.TargetId, _switchedTargetId, StringComparison.Ordinal));
        var losingCandidate = selector?.Candidates.FirstOrDefault(value =>
            string.Equals(value.TargetId, priorBacklineTargetId, StringComparison.Ordinal));
        var intentObservation = selector == null
            ? null
            : _diagnostics.IntentOverrides
                .Where(value => value.StepIndex == selector.StepIndex
                                && string.Equals(value.FinalTargetId, _switchedTargetId, StringComparison.Ordinal))
                .LastOrDefault();
        var entryStep = _diveAttempt?.StepIndex ?? int.MinValue;
        var switchStep = selector?.StepIndex ?? step.StepIndex;
        var diveEvaluations = _diagnostics.DiveIntentEvaluations
            .Where(value => value.StepIndex >= entryStep
                            && value.StepIndex <= switchStep
                            && (string.Equals(value.SelectedTargetId, priorBacklineTargetId, StringComparison.Ordinal)
                                || string.Equals(value.CurrentIntentTargetId, priorBacklineTargetId, StringComparison.Ordinal)))
            .OrderBy(value => value.StepIndex)
            .ToArray();
        var lastDiveSelection = diveEvaluations
            .Where(value => value.Reason == DiveIntentGateReason.Selected
                            && string.Equals(value.SelectedTargetId, priorBacklineTargetId, StringComparison.Ordinal))
            .OrderBy(value => value.StepIndex)
            .LastOrDefault();
        var chainStartStep = lastDiveSelection?.StepIndex ?? entryStep;
        var intentExit = _diagnostics.IntentOverrides
            .Where(value => value.StepIndex >= chainStartStep
                            && value.StepIndex <= switchStep
                            && value.IntentType != CombatIntentType.Dive)
            .OrderBy(value => value.StepIndex)
            .ThenBy(value => value.TimeSeconds)
            .FirstOrDefault();
        var hardAbort = _diagnostics.DiveHardAborts
            .Where(value => value.StepIndex >= chainStartStep
                            && value.StepIndex <= switchStep
                            && string.Equals(value.DiveTargetId, priorBacklineTargetId, StringComparison.Ordinal))
            .OrderBy(value => value.StepIndex)
            .LastOrDefault();
        var continuationVetoes = diveEvaluations
            .Where(value => value.StepIndex >= chainStartStep
                            && value.ContinuingExistingDive
                            && value.Reason == DiveIntentGateReason.ContinueScoreBelowThreshold)
            .ToArray();
        var geometryVetoes = continuationVetoes
            .Where(value => HasObservedGeometryVeto(value, priorBacklineTargetId))
            .ToArray();
        var commitExpiryObserved = lastDiveSelection != null
                                   && intentExit != null
                                   && geometryVetoes.Any(value => value.StepIndex <= intentExit.StepIndex)
                                   && intentExit.StepIndex >= lastDiveSelection.SelectedCommitUntilStep;
        var screeningSortKeyObserved = selector != null
                                         && winningCandidate != null
                                         && losingCandidate != null
                                         && WasScreeningSortKeyObservedDecisive(selector, winningCandidate, losingCandidate);

        _retargetCause = hardAbort != null
            ? HpHardAbortRetarget
            : geometryVetoes.Length > 0 && commitExpiryObserved
                ? ContinuationGeometryRetarget
                : screeningSortKeyObserved
                    ? ScreeningSortKey
                    : Unattributed;
        var causeObserved = !string.Equals(_retargetCause, Unattributed, StringComparison.Ordinal);
        var switchTime = selector?.TimeSeconds ?? step.TimeSeconds;
        var fallbackMeasurement = Measure(diver, _state.FindUnitById(priorBacklineTargetId), switchTime);
        var remainingEdge = losingCandidate?.EdgeDistance ?? fallbackMeasurement?.EdgeDistance;
        var remainingCenterPath = losingCandidate?.CenterPathDistance ?? fallbackMeasurement?.CenterPathDistance;
        _switch = new DiveSwitchObservation(
            switchStep,
            switchTime,
            priorBacklineTargetId,
            _previousIntentType,
            intentObservation?.IntentType ?? currentIntentType,
            remainingEdge,
            remainingCenterPath,
            _retargetCause,
            causeObserved,
            geometryVetoes.Length > 0,
            commitExpiryObserved,
            screeningSortKeyObserved,
            selector,
            winningCandidate,
            losingCandidate,
            intentObservation,
            hardAbort,
            lastDiveSelection,
            intentExit,
            continuationVetoes);
        _retargetDetail = selector == null
            ? $"Observed target switch to {RoleLabel(_switchedTargetArchetypeId, _switchedTargetRole)} without a matching selector record; attribution=unattributed."
            : $"Observed target switch to {RoleLabel(_switchedTargetArchetypeId, _switchedTargetRole)}; "
              + $"attribution={_retargetCause}, selector={selector.PrimarySelector}, purpose={selector.Purpose}, "
              + $"acquire_range={selector.ResolvedAcquireRange:0.###}, acquire_range_source={selector.AcquireRangeSource}, "
              + $"winner_screen_key={winningCandidate?.ScreenedSortKey}, loser_screen_key={losingCandidate?.ScreenedSortKey}.";
    }

    private string ResolveOutcome()
    {
        if (_firstContactSeconds.HasValue)
        {
            return Success;
        }

        if (_switch != null)
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

    private DiveEventMeasurement? ResolveEventMeasurement(string outcome, DiveEventMeasurement? terminal)
    {
        return outcome switch
        {
            Success => _contactMeasurement,
            DiedEnRoute => _deathMeasurement,
            RetargetedAway when _switch != null => new DiveEventMeasurement(
                _switch.ElapsedSeconds,
                _switch.RemainingEdgeDistance,
                _switch.RemainingCenterPath),
            InRangeNeverOpened => _rangeMeasurement,
            _ => terminal,
        };
    }

    private DiveEventMeasurement? MeasureNearestBackline(UnitSnapshot diver, double elapsedSeconds)
    {
        var target = _backlineIds
            .Select(_state.FindUnitById)
            .Where(value => value != null)
            .Cast<UnitSnapshot>()
            .OrderBy(value => MovementResolver.ComputeEdgeDistance(diver, value))
            .ThenBy(value => value.Id.Value, StringComparer.Ordinal)
            .FirstOrDefault();
        return Measure(diver, target, elapsedSeconds);
    }

    private static DiveEventMeasurement? Measure(UnitSnapshot diver, UnitSnapshot? target, double elapsedSeconds)
        => target == null
            ? null
            : new DiveEventMeasurement(
                elapsedSeconds,
                MovementResolver.ComputeEdgeDistance(diver, target),
                diver.Position.DistanceTo(target.Position));

    private static bool HasObservedGeometryVeto(DiveIntentDiagnosticEvent observation, string targetId)
    {
        var candidate = observation.Candidates.FirstOrDefault(value =>
            string.Equals(value.TargetId, targetId, StringComparison.Ordinal));
        return candidate != null
               && candidate.TotalScore < candidate.RequiredScore
               && (candidate.ForwardDepthScore < 0 || candidate.PathDistanceScore < 0)
               && candidate.TotalScore - candidate.ForwardDepthScore - candidate.PathDistanceScore
               >= candidate.RequiredScore;
    }

    private static bool WasScreeningSortKeyObservedDecisive(
        TargetSelectionDiagnosticEvent selector,
        TargetCandidateDiagnostic winner,
        TargetCandidateDiagnostic loser)
    {
        var keyParticipated = selector.PrimarySelector is TargetSelector.LowestCurrentHpEnemy
            or TargetSelector.LowestHpPercentEnemy
            or TargetSelector.LowestEhpEnemy
            || (selector.FallbackUsed && selector.FallbackPolicy == TargetFallbackPolicy.LowestCurrentHpEnemy);
        return keyParticipated
               && winner.ScreenedSortKey < loser.ScreenedSortKey
               && string.Equals(selector.FinalSelectedTargetId, winner.TargetId, StringComparison.Ordinal);
    }

    private static string RoleLabel(string archetypeId, string role)
        => string.IsNullOrWhiteSpace(archetypeId) && string.IsNullOrWhiteSpace(role)
            ? "unknown"
            : $"{archetypeId}/{role}";

    private static bool IsEligibleDiveBackline(UnitSnapshot unit)
        => unit.Behavior.FormationLine == FormationLine.Backline
           && unit.Definition.ClassId is "ranger" or "mystic";

    private sealed record DiveEventMeasurement(
        double ElapsedSeconds,
        double? EdgeDistance,
        double? CenterPathDistance);
}
