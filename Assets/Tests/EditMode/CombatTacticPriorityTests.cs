using System.Collections.Generic;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Stats;

namespace SM.Tests.EditMode;

public class CombatTacticPriorityTests
{
    [Test]
    public void HigherPriority_HealRule_Wins_Over_AttackRule()
    {
        var healSkill = new SkillDefinition("skill.heal", "Heal", SkillKind.Heal, 4f, 2);
        var actor = new UnitDefinition(
            "ally.mystic",
            "Mystic",
            "human",
            "mystic",
            RowPosition.Back,
            new Dictionary<StatKey, float>
            {
                [StatKey.MaxHealth] = 20,
                [StatKey.Attack] = 3,
                [StatKey.Defense] = 1,
                [StatKey.Speed] = 5,
                [StatKey.HealPower] = 4,
            },
            new[]
            {
                new TacticRule(0, TacticConditionType.AllyHpBelow, 0.6f, BattleActionType.ActiveSkill, TargetSelectorType.LowestHpAlly, healSkill.Id),
                new TacticRule(1, TacticConditionType.EnemyInRange, 0f, BattleActionType.BasicAttack, TargetSelectorType.FirstEnemyInRange),
                new TacticRule(2, TacticConditionType.Fallback, 0f, BattleActionType.WaitDefend, TargetSelectorType.Self),
            },
            new[] { healSkill });

        var ally2 = new UnitDefinition(
            "ally.vanguard",
            "Vanguard",
            "human",
            "vanguard",
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

        var enemy = new UnitDefinition(
            "enemy.vanguard",
            "Enemy",
            "undead",
            "vanguard",
            RowPosition.Front,
            new Dictionary<StatKey, float>
            {
                [StatKey.MaxHealth] = 20,
                [StatKey.Attack] = 5,
                [StatKey.Defense] = 2,
                [StatKey.Speed] = 1,
            },
            new[] { new TacticRule(0, TacticConditionType.Fallback, 0f, BattleActionType.BasicAttack, TargetSelectorType.FirstEnemyInRange) },
            new SkillDefinition[0]);

        var state = BattleFactory.Create(new[] { actor, ally2 }, new[] { enemy, enemy with { Id = "enemy.vanguard.2" } });
        var lowestAlly = state.Allies[1];
        lowestAlly.TakeDamage(10f);

        var evaluated = TacticEvaluator.Evaluate(state, state.Allies[0]);

        Assert.That(evaluated.ActionType, Is.EqualTo(BattleActionType.ActiveSkill));
        Assert.That(evaluated.Target, Is.EqualTo(lowestAlly));
    }
}
