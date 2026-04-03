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
public sealed class SynergyReachabilityValidatorTests
{
    [Test]
    public void GrammarPath_MajorReachable_ProducesNoErrors()
    {
        // 4 human archetypes → race major threshold 4 is reachable
        var snapshot = BuildSnapshot(new[]
        {
            MakeArchetype("a1", "human", "vanguard"),
            MakeArchetype("a2", "human", "duelist"),
            MakeArchetype("a3", "human", "ranger"),
            MakeArchetype("a4", "human", "mystic"),
        });

        var slice = new FirstPlayableSliceDefinition
        {
            UnitBlueprintIds = new[] { "a1", "a2", "a3", "a4" },
            SynergyGrammar = new[]
            {
                new SynergyGrammarEntry { FamilyId = "human", FamilyType = SynergyFamilyType.Race, MinorThreshold = 2, MajorThreshold = 4 },
            },
        };

        var issues = new List<ContentValidationIssue>();
        SynergyReachabilityValidator.Validate(snapshot, slice, issues);

        Assert.That(issues.Where(i => i.Severity == ContentValidationSeverity.Error), Is.Empty,
            "4명의 human archetype이 있으므로 major 4 도달 가능 — 에러 없어야 함");
    }

    [Test]
    public void GrammarPath_MajorUnreachable_ProducesError()
    {
        // Only 3 human archetypes → race major threshold 4 unreachable
        var snapshot = BuildSnapshot(new[]
        {
            MakeArchetype("a1", "human", "vanguard"),
            MakeArchetype("a2", "human", "duelist"),
            MakeArchetype("a3", "human", "ranger"),
            MakeArchetype("a4", "elf", "mystic"),
        });

        var slice = new FirstPlayableSliceDefinition
        {
            UnitBlueprintIds = new[] { "a1", "a2", "a3", "a4" },
            SynergyGrammar = new[]
            {
                new SynergyGrammarEntry { FamilyId = "human", FamilyType = SynergyFamilyType.Race, MinorThreshold = 2, MajorThreshold = 4 },
            },
        };

        var issues = new List<ContentValidationIssue>();
        SynergyReachabilityValidator.Validate(snapshot, slice, issues);

        Assert.That(issues.Any(i => i.Code == "synergy.reachability.major_unreachable"), Is.True,
            "human이 3명뿐이므로 major 4 도달 불가 에러가 있어야 함");
    }

    [Test]
    public void GrammarPath_MinorUnreachable_ProducesError()
    {
        // Only 1 elf archetype → minor threshold 2 unreachable
        var snapshot = BuildSnapshot(new[]
        {
            MakeArchetype("a1", "elf", "ranger"),
            MakeArchetype("a2", "human", "vanguard"),
        });

        var slice = new FirstPlayableSliceDefinition
        {
            UnitBlueprintIds = new[] { "a1", "a2" },
            SynergyGrammar = new[]
            {
                new SynergyGrammarEntry { FamilyId = "elf", FamilyType = SynergyFamilyType.Race, MinorThreshold = 2, MajorThreshold = 4 },
            },
        };

        var issues = new List<ContentValidationIssue>();
        SynergyReachabilityValidator.Validate(snapshot, slice, issues);

        Assert.That(issues.Any(i => i.Code == "synergy.reachability.minor_unreachable"), Is.True,
            "elf가 1명뿐이므로 minor 2 도달 불가 에러가 있어야 함");
    }

    [Test]
    public void GrammarPath_ClassFamily_CountsByClassId()
    {
        // 3 vanguards → class major threshold 3 reachable
        var snapshot = BuildSnapshot(new[]
        {
            MakeArchetype("a1", "human", "vanguard"),
            MakeArchetype("a2", "elf", "vanguard"),
            MakeArchetype("a3", "orc", "vanguard"),
        });

        var slice = new FirstPlayableSliceDefinition
        {
            UnitBlueprintIds = new[] { "a1", "a2", "a3" },
            SynergyGrammar = new[]
            {
                new SynergyGrammarEntry { FamilyId = "vanguard", FamilyType = SynergyFamilyType.Class, MinorThreshold = 2, MajorThreshold = 3 },
            },
        };

        var issues = new List<ContentValidationIssue>();
        SynergyReachabilityValidator.Validate(snapshot, slice, issues);

        Assert.That(issues.Where(i => i.Severity == ContentValidationSeverity.Error), Is.Empty,
            "vanguard 3명 → class major 3 도달 가능");
    }

    [Test]
    public void CatalogFallback_Fires_WhenGrammarEmpty()
    {
        var archetypes = new[]
        {
            MakeArchetype("a1", "human", "vanguard"),
            MakeArchetype("a2", "human", "duelist"),
        };
        var synergyCatalog = new Dictionary<string, SynergyTierTemplate>
        {
            ["human:2"] = new SynergyTierTemplate("human:2", new TeamSynergyTierRule("human_synergy", "human", 2, Array.Empty<StatModifier>())),
            ["human:4"] = new SynergyTierTemplate("human:4", new TeamSynergyTierRule("human_synergy", "human", 4, Array.Empty<StatModifier>())),
        };

        var snapshot = BuildSnapshot(archetypes, synergyCatalog: synergyCatalog);
        var slice = new FirstPlayableSliceDefinition
        {
            UnitBlueprintIds = new[] { "a1", "a2" },
            SynergyFamilyIds = new[] { "human_synergy" },
            SynergyGrammar = Array.Empty<SynergyGrammarEntry>(), // empty → fallback
        };

        var issues = new List<ContentValidationIssue>();
        SynergyReachabilityValidator.Validate(snapshot, slice, issues);

        Assert.That(issues.Any(i => i.Code == "synergy.reachability.no_grammar"), Is.True,
            "Grammar이 비어 있으므로 fallback 경고가 있어야 함");
        Assert.That(issues.Any(i => i.Code == "synergy.reachability.major_unreachable"), Is.True,
            "human 2명으로 max threshold 4 도달 불가");
    }

    [Test]
    public void NullInputs_DoesNotThrow()
    {
        var issues = new List<ContentValidationIssue>();
        SynergyReachabilityValidator.Validate(null!, null!, issues);
        Assert.That(issues, Is.Empty);
    }

    // ── helpers ──

    private static CombatArchetypeTemplate MakeArchetype(string id, string raceId, string classId)
    {
        return new CombatArchetypeTemplate(
            id,
            id,
            raceId,
            classId,
            DeploymentAnchorId.FrontCenter,
            new Dictionary<StatKey, float>(),
            Array.Empty<TacticRule>(),
            Array.Empty<BattleSkillSpec>());
    }

    private static CombatContentSnapshot BuildSnapshot(
        IReadOnlyList<CombatArchetypeTemplate> archetypes,
        IReadOnlyDictionary<string, SynergyTierTemplate>? synergyCatalog = null)
    {
        return new CombatContentSnapshot(
            Archetypes: archetypes.ToDictionary(a => a.Id, StringComparer.Ordinal),
            TraitPackages: new Dictionary<string, CombatModifierPackage>(),
            ItemPackages: new Dictionary<string, CombatModifierPackage>(),
            AffixPackages: new Dictionary<string, CombatModifierPackage>(),
            AugmentPackages: new Dictionary<string, CombatModifierPackage>(),
            SkillCatalog: new Dictionary<string, BattleSkillSpec>(),
            TeamTactics: new Dictionary<string, TeamTacticTemplate>(),
            RoleInstructions: new Dictionary<string, RoleInstructionTemplate>(),
            PassiveNodes: new Dictionary<string, PassiveNodeTemplate>(),
            AugmentCatalog: new Dictionary<string, AugmentCatalogEntry>(),
            SynergyCatalog: synergyCatalog ?? new Dictionary<string, SynergyTierTemplate>());
    }
}
