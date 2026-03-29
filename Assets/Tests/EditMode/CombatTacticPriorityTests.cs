using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;

namespace SM.Tests.EditMode;

public sealed class CombatTacticPriorityTests
{
    [Test]
    public void HigherPriority_HealRule_Wins_Over_AttackRule()
    {
        var healSkill = new SkillDefinition("skill.heal", "Heal", SkillKind.Heal, 4f, 3f);
        var actor = CombatTestFactory.CreateUnit(
            "ally.mystic",
            classId: "mystic",
            anchor: DeploymentAnchorId.BackCenter,
            healPower: 4f,
            attackRange: 2.8f,
            tactics: new[]
            {
                new TacticRule(0, TacticConditionType.AllyHpBelow, 0.6f, BattleActionType.ActiveSkill, TargetSelectorType.LowestHpAlly, healSkill.Id),
                new TacticRule(1, TacticConditionType.EnemyInRange, 0f, BattleActionType.BasicAttack, TargetSelectorType.FirstEnemyInRange),
                new TacticRule(2, TacticConditionType.Fallback, 0f, BattleActionType.WaitDefend, TargetSelectorType.Self),
            },
            skills: new[] { healSkill });

        var ally2 = CombatTestFactory.CreateUnit("ally.vanguard", classId: "vanguard", anchor: DeploymentAnchorId.FrontCenter);
        var enemy = CombatTestFactory.CreateUnit("enemy.vanguard", race: "undead", classId: "vanguard", anchor: DeploymentAnchorId.FrontCenter);

        var state = CombatTestFactory.CreateBattleState(new[] { actor, ally2 }, new[] { enemy, enemy with { Id = "enemy.vanguard.2", PreferredAnchor = DeploymentAnchorId.BackCenter } });
        var lowestAlly = state.Allies[1];
        lowestAlly.TakeDamage(10f);

        var evaluated = TacticEvaluator.Evaluate(state, state.Allies[0]);

        Assert.That(evaluated.ActionType, Is.EqualTo(BattleActionType.ActiveSkill));
        Assert.That(evaluated.Target, Is.EqualTo(lowestAlly));
    }
}
