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

    public BattleState(
        IReadOnlyList<UnitSnapshot> allies,
        IReadOnlyList<UnitSnapshot> enemies,
        TeamPostureType allyPosture,
        TeamPostureType enemyPosture,
        float fixedStepSeconds,
        int seed,
        TelemetryContext? telemetryContext = null)
    {
        Allies = allies;
        Enemies = enemies;
        AllyPosture = allyPosture;
        EnemyPosture = enemyPosture;
        FixedStepSeconds = fixedStepSeconds;
        Seed = seed;
        TelemetryContext = telemetryContext;
    }

    public IReadOnlyList<UnitSnapshot> Allies { get; }
    public IReadOnlyList<UnitSnapshot> Enemies { get; }
    public TeamPostureType AllyPosture { get; }
    public TeamPostureType EnemyPosture { get; }
    public float FixedStepSeconds { get; }
    public int Seed { get; }
    public TelemetryContext? TelemetryContext { get; }
    public int StepIndex { get; private set; }
    public float ElapsedSeconds { get; private set; }
    private readonly List<TelemetryEventRecord> _telemetryEvents = new();
    public IReadOnlyList<TelemetryEventRecord> TelemetryEvents => _telemetryEvents;

    public IEnumerable<UnitSnapshot> AllUnits => Allies.Concat(Enemies);
    public IEnumerable<UnitSnapshot> LivingAllies => Allies.Where(x => x.IsAlive);
    public IEnumerable<UnitSnapshot> LivingEnemies => Enemies.Where(x => x.IsAlive);

    public IEnumerable<UnitSnapshot> GetTeam(TeamSide side) => side == TeamSide.Ally ? Allies : Enemies;
    public IEnumerable<UnitSnapshot> GetOpponents(TeamSide side) => side == TeamSide.Ally ? Enemies : Allies;
    public TeamPostureType GetPosture(TeamSide side) => side == TeamSide.Ally ? AllyPosture : EnemyPosture;

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
    }
}
