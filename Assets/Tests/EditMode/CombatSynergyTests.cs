using System.Collections.Generic;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Stats;

namespace SM.Tests.EditMode;

public class CombatSynergyTests
{
    [Test]
    public void RaceAndClassThresholds_Apply_Modifiers()
    {
        var allyA = CreateUnit("ally.a", "human", "vanguard");
        var allyB = CreateUnit("ally.b", "human", "vanguard");
        var enemyA = CreateUnit("enemy.a", "undead", "duelist");
        var enemyB = CreateUnit("enemy.b", "beastkin", "ranger");

        var state = BattleFactory.Create(new[] { allyA, allyB }, new[] { enemyA, enemyB });

        Assert.That(state.Allies[0].Attack, Is.EqualTo(7f).Within(0.001f));
        Assert.That(state.Allies[0].Defense, Is.EqualTo(4f).Within(0.001f));
    }

    private static UnitDefinition CreateUnit(string id, string race, string @class)
    {
        return new UnitDefinition(
            id,
            id,
            race,
            @class,
            RowPosition.Front,
            new Dictionary<StatKey, float>
            {
                [StatKey.MaxHealth] = 20,
                [StatKey.Attack] = 5,
                [StatKey.Defense] = 2,
                [StatKey.Speed] = 3,
            },
            new[] { new TacticRule(0, TacticConditionType.Fallback, 0f, BattleActionType.BasicAttack, TargetSelectorType.FirstEnemyInRange) },
            new SkillDefinition[0]);
    }
}
