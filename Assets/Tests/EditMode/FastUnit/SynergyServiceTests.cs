using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;

namespace SM.Tests.EditMode;

/// <summary>
/// SynergyService 폴백(authored tier rule 부재 시 안전망)의 티어 breakpoint 가 V1 권위와 일치하는지 고정한다.
/// V1: 세력(race) 2/4 · 직업(class) 2/3 (wiki-combat-v1-index / project_v1_system_authority).
/// 이전 폴백은 세력 2/3 · 직업 2/4 로 swap 돼 있어 시너지 발동 티어가 어긋났다.
/// </summary>
[Category("FastUnit")]
public sealed class SynergyServiceTests
{
    private static BattleUnitLoadout Unit(string id, string race, string classId)
        => CombatTestFactory.CreateUnit(id, race: race, classId: classId);

    private static float RaceBonus(IReadOnlyList<CombatModifierPackage> packages, string race)
        => packages.Single(package => package.SourceId.StartsWith($"race:{race}")).Modifiers.Single().Value;

    private static float ClassBonus(IReadOnlyList<CombatModifierPackage> packages, string classId)
        => packages.Single(package => package.SourceId.StartsWith($"class:{classId}")).Modifiers.Single().Value;

    [Test]
    public void FallbackFactionSynergy_TierTwoActivatesAtFour_NotThree()
    {
        // 3 faction members → tier-1 (2/4 breakpoint, tier-2 not yet)
        var three = SynergyService.BuildForTeam(new[]
        {
            Unit("a", "human", "vanguard"), Unit("b", "human", "duelist"), Unit("c", "human", "ranger"),
        });
        Assert.That(RaceBonus(three, "human"), Is.EqualTo(2f), "3 faction members must stay tier-1 under 세력 2/4");
        Assert.That(three.Any(package => !string.IsNullOrEmpty(package.GrantedTeamRuleId)), Is.False,
            "race@4 미만은 상위 규칙을 부여하지 않는다");

        // 4 faction members → tier-2
        var four = SynergyService.BuildForTeam(new[]
        {
            Unit("a", "human", "vanguard"), Unit("b", "human", "duelist"), Unit("c", "human", "ranger"), Unit("d", "human", "mystic"),
        });
        Assert.That(RaceBonus(four, "human"), Is.EqualTo(4f), "4 faction members reach tier-2");
        Assert.That(four.Single(package => package.SourceId.StartsWith("race:human")).GrantedTeamRuleId,
            Is.EqualTo(TeamRuleSet.PhalanxRuleId));
    }

    [Test]
    public void FallbackClassSynergy_TierTwoActivatesAtThree()
    {
        // 2 class members → tier-1 (race counts stay 1 so no faction synergy noise)
        var two = SynergyService.BuildForTeam(new[]
        {
            Unit("a", "human", "vanguard"), Unit("b", "beastkin", "vanguard"),
        });
        Assert.That(ClassBonus(two, "vanguard"), Is.EqualTo(2f), "2 class members stay tier-1");

        // 3 class members → tier-2 under 직업 2/3
        var three = SynergyService.BuildForTeam(new[]
        {
            Unit("a", "human", "vanguard"), Unit("b", "beastkin", "vanguard"), Unit("c", "undead", "vanguard"),
        });
        Assert.That(ClassBonus(three, "vanguard"), Is.EqualTo(4f), "3 class members reach tier-2 under 직업 2/3");
        Assert.That(three.Any(package => !string.IsNullOrEmpty(package.GrantedTeamRuleId)), Is.False,
            "class@3 규칙은 이번 슬라이스 범위 밖이다");
    }

    [Test]
    public void AuthoredGrantedRule_IsCompiledIntoTeamRuleSet_OnlyWhenActiveAndAuthored()
    {
        const string customRuleId = "rule.test.authored";
        var tier = new TeamSynergyTierRule(
            "test_synergy",
            "test-tag",
            2,
            System.Array.Empty<SM.Core.Stats.StatModifier>(),
            customRuleId);
        var activeUnits = new[]
        {
            Unit("a", "alpha", "one") with { CompileTags = new[] { "test-tag" } },
            Unit("b", "beta", "two") with { CompileTags = new[] { "test-tag" } },
        };
        var activePackages = SynergyService.BuildForTeam(activeUnits, new[] { tier });
        var activeState = BattleFactory.Create(
            activeUnits.Select(unit => unit with { TeamPackages = activePackages }).ToList(),
            new[] { Unit("enemy", "enemy", "enemy") });

        Assert.That(activeState.TeamRuleSet.Has(TeamSide.Ally, customRuleId), Is.True);
        Assert.That(activeState.TeamRuleSet.Has(TeamSide.Enemy, customRuleId), Is.False,
            "TeamRuleSet은 규칙을 부여받은 팀에만 노출한다");

        var belowThresholdPackages = SynergyService.BuildForTeam(activeUnits.Take(1), new[] { tier });
        var belowThresholdState = BattleFactory.Create(
            activeUnits.Take(1).Select(unit => unit with { TeamPackages = belowThresholdPackages }).ToList(),
            new[] { Unit("enemy", "enemy", "enemy") });
        Assert.That(belowThresholdState.TeamRuleSet.Has(TeamSide.Ally, customRuleId), Is.False);

        var statOnlyTier = tier with { GrantedTeamRuleId = string.Empty };
        var statOnlyPackages = SynergyService.BuildForTeam(activeUnits, new[] { statOnlyTier });
        var statOnlyState = BattleFactory.Create(
            activeUnits.Select(unit => unit with { TeamPackages = statOnlyPackages }).ToList(),
            new[] { Unit("enemy", "enemy", "enemy") });
        Assert.That(statOnlyState.TeamRuleSet.Has(TeamSide.Ally, customRuleId), Is.False);
    }
}
