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
/// 실 committed passive/support 스킬의 발동형 효과(TriggeredEffects)가 실 컴파일 유닛의
/// 트리거 채널까지 도달하는지 단언하는 witness — inert 수리 2차(스킬 트리거 배선)의 실 asset 쌍.
/// 과거엔 AppliedStatuses가 저작돼 있어도 컴파일(ResolveLoopAPassive)이 드롭해 전투 효과 0이었다.
/// 저작 4종: pelt_last_stand(빈사 unstoppable)·savant_last_word(빈사 시 상대 silence)·
/// line_anchor(개전 guarded)·resonance_cleanse(빈사 unstoppable).
/// </summary>
[Category("BatchOnly")]
public sealed class PassiveSkillContentWitnessTests
{
    [SetUp]
    public void SetUp()
    {
        SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(PassiveSkillContentWitnessTests));
    }

    [Test]
    public void RealSkillCatalog_CarriesAuthoredTriggeredEffects()
    {
        var snapshot = new RuntimeCombatContentLookup().Snapshot;

        var lineAnchor = snapshot.SkillCatalog["support_line_anchor"];
        Assert.That(lineAnchor.TriggeredEffects, Is.Not.Null,
            "support_line_anchor의 발동형 효과(개전 guarded)가 스냅샷까지 실려야 한다");
        Assert.That(
            lineAnchor.TriggeredEffects!.Any(effect =>
                effect.Trigger == CombatTriggerKind.BattleStart
                && effect.Op == TriggeredEffectOp.ApplyStatus
                && effect.StatusId == "guarded"),
            Is.True);

        var lastStand = snapshot.SkillCatalog["skill_pelt_last_stand"];
        Assert.That(
            (lastStand.TriggeredEffects ?? Array.Empty<CombatTriggeredEffect>()).Any(effect =>
                effect.Trigger == CombatTriggerKind.OnHpBelow
                && effect.StatusId == "unstoppable"
                && effect.ThresholdRatio > 0f),
            Is.True,
            "skill_pelt_last_stand는 빈사(OnHpBelow) unstoppable 발동을 실어야 한다");

        var lastWord = snapshot.SkillCatalog["skill_savant_last_word"];
        Assert.That(
            (lastWord.TriggeredEffects ?? Array.Empty<CombatTriggeredEffect>()).Any(effect =>
                effect.Trigger == CombatTriggerKind.OnHpBelow
                && effect.Scope == EffectScope.CurrentTarget
                && effect.StatusId == "silence"),
            Is.True,
            "skill_savant_last_word는 빈사 시 현재 상대에게 silence를 발동해야 한다");

        var resonance = snapshot.SkillCatalog["support_resonance_cleanse"];
        Assert.That(
            (resonance.TriggeredEffects ?? Array.Empty<CombatTriggeredEffect>()).Any(effect =>
                effect.Trigger == CombatTriggerKind.OnHpBelow
                && effect.StatusId == "unstoppable"),
            Is.True);
    }

    [Test]
    public void RealCompile_EquippedSupportSkill_TriggeredEffectReachesUnitChannel()
    {
        var snapshot = new RuntimeCombatContentLookup().Snapshot;
        Assert.That(snapshot.Archetypes.ContainsKey("warden"), Is.True);

        var compiled = CompileWardenWithSupportSkill(snapshot, "support_line_anchor");
        var unit = compiled.Allies.Single();

        Assert.That(
            unit.EffectiveTriggeredEffects.Any(effect =>
                effect.SourceId == "support_line_anchor"
                && effect.Trigger == CombatTriggerKind.BattleStart
                && effect.StatusId == "guarded"),
            Is.True,
            "장착된 서포트 스킬의 발동형 효과가 유닛 트리거 채널(CombatTriggerEngine 소비)에 도달해야 한다");
    }

    [Test]
    public void RealCompile_SupportGem_TransformsMatchedActive_AndOwnerModifiersLand()
    {
        var snapshot = new RuntimeCombatContentLookup().Snapshot;

        // baseline: 변조 없는 서포트(line_anchor는 TriggeredEffects만 보유) — 코어 원값 확보.
        var baseline = CompileWardenWithSupportSkill(snapshot, "support_line_anchor");
        var baselineCore = baseline.Allies.Single().Skills.Single(skill => skill.Id == "skill_power_strike");

        // support_brutal(Include: strike/burst, Power ×1.25) — 실 warden 코어(strike 태그)와 매칭.
        var brutal = CompileWardenWithSupportSkill(snapshot, "support_brutal");
        var brutalCore = brutal.Allies.Single().Skills.Single(skill => skill.Id == "skill_power_strike");
        Assert.That(brutalCore.Power, Is.EqualTo(baselineCore.Power * 1.25f).Within(0.001f),
            "실 support_brutal 젬이 실 warden 코어(strike)의 위력을 ×1.25 변조해야 한다");

        // support_anchored(Classes: vanguard, Weapons: shield) — 클래스는 통과, 무기는 장착 여부로 갈린다.
        var anchoredNoShield = CompileWardenWithSupportSkill(snapshot, "support_anchored");
        Assert.That(
            anchoredNoShield.Allies.Single().NumericPackages.Any(package => package.SourceId == "support:support_anchored"),
            Is.False,
            "방패 미장착이면 support_anchored의 무기 게이트(shield)가 젬을 차단해야 한다");

        var anchoredWithShield = CompileWardenWithSupportSkill(snapshot, "support_anchored", itemId: "item_guardian_shield");
        Assert.That(
            anchoredWithShield.Allies.Single().NumericPackages.Any(package => package.SourceId == "support:support_anchored"),
            Is.True,
            "방패(item_guardian_shield) 장착 시 실 support_anchored 젬의 OwnerModifiers(tenacity)가 유닛 numeric package로 합류해야 한다");
    }

    [Test]
    public void RealClassDefaultSkills_AllCarrySimEffectivePayload()
    {
        var snapshot = new RuntimeCombatContentLookup().Snapshot;
        var classSkillIds =
            from classId in new[] { "vanguard", "duelist", "ranger", "mystic" }
            from kind in new[] { "passive", "support" }
            from tier in new[] { 1, 2 }
            select $"skill_{classId}_{kind}_{tier}";

        foreach (var skillId in classSkillIds)
        {
            Assert.That(snapshot.SkillCatalog.ContainsKey(skillId), Is.True, $"{skillId} 카탈로그 존재");
            var skill = snapshot.SkillCatalog[skillId];
            var effective = (skill.TriggeredEffects?.Count ?? 0) > 0 || skill.SupportModifier != null;
            Assert.That(effective, Is.True,
                $"{skillId}는 sim-effective payload(TriggeredEffects 또는 SupportModifier)를 가져야 한다 — " +
                "매 전투 장착되는 클래스 기본 스킬의 빈 껍데기 회귀 방지 계약");
        }
    }

    [Test]
    public void RealCompile_ArchetypeDefaults_CarryClassSkillEffects()
    {
        var snapshot = new RuntimeCombatContentLookup().Snapshot;

        // warden 기본셋(스킬 로드아웃 없음) = skill_vanguard_passive_1 + skill_vanguard_support_1 포함.
        // 기본 서포트는 무기 요구(shield)를 저작하고 있으므로 방패를 장착해 게이트를 충족시킨다.
        var compiled = CompileWardenWithSupportSkill(snapshot, skillId: null, itemId: "item_guardian_shield");
        var unit = compiled.Allies.Single();

        Assert.That(
            unit.EffectiveTriggeredEffects.Any(effect =>
                effect.SourceId == "skill_vanguard_passive_1" && effect.Op == TriggeredEffectOp.Barrier),
            Is.True,
            "아키타입 기본 패시브(개전 방벽)가 유닛 트리거 채널에 도달해야 한다");
        Assert.That(
            unit.NumericPackages.Any(package => package.SourceId == "support:skill_vanguard_support_1"),
            Is.True,
            "아키타입 기본 서포트의 OwnerModifiers(armor)가 무기 게이트(shield) 충족 시 유닛 numeric package로 합류해야 한다");
    }

    private static BattleLoadoutSnapshot CompileWardenWithSupportSkill(CombatContentSnapshot content, string? skillId, string? itemId = null)
    {
        var archetype = content.Archetypes["warden"];
        var heroes = new List<HeroRecord>
        {
            new("hero.witness", "hero.witness", archetype.Id, archetype.RaceId, archetype.ClassId, string.Empty, string.Empty),
        };
        var skillInstances = new Dictionary<string, SkillInstanceState>(StringComparer.Ordinal);
        var equippedSkillInstanceIds = new List<string>();
        if (skillId != null)
        {
            skillInstances["hero.witness.skill.0"] = new("hero.witness.skill.0", skillId, "support", Array.Empty<string>());
            equippedSkillInstanceIds.Add("hero.witness.skill.0");
        }

        var itemInstances = new Dictionary<string, ItemInstanceState>(StringComparer.Ordinal);
        var equippedItemInstanceIds = new List<string>();
        if (itemId != null)
        {
            itemInstances["hero.witness.item.0"] = new("hero.witness.item.0", itemId, Array.Empty<string>(), "hero.witness");
            equippedItemInstanceIds.Add("hero.witness.item.0");
        }

        var heroLoadouts = new Dictionary<string, HeroLoadoutState>(StringComparer.Ordinal)
        {
            ["hero.witness"] = new(
                "hero.witness",
                equippedItemInstanceIds,
                equippedSkillInstanceIds,
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
            skillInstances,
            new Dictionary<string, PassiveBoardSelectionState>(StringComparer.Ordinal),
            new PermanentAugmentLoadoutState("bp.skill_witness", Array.Empty<string>()),
            new SquadBlueprintState(
                "bp.skill_witness",
                "bp.skill_witness",
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
