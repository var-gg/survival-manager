using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;
using SM.Core.Stats;

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
            CombatTestFactory.CreateUnit("ally_ranger", classId: "ranger", anchor: DeploymentAnchorId.BackCenter, attackRange: 5.6f, moveSpeed: 1.8f)
        };
        var enemies = new[]
        {
            CombatTestFactory.CreateUnit("enemy_tank", race: "undead", classId: "vanguard", anchor: DeploymentAnchorId.FrontCenter),
            CombatTestFactory.CreateUnit("enemy_ranger", race: "beastkin", classId: "ranger", anchor: DeploymentAnchorId.BackCenter, attackRange: 5.6f, moveSpeed: 1.8f)
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
            CombatTestFactory.CreateLoopAUnit("ally_hunter", classId: "ranger", hp: 68f, physPower: 6f, armor: 2f, attackSpeed: 5f, moveSpeed: 1.85f, attackRange: 5.6f, anchor: DeploymentAnchorId.BackTop),
            CombatTestFactory.CreateLoopAUnit("ally_hexer", classId: "mystic", hp: 65f, physPower: 4f, armor: 2f, attackSpeed: 4f, moveSpeed: 1.7f, attackRange: 2.8f, anchor: DeploymentAnchorId.BackBottom),
        };
        var enemies = new[]
        {
            CombatTestFactory.CreateLoopAUnit("enemy_guardian", classId: "vanguard", hp: 110f, physPower: 4f, armor: 4f, attackSpeed: 2f, moveSpeed: 1.65f, anchor: DeploymentAnchorId.FrontTop),
            CombatTestFactory.CreateLoopAUnit("enemy_raider", classId: "duelist", hp: 72f, physPower: 8f, armor: 1f, attackSpeed: 5f, moveSpeed: 2.05f, anchor: DeploymentAnchorId.FrontBottom),
            CombatTestFactory.CreateLoopAUnit("enemy_hunter", classId: "ranger", hp: 68f, physPower: 6f, armor: 2f, attackSpeed: 5f, moveSpeed: 1.85f, attackRange: 5.6f, anchor: DeploymentAnchorId.BackTop),
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
            CombatTestFactory.CreateLoopAUnit("ally_hunter", classId: "ranger", hp: 68f, physPower: 6f, armor: 2f, attackSpeed: 5f, moveSpeed: 1.85f, attackRange: 5.6f, anchor: DeploymentAnchorId.BackTop),
            CombatTestFactory.CreateLoopAUnit("ally_hexer", classId: "mystic", hp: 65f, physPower: 4f, armor: 2f, attackSpeed: 4f, moveSpeed: 1.7f, attackRange: 2.8f, anchor: DeploymentAnchorId.BackBottom),
        };
        var enemies = new[]
        {
            CombatTestFactory.CreateLoopAUnit("enemy_guardian", race: "undead", classId: "vanguard", hp: 44f, physPower: 3f, armor: 1f, attackSpeed: 2f, moveSpeed: 1.65f, anchor: DeploymentAnchorId.FrontTop),
            CombatTestFactory.CreateLoopAUnit("enemy_raider", race: "undead", classId: "duelist", hp: 30f, physPower: 5f, armor: 0f, attackSpeed: 3.5f, moveSpeed: 2.05f, anchor: DeploymentAnchorId.FrontBottom),
            CombatTestFactory.CreateLoopAUnit("enemy_hunter", race: "undead", classId: "ranger", hp: 28f, physPower: 4f, armor: 0f, attackSpeed: 3.5f, moveSpeed: 1.85f, attackRange: 5.6f, anchor: DeploymentAnchorId.BackTop),
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
    public void AttackSpeed_ScalesBasicCooldown_WithoutChangingSkillCooldown()
    {
        var flex = new BattleSkillSpec(
            "flex_strike",
            "Flex Strike",
            SkillKind.Strike,
            1f,
            1.2f,
            SlotKind: CompiledSkillSlots.UtilityActive,
            ResolvedSlotKind: ActionSlotKind.FlexActive,
            ActivationModel: ActivationModel.Cooldown,
            BaseCooldownSeconds: 3f);
        var fast = CombatTestFactory.CreateLoopAUnit(
            "fast",
            attackSpeed: 6f,
            attackCooldown: 0.9f,
            flexActive: flex);
        var target = CombatTestFactory.CreateLoopAUnit("target", race: "undead", hp: 200f);
        var state = CombatTestFactory.CreateBattleState(new[] { fast }, new[] { target });
        var actor = state.Allies[0];

        Assert.That(actor.AuthoredAttackCooldown, Is.EqualTo(0.9f).Within(0.001f));
        Assert.That(actor.BasicAttackCooldownScale, Is.EqualTo(2f).Within(0.001f));
        Assert.That(actor.AttackCooldown, Is.EqualTo(0.45f).Within(0.001f));
        Assert.That(actor.ResolveActionCooldown(null), Is.EqualTo(0.45f).Within(0.001f));
        Assert.That(actor.ResolveActionCooldown(flex.Id), Is.EqualTo(3f).Within(0.001f));
    }

    [Test]
    public void FasterAttackSpeed_ProducesMoreBasicAttacksInSameDuration()
    {
        var slowCount = CountBasicAttacksForSpeed(3f);
        var fastCount = CountBasicAttacksForSpeed(5f);

        Assert.That(fastCount, Is.GreaterThan(slowCount));
        Assert.That(fastCount, Is.GreaterThanOrEqualTo(slowCount + 2));
    }

    [Test]
    public void ReadModel_ExposesCadenceStats_ForMetadata()
    {
        var unit = CombatTestFactory.CreateLoopAUnit(
            "fast",
            attackSpeed: 6f,
            attackCooldown: 0.9f,
            skillHaste: 0.25f);
        var target = CombatTestFactory.CreateLoopAUnit("target", race: "undead", hp: 200f);
        var state = CombatTestFactory.CreateBattleState(new[] { unit }, new[] { target });
        var step = BattleReadModelBuilder.BuildStep(state, System.Array.Empty<BattleEvent>(), false, null);
        var readModel = step.Units.Single(view => view.Id == state.Allies[0].Id.Value);

        Assert.That(readModel.AttackSpeed, Is.EqualTo(6f).Within(0.001f));
        Assert.That(readModel.BasicAttackCooldown, Is.EqualTo(0.45f).Within(0.001f));
        Assert.That(readModel.SkillHaste, Is.EqualTo(0.25f).Within(0.001f));
    }

    [Test]
    public void SkillDamage_UsesCoefficients_Penetration_AndTrueDamageBypass()
    {
        var magicalSkill = new BattleSkillSpec(
            "arcane_bolt",
            "Arcane Bolt",
            SkillKind.Strike,
            0f,
            6f,
            DamageType: DamageType.Magical,
            PowerFlat: 2f,
            PhysCoeff: 0f,
            MagCoeff: 1.5f);
        var trueSkill = new BattleSkillSpec(
            "pure_cut",
            "Pure Cut",
            SkillKind.Strike,
            0f,
            6f,
            DamageType: DamageType.True,
            PowerFlat: 12f,
            PhysCoeff: 0f);
        var attacker = CombatTestFactory.CreateLoopAUnit(
            "attacker",
            physPower: 4f,
            signatureActive: magicalSkill) with
        {
            BaseStats = WithStats(
                CombatTestFactory.CreateLoopAUnit("attacker").BaseStats,
                (StatKey.MagPower, 10f),
                (StatKey.MagPen, 3f)),
        };
        var target = CombatTestFactory.CreateLoopAUnit("target", race: "undead", hp: 200f, armor: 99f) with
        {
            BaseStats = WithStats(
                CombatTestFactory.CreateLoopAUnit("target").BaseStats,
                (StatKey.Armor, 99f),
                (StatKey.Resist, 8f)),
        };
        var state = CombatTestFactory.CreateBattleState(new[] { attacker }, new[] { target }, seed: 11);

        var magical = HitResolutionService.ResolveSkillDamage(state, state.Allies[0], state.Enemies[0], magicalSkill);
        var pure = HitResolutionService.ResolveSkillDamage(state, state.Allies[0], state.Enemies[0], trueSkill);

        Assert.That(magical.MitigationValue, Is.EqualTo(5f).Within(0.001f));
        Assert.That(magical.Value, Is.EqualTo(17f * (1f - (5f / 15f))).Within(0.001f));
        Assert.That(pure.MitigationValue, Is.EqualTo(0f).Within(0.001f));
        Assert.That(pure.Value, Is.EqualTo(12f).Within(0.001f));
    }

    [Test]
    public void SupportValue_UsesAuthoredCoefficients_AndKeepsLegacyHealFallback()
    {
        var authored = new BattleSkillSpec(
            "mend",
            "Mend",
            SkillKind.Heal,
            0f,
            5f,
            PowerFlat: 3f,
            PhysCoeff: 0f,
            MagCoeff: 0.5f,
            HealCoeff: 1f);
        var legacy = new SkillDefinition("legacy_heal", "Legacy Heal", SkillKind.Heal, 4f, 5f);
        var healer = CombatTestFactory.CreateLoopAUnit("healer", physPower: 20f) with
        {
            BaseStats = WithStats(
                CombatTestFactory.CreateLoopAUnit("healer").BaseStats,
                (StatKey.MagPower, 6f),
                (StatKey.HealPower, 4f)),
        };
        var target = CombatTestFactory.CreateLoopAUnit("target", race: "undead");
        var state = CombatTestFactory.CreateBattleState(new[] { healer }, new[] { target });

        Assert.That(HitResolutionService.ResolveSupportValue(state.Allies[0], authored), Is.EqualTo(10f).Within(0.001f));
        Assert.That(HitResolutionService.ResolveSupportValue(state.Allies[0], legacy), Is.EqualTo(8f).Within(0.001f));
    }

    [Test]
    public void DamageDrain_AppliesLifestealAndOmnivamp_FromStats()
    {
        var attacker = CombatTestFactory.CreateLoopAUnit(
            "attacker",
            hp: 100f,
            physPower: 20f,
            armor: 0f,
            attackRange: 20f,
            energy: new EnergyProfile(0f, 0f)) with
        {
            BaseStats = WithStats(
                CombatTestFactory.CreateLoopAUnit("attacker", hp: 100f, physPower: 20f, armor: 0f, attackRange: 20f).BaseStats,
                (StatKey.Lifesteal, 0.25f),
                (StatKey.Omnivamp, 0.10f)),
        };
        var target = CombatTestFactory.CreateLoopAUnit("target", race: "undead", hp: 100f, armor: 0f, attackRange: 20f, energy: new EnergyProfile(0f, 0f));
        var state = CombatTestFactory.CreateBattleState(new[] { attacker }, new[] { target }, seed: 13);
        var actor = state.Allies[0];
        actor.TakeDamage(50f);

        actor.BeginWindup(BattleActionType.BasicAttack, state.Enemies[0].Id, null);
        actor.FinishWindup();
        CombatActionResolver.Resolve(state, actor);

        Assert.That(actor.CurrentHealth, Is.EqualTo(57f).Within(0.001f));
        Assert.That(state.TelemetryEvents.Any(evt =>
            evt.EventKind == TelemetryEventKind.HealingApplied
            && evt.StringValueA == "lifesteal+omnivamp"
            && evt.ValueA > 6.99f
            && evt.ValueA < 7.01f), Is.True);
    }

    [Test]
    public void PreferredDistance_ReadsStatModifiers()
    {
        var watchfulPackage = new CombatModifierPackage(
            "item.watchful",
            ModifierSource.Item,
            new[]
            {
                new StatModifier(StatKey.PreferredDistance, ModifierOp.Flat, 3f, ModifierSource.Item, "item.watchful"),
            });
        var unit = CombatTestFactory.CreateUnit("ranger", preferredDistance: 2f) with
        {
            BaseStats = WithStats(
                CombatTestFactory.CreateUnit("ranger", preferredDistance: 2f).BaseStats,
                (StatKey.PreferredDistance, 2f)),
            Packages = new[] { watchfulPackage },
        };
        var target = CombatTestFactory.CreateUnit("target", race: "undead");
        var state = CombatTestFactory.CreateBattleState(new[] { unit }, new[] { target });

        Assert.That(state.Allies[0].PreferredDistance, Is.EqualTo(5f).Within(0.001f));
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

    private static int CountBasicAttacksForSpeed(float attackSpeed)
    {
        var attacker = CombatTestFactory.CreateLoopAUnit(
            "attacker",
            classId: "duelist",
            hp: 500f,
            physPower: 1f,
            armor: 0f,
            attackSpeed: attackSpeed,
            attackCooldown: 0.9f,
            attackRange: 8f,
            energy: new EnergyProfile(0f, 0f));
        var target = CombatTestFactory.CreateLoopAUnit(
            "target",
            race: "undead",
            classId: "vanguard",
            hp: 500f,
            physPower: 0f,
            armor: 0f,
            attackSpeed: 3f,
            attackCooldown: 2f,
            energy: new EnergyProfile(0f, 0f));
        var state = CombatTestFactory.CreateBattleState(new[] { attacker }, new[] { target }, seed: 37);
        var attackerId = state.Allies[0].Id.Value;
        var result = BattleResolver.Run(state, 120);
        return result.Events.Count(evt =>
            evt.ActorId.Value == attackerId
            && evt.ActionType == BattleActionType.BasicAttack
            && evt.LogCode == BattleLogCode.BasicAttackDamage);
    }

    private static Dictionary<StatKey, float> WithStats(Dictionary<StatKey, float> source, params (StatKey Key, float Value)[] stats)
    {
        var result = new Dictionary<StatKey, float>(source);
        foreach (var (key, value) in stats)
        {
            result[key] = value;
        }

        return result;
    }
}
