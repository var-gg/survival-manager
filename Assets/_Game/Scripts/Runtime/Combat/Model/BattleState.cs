using System.Collections.Generic;
using System.Linq;

namespace SM.Combat.Model;

public sealed class BattleState
{
    public BattleState(IReadOnlyList<UnitSnapshot> allies, IReadOnlyList<UnitSnapshot> enemies)
    {
        Allies = allies;
        Enemies = enemies;
    }

    public IReadOnlyList<UnitSnapshot> Allies { get; }
    public IReadOnlyList<UnitSnapshot> Enemies { get; }
    public int Tick { get; private set; }

    public IEnumerable<UnitSnapshot> AllUnits => Allies.Concat(Enemies);
    public IEnumerable<UnitSnapshot> LivingAllies => Allies.Where(x => x.IsAlive);
    public IEnumerable<UnitSnapshot> LivingEnemies => Enemies.Where(x => x.IsAlive);

    public IEnumerable<UnitSnapshot> GetTeam(TeamSide side) => side == TeamSide.Ally ? Allies : Enemies;
    public IEnumerable<UnitSnapshot> GetOpponents(TeamSide side) => side == TeamSide.Ally ? Enemies : Allies;

    public void AdvanceTick() => Tick++;
}
