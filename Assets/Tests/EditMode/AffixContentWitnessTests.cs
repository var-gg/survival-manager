using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Core.Contracts;
using SM.Editor.SeedData;
using SM.Meta;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Unity;

namespace SM.Tests.EditMode;

/// <summary>
/// 실 committed affix 콘텐츠의 non-numeric 계약(rule tag/조건 게이트)이 실 컴파일 유닛까지
/// 도달하는지 단언하는 witness — FastUnit 손조립(ContentEffectDifferentialTests)의 실 asset 쌍.
/// 과거엔 ModifierPackageConverter가 수치만 변환해 affix_relentless(BuildShaping)의 rule tag,
/// affix_farshot(ConditionalTagged)의 RequiredTags가 전량 드롭됐다(조건부가 무조건으로 동작).
/// </summary>
[Category("BatchOnly")]
public sealed class AffixContentWitnessTests
{
    [SetUp]
    public void SetUp()
    {
        SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(AffixContentWitnessTests));
    }

    [Test]
    public void RealAffixCatalog_CarriesRuleAndConditionContracts()
    {
        var snapshot = new RuntimeCombatContentLookup().Snapshot;

        Assert.That(snapshot.AffixCatalog, Is.Not.Null,
            "실 콘텐츠 스냅샷은 AffixCatalog(태그/조건/rule 계약)를 실어야 한다");
        Assert.That(snapshot.AffixCatalog!.ContainsKey("affix_relentless"), Is.True);
        Assert.That(snapshot.AffixCatalog.ContainsKey("affix_farshot"), Is.True);

        var relentless = snapshot.AffixCatalog["affix_relentless"];
        Assert.That(relentless.CompileTags, Does.Contain("tempo"));
        Assert.That(relentless.CompileTags, Does.Contain("execute"));
        Assert.That(relentless.RulePackage, Is.Not.Null,
            "BuildShaping affix의 RuleModifierTags는 rule package로 변환돼야 한다");
        Assert.That(
            relentless.RulePackage!.Modifiers.Any(modifier =>
                modifier.Kind == RuleModifierKind.BehaviorTag && modifier.Value == "tempo"),
            Is.True);

        var farshot = snapshot.AffixCatalog["affix_farshot"];
        Assert.That(farshot.RequiredTags, Does.Contain("projectile"),
            "ConditionalTagged affix의 조건(RequiredTags)이 스냅샷까지 실려야 게이트가 가능하다");
        Assert.That(farshot.IsConditional, Is.True);
    }

    [Test]
    public void RealCompile_RuleAffixReachesUnit_AndUnmetConditionalStaysInert()
    {
        var snapshot = new RuntimeCombatContentLookup().Snapshot;
        Assert.That(snapshot.ItemPackages.ContainsKey("item_raider_armor"), Is.True,
            "전제: canonical 콘텐츠에 item_raider_armor가 존재(균형 sweep 시나리오와 동일 조합)");
        Assert.That(snapshot.Archetypes.ContainsKey("warden"), Is.True);

        var compiled = CompileWarden(snapshot, new[] { "affix_relentless", "affix_farshot" });
        var unit = compiled.Allies.Single();

        // 전제 명시: warden(근접 vanguard)은 projectile 태그가 없어야 farshot 미충족 케이스가 성립한다.
        // 콘텐츠 진화로 이 전제가 깨지면 여기서 명시적으로 드러난다(무음 반전 방지).
        Assert.That(unit.CompileTags, Does.Not.Contain("projectile"),
            "전제 파손: warden이 projectile 태그를 갖게 됐다 — 미충족 케이스 affix를 교체할 것");

        Assert.That(unit.CompileTags, Does.Contain("tempo"),
            "실 affix_relentless의 CompileTags가 컴파일 유닛에 도달해야 한다");
        Assert.That(unit.CompileTags, Does.Contain("execute"));
        Assert.That(
            (unit.RulePackages ?? Array.Empty<CombatRuleModifierPackage>())
                .Any(package => package.SourceId == "affix_relentless"),
            Is.True,
            "실 affix_relentless의 rule package가 컴파일 유닛에 도달해야 한다");

        Assert.That(unit.NumericPackages.Any(package => package.SourceId == "affix_relentless"), Is.True,
            "비조건 affix 수치는 그대로 적용");
        Assert.That(unit.NumericPackages.Any(package => package.SourceId == "affix_farshot"), Is.False,
            "미충족 조건부 affix(projectile 요구) 수치는 이제 게이트로 차단돼야 한다");
    }

    private static BattleLoadoutSnapshot CompileWarden(CombatContentSnapshot content, IReadOnlyList<string> affixIds)
    {
        var archetype = content.Archetypes["warden"];
        var heroes = new List<HeroRecord>
        {
            new("hero.witness", "hero.witness", archetype.Id, archetype.RaceId, archetype.ClassId, string.Empty, string.Empty),
        };
        var itemInstances = new Dictionary<string, ItemInstanceState>(StringComparer.Ordinal)
        {
            ["hero.witness.item.0"] = new("hero.witness.item.0", "item_raider_armor", affixIds, "hero.witness"),
        };
        var heroLoadouts = new Dictionary<string, HeroLoadoutState>(StringComparer.Ordinal)
        {
            ["hero.witness"] = new(
                "hero.witness",
                new[] { "hero.witness.item.0" },
                Array.Empty<string>(),
                "board.vanguard",
                Array.Empty<string>(),
                Array.Empty<string>()),
        };
        var heroProgressions = new Dictionary<string, HeroProgressionState>(StringComparer.Ordinal)
        {
            ["hero.witness"] = new("hero.witness", 1, 0, Array.Empty<string>(), archetype.Skills.Select(skill => skill.Id).ToList()),
        };

        return new LoadoutCompiler().Compile(
            heroes,
            heroLoadouts,
            heroProgressions,
            itemInstances,
            new Dictionary<string, SkillInstanceState>(StringComparer.Ordinal),
            new Dictionary<string, PassiveBoardSelectionState>(StringComparer.Ordinal),
            new PermanentAugmentLoadoutState("bp.witness", Array.Empty<string>()),
            new SquadBlueprintState(
                "bp.witness",
                "bp.witness",
                TeamPostureType.StandardAdvance,
                "team_tactic_standard_advance",
                new Dictionary<DeploymentAnchorId, string> { [DeploymentAnchorId.FrontCenter] = "hero.witness" },
                new[] { "hero.witness" },
                new Dictionary<string, string>(StringComparer.Ordinal),
                null),
            new RunOverlayState(0, Array.Empty<string>(), Array.Empty<string>(), LoadoutCompiler.CurrentCompileVersion, string.Empty),
            content);
    }
}
