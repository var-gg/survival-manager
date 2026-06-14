using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Core.Stats;
using SM.Meta.Model;
using SM.Meta.Services;

namespace SM.Tests.EditMode;

/// <summary>
/// SquadSynergyPreview 순수 집계 검증 — 배치 분대 태그 → 시너지 활성/티어/다음 티어.
/// V1 권위 breakpoint(세력 2/4 · 직업 2/3)를 카탈로그 픽스처로 재현.
/// </summary>
[Category("FastUnit")]
public sealed class SquadSynergyPreviewFastTests
{
    private static IReadOnlyDictionary<string, SynergyTierTemplate> Catalog()
    {
        SynergyTierTemplate Tier(string synergyId, string tag, int threshold) =>
            new(
                $"{synergyId}:{threshold}",
                new TeamSynergyTierRule(synergyId, tag, threshold, Array.Empty<StatModifier>()));

        return new Dictionary<string, SynergyTierTemplate>(StringComparer.Ordinal)
        {
            ["synergy_human:2"] = Tier("synergy_human", "human", 2),
            ["synergy_human:4"] = Tier("synergy_human", "human", 4),
            ["synergy_vanguard:2"] = Tier("synergy_vanguard", "vanguard", 2),
            ["synergy_vanguard:3"] = Tier("synergy_vanguard", "vanguard", 3),
        };
    }

    private static IReadOnlyList<string> Unit(params string[] tags) => tags;

    [Test]
    public void EmptyDeployment_YieldsNothing()
    {
        var result = SquadSynergyPreview.Evaluate(new List<IReadOnlyList<string>>(), Catalog());
        Assert.IsEmpty(result);
    }

    [Test]
    public void EmptyCatalog_YieldsNothing()
    {
        var units = new List<IReadOnlyList<string>> { Unit("human"), Unit("human") };
        var result = SquadSynergyPreview.Evaluate(units, new Dictionary<string, SynergyTierTemplate>());
        Assert.IsEmpty(result);
    }

    [Test]
    public void TwoSameRace_ActivatesLowTier_NextIsHighTier()
    {
        var units = new List<IReadOnlyList<string>>
        {
            Unit("human", "vanguard"),
            Unit("human", "ranger"),
        };

        var human = SquadSynergyPreview.Evaluate(units, Catalog())
            .Single(surface => surface.SynergyId == "synergy_human");

        Assert.AreEqual(2, human.CurrentCount);
        Assert.AreEqual(2, human.ActiveThreshold, "2 human → tier 2 발현");
        Assert.AreEqual(4, human.NextThreshold, "다음 티어는 4");
        Assert.IsTrue(human.IsActive);
    }

    [Test]
    public void OneTag_NotActive_ReportsCountAndNextThreshold()
    {
        var units = new List<IReadOnlyList<string>> { Unit("human", "vanguard") };

        var human = SquadSynergyPreview.Evaluate(units, Catalog())
            .Single(surface => surface.SynergyId == "synergy_human");

        Assert.AreEqual(1, human.CurrentCount);
        Assert.AreEqual(0, human.ActiveThreshold);
        Assert.AreEqual(2, human.NextThreshold);
        Assert.IsFalse(human.IsActive);
    }

    [Test]
    public void FourSameRace_ReachesHighTier_NoNext()
    {
        var units = new List<IReadOnlyList<string>>
        {
            Unit("human"), Unit("human"), Unit("human"), Unit("human"),
        };

        var human = SquadSynergyPreview.Evaluate(units, Catalog())
            .Single(surface => surface.SynergyId == "synergy_human");

        Assert.AreEqual(4, human.CurrentCount);
        Assert.AreEqual(4, human.ActiveThreshold);
        Assert.AreEqual(0, human.NextThreshold, "최고 티어 도달 → 다음 없음");
    }

    [Test]
    public void DuplicateTagWithinUnit_CountsOncePerUnit()
    {
        var units = new List<IReadOnlyList<string>> { Unit("human", "human") };

        var human = SquadSynergyPreview.Evaluate(units, Catalog())
            .Single(surface => surface.SynergyId == "synergy_human");

        Assert.AreEqual(1, human.CurrentCount, "유닛 내 중복 태그는 1회만 기여");
        Assert.IsFalse(human.IsActive);
    }

    [Test]
    public void ActiveSynergies_SortBeforeInactive()
    {
        var units = new List<IReadOnlyList<string>>
        {
            Unit("human", "vanguard"),
            Unit("human", "vanguard"),
        };

        var result = SquadSynergyPreview.Evaluate(units, Catalog());

        Assert.AreEqual(2, result.Count(surface => surface.IsActive), "human·vanguard 둘 다 2명 → 둘 다 발현");
        Assert.IsTrue(result[0].IsActive, "활성 시너지가 앞에 정렬");
    }
}
