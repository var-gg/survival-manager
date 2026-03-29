using System.Collections.Generic;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Core.Ids;
using SM.Core.Stats;
using SM.Unity;

namespace SM.Tests.EditMode;

public sealed class BattleReplayBuilderTests
{
    [Test]
    public void Build_Adds_Intro_And_Result_When_Battle_Has_No_Events()
    {
        var state = CreateBattleState();
        var result = new BattleResult(TeamSide.Ally, 0, new List<BattleEvent>());

        var track = BattleReplayBuilder.Build(state, result);

        Assert.That(track.Frames.Count, Is.EqualTo(2));
        Assert.That(track.Frames[0].FrameKind, Is.EqualTo(BattleReplayFrameKind.Intro));
        Assert.That(track.Frames[^1].FrameKind, Is.EqualTo(BattleReplayFrameKind.Result));
        Assert.That(track.InitialRoster.Count, Is.EqualTo(3));
    }

    [Test]
    public void Build_Preserves_Damage_Heal_And_Defend_Frame_Data()
    {
        var state = CreateBattleState();
        var allyDamage = state.Allies[0];
        var allyHeal = state.Allies[1];
        var enemy = state.Enemies[0];
        var result = new BattleResult(
            TeamSide.Ally,
            3,
            new[]
            {
                new BattleEvent(0, allyDamage.Id, BattleActionType.BasicAttack, enemy.Id, 4f, "basic_attack"),
                new BattleEvent(1, allyHeal.Id, BattleActionType.ActiveSkill, allyDamage.Id, 3f, "heal_skill"),
                new BattleEvent(2, enemy.Id, BattleActionType.WaitDefend, enemy.Id, 0f, "wait_defend"),
            });

        var track = BattleReplayBuilder.Build(state, result);

        var damageFrame = track.Frames[1];
        Assert.That(damageFrame.ActionType, Is.EqualTo(BattleActionType.BasicAttack));
        Assert.That(damageFrame.BeforeTargetHealth, Is.EqualTo(18f));
        Assert.That(damageFrame.AfterTargetHealth, Is.EqualTo(14f));

        var healFrame = track.Frames[2];
        Assert.That(healFrame.ActionType, Is.EqualTo(BattleActionType.ActiveSkill));
        Assert.That(healFrame.Note, Is.EqualTo("heal_skill"));
        Assert.That(healFrame.BeforeTargetHealth, Is.EqualTo(20f));
        Assert.That(healFrame.AfterTargetHealth, Is.EqualTo(20f));

        var defendFrame = track.Frames[3];
        Assert.That(defendFrame.ActionType, Is.EqualTo(BattleActionType.WaitDefend));
        Assert.That(defendFrame.Value, Is.EqualTo(0f));
        Assert.That(defendFrame.BeforeSourceHealth, Is.EqualTo(defendFrame.AfterSourceHealth));
    }

    private static BattleState CreateBattleState()
    {
        var allyDamage = new UnitSnapshot(new EntityId("ally_damage"), TeamSide.Ally, CreateUnit("ally-damage", "Ally Damage", "human", "vanguard", RowPosition.Front, 20f, 6f, 2f, 4f, 1f));
        var allyHeal = new UnitSnapshot(new EntityId("ally_heal"), TeamSide.Ally, CreateUnit("ally-heal", "Ally Heal", "human", "mystic", RowPosition.Back, 16f, 3f, 1f, 3f, 5f));
        var enemy = new UnitSnapshot(new EntityId("enemy_front"), TeamSide.Enemy, CreateUnit("enemy-front", "Enemy Front", "undead", "vanguard", RowPosition.Front, 18f, 5f, 2f, 3f, 1f));
        return new BattleState(new[] { allyDamage, allyHeal }, new[] { enemy });
    }

    private static UnitDefinition CreateUnit(string id, string name, string raceId, string classId, RowPosition row, float hp, float atk, float def, float speed, float healPower)
    {
        return new UnitDefinition(
            id,
            name,
            raceId,
            classId,
            row,
            new Dictionary<StatKey, float>
            {
                [StatKey.MaxHealth] = hp,
                [StatKey.Attack] = atk,
                [StatKey.Defense] = def,
                [StatKey.Speed] = speed,
                [StatKey.HealPower] = healPower,
            },
            new TacticRule[0],
            new SkillDefinition[0]);
    }
}
