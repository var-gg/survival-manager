using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;
using SM.Core.Stats;
using SM.Editor.SeedData;
using SM.Meta;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Unity;

namespace SM.Tests.EditMode;

[Category("BatchOnly")]
public sealed class DecisionAffixBattleWitnessTests
{
    private const string OwnerId = "hero.decision-affix-owner";
    private const string SacrificeId = "hero.decision-affix-sacrifice";
    private const string EnemyId = "enemy.decision-affix-witness";

    private static readonly string[] DecisionAffixIds =
    {
        "affix_reckless_edge",
        "affix_brittle_focus",
        "affix_overclocked",
        "affix_blood_price",
        "affix_lightfooted_plate",
        "affix_burdened_reach",
        "affix_reaper_spark",
        "affix_last_ward",
        "affix_executioners_edge",
        "affix_desperate_focus",
        "affix_mourning_aegis",
        "affix_first_light",
        "affix_war_chorus",
        "affix_fallen_chorus",
    };

    [SetUp]
    public void SetUp()
    {
        SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(DecisionAffixBattleWitnessTests));
    }

    [Test]
    public void RealCatalog_AllFourteenAffixesCarryDecisionPayloads()
    {
        var snapshot = new RuntimeCombatContentLookup().Snapshot;
        var statSticks = new List<string>();

        foreach (var affixId in DecisionAffixIds)
        {
            Assert.That(snapshot.AffixCatalog, Is.Not.Null);
            Assert.That(snapshot.AffixCatalog!.ContainsKey(affixId), Is.True, affixId);
            Assert.That(snapshot.AffixPackages.ContainsKey(affixId), Is.True, affixId);

            var template = snapshot.AffixCatalog[affixId];
            var package = snapshot.AffixPackages[affixId];
            var hasNegativeModifier = package.Modifiers.Any(modifier => modifier.Value < 0f);
            var hasRule = template.RulePackage is { Modifiers.Count: > 0 };
            var hasTrigger = template.TriggeredEffects is { Count: > 0 };
            if (!hasNegativeModifier && !hasRule && !hasTrigger)
            {
                statSticks.Add(affixId);
            }
        }

        Assert.That(statSticks, Is.Empty,
            "Every decision-affix must carry a real drawback, interpreted rule, or combat trigger.");
    }

    [TestCase("affix_reckless_edge", "item_iron_sword", "phys_power", "armor")]
    [TestCase("affix_brittle_focus", "item_iron_sword", "mag_power", "resist")]
    [TestCase("affix_overclocked", "item_warden_trinket", "attack_speed", "max_health")]
    [TestCase("affix_blood_price", "item_iron_sword", "lifesteal", "max_health")]
    [TestCase("affix_lightfooted_plate", "item_raider_armor", "move_speed", "armor")]
    [TestCase("affix_burdened_reach", "item_iron_sword", "attack_range", "move_speed")]
    public void RealTradeoffAffix_AppliesUpsideAndDownside_InSimulatedBattle(
        string affixId,
        string itemBaseId,
        string upsideStatId,
        string downsideStatId)
    {
        var snapshot = new RuntimeCombatContentLookup().Snapshot;
        var baseline = CreateWitness(snapshot, null, itemBaseId);
        var tradeoff = CreateWitness(snapshot, affixId, itemBaseId);

        Assert.That(StatKey.TryResolve(upsideStatId, out var upside), Is.True);
        Assert.That(StatKey.TryResolve(downsideStatId, out var downside), Is.True);
        Assert.That(tradeoff.Owner.Stats.Get(upside), Is.GreaterThan(baseline.Owner.Stats.Get(upside)),
            $"{affixId} upside did not reach the battle unit");
        Assert.That(tradeoff.Owner.Stats.Get(downside), Is.LessThan(baseline.Owner.Stats.Get(downside)),
            $"{affixId} downside did not reach the battle unit");

        var package = tradeoff.Loadout.Allies.Single().NumericPackages.Single(value => value.SourceId == affixId);
        Assert.That(package.Modifiers.Count, Is.EqualTo(2));
        Assert.That(package.Modifiers.Any(modifier => modifier.Value < 0f), Is.True);

        Assert.That(tradeoff.Simulator.Step().StepIndex, Is.EqualTo(1),
            "The compiled tradeoff must enter and advance a real BattleSimulator.");
    }

    [Test]
    public void RealReaperSpark_OnKillFiresInSimulatedBattle()
    {
        var witness = CreateWitness(
            new RuntimeCombatContentLookup().Snapshot,
            "affix_reaper_spark",
            "item_iron_sword");
        witness.Enemy.TakeDamage(Math.Max(0f, witness.Enemy.CurrentHealth - 1f));

        var beat = RunUntilAffixBeat(witness.Simulator, "affix_reaper_spark");

        Assert.That(beat.Type, Is.EqualTo(CombatBeatType.OnKillEffect));
        Assert.That(beat.Value, Is.EqualTo(10f).Within(0.0001f));
        Assert.That(witness.Owner.CurrentEnergy, Is.GreaterThanOrEqualTo(10f));
    }

    [Test]
    public void RealLastWard_HpThresholdFiresInSimulatedBattle()
    {
        var witness = CreateWitness(
            new RuntimeCombatContentLookup().Snapshot,
            "affix_last_ward",
            "item_raider_armor");
        witness.Owner.TakeDamage(witness.Owner.Barrier + (witness.Owner.MaxHealth * 0.51f) + 0.1f);

        var beat = RequireAffixBeat(witness.Simulator.Step().Beats, "affix_last_ward");

        Assert.That(beat.Type, Is.EqualTo(CombatBeatType.HpThresholdEffect));
        Assert.That(beat.Value, Is.EqualTo(6f).Within(0.0001f));
        Assert.That(witness.Owner.Barrier, Is.GreaterThanOrEqualTo(6f));
    }

    [Test]
    public void RealExecutionersEdge_LowHealthRuleChangesResolvedDamage()
    {
        var snapshot = new RuntimeCombatContentLookup().Snapshot;
        var healthyTarget = CreateWitness(snapshot, "affix_executioners_edge", "item_iron_sword", seed: 109);
        var lowTarget = CreateWitness(snapshot, "affix_executioners_edge", "item_iron_sword", seed: 109);
        lowTarget.Enemy.TakeDamage((lowTarget.Enemy.MaxHealth * 0.66f) + 0.1f);

        var healthyHit = HitResolutionService.ResolveBasicAttack(
            healthyTarget.Simulator.State,
            healthyTarget.Owner,
            healthyTarget.Enemy);
        var executeHit = HitResolutionService.ResolveBasicAttack(
            lowTarget.Simulator.State,
            lowTarget.Owner,
            lowTarget.Enemy);

        Assert.That(healthyTarget.Owner.HasBehaviorTag(CombatBehaviorTags.ExecuteLowHp), Is.True);
        Assert.That(healthyHit.WasDodged, Is.False);
        Assert.That(executeHit.WasDodged, Is.False);
        Assert.That(healthyHit.Note, Does.Not.Contain("execute"));
        Assert.That(executeHit.Note, Does.Contain("execute"));
        Assert.That(executeHit.Value, Is.GreaterThan(healthyHit.Value * 1.20f),
            "The interpreted execute_low_hp rule must materially change combat damage.");
    }

    [Test]
    public void RealDesperateFocus_HpThresholdFiresInSimulatedBattle()
    {
        var witness = CreateWitness(
            new RuntimeCombatContentLookup().Snapshot,
            "affix_desperate_focus",
            "item_warden_trinket");
        witness.Owner.TakeDamage(witness.Owner.Barrier + (witness.Owner.MaxHealth * 0.61f) + 0.1f);

        var beat = RequireAffixBeat(witness.Simulator.Step().Beats, "affix_desperate_focus");

        Assert.That(beat.Type, Is.EqualTo(CombatBeatType.HpThresholdEffect));
        Assert.That(beat.Value, Is.EqualTo(20f).Within(0.0001f));
        Assert.That(witness.Owner.CurrentEnergy, Is.GreaterThanOrEqualTo(20f));
    }

    [Test]
    public void RealMourningAegis_AllyDeathFiresInSimulatedBattle()
    {
        var witness = CreateWitness(
            new RuntimeCombatContentLookup().Snapshot,
            "affix_mourning_aegis",
            "item_raider_armor",
            includeSacrifice: true);
        Assert.That(witness.Sacrifice, Is.Not.Null);
        witness.Sacrifice!.TakeDamage(Math.Max(0f, witness.Sacrifice.CurrentHealth - 1f));

        var beat = RunUntilAffixBeat(witness.Simulator, "affix_mourning_aegis");

        Assert.That(beat.Type, Is.EqualTo(CombatBeatType.AllyDeathEffect));
        Assert.That(beat.Value, Is.EqualTo(6f).Within(0.0001f));
        Assert.That(witness.Owner.Barrier, Is.GreaterThanOrEqualTo(6f));
    }

    [Test]
    public void RealFirstLight_BattleStartFiresOnSelfOnly()
    {
        var snapshot = new RuntimeCombatContentLookup().Snapshot;
        var baseline = CreateWitness(
            snapshot,
            null,
            "item_warden_trinket",
            includeSacrifice: true);
        var witness = CreateWitness(
            snapshot,
            "affix_first_light",
            "item_warden_trinket",
            includeSacrifice: true);

        var beats = AffixBeats(witness.Simulator.CurrentStep.Beats, "affix_first_light");

        Assert.That(beats, Has.Count.EqualTo(1));
        Assert.That(beats[0].Type, Is.EqualTo(CombatBeatType.BattleStartEffect));
        Assert.That(beats[0].Value, Is.EqualTo(4f).Within(0.0001f));
        Assert.That(beats[0].TargetId, Is.EqualTo(witness.Owner.Id));
        Assert.That(witness.Owner.Barrier, Is.GreaterThanOrEqualTo(4f));
        Assert.That(witness.Sacrifice!.Barrier, Is.EqualTo(baseline.Sacrifice!.Barrier).Within(0.0001f));
    }

    [Test]
    public void RealWarChorus_BattleStartFiresOnAlliedCombatants()
    {
        var witness = CreateWitness(
            new RuntimeCombatContentLookup().Snapshot,
            "affix_war_chorus",
            "item_warden_trinket",
            includeSacrifice: true);

        var beats = AffixBeats(witness.Simulator.CurrentStep.Beats, "affix_war_chorus");

        Assert.That(beats, Has.Count.EqualTo(2));
        Assert.That(beats.All(beat => beat.Type == CombatBeatType.BattleStartEffect), Is.True);
        Assert.That(beats.All(beat => Math.Abs(beat.Value - 3f) <= 0.0001f), Is.True);
        Assert.That(witness.Owner.Barrier, Is.GreaterThanOrEqualTo(3f));
        Assert.That(witness.Sacrifice!.Barrier, Is.GreaterThanOrEqualTo(3f));
    }

    [Test]
    public void RealFallenChorus_AllyDeathFiresInSimulatedBattle()
    {
        var witness = CreateWitness(
            new RuntimeCombatContentLookup().Snapshot,
            "affix_fallen_chorus",
            "item_warden_trinket",
            includeSacrifice: true);
        Assert.That(witness.Sacrifice, Is.Not.Null);
        witness.Owner.TakeDamage(10f);
        var woundedHealth = witness.Owner.CurrentHealth;
        witness.Sacrifice!.TakeDamage(Math.Max(0f, witness.Sacrifice.CurrentHealth - 1f));

        var beat = RunUntilAffixBeat(witness.Simulator, "affix_fallen_chorus");

        Assert.That(beat.Type, Is.EqualTo(CombatBeatType.AllyDeathEffect));
        Assert.That(beat.Value, Is.EqualTo(5f).Within(0.0001f));
        Assert.That(witness.Owner.CurrentHealth, Is.GreaterThan(woundedHealth),
            "The surviving owner must receive actual healing, not only a presentation beat.");
    }

    private static BattleWitness CreateWitness(
        CombatContentSnapshot snapshot,
        string? affixId,
        string itemBaseId,
        bool includeSacrifice = false,
        int seed = 73)
    {
        var loadout = CompileAllies(snapshot, affixId, itemBaseId, includeSacrifice);
        var enemyBuild = BattleSetupBuilder.Build(
            Array.Empty<BattleParticipantSpec>(),
            new BattleEncounterPlan(
                new[]
                {
                    new BattleParticipantSpec(
                        EnemyId,
                        EnemyId,
                        "warden",
                        DeploymentAnchorId.FrontCenter,
                        string.Empty,
                        string.Empty,
                        Array.Empty<BattleEquippedItemSpec>(),
                        Array.Empty<string>()),
                },
                TeamPostureType.StandardAdvance),
            snapshot);
        Assert.That(enemyBuild.IsSuccess, Is.True, enemyBuild.Error);

        var state = BattleFactory.Create(
            loadout.Allies,
            enemyBuild.Enemies,
            seed: seed,
            statusRules: enemyBuild.StatusRules);
        var simulator = new BattleSimulator(state);
        return new BattleWitness(
            simulator,
            loadout,
            state.Allies.Single(unit => unit.Definition.Id == OwnerId),
            state.Allies.SingleOrDefault(unit => unit.Definition.Id == SacrificeId),
            state.Enemies.Single(unit => unit.Definition.Id == EnemyId));
    }

    private static BattleLoadoutSnapshot CompileAllies(
        CombatContentSnapshot snapshot,
        string? affixId,
        string itemBaseId,
        bool includeSacrifice)
    {
        var archetype = snapshot.Archetypes["warden"];
        var heroIds = includeSacrifice
            ? new[] { OwnerId, SacrificeId }
            : new[] { OwnerId };
        var heroes = heroIds
            .Select(heroId => new HeroRecord(
                heroId,
                heroId,
                archetype.Id,
                archetype.RaceId,
                archetype.ClassId,
                string.Empty,
                string.Empty))
            .ToList();

        var ownerItemId = $"{OwnerId}.item.0";
        var affixIds = string.IsNullOrWhiteSpace(affixId)
            ? Array.Empty<string>()
            : new[] { affixId };
        IReadOnlyDictionary<string, float>? magnitudes = null;
        if (!string.IsNullOrWhiteSpace(affixId))
        {
            var template = snapshot.AffixCatalog![affixId];
            magnitudes = new Dictionary<string, float>(StringComparer.Ordinal)
            {
                [affixId] = (template.ValueMin + template.ValueMax) * 0.5f,
            };
        }

        var itemInstances = new Dictionary<string, ItemInstanceState>(StringComparer.Ordinal)
        {
            [ownerItemId] = new ItemInstanceState(
                ownerItemId,
                itemBaseId,
                affixIds,
                OwnerId,
                AffixMagnitudes: magnitudes),
        };
        var heroLoadouts = heroIds.ToDictionary(
            heroId => heroId,
            heroId => new HeroLoadoutState(
                heroId,
                heroId == OwnerId ? new[] { ownerItemId } : Array.Empty<string>(),
                Array.Empty<string>(),
                "board.vanguard",
                Array.Empty<string>(),
                Array.Empty<string>()),
            StringComparer.Ordinal);
        var heroProgressions = heroIds.ToDictionary(
            heroId => heroId,
            heroId => new HeroProgressionState(
                heroId,
                1,
                0,
                Array.Empty<string>(),
                archetype.Skills.Select(skill => skill.Id).ToList()),
            StringComparer.Ordinal);
        var assignments = new Dictionary<DeploymentAnchorId, string>
        {
            [includeSacrifice ? DeploymentAnchorId.BackCenter : DeploymentAnchorId.FrontCenter] = OwnerId,
        };
        if (includeSacrifice)
        {
            assignments[DeploymentAnchorId.FrontCenter] = SacrificeId;
        }

        return new LoadoutCompiler().Compile(
            heroes,
            heroLoadouts,
            heroProgressions,
            itemInstances,
            new Dictionary<string, SkillInstanceState>(StringComparer.Ordinal),
            new Dictionary<string, PassiveBoardSelectionState>(StringComparer.Ordinal),
            new PermanentAugmentLoadoutState("bp.decision-affix-witness", Array.Empty<string>()),
            new SquadBlueprintState(
                "bp.decision-affix-witness",
                "bp.decision-affix-witness",
                TeamPostureType.StandardAdvance,
                "team_tactic_standard_advance",
                assignments,
                heroIds,
                new Dictionary<string, string>(StringComparer.Ordinal),
                null),
            new RunOverlayState(
                0,
                Array.Empty<string>(),
                Array.Empty<string>(),
                LoadoutCompiler.CurrentCompileVersion,
                string.Empty),
            snapshot);
    }

    private static CombatBeat RunUntilAffixBeat(BattleSimulator simulator, string affixId)
    {
        var initial = AffixBeats(simulator.CurrentStep.Beats, affixId).FirstOrDefault();
        if (initial != null)
        {
            return initial;
        }

        while (!simulator.IsFinished)
        {
            var beat = AffixBeats(simulator.Step().Beats, affixId).FirstOrDefault();
            if (beat != null)
            {
                return beat;
            }
        }

        Assert.Fail($"Affix '{affixId}' never fired before the simulated battle ended.");
        return null!;
    }

    private static CombatBeat RequireAffixBeat(IReadOnlyList<CombatBeat>? beats, string affixId)
    {
        var matches = AffixBeats(beats, affixId);
        Assert.That(matches, Has.Count.EqualTo(1), $"Expected exactly one '{affixId}' beat.");
        return matches[0];
    }

    private static IReadOnlyList<CombatBeat> AffixBeats(
        IReadOnlyList<CombatBeat>? beats,
        string affixId)
    {
        return (beats ?? Array.Empty<CombatBeat>())
            .Where(beat => string.Equals(beat.Tag, affixId, StringComparison.Ordinal))
            .ToList();
    }

    private sealed record BattleWitness(
        BattleSimulator Simulator,
        BattleLoadoutSnapshot Loadout,
        UnitSnapshot Owner,
        UnitSnapshot? Sacrifice,
        UnitSnapshot Enemy);
}
