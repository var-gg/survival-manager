using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;
using SM.Core.Stats;
using SM.Meta;
using SM.Meta.Model;
using SM.Meta.Services;

namespace SM.Tests.EditMode;

/// <summary>
/// differential 하네스 1차 — "콘텐츠가 sim에서 실제로 뭔가 바꾸는가"를 단언한다.
/// 배경: 2026-07 준비도 감사에서 콘텐츠→sim 효과 사슬 assertion이 0으로 판명
/// (affix의 CompileTags/RequiredTags/RuleModifierTags 전량 드롭, 조건부 affix가
/// 무조건 affix로 동작). 이 파일은 compile 출력을 실 BattleSimulator에 넣어
/// 장착 유무가 전투 결과를 바꾸는지까지 검증하는 최초의 compile→sim 연결이다.
/// </summary>
[Category("FastUnit")]
public sealed class ContentEffectDifferentialTests
{
    private const int Seed = 7;
    private const int MaxSteps = 600;

    [Test]
    public void RuleAffix_CompileTagsAndRulePackage_ReachCompiledUnit()
    {
        var compiled = CompileSingleHero(affixIds: new[] { "affix.tempo" });
        var ally = compiled.Allies.Single();

        Assert.That(ally.CompileTags, Does.Contain("tempo"),
            "affix의 CompileTags는 유닛 태그로 전파돼야 한다 — 드롭되면 태그 기반 계약(조건/시너지 확장)이 전부 무효화된다");
        Assert.That(ally.CompileTags, Does.Contain("execute"));
        Assert.That(
            (ally.RulePackages ?? Array.Empty<CombatRuleModifierPackage>())
                .Any(package => package.SourceId == "affix.tempo"
                    && package.Modifiers.Any(modifier => modifier.Kind == RuleModifierKind.BehaviorTag && modifier.Value == "tempo")),
            Is.True,
            "affix RuleModifierTags는 skill/augment/passive와 동일하게 RulePackages로 컴파일돼야 한다");
    }

    [Test]
    public void ConditionalAffix_UnmetRequirement_IsFullyInert_InCompileAndSim()
    {
        var withUnmetConditional = CompileSingleHero(affixIds: new[] { "affix.cond_unmet" });
        var withoutAffix = CompileSingleHero(affixIds: Array.Empty<string>());

        Assert.That(
            withUnmetConditional.Allies.Single().NumericPackages.Any(package => package.SourceId == "affix.cond_unmet"),
            Is.False,
            "RequiredTags 미충족 조건부 affix의 수치는 적용되면 안 된다 — 현재는 조건 무시로 무조건 적용되는 버그");

        var unmetRun = RunCompiledAllyVersusRaider(withUnmetConditional);
        var baselineRun = RunCompiledAllyVersusRaider(withoutAffix);
        Assert.That(unmetRun.Stream, Is.EqualTo(baselineRun.Stream),
            "미충족 조건부 affix 장착은 미장착과 전투 결과가 완전히 동일해야 한다(수치 누출 0)");
    }

    [Test]
    public void ConditionalAffix_MetRequirement_AppliesNumeric_AndChangesSimOutcome()
    {
        var withMetConditional = CompileSingleHero(affixIds: new[] { "affix.cond_met" });
        var withoutAffix = CompileSingleHero(affixIds: Array.Empty<string>());

        Assert.That(
            withMetConditional.Allies.Single().NumericPackages.Any(package => package.SourceId == "affix.cond_met"),
            Is.True,
            "RequiredTags 충족(vanguard) 조건부 affix는 수치가 적용돼야 한다 — 게이트가 과잉 차단하면 안 된다");

        var metRun = RunCompiledAllyVersusRaider(withMetConditional);
        var baselineRun = RunCompiledAllyVersusRaider(withoutAffix);
        Assert.That(metRun.AllySurvivedSteps, Is.GreaterThan(baselineRun.AllySurvivedSteps),
            "+200 MaxHealth 충족 affix를 장착한 유닛은 실 sim에서 뚜렷하게 오래 생존해야 한다");
    }

    [Test]
    public void NumericAffix_EquippedVsNot_SurvivesLonger_InRealSim()
    {
        var equipped = CompileSingleHero(affixIds: new[] { "affix.hearty" });
        var bare = CompileSingleHero(affixIds: Array.Empty<string>());

        var equippedRun = RunCompiledAllyVersusRaider(equipped);
        var bareRun = RunCompiledAllyVersusRaider(bare);

        Assert.That(equippedRun.AllySurvivedSteps, Is.GreaterThan(bareRun.AllySurvivedSteps),
            "장착 vs 미장착이 실 BattleSimulator 결과를 바꿔야 한다 — 이 단언이 실 콘텐츠 사슬의 silent 드롭을 잡는 1차 가드다");
    }

    [Test]
    public void PassiveSkillTriggeredEffect_ReachesUnit_AndChangesSimOutcome()
    {
        var withTrigger = CompileSingleHero(
            affixIds: Array.Empty<string>(),
            mutatePassive: skill => skill with
            {
                TriggeredEffects = new[]
                {
                    new CombatTriggeredEffect(skill.Id, CombatTriggerKind.BattleStart, TriggeredEffectOp.Barrier, EffectScope.Self, Magnitude: 60f),
                },
            });
        var without = CompileSingleHero(affixIds: Array.Empty<string>());

        Assert.That(
            withTrigger.Allies.Single().EffectiveTriggeredEffects
                .Any(effect => effect.SourceId == "skill.vanguard.passive" && effect.Op == TriggeredEffectOp.Barrier),
            Is.True,
            "패시브 슬롯 스킬의 TriggeredEffects는 유닛 트리거 채널로 합류해야 한다(증강과 동일 계약)");
        Assert.That(
            without.Allies.Single().EffectiveTriggeredEffects
                .Any(effect => effect.SourceId == "skill.vanguard.passive"),
            Is.False,
            "음성 대조군: 발동 효과가 없는 패시브는 트리거 채널에 아무 것도 남기지 않아야 한다");

        var withRun = RunCompiledAllyVersusRaider(withTrigger);
        var withoutRun = RunCompiledAllyVersusRaider(without);
        Assert.That(withRun.AllySurvivedSteps, Is.GreaterThan(withoutRun.AllySurvivedSteps),
            "BattleStart Barrier(60)를 발동한 유닛은 실 sim에서 뚜렷하게 오래 생존해야 한다 — 패시브 슬롯 실전투 효과의 1차 가드");
    }

    [Test]
    public void SupportGem_TransformsMatchedActive_InCompile()
    {
        BattleSkillSpec MutateCore(BattleSkillSpec skill) => skill with { CompileTags = new[] { "strike" } };

        // 매칭: allowed(strike)가 코어와 교집합 → Power ×2 + marked 부여, 비매칭 유틸리티는 무변.
        var matched = CompileSingleHero(
            Array.Empty<string>(),
            mutateCore: MutateCore,
            mutateSupport: skill => skill with
            {
                SupportAllowedTags = new[] { "strike" },
                SupportModifier = new BattleSupportModifierSpec(
                    PowerMultiplier: 2f,
                    AddedStatuses: new[] { new StatusApplicationSpec("gem:marked", "marked", 3f, 1f) }),
            });
        var matchedCore = matched.Allies.Single().Skills.Single(skill => skill.Id == "skill.warden.core");
        Assert.That(matchedCore.Power, Is.EqualTo(9f).Within(0.0001f),
            "매칭된 서포트 젬은 페어 액티브의 위력을 변조해야 한다 (4.5 × 2)");
        Assert.That(
            (matchedCore.AppliedStatuses ?? Array.Empty<StatusApplicationSpec>()).Any(status => status.StatusId == "marked"),
            Is.True,
            "젬의 AddedStatuses가 매칭 액티브에 부여돼야 한다");
        var matchedUtility = matched.Allies.Single().Skills.Single(skill => skill.Id == "skill.warden.utility");
        Assert.That(matchedUtility.Power, Is.EqualTo(0f).Within(0.0001f), "비매칭(태그 없음) 액티브는 무변");

        // 비매칭: allowed(projectile)가 어느 액티브와도 교집합 없음 → 전부 무변.
        var unmatched = CompileSingleHero(
            Array.Empty<string>(),
            mutateCore: MutateCore,
            mutateSupport: skill => skill with
            {
                SupportAllowedTags = new[] { "projectile" },
                SupportModifier = new BattleSupportModifierSpec(PowerMultiplier: 2f),
            });
        Assert.That(
            unmatched.Allies.Single().Skills.Single(skill => skill.Id == "skill.warden.core").Power,
            Is.EqualTo(4.5f).Within(0.0001f),
            "allowed 태그 비매칭이면 변조가 없어야 한다");

        // 클래스 게이트: RequiredClassTags(mystic)를 warden(vanguard)이 불충족 → 무변.
        var classGated = CompileSingleHero(
            Array.Empty<string>(),
            mutateCore: MutateCore,
            mutateSupport: skill => skill with
            {
                SupportAllowedTags = new[] { "strike" },
                RequiredClassTags = new[] { "mystic" },
                SupportModifier = new BattleSupportModifierSpec(PowerMultiplier: 2f),
            });
        Assert.That(
            classGated.Allies.Single().Skills.Single(skill => skill.Id == "skill.warden.core").Power,
            Is.EqualTo(4.5f).Within(0.0001f),
            "RequiredClassTags 불충족 젬은 변조하지 않아야 한다");
    }

    [Test]
    public void SupportGem_WeaponGate_RequiresMatchingWeaponFamily()
    {
        BattleSkillSpec MutateCore(BattleSkillSpec skill) => skill with { CompileTags = new[] { "strike" } };
        BattleSkillSpec MutateSupport(BattleSkillSpec skill) => skill with
        {
            SupportAllowedTags = new[] { "strike" },
            RequiredWeaponTags = new[] { "bow" },
            SupportModifier = new BattleSupportModifierSpec(PowerMultiplier: 2f),
        };

        // 활(bow) 미장착 → 무기 게이트가 젬을 차단(변조 없음).
        var withoutBow = CompileSingleHero(
            Array.Empty<string>(), mutateCore: MutateCore, mutateSupport: MutateSupport);
        Assert.That(
            withoutBow.Allies.Single().Skills.Single(skill => skill.Id == "skill.warden.core").Power,
            Is.EqualTo(4.5f).Within(0.0001f),
            "RequiredWeaponTags(bow) 미충족 젬은 변조하지 않아야 한다");

        // 활 장착 → 무기 family 태그가 유닛에 전파되고 게이트 통과(변조 적용).
        var withBow = CompileSingleHero(
            Array.Empty<string>(),
            mutateCore: MutateCore,
            mutateSupport: MutateSupport,
            itemIds: new[] { "item.shield", "item.bow" });
        var unit = withBow.Allies.Single();
        Assert.That(unit.CompileTags, Does.Contain("bow"),
            "아이템의 weapon family 태그가 유닛 태그로 전파돼야 한다");
        Assert.That(
            unit.Skills.Single(skill => skill.Id == "skill.warden.core").Power,
            Is.EqualTo(9f).Within(0.0001f),
            "무기 게이트 충족 시 젬 변조가 적용돼야 한다 (4.5 × 2)");
    }

    [Test]
    public void SupportGem_OwnerModifiers_ChangeSimOutcome()
    {
        var withGem = CompileSingleHero(
            Array.Empty<string>(),
            mutateSupport: skill => skill with
            {
                SupportModifier = new BattleSupportModifierSpec(
                    OwnerModifiers: new[]
                    {
                        new StatModifier(StatKey.MaxHealth, ModifierOp.Flat, 200f, ModifierSource.Skill, "gem.owner"),
                    }),
            });
        var without = CompileSingleHero(Array.Empty<string>());

        Assert.That(
            withGem.Allies.Single().NumericPackages.Any(package => package.SourceId == "support:skill.vanguard.support"),
            Is.True,
            "젬의 OwnerModifiers는 유닛 numeric package로 합류해야 한다");

        var withRun = RunCompiledAllyVersusRaider(withGem);
        var withoutRun = RunCompiledAllyVersusRaider(without);
        Assert.That(withRun.AllySurvivedSteps, Is.GreaterThan(withoutRun.AllySurvivedSteps),
            "젬 OwnerModifiers(+200 MaxHealth)가 실 sim 생존을 바꿔야 한다");
    }

    [Test]
    public void GuardedIncomingDamageDelta_IsContentTunable_InRealSim()
    {
        // 같은 전투를 상태 규칙만 바꿔 두 번 — guarded delta는 이제 콘텐츠 튜닝값이다.
        // 프로브 유닛에 개전 시 장시간 guarded를 발동시켜(slic A 트리거 채널) delta가 매 피격에 적용되게 한다.
        var compiled = CompileSingleHero(
            Array.Empty<string>(),
            mutatePassive: skill => skill with
            {
                TriggeredEffects = new[]
                {
                    new CombatTriggeredEffect(
                        skill.Id,
                        CombatTriggerKind.BattleStart,
                        TriggeredEffectOp.ApplyStatus,
                        EffectScope.Self,
                        Magnitude: 1f,
                        StatusId: "guarded",
                        DurationSeconds: 600f),
                },
            });

        var defaultRun = RunCompiledAllyVersusRaider(compiled, CombatStatusRules.Default);
        var strongGuard = new CombatStatusRules(
            CombatStatusRules.Default.StatusFamilies
                .ToDictionary(pair => pair.Key, pair => pair.Key == "guarded"
                    ? pair.Value with { IncomingDamageDelta = -0.6f }
                    : pair.Value, StringComparer.Ordinal),
            null,
            null);
        var strongRun = RunCompiledAllyVersusRaider(compiled, strongGuard);

        Assert.That(strongRun.AllySurvivedSteps, Is.GreaterThan(defaultRun.AllySurvivedSteps),
            "guarded 받는피해 delta를 콘텐츠 값(-0.6)으로 키우면 실 sim 생존이 뚜렷하게 늘어야 한다 — 숫자 콘텐츠화의 1차 가드");
    }

    [Test]
    public void MarkedMagnitudeScale_IsContentTunable_InRealSim()
    {
        // 같은 전투를 상태 규칙만 바꿔 — marked magnitude(0.5)가 받는 피해 배수 가산에 실리는
        // 배율은 이제 콘텐츠 튜닝값이다(숫자 콘텐츠화 2보). 개전 self-marked 600s로 매 피격에 프로브.
        var compiled = CompileStatusProbe("marked", 0.5f);

        var defaultRun = RunCompiledAllyVersusRaider(compiled, CombatStatusRules.Default);
        var amplifiedRun = RunCompiledAllyVersusRaider(compiled, RulesWithMagnitudeScale("marked", 4f));
        Assert.That(amplifiedRun.AllySurvivedSteps, Is.LessThan(defaultRun.AllySurvivedSteps),
            "marked 배율을 콘텐츠 값(4)으로 키우면(×1.5→×3.0) 실 sim 생존이 뚜렷하게 줄어야 한다");

        // 항등 계약: 배율 1 저작 == 미저작 기본 — 전투 스트림 byte-identical(콘텐츠가 현행 값을
        // 저작하면 결과 무변이라는 2보 핵심 계약의 실행 사양).
        var explicitOneRun = RunCompiledAllyVersusRaider(compiled, RulesWithMagnitudeScale("marked", 1f));
        Assert.That(explicitOneRun.Stream, Is.EqualTo(defaultRun.Stream),
            "MagnitudeScale=1 명시 저작은 기본 규칙과 전투 스트림이 완전히 동일해야 한다(항등 배율)");
    }

    [Test]
    public void ExposedMagnitudeScale_IsContentTunable_InRealSim()
    {
        var compiled = CompileStatusProbe("exposed", 0.5f);

        var defaultRun = RunCompiledAllyVersusRaider(compiled, CombatStatusRules.Default);
        var amplifiedRun = RunCompiledAllyVersusRaider(compiled, RulesWithMagnitudeScale("exposed", 4f));
        Assert.That(amplifiedRun.AllySurvivedSteps, Is.LessThan(defaultRun.AllySurvivedSteps),
            "exposed 배율을 콘텐츠 값(4)으로 키우면 실 sim 생존이 뚜렷하게 줄어야 한다");
    }

    [Test]
    public void SunderMagnitudeScale_IsContentTunable_InRealSim()
    {
        // 파쇄는 magnitude × 배율이 곧 방어/저항 차감량. 방어(+30 affix, 합계 31)를 세운 프로브에
        // self-sunder(30)를 얹어 배율 0(차감 무효, 방어 31 유지) vs 기본 1(방어 1로 파쇄)을 대조한다.
        var compiled = CompileStatusProbe("sunder", 30f, affixIds: new[] { "affix.plated" });

        var defaultRun = RunCompiledAllyVersusRaider(compiled, CombatStatusRules.Default);
        var neutralizedRun = RunCompiledAllyVersusRaider(compiled, RulesWithMagnitudeScale("sunder", 0f));
        Assert.That(neutralizedRun.AllySurvivedSteps, Is.GreaterThan(defaultRun.AllySurvivedSteps),
            "sunder 배율을 콘텐츠 값(0)으로 끄면 방어 차감이 사라져 실 sim 생존이 뚜렷하게 늘어야 한다");
    }

    [Test]
    public void WoundMagnitudeScale_IsContentTunable_InRealSim()
    {
        // 상처는 magnitude × 배율이 치유 감소율(UnitSnapshot.GetHealingTakenMultiplier). 컴파일 유닛의
        // 흡혈은 basic attack 전용이라 프로브가 안 되므로(1차 실측), 체력 50% 관문에서 자가 힐(15)을
        // 발동시키고 self-wound(0.9)로 그 힐을 깎는다 — 배율 0(치유 온전 +15) vs 기본 1(치유 ×0.1) 대조.
        // 힐량 15는 MaxHealth(33) 캡에 물리지 않는 크기다(캡에 닿으면 양 런이 같아져 측정 허구).
        var compiled = CompileSingleHero(
            Array.Empty<string>(),
            mutatePassive: skill => skill with
            {
                TriggeredEffects = new[]
                {
                    new CombatTriggeredEffect(
                        skill.Id,
                        CombatTriggerKind.BattleStart,
                        TriggeredEffectOp.ApplyStatus,
                        EffectScope.Self,
                        Magnitude: 0.9f,
                        StatusId: "wound",
                        DurationSeconds: 600f),
                    new CombatTriggeredEffect(
                        skill.Id,
                        CombatTriggerKind.OnHpBelow,
                        TriggeredEffectOp.Heal,
                        EffectScope.Self,
                        Magnitude: 15f,
                        ThresholdRatio: 0.5f),
                },
            });

        var defaultRun = RunCompiledAllyVersusRaider(compiled, CombatStatusRules.Default);
        var neutralizedRun = RunCompiledAllyVersusRaider(compiled, RulesWithMagnitudeScale("wound", 0f));
        Assert.That(neutralizedRun.AllySurvivedSteps, Is.GreaterThan(defaultRun.AllySurvivedSteps),
            "wound 배율을 콘텐츠 값(0)으로 끄면 관문 치유가 온전해져 실 sim 생존이 뚜렷하게 늘어야 한다");
    }

    [Test]
    public void SlowMagnitudeScale_IsContentTunable_InRealSim()
    {
        // 감속은 magnitude × 배율이 공속/이속 감쇠율. 컴파일 유닛의 화력은 스킬(공속 무관) 기반이라
        // 자기 감속으로는 결과가 안 바뀌므로(1차 실측), basic attack 유닛인 적 전체에 개전 slow(0.45)를
        // 걸어 배율 2(공속/이속 ×0.1 바닥) vs 기본 1(×0.55)의 적 화력 둔화를 아군 생존으로 대조한다.
        var compiled = CompileStatusProbe("slow", 0.45f, scope: EffectScope.EnemyCombatants);

        var defaultRun = RunCompiledAllyVersusRaider(compiled, CombatStatusRules.Default);
        var amplifiedRun = RunCompiledAllyVersusRaider(compiled, RulesWithMagnitudeScale("slow", 2f));
        Assert.That(amplifiedRun.AllySurvivedSteps, Is.GreaterThan(defaultRun.AllySurvivedSteps),
            "slow 배율을 콘텐츠 값(2)으로 키우면 적 공속/이속이 더 죽어 아군 생존이 뚜렷하게 늘어야 한다");
    }

    [Test]
    public void BurnMagnitudeScale_IsContentTunable_InRealSim()
    {
        // 주기 피해(burn/bleed)는 magnitude × 배율이 틱 피해량(StatusResolutionService.ApplyPeriodicDamage,
        // 바닥 1은 코드 소유). 개전 self-burn(5) 600s 프로브 — 배율 3(틱 15) vs 기본 1(틱 5)의
        // 자가 소모를 생존으로 대조하고, 배율 1 명시 저작의 항등 계약을 스트림으로 단언한다.
        var compiled = CompileStatusProbe("burn", 5f);

        var defaultRun = RunCompiledAllyVersusRaider(compiled, CombatStatusRules.Default);
        var amplifiedRun = RunCompiledAllyVersusRaider(compiled, RulesWithMagnitudeScale("burn", 3f));
        Assert.That(amplifiedRun.AllySurvivedSteps, Is.LessThan(defaultRun.AllySurvivedSteps),
            "burn 배율을 콘텐츠 값(3)으로 키우면(틱 5→15) 실 sim 생존이 뚜렷하게 줄어야 한다");

        var explicitOneRun = RunCompiledAllyVersusRaider(compiled, RulesWithMagnitudeScale("burn", 1f));
        Assert.That(explicitOneRun.Stream, Is.EqualTo(defaultRun.Stream),
            "MagnitudeScale=1 명시 저작은 기본 규칙과 전투 스트림이 완전히 동일해야 한다(항등 배율)");
    }

    [Test]
    public void BarrierOnApplyKind_IsContentDriven_InRealSim()
    {
        // 효과 종류 데이터화 3보 1슬라이스 — "적용 시 즉시 보호막 전환"은 이제 StatusId=="barrier"
        // 문자열 분기가 아니라 콘텐츠 kind(GrantsBarrierOnApply)다. 1차 프로브(컴파일 유닛의 적 대상
        // 스킬 시전)는 측정 허구로 판명(적 최종 체력 800 무변 — 컴파일 워든의 공격 접촉은 어떤
        // differential 도 전제한 적 없는 미검증 가정) — 실측 후 legacy 손조립 캐스터의 Self
        // ActiveSkill 반복 시전으로 교정. Utility kind 는 피해 스킵 + ApplySkillStatuses 만 태우는
        // 확정 경로다(StatusResolutionServiceTests 와 동일 조립, 사거리 재검증도 없음).
        var barrierSkill = new BattleSkillSpec(
            "skill.probe.barrier",
            "skill.probe.barrier",
            SkillKind.Utility,
            0f,
            1f,
            AppliedStatuses: new[] { new StatusApplicationSpec("probe:barrier", "barrier", 3f, 60f) });
        var caster = CombatTestFactory.CreateUnit(
            "caster",
            hp: 40f,
            attack: 1f,
            skills: new[] { barrierSkill },
            tactics: new[]
            {
                new TacticRule(0, TacticConditionType.Fallback, 0f, BattleActionType.ActiveSkill, TargetSelectorType.Self, "skill.probe.barrier"),
                new TacticRule(1, TacticConditionType.Fallback, 0f, BattleActionType.WaitDefend, TargetSelectorType.Self),
            });

        var defaultRun = RunBarrierCasterVersusRaider(caster, CombatStatusRules.Default);
        var disabledRun = RunBarrierCasterVersusRaider(caster, RulesWithBarrierOnApply(false));
        Assert.That(disabledRun.AllySurvivedSteps, Is.LessThan(defaultRun.AllySurvivedSteps),
            "barrier 전환 kind를 콘텐츠 값(false)으로 끄면 자기 시전이 보호막 대신 무효과 잔존 상태로 " +
            "떨어져 실 sim 생존이 뚜렷하게 줄어야 한다 — 효과 종류가 규칙에서 소비된다는 1차 가드");

        // 항등 계약: true 명시 저작 == 기본 규칙 — 전투 스트림 byte-identical(콘텐츠가 현행 값을
        // 저작하면 결과 무변이라는 1보/2보와 같은 핵심 계약의 kind 판).
        var explicitTrueRun = RunBarrierCasterVersusRaider(caster, RulesWithBarrierOnApply(true));
        Assert.That(explicitTrueRun.Stream, Is.EqualTo(defaultRun.Stream),
            "GrantsBarrierOnApply=true 명시 저작은 기본 규칙과 전투 스트림이 완전히 동일해야 한다(항등 서술자)");
    }

    [Test]
    public void UnstoppableKind_IsContentDriven_InRealSim()
    {
        // 효과 종류 데이터화 3보 3b — "보유 시 저지불가(하드 컨트롤 적용 면역)"는 이제 HasStatus("unstoppable")
        // 문자열 조회가 아니라 콘텐츠 kind(GrantsUnstoppable)가 파생한 상태 id set이다(핫패스 set 스냅샷 최초 개통).
        // 프로브는 legacy 손조립 캐스터의 Self ActiveSkill 확정 경로만 사용(3a에서 실측된 컴파일 유닛 공격 접촉
        // 미검증 함정 회피): 한 시전이 unstoppable(60s)을 먼저 적용하고 곧이어 self stun(2s)을 적용한다 —
        // 기본 규칙에선 면역이 stun을 기각하고, kind를 끄면 같은 시전이 자기 스턴으로 떨어진다.
        var probeSkill = new BattleSkillSpec(
            "skill.probe.unstoppable",
            "skill.probe.unstoppable",
            SkillKind.Utility,
            0f,
            1f,
            AppliedStatuses: new[]
            {
                new StatusApplicationSpec("probe:unstoppable", "unstoppable", 60f, 1f),
                new StatusApplicationSpec("probe:self_stun", "stun", 2f, 1f),
            });
        var caster = CombatTestFactory.CreateUnit(
            "caster",
            hp: 400f,
            attack: 1f,
            skills: new[] { probeSkill },
            tactics: new[]
            {
                new TacticRule(0, TacticConditionType.Fallback, 0f, BattleActionType.ActiveSkill, TargetSelectorType.Self, "skill.probe.unstoppable"),
                new TacticRule(1, TacticConditionType.Fallback, 0f, BattleActionType.WaitDefend, TargetSelectorType.Self),
            });

        var defaultRun = RunUnstoppableCasterVersusRaider(caster, CombatStatusRules.Default);
        var disabledRun = RunUnstoppableCasterVersusRaider(caster, RulesWithUnstoppableKind(false));
        Assert.That(defaultRun.AllyStunnedSteps, Is.Zero,
            "기본 규칙(unstoppable family가 kind 보유)에선 하드 컨트롤 면역이 self stun을 매 시전 기각해 " +
            "실 sim 전체에서 스턴 보유 관측이 0이어야 한다");
        Assert.That(disabledRun.AllyStunnedSteps, Is.GreaterThan(0),
            "저지불가 kind를 콘텐츠 값(false)으로 끄면 같은 시전이 자기 스턴으로 떨어져 실 sim에서 스턴이 " +
            "관측돼야 한다 — 면역 membership이 규칙 파생 set에서 소비된다는 1차 가드");

        // 항등 계약: true 명시 저작 == 기본 규칙 — 전투 스트림 byte-identical(1보/2보/3a와 같은 핵심 계약).
        var explicitTrueRun = RunUnstoppableCasterVersusRaider(caster, RulesWithUnstoppableKind(true));
        Assert.That(explicitTrueRun.Stream, Is.EqualTo(defaultRun.Stream),
            "GrantsUnstoppable=true 명시 저작은 기본 규칙과 전투 스트림이 완전히 동일해야 한다(항등 서술자)");
    }

    [Test]
    public void SilenceKind_IsContentDriven_InRealSim()
    {
        // 효과 종류 데이터화 3보 3c — "액티브 시전 차단"은 이제 HasStatus("silence") 문자열 조회가
        // 아니라 콘텐츠 kind(BlocksActiveSkills)가 파생한 set이다(3b가 개통한 set 스냅샷 재사용).
        // 프로브: legacy 손조립 Self ActiveSkill 확정 경로 — 시전이 자기에게 silence(2.5s)를 적용.
        // 기본 규칙에선 침묵 창 동안 ActiveSkill 전술 규칙이 게이트돼(TacticEvaluator) WaitDefend로
        // 강등(방어 관측 발생), kind를 끄면 침묵이 잔존만 하고 시전을 안 막아 방어 강등이 사라진다.
        var probeSkill = new BattleSkillSpec(
            "skill.probe.silence",
            "skill.probe.silence",
            SkillKind.Utility,
            0f,
            1f,
            AppliedStatuses: new[] { new StatusApplicationSpec("probe:self_silence", "silence", 2.5f, 1f) });
        var caster = CombatTestFactory.CreateUnit(
            "caster",
            hp: 400f,
            attack: 1f,
            skills: new[] { probeSkill },
            tactics: new[]
            {
                new TacticRule(0, TacticConditionType.Fallback, 0f, BattleActionType.ActiveSkill, TargetSelectorType.Self, "skill.probe.silence"),
                new TacticRule(1, TacticConditionType.Fallback, 0f, BattleActionType.WaitDefend, TargetSelectorType.Self),
            });

        var defaultRun = RunSilenceCasterVersusRaider(caster, CombatStatusRules.Default);
        var disabledRun = RunSilenceCasterVersusRaider(caster, RulesWithSilenceKind(false));
        Assert.That(defaultRun.AllyDefendingSteps, Is.GreaterThan(0),
            "기본 규칙(silence family가 kind 보유)에선 자기 침묵 창 동안 ActiveSkill 전술이 게이트돼 " +
            "WaitDefend로 강등된 방어 관측이 있어야 한다 — 차단이 규칙 파생 set에서 소비된다는 1차 가드");
        Assert.That(disabledRun.AllyDefendingSteps, Is.LessThan(defaultRun.AllyDefendingSteps),
            "차단 kind를 콘텐츠 값(false)으로 끄면 침묵이 잔존해도 시전이 막히지 않아 방어 강등이 " +
            "뚜렷하게 줄어야 한다(잔존 상태 != 차단 효과의 분리 증명)");

        // 항등 계약: true 명시 저작 == 기본 규칙 — 전투 스트림 byte-identical.
        var explicitTrueRun = RunSilenceCasterVersusRaider(caster, RulesWithSilenceKind(true));
        Assert.That(explicitTrueRun.Stream, Is.EqualTo(defaultRun.Stream),
            "BlocksActiveSkills=true 명시 저작은 기본 규칙과 전투 스트림이 완전히 동일해야 한다(항등 서술자)");
    }

    [Test]
    public void PassiveNodeGrant_TriggeredEffect_ReachesUnit_AndChangesSimOutcome()
    {
        // PoE식 노드 도달 보상(passive-granted-skill.v1) — 노드 선택이 부여 스킬의
        // 발동형 효과를 유닛 트리거 채널로 실어야 하고, 미선택이면 흔적이 없어야 한다.
        var grantedSkill = CreateSkill("skill.node.granted", CompiledSkillSlots.Passive, 0f, SkillKind.Buff) with
        {
            CompileTags = new[] { "node_grant_witness" },
            TriggeredEffects = new[]
            {
                new CombatTriggeredEffect("skill.node.granted", CombatTriggerKind.BattleStart, TriggeredEffectOp.Barrier, EffectScope.Self, Magnitude: 60f),
            },
        };
        var nodes = new Dictionary<string, PassiveNodeTemplate>(StringComparer.Ordinal)
        {
            ["node.grant"] = new PassiveNodeTemplate(
                "node.grant",
                new CombatModifierPackage("node.grant", ModifierSource.Other, Array.Empty<StatModifier>()),
                Array.Empty<string>(),
                BoardId: "board.vanguard",
                GrantedSkillId: "skill.node.granted"),
        };

        var selected = CompileSingleHero(
            Array.Empty<string>(), extraSkills: new[] { grantedSkill }, passiveNodes: nodes, selectedNodeIds: new[] { "node.grant" });
        var selectedUnit = selected.Allies.Single();
        Assert.That(
            selectedUnit.EffectiveTriggeredEffects.Any(effect =>
                effect.SourceId == "skill.node.granted" && effect.Op == TriggeredEffectOp.Barrier),
            Is.True,
            "선택된 노드의 부여 스킬 발동형 효과가 유닛 트리거 채널에 도달해야 한다");
        Assert.That(selectedUnit.CompileTags, Does.Contain("node_grant_witness"),
            "부여 스킬의 CompileTags가 유닛 태그로 전파돼야 한다");
        Assert.That(selectedUnit.Skills.Any(skill => skill.Id == "skill.node.granted"), Is.False,
            "부여 스킬은 4슬롯 계약 밖 효과 캐리어다 — 스킬 슬롯을 차지하면 안 된다");

        var unselected = CompileSingleHero(Array.Empty<string>(), extraSkills: new[] { grantedSkill }, passiveNodes: nodes);
        Assert.That(
            unselected.Allies.Single().EffectiveTriggeredEffects.Any(effect => effect.SourceId == "skill.node.granted"),
            Is.False,
            "음성 대조군: 노드 미선택이면 부여 효과 흔적이 없어야 한다");

        var selectedRun = RunCompiledAllyVersusRaider(selected);
        var unselectedRun = RunCompiledAllyVersusRaider(unselected);
        Assert.That(selectedRun.AllySurvivedSteps, Is.GreaterThan(unselectedRun.AllySurvivedSteps),
            "노드 도달 보상(개전 방벽 60)이 실 sim 생존을 바꿔야 한다 — 유령 패시브 획득 경로의 1차 가드");
    }

    [Test]
    public void PassiveNodeGrant_SupportGem_TransformsActive_WithoutSlotUse()
    {
        // SupportModifier 보유 부여 스킬은 장착 젬과 동일하게 매칭 액티브를 변조해야 한다(슬롯 밖 합류).
        BattleSkillSpec MutateCore(BattleSkillSpec skill) => skill with { CompileTags = new[] { "strike" } };
        var gemSkill = CreateSkill("skill.node.gem", CompiledSkillSlots.Support, 0f, SkillKind.Buff) with
        {
            SupportAllowedTags = new[] { "strike" },
            SupportModifier = new BattleSupportModifierSpec(PowerMultiplier: 2f),
        };
        var nodes = new Dictionary<string, PassiveNodeTemplate>(StringComparer.Ordinal)
        {
            ["node.gem"] = new PassiveNodeTemplate(
                "node.gem",
                new CombatModifierPackage("node.gem", ModifierSource.Other, Array.Empty<StatModifier>()),
                Array.Empty<string>(),
                BoardId: "board.vanguard",
                GrantedSkillId: "skill.node.gem"),
        };

        var selected = CompileSingleHero(
            Array.Empty<string>(), mutateCore: MutateCore, extraSkills: new[] { gemSkill }, passiveNodes: nodes, selectedNodeIds: new[] { "node.gem" });
        var selectedUnit = selected.Allies.Single();
        Assert.That(
            selectedUnit.Skills.Single(skill => skill.Id == "skill.warden.core").Power,
            Is.EqualTo(9f).Within(0.0001f),
            "노드 부여 젬이 매칭 액티브(strike)를 변조해야 한다 (4.5 × 2)");
        Assert.That(selectedUnit.Skills.Any(skill => skill.Id == "skill.node.gem"), Is.False,
            "부여 젬은 스킬 슬롯을 차지하지 않아야 한다");

        var unselected = CompileSingleHero(
            Array.Empty<string>(), mutateCore: MutateCore, extraSkills: new[] { gemSkill }, passiveNodes: nodes);
        Assert.That(
            unselected.Allies.Single().Skills.Single(skill => skill.Id == "skill.warden.core").Power,
            Is.EqualTo(4.5f).Within(0.0001f),
            "음성 대조군: 노드 미선택이면 변조가 없어야 한다");
    }

    [Test]
    public void PassiveNodeGrant_MissingSkillId_IsFullyInert_InSim()
    {
        // 저작 오류(존재하지 않는 스킬 id)는 컴파일이 조용히 건너뛰고(sim 누출 0),
        // 저작 시점 차단은 catalog validator(passive_node.granted_skill_ref)의 몫이다.
        var nodes = new Dictionary<string, PassiveNodeTemplate>(StringComparer.Ordinal)
        {
            ["node.grant_missing"] = new PassiveNodeTemplate(
                "node.grant_missing",
                new CombatModifierPackage("node.grant_missing", ModifierSource.Other, Array.Empty<StatModifier>()),
                Array.Empty<string>(),
                BoardId: "board.vanguard",
                GrantedSkillId: "skill.not_exists"),
            ["node.stat_only"] = new PassiveNodeTemplate(
                "node.stat_only",
                new CombatModifierPackage("node.stat_only", ModifierSource.Other, Array.Empty<StatModifier>()),
                Array.Empty<string>(),
                BoardId: "board.vanguard"),
        };

        var missingGrant = CompileSingleHero(
            Array.Empty<string>(), passiveNodes: nodes, selectedNodeIds: new[] { "node.grant_missing" });
        var statOnly = CompileSingleHero(
            Array.Empty<string>(), passiveNodes: nodes, selectedNodeIds: new[] { "node.stat_only" });

        Assert.That(missingGrant.Allies.Single().EffectiveTriggeredEffects, Is.Empty,
            "미존재 스킬 grant는 트리거 채널에 아무 것도 남기지 않아야 한다");
        var missingRun = RunCompiledAllyVersusRaider(missingGrant);
        var statOnlyRun = RunCompiledAllyVersusRaider(statOnly);
        Assert.That(missingRun.Stream, Is.EqualTo(statOnlyRun.Stream),
            "미존재 grant 노드는 스탯 전용 노드와 전투 결과가 완전히 동일해야 한다(누출 0)");
    }

    [Test]
    public void AffixTemplateCompile_SameScenarioTwice_StableHash()
    {
        var first = CompileSingleHero(affixIds: new[] { "affix.tempo", "affix.cond_met", "affix.cond_unmet" });
        var second = CompileSingleHero(affixIds: new[] { "affix.tempo", "affix.cond_met", "affix.cond_unmet" });

        Assert.That(first.CompileHash, Is.EqualTo(second.CompileHash),
            "affix 템플릿(태그/조건/rule) 경로가 추가돼도 컴파일은 결정적이어야 한다");
    }

    private sealed record SimRun(string Stream, int AllySurvivedSteps);

    private static SimRun RunCompiledAllyVersusRaider(BattleLoadoutSnapshot compiled, CombatStatusRules? statusRules = null)
    {
        var enemy = CombatTestFactory.CreateUnit(
            "enemy.raider",
            race: "undead",
            classId: "duelist",
            anchor: DeploymentAnchorId.FrontBottom,
            hp: 500f,
            attack: 8f);
        var simulator = new BattleSimulator(
            CombatTestFactory.CreateBattleState(compiled.Allies, new[] { enemy }, seed: Seed, statusRules: statusRules),
            MaxSteps);

        var sb = new StringBuilder();
        var allySurvivedSteps = 0;
        var steps = 0;
        var guard = 0;
        while (!simulator.IsFinished && guard++ < 10000)
        {
            var step = simulator.Step();
            steps++;
            foreach (var unit in step.Units)
            {
                sb.Append(unit.Id).Append(':')
                    .Append(unit.CurrentHealth.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(unit.IsAlive ? '1' : '0').Append(';');
                // BattleFactory id 계약: ally_{index}_{loadoutId} (BattleFactory.cs:31)
                if (unit.Id == "ally_0_hero.warden" && unit.IsAlive)
                {
                    allySurvivedSteps = steps;
                }
            }

            sb.Append('\n');
        }

        return new SimRun(sb.ToString(), allySurvivedSteps);
    }

    /// <summary>개전 시 지정 스코프에 장시간(600s) 상태를 발동시키는 프로브 컴파일 — 숫자 채널
    /// (sunder/marked/exposed/wound/slow) differential 의 공용 recipe(guarded 1보 트리거 채널과 동일).</summary>
    private static BattleLoadoutSnapshot CompileStatusProbe(
        string statusId,
        float magnitude,
        EffectScope scope = EffectScope.Self,
        IReadOnlyList<string>? affixIds = null)
    {
        return CompileSingleHero(
            affixIds ?? Array.Empty<string>(),
            mutatePassive: skill => skill with
            {
                TriggeredEffects = new[]
                {
                    new CombatTriggeredEffect(
                        skill.Id,
                        CombatTriggerKind.BattleStart,
                        TriggeredEffectOp.ApplyStatus,
                        scope,
                        Magnitude: magnitude,
                        StatusId: statusId,
                        DurationSeconds: 600f),
                },
            });
    }

    /// <summary>기본 규칙에서 한 family 의 MagnitudeScale 만 바꾼 상태 규칙 — 콘텐츠 튜닝 시나리오 재현.</summary>
    private static CombatStatusRules RulesWithMagnitudeScale(string statusId, float scale)
    {
        return new CombatStatusRules(
            CombatStatusRules.Default.StatusFamilies
                .ToDictionary(pair => pair.Key, pair => pair.Key == statusId
                    ? pair.Value with { MagnitudeScale = scale }
                    : pair.Value, StringComparer.Ordinal),
            null,
            null);
    }

    /// <summary>기본 규칙에서 barrier family 의 즉시 보호막 전환 kind 만 바꾼 상태 규칙 — 3보 1슬라이스 differential.</summary>
    private static CombatStatusRules RulesWithBarrierOnApply(bool grantsBarrierOnApply)
    {
        return new CombatStatusRules(
            CombatStatusRules.Default.StatusFamilies
                .ToDictionary(pair => pair.Key, pair => pair.Key == "barrier"
                    ? pair.Value with { GrantsBarrierOnApply = grantsBarrierOnApply }
                    : pair.Value, StringComparer.Ordinal),
            null,
            null);
    }

    /// <summary>손조립 barrier 캐스터 vs 손조립 raider(hp 500/attack 8) — Self ActiveSkill 반복 시전이
    /// StatusResolutionService.ApplyStatus 의 barrier 전환 kind 를 매 시전 태우는 러너(3보 1슬라이스 전용).</summary>
    private static SimRun RunBarrierCasterVersusRaider(BattleUnitLoadout caster, CombatStatusRules? statusRules)
    {
        var enemy = CombatTestFactory.CreateUnit(
            "enemy.raider",
            race: "undead",
            classId: "duelist",
            anchor: DeploymentAnchorId.FrontBottom,
            hp: 500f,
            attack: 8f);
        var simulator = new BattleSimulator(
            CombatTestFactory.CreateBattleState(new[] { caster }, new[] { enemy }, seed: Seed, statusRules: statusRules),
            MaxSteps);

        var sb = new StringBuilder();
        var allySurvivedSteps = 0;
        var steps = 0;
        var guard = 0;
        while (!simulator.IsFinished && guard++ < 10000)
        {
            var step = simulator.Step();
            steps++;
            foreach (var unit in step.Units)
            {
                sb.Append(unit.Id).Append(':')
                    .Append(unit.CurrentHealth.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(unit.IsAlive ? '1' : '0').Append(';');
                // BattleFactory id 계약: ally_{index}_{loadoutId} (BattleFactory.cs:31)
                if (unit.Id == "ally_0_caster" && unit.IsAlive)
                {
                    allySurvivedSteps = steps;
                }
            }

            sb.Append('\n');
        }

        return new SimRun(sb.ToString(), allySurvivedSteps);
    }

    private sealed record UnstoppableRun(string Stream, int AllyStunnedSteps);

    /// <summary>기본 규칙에서 unstoppable family 의 저지불가 kind 만 바꾼 상태 규칙 — 3보 3b differential.</summary>
    private static CombatStatusRules RulesWithUnstoppableKind(bool grantsUnstoppable)
    {
        return new CombatStatusRules(
            CombatStatusRules.Default.StatusFamilies
                .ToDictionary(pair => pair.Key, pair => pair.Key == "unstoppable"
                    ? pair.Value with { GrantsUnstoppable = grantsUnstoppable }
                    : pair.Value, StringComparer.Ordinal),
            null,
            null);
    }

    /// <summary>손조립 unstoppable 캐스터 vs 손조립 raider(hp 500/attack 8) — Self ActiveSkill 확정 경로가
    /// 한 시전에서 unstoppable(60s) 적용 직후 self stun(2s) 적용을 시도해 StatusResolutionService.ApplyStatus
    /// 의 하드 컨트롤 면역 분기를 매 시전 태우는 러너(3보 3b 전용). 스트림에 stun 보유 여부를 함께 기록한다.</summary>
    private static UnstoppableRun RunUnstoppableCasterVersusRaider(BattleUnitLoadout caster, CombatStatusRules? statusRules)
    {
        var enemy = CombatTestFactory.CreateUnit(
            "enemy.raider",
            race: "undead",
            classId: "duelist",
            anchor: DeploymentAnchorId.FrontBottom,
            hp: 500f,
            attack: 8f);
        var simulator = new BattleSimulator(
            CombatTestFactory.CreateBattleState(new[] { caster }, new[] { enemy }, seed: Seed, statusRules: statusRules),
            MaxSteps);

        var sb = new StringBuilder();
        var allyStunnedSteps = 0;
        var guard = 0;
        while (!simulator.IsFinished && guard++ < 10000)
        {
            var step = simulator.Step();
            foreach (var unit in step.Units)
            {
                var hasStun = unit.StatusIds?.Contains("stun") == true;
                sb.Append(unit.Id).Append(':')
                    .Append(unit.CurrentHealth.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(unit.IsAlive ? '1' : '0').Append(',')
                    .Append(hasStun ? '1' : '0').Append(';');
                // BattleFactory id 계약: ally_{index}_{loadoutId} (BattleFactory.cs:31)
                if (unit.Id == "ally_0_caster" && hasStun)
                {
                    allyStunnedSteps++;
                }
            }

            sb.Append('\n');
        }

        return new UnstoppableRun(sb.ToString(), allyStunnedSteps);
    }

    private sealed record SilenceRun(string Stream, int AllyDefendingSteps);

    /// <summary>기본 규칙에서 silence family 의 액티브 시전 차단 kind 만 바꾼 상태 규칙 — 3보 3c differential.</summary>
    private static CombatStatusRules RulesWithSilenceKind(bool blocksActiveSkills)
    {
        return new CombatStatusRules(
            CombatStatusRules.Default.StatusFamilies
                .ToDictionary(pair => pair.Key, pair => pair.Key == "silence"
                    ? pair.Value with { BlocksActiveSkills = blocksActiveSkills }
                    : pair.Value, StringComparer.Ordinal),
            null,
            null);
    }

    /// <summary>손조립 silence 캐스터 vs 손조립 raider — Self ActiveSkill이 자기 침묵을 반복 적용해
    /// TacticEvaluator의 ActiveSkill 게이트(CanUseActiveSkill)를 매 결정마다 태우는 러너(3보 3c 전용).
    /// 스트림에 방어 여부를 함께 기록한다(침묵 차단 시 WaitDefend 강등이 유일한 행동 창구).</summary>
    private static SilenceRun RunSilenceCasterVersusRaider(BattleUnitLoadout caster, CombatStatusRules? statusRules)
    {
        var enemy = CombatTestFactory.CreateUnit(
            "enemy.raider",
            race: "undead",
            classId: "duelist",
            anchor: DeploymentAnchorId.FrontBottom,
            hp: 500f,
            attack: 8f);
        var simulator = new BattleSimulator(
            CombatTestFactory.CreateBattleState(new[] { caster }, new[] { enemy }, seed: Seed, statusRules: statusRules),
            MaxSteps);

        var sb = new StringBuilder();
        var allyDefendingSteps = 0;
        var guard = 0;
        while (!simulator.IsFinished && guard++ < 10000)
        {
            var step = simulator.Step();
            foreach (var unit in step.Units)
            {
                sb.Append(unit.Id).Append(':')
                    .Append(unit.CurrentHealth.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(unit.IsAlive ? '1' : '0').Append(',')
                    .Append(unit.IsDefending ? '1' : '0').Append(';');
                // BattleFactory id 계약: ally_{index}_{loadoutId} (BattleFactory.cs:31)
                if (unit.Id == "ally_0_caster" && unit.IsDefending)
                {
                    allyDefendingSteps++;
                }
            }

            sb.Append('\n');
        }

        return new SilenceRun(sb.ToString(), allyDefendingSteps);
    }

    private static BattleLoadoutSnapshot CompileSingleHero(
        IReadOnlyList<string> affixIds,
        Func<BattleSkillSpec, BattleSkillSpec>? mutatePassive = null,
        Func<BattleSkillSpec, BattleSkillSpec>? mutateCore = null,
        Func<BattleSkillSpec, BattleSkillSpec>? mutateSupport = null,
        IReadOnlyList<string>? itemIds = null,
        IReadOnlyList<BattleSkillSpec>? extraSkills = null,
        IReadOnlyDictionary<string, PassiveNodeTemplate>? passiveNodes = null,
        IReadOnlyList<string>? selectedNodeIds = null)
    {
        itemIds ??= new[] { "item.shield" };
        var content = BuildContentSnapshot(mutatePassive, mutateCore, mutateSupport, extraSkills, passiveNodes);
        var archetype = content.Archetypes["warden"];
        var heroes = new List<HeroRecord>
        {
            new("hero.warden", "hero.warden", archetype.Id, archetype.RaceId, archetype.ClassId, string.Empty, string.Empty),
        };
        var itemInstances = new Dictionary<string, ItemInstanceState>(StringComparer.Ordinal);
        var itemInstanceIds = new List<string>();
        for (var index = 0; index < itemIds.Count; index++)
        {
            var instanceId = $"hero.warden.item.{index}";
            // affix는 첫 아이템에만 부착(기존 시나리오 보존).
            itemInstances[instanceId] = new(instanceId, itemIds[index], index == 0 ? affixIds : Array.Empty<string>(), "hero.warden");
            itemInstanceIds.Add(instanceId);
        }

        var heroLoadouts = new Dictionary<string, HeroLoadoutState>(StringComparer.Ordinal)
        {
            ["hero.warden"] = new(
                "hero.warden",
                itemInstanceIds,
                Array.Empty<string>(),
                "board.vanguard",
                Array.Empty<string>(),
                Array.Empty<string>()),
        };
        var heroProgressions = new Dictionary<string, HeroProgressionState>(StringComparer.Ordinal)
        {
            ["hero.warden"] = new("hero.warden", 1, 0, Array.Empty<string>(), archetype.Skills.Select(skill => skill.Id).ToList()),
        };

        var passiveSelections = new Dictionary<string, PassiveBoardSelectionState>(StringComparer.Ordinal);
        if (selectedNodeIds is { Count: > 0 })
        {
            passiveSelections["hero.warden"] = new PassiveBoardSelectionState("hero.warden", "board.vanguard", selectedNodeIds);
        }

        return new LoadoutCompiler().Compile(
            heroes,
            heroLoadouts,
            heroProgressions,
            itemInstances,
            new Dictionary<string, SkillInstanceState>(StringComparer.Ordinal),
            passiveSelections,
            new PermanentAugmentLoadoutState("bp.differential", Array.Empty<string>()),
            new SquadBlueprintState(
                "bp.differential",
                "bp.differential",
                TeamPostureType.StandardAdvance,
                "team_tactic_standard_advance",
                new Dictionary<DeploymentAnchorId, string> { [DeploymentAnchorId.FrontCenter] = "hero.warden" },
                new[] { "hero.warden" },
                new Dictionary<string, string>(StringComparer.Ordinal),
                null),
            new RunOverlayState(0, Array.Empty<string>(), Array.Empty<string>(), LoadoutCompiler.CurrentCompileVersion, string.Empty),
            content);
    }

    private static CombatContentSnapshot BuildContentSnapshot(
        Func<BattleSkillSpec, BattleSkillSpec>? mutatePassive = null,
        Func<BattleSkillSpec, BattleSkillSpec>? mutateCore = null,
        Func<BattleSkillSpec, BattleSkillSpec>? mutateSupport = null,
        IReadOnlyList<BattleSkillSpec>? extraSkills = null,
        IReadOnlyDictionary<string, PassiveNodeTemplate>? passiveNodes = null)
    {
        var baseRules = new[]
        {
            new TacticRule(0, TacticConditionType.Fallback, 0f, BattleActionType.WaitDefend, TargetSelectorType.Self),
        };
        var coreSkill = CreateSkill("skill.warden.core", CompiledSkillSlots.CoreActive, 4.5f);
        if (mutateCore != null)
        {
            coreSkill = mutateCore(coreSkill);
        }

        var passiveSkill = CreateSkill("skill.vanguard.passive", CompiledSkillSlots.Passive, 0f, SkillKind.Buff);
        if (mutatePassive != null)
        {
            passiveSkill = mutatePassive(passiveSkill);
        }

        var supportSkill = CreateSkill("skill.vanguard.support", CompiledSkillSlots.Support, 0f, SkillKind.Buff);
        if (mutateSupport != null)
        {
            supportSkill = mutateSupport(supportSkill);
        }

        var wardenSkills = new[]
        {
            coreSkill,
            CreateSkill("skill.warden.utility", CompiledSkillSlots.UtilityActive, 0f, SkillKind.Utility),
            passiveSkill,
            supportSkill,
        };

        return new CombatContentSnapshot(
            new Dictionary<string, CombatArchetypeTemplate>(StringComparer.Ordinal)
            {
                ["warden"] = new CombatArchetypeTemplate(
                    "warden",
                    "Warden",
                    "human",
                    "vanguard",
                    DeploymentAnchorId.FrontCenter,
                    CreateBaseStats(30f, 1f, 2f),
                    baseRules,
                    wardenSkills,
                    "anchor",
                    null,
                    null,
                    null,
                    0.25f,
                    1.2f,
                    new ManaEnvelope(0f, 0f, 0f)),
            },
            new Dictionary<string, CombatModifierPackage>(StringComparer.Ordinal),
            new Dictionary<string, CombatModifierPackage>(StringComparer.Ordinal)
            {
                ["item.shield"] = new CombatModifierPackage("item.shield", ModifierSource.Item, new[]
                {
                    new StatModifier(StatKey.MaxHealth, ModifierOp.Flat, 3f, ModifierSource.Item, "item.shield"),
                }),
                ["item.bow"] = new CombatModifierPackage("item.bow", ModifierSource.Item, new[]
                {
                    new StatModifier(StatKey.AttackRange, ModifierOp.Flat, 0.5f, ModifierSource.Item, "item.bow"),
                }),
            },
            new Dictionary<string, CombatModifierPackage>(StringComparer.Ordinal)
            {
                ["affix.hearty"] = new CombatModifierPackage("affix.hearty", ModifierSource.Item, new[]
                {
                    new StatModifier(StatKey.MaxHealth, ModifierOp.Flat, 200f, ModifierSource.Item, "affix.hearty"),
                }),
                ["affix.cond_unmet"] = new CombatModifierPackage("affix.cond_unmet", ModifierSource.Item, new[]
                {
                    new StatModifier(StatKey.MaxHealth, ModifierOp.Flat, 200f, ModifierSource.Item, "affix.cond_unmet"),
                }),
                ["affix.cond_met"] = new CombatModifierPackage("affix.cond_met", ModifierSource.Item, new[]
                {
                    new StatModifier(StatKey.MaxHealth, ModifierOp.Flat, 200f, ModifierSource.Item, "affix.cond_met"),
                }),
                ["affix.tempo"] = new CombatModifierPackage("affix.tempo", ModifierSource.Item, new[]
                {
                    new StatModifier(StatKey.AttackSpeed, ModifierOp.Increased, 0.06f, ModifierSource.Item, "affix.tempo"),
                }),
                // 숫자 채널 differential 프로브용: sunder 는 유의미한 방어 스탯이 있어야
                // 배율 변화가 실 sim 결과로 드러난다.
                ["affix.plated"] = new CombatModifierPackage("affix.plated", ModifierSource.Item, new[]
                {
                    new StatModifier(StatKey.Armor, ModifierOp.Flat, 30f, ModifierSource.Item, "affix.plated"),
                }),
            },
            new Dictionary<string, CombatModifierPackage>(StringComparer.Ordinal),
            wardenSkills.Concat(extraSkills ?? Array.Empty<BattleSkillSpec>()).ToDictionary(skill => skill.Id, skill => skill, StringComparer.Ordinal),
            new Dictionary<string, TeamTacticTemplate>(StringComparer.Ordinal)
            {
                ["team_tactic_standard_advance"] = new TeamTacticTemplate(
                    "team_tactic_standard_advance",
                    new TeamTacticProfile("team_tactic_standard_advance", "Standard", TeamPostureType.StandardAdvance, 1f, 0f, 0f, 0f, 0f, 0f)),
            },
            new Dictionary<string, RoleInstructionTemplate>(StringComparer.Ordinal),
            passiveNodes ?? new Dictionary<string, PassiveNodeTemplate>(StringComparer.Ordinal),
            new Dictionary<string, AugmentCatalogEntry>(StringComparer.Ordinal),
            new Dictionary<string, SynergyTierTemplate>(StringComparer.Ordinal),
            new Dictionary<string, IReadOnlyList<BattleSkillSpec>>(StringComparer.Ordinal),
            ItemCatalog: new Dictionary<string, ItemTemplate>(StringComparer.Ordinal)
            {
                ["item.shield"] = new ItemTemplate("item.shield", Array.Empty<string>(), "shield"),
                ["item.bow"] = new ItemTemplate("item.bow", Array.Empty<string>(), "bow"),
            },
            AffixCatalog: new Dictionary<string, AffixTemplate>(StringComparer.Ordinal)
            {
                // rule affix: 태그 + rule package 보유(계약: EquipmentContentV1 BuildShaping 계열의 축소판)
                ["affix.tempo"] = new AffixTemplate(
                    "affix.tempo",
                    new[] { "tempo", "execute" },
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    new CombatRuleModifierPackage("affix.tempo", ModifierSource.Item, new[]
                    {
                        new RuleModifier(RuleModifierKind.BehaviorTag, "tempo"),
                        new RuleModifier(RuleModifierKind.BehaviorTag, "execute"),
                    })),
                // 조건부: warden(vanguard)이 절대 갖지 않는 태그를 요구 → 미충족
                ["affix.cond_unmet"] = new AffixTemplate(
                    "affix.cond_unmet",
                    new[] { "backline" },
                    new[] { "projectile" },
                    Array.Empty<string>(),
                    null),
                // 조건부: 클래스 raw 태그(vanguard)를 요구 → 충족
                ["affix.cond_met"] = new AffixTemplate(
                    "affix.cond_met",
                    Array.Empty<string>(),
                    new[] { "vanguard" },
                    Array.Empty<string>(),
                    null),
                // affix.hearty는 의도적으로 템플릿 없음 — 레거시(numeric-only) 경로 parity 확인
            });
    }

    private static BattleSkillSpec CreateSkill(string id, string slotKind, float power, SkillKind kind = SkillKind.Strike)
    {
        return new BattleSkillSpec(
            id,
            id,
            kind,
            power,
            1.5f,
            slotKind,
            Array.Empty<string>(),
            kind == SkillKind.Utility || kind == SkillKind.Buff ? DamageType.Healing : DamageType.Physical,
            power,
            1f,
            0f,
            kind == SkillKind.Buff ? 0.5f : 0f,
            0f,
            1.2f,
            0.2f,
            Array.Empty<string>(),
            0f,
            false,
            kind == SkillKind.Utility || kind == SkillKind.Buff ? SkillDelivery.Aura : SkillDelivery.Melee,
            kind == SkillKind.Buff ? SkillTargetRule.Self : SkillTargetRule.NearestEnemy,
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>());
    }

    private static Dictionary<StatKey, float> CreateBaseStats(float health, float armor, float attackSpeed)
    {
        return new Dictionary<StatKey, float>
        {
            [StatKey.MaxHealth] = health,
            [StatKey.Armor] = armor,
            [StatKey.AttackSpeed] = attackSpeed,
            [StatKey.AttackRange] = 1.5f,
            [StatKey.AttackWindup] = 0.2f,
            [StatKey.AttackCooldown] = 1f,
            [StatKey.LeashDistance] = 5f,
            [StatKey.TargetSwitchDelay] = 0.2f,
            [StatKey.PhysPower] = 4f,
        };
    }
}
