using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;
using SM.Editor.SeedData;
using SM.Meta;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Persistence.Abstractions.Models;
using SM.Unity;

namespace SM.Tests.EditMode;

/// <summary>
/// 실 committed 상태이상 콘텐츠의 튜닝값이 전투 규칙(CombatStatusRules)까지 도달하는지 단언하는
/// witness — "상태이상 숫자 콘텐츠화" 1차(guarded 받는피해 delta의 리터럴→콘텐츠 승격) 실 asset 쌍.
/// 과거 guarded 감소율은 UnitSnapshot에 -0.1 리터럴로 박혀 있어 에디터 없이 튜닝이 불가능했다.
/// </summary>
[Category("BatchOnly")]
public sealed class StatusContentWitnessTests
{
    [SetUp]
    public void SetUp()
    {
        SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(StatusContentWitnessTests));
    }

    [Test]
    public void RealGuardedFamily_CarriesIncomingDamageDelta()
    {
        var snapshot = new RuntimeCombatContentLookup().Snapshot;
        var rules = CombatStatusRuleCompiler.Compile(snapshot);

        Assert.That(rules.TryGetStatusFamily("guarded", out var guarded), Is.True,
            "실 콘텐츠에 guarded 상태 패밀리가 존재해야 한다");
        Assert.That(guarded.IncomingDamageDelta, Is.EqualTo(-0.1f).Within(0.0001f),
            "guarded의 받는피해 delta(-0.1)가 콘텐츠(status_family_guarded.asset)에서 전투 규칙까지 실려야 한다 — " +
            "0이면 콘텐츠 미저작으로 guarded가 무효과가 되는 회귀");
        Assert.That(rules.ResolveIncomingDamageDelta("guarded"), Is.EqualTo(-0.1f).Within(0.0001f));
    }

    [Test]
    public void RealMagnitudeChannelFamilies_CarryMagnitudeScale()
    {
        // 숫자 콘텐츠화 2보 — magnitude 직소비 채널(sunder=방어/저항 차감, marked/exposed=받는 피해 가산,
        // wound=치유 감소, slow=공속/이속 감쇠)의 배율이 실 committed asset 에서 전투 규칙까지 실려야 한다.
        var snapshot = new RuntimeCombatContentLookup().Snapshot;
        var rules = CombatStatusRuleCompiler.Compile(snapshot);

        foreach (var statusId in new[] { "sunder", "marked", "exposed", "wound", "slow" })
        {
            Assert.That(rules.TryGetStatusFamily(statusId, out var rule), Is.True,
                $"실 콘텐츠에 {statusId} 상태 패밀리가 존재해야 한다");
            Assert.That(rule.MagnitudeScale, Is.EqualTo(1f).Within(0.0001f),
                $"{statusId}의 MagnitudeScale(1)이 콘텐츠(status_family_{statusId}.asset)에서 전투 규칙까지 실려야 한다 — " +
                "0이면 미저작/파서 결손으로 해당 숫자 채널이 통째로 무효화되는 회귀");
            Assert.That(rules.ResolveMagnitudeScale(statusId), Is.EqualTo(1f).Within(0.0001f));
        }
    }

    [Test]
    public void RealSkillAppliedStatus_LandsOnEnemy_InRealSim()
    {
        // 오너 의심(2026-07-12) 실측 응답: "스킬에서 상태이상 적용이 미구현 아닌가" —
        // 실 raider(코어 스킬 marked 3s 저작)를 컴파일해 실 BattleSimulator에서 적에게
        // marked가 실제로 랜딩하는지 단언한다. 메커니즘+실 콘텐츠 사슬의 end-to-end witness.
        var snapshot = new RuntimeCombatContentLookup().Snapshot;
        Assert.That(snapshot.Archetypes.ContainsKey("raider"), Is.True);

        var compiled = CompileSingleHero(snapshot, "raider");
        var enemy = CombatTestFactory.CreateUnit(
            "enemy.dummy",
            race: "undead",
            classId: "vanguard",
            anchor: DeploymentAnchorId.FrontBottom,
            hp: 800f,
            attack: 1f);
        var state = CombatTestFactory.CreateBattleState(
            compiled.Allies,
            new[] { enemy },
            seed: 7,
            statusRules: CombatStatusRuleCompiler.Compile(snapshot));
        var simulator = new BattleSimulator(state, 900);

        var markedLanded = false;
        var guard = 0;
        while (!simulator.IsFinished && guard++ < 900)
        {
            simulator.Step();
            if (state.Enemies.Any(unit => unit.HasStatus("marked")))
            {
                markedLanded = true;
                break;
            }
        }

        Assert.That(markedLanded, Is.True,
            "실 raider 코어 스킬의 AppliedStatuses(marked)가 실 전투에서 적에게 랜딩해야 한다 — " +
            "저작→컴파일→시전→StatusResolutionService 사슬의 실 콘텐츠 witness");
    }

    [Test]
    public void ClassIdentityUtilities_AuthorTheirSignatureStatus()
    {
        // 이름부터 상태이상인 유틸리티(mark_followup/bleed_followup/hexer_silence)가
        // 빈 껍데기였던 결함의 회귀 방지 계약.
        var snapshot = new RuntimeCombatContentLookup().Snapshot;
        var expectations = new (string SkillId, string StatusId)[]
        {
            ("skill_raider_utility", "marked"),
            ("skill_slayer_utility", "bleed"),
            ("skill_hexer_utility", "silence"),
        };
        foreach (var (skillId, statusId) in expectations)
        {
            var skill = snapshot.SkillCatalog[skillId];
            Assert.That(
                (skill.AppliedStatuses ?? Array.Empty<StatusApplicationSpec>()).Any(status => status.StatusId == statusId),
                Is.True,
                $"{skillId}는 정체성 상태({statusId})를 저작해야 한다");
        }
    }

    private static BattleLoadoutSnapshot CompileSingleHero(CombatContentSnapshot content, string archetypeId)
    {
        var archetype = content.Archetypes[archetypeId];
        var heroes = new List<HeroRecord>
        {
            new("hero.witness", "hero.witness", archetype.Id, archetype.RaceId, archetype.ClassId, string.Empty, string.Empty),
        };
        var heroLoadouts = new Dictionary<string, HeroLoadoutState>(StringComparer.Ordinal)
        {
            ["hero.witness"] = new(
                "hero.witness",
                Array.Empty<string>(),
                Array.Empty<string>(),
                $"board.{archetype.ClassId}",
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
            new Dictionary<string, ItemInstanceState>(StringComparer.Ordinal),
            new Dictionary<string, SkillInstanceState>(StringComparer.Ordinal),
            new Dictionary<string, PassiveBoardSelectionState>(StringComparer.Ordinal),
            new PermanentAugmentLoadoutState("bp.status_witness", Array.Empty<string>()),
            new SquadBlueprintState(
                "bp.status_witness",
                "bp.status_witness",
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
