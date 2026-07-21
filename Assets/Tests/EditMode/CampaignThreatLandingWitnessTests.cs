using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;
using SM.Core.Ids;
using SM.Core.Stats;
using SM.Editor.Validation;

namespace SM.Tests.EditMode;

[Category("BatchOnly")]
public sealed class CampaignThreatLandingWitnessTests
{
    [Test]
    public void MissingBossCaptain_ProducesEmptyObservationWithoutThrowing()
    {
        var state = BattleFactory.Create(
            new[] { Unit("ally", DeploymentAnchorId.FrontCenter, "vanguard", "anchor") },
            new[] { Unit("enemy", DeploymentAnchorId.FrontCenter, "vanguard", "enemy_unit") },
            TeamPostureType.StandardAdvance,
            TeamPostureType.StandardAdvance,
            0.1f,
            1700);

        var observer = new CampaignThreatLandingBattleObserver(state);
        observer.ObserveStep(new BattleSimulationStep(
            1,
            0.1f,
            ReadModels(state),
            Array.Empty<BattleEvent>(),
            false,
            null));

        var observation = observer.BuildObservation();
        Assert.That(observation.BattleSeed, Is.EqualTo(1700));
        Assert.That(observation.FirstThreatLandingTick, Is.Null);
        Assert.That(observation.FirstThreatLandingSeconds, Is.Null);
        Assert.That(observation.FirstThreatKind, Is.Empty);
        Assert.That(observation.ThreatLandedBeforeCaptainDeath, Is.False);
        Assert.That(observation.CaptainDeathTick, Is.Null);
        Assert.That(observation.FirstFrontlineBodyDeathTick, Is.Null);
        Assert.That(observation.PreThreatPlayerDamage, Is.Zero);
        Assert.That(observation.CoreEhpFractionAtFirstThreat, Is.Null);
    }

    [Test]
    public void TypedSteps_ProjectThreatRoutingCoreEhpAndFrontlineTtk()
    {
        var state = BuildWitnessState(areaThreat: false);
        var observer = new CampaignThreatLandingBattleObserver(state);
        var allyFront = state.Allies[0];
        var allyBack = state.Allies[1];
        var body = state.Enemies.Single(unit => unit.Definition.Id == "body");
        var core = state.Enemies.Single(unit => unit.Definition.Id == "core");
        var threat = state.Enemies.Single(unit => unit.Definition.Id == "threat");

        observer.ObserveStep(new BattleSimulationStep(
            1,
            0.5f,
            ReadModels(state),
            new[]
            {
                DamageEvent(1, 0.5f, allyFront, body, 30f),
                DamageEvent(1, 0.5f, allyFront, core, 10f),
            },
            false,
            null));
        observer.ObserveStep(new BattleSimulationStep(
            2,
            1f,
            ReadModels(state, core.Id.Value, 60f, threat.Id.Value),
            Array.Empty<BattleEvent>(),
            false,
            null,
            CombatEventIntents: new[] { Contacted(threat, allyBack, "enemy_single", SkillDelivery.Melee) }));
        observer.ObserveStep(new BattleSimulationStep(
            3,
            1.5f,
            ReadModels(state),
            new[]
            {
                new BattleEvent(
                    3,
                    1.5f,
                    allyFront.Id,
                    allyFront.Definition.Name,
                    BattleActionType.BasicAttack,
                    BattleLogCode.Generic,
                    body.Id,
                    body.Definition.Name,
                    0f,
                    BattleEventKind.Kill),
            },
            false,
            null));

        var observation = observer.BuildObservation();
        Assert.That(observation.FirstThreatLandingTick, Is.EqualTo(2));
        Assert.That(observation.FirstThreatLandingSeconds, Is.EqualTo(1d));
        Assert.That(observation.FirstThreatKind, Is.EqualTo("dive"));
        Assert.That(observation.ThreatLandedBeforeCaptainDeath, Is.True);
        Assert.That(observation.FirstFrontlineBodyTtkSeconds, Is.EqualTo(1.5d));
        Assert.That(observation.PreThreatFrontlineDamageFraction, Is.EqualTo(0.75d));
        Assert.That(observation.PreThreatCoreDamageFraction, Is.EqualTo(0.25d));
        Assert.That(observation.CoreEhpFractionAtFirstThreat, Is.EqualTo(0.6d));
        Assert.That(observation.FrontlineTtkMinusThreatSeconds, Is.EqualTo(0.5d));

        var accumulator = new CampaignThreatLandingSweepAccumulator();
        accumulator.Record(
            new CampaignBalanceArmSpec("naive", "greedy", false, "test"),
            new CampaignBalanceGridCell(
                new CampaignReferenceSquadSpec("ranged", Array.Empty<string>(), Array.Empty<string>()),
                new CampaignBuildPowerQuantileSpec("P50", 0, false),
                new CampaignEnemyCompositionVariantSpec(0, "authored"),
                new CampaignRosterCoverageSpec("C0", 0)),
            new CampaignNodeIdentity("chapter", 1, "site", 1, "boss", 1, "encounter", false, true),
            observation);
        var aggregate = accumulator.BuildReport().Ranged3RByBossAndArm.Single();
        Assert.That(aggregate.ThreatBeforeCaptainDeathRate, Is.EqualTo(1d));
        Assert.That(aggregate.FirstFrontlineBodyTtkMedianSeconds, Is.EqualTo(1.5d));
        Assert.That(aggregate.FrontlineTtkMinusThreatMedianSeconds, Is.EqualTo(0.5d));
        Assert.That(aggregate.FrontlineTtkMinusThreatP90Seconds, Is.EqualTo(0.5d));
    }

    [Test]
    public void AuthoredAreaSkillContact_IsThreatWithoutDiveIntent()
    {
        var state = BuildWitnessState(areaThreat: true);
        var observer = new CampaignThreatLandingBattleObserver(state);
        var target = state.Allies[1];
        var actor = state.Enemies.Single(unit => unit.Definition.Id == "threat");
        observer.ObserveStep(new BattleSimulationStep(
            4,
            2f,
            ReadModels(state),
            Array.Empty<BattleEvent>(),
            false,
            null,
            CombatEventIntents: new[] { Contacted(actor, target, "enemy_aoe", SkillDelivery.Projectile) }));

        var observation = observer.BuildObservation();
        Assert.That(observation.FirstThreatLandingTick, Is.EqualTo(4));
        Assert.That(observation.FirstThreatKind, Is.EqualTo("aoe"));
    }

    [Test]
    public void Observer_DoesNotChangeCanonicalBattleBytes()
    {
        var baselineState = BuildDeterminismState();
        var observedState = BuildDeterminismState();
        var baselineHashes = new List<string>();
        var observedHashes = new List<string>();
        var observer = new CampaignThreatLandingBattleObserver(observedState);

        var baseline = BattleResolver.Run(
            baselineState,
            180,
            _ => baselineHashes.Add(BattleStateCanonicalHash.Compute(baselineState)));
        var observed = BattleResolver.Run(
            observedState,
            180,
            step =>
            {
                observer.ObserveStep(step);
                observedHashes.Add(BattleStateCanonicalHash.Compute(observedState));
            });

        Assert.That(observedHashes, Is.EqualTo(baselineHashes));
        Assert.That(observed.Winner, Is.EqualTo(baseline.Winner));
        Assert.That(observed.StepCount, Is.EqualTo(baseline.StepCount));
        Assert.That(
            BitConverter.SingleToInt32Bits(observed.DurationSeconds),
            Is.EqualTo(BitConverter.SingleToInt32Bits(baseline.DurationSeconds)));
        Assert.That(
            observed.Events.Select(EventFingerprint),
            Is.EqualTo(baseline.Events.Select(EventFingerprint)),
            "Adding the witness observer must leave the BattleResult event bytes identical.");
    }

    private static BattleState BuildWitnessState(bool areaThreat)
    {
        var areaSkill = new BattleSkillSpec(
            "enemy_aoe",
            "Enemy Aoe",
            SkillKind.Strike,
            8f,
            6f,
            "core_active",
            DamageType: DamageType.Physical,
            CanCrit: false,
            Delivery: SkillDelivery.Melee,
            TargetRule: SkillTargetRule.NearestEnemy,
            ResolvedSlotKind: ActionSlotKind.SignatureActive,
            ActivationModel: ActivationModel.Cooldown,
            Lane: ActionLane.Primary,
            LockRule: ActionLockRule.HardCommit,
            AuthorityLayer: AuthorityLayer.Skill,
            AreaRadius: 0.5f,
            AreaEffectFamily: BattleAreaEffectFamily.GroundAoe);
        return BattleFactory.Create(
            new[]
            {
                Unit("ally_front", DeploymentAnchorId.FrontCenter, "vanguard", "anchor"),
                Unit("ally_back", DeploymentAnchorId.BackCenter, "ranger", "carry"),
            },
            new[]
            {
                Unit("body", DeploymentAnchorId.FrontCenter, "vanguard", "anchor"),
                Unit("core", DeploymentAnchorId.BackCenter, "mystic", "boss_captain", maxHealth: 100f),
                Unit(
                    "threat",
                    DeploymentAnchorId.FrontTop,
                    "duelist",
                    "escort",
                    areaThreat ? new[] { areaSkill } : Array.Empty<BattleSkillSpec>()),
            },
            TeamPostureType.StandardAdvance,
            TeamPostureType.StandardAdvance,
            0.1f,
            1701);
    }

    private static BattleState BuildDeterminismState()
    {
        return BattleFactory.Create(
            new[] { Unit("ally", DeploymentAnchorId.BackCenter, "ranger", "carry", maxHealth: 40f, physPower: 8f) },
            new[] { Unit("core", DeploymentAnchorId.FrontCenter, "vanguard", "boss_captain", maxHealth: 40f, physPower: 7f) },
            TeamPostureType.StandardAdvance,
            TeamPostureType.StandardAdvance,
            0.1f,
            9917);
    }

    private static BattleUnitLoadout Unit(
        string id,
        DeploymentAnchorId anchor,
        string classId,
        string roleTag,
        IReadOnlyList<BattleSkillSpec>? skills = null,
        float maxHealth = 40f,
        float physPower = 6f)
    {
        return new BattleUnitLoadout(
            id,
            id,
            "human",
            classId,
            anchor,
            new Dictionary<StatKey, float>
            {
                [StatKey.MaxHealth] = maxHealth,
                [StatKey.PhysPower] = physPower,
                [StatKey.MagPower] = 0f,
                [StatKey.Armor] = 1f,
                [StatKey.Resist] = 0f,
                [StatKey.AttackSpeed] = 3f,
                [StatKey.MoveSpeed] = 2f,
                [StatKey.AttackRange] = 1.5f,
                [StatKey.AggroRadius] = 12f,
                [StatKey.AttackCooldown] = 1f,
                [StatKey.AttackWindup] = 0.1f,
                [StatKey.CastWindup] = 0.1f,
                [StatKey.CollisionRadius] = 0.35f,
                [StatKey.LeashDistance] = 20f,
                [StatKey.TargetSwitchDelay] = 0.1f,
                [StatKey.PreferredDistance] = 1.2f,
                [StatKey.RepositionCooldown] = 0.1f,
                [StatKey.CritChance] = 0f,
                [StatKey.CritMultiplier] = 0.5f,
            },
            Array.Empty<UnitRuleChain>(),
            skills ?? Array.Empty<BattleSkillSpec>(),
            RoleTag: roleTag,
            ArchetypeId: id);
    }

    private static IReadOnlyList<BattleUnitReadModel> ReadModels(
        BattleState state,
        string coreId = "",
        float coreHealth = 0f,
        string diveActorId = "")
    {
        return state.AllUnits.Select(unit => new BattleUnitReadModel(
            unit.Id.Value,
            unit.Definition.Name,
            unit.Side,
            unit.Anchor,
            unit.Definition.RaceId,
            unit.Definition.ClassId,
            unit.Position,
            string.Equals(unit.Id.Value, coreId, StringComparison.Ordinal) ? coreHealth : unit.CurrentHealth,
            unit.MaxHealth,
            unit.IsAlive,
            unit.ActionState,
            unit.PendingActionType,
            unit.CurrentTargetId?.Value,
            null,
            0f,
            0f,
            unit.CurrentEnergy,
            unit.MaxEnergy,
            unit.IsDefending,
            unit.Barrier,
            RoleTag: unit.Definition.RoleTag,
            PositioningIntent: string.Equals(unit.Id.Value, diveActorId, StringComparison.Ordinal)
                ? PositioningIntentKind.BacklineDive
                : PositioningIntentKind.None)).ToArray();
    }

    private static BattleEvent DamageEvent(
        int step,
        float time,
        UnitSnapshot actor,
        UnitSnapshot target,
        float damage)
    {
        return new BattleEvent(
            step,
            time,
            actor.Id,
            actor.Definition.Name,
            BattleActionType.BasicAttack,
            BattleLogCode.BasicAttackDamage,
            target.Id,
            target.Definition.Name,
            damage);
    }

    private static BattleCombatEventIntent Contacted(
        UnitSnapshot actor,
        UnitSnapshot target,
        string skillId,
        SkillDelivery delivery)
    {
        return new BattleCombatEventIntent(
            0,
            new ActionInstanceId(1),
            actor.Id,
            CombatEventKind.Skill,
            delivery,
            0,
            0,
            CombatEventIntentStatus.Contacted,
            target.Id,
            SkillId: skillId,
            Contacts: new[]
            {
                new BattleContactIntent(0, 0, 0, target.Id, CombatOutcome.Hit, 10f),
            });
    }

    private static string EventFingerprint(BattleEvent value)
    {
        return string.Join(
            "|",
            value.StepIndex,
            BitConverter.SingleToInt32Bits(value.TimeSeconds),
            value.ActorId.Value,
            (int)value.ActionType,
            (int)value.LogCode,
            value.TargetId?.Value ?? string.Empty,
            BitConverter.SingleToInt32Bits(value.Value),
            (int)value.EventKind,
            value.PayloadId,
            BitConverter.SingleToInt32Bits(value.SecondaryValue),
            value.Note,
            value.KillPayload?.ActualKiller.Value ?? string.Empty,
            value.KillPayload?.ActualVictim.Value ?? string.Empty);
    }
}
