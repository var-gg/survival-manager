using System;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
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

    [TestCase(TeamSide.Ally)]
    [TestCase(TeamSide.Enemy)]
    public void LoopA_AlliedHeal_ProducesPositiveTickHealthDelta_InRealBattle(TeamSide healerSide)
    {
        const string healerId = "healing_regression_healer";
        const string patientId = "healing_regression_patient";
        var state = CreateHealingBattle(healerSide, CreateHealSkill(TargetDomain.AlliedUnit), healerId, patientId);
        var patient = healerSide == TeamSide.Ally ? state.Allies[1] : state.Enemies[1];
        patient.TakeDamage(40f);

        var maxPositiveDelta = MeasureMaximumPositiveHealthDelta(state, patientId);

        Assert.That(maxPositiveDelta, Is.GreaterThan(0f),
            $"{healerSide} healer must increase the injured ally's real CurrentHealth between ticks.");
    }

    [TestCase(TeamSide.Ally)]
    [TestCase(TeamSide.Enemy)]
    public void LoopA_EnemyDomainHeal_NeverRestoresOpposingSideHealth(TeamSide healerSide)
    {
        const string healerId = "wrong_side_healer";
        const string opposingPatientId = "wrong_side_opponent";
        var healer = CombatTestFactory.CreateLoopAUnit(
            healerId,
            classId: "mystic",
            anchor: DeploymentAnchorId.BackCenter,
            hp: 100f,
            physPower: 2f,
            armor: 20f,
            attackSpeed: 5f,
            attackRange: 3f,
            flexActive: CreateHealSkill(TargetDomain.EnemyUnit));
        var opposingPatient = CombatTestFactory.CreateLoopAUnit(
            opposingPatientId,
            classId: "vanguard",
            hp: 100f,
            physPower: 0f,
            armor: 30f,
            attackSpeed: 0.1f,
            attackCooldown: 10f);
        var state = healerSide == TeamSide.Ally
            ? CombatTestFactory.CreateBattleState(new[] { healer }, new[] { opposingPatient }, seed: 19)
            : CombatTestFactory.CreateBattleState(new[] { opposingPatient }, new[] { healer }, seed: 19);
        var patient = healerSide == TeamSide.Ally ? state.Enemies[0] : state.Allies[0];
        patient.TakeDamage(40f);

        var maxPositiveDelta = MeasureMaximumPositiveHealthDelta(state, opposingPatientId);

        Assert.That(maxPositiveDelta, Is.EqualTo(0f),
            $"{healerSide} Heal authored toward enemies must never restore opposing-side CurrentHealth.");
    }

    [Test]
    public void LoopA_HealWithNoInjuredAlly_DoesNotLockOutBasicAttack()
    {
        const string healerId = "no_heal_lock_healer";
        const string enemyId = "no_heal_lock_enemy";
        var healer = CombatTestFactory.CreateLoopAUnit(
            healerId,
            classId: "mystic",
            anchor: DeploymentAnchorId.BackCenter,
            hp: 200f,
            physPower: 5f,
            armor: 30f,
            attackSpeed: 5f,
            attackRange: 3f,
            flexActive: CreateHealSkill(TargetDomain.AlliedUnit));
        var enemy = CombatTestFactory.CreateLoopAUnit(
            enemyId,
            race: "undead",
            hp: 2000f,
            physPower: 8f,
            armor: 40f,
            attackSpeed: 3f,
            attackCooldown: 0.7f);
        var state = CombatTestFactory.CreateBattleState(new[] { healer }, new[] { enemy }, seed: 23);
        state.Allies[0].TakeDamage(10f);
        var healerBasicAttacks = 0;
        var enemyDamageEvents = 0;

        BattleResolver.Run(state, 240, step =>
        {
            healerBasicAttacks += step.Events.Count(evt =>
                evt.ActorName == healerId && evt.LogCode == BattleLogCode.BasicAttackDamage);
            enemyDamageEvents += step.Events.Count(evt =>
                evt.ActorName == enemyId
                && evt.TargetName == healerId
                && evt.LogCode == BattleLogCode.BasicAttackDamage
                && evt.Value > 0f);
        });

        Assert.That(enemyDamageEvents, Is.GreaterThan(0),
            "The regression must exercise a healer that remains under real incoming damage.");
        Assert.That(healerBasicAttacks, Is.GreaterThan(0),
            "A damaged healer must still use the basic-attack ground state while healing only meaningful injury.");
        Assert.That(state.Enemies[0].CurrentHealth, Is.LessThan(state.Enemies[0].MaxHealth));
    }

    [Test]
    public void LoopA_HealTargetLock_NeverProducesSameSideBasicAttackDamage()
    {
        const string healerId = "same_side_lock_healer";
        const string patientId = "same_side_lock_patient";
        var state = CreateHealingBattle(TeamSide.Ally, CreateHealSkill(TargetDomain.AlliedUnit), healerId, patientId);
        var patient = state.Allies[1];
        patient.TakeDamage(40f);
        var previousPatientHealth = patient.CurrentHealth;
        var maxPositiveHealthDelta = 0f;
        var healerBasicAttacks = 0;
        var sameSideBasicAttacks = 0;

        BattleResolver.Run(state, 300, step =>
        {
            var patientHealth = step.Units.Single(unit => unit.Name == patientId).CurrentHealth;
            maxPositiveHealthDelta = Math.Max(maxPositiveHealthDelta, patientHealth - previousPatientHealth);
            previousPatientHealth = patientHealth;

            foreach (var evt in step.Events.Where(evt => evt.LogCode == BattleLogCode.BasicAttackDamage))
            {
                var source = state.FindUnit(evt.ActorId);
                var target = state.FindUnit(evt.TargetId);
                if (source != null && target != null && source.Side == target.Side)
                {
                    sameSideBasicAttacks++;
                }

                if (evt.ActorName == healerId)
                {
                    healerBasicAttacks++;
                }
            }
        });

        Assert.That(maxPositiveHealthDelta, Is.GreaterThan(0f),
            "The scenario must first park the healer's current target on an allied heal recipient.");
        Assert.That(healerBasicAttacks, Is.GreaterThan(0),
            "The healer must return to basic attacks after the meaningful-injury gate closes.");
        Assert.That(sameSideBasicAttacks, Is.EqualTo(0),
            "BasicAttackDamage must never resolve against a same-side target left in CurrentTargetId by healing.");
    }

    [Test]
    public void IntentTargetOverride_DoesNotReplaceAlliedEvaluatedTarget()
    {
        var state = CreateIntentOverrideBattle(out var actor, out var ally, out _, out var forcedEnemy);
        actor.SetCombatIntent(new CombatIntent(CombatIntentType.Dive, forcedEnemy.Id, null, default, 20, 10));
        var evaluated = CreateEvaluatedSupportAction(ally, CreateHealSkill(TargetDomain.AlliedUnit));

        var applied = TacticEvaluator.TryApplyIntentTargetOverride(state, actor, evaluated, out var retargeted);

        Assert.That(applied, Is.False);
        Assert.That(retargeted.Target, Is.SameAs(ally));
    }

    [TestCase(SkillKind.Heal)]
    [TestCase(SkillKind.Shield)]
    [TestCase(SkillKind.Buff)]
    public void IntentTargetOverride_DoesNotRedirectAlliedSupportKindToForcedEnemy(SkillKind kind)
    {
        var state = CreateIntentOverrideBattle(out var actor, out _, out var currentEnemy, out var forcedEnemy);
        actor.SetCombatIntent(new CombatIntent(CombatIntentType.Dive, forcedEnemy.Id, null, default, 20, 10));
        var supportSkill = CreateHealSkill(TargetDomain.AlliedUnit) with { Kind = kind };
        var evaluated = CreateEvaluatedSupportAction(currentEnemy, supportSkill);

        var applied = TacticEvaluator.TryApplyIntentTargetOverride(state, actor, evaluated, out var retargeted);

        Assert.That(applied, Is.False);
        Assert.That(retargeted.Target, Is.SameAs(currentEnemy));
    }

    [TestCase(SkillKind.Shield)]
    [TestCase(SkillKind.Buff)]
    public void LoopA_AlliedSupportKind_NeverEvaluatesEnemyTarget(SkillKind kind)
    {
        var supportSkill = CreateHealSkill(TargetDomain.AlliedUnit) with
        {
            Kind = kind,
            ResolvedSlotKind = ActionSlotKind.SignatureActive,
            ActivationModel = ActivationModel.Energy,
        };
        var actorLoadout = CombatTestFactory.CreateLoopAUnit(
            "allied_support_actor",
            classId: "mystic",
            signatureActive: supportSkill);
        var allyLoadout = CombatTestFactory.CreateLoopAUnit("allied_support_target", hp: 100f);
        var enemyLoadout = CombatTestFactory.CreateLoopAUnit("allied_support_enemy", race: "undead", hp: 100f);
        var state = CombatTestFactory.CreateBattleState(new[] { actorLoadout, allyLoadout }, new[] { enemyLoadout }, seed: 31);
        var actor = state.Allies[0];
        var enemy = state.Enemies[0];
        actor.SetCurrentTarget(enemy.Id);

        var evaluated = TacticEvaluator.Evaluate(state, actor);

        Assert.That(
            evaluated.ActionType == BattleActionType.ActiveSkill && evaluated.Target?.Side != actor.Side,
            Is.False,
            $"{kind} must never evaluate an enemy as its active-skill target.");
    }

    private static BattleState CreateHealingBattle(
        TeamSide healerSide,
        BattleSkillSpec healSkill,
        string healerId,
        string patientId)
    {
        var healer = CombatTestFactory.CreateLoopAUnit(
            healerId,
            classId: "mystic",
            anchor: DeploymentAnchorId.BackCenter,
            hp: 100f,
            physPower: 2f,
            armor: 20f,
            attackSpeed: 5f,
            attackRange: 3f,
            flexActive: healSkill);
        var patient = CombatTestFactory.CreateLoopAUnit(
            patientId,
            classId: "vanguard",
            anchor: DeploymentAnchorId.FrontCenter,
            hp: 100f,
            physPower: 2f,
            armor: 20f,
            attackSpeed: 1f);
        var opponent = CombatTestFactory.CreateLoopAUnit(
            "healing_regression_opponent",
            race: "undead",
            classId: "vanguard",
            hp: 400f,
            physPower: 0f,
            armor: 40f,
            attackSpeed: 0.1f,
            attackCooldown: 10f);

        return healerSide == TeamSide.Ally
            ? CombatTestFactory.CreateBattleState(new[] { healer, patient }, new[] { opponent }, seed: 17)
            : CombatTestFactory.CreateBattleState(new[] { opponent }, new[] { healer, patient }, seed: 17);
    }

    private static BattleSkillSpec CreateHealSkill(TargetDomain domain)
    {
        var isAllied = domain == TargetDomain.AlliedUnit;
        return new BattleSkillSpec(
            "skill_healing_regression",
            "Healing Regression",
            SkillKind.Heal,
            12f,
            6f,
            SlotKind: CompiledSkillSlots.UtilityActive,
            DamageType: DamageType.Healing,
            PowerFlat: 12f,
            PhysCoeff: 0f,
            MagCoeff: 0f,
            HealCoeff: 1f,
            BaseCooldownSeconds: 1.2f,
            CastWindupSeconds: 0.05f,
            ResolvedSlotKind: ActionSlotKind.FlexActive,
            ActivationModel: ActivationModel.Cooldown,
            Lane: ActionLane.Primary,
            LockRule: ActionLockRule.HardCommit,
            TargetRuleData: new TargetRule
            {
                Domain = domain,
                PrimarySelector = isAllied ? TargetSelector.LowestHpPercentAlly : TargetSelector.LowestHpPercentEnemy,
                FallbackPolicy = isAllied ? TargetFallbackPolicy.Self : TargetFallbackPolicy.NearestReachableEnemy,
                Filters = TargetFilterFlags.ExcludeUntargetable
                          | (isAllied ? TargetFilterFlags.ExcludeFullHealthAllies : TargetFilterFlags.None),
                LockTargetAtCastStart = true,
                RetargetLockMode = RetargetLockMode.UntilCastComplete,
            });
    }

    private static BattleState CreateIntentOverrideBattle(
        out UnitSnapshot actor,
        out UnitSnapshot ally,
        out UnitSnapshot currentEnemy,
        out UnitSnapshot forcedEnemy)
    {
        var state = CombatTestFactory.CreateBattleState(
            new[]
            {
                CombatTestFactory.CreateLoopAUnit("intent_support_actor", classId: "mystic"),
                CombatTestFactory.CreateLoopAUnit("intent_support_ally"),
            },
            new[]
            {
                CombatTestFactory.CreateLoopAUnit("intent_current_enemy", race: "undead"),
                CombatTestFactory.CreateLoopAUnit("intent_forced_enemy", race: "undead"),
            },
            seed: 29);
        actor = state.Allies[0];
        ally = state.Allies[1];
        currentEnemy = state.Enemies[0];
        forcedEnemy = state.Enemies[1];
        return state;
    }

    private static EvaluatedAction CreateEvaluatedSupportAction(UnitSnapshot target, BattleSkillSpec skill)
    {
        return new EvaluatedAction(
            BattleActionType.ActiveSkill,
            target,
            skill,
            new TacticRule(999, TacticConditionType.Fallback, 0f, BattleActionType.WaitDefend, TargetSelectorType.Self, null),
            new FloatRange(0f, skill.Range),
            CombatActionState.ExecuteAction,
            ReevaluationReason.None,
            null);
    }

    private static float MeasureMaximumPositiveHealthDelta(BattleState state, string patientName)
    {
        var patient = state.AllUnits.Single(unit => unit.Definition.Name == patientName);
        var previousHealth = patient.CurrentHealth;
        var maxPositiveDelta = 0f;

        BattleResolver.Run(state, 300, step =>
        {
            var currentHealth = step.Units.Single(unit => unit.Name == patientName).CurrentHealth;
            maxPositiveDelta = Math.Max(maxPositiveDelta, currentHealth - previousHealth);
            previousHealth = currentHealth;
        });

        return maxPositiveDelta;
    }
}
