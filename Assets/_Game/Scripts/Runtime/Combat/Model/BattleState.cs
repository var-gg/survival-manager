using System.Collections.Generic;
using System.Linq;
using SM.Core.Contracts;
using SM.Core.Ids;

namespace SM.Combat.Model;

public sealed class BattleState
{
    private readonly Dictionary<string, HashSet<string>> _damageContributorsByVictim = new();
    private readonly HashSet<string> _scheduledOwnerDeaths = new();
    private readonly Dictionary<string, float> _ownedEntityDespawnTimers = new();
    private readonly Dictionary<string, GroupDispersalLockState> _groupDispersalLocks = new();
    private int _tacticContextStep = -1;
    private TacticContext? _allyTacticContext;
    private TacticContext? _enemyTacticContext;

    public BattleState(
        IReadOnlyList<UnitSnapshot> allies,
        IReadOnlyList<UnitSnapshot> enemies,
        TeamPostureType allyPosture,
        TeamPostureType enemyPosture,
        float fixedStepSeconds,
        int seed,
        TelemetryContext? telemetryContext = null,
        TeamTacticProfile? allyTactic = null,
        TeamTacticProfile? enemyTactic = null)
    {
        Allies = allies;
        Enemies = enemies;
        AllyTactic = allyTactic ?? TacticContext.DefaultProfile(allyPosture);
        EnemyTactic = enemyTactic ?? TacticContext.DefaultProfile(enemyPosture);
        AllyPosture = AllyTactic.Posture;
        EnemyPosture = EnemyTactic.Posture;
        FixedStepSeconds = fixedStepSeconds;
        Seed = seed;
        TelemetryContext = telemetryContext;
    }

    public IReadOnlyList<UnitSnapshot> Allies { get; }
    public IReadOnlyList<UnitSnapshot> Enemies { get; }
    public TeamPostureType AllyPosture { get; }
    public TeamPostureType EnemyPosture { get; }
    public TeamTacticProfile AllyTactic { get; }
    public TeamTacticProfile EnemyTactic { get; }
    public float FixedStepSeconds { get; }
    public int Seed { get; }
    public TelemetryContext? TelemetryContext { get; }
    public BattleActivityTelemetryAccumulator ActivityTelemetry { get; } = new();
    public int StepIndex { get; private set; }
    public float ElapsedSeconds { get; private set; }
    public EffectPositionSnapshot? EffectPositionSnapshot { get; private set; }
    public int EffectPositionSnapshotStep { get; private set; } = -1;
    public IEnumerable<GroupDispersalLockState> ActiveGroupDispersalLocks => _groupDispersalLocks.Values;
    private readonly List<TelemetryEventRecord> _telemetryEvents = new();
    public IReadOnlyList<TelemetryEventRecord> TelemetryEvents => _telemetryEvents;

    public IEnumerable<UnitSnapshot> AllUnits => Allies.Concat(Enemies);
    public IEnumerable<UnitSnapshot> LivingAllies => Allies.Where(x => x.IsAlive);
    public IEnumerable<UnitSnapshot> LivingEnemies => Enemies.Where(x => x.IsAlive);

    public IEnumerable<UnitSnapshot> GetTeam(TeamSide side) => side == TeamSide.Ally ? Allies : Enemies;
    public IEnumerable<UnitSnapshot> GetOpponents(TeamSide side) => side == TeamSide.Ally ? Enemies : Allies;
    public TeamPostureType GetPosture(TeamSide side) => side == TeamSide.Ally ? AllyPosture : EnemyPosture;
    public TeamTacticProfile GetTeamTactic(TeamSide side) => side == TeamSide.Ally ? AllyTactic : EnemyTactic;

    public TacticContext GetTacticContext(TeamSide side)
    {
        if (_tacticContextStep != StepIndex)
        {
            _allyTacticContext = null;
            _enemyTacticContext = null;
            _tacticContextStep = StepIndex;
        }

        if (side == TeamSide.Ally)
        {
            _allyTacticContext ??= TacticContext.Create(TeamSide.Ally, AllyTactic, StepIndex);
            return _allyTacticContext;
        }

        _enemyTacticContext ??= TacticContext.Create(TeamSide.Enemy, EnemyTactic, StepIndex);
        return _enemyTacticContext;
    }

    public UnitSnapshot? FindUnitById(string? id)
    {
        return string.IsNullOrWhiteSpace(id)
            ? null
            : AllUnits.FirstOrDefault(unit => unit.Id.Value == id);
    }

    public UnitSnapshot? FindUnit(EntityId? id) => id is { } value ? FindUnitById(value.Value) : null;

    public void AddTelemetry(TelemetryEventRecord record)
    {
        if (record != null)
        {
            _telemetryEvents.Add(record);
        }
    }

    public void SetEffectPositionSnapshot(EffectPositionSnapshot snapshot)
    {
        EffectPositionSnapshot = snapshot;
        EffectPositionSnapshotStep = snapshot.StepIndex;
    }

    public bool ApplyGroupDispersalLock(UnitSnapshot unit, CombatVector2 center, float severity, int affectedCount = 3)
    {
        if (unit == null || !unit.IsAlive)
        {
            return false;
        }

        var safeSeverity = System.Math.Clamp(severity, 0f, 1f);
        var duration = System.Math.Min(1.8f, 0.75f + (0.25f * System.Math.Max(0, affectedCount - 2)) + (0.25f * safeSeverity));
        var until = ElapsedSeconds + duration;
        if (_groupDispersalLocks.TryGetValue(unit.Id.Value, out var existing)
            && existing.DispersedUntilSeconds >= until - 0.001f)
        {
            return false;
        }

        _groupDispersalLocks[unit.Id.Value] = new GroupDispersalLockState(unit.Id.Value, center, until, safeSeverity);
        unit.RequestReevaluation(ReevaluationReason.TargetMoved);
        return true;
    }

    public bool IsUnderGroupDispersalLock(UnitSnapshot unit)
    {
        return unit != null
               && _groupDispersalLocks.TryGetValue(unit.Id.Value, out var state)
               && state.DispersedUntilSeconds > ElapsedSeconds;
    }

    public void RegisterDamage(UnitSnapshot attacker, UnitSnapshot victim)
    {
        if (!_damageContributorsByVictim.TryGetValue(victim.Id.Value, out var contributors))
        {
            contributors = new HashSet<string>();
            _damageContributorsByVictim[victim.Id.Value] = contributors;
        }

        contributors.Add(attacker.Id.Value);
    }

    public IReadOnlyList<UnitSnapshot> ConsumeAssistContributors(EntityId victimId, EntityId actualKillerId)
    {
        if (!_damageContributorsByVictim.Remove(victimId.Value, out var contributors))
        {
            return System.Array.Empty<UnitSnapshot>();
        }

        return contributors
            .Where(id => !string.Equals(id, actualKillerId.Value, System.StringComparison.Ordinal))
            .Select(FindUnitById)
            .Where(unit => unit is { IsAlive: true })
            .Cast<UnitSnapshot>()
            .ToList();
    }

    public void ScheduleOwnedEntityDespawnIfOwnerDead()
    {
        foreach (var owner in AllUnits.Where(unit => !unit.IsAlive))
        {
            if (!_scheduledOwnerDeaths.Add(owner.Id.Value))
            {
                continue;
            }

            foreach (var owned in AllUnits.Where(unit =>
                         unit.IsAlive
                         && unit.EntityKind is not CombatEntityKind.RosterUnit
                         && unit.Ownership is { } ownership
                         && ownership.OwnerEntity == owner.Id))
            {
                var delay = owned.SummonProfile?.OwnerDeathDespawnDelaySeconds ?? 1f;
                _ownedEntityDespawnTimers[owned.Id.Value] = delay;
            }
        }
    }

    public void AdvanceOwnedEntityDespawns()
    {
        var keys = _ownedEntityDespawnTimers.Keys.ToList();
        foreach (var unitId in keys)
        {
            var remaining = _ownedEntityDespawnTimers[unitId] - FixedStepSeconds;
            if (remaining > 0f)
            {
                _ownedEntityDespawnTimers[unitId] = remaining;
                continue;
            }

            _ownedEntityDespawnTimers.Remove(unitId);
            var unit = FindUnitById(unitId);
            if (unit is { IsAlive: true })
            {
                unit.Despawn();
            }
        }
    }

    public void AdvanceGroupDispersalLocks()
    {
        foreach (var key in _groupDispersalLocks
                     .Where(pair => pair.Value.DispersedUntilSeconds <= ElapsedSeconds)
                     .Select(pair => pair.Key)
                     .ToList())
        {
            _groupDispersalLocks.Remove(key);
        }
    }

    public void DespawnNonRosterEntities()
    {
        foreach (var unit in AllUnits.Where(unit => unit.IsAlive && unit.EntityKind is not CombatEntityKind.RosterUnit))
        {
            unit.Despawn();
        }
    }

    public void AdvanceStep()
    {
        StepIndex++;
        ElapsedSeconds += FixedStepSeconds;
        EffectPositionSnapshot = null;
        EffectPositionSnapshotStep = -1;
        _allyTacticContext = null;
        _enemyTacticContext = null;
        _tacticContextStep = StepIndex;
    }
}
