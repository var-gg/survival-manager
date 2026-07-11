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

    private static BattleLoadoutSnapshot CompileWardenWithSupportSkill(CombatContentSnapshot content, string skillId)
    {
        var archetype = content.Archetypes["warden"];
        var heroes = new List<HeroRecord>
        {
            new("hero.witness", "hero.witness", archetype.Id, archetype.RaceId, archetype.ClassId, string.Empty, string.Empty),
        };
        var skillInstances = new Dictionary<string, SkillInstanceState>(StringComparer.Ordinal)
        {
            ["hero.witness.skill.0"] = new("hero.witness.skill.0", skillId, "support", Array.Empty<string>()),
        };
        var heroLoadouts = new Dictionary<string, HeroLoadoutState>(StringComparer.Ordinal)
        {
            ["hero.witness"] = new(
                "hero.witness",
                Array.Empty<string>(),
                new[] { "hero.witness.skill.0" },
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
            new Dictionary<string, ItemInstanceState>(StringComparer.Ordinal),
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
