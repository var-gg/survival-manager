using NUnit.Framework;
using SM.Combat.Model;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class CombatSynergyTests
{
    [Test]
    public void RaceAndClassThresholds_Apply_Modifiers()
    {
        var allyA = CombatTestFactory.CreateUnit("ally.a", race: "human", classId: "vanguard", anchor: DeploymentAnchorId.FrontTop);
        var allyB = CombatTestFactory.CreateUnit("ally.b", race: "human", classId: "vanguard", anchor: DeploymentAnchorId.FrontBottom);
        var enemyA = CombatTestFactory.CreateUnit("enemy.a", race: "undead", classId: "duelist", anchor: DeploymentAnchorId.FrontTop);
        var enemyB = CombatTestFactory.CreateUnit("enemy.b", race: "beastkin", classId: "ranger", anchor: DeploymentAnchorId.BackTop);

        var state = CombatTestFactory.CreateBattleState(new[] { allyA, allyB }, new[] { enemyA, enemyB });

        Assert.That(state.Allies[0].Attack, Is.EqualTo(7f).Within(0.001f));
        Assert.That(state.Allies[0].Defense, Is.EqualTo(4f).Within(0.001f));
    }
}
