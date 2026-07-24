using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Core.Stats;
using SM.Meta;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Tests.EditMode.Fakes;

namespace SM.Tests.EditMode;

/// <summary>
/// 무한 순환 순수 규칙(EndlessCycleService) 계약 — 사이클 전이·Heat 패키지·Echo 스케일.
/// 공유 static(EndlessCycleStateRecord.Empty)의 Modifiers dict가 전이에서 오염되지 않음을 함께 잠근다
/// (세이브 로드 Empty 오염과 동계열 함정, SaveLoadSharedRecordIsolationFastTests 참조).
/// </summary>
[Category("FastUnit")]
public sealed class EndlessCycleServiceFastTests
{
    [Test]
    public void BeginNextCycle_IncrementsCycleAndHeat_WithoutMutatingSharedEmpty()
    {
        var first = EndlessCycleService.BeginNextCycle(EndlessCycleStateRecord.Empty);
        Assert.That(first.CycleIndex, Is.EqualTo(1));
        Assert.That(first.Heat, Is.EqualTo(1));

        var second = EndlessCycleService.BeginNextCycle(first);
        Assert.That(second.CycleIndex, Is.EqualTo(2));
        Assert.That(second.Heat, Is.EqualTo(2));

        // 전이 결과의 Modifiers는 독립 인스턴스 — 쓰기가 전역 Empty에 새지 않는다.
        first.Modifiers["probe"] = 1;
        Assert.That(EndlessCycleStateRecord.Empty.Modifiers, Is.Empty,
            "공유 static Empty의 dict는 전이/쓰기 후에도 무결해야 한다.");
        Assert.That(second.Modifiers, Is.Empty, "전이는 이전 상태의 dict를 복사하므로 이후 쓰기와 무관.");
    }

    [Test]
    public void BeginNextCycle_NullInput_StartsFromCycleOne()
    {
        var next = EndlessCycleService.BeginNextCycle(null);
        Assert.That(next.CycleIndex, Is.EqualTo(1));
        Assert.That(next.Heat, Is.EqualTo(1));
    }

    [Test]
    public void BuildEnemyHeatPackages_ZeroHeat_IsEmpty_StoryPathPreserved()
    {
        Assert.That(EndlessCycleService.BuildEnemyHeatPackages(0), Is.Empty);
        Assert.That(EndlessCycleService.BuildEnemyHeatPackages(-1), Is.Empty);
        Assert.That(EndlessCycleService.BuildEnemyHeatSecondaryPressurePackages(0), Is.Empty);
        Assert.That(EndlessCycleService.BuildEnemyHeatSecondaryPressurePackages(-1), Is.Empty);
    }

    [TestCase(1)]
    [TestCase(3)]
    [TestCase(5)]
    [TestCase(8)]
    public void SecondaryPressureFraction_ZeroScale_EmitsNoRulePackage(int heat)
    {
        Assert.That(EndlessCycleService.HeatSecondaryPressureScale, Is.Zero);
        Assert.That(EndlessCycleService.SecondaryPressureFraction(heat), Is.Zero);
        Assert.That(EndlessCycleService.BuildEnemyHeatSecondaryPressurePackages(heat), Is.Empty);
    }

    [TestCase(1)]
    [TestCase(5)]
    [TestCase(10)]
    [TestCase(100)]
    public void EnemyHealthMultiplier_FutureCap_DoesNotBindShippedHeat(int heat)
    {
        var package = EndlessCycleService.BuildEnemyHeatPackages(heat).Single();
        var maxHealth = package.Modifiers.Single(value => value.Stat == StatKey.MaxHealth);
        Assert.That(EndlessCycleService.HeatMaxHealthCapHeat, Is.EqualTo(int.MaxValue));
        Assert.That(
            maxHealth.Value,
            Is.EqualTo(0.10f * heat)
                .Within(0.000001f));
    }

    [TestCase(1)]
    [TestCase(3)]
    [TestCase(5)]
    [TestCase(8)]
    public void BuildEnemyHeatPackages_IsNumericallyIdenticalToShippedPackage(int heat)
    {
        var packages = EndlessCycleService.BuildEnemyHeatPackages(heat);
        Assert.That(packages.Count, Is.EqualTo(1));

        var package = packages[0];
        Assert.That(package.SourceId, Is.EqualTo($"endless_heat:h{heat}"),
            "강화 출처가 sourceId로 읽혀야 전투 로그/리플레이에서 추적 가능.");

        var byStat = package.Modifiers.ToDictionary(modifier => modifier.Stat, modifier => modifier);
        Assert.That(byStat[StatKey.MaxHealth].Op, Is.EqualTo(ModifierOp.Increased));
        Assert.That(byStat[StatKey.MaxHealth].Value,
            Is.EqualTo(0.10f * heat).Within(0.0001f));
        Assert.That(byStat[StatKey.PhysPower].Value,
            Is.EqualTo(0.06f * heat).Within(0.0001f));
        Assert.That(byStat[StatKey.MagPower].Value,
            Is.EqualTo(0.06f * heat).Within(0.0001f));
    }

    [TestCase(1)]
    [TestCase(5)]
    [TestCase(10)]
    public void EnemyHeatPackages_ThroughProductionChannel_ReachMeasuredEnemyStats(int heat)
    {
        var enemy = new BattleUnitLoadout(
            "enemy.heat-probe",
            "Heat Probe",
            "human",
            "vanguard",
            DeploymentAnchorId.FrontCenter,
            new Dictionary<StatKey, float>
            {
                [StatKey.MaxHealth] = 100f,
                [StatKey.PhysPower] = 40f,
                [StatKey.MagPower] = 25f,
            },
            Array.Empty<UnitRuleChain>(),
            Array.Empty<BattleSkillSpec>());
        var applied = PoliticalCombatConditionService.ApplyEnemyPackages(
            new[] { enemy },
            EndlessCycleService.BuildEnemyHeatPackages(heat)).Single();
        var measured = HeroEffectiveStatPreview.Resolve(
                applied,
                new[] { StatKey.MaxHealth, StatKey.PhysPower, StatKey.MagPower })
            .ToDictionary(value => value.Key, value => value);

        Assert.That(
            measured[StatKey.MaxHealth].EffectiveValue / measured[StatKey.MaxHealth].BaseValue,
            Is.EqualTo(1f + (EndlessCycleService.HeatMaxHealthIncreasedPerHeat * Math.Min(heat, EndlessCycleService.HeatMaxHealthCapHeat))).Within(0.00001f));
        Assert.That(
            measured[StatKey.PhysPower].EffectiveValue / measured[StatKey.PhysPower].BaseValue,
            Is.EqualTo(1f + (EndlessCycleService.HeatPrimaryPowerIncreasedPerHeat * heat)).Within(0.00001f));
        Assert.That(
            measured[StatKey.MagPower].EffectiveValue / measured[StatKey.MagPower].BaseValue,
            Is.EqualTo(1f + (EndlessCycleService.HeatPrimaryPowerIncreasedPerHeat * heat)).Within(0.00001f));
        Assert.That(
            applied.NumericPackages.Select(package => package.SourceId),
            Does.Contain($"endless_heat:h{heat}"));
    }

    [Test]
    public void DropLatentMeanShift_UsesSaturatingFormula_AndPreservesHeatZero()
    {
        Assert.That(EndlessCycleService.DropLatentMeanShift(0), Is.EqualTo(0d));
        Assert.That(EndlessCycleService.DropLatentMeanShift(-1), Is.EqualTo(0d));
        Assert.That(EndlessCycleService.DropLatentMeanShift(1), Is.EqualTo(0.15d / 1.15d).Within(0.00000001d));
        Assert.That(EndlessCycleService.DropLatentMeanShift(5), Is.EqualTo(0.75d / 1.75d).Within(0.00000001d));
        Assert.That(EndlessCycleService.DropLatentMeanShift(10), Is.EqualTo(1.50d / 2.5d).Within(0.00000001d));
    }

    [TestCase(0, 0.011d)]
    [TestCase(1, 0.013d)]
    [TestCase(2, 0.015d)]
    [TestCase(3, 0.017d)]
    [TestCase(5, 0.021d)]
    [TestCase(8, 0.027d)]
    [TestCase(10, 0.031d)]
    public void DropJackpotWeight_UsesStepAndBothCaps(
        int heat,
        double expected)
    {
        Assert.That(
            EndlessCycleService.DropJackpotWeight(0.011d, heat),
            Is.EqualTo(expected).Within(0.000000000001d));
    }

    [Test]
    public void DropJackpotWeight_AbsoluteCapBindsForHighBaseWeight()
    {
        Assert.That(
            EndlessCycleService.DropJackpotWeight(0.15d, 25),
            Is.EqualTo(0.20d).Within(0.000000000001d));
    }

    [Test]
    public void BattleContextHash_DivergesPerCycle_AndStoryPathIsDeterministic()
    {
        var lookup = EditorFreeCombatContentFixture.CreateRunLoopLookup();
        Assert.That(lookup.TryGetCombatSnapshot(out var snapshot, out _), Is.True);
        var resolver = new EncounterResolutionService(snapshot);

        var blueprint = new SquadBlueprintState(
            BlueprintId: "bp.endless.hash",
            DisplayName: "Endless Hash Probe",
            TeamPosture: TeamPostureType.StandardAdvance,
            TeamTacticId: string.Empty,
            DeploymentAssignments: new Dictionary<DeploymentAnchorId, string>(),
            ExpeditionSquadHeroIds: Array.Empty<string>(),
            HeroRoleIds: new Dictionary<string, string>());
        var storyRun = RunStateService.StartRun("site_alpha_gate", blueprint, isQuickBattle: false);

        var story = resolver.BuildBattleContext(storyRun, "chapter_alpha", "site_alpha_gate", 0);
        var storyAgain = resolver.BuildBattleContext(storyRun, "chapter_alpha", "site_alpha_gate", 0);
        var cycle1 = resolver.BuildBattleContext(storyRun with { EndlessCycleIndex = 1 }, "chapter_alpha", "site_alpha_gate", 0);
        var cycle2 = resolver.BuildBattleContext(storyRun with { EndlessCycleIndex = 2 }, "chapter_alpha", "site_alpha_gate", 0);

        // 스토리(cycle 0) 결정성 — 같은 콘텐츠 좌표 = 같은 hash/seed (기존 골든/세이브 보존).
        Assert.That(story.BattleContextHash, Is.EqualTo(storyAgain.BattleContextHash));
        Assert.That(story.BattleSeed, Is.EqualTo(storyAgain.BattleSeed));

        // 회차별 분화 — 같은 노드 재방문인데 회차마다 hash/seed가 다르다(RewardCommitId도 이 hash에서 파생).
        Assert.That(cycle1.BattleContextHash, Is.Not.EqualTo(story.BattleContextHash));
        Assert.That(cycle2.BattleContextHash, Is.Not.EqualTo(story.BattleContextHash));
        Assert.That(cycle1.BattleContextHash, Is.Not.EqualTo(cycle2.BattleContextHash));
        Assert.That(cycle1.BattleSeed, Is.Not.EqualTo(story.BattleSeed));
    }

    [Test]
    public void ScaleEchoAmount_ScalesUpWithHeat_AndPreservesStoryPath()
    {
        Assert.That(EndlessCycleService.ScaleEchoAmount(2, 0), Is.EqualTo(2), "스토리(heat 0)는 원값.");
        Assert.That(EndlessCycleService.ScaleEchoAmount(0, 5), Is.EqualTo(0), "0 지급은 스케일 없음.");

        // base 2, heat 5 → 2 + round(2 * 0.15 * 5) = 4
        Assert.That(EndlessCycleService.ScaleEchoAmount(2, 5), Is.EqualTo(4));
        // base 10, heat 10 → 10 + round(15) = 25
        Assert.That(EndlessCycleService.ScaleEchoAmount(10, 10), Is.EqualTo(25));
    }
}
