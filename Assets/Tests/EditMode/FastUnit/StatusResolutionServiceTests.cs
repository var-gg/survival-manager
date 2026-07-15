using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Content;
using SM.Core.Stats;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class StatusResolutionServiceTests
{
    [Test]
    public void ChannelMembership_SumsAcrossFamilies_WithinChannel()
    {
        // 3f 합산 규칙(오너 비준 2026-07-12)의 실행 스펙 — 채널 내 family 간 **가산**, family 내 Max
        // (스택 병합), 클램프 코드 소유, 가산 순서 id ordinal. canonical id가 아닌 신규 family("brand"/
        // "ward")가 같은 채널에 저작되면 각자 가산된다 — 채널 membership이 문자열 id에서 해방됐다는 증명.
        var rules = new CombatStatusRules(
            new Dictionary<string, CombatStatusFamilyRule>
            {
                ["marked"] = CombatStatusRules.Default.StatusFamilies["marked"],
                ["brand"] = new("brand", StatusGroupValue.TacticalMark, false, false, false, 0f, false, false,
                    AmplifiesIncomingDamage: true, MagnitudeScale: 1f),
                ["guarded"] = CombatStatusRules.Default.StatusFamilies["guarded"],
                ["ward"] = new("ward", StatusGroupValue.DefensiveBoon, false, false, false, 0f, false, false,
                    GrantsGuardedDefense: true, IncomingDamageDelta: -0.05f),
                ["sunder"] = CombatStatusRules.Default.StatusFamilies["sunder"],
                ["rend"] = new("rend", StatusGroupValue.Attrition, false, false, false, 0f, false, false,
                    ShredsDefense: true, MagnitudeScale: 1f),
            },
            null,
            null);
        var state = CombatTestFactory.CreateBattleState(
            new[] { CombatTestFactory.CreateUnit("actor") },
            new[] { CombatTestFactory.CreateUnit("enemy") },
            statusRules: rules);
        var actor = state.Allies.Single();

        actor.ApplyStatus(new StatusApplicationSpec("t:marked", "marked", 5f, 0.2f));
        actor.ApplyStatus(new StatusApplicationSpec("t:brand", "brand", 5f, 0.3f));
        Assert.That(actor.GetIncomingDamageMultiplier(), Is.EqualTo(1.5f).Within(0.0001f),
            "같은 증폭 채널의 두 family(marked 0.2 + brand 0.3)는 가산돼야 한다(1 + 0.2 + 0.3)");

        actor.ApplyStatus(new StatusApplicationSpec("t:guarded", "guarded", 5f, 1f));
        actor.ApplyStatus(new StatusApplicationSpec("t:ward", "ward", 5f, 1f));
        Assert.That(actor.GetIncomingDamageMultiplier(), Is.EqualTo(1.35f).Within(0.0001f),
            "수호 채널의 두 family delta(-0.1 + -0.05)도 가산돼야 한다(1 + 0.5 - 0.15)");

        var baseArmor = actor.Stats.Get(StatKey.Armor);
        actor.ApplyStatus(new StatusApplicationSpec("t:sunder", "sunder", 5f, 2f));
        actor.ApplyStatus(new StatusApplicationSpec("t:rend", "rend", 5f, 3f));
        Assert.That(actor.Armor, Is.EqualTo(Math.Max(0f, baseArmor - 5f)).Within(0.0001f),
            "방어 차감 채널의 두 family(sunder 2 + rend 3)는 가산 차감돼야 한다(바닥 0은 코드 소유)");
    }

    [Test]
    public void ReapplyPolicy_UsesSingleSlotMaxMagnitudeBoundedStacksAndLatestSource()
    {
        var weakBurn = new BattleSkillSpec(
            "skill.burn.weak",
            "skill.burn.weak",
            SkillKind.Utility,
            0f,
            1f,
            AppliedStatuses: new[] { new StatusApplicationSpec("apply.burn.weak", "burn", 2f, 2f, 3) });
        var strongBurn = new BattleSkillSpec(
            "skill.burn.strong",
            "skill.burn.strong",
            SkillKind.Utility,
            0f,
            1f,
            AppliedStatuses: new[] { new StatusApplicationSpec("apply.burn.strong", "burn", 1f, 5f, 3) });
        var actorA = CombatTestFactory.CreateUnit("actor_a", classId: "mystic", skills: new[] { weakBurn });
        var actorB = CombatTestFactory.CreateUnit("actor_b", classId: "mystic", skills: new[] { strongBurn });
        var targetLoadout = CombatTestFactory.CreateUnit("target");
        var state = CombatTestFactory.CreateBattleState(new[] { actorA, actorB }, new[] { targetLoadout });
        var firstActor = state.Allies[0];
        var secondActor = state.Allies[1];
        var target = state.Enemies.Single();
        var events = new List<BattleEvent>();

        StatusResolutionService.ApplySkillStatuses(state, firstActor, target, weakBurn, events);
        StatusResolutionService.ApplySkillStatuses(state, secondActor, target, strongBurn, events);

        var burn = target.Statuses.Single(status => status.StatusId == "burn");
        Assert.That(burn.Stacks, Is.EqualTo(2));
        Assert.That(burn.RemainingSeconds, Is.EqualTo(2f).Within(0.001f));
        Assert.That(burn.DurationSeconds, Is.EqualTo(2f).Within(0.001f));
        Assert.That(burn.Magnitude, Is.EqualTo(5f).Within(0.001f));
        Assert.That(burn.SourceActorId, Is.EqualTo(secondActor.Id.Value));
        Assert.That(burn.SourceSkillId, Is.EqualTo("skill.burn.strong"));
        Assert.That(burn.SourceApplicationId, Is.EqualTo("apply.burn.strong"));
    }

    [Test]
    public void StatusPotency_ScalesAppliedStatusMagnitude_AtSkillChokePoint()
    {
        // Move 5 아이템=동사 증폭: 적용자(applier) status_potency가 스킬 AppliedStatuses의 magnitude를
        // ×(1+potency)로 키운다. 스킬 캐스팅 sim 동역학에 의존하지 않도록 public choke point를 직접 태운다.
        // potency 없는 적용자는 완전 항등(0.5), 0.2 적용자는 0.6 — 저장 magnitude와 StatusApplied 이벤트 둘 다.
        var markSkill = new BattleSkillSpec(
            "skill.mark",
            "skill.mark",
            SkillKind.Utility,
            0f,
            1f,
            AppliedStatuses: new[] { new StatusApplicationSpec("apply.marked", "marked", 5f, 0.5f) });
        var potentBase = CombatTestFactory.CreateUnit("actor_potent", classId: "mystic", skills: new[] { markSkill });
        var potentStats = new Dictionary<StatKey, float>(potentBase.BaseStats)
        {
            [StatKey.StatusPotency] = 0.2f,
        };
        var potentActor = potentBase with { BaseStats = potentStats };
        var plainActor = CombatTestFactory.CreateUnit("actor_plain", classId: "mystic", skills: new[] { markSkill });
        var state = CombatTestFactory.CreateBattleState(
            new[] { potentActor, plainActor },
            new[] { CombatTestFactory.CreateUnit("target_a"), CombatTestFactory.CreateUnit("target_b") });
        var potent = state.Allies[0];
        var plain = state.Allies[1];
        var targetA = state.Enemies[0];
        var targetB = state.Enemies[1];
        var events = new List<BattleEvent>();

        StatusResolutionService.ApplySkillStatuses(state, potent, targetA, markSkill, events);
        StatusResolutionService.ApplySkillStatuses(state, plain, targetB, markSkill, events);

        Assert.That(targetA.Statuses.Single(status => status.StatusId == "marked").Magnitude, Is.EqualTo(0.6f).Within(0.0001f),
            "적용자 status_potency 0.2가 choke point에서 marked magnitude 0.5를 ×1.2해 0.6으로 저장해야 한다");
        Assert.That(targetB.Statuses.Single(status => status.StatusId == "marked").Magnitude, Is.EqualTo(0.5f).Within(0.0001f),
            "potency 없는 적용자는 magnitude 항등(0.5)이어야 한다");
        Assert.That(
            events.First(@event => @event.EventKind == BattleEventKind.StatusApplied && @event.TargetId == targetA.Id).Value,
            Is.EqualTo(0.6f).Within(0.0001f),
            "StatusApplied 이벤트 값도 스케일된 magnitude를 실어 관전/텔레메트리와 정합해야 한다");
    }

    [Test]
    public void PeriodicDamageTelemetry_AttributesTickToStatusSource()
    {
        var burnSkill = new BattleSkillSpec(
            "skill.burn",
            "skill.burn",
            SkillKind.Utility,
            0f,
            1f,
            AppliedStatuses: new[] { new StatusApplicationSpec("apply.burn", "burn", 2f, 3f) });
        var actorLoadout = CombatTestFactory.CreateUnit("actor", classId: "mystic", skills: new[] { burnSkill });
        var targetLoadout = CombatTestFactory.CreateUnit("target", hp: 20f);
        var state = CombatTestFactory.CreateBattleState(new[] { actorLoadout }, new[] { targetLoadout });
        var actor = state.Allies.Single();
        var target = state.Enemies.Single();
        var events = new List<BattleEvent>();

        StatusResolutionService.ApplySkillStatuses(state, actor, target, burnSkill, events);
        events.Clear();
        StatusResolutionService.AdvanceStatuses(state, events);

        var tick = events.Single(@event => @event.Note == "status_tick");
        Assert.That(tick.ActorId, Is.EqualTo(actor.Id));
        Assert.That(tick.TargetId, Is.EqualTo(target.Id));
        Assert.That(tick.PayloadId, Is.EqualTo("burn"));

        var telemetry = state.TelemetryEvents.Last(record => record.StringValueA == "status_tick");
        Assert.That(telemetry.Actor!.UnitInstanceId, Is.EqualTo(actor.Id.Value));
        Assert.That(telemetry.Target!.UnitInstanceId, Is.EqualTo(target.Id.Value));
        Assert.That(telemetry.StatusId, Is.EqualTo("burn"));
        Assert.That(telemetry.Explain!.SourceContentId, Is.EqualTo("burn"));
    }

    [Test]
    public void HardControl_TenacityAndDrWindow_ReduceReappliedDuration()
    {
        var targetBase = CombatTestFactory.CreateUnit("target");
        var targetStats = new Dictionary<StatKey, float>(targetBase.BaseStats)
        {
            [StatKey.Tenacity] = 0.5f,
        };
        var targetLoadout = targetBase with { BaseStats = targetStats };

        var stunSkill = new BattleSkillSpec(
            "skill.stun",
            "skill.stun",
            SkillKind.Utility,
            0f,
            1f,
            AppliedStatuses: new[] { new StatusApplicationSpec("apply.stun", "stun", 2f, 0f) });
        var actorLoadout = CombatTestFactory.CreateUnit("actor", classId: "mystic", skills: new[] { stunSkill });
        var state = CombatTestFactory.CreateBattleState(new[] { actorLoadout }, new[] { targetLoadout });
        var actor = state.Allies.Single();
        var target = state.Enemies.Single();
        var applyEvents = new List<BattleEvent>();

        StatusResolutionService.ApplySkillStatuses(state, actor, target, stunSkill, applyEvents);

        Assert.That(target.HasStatus("stun"), Is.True);
        Assert.That(target.Statuses.Single(status => status.StatusId == "stun").RemainingSeconds, Is.EqualTo(1f).Within(0.001f));

        var advanceEvents = new List<BattleEvent>();
        for (var index = 0; index < 10; index++)
        {
            StatusResolutionService.AdvanceStatuses(state, advanceEvents);
        }

        Assert.That(target.HasStatus("stun"), Is.False);
        Assert.That(target.ControlResistWindow, Is.Not.Null);
        Assert.That(advanceEvents.Any(@event => @event.EventKind == BattleEventKind.ControlResistApplied && @event.PayloadId == "stun"), Is.True);

        applyEvents.Clear();
        StatusResolutionService.ApplySkillStatuses(state, actor, target, stunSkill, applyEvents);

        Assert.That(target.Statuses.Single(status => status.StatusId == "stun").RemainingSeconds, Is.EqualTo(0.5f).Within(0.001f));
    }

    [Test]
    public void BreakAndUnstoppable_CleansesHardControl_AndAppliesUnstoppableWindow()
    {
        var selfCleanse = new BattleSkillSpec(
            "skill.break",
            "skill.break",
            SkillKind.Utility,
            0f,
            0f,
            CleanseProfileId: "break_and_unstoppable");
        var actorLoadout = CombatTestFactory.CreateUnit("actor", skills: new[] { selfCleanse });
        var enemyLoadout = CombatTestFactory.CreateUnit("enemy");
        var state = CombatTestFactory.CreateBattleState(new[] { actorLoadout }, new[] { enemyLoadout });
        var actor = state.Allies.Single();
        var events = new List<BattleEvent>();

        actor.ApplyStatus(new StatusApplicationSpec("apply.root", "root", 2f, 0f));
        actor.ApplyStatus(new StatusApplicationSpec("apply.burn", "burn", 3f, 2f));

        StatusResolutionService.ApplySkillStatuses(state, actor, actor, selfCleanse, events);

        Assert.That(actor.HasStatus("root"), Is.False);
        Assert.That(actor.HasStatus("unstoppable"), Is.True);
        Assert.That(actor.ControlResistWindow, Is.Not.Null);
        Assert.That(events.Any(@event => @event.EventKind == BattleEventKind.CleanseTriggered && @event.PayloadId == "break_and_unstoppable"), Is.True);
        Assert.That(events.Any(@event => @event.EventKind == BattleEventKind.ControlResistApplied), Is.True);
    }

    [Test]
    public void AuthoredCleanseGrantedStatusId_AppliesConfiguredStatus_InsteadOfLiteral()
    {
        // ApplyCleanse 부여 상태 id의 콘텐츠 승격(위생 꼬리) — 과거 "unstoppable" 리터럴 하드코딩이라
        // 프로필이 다른 파생 상태(시전 슈퍼아머 등)를 부여하도록 저작해도 무시됐다. 저지불가 kind(3b)를
        // 가진 신규 family를 부여 대상으로 저작하면 IsUnstoppable까지 성립해야 한다(kind 사슬 합류 증명).
        var statusRules = new CombatStatusRules(
            new Dictionary<string, CombatStatusFamilyRule>
            {
                ["cast_aegis"] = new("cast_aegis", StatusGroupValue.DefensiveBoon, false, false, false, 0f, false, false,
                    GrantsUnstoppable: true),
            },
            new Dictionary<string, CombatCleanseProfileRule>
            {
                ["break_custom"] = new("break_custom", Array.Empty<string>(), false, true, 1.5f, "cast_aegis"),
            },
            null);
        var cleanseSkill = new BattleSkillSpec(
            "skill.break.custom",
            "skill.break.custom",
            SkillKind.Utility,
            0f,
            0f,
            CleanseProfileId: "break_custom");
        var actorLoadout = CombatTestFactory.CreateUnit("actor", skills: new[] { cleanseSkill });
        var state = CombatTestFactory.CreateBattleState(
            new[] { actorLoadout },
            new[] { CombatTestFactory.CreateUnit("enemy") },
            statusRules: statusRules);
        var actor = state.Allies.Single();
        var events = new List<BattleEvent>();

        StatusResolutionService.ApplySkillStatuses(state, actor, actor, cleanseSkill, events);

        Assert.That(actor.HasStatus("cast_aegis"), Is.True,
            "부여 상태 id는 프로필 데이터(GrantedStatusId)가 소유해야 한다");
        Assert.That(actor.HasStatus("unstoppable"), Is.False,
            "과거 리터럴 잔재가 부여되면 안 된다");
        Assert.That(actor.IsUnstoppable, Is.True,
            "저지불가 kind를 가진 파생 상태를 부여하면 저지불가가 성립해야 한다(3b set 사슬 합류)");
        Assert.That(events.Any(@event => @event.EventKind == BattleEventKind.ControlResistApplied && @event.PayloadId == "cast_aegis"), Is.True,
            "제어 저항 이벤트 라벨도 부여 상태 id를 따라가야 한다");
    }

    [Test]
    public void AuthoredStatusRules_DrivePeriodicDamageCleanseAndDiminishing()
    {
        var statusRules = new CombatStatusRules(
            new Dictionary<string, CombatStatusFamilyRule>
            {
                ["stagger"] = new("stagger", StatusGroupValue.Control, true, true, true, 0.25f, false, false),
                ["scorch"] = new("scorch", StatusGroupValue.Attrition, false, false, false, 0f, true, false),
                ["unstoppable"] = CombatStatusRules.Default.StatusFamilies["unstoppable"],
            },
            new Dictionary<string, CombatCleanseProfileRule>
            {
                ["cleanse_scorch"] = new("cleanse_scorch", new[] { "scorch" }, false, false, 0f),
            },
            new CombatControlDiminishingRule("custom_dr", 0.25f, 2.25f, Array.Empty<string>(), Array.Empty<string>()));
        var targetBase = CombatTestFactory.CreateUnit("target", hp: 20f);
        var targetStats = new Dictionary<StatKey, float>(targetBase.BaseStats)
        {
            [StatKey.Tenacity] = 0.4f,
        };
        var targetLoadout = targetBase with { BaseStats = targetStats };
        var statusSkill = new BattleSkillSpec(
            "skill.status",
            "skill.status",
            SkillKind.Utility,
            0f,
            1f,
            AppliedStatuses: new[]
            {
                new StatusApplicationSpec("apply.stagger", "stagger", 2f, 0f),
                new StatusApplicationSpec("apply.scorch", "scorch", 3f, 2f),
            });
        var cleanseSkill = new BattleSkillSpec(
            "skill.cleanse",
            "skill.cleanse",
            SkillKind.Utility,
            0f,
            1f,
            CleanseProfileId: "cleanse_scorch");
        var actorLoadout = CombatTestFactory.CreateUnit("actor", classId: "mystic", skills: new[] { statusSkill, cleanseSkill });
        var state = CombatTestFactory.CreateBattleState(new[] { actorLoadout }, new[] { targetLoadout }, statusRules: statusRules);
        var actor = state.Allies.Single();
        var target = state.Enemies.Single();
        var events = new List<BattleEvent>();

        StatusResolutionService.ApplySkillStatuses(state, actor, target, statusSkill, events);

        Assert.That(target.Statuses.Single(status => status.StatusId == "stagger").RemainingSeconds, Is.EqualTo(1.8f).Within(0.001f));
        StatusResolutionService.AdvanceStatuses(state, events);
        Assert.That(target.CurrentHealth, Is.EqualTo(18f).Within(0.001f));

        StatusResolutionService.ApplySkillStatuses(state, actor, target, cleanseSkill, events);

        Assert.That(target.HasStatus("scorch"), Is.False);
    }
}
