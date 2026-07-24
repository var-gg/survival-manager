using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Numerics;
using SM.Core.Stats;
using SM.Meta;
using SM.Meta.Services;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class SecondaryPressureServiceFastTests
{
    [Test]
    public void BasicDamageAction_SplitsOneNormalizedBudget_InStableEntityOrder()
    {
        var enemyDefinition = BuildPressureEnemy(pressureFraction: 0.25f);
        var allyDefinitions = BuildAllies();
        var state = CombatTestFactory.CreateBattleState(allyDefinitions, new[] { enemyDefinition }, seed: 17);
        var actor = state.Enemies.Single();
        var primary = state.Allies.Single(value => value.Definition.Id == "hero-z-primary");
        var secondaryEnergyBefore = state.Allies
            .Where(value => value.Id != primary.Id)
            .ToDictionary(value => value.Id.Value, value => value.CurrentEnergy, StringComparer.Ordinal);

        actor.BeginWindup(BattleActionType.BasicAttack, primary.Id, null);
        actor.FinishWindup();
        var events = CombatActionResolver.Resolve(state, actor);

        var action = state.SecondaryPressureTelemetry.Actions.Single();
        Assert.That(action.PrimaryTargetId, Is.EqualTo(primary.Id.Value));
        Assert.That(action.Recipients, Has.Count.EqualTo(3));
        Assert.That(
            action.Recipients.Select(value => value.TargetId),
            Is.EqualTo(action.Recipients.Select(value => value.TargetId).OrderBy(value => value, StringComparer.Ordinal)));
        Assert.That(action.Recipients.Any(value => value.TargetId == primary.Id.Value), Is.False);

        var expectedTotal = Hp64.FromRaw(action.NormalizedDamageBudgetRaw)
                            * actor.SecondaryPressureFraction;
        Assert.That(action.Recipients.Sum(value => value.RawAllocated), Is.EqualTo(expectedTotal.Raw));
        Assert.That(
            action.Recipients.Max(value => value.RawAllocated)
            - action.Recipients.Min(value => value.RawAllocated),
            Is.LessThanOrEqualTo(1L));
        var remainder = expectedTotal.Raw % action.Recipients.Count;
        for (var index = 0; index < action.Recipients.Count; index++)
        {
            var expected = (expectedTotal.Raw / action.Recipients.Count)
                           + (index < remainder ? 1L : 0L);
            Assert.That(action.Recipients[index].RawAllocated, Is.EqualTo(expected));
        }

        var secondaryEvents = events
            .Where(value => value.LogCode == BattleLogCode.SecondaryPressureDamage
                            && value.EventKind != BattleEventKind.Kill)
            .ToArray();
        Assert.That(secondaryEvents, Has.Length.EqualTo(3));
        Assert.That(secondaryEvents.All(value => value.Note == "endless_heat_secondary_pressure"), Is.True);
        foreach (var target in state.Allies.Where(value => value.Id != primary.Id))
        {
            Assert.That(
                target.CurrentEnergy,
                Is.EqualTo(secondaryEnergyBefore[target.Id.Value]),
                "Secondary pressure is not a direct-hit energy event.");
        }
    }

    [Test]
    public void AreaDamageAction_RecordsOnePressureBudget_NotOnePerHit()
    {
        var areaSkill = new BattleSkillSpec(
            "enemy-area",
            "Enemy Area",
            SkillKind.Strike,
            20f,
            20f,
            DamageType: DamageType.Physical,
            CanCrit: true,
            AreaRadius: 20f,
            AreaEffectFamily: BattleAreaEffectFamily.GroundAoe);
        var state = CombatTestFactory.CreateBattleState(
            BuildAllies(),
            new[] { BuildPressureEnemy(pressureFraction: 0.25f, areaSkill: areaSkill) },
            seed: 19);
        var actor = state.Enemies.Single();
        var primary = state.Allies.Single(value => value.Definition.Id == "hero-z-primary");
        actor.SetPosition(CombatVector2.Zero);
        for (var index = 0; index < state.Allies.Count; index++)
        {
            state.Allies[index].SetPosition(new CombatVector2(1f, index * 0.1f));
        }

        actor.BeginWindup(BattleActionType.ActiveSkill, primary.Id, areaSkill.Id);
        actor.FinishWindup();
        var events = CombatActionResolver.Resolve(state, actor);

        Assert.That(
            events.Count(value => value.LogCode == BattleLogCode.ActiveSkillDamage),
            Is.GreaterThan(1),
            "Precondition: the authored area action hit multiple heroes.");
        var pressure = state.SecondaryPressureTelemetry.Actions.Single();
        Assert.That(pressure.ActorId, Is.EqualTo(actor.Id.Value));
        Assert.That(pressure.Recipients, Has.Count.EqualTo(3));
        var expectedTotal = Hp64.FromRaw(pressure.NormalizedDamageBudgetRaw)
                            * actor.SecondaryPressureFraction;
        Assert.That(pressure.Recipients.Sum(value => value.RawAllocated), Is.EqualTo(expectedTotal.Raw));
    }

    [Test]
    public void HeatZero_ProducesNoPressureEventOrTelemetry_AndKeepsActionBytes()
    {
        var baseline = ResolveAction(BuildBaseEnemy());
        var heatZero = ResolveAction(BuildCurrentHeatEnemy(heat: 0));

        Assert.That(heatZero.Events.Any(value =>
            value.LogCode == BattleLogCode.SecondaryPressureDamage), Is.False);
        Assert.That(heatZero.Telemetry.Actions, Is.Empty);
        Assert.That(heatZero.EventFingerprints, Is.EqualTo(baseline.EventFingerprints));
        Assert.That(heatZero.AllyHealth, Is.EqualTo(baseline.AllyHealth));
        Assert.That(heatZero.CanonicalStateHash, Is.EqualTo(baseline.CanonicalStateHash));
    }

    [TestCase(1)]
    [TestCase(3)]
    [TestCase(5)]
    [TestCase(8)]
    public void NeutralizedHeat_MatchesLiteralShippedActionBytes(int heat)
    {
        var shipped = ResolveAction(BuildLiteralShippedHeatEnemy(heat));
        var neutralized = ResolveAction(BuildCurrentHeatEnemy(heat, chanceBearing: true));

        Assert.That(EndlessCycleService.HeatSecondaryPressureScale, Is.Zero);
        Assert.That(EndlessCycleService.BuildEnemyHeatSecondaryPressurePackages(heat), Is.Empty);
        Assert.That(neutralized.SecondaryPressureFractionRaw, Is.Zero);
        Assert.That(neutralized.Telemetry.Actions, Is.Empty);
        Assert.That(
            neutralized.Events.Any(value => value.LogCode == BattleLogCode.SecondaryPressureDamage),
            Is.False);
        Assert.That(neutralized.EventFingerprints, Is.EqualTo(shipped.EventFingerprints),
            "Primary damage and chance-bearing combat event bytes must match the literal shipped package.");
        Assert.That(neutralized.AllyHealth, Is.EqualTo(shipped.AllyHealth),
            "All applied damage must be byte-identical to shipped.");
        Assert.That(neutralized.CanonicalStateHash, Is.EqualTo(shipped.CanonicalStateHash),
            "The post-action authoritative state, including the seeded random outcome, must match shipped.");
    }

    private static ActionObservation ResolveAction(BattleUnitLoadout enemyDefinition)
    {
        var state = CombatTestFactory.CreateBattleState(
            BuildAllies(),
            new[] { enemyDefinition },
            seed: 23);
        var actor = state.Enemies.Single();
        var primary = state.Allies.Single(value => value.Definition.Id == "hero-z-primary");
        actor.BeginWindup(BattleActionType.BasicAttack, primary.Id, null);
        actor.FinishWindup();
        var events = CombatActionResolver.Resolve(state, actor);
        return new ActionObservation(
            events,
            state.SecondaryPressureTelemetry.BuildSnapshot(),
            events.Select(value => value.ToString()).ToArray(),
            state.Allies
                .OrderBy(value => value.Id.Value, StringComparer.Ordinal)
                .Select(value => BitConverter.SingleToInt32Bits(value.CurrentHealth))
                .ToArray(),
            BattleStateCanonicalHash.Compute(state),
            actor.SecondaryPressureFraction.Raw);
    }

    private static BattleUnitLoadout BuildCurrentHeatEnemy(int heat, bool chanceBearing = false)
    {
        var enemy = chanceBearing ? BuildChanceBearingBaseEnemy() : BuildBaseEnemy();
        var numeric = PoliticalCombatConditionService.ApplyEnemyPackages(
            new[] { enemy },
            EndlessCycleService.BuildEnemyHeatPackages(heat));
        return PoliticalCombatConditionService.ApplyEnemyRulePackages(
                numeric,
                EndlessCycleService.BuildEnemyHeatSecondaryPressurePackages(heat))
            .Single();
    }

    private static BattleUnitLoadout BuildLiteralShippedHeatEnemy(int heat)
    {
        var sourceId = $"endless_heat:h{heat}";
        return PoliticalCombatConditionService.ApplyEnemyPackages(
                new[] { BuildChanceBearingBaseEnemy() },
                new[]
                {
                    new CombatModifierPackage(sourceId, ModifierSource.Other, new[]
                    {
                        new StatModifier(StatKey.MaxHealth, ModifierOp.Increased, 0.10f * heat, ModifierSource.Other, sourceId),
                        new StatModifier(StatKey.PhysPower, ModifierOp.Increased, 0.06f * heat, ModifierSource.Other, sourceId),
                        new StatModifier(StatKey.MagPower, ModifierOp.Increased, 0.06f * heat, ModifierSource.Other, sourceId),
                    }),
                })
            .Single();
    }

    private static BattleUnitLoadout BuildPressureEnemy(
        float pressureFraction,
        BattleSkillSpec? areaSkill = null)
    {
        const string sourceId = "test:secondary-pressure";
        var numeric = PoliticalCombatConditionService.ApplyEnemyPackages(
            new[] { BuildBaseEnemy(areaSkill) },
            new[]
            {
                new CombatModifierPackage(sourceId, ModifierSource.Other, new[]
                {
                    new StatModifier(StatKey.MaxHealth, ModifierOp.Increased, 0.20f, ModifierSource.Other, sourceId),
                    new StatModifier(StatKey.PhysPower, ModifierOp.Increased, 0.12f, ModifierSource.Other, sourceId),
                    new StatModifier(StatKey.MagPower, ModifierOp.Increased, 0.12f, ModifierSource.Other, sourceId),
                }),
            });
        return PoliticalCombatConditionService.ApplyEnemyRulePackages(
                numeric,
                new[]
                {
                    new CombatRuleModifierPackage(sourceId, ModifierSource.Other, new[]
                    {
                        new RuleModifier(
                            RuleModifierKind.SecondaryPressure,
                            "equal-non-primary",
                            pressureFraction),
                    }),
                })
            .Single();
    }

    private static BattleUnitLoadout BuildChanceBearingBaseEnemy()
    {
        var enemy = BuildBaseEnemy();
        var stats = new Dictionary<StatKey, float>(enemy.BaseStats)
        {
            [StatKey.CritChance] = 0.5f,
            [StatKey.CritMultiplier] = 0.5f,
        };
        return enemy with { BaseStats = stats };
    }

    private static BattleUnitLoadout BuildBaseEnemy(BattleSkillSpec? areaSkill = null)
        => CombatTestFactory.CreateLoopAUnit(
            "enemy-pressure",
            race: "undead",
            hp: 500f,
            physPower: 100f,
            armor: 0f,
            attackRange: 20f,
            signatureActive: areaSkill,
            energy: new EnergyProfile(100f, 0f));

    private static IReadOnlyList<BattleUnitLoadout> BuildAllies()
        => new[]
        {
            CombatTestFactory.CreateLoopAUnit("hero-a", hp: 1000f, armor: 0f, attackRange: 20f),
            CombatTestFactory.CreateLoopAUnit("hero-b", hp: 1000f, armor: 0f, attackRange: 20f),
            CombatTestFactory.CreateLoopAUnit("hero-c", hp: 1000f, armor: 0f, attackRange: 20f),
            CombatTestFactory.CreateLoopAUnit("hero-z-primary", hp: 1000f, armor: 0f, attackRange: 20f),
        };

    private sealed record ActionObservation(
        IReadOnlyList<BattleEvent> Events,
        SecondaryPressureTelemetrySnapshot Telemetry,
        IReadOnlyList<string> EventFingerprints,
        IReadOnlyList<int> AllyHealth,
        string CanonicalStateHash,
        int SecondaryPressureFractionRaw);
}
