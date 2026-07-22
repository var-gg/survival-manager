using SM.Combat.Model;
using SM.Core.Contracts;

internal sealed record RuntimeFormationObservation(
    string UnitId,
    string ArchetypeId,
    string ClassId,
    int SideIndex,
    FormationLine FormationLine);

internal sealed record RangedFreeFireSample(
    string Panel,
    int BattleSeed,
    string UnitId,
    string ArchetypeId,
    int AttacksBeforeFirstDamage,
    double? TimeToFirstDamageSeconds,
    bool DamageCensoredAtBattleEnd);

internal sealed record BacklineContactToKillSample(
    string Panel,
    int BattleSeed,
    string TargetId,
    string TargetArchetypeId,
    string TargetClassId,
    int TargetSideIndex,
    double ContactSeconds,
    double? DeathSeconds,
    double? ContactToKillSeconds);

internal sealed record DamageShareBeforeFirstDeathSample(
    string Panel,
    int BattleSeed,
    string UnitId,
    string ArchetypeId,
    string Role,
    double DamageTaken,
    double TeamDamageTaken);

internal sealed record CounterplayBattleObservation(
    string Panel,
    int BattleSeed,
    string DiverId,
    string DiveOutcome,
    IReadOnlyList<RuntimeFormationObservation> RuntimeFormation,
    bool ChargeEquipped,
    IReadOnlyList<TargetSelectionDiagnosticEvent> TargetSelections,
    IReadOnlyList<TacticEvaluationDiagnosticEvent> TacticEvaluations,
    IReadOnlyList<IntentOverrideDiagnosticEvent> IntentOverrides,
    IReadOnlyList<DiveIntentDiagnosticEvent> DiveIntentEvaluations,
    IReadOnlyList<DisplacementLifecycleDiagnosticEvent> DisplacementLifecycle,
    IReadOnlyList<HealingApplicationDiagnosticEvent> HealingApplications,
    IReadOnlyList<RangedFreeFireSample> RangedFreeFire,
    IReadOnlyList<BacklineContactToKillSample> ContactToKill,
    IReadOnlyList<DamageShareBeforeFirstDeathSample> DamageShareBeforeFirstDeath);

/// <summary>
/// Measurement-only consumer of the injected SM.Combat diagnostic seam plus canonical post-battle telemetry.
/// It cannot write BattleState, choose a target, draw RNG, or participate in resolution.
/// </summary>
internal sealed class CounterplayInstrumentationObserver : IBattleDiagnosticObserver
{
    internal const string ChargeSkillId = "skill_rusthide_charge";
    internal const string KnockbackSkillId = "skill_cinder_overrun";

    private readonly BattleState _state;
    private readonly string _panel;
    private readonly string _diverId;
    private readonly IReadOnlyList<RuntimeFormationObservation> _runtimeFormation;
    private readonly Dictionary<string, UnitSnapshot> _unitsById;
    private readonly List<TargetSelectionDiagnosticEvent> _targetSelections = new();
    private readonly List<TacticEvaluationDiagnosticEvent> _tacticEvaluations = new();
    private readonly List<IntentOverrideDiagnosticEvent> _intentOverrides = new();
    private readonly List<DiveIntentDiagnosticEvent> _diveIntentEvaluations = new();
    private readonly List<DisplacementLifecycleDiagnosticEvent> _displacementLifecycle = new();
    private readonly List<HealingApplicationDiagnosticEvent> _healingApplications = new();

    internal CounterplayInstrumentationObserver(BattleState state, string panel, string diverId)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _panel = panel;
        _diverId = diverId;
        _unitsById = state.AllUnits.ToDictionary(unit => unit.Id.Value, StringComparer.Ordinal);
        _runtimeFormation = state.AllUnits
            .OrderBy(unit => unit.Side)
            .ThenBy(unit => unit.Id.Value, StringComparer.Ordinal)
            .Select(unit => new RuntimeFormationObservation(
                unit.Id.Value,
                unit.Definition.ArchetypeId,
                unit.Definition.ClassId,
                (int)unit.Side,
                unit.Behavior.FormationLine))
            .ToArray();
    }

    public bool ShouldObserve(BattleDiagnosticKind kind, string actorId, string skillId = "")
    {
        return kind switch
        {
            BattleDiagnosticKind.TargetSelection
                or BattleDiagnosticKind.TacticEvaluation
                or BattleDiagnosticKind.IntentOverride
                or BattleDiagnosticKind.DiveIntentEvaluation
                => string.Equals(actorId, _diverId, StringComparison.Ordinal),
            BattleDiagnosticKind.DisplacementLifecycle
                => string.Equals(skillId, ChargeSkillId, StringComparison.Ordinal)
                   || string.Equals(skillId, KnockbackSkillId, StringComparison.Ordinal),
            BattleDiagnosticKind.HealingApplication => true,
            _ => false,
        };
    }

    public void Observe(BattleDiagnosticEvent diagnosticEvent)
    {
        switch (diagnosticEvent)
        {
            case TargetSelectionDiagnosticEvent targetSelection:
                _targetSelections.Add(targetSelection);
                break;
            case TacticEvaluationDiagnosticEvent tacticEvaluation:
                _tacticEvaluations.Add(tacticEvaluation);
                break;
            case IntentOverrideDiagnosticEvent intentOverride:
                _intentOverrides.Add(intentOverride);
                break;
            case DiveIntentDiagnosticEvent diveIntent:
                _diveIntentEvaluations.Add(diveIntent);
                break;
            case DisplacementLifecycleDiagnosticEvent displacement:
                _displacementLifecycle.Add(displacement);
                break;
            case HealingApplicationDiagnosticEvent healing:
                _healingApplications.Add(healing);
                break;
        }
    }

    internal CounterplayBattleObservation Complete(BattleResult result, DiveFailureObservation diveFailure)
    {
        return new CounterplayBattleObservation(
            _panel,
            _state.Seed,
            _diverId,
            diveFailure.Outcome,
            _runtimeFormation,
            HasEquippedCharge(),
            _targetSelections.ToArray(),
            _tacticEvaluations.ToArray(),
            _intentOverrides.ToArray(),
            _diveIntentEvaluations.ToArray(),
            _displacementLifecycle.ToArray(),
            _healingApplications.ToArray(),
            BuildRangedFreeFire(result),
            BuildContactToKill(),
            BuildDamageShares());
    }

    private bool HasEquippedCharge()
    {
        return _state.AllUnits.Any(unit =>
            string.Equals(unit.Definition.EffectiveSignatureActive?.Id, ChargeSkillId, StringComparison.Ordinal)
            || string.Equals(unit.Definition.EffectiveFlexActive?.Id, ChargeSkillId, StringComparison.Ordinal));
    }

    private IReadOnlyList<RangedFreeFireSample> BuildRangedFreeFire(BattleResult result)
    {
        var telemetry = _state.TelemetryEvents;
        return _state.Allies
            .Where(unit => string.Equals(unit.Definition.ClassId, "ranger", StringComparison.Ordinal))
            .OrderBy(unit => unit.Id.Value, StringComparer.Ordinal)
            .Select(unit =>
            {
                var firstDamage = telemetry
                    .Where(record => record.EventKind == TelemetryEventKind.DamageApplied
                                     && record.ValueA > 0f
                                     && string.Equals(record.Target?.UnitInstanceId, unit.Id.Value, StringComparison.Ordinal))
                    .Select(record => (double?)record.TimeSeconds)
                    .Min();
                var attacks = telemetry.Count(record =>
                    record.EventKind is TelemetryEventKind.BasicAttackResolved or TelemetryEventKind.SkillCastResolved
                    && record.ValueA > 0f
                    && string.Equals(record.Actor?.UnitInstanceId, unit.Id.Value, StringComparison.Ordinal)
                    && record.Target is { SideIndex: 1 }
                    && (!firstDamage.HasValue || record.TimeSeconds < firstDamage.Value));
                return new RangedFreeFireSample(
                    _panel,
                    _state.Seed,
                    unit.Id.Value,
                    unit.Definition.ArchetypeId,
                    attacks,
                    firstDamage,
                    !firstDamage.HasValue && result.DurationSeconds > 0f);
            })
            .ToArray();
    }

    private IReadOnlyList<BacklineContactToKillSample> BuildContactToKill()
    {
        var telemetry = _state.TelemetryEvents;
        var results = new List<BacklineContactToKillSample>();
        foreach (var target in _state.AllUnits
                     .Where(unit => unit.Behavior.FormationLine == FormationLine.Backline)
                     .OrderBy(unit => unit.Id.Value, StringComparer.Ordinal))
        {
            var firstContact = telemetry
                .Where(record => record.EventKind == TelemetryEventKind.DamageApplied
                                 && record.ValueA > 0f
                                 && string.Equals(record.Target?.UnitInstanceId, target.Id.Value, StringComparison.Ordinal)
                                 && record.Actor != null
                                 && record.Actor.SideIndex != (int)target.Side)
                .Select(record => (double?)record.TimeSeconds)
                .Min();
            if (!firstContact.HasValue)
            {
                continue;
            }

            var death = telemetry
                .Where(record => record.EventKind == TelemetryEventKind.UnitDied
                                 && string.Equals(record.Actor?.UnitInstanceId, target.Id.Value, StringComparison.Ordinal)
                                 && record.TimeSeconds >= firstContact.Value)
                .Select(record => (double?)record.TimeSeconds)
                .Min();
            results.Add(new BacklineContactToKillSample(
                _panel,
                _state.Seed,
                target.Id.Value,
                target.Definition.ArchetypeId,
                target.Definition.ClassId,
                (int)target.Side,
                firstContact.Value,
                death,
                death.HasValue ? death.Value - firstContact.Value : null));
        }

        return results;
    }

    private IReadOnlyList<DamageShareBeforeFirstDeathSample> BuildDamageShares()
    {
        var telemetry = _state.TelemetryEvents;
        var firstAllyDeath = telemetry
            .Where(record => record.EventKind == TelemetryEventKind.UnitDied
                             && record.Actor is { SideIndex: 0 })
            .Select(record => (double?)record.TimeSeconds)
            .Min();
        if (!firstAllyDeath.HasValue)
        {
            return Array.Empty<DamageShareBeforeFirstDeathSample>();
        }

        var damageByUnit = telemetry
            .Where(record => record.EventKind == TelemetryEventKind.DamageApplied
                             && record.ValueA > 0f
                             && record.TimeSeconds <= firstAllyDeath.Value
                             && record.Target is { SideIndex: 0 })
            .GroupBy(record => record.Target!.UnitInstanceId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Sum(record => (double)record.ValueA), StringComparer.Ordinal);
        var teamDamage = damageByUnit.Values.Sum();
        if (teamDamage <= 0d)
        {
            return Array.Empty<DamageShareBeforeFirstDeathSample>();
        }

        return _state.Allies
            .OrderBy(unit => unit.Id.Value, StringComparer.Ordinal)
            .Select(unit => new DamageShareBeforeFirstDeathSample(
                _panel,
                _state.Seed,
                unit.Id.Value,
                unit.Definition.ArchetypeId,
                unit.Definition.ClassId,
                damageByUnit.GetValueOrDefault(unit.Id.Value, 0d),
                teamDamage))
            .ToArray();
    }
}
