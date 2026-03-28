using System.Collections.Generic;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Stats;

namespace SM.Tests.EditMode;

public class BattleResolutionTests
{
    [Test]
    public void BattleRun_Ends_With_A_Winner_And_Events()
    {
        var ally = CreateUnit("ally_1", 24, 7, 2, 5);
        var enemy = CreateUnit("enemy_1", 20, 5, 1, 3);

        var state = BattleFactory.Create(new[] { ally, ally with { Id = "ally_2" } }, new[] { enemy, enemy with { Id = "enemy_2" } });
        var result = BattleResolver.Run(state, 20);

        Assert.That(result.Events.Count, Is.GreaterThan(0));
        Assert.That(result.Winner == TeamSide.Ally || result.Winner == TeamSide.Enemy, Is.True);
    }

    private static UnitDefinition CreateUnit(string id, float hp, float atk, float def, float speed)
    {
        return new UnitDefinition(
            id,
            id,
            "human",
            "vanguard",
            RowPosition.Front,
            new Dictionary<StatKey, float>
            {
                [StatKey.MaxHealth] = hp,
                [StatKey.Attack] = atk,
                [StatKey.Defense] = def,
                [StatKey.Speed] = speed,
            },
            new[]
            {
                new TacticRule(0, TacticConditionType.EnemyInRange, 0f, BattleActionType.BasicAttack, TargetSelectorType.FirstEnemyInRange),
                new TacticRule(1, TacticConditionType.Fallback, 0f, BattleActionType.WaitDefend, TargetSelectorType.Self),
            },
            new SkillDefinition[0]);
    }
}
