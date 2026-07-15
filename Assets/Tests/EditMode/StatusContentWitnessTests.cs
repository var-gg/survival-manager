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
    public void RealPrimerFamilies_CarryComboPayoffBonus_ThroughCompiledPath()
    {
        // Move 1 make-or-break — 콤보 payoff는 Default(코드 SoT)에만 저작되는데 실게임은 compiler 경로로
        // 돈다. compiler의 Default overlay가 빠지면 compiled(실게임) 규칙에서 payoff가 0으로 떨어져
        // 콤보 추가타가 통째로 무결과가 되는 회귀. 실 committed 콘텐츠를 컴파일해 payoff가 실게임 규칙까지
        // 실리는지 단언한다. (Default 기반 FastUnit 테스트는 이 회귀를 못 잡는다 — 실게임 경로 witness.)
        var snapshot = new RuntimeCombatContentLookup().Snapshot;
        var rules = CombatStatusRuleCompiler.Compile(snapshot);

        foreach (var (statusId, expected) in new[]
                 {
                     ("stun", 0.35f), ("root", 0.25f), ("slow", 0.15f),
                     ("sunder", 0.25f), ("marked", 0.20f), ("exposed", 0.30f),
                 })
        {
            Assert.That(rules.ResolveComboPayoffBonus(statusId), Is.EqualTo(expected).Within(0.0001f),
                $"{statusId}의 콤보 payoff가 compiled(실게임) 경로까지 실려야 한다 — 0이면 compiler overlay 결손으로 " +
                "실게임 콤보 추가타가 무결과로 추락하는 회귀");
        }

        Assert.That(rules.ResolveComboPayoffBonus("guarded"), Is.Zero,
            "프라이머가 아닌 family는 콤보 payoff 0(현행 보존)");
    }

    [Test]
    public void RealMagnitudeChannelFamilies_CarryMagnitudeScale()
    {
        // 숫자 콘텐츠화 2보 — magnitude 직소비 채널(sunder=방어/저항 차감, marked/exposed=받는 피해 가산,
        // wound=치유 감소, slow=공속/이속 감쇠, burn/bleed=주기 틱 피해)의 배율이
        // 실 committed asset 에서 전투 규칙까지 실려야 한다.
        var snapshot = new RuntimeCombatContentLookup().Snapshot;
        var rules = CombatStatusRuleCompiler.Compile(snapshot);

        foreach (var statusId in new[] { "sunder", "marked", "exposed", "wound", "slow", "burn", "bleed" })
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
    public void RealBarrierFamily_CarriesGrantsBarrierOnApply()
    {
        // 효과 종류 데이터화 3보 1슬라이스 — 적용 시 즉시 보호막 전환 kind가 실 committed asset
        // (status_family_barrier.asset)에서 전투 규칙까지 실려야 한다. 실 콘텐츠 5개 스킬
        // (bulwark_core/priest_core/aegis_sentinel_oath/memory_tuning/square_wall)이 이 kind에 의존한다.
        var snapshot = new RuntimeCombatContentLookup().Snapshot;
        var rules = CombatStatusRuleCompiler.Compile(snapshot);

        Assert.That(rules.TryGetStatusFamily("barrier", out var barrier), Is.True,
            "실 콘텐츠에 barrier 상태 패밀리가 존재해야 한다");
        Assert.That(barrier.GrantsBarrierOnApply, Is.True,
            "barrier의 즉시 보호막 전환 kind가 콘텐츠(status_family_barrier.asset)에서 전투 규칙까지 실려야 한다 — " +
            "false면 미저작/파서 결손으로 barrier 스킬이 무효과 잔존 상태로 추락하는 회귀");
        Assert.That(rules.ResolveGrantsBarrierOnApply("barrier"), Is.True);
        Assert.That(rules.ResolveGrantsBarrierOnApply("guarded"), Is.False,
            "즉시 보호막 전환 kind가 다른 family로 새면 안 된다(전환은 barrier 저작 전용)");
    }

    [Test]
    public void RealUnstoppableFamily_CarriesGrantsUnstoppable()
    {
        // 효과 종류 데이터화 3보 3b — 저지불가 kind(하드 컨트롤 적용 면역 + 저작 강제이동 면역)가
        // 실 committed asset(status_family_unstoppable.asset)에서 전투 규칙까지 실려야 한다.
        // 실 콘텐츠 skill_warden_utility(정화 break_and_unstoppable의 unstoppable 부여)가 이 kind에 의존한다.
        var snapshot = new RuntimeCombatContentLookup().Snapshot;
        var rules = CombatStatusRuleCompiler.Compile(snapshot);

        Assert.That(rules.TryGetStatusFamily("unstoppable", out var unstoppable), Is.True,
            "실 콘텐츠에 unstoppable 상태 패밀리가 존재해야 한다");
        Assert.That(unstoppable.GrantsUnstoppable, Is.True,
            "unstoppable의 저지불가 kind가 콘텐츠(status_family_unstoppable.asset)에서 전투 규칙까지 실려야 한다 — " +
            "false면 미저작/파서 결손으로 하드 컨트롤 면역·강제이동 면역이 통째로 꺼지는 회귀");

        var carriers = rules.StatusFamilies.Values
            .Where(rule => rule.GrantsUnstoppable)
            .Select(rule => rule.Id)
            .ToList();
        Assert.That(carriers, Is.EqualTo(new[] { "unstoppable" }),
            "실 콘텐츠에서 저지불가 kind 보유 family는 정확히 unstoppable 하나여야 한다 — 새 family가 " +
            "의도적으로 저지불가를 저작하면 이 목록을 의식적으로 갱신할 것(무의식 누출 가드)");
    }

    [Test]
    public void RealSilenceFamily_CarriesBlocksActiveSkills()
    {
        // 효과 종류 데이터화 3보 3c — 액티브 시전 차단 kind가 실 committed asset
        // (status_family_silence.asset)에서 전투 규칙까지 실려야 한다. 실 콘텐츠 hexer 코어/유틸리티
        // (silence 저작)가 이 kind에 의존한다.
        var snapshot = new RuntimeCombatContentLookup().Snapshot;
        var rules = CombatStatusRuleCompiler.Compile(snapshot);

        Assert.That(rules.TryGetStatusFamily("silence", out var silence), Is.True,
            "실 콘텐츠에 silence 상태 패밀리가 존재해야 한다");
        Assert.That(silence.BlocksActiveSkills, Is.True,
            "silence의 액티브 시전 차단 kind가 콘텐츠(status_family_silence.asset)에서 전투 규칙까지 실려야 한다 — " +
            "false면 미저작/파서 결손으로 침묵이 잔존만 하는 무효과 상태로 추락하는 회귀");

        var carriers = rules.StatusFamilies.Values
            .Where(rule => rule.BlocksActiveSkills)
            .Select(rule => rule.Id)
            .ToList();
        Assert.That(carriers, Is.EqualTo(new[] { "silence" }),
            "실 콘텐츠에서 액티브 차단 kind 보유 family는 정확히 silence 하나여야 한다 — 새 family가 " +
            "의도적으로 침묵을 저작하면 이 목록을 의식적으로 갱신할 것(무의식 누출 가드)");
    }

    [Test]
    public void RealRootFamily_CarriesBlocksMovement()
    {
        // 효과 종류 데이터화 3보 3d — 자발 이동 차단 kind가 실 committed asset
        // (status_family_root.asset)에서 전투 규칙까지 실려야 한다.
        var snapshot = new RuntimeCombatContentLookup().Snapshot;
        var rules = CombatStatusRuleCompiler.Compile(snapshot);

        Assert.That(rules.TryGetStatusFamily("root", out var root), Is.True,
            "실 콘텐츠에 root 상태 패밀리가 존재해야 한다");
        Assert.That(root.BlocksMovement, Is.True,
            "root의 자발 이동 차단 kind가 콘텐츠(status_family_root.asset)에서 전투 규칙까지 실려야 한다 — " +
            "false면 미저작/파서 결손으로 속박이 잔존만 하는 무효과 상태로 추락하는 회귀");

        var carriers = rules.StatusFamilies.Values
            .Where(rule => rule.BlocksMovement)
            .Select(rule => rule.Id)
            .ToList();
        Assert.That(carriers, Is.EqualTo(new[] { "root" }),
            "실 콘텐츠에서 이동 차단 kind 보유 family는 정확히 root 하나여야 한다 — 새 family가 " +
            "의도적으로 속박을 저작하면 이 목록을 의식적으로 갱신할 것(무의식 누출 가드)");
    }

    [Test]
    public void RealStunFamily_CarriesBlocksAction()
    {
        // 효과 종류 데이터화 3보 3e — 행동 차단 kind가 실 committed asset(status_family_stun.asset)에서
        // 전투 규칙까지 실려야 한다. 실 콘텐츠 제어 스킬(스턴 저작)이 이 kind에 의존한다.
        var snapshot = new RuntimeCombatContentLookup().Snapshot;
        var rules = CombatStatusRuleCompiler.Compile(snapshot);

        Assert.That(rules.TryGetStatusFamily("stun", out var stun), Is.True,
            "실 콘텐츠에 stun 상태 패밀리가 존재해야 한다");
        Assert.That(stun.BlocksAction, Is.True,
            "stun의 행동 차단 kind가 콘텐츠(status_family_stun.asset)에서 전투 규칙까지 실려야 한다 — " +
            "false면 미저작/파서 결손으로 기절이 잔존만 하는 무효과 상태로 추락하는 회귀");

        var carriers = rules.StatusFamilies.Values
            .Where(rule => rule.BlocksAction)
            .Select(rule => rule.Id)
            .ToList();
        Assert.That(carriers, Is.EqualTo(new[] { "stun" }),
            "실 콘텐츠에서 행동 차단 kind 보유 family는 정확히 stun 하나여야 한다 — 새 family가 " +
            "의도적으로 행동차단을 저작하면 이 목록을 의식적으로 갱신할 것(무의식 누출 가드)");
    }

    [Test]
    public void RealChannelFamilies_CarryMembership_ExactCarrierSets()
    {
        // 채널 membership 데이터화 3보 3f — 5채널 membership이 실 committed asset에서 전투 규칙까지
        // 실리고, 채널별 carrier가 정확히 현행 배선과 일치해야 한다(무의식 누출/결손 양방향 가드).
        var snapshot = new RuntimeCombatContentLookup().Snapshot;
        var rules = CombatStatusRuleCompiler.Compile(snapshot);

        List<string> Carriers(Func<CombatStatusFamilyRule, bool> hasKind) => rules.StatusFamilies.Values
            .Where(hasKind)
            .Select(rule => rule.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        Assert.That(Carriers(rule => rule.AmplifiesIncomingDamage), Is.EqualTo(new[] { "exposed", "marked" }),
            "받는피해 증폭 채널 carrier는 정확히 exposed/marked여야 한다");
        Assert.That(Carriers(rule => rule.GrantsGuardedDefense), Is.EqualTo(new[] { "guarded" }),
            "수호 delta 채널 carrier는 정확히 guarded여야 한다");
        Assert.That(Carriers(rule => rule.ShredsDefense), Is.EqualTo(new[] { "sunder" }),
            "방어/저항 차감 채널 carrier는 정확히 sunder여야 한다");
        Assert.That(Carriers(rule => rule.ReducesHealing), Is.EqualTo(new[] { "wound" }),
            "치유 감소 채널 carrier는 정확히 wound여야 한다");
        Assert.That(Carriers(rule => rule.DampensTempo), Is.EqualTo(new[] { "slow" }),
            "공속/이속 감쇠 채널 carrier는 정확히 slow여야 한다");
        Assert.That(Carriers(rule => rule.MarksTarget), Is.EqualTo(new[] { "marked" }),
            "타게팅 표식 membership carrier는 정확히 marked여야 한다(3g) — 셀렉터/필터/포커스가 이 kind를 소비");
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
