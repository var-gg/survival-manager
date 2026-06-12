using System.Collections.Generic;
using System.Linq;
using SM.Combat.Services;
using SM.Core.Contracts;
using SM.Core.Ids;

namespace SM.Combat.Model;

public sealed class BattleState
{
    private readonly Dictionary<string, HashSet<string>> _damageContributorsByVictim = new();
    private readonly HashSet<string> _scheduledOwnerDeaths = new();
    private readonly Dictionary<string, int> _ownedEntityDespawnTimers = new();
    private readonly Dictionary<string, GroupDispersalLockState> _groupDispersalLocks = new();
    private int _tacticContextStep = -1;
    private TacticContext? _allyTacticContext;
    private TacticContext? _enemyTacticContext;
    private TeamBlackboard _allyBlackboard = TeamBlackboard.CreateEmpty(TeamSide.Ally);
    private TeamBlackboard _enemyBlackboard = TeamBlackboard.CreateEmpty(TeamSide.Enemy);

    public BattleState(
        IReadOnlyList<UnitSnapshot> allies,
        IReadOnlyList<UnitSnapshot> enemies,
        TeamPostureType allyPosture,
        TeamPostureType enemyPosture,
        float fixedStepSeconds,
        int seed,
        TelemetryContext? telemetryContext = null,
        TeamTacticProfile? allyTactic = null,
        TeamTacticProfile? enemyTactic = null,
        CombatStatusRules? statusRules = null)
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
        StatusRules = statusRules ?? CombatStatusRules.Default;
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
    public CombatStatusRules StatusRules { get; }
    public BattleActivityTelemetryAccumulator ActivityTelemetry { get; } = new();
    public int StepIndex { get; private set; }
    // Phase 2.2d: StepIndex 파생(누산 float 제거). 권위는 정수 StepIndex, 이 값은 telemetry/read-model용 초 projection.
    public float ElapsedSeconds => StepIndex * FixedStepSeconds;
    public EffectPositionSnapshot? EffectPositionSnapshot { get; private set; }
    public int EffectPositionSnapshotStep { get; private set; } = -1;
    public IEnumerable<GroupDispersalLockState> ActiveGroupDispersalLocks => _groupDispersalLocks.Values;
    private readonly List<TelemetryEventRecord> _telemetryEvents = new();
    public IReadOnlyList<TelemetryEventRecord> TelemetryEvents => _telemetryEvents;

    private readonly List<BattleMotionIntent> _stepMotions = new();
    private int _motionSequence;

    private readonly List<BattleCombatEventIntent> _stepCombatEvents = new();
    private long _nextActionInstanceId = 1;

    /// <summary>Position transitions recorded during the current tick. Drained per step into the read model (C1 motion-intent channel).</summary>
    public IReadOnlyList<BattleMotionIntent> StepMotions => _stepMotions;

    /// <summary>Action phase/contact intents recorded during the current tick. Sibling of <see cref="StepMotions"/>; drained per step into the read model (combat-event-intent channel, action choreography seam C1).</summary>
    public IReadOnlyList<BattleCombatEventIntent> StepCombatEvents => _stepCombatEvents;

    /// <summary>Monotonic id of the next action instance to allocate. Part of save/replay identity so <see cref="ActionInstanceId"/> is stable and never reused (GPT Pro J23).</summary>
    public long NextActionInstanceIdValue => _nextActionInstanceId;

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

    /// <summary>
    /// Phase 2 팀 블랙보드(FocusMark/carry/breach/약측). 시뮬레이터가 step 선두에서
    /// <see cref="RefreshTeamBlackboardsIfDue"/>로 갱신하므로 한 step 안에서는 고정값이다.
    /// 시뮬레이터 없이 서비스만 직접 호출하는 경로(테스트)에서는 첫 접근이 lazy 계산한다 — 둘 다
    /// battle truth의 순수 함수라 결정론이 보존된다.
    /// </summary>
    public TeamBlackboard GetTeamBlackboard(TeamSide side)
    {
        RefreshTeamBlackboardsIfDue();
        return side == TeamSide.Ally ? _allyBlackboard : _enemyBlackboard;
    }

    public void RefreshTeamBlackboardsIfDue()
    {
        if (_allyBlackboard.HasBeenComputed
            && StepIndex - _allyBlackboard.ComputedAtStep < Services.TeamBlackboardService.CadenceSteps)
        {
            return;
        }

        var previousAlly = _allyBlackboard;
        var previousEnemy = _enemyBlackboard;
        _allyBlackboard = Services.TeamBlackboardService.Compute(this, TeamSide.Ally, previousAlly);
        _enemyBlackboard = Services.TeamBlackboardService.Compute(this, TeamSide.Enemy, previousEnemy);
        RecordBlackboardTransitions(previousAlly, _allyBlackboard);
        RecordBlackboardTransitions(previousEnemy, _enemyBlackboard);
    }

    private void RecordBlackboardTransitions(TeamBlackboard previous, TeamBlackboard current)
    {
        if (previous.HasBeenComputed
            && previous.FocusMarkId is { } previousMark
            && current.FocusMarkId is { } nextMark
            && previousMark != nextMark)
        {
            ActivityTelemetry.RecordFocusMarkSwitch();
        }

        foreach (var breacher in current.FrontlineBreachers)
        {
            if (!previous.IsBreacher(breacher))
            {
                ActivityTelemetry.RecordFrontlineBreach();
            }
        }
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
        var untilTick = StepIndex + BattleTickMath.DurationToTicks(duration);
        if (_groupDispersalLocks.TryGetValue(unit.Id.Value, out var existing)
            && existing.DispersedUntilTick >= untilTick)
        {
            return false;
        }

        _groupDispersalLocks[unit.Id.Value] = new GroupDispersalLockState(unit.Id.Value, center, untilTick, safeSeverity);
        unit.RequestReevaluation(ReevaluationReason.TargetMoved);
        return true;
    }

    public bool IsUnderGroupDispersalLock(UnitSnapshot unit)
    {
        return unit != null
               && _groupDispersalLocks.TryGetValue(unit.Id.Value, out var state)
               && state.DispersedUntilTick > StepIndex;
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
                _ownedEntityDespawnTimers[owned.Id.Value] = BattleTickMath.DurationToTicks(delay);
            }
        }
    }

    public void AdvanceOwnedEntityDespawns()
    {
        var keys = _ownedEntityDespawnTimers.Keys.ToList();
        foreach (var unitId in keys)
        {
            var remaining = _ownedEntityDespawnTimers[unitId] - 1;
            if (remaining > 0)
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
                     .Where(pair => pair.Value.DispersedUntilTick <= StepIndex)
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

    public void ResetStepMotions()
    {
        _stepMotions.Clear();
        _motionSequence = 0;
    }

    public void ResetStepCombatEvents()
    {
        _stepCombatEvents.Clear();
    }

    /// <summary>Allocates the next monotonic <see cref="ActionInstanceId"/>. Called exactly once per
    /// accepted BeginWindup transition (never on cancel/death/retarget), so ids are contiguous and
    /// reproduce identically across replay from the same seed (GPT Pro J9/J23).</summary>
    public ActionInstanceId AllocateActionInstanceId()
    {
        return new ActionInstanceId(_nextActionInstanceId++);
    }

    public void RecordCombatEvent(BattleCombatEventIntent intent)
    {
        _stepCombatEvents.Add(intent);
    }

    public void RecordMotion(
        EntityId actorId,
        BattleMotionKind kind,
        CombatVector2 from,
        CombatVector2 to,
        EntityId? sourceActorId = null,
        bool isDiscrete = false)
    {
        // StepIndex here is the in-progress (pre-AdvanceStep) value; BattleReadModelBuilder.BuildStep
        // re-stamps each motion to the containing step's StepIndex so motion.StepIndex == step.StepIndex.
        _stepMotions.Add(new BattleMotionIntent(
            StepIndex,
            _motionSequence++,
            actorId,
            kind,
            from,
            to,
            sourceActorId,
            isDiscrete));
    }

    public void AdvanceStep()
    {
        StepIndex++;
        EffectPositionSnapshot = null;
        EffectPositionSnapshotStep = -1;
        _allyTacticContext = null;
        _enemyTacticContext = null;
        _tacticContextStep = StepIndex;
    }
}
