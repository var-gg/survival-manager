using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;
using SM.Core.Ids;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class CombatActionResolverTests
{
    private static UnitSnapshot CreatePositionedUnit(
        string id,
        TeamSide side,
        DeploymentAnchorId anchor = DeploymentAnchorId.FrontCenter,
        float hp = 20f,
        float physPower = 5f,
        float armor = 2f,
        BattleSkillSpec? signatureActive = null,
        BattleSkillSpec? flexActive = null,
        string classId = "vanguard",
        float attackRange = 1.2f,
        BattleBasicAttackSpec? basicAttack = null,
        CombatEntityKind entityKind = CombatEntityKind.RosterUnit,
        OwnershipLink? ownership = null,
        SummonProfile? summonProfile = null)
    {
        var loadout = CombatTestFactory.CreateLoopAUnit(
            id,
            anchor: anchor,
            classId: classId,
            hp: hp,
            physPower: physPower,
            armor: armor,
            attackRange: attackRange,
            signatureActive: signatureActive,
            flexActive: flexActive,
            basicAttack: basicAttack,
            entityKind: entityKind,
            ownership: ownership,
            summonProfile: summonProfile);
        var unit = new UnitSnapshot(
            new EntityId(id),
            side,
            loadout,
            BattleFactory.ResolveAnchorPosition(side, anchor),
            BattleFactory.ResolveSpawnPosition(side, anchor));
        unit.SetPosition(side == TeamSide.Ally
            ? new CombatVector2(-0.6f, 0f)
            : new CombatVector2(0.6f, 0f));
        unit.SetActionState(CombatActionState.AcquireTarget);
        return unit;
    }

    private static BattleState CreateState(UnitSnapshot[] allies, UnitSnapshot[] enemies, int seed = 42)
    {
        return new BattleState(
            allies, enemies,
            TeamPostureType.StandardAdvance, TeamPostureType.StandardAdvance,
            BattleSimulator.DefaultFixedStepSeconds, seed);
    }

    // ── BasicAttack ──

    [Test]
    public void BasicAttack_DealsDamage_AndGrantsActorEnergy()
    {
        var actor = CreatePositionedUnit("ally_attacker", TeamSide.Ally, physPower: 8f);
        var target = CreatePositionedUnit("enemy_target", TeamSide.Enemy, hp: 40f, armor: 0f);
        var state = CreateState(new[] { actor }, new[] { target });

        var energyBefore = actor.CurrentEnergy;
        actor.BeginWindup(BattleActionType.BasicAttack, target.Id, null);
        actor.FinishWindup();
        var events = CombatActionResolver.Resolve(state, actor);

        Assert.That(events.Count, Is.GreaterThanOrEqualTo(1), "Should produce at least one event");
        var attackEvent = events.First(e => e.ActionType == BattleActionType.BasicAttack);
        Assert.That(attackEvent.Value, Is.GreaterThan(0f), "Attack should deal positive damage");
        Assert.That(target.CurrentHealth, Is.LessThan(40f), "Target should have lost health");
        Assert.That(actor.CurrentEnergy, Is.GreaterThan(energyBefore), "Attacker should gain energy from basic attack");
        Assert.That(actor.ActionState, Is.EqualTo(CombatActionState.Recover), "Attacker should enter recovery");
    }

    [Test]
    public void BasicAttack_TargetAlreadyDead_ClearsTargetWithoutDamage()
    {
        var actor = CreatePositionedUnit("ally_attacker", TeamSide.Ally);
        var target = CreatePositionedUnit("enemy_dead", TeamSide.Enemy, hp: 1f);
        var state = CreateState(new[] { actor }, new[] { target });

        target.TakeDamage(999f);
        Assert.That(target.IsAlive, Is.False, "Precondition: target is dead");

        actor.BeginWindup(BattleActionType.BasicAttack, target.Id, null);
        actor.FinishWindup();
        var events = CombatActionResolver.Resolve(state, actor);

        Assert.That(events, Is.Empty, "No events when target is already dead");
        Assert.That(actor.ActionState, Is.EqualTo(CombatActionState.AcquireTarget), "Should transition to AcquireTarget");
    }

    [Test]
    public void BasicAttack_TargetLeavesRangeDuringWindup_ProducesMissWithoutDamage()
    {
        var actor = CreatePositionedUnit("ally_attacker", TeamSide.Ally, physPower: 8f);
        var target = CreatePositionedUnit("enemy_target", TeamSide.Enemy, hp: 40f, armor: 0f);
        var state = CreateState(new[] { actor }, new[] { target });

        var hpBefore = target.CurrentHealth;
        var energyBefore = actor.CurrentEnergy;
        actor.BeginWindup(BattleActionType.BasicAttack, target.Id, null);
        target.SetPosition(new CombatVector2(5f, 0f));

        var events = CombatActionResolver.Resolve(state, actor);

        var attackEvent = events.Single(e => e.LogCode == BattleLogCode.BasicAttackDamage);
        Assert.That(attackEvent.Value, Is.EqualTo(0f));
        Assert.That(attackEvent.Note, Is.EqualTo("miss_range"));
        Assert.That(target.CurrentHealth, Is.EqualTo(hpBefore));
        Assert.That(actor.CurrentEnergy, Is.EqualTo(energyBefore));
        Assert.That(actor.ActionState, Is.EqualTo(CombatActionState.Recover));
    }

    [Test]
    public void BasicAttack_LungeProfileStepsIntoContactBeforeDamage()
    {
        var basicAttack = new BattleBasicAttackSpec(
            "ally_lunge:basic",
            "Lunge Basic",
            ActionProfile: BasicAttackActionProfile.LungeStrike);
        var actor = CreatePositionedUnit(
            "ally_lunge",
            TeamSide.Ally,
            classId: "duelist",
            physPower: 8f,
            armor: 0f,
            attackRange: 1.25f,
            basicAttack: basicAttack);
        var target = CreatePositionedUnit("enemy_target", TeamSide.Enemy, hp: 40f, armor: 0f);
        var state = CreateState(new[] { actor }, new[] { target });

        actor.SetPosition(new CombatVector2(0f, 0f));
        target.SetPosition(new CombatVector2(actor.NavigationRadius + target.NavigationRadius + 1.20f, 0f));
        var profile = BasicAttackActionProfileResolver.Resolve(actor);
        var beforeX = actor.Position.X;
        var beforeEdge = MovementResolver.ComputeEdgeDistance(actor, target);

        actor.BeginWindup(BattleActionType.BasicAttack, target.Id, null);
        actor.FinishWindup();
        var events = CombatActionResolver.Resolve(state, actor);

        var attackEvent = events.Single(e => e.LogCode == BattleLogCode.BasicAttackDamage);
        Assert.That(beforeEdge, Is.GreaterThan(profile.ContactRange + 0.2f));
        Assert.That(actor.Position.X, Is.GreaterThan(beforeX + 0.45f), "Lunge profile should move the attacker toward contact before hit resolution.");
        Assert.That(attackEvent.Value, Is.GreaterThan(0f));
        Assert.That(attackEvent.Note, Does.Contain("profile_lunge"));
    }

    [Test]
    public void BasicAttack_RangedAutoProfileDoesNotStepIntoContact()
    {
        var actor = CreatePositionedUnit(
            "ally_ranger",
            TeamSide.Ally,
            classId: "ranger",
            physPower: 8f,
            armor: 0f,
            attackRange: 3.2f);
        var target = CreatePositionedUnit("enemy_target", TeamSide.Enemy, hp: 40f, armor: 0f);
        var state = CreateState(new[] { actor }, new[] { target });

        actor.SetPosition(new CombatVector2(0f, 0f));
        target.SetPosition(new CombatVector2(actor.NavigationRadius + target.NavigationRadius + 2.8f, 0f));
        var before = actor.Position;
        var profile = BasicAttackActionProfileResolver.Resolve(actor);

        actor.BeginWindup(BattleActionType.BasicAttack, target.Id, null);
        actor.FinishWindup();
        var events = CombatActionResolver.Resolve(state, actor);

        var attackEvent = events.Single(e => e.LogCode == BattleLogCode.BasicAttackDamage);
        Assert.That(profile.Profile, Is.EqualTo(BasicAttackActionProfile.StationaryStrike));
        Assert.That(profile.ContactRange, Is.EqualTo(profile.LogicalRange).Within(0.001f));
        Assert.That(actor.Position.X, Is.EqualTo(before.X).Within(0.001f));
        Assert.That(actor.Position.Y, Is.EqualTo(before.Y).Within(0.001f));
        Assert.That(attackEvent.Value, Is.GreaterThan(0f));
        Assert.That(attackEvent.Note, Does.Not.Contain("profile_lunge"));
        Assert.That(attackEvent.Note, Does.Not.Contain("profile_stepin"));
        Assert.That(attackEvent.Note, Does.Not.Contain("profile_dash"));
    }

    [Test]
    public void BasicAttack_KillingBlow_ProducesKillEvent_AndGrantsKillEnergy()
    {
        var actor = CreatePositionedUnit("ally_killer", TeamSide.Ally, physPower: 50f);
        var target = CreatePositionedUnit("enemy_fragile", TeamSide.Enemy, hp: 1f, armor: 0f);
        var state = CreateState(new[] { actor }, new[] { target });

        var energyBefore = actor.CurrentEnergy;
        actor.BeginWindup(BattleActionType.BasicAttack, target.Id, null);
        actor.FinishWindup();
        var events = CombatActionResolver.Resolve(state, actor);

        Assert.That(target.IsAlive, Is.False, "Target should be dead");
        var killEvent = events.FirstOrDefault(e => e.EventKind == BattleEventKind.Kill);
        Assert.That(killEvent, Is.Not.Null, "Should produce a kill event");
        Assert.That(actor.CurrentEnergy, Is.GreaterThan(energyBefore), "Killer should gain energy");
    }

    // ── ActiveSkill: Heal ──

    [Test]
    public void ActiveSkill_Heal_RestoresHealth_AndStartsCooldown()
    {
        var healSkill = new BattleSkillSpec(
            "heal_skill",
            "Test Heal",
            SkillKind.Heal,
            10f,
            5f,
            ResolvedSlotKind: ActionSlotKind.FlexActive,
            ActivationModel: ActivationModel.Cooldown,
            Lane: ActionLane.Primary,
            LockRule: ActionLockRule.HardCommit,
            BaseCooldownSeconds: 2f,
            TargetRuleData: new TargetRule
            {
                Domain = TargetDomain.AlliedUnit,
                PrimarySelector = TargetSelector.LowestHpPercentAlly,
                FallbackPolicy = TargetFallbackPolicy.Self,
            });

        var healer = CreatePositionedUnit("ally_healer", TeamSide.Ally, flexActive: healSkill);
        var wounded = CreatePositionedUnit("ally_wounded", TeamSide.Ally, hp: 30f);
        wounded.TakeDamage(20f);
        var hpBefore = wounded.CurrentHealth;
        var state = CreateState(new[] { healer, wounded }, System.Array.Empty<UnitSnapshot>());

        healer.BeginWindup(BattleActionType.ActiveSkill, wounded.Id, healSkill.Id);
        healer.FinishWindup();
        var events = CombatActionResolver.Resolve(state, healer);

        Assert.That(wounded.CurrentHealth, Is.GreaterThan(hpBefore), "Wounded ally should be healed");
        Assert.That(events.Any(e => e.LogCode == BattleLogCode.ActiveSkillHeal), Is.True, "Should produce heal event");
        Assert.That(healer.ActionState, Is.EqualTo(CombatActionState.Recover), "Healer should enter recovery");
        Assert.That(healer.CooldownRemaining, Is.GreaterThan(0f), "Cooldown should start");
    }

    // ── ActiveSkill: Shield ──

    [Test]
    public void ActiveSkill_Shield_AddsBarrier()
    {
        var shieldSkill = new BattleSkillSpec(
            "shield_skill",
            "Test Shield",
            SkillKind.Shield,
            8f,
            5f,
            ResolvedSlotKind: ActionSlotKind.FlexActive,
            ActivationModel: ActivationModel.Cooldown,
            Lane: ActionLane.Primary,
            LockRule: ActionLockRule.HardCommit,
            BaseCooldownSeconds: 3f,
            TargetRuleData: new TargetRule
            {
                Domain = TargetDomain.AlliedUnit,
                PrimarySelector = TargetSelector.LowestHpPercentAlly,
                FallbackPolicy = TargetFallbackPolicy.Self,
            });

        var caster = CreatePositionedUnit("ally_caster", TeamSide.Ally, flexActive: shieldSkill);
        var recipient = CreatePositionedUnit("ally_recipient", TeamSide.Ally, hp: 20f);
        var state = CreateState(new[] { caster, recipient }, System.Array.Empty<UnitSnapshot>());

        Assert.That(recipient.Barrier, Is.EqualTo(0f), "Precondition: no barrier");

        caster.BeginWindup(BattleActionType.ActiveSkill, recipient.Id, shieldSkill.Id);
        caster.FinishWindup();
        CombatActionResolver.Resolve(state, caster);

        Assert.That(recipient.Barrier, Is.GreaterThan(0f), "Recipient should have barrier");
    }

    // ── ActiveSkill: Damage ──

    [Test]
    public void ActiveSkill_Damage_DealsDamage_AndTracksKill()
    {
        var strikeSkill = new BattleSkillSpec(
            "strike_skill",
            "Test Strike",
            SkillKind.Strike,
            50f,
            3f,
            ResolvedSlotKind: ActionSlotKind.SignatureActive,
            ActivationModel: ActivationModel.Energy,
            Lane: ActionLane.Primary,
            LockRule: ActionLockRule.HardCommit,
            TargetRuleData: new TargetRule());

        var actor = CreatePositionedUnit("ally_striker", TeamSide.Ally, physPower: 10f, signatureActive: strikeSkill);
        var target = CreatePositionedUnit("enemy_weak", TeamSide.Enemy, hp: 3f, armor: 0f);
        var state = CreateState(new[] { actor }, new[] { target });

        actor.BeginWindup(BattleActionType.ActiveSkill, target.Id, strikeSkill.Id);
        actor.FinishWindup();
        var events = CombatActionResolver.Resolve(state, actor);

        var damageEvent = events.FirstOrDefault(e => e.LogCode == BattleLogCode.ActiveSkillDamage);
        Assert.That(damageEvent, Is.Not.Null, "Should produce damage event");
        Assert.That(damageEvent!.Value, Is.GreaterThan(0f), "Should deal positive damage");

        Assert.That(target.IsAlive, Is.False, "Target should be dead from high power");
        Assert.That(events.Any(e => e.EventKind == BattleEventKind.Kill), Is.True, "Should produce kill event");
    }

    [Test]
    public void ActiveSkill_Damage_TargetLeavesRangeDuringWindup_ProducesMissWithoutDamage()
    {
        var strikeSkill = new BattleSkillSpec(
            "strike_skill",
            "Test Strike",
            SkillKind.Strike,
            50f,
            1.2f,
            ResolvedSlotKind: ActionSlotKind.FlexActive,
            ActivationModel: ActivationModel.Cooldown,
            Lane: ActionLane.Primary,
            LockRule: ActionLockRule.HardCommit,
            BaseCooldownSeconds: 1.2f,
            TargetRuleData: new TargetRule());

        var actor = CreatePositionedUnit("ally_striker", TeamSide.Ally, physPower: 10f, flexActive: strikeSkill);
        var target = CreatePositionedUnit("enemy_target", TeamSide.Enemy, hp: 40f, armor: 0f);
        var state = CreateState(new[] { actor }, new[] { target });

        var hpBefore = target.CurrentHealth;
        actor.BeginWindup(BattleActionType.ActiveSkill, target.Id, strikeSkill.Id);
        target.SetPosition(new CombatVector2(5f, 0f));

        var events = CombatActionResolver.Resolve(state, actor);

        var damageEvent = events.Single(e => e.LogCode == BattleLogCode.ActiveSkillDamage);
        Assert.That(damageEvent.Value, Is.EqualTo(0f));
        Assert.That(damageEvent.Note, Is.EqualTo("miss_range"));
        Assert.That(target.CurrentHealth, Is.EqualTo(hpBefore));
        Assert.That(actor.CooldownRemaining, Is.GreaterThan(0f));
        Assert.That(actor.ActionState, Is.EqualTo(CombatActionState.Recover));
    }

    // ── Assist Energy ──

    [Test]
    public void Kill_GrantsAssistEnergy_ToOtherDamageContributors()
    {
        var attacker1 = CreatePositionedUnit("ally_1", TeamSide.Ally, physPower: 50f);
        var attacker2 = CreatePositionedUnit("ally_2", TeamSide.Ally, physPower: 50f);
        var target = CreatePositionedUnit("enemy_tank", TeamSide.Enemy, hp: 100f, armor: 0f);
        var state = CreateState(new[] { attacker1, attacker2 }, new[] { target });

        state.RegisterDamage(attacker2, target);
        target.TakeDamage(50f);

        var assistEnergyBefore = attacker2.CurrentEnergy;
        attacker1.BeginWindup(BattleActionType.BasicAttack, target.Id, null);
        attacker1.FinishWindup();
        CombatActionResolver.Resolve(state, attacker1);

        Assert.That(target.IsAlive, Is.False, "Target should be dead");
        Assert.That(attacker2.CurrentEnergy, Is.GreaterThan(assistEnergyBefore), "Assister should gain energy");
    }

    // ── Summon Kill Mirroring ──

    [Test]
    public void SummonKill_MirroredOwner_RespectsEnergyPolicy()
    {
        var ownerId = new EntityId("ally_owner");
        var owner = CreatePositionedUnit("ally_owner", TeamSide.Ally);
        var summonProfile = new SummonProfile
        {
            CreditPolicy = CombatCreditFlags.EligibleForMirroredOwnerKill
                           | CombatCreditFlags.EligibleForMirroredOwnerAssist,
        };
        var summon = CreatePositionedUnit(
            "ally_summon", TeamSide.Ally,
            physPower: 50f,
            entityKind: CombatEntityKind.OwnedSummon,
            ownership: new OwnershipLink
            {
                OwnerEntity = ownerId,
                SourceEntity = ownerId,
                SourceDefinitionId = "skill_summon",
                SummonGeneration = 0,
            },
            summonProfile: summonProfile);

        var target = CreatePositionedUnit("enemy_fragile", TeamSide.Enemy, hp: 1f, armor: 0f);
        var state = CreateState(new[] { owner, summon }, new[] { target });

        var ownerEnergyBefore = owner.CurrentEnergy;
        summon.BeginWindup(BattleActionType.BasicAttack, target.Id, null);
        summon.FinishWindup();
        var events = CombatActionResolver.Resolve(state, summon);

        var killEvent = events.Single(e => e.EventKind == BattleEventKind.Kill);
        Assert.That(killEvent.KillPayload!.IsMirroredFromOwnedSummon, Is.True, "Kill should be mirrored");
        Assert.That(killEvent.KillPayload.MirroredOwner, Is.EqualTo(ownerId), "Owner should be set");
        Assert.That(killEvent.KillPayload.GrantsOwnerEnergy, Is.False,
            "EligibleForMirroredOwnerKill alone should NOT grant owner energy");
        Assert.That(owner.CurrentEnergy, Is.EqualTo(ownerEnergyBefore),
            "Owner energy should not change without EligibleForOwnerEnergyGain flag");
    }

    // ── WaitDefend ──

    [Test]
    public void WaitDefend_SetsDefendingState_AndProducesEvent()
    {
        var actor = CreatePositionedUnit("ally_defender", TeamSide.Ally);
        var state = CreateState(new[] { actor }, System.Array.Empty<UnitSnapshot>());

        actor.BeginWindup(BattleActionType.WaitDefend, actor.Id, null);
        actor.FinishWindup();
        var events = CombatActionResolver.Resolve(state, actor);

        Assert.That(actor.IsDefending, Is.True, "Should be defending");
        Assert.That(events.Any(e => e.LogCode == BattleLogCode.WaitDefend), Is.True, "Should produce defend event");
    }

    // ── Null/dead actor ──

    [Test]
    public void Resolve_DeadActor_ReturnsEmpty()
    {
        var actor = CreatePositionedUnit("ally_dead", TeamSide.Ally, hp: 1f);
        var state = CreateState(new[] { actor }, System.Array.Empty<UnitSnapshot>());

        actor.TakeDamage(999f);
        var events = CombatActionResolver.Resolve(state, actor);

        Assert.That(events, Is.Empty);
    }

    [Test]
    public void Resolve_NoPendingAction_ReturnsEmpty()
    {
        var actor = CreatePositionedUnit("ally_idle", TeamSide.Ally);
        var state = CreateState(new[] { actor }, System.Array.Empty<UnitSnapshot>());

        var events = CombatActionResolver.Resolve(state, actor);

        Assert.That(events, Is.Empty);
    }
}
