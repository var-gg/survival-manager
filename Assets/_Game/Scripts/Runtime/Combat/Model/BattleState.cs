using System.Collections.Generic;
using System.Linq;
using SM.Core.Ids;

namespace SM.Combat.Model;

public sealed class BattleState
{
    public BattleState(
        IReadOnlyList<UnitSnapshot> allies,
        IReadOnlyList<UnitSnapshot> enemies,
        TeamPostureType allyPosture,
        TeamPostureType enemyPosture,
        float fixedStepSeconds,
        int seed)
    {
        Allies = allies;
        Enemies = enemies;
        AllyPosture = allyPosture;
        EnemyPosture = enemyPosture;
        FixedStepSeconds = fixedStepSeconds;
        Seed = seed;
    }

    public IReadOnlyList<UnitSnapshot> Allies { get; }
    public IReadOnlyList<UnitSnapshot> Enemies { get; }
    public TeamPostureType AllyPosture { get; }
    public TeamPostureType EnemyPosture { get; }
    public float FixedStepSeconds { get; }
    public int Seed { get; }
    public int StepIndex { get; private set; }
    public float ElapsedSeconds { get; private set; }

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

    public void AdvanceStep()
    {
        StepIndex++;
        ElapsedSeconds += FixedStepSeconds;
    }
}
