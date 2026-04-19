using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Core.Stats;
using SM.Editor.Validation;
using SM.Meta.Model;

namespace SM.Tests.EditMode;

[Category("BatchOnly")]
public sealed class PoolLeakValidatorTests
{
    [Test]
    public void NoLeaks_ProducesNoErrors()
    {
        var snapshot = BuildSnapshot(
            archetypes: new[]
            {
                MakeArchetype("a1", recruitable: true),
            },
            affixKeys: new[] { "affix1" },
            augments: new[]
            {
                MakeAugment("aug_t1", false),
                MakeAugment("aug_p1", true),
            });

        var slice = new FirstPlayableSliceDefinition
        {
            UnitBlueprintIds = new[] { "a1" },
            AffixIds = new[] { "affix1" },
            TemporaryAugmentIds = new[] { "aug_t1" },
            PermanentAugmentIds = new[] { "aug_p1" },
        };

        var issues = new List<ContentValidationIssue>();
        PoolLeakValidator.Validate(snapshot, slice, issues);

        var errors = issues.Where(i => i.Severity == ContentValidationSeverity.Error).ToList();
        Assert.That(errors, Is.Empty, "모든 콘텐츠가 slice 안에 있으므로 leak 에러 없어야 함");
    }

    [Test]
    public void RecruitLeak_RecruitableArchetypeOutsideSlice()
    {
        var snapshot = BuildSnapshot(
            archetypes: new[]
            {
                MakeArchetype("a1", recruitable: true),
                MakeArchetype("a_leaked", recruitable: true),
            });

        var slice = new FirstPlayableSliceDefinition
        {
            UnitBlueprintIds = new[] { "a1" }, // a_leaked not listed
        };

        var issues = new List<ContentValidationIssue>();
        PoolLeakValidator.Validate(snapshot, slice, issues);

        Assert.That(issues.Any(i => i.Code == "pool_leak.recruit" && i.Message.Contains("a_leaked")), Is.True,
            "Recruitable 'a_leaked'이 slice에 없으므로 recruit leak 에러 필요");
    }

    [Test]
    public void NonRecruitableArchetype_OutsideSlice_IsNotALeak()
    {
        var snapshot = BuildSnapshot(
            archetypes: new[]
            {
                MakeArchetype("a1", recruitable: true),
                MakeArchetype("a_npc", recruitable: false),
            });

        var slice = new FirstPlayableSliceDefinition
        {
            UnitBlueprintIds = new[] { "a1" }, // a_npc not listed but not recruitable
        };

        var issues = new List<ContentValidationIssue>();
        PoolLeakValidator.Validate(snapshot, slice, issues);

        Assert.That(issues.Any(i => i.Code == "pool_leak.recruit"), Is.False,
            "Non-recruitable archetype는 leak이 아님");
    }

    [Test]
    public void AugmentLeak_TempAugmentOutsideSlice()
    {
        var snapshot = BuildSnapshot(
            augments: new[]
            {
                MakeAugment("aug_t1", false),
                MakeAugment("aug_leaked", false),
            });

        var slice = new FirstPlayableSliceDefinition
        {
            TemporaryAugmentIds = new[] { "aug_t1" },
        };

        var issues = new List<ContentValidationIssue>();
        PoolLeakValidator.Validate(snapshot, slice, issues);

        Assert.That(issues.Any(i => i.Code == "pool_leak.temporary_augment" && i.Message.Contains("aug_leaked")), Is.True,
            "slice에 없는 temporary augment는 leak 에러여야 함");
    }

    [Test]
    public void AugmentLeak_PermanentAugmentOutsideSlice()
    {
        var snapshot = BuildSnapshot(
            augments: new[]
            {
                MakeAugment("aug_p1", true),
                MakeAugment("aug_p_leaked", true),
            });

        var slice = new FirstPlayableSliceDefinition
        {
            PermanentAugmentIds = new[] { "aug_p1" },
        };

        var issues = new List<ContentValidationIssue>();
        PoolLeakValidator.Validate(snapshot, slice, issues);

        Assert.That(issues.Any(i => i.Code == "pool_leak.permanent_augment" && i.Message.Contains("aug_p_leaked")), Is.True,
            "slice에 없는 permanent augment는 leak 에러여야 함");
    }

    [Test]
    public void AffixLeak_AffixOutsideSlice()
    {
        var snapshot = BuildSnapshot(
            affixKeys: new[] { "affix1", "affix_leaked" });

        var slice = new FirstPlayableSliceDefinition
        {
            AffixIds = new[] { "affix1" },
        };

        var issues = new List<ContentValidationIssue>();
        PoolLeakValidator.Validate(snapshot, slice, issues);

        Assert.That(issues.Any(i => i.Code == "pool_leak.affix" && i.Message.Contains("affix_leaked")), Is.True,
            "slice에 없는 affix는 leak 에러여야 함");
    }

    [Test]
    public void FlexPoolLeak_SkillOutsideSlice()
    {
        var leakedSkill = new BattleSkillSpec("skill_leaked", "Leaked", SkillKind.Strike, 5f, 1.2f);
        var archetype = new CombatArchetypeTemplate(
            "a1", "a1", "human", "vanguard",
            DeploymentAnchorId.FrontCenter,
            new Dictionary<StatKey, float>(),
            Array.Empty<TacticRule>(),
            Array.Empty<BattleSkillSpec>(),
            RecruitFlexActivePool: new[] { leakedSkill });

        var snapshot = BuildSnapshot(archetypes: new[] { archetype });

        var slice = new FirstPlayableSliceDefinition
        {
            UnitBlueprintIds = new[] { "a1" },
            FlexActiveIds = Array.Empty<string>(), // skill_leaked not listed
        };

        var issues = new List<ContentValidationIssue>();
        PoolLeakValidator.Validate(snapshot, slice, issues);

        Assert.That(issues.Any(i => i.Code == "pool_leak.flex_active" && i.Message.Contains("skill_leaked")), Is.True,
            "FlexActiveIds에 없는 pool skill은 leak 에러여야 함");
    }

    [Test]
    public void NullInputs_DoesNotThrow()
    {
        var issues = new List<ContentValidationIssue>();
        PoolLeakValidator.Validate(null!, null!, issues);
        Assert.That(issues, Is.Empty);
    }

    // ── helpers ──

    private static CombatArchetypeTemplate MakeArchetype(string id, bool recruitable = false)
    {
        return new CombatArchetypeTemplate(id, id, "human", "vanguard",
            DeploymentAnchorId.FrontCenter, new Dictionary<StatKey, float>(),
            Array.Empty<TacticRule>(), Array.Empty<BattleSkillSpec>(),
            IsRecruitable: recruitable);
    }

    private static AugmentCatalogEntry MakeAugment(string id, bool isPermanent)
    {
        return new AugmentCatalogEntry(id, "test", "family", 1, isPermanent, false,
            Array.Empty<string>(), Array.Empty<string>(),
            new CombatModifierPackage("empty", ModifierSource.Other, Array.Empty<StatModifier>()));
    }

    private static CombatContentSnapshot BuildSnapshot(
        CombatArchetypeTemplate[]? archetypes = null,
        string[]? affixKeys = null,
        AugmentCatalogEntry[]? augments = null)
    {
        var archetypeDict = (archetypes ?? Array.Empty<CombatArchetypeTemplate>())
            .ToDictionary(a => a.Id, StringComparer.Ordinal);
        var affixDict = (affixKeys ?? Array.Empty<string>())
            .ToDictionary(id => id, _ => new CombatModifierPackage("empty", ModifierSource.Other, Array.Empty<StatModifier>()), StringComparer.Ordinal);
        var augmentDict = (augments ?? Array.Empty<AugmentCatalogEntry>())
            .ToDictionary(a => a.Id, StringComparer.Ordinal);

        return new CombatContentSnapshot(
            Archetypes: archetypeDict,
            TraitPackages: new Dictionary<string, CombatModifierPackage>(),
            ItemPackages: new Dictionary<string, CombatModifierPackage>(),
            AffixPackages: affixDict,
            AugmentPackages: new Dictionary<string, CombatModifierPackage>(),
            SkillCatalog: new Dictionary<string, BattleSkillSpec>(),
            TeamTactics: new Dictionary<string, TeamTacticTemplate>(),
            RoleInstructions: new Dictionary<string, RoleInstructionTemplate>(),
            PassiveNodes: new Dictionary<string, PassiveNodeTemplate>(),
            AugmentCatalog: augmentDict,
            SynergyCatalog: new Dictionary<string, SynergyTierTemplate>());
    }
}
