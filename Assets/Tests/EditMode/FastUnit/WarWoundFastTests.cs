using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Content;
using SM.Core.Contracts;
using SM.Core.Ids;
using SM.Core.Stats;
using SM.Meta.Model;
using SM.Meta.Services;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class WarWoundFastTests
{
    private static readonly WarWoundSpec Spec = new(
        WoundTriggerHpRatio: 0.30f,
        WoundAbilityScalar: 0.88f,
        MaxWoundsAppliedPerBattle: 2,
        MaxActiveWounds: 2,
        WoundStacksPerUnitMax: 1,
        ApplyWoundOnLoss: false);

    [Test]
    public void Resolve_VictoryBelowThreshold_AssignsWound()
    {
        var result = WarWoundResolutionService.Resolve(
            CreateRun("hero-a"),
            victory: true,
            new[] { new WarWoundCandidate("hero-a", 29f, 100f) },
            Spec);

        Assert.That(result.AppliedHeroIds, Is.EqualTo(new[] { "hero-a" }));
        Assert.That(result.UpdatedRun.ActiveWoundHeroIds, Is.EqualTo(new[] { "hero-a" }));
    }

    [Test]
    public void Resolve_LossBelowThreshold_DoesNotAssignWound()
    {
        var run = CreateRun("hero-a");
        var result = WarWoundResolutionService.Resolve(
            run,
            victory: false,
            new[] { new WarWoundCandidate("hero-a", 1f, 100f) },
            Spec);

        Assert.That(result.AppliedHeroIds, Is.Empty);
        Assert.That(result.UpdatedRun, Is.SameAs(run));
    }

    [Test]
    public void Resolve_ThreeEligible_SelectsLowestTwo_WithOrdinalTieBreakAndGlobalCap()
    {
        var run = CreateRun("hero-c", "hero-b", "hero-a");
        var result = WarWoundResolutionService.Resolve(
            run,
            victory: true,
            new[]
            {
                new WarWoundCandidate("hero-b", 20f, 100f),
                new WarWoundCandidate("hero-c", 10f, 100f),
                new WarWoundCandidate("hero-a", 20f, 100f),
            },
            Spec);

        Assert.That(result.AppliedHeroIds, Is.EqualTo(new[] { "hero-c", "hero-a" }));
        Assert.That(result.UpdatedRun.ActiveWoundHeroIds, Is.EqualTo(new[] { "hero-a", "hero-c" }));

        var capped = WarWoundResolutionService.Resolve(
            result.UpdatedRun,
            victory: true,
            new[] { new WarWoundCandidate("hero-b", 1f, 100f) },
            Spec);
        Assert.That(capped.AppliedHeroIds, Is.Empty);
        Assert.That(capped.UpdatedRun.ActiveWoundHeroIds!.Count, Is.EqualTo(2));
    }

    [Test]
    public void Compile_WoundedHero_ActiveAbilityOutputAndControlDurationAreScaledByPointEightEight()
    {
        var content = CreateAbilityContent();
        var baseline = Compile(content, Array.Empty<string>());
        var wounded = Compile(content, new[] { "hero-a" });
        var baselineUnit = CreateUnit(baseline.Allies.Single());
        var woundedUnit = CreateUnit(wounded.Allies.Single());
        var baselineSkill = baseline.Allies.Single().EffectiveSignatureActive!;
        var woundedSkill = wounded.Allies.Single().EffectiveSignatureActive!;

        var baselineOutput = HitResolutionService.ResolveSupportValue(baselineUnit, baselineSkill);
        var woundedOutput = HitResolutionService.ResolveSupportValue(woundedUnit, woundedSkill);

        Assert.That(woundedOutput, Is.EqualTo(baselineOutput * 0.88f).Within(0.0001f));
        Assert.That(woundedSkill.PowerFlat, Is.EqualTo(baselineSkill.PowerFlat * 0.88f).Within(0.0001f));
        Assert.That(woundedSkill.HealCoeff, Is.EqualTo(baselineSkill.HealCoeff * 0.88f).Within(0.0001f));
        Assert.That(
            woundedSkill.AppliedStatuses!.Single(status => status.StatusId == "stun").DurationSeconds,
            Is.EqualTo(8.8f).Within(0.0001f));
        Assert.That(
            woundedSkill.AppliedStatuses!.Single(status => status.StatusId == "guarded").DurationSeconds,
            Is.EqualTo(10f).Within(0.0001f),
            "전상은 제어 지속시간만 줄이고 비제어 상태 지속시간은 바꾸지 않는다.");
        Assert.That(
            wounded.Allies.Single().BaseStats[StatKey.MaxHealth],
            Is.EqualTo(baseline.Allies.Single().BaseStats[StatKey.MaxHealth]),
            "전상은 최대 HP를 줄이지 않는다.");
    }

    private static ActiveRunState CreateRun(params string[] heroIds)
    {
        var assignments = heroIds
            .Select((heroId, index) => new
            {
                HeroId = heroId,
                Anchor = index switch
                {
                    0 => DeploymentAnchorId.FrontTop,
                    1 => DeploymentAnchorId.FrontCenter,
                    _ => DeploymentAnchorId.FrontBottom,
                },
            })
            .ToDictionary(value => value.Anchor, value => value.HeroId);
        var blueprint = new SquadBlueprintState(
            "bp-wound",
            "War Wound",
            TeamPostureType.StandardAdvance,
            "tactic-standard",
            assignments,
            heroIds,
            new Dictionary<string, string>(StringComparer.Ordinal));
        return new ActiveRunState(
            "run-wound",
            "site-wound",
            blueprint,
            new RunOverlayState(0, Array.Empty<string>(), Array.Empty<string>(), string.Empty, string.Empty),
            heroIds,
            IsQuickBattle: false);
    }

    private static CombatContentSnapshot CreateAbilityContent()
    {
        var active = new BattleSkillSpec(
            Id: "skill-field-mend",
            Name: "Field Mend",
            Kind: SkillKind.Heal,
            Power: 10f,
            Range: 3f,
            SlotKind: CompiledSkillSlots.CoreActive,
            DamageType: DamageType.Healing,
            PowerFlat: 10f,
            PhysCoeff: 0f,
            HealCoeff: 1f,
            AppliedStatuses: new[]
            {
                new StatusApplicationSpec("status-stun", "stun", 10f, 1f),
                new StatusApplicationSpec("status-guarded", "guarded", 10f, 1f),
            },
            ResolvedSlotKind: ActionSlotKind.SignatureActive);
        var utility = new BattleSkillSpec(
            "skill-field-utility",
            "Field Utility",
            SkillKind.Utility,
            0f,
            3f,
            SlotKind: CompiledSkillSlots.UtilityActive,
            ResolvedSlotKind: ActionSlotKind.FlexActive);
        var passive = new BattleSkillSpec(
            "skill-field-passive",
            "Field Passive",
            SkillKind.Buff,
            0f,
            0f,
            SlotKind: CompiledSkillSlots.Passive,
            ResolvedSlotKind: ActionSlotKind.SignaturePassive);
        var support = new BattleSkillSpec(
            "skill-field-support",
            "Field Support",
            SkillKind.Buff,
            0f,
            0f,
            SlotKind: CompiledSkillSlots.Support,
            ResolvedSlotKind: ActionSlotKind.FlexPassive);
        var skills = new[] { active, utility, passive, support };
        var baseStats = new Dictionary<StatKey, float>
        {
            [StatKey.MaxHealth] = 100f,
            [StatKey.HealPower] = 20f,
            [StatKey.PhysPower] = 5f,
            [StatKey.AttackSpeed] = 1f,
            [StatKey.AttackRange] = 1.5f,
            [StatKey.AttackWindup] = 0.2f,
            [StatKey.AttackCooldown] = 1f,
            [StatKey.MoveSpeed] = 2f,
        };
        var archetype = new CombatArchetypeTemplate(
            "medic",
            "Medic",
            "human",
            "mystic",
            DeploymentAnchorId.BackCenter,
            baseStats,
            Array.Empty<TacticRule>(),
            skills,
            SignatureActive: active);
        var emptyPackages = new Dictionary<string, CombatModifierPackage>(StringComparer.Ordinal);
        return new CombatContentSnapshot(
            Archetypes: new Dictionary<string, CombatArchetypeTemplate>(StringComparer.Ordinal) { [archetype.Id] = archetype },
            TraitPackages: emptyPackages,
            ItemPackages: emptyPackages,
            AffixPackages: emptyPackages,
            AugmentPackages: emptyPackages,
            SkillCatalog: skills.ToDictionary(skill => skill.Id, StringComparer.Ordinal),
            TeamTactics: new Dictionary<string, TeamTacticTemplate>(StringComparer.Ordinal)
            {
                ["tactic-standard"] = new TeamTacticTemplate(
                    "tactic-standard",
                    new TeamTacticProfile("tactic-standard", "Standard", TeamPostureType.StandardAdvance)),
            },
            RoleInstructions: new Dictionary<string, RoleInstructionTemplate>(StringComparer.Ordinal),
            PassiveNodes: new Dictionary<string, PassiveNodeTemplate>(StringComparer.Ordinal),
            AugmentCatalog: new Dictionary<string, AugmentCatalogEntry>(StringComparer.Ordinal),
            SynergyCatalog: new Dictionary<string, SynergyTierTemplate>(StringComparer.Ordinal),
            StatusFamilies: new Dictionary<string, StatusFamilyTemplate>(StringComparer.Ordinal)
            {
                ["stun"] = new StatusFamilyTemplate(
                    "stun", StatusGroupValue.Control, true, true, true, 1f, false, string.Empty, false, Array.Empty<string>()),
                ["guarded"] = new StatusFamilyTemplate(
                    "guarded", StatusGroupValue.DefensiveBoon, false, false, false, 1f, false, string.Empty, false, Array.Empty<string>()),
            },
            WarWound: Spec);
    }

    private static BattleLoadoutSnapshot Compile(
        CombatContentSnapshot content,
        IReadOnlyCollection<string> activeWoundHeroIds)
    {
        var hero = new HeroRecord("hero-a", "Hero A", "medic", "human", "mystic", string.Empty, string.Empty);
        return new LoadoutCompiler().Compile(
            new[] { hero },
            new Dictionary<string, HeroLoadoutState>(StringComparer.Ordinal),
            new Dictionary<string, HeroProgressionState>(StringComparer.Ordinal)
            {
                [hero.Id] = new HeroProgressionState(
                    hero.Id,
                    1,
                    0,
                    Array.Empty<string>(),
                    new[] { "skill-field-mend", "skill-field-utility", "skill-field-passive", "skill-field-support" }),
            },
            new Dictionary<string, ItemInstanceState>(StringComparer.Ordinal),
            new Dictionary<string, SkillInstanceState>(StringComparer.Ordinal),
            new Dictionary<string, PassiveBoardSelectionState>(StringComparer.Ordinal),
            new PermanentAugmentLoadoutState("bp-wound", Array.Empty<string>()),
            new SquadBlueprintState(
                "bp-wound",
                "War Wound",
                TeamPostureType.StandardAdvance,
                "tactic-standard",
                new Dictionary<DeploymentAnchorId, string> { [DeploymentAnchorId.BackCenter] = hero.Id },
                new[] { hero.Id },
                new Dictionary<string, string>(StringComparer.Ordinal)),
            new RunOverlayState(0, Array.Empty<string>(), Array.Empty<string>(), LoadoutCompiler.CurrentCompileVersion, string.Empty),
            content,
            warWoundSpec: Spec,
            activeWoundHeroIds: activeWoundHeroIds);
    }

    private static UnitSnapshot CreateUnit(BattleUnitLoadout loadout)
        => new(
            new EntityId(loadout.Id),
            TeamSide.Ally,
            loadout,
            BattleFactory.ResolveAnchorPosition(TeamSide.Ally, loadout.PreferredAnchor),
            BattleFactory.ResolveSpawnPosition(TeamSide.Ally, loadout.PreferredAnchor));
}
