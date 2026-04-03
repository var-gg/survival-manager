using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Core.Stats;
using SM.Editor.Validation;
using SM.Meta.Model;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class FirstPlayableClosureValidatorTests
{
    [Test]
    public void CleanSlice_ProducesNoIssues()
    {
        var (snapshot, slice) = BuildAligned(new[] { "a1", "a2" }, new[] { "affix1" }, new[] { "aug_t1" }, new[] { "aug_p1" });

        var issues = new List<ContentValidationIssue>();
        FirstPlayableClosureValidator.Validate(snapshot, slice, issues);

        var errors = issues.Where(i => i.Severity == ContentValidationSeverity.Error).ToList();
        Assert.That(errors, Is.Empty, "정렬된 slice는 에러가 없어야 함");
    }

    [Test]
    public void DuplicateInSlice_ProducesDuplicateError()
    {
        var (snapshot, slice) = BuildAligned(new[] { "a1", "a1" }, new[] { "affix1" }, new[] { "aug_t1" }, new[] { "aug_p1" });

        var issues = new List<ContentValidationIssue>();
        FirstPlayableClosureValidator.Validate(snapshot, slice, issues);

        Assert.That(issues.Any(i => i.Code == "closure.unit_blueprint.duplicate"), Is.True,
            "UnitBlueprintIds에 중복 'a1'이 있으므로 duplicate 에러 필요");
    }

    [Test]
    public void OverCap_ProducesOverCapError()
    {
        // AffixCap defaults to 24. Create 25 affixes to exceed.
        var affixIds = Enumerable.Range(1, 25).Select(i => $"affix{i}").ToArray();
        var affixDict = affixIds.ToDictionary(id => id, _ => EmptyPackage(), StringComparer.Ordinal);

        var snapshot = new CombatContentSnapshot(
            Archetypes: new Dictionary<string, CombatArchetypeTemplate>(),
            TraitPackages: new Dictionary<string, CombatModifierPackage>(),
            ItemPackages: new Dictionary<string, CombatModifierPackage>(),
            AffixPackages: affixDict,
            AugmentPackages: new Dictionary<string, CombatModifierPackage>(),
            SkillCatalog: new Dictionary<string, BattleSkillSpec>(),
            TeamTactics: new Dictionary<string, TeamTacticTemplate>(),
            RoleInstructions: new Dictionary<string, RoleInstructionTemplate>(),
            PassiveNodes: new Dictionary<string, PassiveNodeTemplate>(),
            AugmentCatalog: new Dictionary<string, AugmentCatalogEntry>(),
            SynergyCatalog: new Dictionary<string, SynergyTierTemplate>());

        var slice = new FirstPlayableSliceDefinition
        {
            AffixIds = affixIds,
            AffixCap = 24,
        };

        var issues = new List<ContentValidationIssue>();
        FirstPlayableClosureValidator.Validate(snapshot, slice, issues);

        Assert.That(issues.Any(i => i.Code == "closure.affix.over_cap"), Is.True,
            "25개 affix > cap 24 이므로 over_cap 에러 필요");
    }

    [Test]
    public void DanglingId_InSliceButNotSnapshot_ProducesDanglingError()
    {
        // "a_ghost" in slice but not in snapshot
        var snapshot = new CombatContentSnapshot(
            Archetypes: new Dictionary<string, CombatArchetypeTemplate>
            {
                ["a1"] = MakeArchetype("a1"),
            },
            TraitPackages: new Dictionary<string, CombatModifierPackage>(),
            ItemPackages: new Dictionary<string, CombatModifierPackage>(),
            AffixPackages: new Dictionary<string, CombatModifierPackage>(),
            AugmentPackages: new Dictionary<string, CombatModifierPackage>(),
            SkillCatalog: new Dictionary<string, BattleSkillSpec>(),
            TeamTactics: new Dictionary<string, TeamTacticTemplate>(),
            RoleInstructions: new Dictionary<string, RoleInstructionTemplate>(),
            PassiveNodes: new Dictionary<string, PassiveNodeTemplate>(),
            AugmentCatalog: new Dictionary<string, AugmentCatalogEntry>(),
            SynergyCatalog: new Dictionary<string, SynergyTierTemplate>());

        var slice = new FirstPlayableSliceDefinition
        {
            UnitBlueprintIds = new[] { "a1", "a_ghost" },
        };

        var issues = new List<ContentValidationIssue>();
        FirstPlayableClosureValidator.Validate(snapshot, slice, issues);

        Assert.That(issues.Any(i => i.Code == "closure.unit_blueprint.dangling" && i.Message.Contains("a_ghost")), Is.True,
            "snapshot에 없는 'a_ghost'는 dangling 에러여야 함");
    }

    [Test]
    public void UnlistedId_InSnapshotButNotSlice_ProducesWarning()
    {
        var snapshot = new CombatContentSnapshot(
            Archetypes: new Dictionary<string, CombatArchetypeTemplate>
            {
                ["a1"] = MakeArchetype("a1"),
                ["a_extra"] = MakeArchetype("a_extra"),
            },
            TraitPackages: new Dictionary<string, CombatModifierPackage>(),
            ItemPackages: new Dictionary<string, CombatModifierPackage>(),
            AffixPackages: new Dictionary<string, CombatModifierPackage>(),
            AugmentPackages: new Dictionary<string, CombatModifierPackage>(),
            SkillCatalog: new Dictionary<string, BattleSkillSpec>(),
            TeamTactics: new Dictionary<string, TeamTacticTemplate>(),
            RoleInstructions: new Dictionary<string, RoleInstructionTemplate>(),
            PassiveNodes: new Dictionary<string, PassiveNodeTemplate>(),
            AugmentCatalog: new Dictionary<string, AugmentCatalogEntry>(),
            SynergyCatalog: new Dictionary<string, SynergyTierTemplate>());

        var slice = new FirstPlayableSliceDefinition
        {
            UnitBlueprintIds = new[] { "a1" },
        };

        var issues = new List<ContentValidationIssue>();
        FirstPlayableClosureValidator.Validate(snapshot, slice, issues);

        Assert.That(issues.Any(i =>
            i.Severity == ContentValidationSeverity.Warning
            && i.Code == "closure.unit_blueprint.unlisted"
            && i.Message.Contains("a_extra")), Is.True,
            "slice에 없는 'a_extra'는 unlisted 경고여야 함");
    }

    [Test]
    public void AugmentSplit_ValidatesByIsPermanentFlag()
    {
        var snapshot = new CombatContentSnapshot(
            Archetypes: new Dictionary<string, CombatArchetypeTemplate>(),
            TraitPackages: new Dictionary<string, CombatModifierPackage>(),
            ItemPackages: new Dictionary<string, CombatModifierPackage>(),
            AffixPackages: new Dictionary<string, CombatModifierPackage>(),
            AugmentPackages: new Dictionary<string, CombatModifierPackage>(),
            SkillCatalog: new Dictionary<string, BattleSkillSpec>(),
            TeamTactics: new Dictionary<string, TeamTacticTemplate>(),
            RoleInstructions: new Dictionary<string, RoleInstructionTemplate>(),
            PassiveNodes: new Dictionary<string, PassiveNodeTemplate>(),
            AugmentCatalog: new Dictionary<string, AugmentCatalogEntry>
            {
                ["at1"] = MakeAugment("at1", isPermanent: false),
                ["ap1"] = MakeAugment("ap1", isPermanent: true),
            },
            SynergyCatalog: new Dictionary<string, SynergyTierTemplate>());

        var slice = new FirstPlayableSliceDefinition
        {
            TemporaryAugmentIds = new[] { "at1" },
            PermanentAugmentIds = new[] { "ap1" },
        };

        var issues = new List<ContentValidationIssue>();
        FirstPlayableClosureValidator.Validate(snapshot, slice, issues);

        var augmentErrors = issues.Where(i =>
            i.Code.Contains("temporary_augment") || i.Code.Contains("permanent_augment")).ToList();
        Assert.That(augmentErrors.Where(i => i.Severity == ContentValidationSeverity.Error), Is.Empty,
            "IsPermanent 플래그에 따라 정확히 분류되므로 에러 없어야 함");
    }

    [Test]
    public void PassiveBoardDuplicate_ProducesError()
    {
        var snapshot = BuildMinimalSnapshot();

        var slice = new FirstPlayableSliceDefinition
        {
            PassiveBoardIds = new[] { "board_a", "board_a" },
        };

        var issues = new List<ContentValidationIssue>();
        FirstPlayableClosureValidator.Validate(snapshot, slice, issues);

        Assert.That(issues.Any(i => i.Code == "closure.passive_board.duplicate"), Is.True,
            "PassiveBoardIds에 중복이 있으므로 duplicate 에러 필요");
    }

    [Test]
    public void NullInputs_DoesNotThrow()
    {
        var issues = new List<ContentValidationIssue>();
        FirstPlayableClosureValidator.Validate(null!, null!, issues);
        Assert.That(issues, Is.Empty);
    }

    // ── helpers ──

    private static (CombatContentSnapshot, FirstPlayableSliceDefinition) BuildAligned(
        string[] unitIds, string[] affixIds, string[] tempAugIds, string[] permAugIds)
    {
        var archetypes = unitIds.Distinct().ToDictionary(id => id, id => MakeArchetype(id), StringComparer.Ordinal);
        var affixes = affixIds.Distinct().ToDictionary(id => id, _ => EmptyPackage(), StringComparer.Ordinal);
        var augCatalog = new Dictionary<string, AugmentCatalogEntry>(StringComparer.Ordinal);
        foreach (var id in tempAugIds.Distinct())
            augCatalog[id] = MakeAugment(id, false);
        foreach (var id in permAugIds.Distinct())
            augCatalog[id] = MakeAugment(id, true);

        var snapshot = new CombatContentSnapshot(
            Archetypes: archetypes,
            TraitPackages: new Dictionary<string, CombatModifierPackage>(),
            ItemPackages: new Dictionary<string, CombatModifierPackage>(),
            AffixPackages: affixes,
            AugmentPackages: new Dictionary<string, CombatModifierPackage>(),
            SkillCatalog: new Dictionary<string, BattleSkillSpec>(),
            TeamTactics: new Dictionary<string, TeamTacticTemplate>(),
            RoleInstructions: new Dictionary<string, RoleInstructionTemplate>(),
            PassiveNodes: new Dictionary<string, PassiveNodeTemplate>(),
            AugmentCatalog: augCatalog,
            SynergyCatalog: new Dictionary<string, SynergyTierTemplate>());

        var slice = new FirstPlayableSliceDefinition
        {
            UnitBlueprintIds = unitIds,
            AffixIds = affixIds,
            TemporaryAugmentIds = tempAugIds,
            PermanentAugmentIds = permAugIds,
        };

        return (snapshot, slice);
    }

    private static CombatContentSnapshot BuildMinimalSnapshot()
    {
        return new CombatContentSnapshot(
            Archetypes: new Dictionary<string, CombatArchetypeTemplate>(),
            TraitPackages: new Dictionary<string, CombatModifierPackage>(),
            ItemPackages: new Dictionary<string, CombatModifierPackage>(),
            AffixPackages: new Dictionary<string, CombatModifierPackage>(),
            AugmentPackages: new Dictionary<string, CombatModifierPackage>(),
            SkillCatalog: new Dictionary<string, BattleSkillSpec>(),
            TeamTactics: new Dictionary<string, TeamTacticTemplate>(),
            RoleInstructions: new Dictionary<string, RoleInstructionTemplate>(),
            PassiveNodes: new Dictionary<string, PassiveNodeTemplate>(),
            AugmentCatalog: new Dictionary<string, AugmentCatalogEntry>(),
            SynergyCatalog: new Dictionary<string, SynergyTierTemplate>());
    }

    private static CombatArchetypeTemplate MakeArchetype(string id)
    {
        return new CombatArchetypeTemplate(id, id, "human", "vanguard",
            DeploymentAnchorId.FrontCenter, new Dictionary<StatKey, float>(),
            Array.Empty<TacticRule>(), Array.Empty<BattleSkillSpec>());
    }

    private static AugmentCatalogEntry MakeAugment(string id, bool isPermanent)
    {
        return new AugmentCatalogEntry(id, "test", "family", 1, isPermanent, false,
            Array.Empty<string>(), Array.Empty<string>(), EmptyPackage());
    }

    private static CombatModifierPackage EmptyPackage()
    {
        return new CombatModifierPackage("empty", ModifierSource.Other, Array.Empty<StatModifier>());
    }
}
