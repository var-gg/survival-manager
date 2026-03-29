using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;

namespace SM.Tests.EditMode;

public sealed class BattleResolutionTests
{
    [Test]
    public void BattleRun_Ends_With_A_Winner_And_Events()
    {
        var ally = CombatTestFactory.CreateUnit("ally_1", attack: 7f, speed: 5f, anchor: DeploymentAnchorId.FrontCenter);
        var enemy = CombatTestFactory.CreateUnit("enemy_1", race: "undead", classId: "duelist", hp: 18f, attack: 5f, speed: 3f, anchor: DeploymentAnchorId.FrontCenter);

        var state = CombatTestFactory.CreateBattleState(
            new[] { ally, ally with { Id = "ally_2", PreferredAnchor = DeploymentAnchorId.BackCenter } },
            new[] { enemy, enemy with { Id = "enemy_2", PreferredAnchor = DeploymentAnchorId.BackCenter } });

        var result = BattleResolver.Run(state, 120);

        Assert.That(result.Events.Count, Is.GreaterThan(0));
        Assert.That(result.Winner == TeamSide.Ally || result.Winner == TeamSide.Enemy, Is.True);
        Assert.That(result.FinalUnits.Count, Is.EqualTo(4));
    }

    [Test]
    public void SameSeed_Produces_Identical_Battle_Result()
    {
        var allies = new[]
        {
            CombatTestFactory.CreateUnit("ally_tank", classId: "vanguard", anchor: DeploymentAnchorId.FrontCenter),
            CombatTestFactory.CreateUnit("ally_ranger", classId: "ranger", anchor: DeploymentAnchorId.BackCenter, attackRange: 3.2f, moveSpeed: 1.8f)
        };
        var enemies = new[]
        {
            CombatTestFactory.CreateUnit("enemy_tank", race: "undead", classId: "vanguard", anchor: DeploymentAnchorId.FrontCenter),
            CombatTestFactory.CreateUnit("enemy_ranger", race: "beastkin", classId: "ranger", anchor: DeploymentAnchorId.BackCenter, attackRange: 3.2f, moveSpeed: 1.8f)
        };

        var resultA = BattleResolver.Run(CombatTestFactory.CreateBattleState(allies, enemies, seed: 19), 160);
        var resultB = BattleResolver.Run(CombatTestFactory.CreateBattleState(allies, enemies, seed: 19), 160);

        Assert.That(resultA.Winner, Is.EqualTo(resultB.Winner));
        Assert.That(resultA.Events.Select(evt => $"{evt.StepIndex}:{evt.ActorId.Value}:{evt.TargetId?.Value}:{evt.Note}:{evt.Value:0.0}"), Is.EqualTo(resultB.Events.Select(evt => $"{evt.StepIndex}:{evt.ActorId.Value}:{evt.TargetId?.Value}:{evt.Note}:{evt.Value:0.0}")));
        Assert.That(resultA.FinalUnits.Select(unit => $"{unit.Id}:{unit.CurrentHealth:0.0}:{unit.Position.X:0.00}:{unit.Position.Y:0.00}"), Is.EqualTo(resultB.FinalUnits.Select(unit => $"{unit.Id}:{unit.CurrentHealth:0.0}:{unit.Position.X:0.00}:{unit.Position.Y:0.00}")));
    }

    [Test]
    public void Anchor_Assignment_Changes_First_Contact_Timing()
    {
        var frontState = CombatTestFactory.CreateBattleState(
            new[] { CombatTestFactory.CreateUnit("ally_melee_front", anchor: DeploymentAnchorId.FrontCenter, moveSpeed: 1.8f) },
            new[] { CombatTestFactory.CreateUnit("enemy_melee", race: "undead", anchor: DeploymentAnchorId.FrontCenter, moveSpeed: 1.8f) });
        var backState = CombatTestFactory.CreateBattleState(
            new[] { CombatTestFactory.CreateUnit("ally_melee_back", anchor: DeploymentAnchorId.BackCenter, moveSpeed: 1.8f) },
            new[] { CombatTestFactory.CreateUnit("enemy_melee", race: "undead", anchor: DeploymentAnchorId.FrontCenter, moveSpeed: 1.8f) });

        var frontResult = BattleResolver.Run(frontState, 160);
        var backResult = BattleResolver.Run(backState, 160);

        var firstFrontAttackStep = frontResult.Events.First(evt => evt.ActionType == BattleActionType.BasicAttack).StepIndex;
        var firstBackAttackStep = backResult.Events.First(evt => evt.ActionType == BattleActionType.BasicAttack).StepIndex;

        Assert.That(firstBackAttackStep, Is.GreaterThan(firstFrontAttackStep));
    }
}
