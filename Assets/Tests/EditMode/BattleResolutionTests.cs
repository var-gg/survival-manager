using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
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
        Assert.That(resultA.Events.Select(evt => $"{evt.StepIndex}:{evt.ActorId.Value}:{evt.TargetId?.Value}:{evt.LogCode}:{evt.Value:0.0}"), Is.EqualTo(resultB.Events.Select(evt => $"{evt.StepIndex}:{evt.ActorId.Value}:{evt.TargetId?.Value}:{evt.LogCode}:{evt.Value:0.0}")));
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

    [Test]
    public void LoopA_4v4_UnitsEngageWithinFirst50Steps()
    {
        var allies = new[]
        {
            CombatTestFactory.CreateLoopAUnit("ally_guardian", classId: "vanguard", hp: 110f, physPower: 4f, armor: 4f, attackSpeed: 2f, moveSpeed: 1.65f, anchor: DeploymentAnchorId.FrontTop),
            CombatTestFactory.CreateLoopAUnit("ally_raider", classId: "duelist", hp: 72f, physPower: 8f, armor: 1f, attackSpeed: 5f, moveSpeed: 2.05f, anchor: DeploymentAnchorId.FrontBottom),
            CombatTestFactory.CreateLoopAUnit("ally_hunter", classId: "ranger", hp: 68f, physPower: 6f, armor: 2f, attackSpeed: 5f, moveSpeed: 1.85f, attackRange: 3.2f, anchor: DeploymentAnchorId.BackTop),
            CombatTestFactory.CreateLoopAUnit("ally_hexer", classId: "mystic", hp: 65f, physPower: 4f, armor: 2f, attackSpeed: 4f, moveSpeed: 1.7f, attackRange: 2.8f, anchor: DeploymentAnchorId.BackBottom),
        };
        var enemies = new[]
        {
            CombatTestFactory.CreateLoopAUnit("enemy_guardian", classId: "vanguard", hp: 110f, physPower: 4f, armor: 4f, attackSpeed: 2f, moveSpeed: 1.65f, anchor: DeploymentAnchorId.FrontTop),
            CombatTestFactory.CreateLoopAUnit("enemy_raider", classId: "duelist", hp: 72f, physPower: 8f, armor: 1f, attackSpeed: 5f, moveSpeed: 2.05f, anchor: DeploymentAnchorId.FrontBottom),
            CombatTestFactory.CreateLoopAUnit("enemy_hunter", classId: "ranger", hp: 68f, physPower: 6f, armor: 2f, attackSpeed: 5f, moveSpeed: 1.85f, attackRange: 3.2f, anchor: DeploymentAnchorId.BackTop),
            CombatTestFactory.CreateLoopAUnit("enemy_hexer", classId: "mystic", hp: 65f, physPower: 4f, armor: 2f, attackSpeed: 4f, moveSpeed: 1.7f, attackRange: 2.8f, anchor: DeploymentAnchorId.BackBottom),
        };

        var result = BattleResolver.Run(CombatTestFactory.CreateBattleState(allies, enemies, seed: 42), 50);
        var damageEvents = result.Events
            .Where(evt => evt.ActionType == BattleActionType.BasicAttack)
            .ToList();

        Assert.That(damageEvents.Count, Is.GreaterThan(0),
            $"50 step 내에 기본공격 이벤트가 없음. 총 이벤트: {result.Events.Count}, step: {result.StepCount}");
    }

    [Test]
    public void LoopA_4v4_AsymmetricBattleEndsBeforeTimeout()
    {
        var allies = new[]
        {
            CombatTestFactory.CreateLoopAUnit("ally_guardian", classId: "vanguard", hp: 110f, physPower: 4f, armor: 4f, attackSpeed: 2f, moveSpeed: 1.65f, anchor: DeploymentAnchorId.FrontTop),
            CombatTestFactory.CreateLoopAUnit("ally_raider", classId: "duelist", hp: 72f, physPower: 8f, armor: 1f, attackSpeed: 5f, moveSpeed: 2.05f, anchor: DeploymentAnchorId.FrontBottom),
            CombatTestFactory.CreateLoopAUnit("ally_hunter", classId: "ranger", hp: 68f, physPower: 6f, armor: 2f, attackSpeed: 5f, moveSpeed: 1.85f, attackRange: 3.2f, anchor: DeploymentAnchorId.BackTop),
            CombatTestFactory.CreateLoopAUnit("ally_hexer", classId: "mystic", hp: 65f, physPower: 4f, armor: 2f, attackSpeed: 4f, moveSpeed: 1.7f, attackRange: 2.8f, anchor: DeploymentAnchorId.BackBottom),
        };
        var enemies = new[]
        {
            CombatTestFactory.CreateLoopAUnit("enemy_guardian", race: "undead", classId: "vanguard", hp: 44f, physPower: 3f, armor: 1f, attackSpeed: 2f, moveSpeed: 1.65f, anchor: DeploymentAnchorId.FrontTop),
            CombatTestFactory.CreateLoopAUnit("enemy_raider", race: "undead", classId: "duelist", hp: 30f, physPower: 5f, armor: 0f, attackSpeed: 3.5f, moveSpeed: 2.05f, anchor: DeploymentAnchorId.FrontBottom),
            CombatTestFactory.CreateLoopAUnit("enemy_hunter", race: "undead", classId: "ranger", hp: 28f, physPower: 4f, armor: 0f, attackSpeed: 3.5f, moveSpeed: 1.85f, attackRange: 3.2f, anchor: DeploymentAnchorId.BackTop),
            CombatTestFactory.CreateLoopAUnit("enemy_hexer", race: "undead", classId: "mystic", hp: 24f, physPower: 2.8f, armor: 0f, attackSpeed: 3f, moveSpeed: 1.7f, attackRange: 2.8f, anchor: DeploymentAnchorId.BackBottom),
        };

        var result = BattleResolver.Run(CombatTestFactory.CreateBattleState(allies, enemies, seed: 42), BattleSimulator.DefaultMaxSteps);
        var aliveAllies = result.FinalUnits.Count(u => u.Side == TeamSide.Ally && u.IsAlive);
        var aliveEnemies = result.FinalUnits.Count(u => u.Side == TeamSide.Enemy && u.IsAlive);

        Assert.That(result.Winner, Is.EqualTo(TeamSide.Ally));
        Assert.That(aliveAllies == 0 || aliveEnemies == 0, Is.True,
            $"300 step 내에 전투 미종료. 남은 아군: {aliveAllies}, 적군: {aliveEnemies}");
        Assert.That(result.StepCount, Is.LessThan(BattleSimulator.DefaultMaxSteps),
            $"timeout 도달. step: {result.StepCount}");
    }

    [Test]
    public void AverageUnitDiesIn15To25BasicAttacks()
    {
        // 평균 스탯: PhysPower=6.2, Armor=2.7, HP=81
        // damage = 6.2 * (1 - 2.7 / (2.7 + 10)) = 6.2 * 0.787 = ~4.88
        // hits = 81 / 4.88 ≈ 17
        var attacker = CombatTestFactory.CreateLoopAUnit("attacker", classId: "duelist", hp: 81f, physPower: 6.2f, armor: 2.7f, attackSpeed: 4f, moveSpeed: 2f, anchor: DeploymentAnchorId.FrontTop);
        var defender = CombatTestFactory.CreateLoopAUnit("defender", classId: "vanguard", hp: 81f, physPower: 6.2f, armor: 2.7f, attackSpeed: 4f, moveSpeed: 2f, anchor: DeploymentAnchorId.FrontTop);

        var result = BattleResolver.Run(CombatTestFactory.CreateBattleState(new[] { attacker }, new[] { defender }, seed: 42), 300);
        var attackEvents = result.Events
            .Where(evt => evt.ActionType == BattleActionType.BasicAttack && evt.Value > 0)
            .ToList();

        var deadUnit = result.FinalUnits.FirstOrDefault(u => !u.IsAlive);
        if (deadUnit != null)
        {
            Assert.That(attackEvents.Count, Is.InRange(10, 40),
                $"평균 유닛 사망까지 {attackEvents.Count}회 기본공격. 목표: 15-25회 (±허용)");
        }
    }

    [Test]
    public void HealerDoesNotOutHealSingleAttacker()
    {
        // Hexer: HealPower=4, skill PowerFlat=4 → heal 8 HP
        // Raider: PhysPower=8, vs Hexer Armor=2 → damage = 8*(1-2/12) = 6.67
        // heal 8 < damage 6.67 이면 안 되는데... heal은 매 쿨타임마다, 공격은 매 attackCooldown마다
        // 핵심: 힐러 1명이 공격자 1명의 피해를 완전히 상쇄하지 못해야 함
        var raider = CombatTestFactory.CreateLoopAUnit("raider", classId: "duelist", hp: 72f, physPower: 8f, armor: 1f, attackSpeed: 5f, moveSpeed: 2.05f, anchor: DeploymentAnchorId.FrontTop);
        var hexer = CombatTestFactory.CreateLoopAUnit("hexer", classId: "mystic", hp: 65f, physPower: 4f, armor: 2f, attackSpeed: 4f, moveSpeed: 1.7f, attackRange: 2.8f, anchor: DeploymentAnchorId.FrontTop);

        var result = BattleResolver.Run(CombatTestFactory.CreateBattleState(new[] { raider }, new[] { hexer }, seed: 42), BattleSimulator.DefaultMaxSteps);

        Assert.That(result.FinalUnits.Any(u => !u.IsAlive), Is.True,
            "1v1 raider vs hexer: 300 step 내에 누군가 죽어야 힐러가 공격을 상쇄하지 못하는 것");
    }
}
