using System;
using System.Collections.Generic;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Core.Contracts;
using SM.Core.Stats;
using SM.Editor.Authoring.CombatSandbox;
using SM.Meta.Model;
using SM.Tests.EditMode.Fakes;
using SM.Unity.Sandbox;
using UnityEngine;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class CombatSandboxLaunchTruthDiffTests
{
    [Test]
    public void CombatSandboxLaunchTruthDiffService_FlagsBaselineAndOutOfRosterScopeUnits()
    {
        var race = ScriptableObject.CreateInstance<RaceDefinition>();
        race.Id = "human";

        var @class = ScriptableObject.CreateInstance<ClassDefinition>();
        @class.Id = "vanguard";

        var warden = ScriptableObject.CreateInstance<UnitArchetypeDefinition>();
        warden.Id = "warden";
        warden.Race = race;
        warden.Class = @class;
        warden.PreferredTeamPosture = TeamPostureTypeValue.StandardAdvance;
        warden.Loadout = new UnitLoadoutDefinition
        {
            SignatureActive = CreateSkillAsset("skill_power_strike"),
            FlexActive = CreateSkillAsset("skill_warden_utility"),
            SignaturePassive = new PassiveDefinition { Id = "skill_vanguard_passive_1" },
            FlexPassive = new PassiveDefinition { Id = "skill_vanguard_support_1" },
        };

        var lookup = new FakeCombatContentLookup(
            firstPlayableSlice: new FirstPlayableSliceDefinition
            {
                UnitBlueprintIds = new[] { "warden" },
                SynergyGrammar = new[]
                {
                    new SynergyGrammarEntry { FamilyId = "synergy_human", MinorThreshold = 2, MajorThreshold = 4 },
                    new SynergyGrammarEntry { FamilyId = "synergy_vanguard", MinorThreshold = 2, MajorThreshold = 3 },
                },
            },
            archetypes: new Dictionary<string, UnitArchetypeDefinition> { ["warden"] = warden });

        try
        {
            var scenario = new CombatSandboxCompiledScenario(
                "baseline",
                "Baseline",
                CombatSandboxLaneKind.DirectCombatSandbox,
                17,
                new CombatSandboxExecutionSettings(),
                CreateTeam(
                    "left",
                    new[]
                    {
                        CreateUnit("left.warden", "warden", "human", "vanguard"),
                    },
                    TeamPostureType.StandardAdvance,
                    "team_tactic_standard_advance"),
                CreateTeam(
                    "right",
                    new[]
                    {
                        CreateUnit("enemy.special", "boss_special", "undead", "mystic"),
                    },
                    TeamPostureType.StandardAdvance,
                    "team_tactic_standard_advance"),
                Array.Empty<string>());

            var preview = CombatSandboxLaunchTruthDiffService.BuildPreview(scenario, lookup);

            Assert.That(preview.BreakpointSummary, Does.Contain("race:human count=1 thresholds=2/4 reached=none"));
            Assert.That(preview.DriftSummary, Does.Contain("- left.warden [warden] baseline"));
            Assert.That(preview.DriftSummary, Does.Contain("- enemy.special [boss_special] out_of_roster_scope"));
            Assert.That(preview.MembershipWarning, Does.Contain("right: enemy.special[boss_special]"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(warden.Loadout.SignatureActive);
            UnityEngine.Object.DestroyImmediate(warden.Loadout.FlexActive);
            UnityEngine.Object.DestroyImmediate(race);
            UnityEngine.Object.DestroyImmediate(@class);
            UnityEngine.Object.DestroyImmediate(warden);
        }
    }

    [Test]
    public void CombatSandboxLaunchTruthDiffService_DetectsSlotEquipmentPassiveAugmentAndPostureDrift()
    {
        var race = ScriptableObject.CreateInstance<RaceDefinition>();
        race.Id = "human";

        var @class = ScriptableObject.CreateInstance<ClassDefinition>();
        @class.Id = "vanguard";

        var warden = ScriptableObject.CreateInstance<UnitArchetypeDefinition>();
        warden.Id = "warden";
        warden.Race = race;
        warden.Class = @class;
        warden.PreferredTeamPosture = TeamPostureTypeValue.HoldLine;
        warden.Loadout = new UnitLoadoutDefinition
        {
            SignatureActive = CreateSkillAsset("skill_power_strike"),
            FlexActive = CreateSkillAsset("skill_warden_utility"),
            SignaturePassive = new PassiveDefinition { Id = "skill_vanguard_passive_1" },
            FlexPassive = new PassiveDefinition { Id = "skill_vanguard_support_1" },
        };
        var board = ScriptableObject.CreateInstance<PassiveBoardDefinition>();
        board.Id = "board_vanguard";

        var lookup = new FakeCombatContentLookup(
            firstPlayableSlice: new FirstPlayableSliceDefinition
            {
                UnitBlueprintIds = new[] { "warden" },
            },
            archetypes: new Dictionary<string, UnitArchetypeDefinition> { ["warden"] = warden },
            passiveBoards: new Dictionary<string, PassiveBoardDefinition> { ["board_vanguard"] = board });

        try
        {
            var unit = CreateUnit(
                "left.warden",
                "warden",
                "human",
                "vanguard",
                signatureActiveId: "skill_wrong_signature",
                flexActiveId: "skill_wrong_flex",
                signaturePassiveId: "skill_wrong_signature_passive",
                flexPassiveId: "skill_wrong_flex_passive");
            var scenario = new CombatSandboxCompiledScenario(
                "drift",
                "Drift",
                CombatSandboxLaneKind.DirectCombatSandbox,
                29,
                new CombatSandboxExecutionSettings(),
                CreateTeam(
                    "left",
                    new[] { unit },
                    TeamPostureType.StandardAdvance,
                    "team_tactic_standard_advance",
                    new[]
                    {
                        new CompileProvenanceEntry(unit.Id, ModifierSource.Item, "item_guardian_shield", "item", Array.Empty<string>()),
                        new CompileProvenanceEntry(unit.Id, ModifierSource.Other, "passive_vanguard_small_01", "passive_numeric", Array.Empty<string>()),
                        new CompileProvenanceEntry(unit.Id, ModifierSource.Augment, "augment_perm_legacy_oath", "augment_permanent", Array.Empty<string>()),
                    }),
                CreateTeam("right", Array.Empty<BattleUnitLoadout>(), TeamPostureType.StandardAdvance, "team_tactic_standard_advance"),
                Array.Empty<string>());

            var preview = CombatSandboxLaunchTruthDiffService.BuildPreview(scenario, lookup);

            Assert.That(preview.DriftSummary, Does.Contain("slot signature_active:skill_power_strike->skill_wrong_signature"));
            Assert.That(preview.DriftSummary, Does.Contain("flex_active:skill_warden_utility->skill_wrong_flex"));
            Assert.That(preview.DriftSummary, Does.Contain("signature_passive:skill_vanguard_passive_1->skill_wrong_signature_passive"));
            Assert.That(preview.DriftSummary, Does.Contain("flex_passive:skill_vanguard_support_1->skill_wrong_flex_passive"));
            Assert.That(preview.DriftSummary, Does.Contain("equipment +item_guardian_shield"));
            Assert.That(preview.DriftSummary, Does.Contain("passive-board baseline=board_vanguard +passive_vanguard_small_01"));
            Assert.That(preview.DriftSummary, Does.Contain("augment +perm[augment_perm_legacy_oath]"));
            Assert.That(preview.DriftSummary, Does.Contain("posture/tactic posture:HoldLine->StandardAdvance, tactic:team_tactic_hold_line->team_tactic_standard_advance"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(warden.Loadout.SignatureActive);
            UnityEngine.Object.DestroyImmediate(warden.Loadout.FlexActive);
            UnityEngine.Object.DestroyImmediate(board);
            UnityEngine.Object.DestroyImmediate(race);
            UnityEngine.Object.DestroyImmediate(@class);
            UnityEngine.Object.DestroyImmediate(warden);
        }
    }

    private static SkillDefinitionAsset CreateSkillAsset(string id)
    {
        var skill = ScriptableObject.CreateInstance<SkillDefinitionAsset>();
        skill.Id = id;
        return skill;
    }

    private static CombatSandboxCompiledTeam CreateTeam(
        string teamId,
        IReadOnlyList<BattleUnitLoadout> units,
        TeamPostureType posture,
        string tacticId,
        IReadOnlyList<CompileProvenanceEntry>? provenance = null)
    {
        return new CombatSandboxCompiledTeam(
            teamId,
            teamId,
            SandboxLoadoutSourceKind.AuthoredSyntheticTeam,
            teamId,
            new SquadBlueprintState(
                $"blueprint.{teamId}",
                teamId,
                posture,
                tacticId,
                new Dictionary<DeploymentAnchorId, string>(),
                Array.Empty<string>(),
                new Dictionary<string, string>()),
            new RunOverlayState(0, Array.Empty<string>(), Array.Empty<string>(), "test", string.Empty),
            new BattleLoadoutSnapshot(
                teamId,
                "test",
                $"{teamId}.hash",
                new TeamTacticProfile(tacticId, tacticId, posture),
                units,
                Array.Empty<string>(),
                Array.Empty<string>(),
                provenance),
            Array.Empty<string>(),
            Array.Empty<string>());
    }

    private static BattleUnitLoadout CreateUnit(
        string id,
        string archetypeId,
        string raceId,
        string classId,
        string signatureActiveId = "skill_power_strike",
        string flexActiveId = "skill_warden_utility",
        string signaturePassiveId = "skill_vanguard_passive_1",
        string flexPassiveId = "skill_vanguard_support_1")
    {
        return new BattleUnitLoadout(
            id,
            id,
            raceId,
            classId,
            DeploymentAnchorId.FrontCenter,
            new Dictionary<SM.Core.Stats.StatKey, float>(),
            Array.Empty<UnitRuleChain>(),
            Array.Empty<BattleSkillSpec>(),
            new TeamTacticProfile("team_tactic_standard_advance", "standard", TeamPostureType.StandardAdvance),
            null,
            ArchetypeId: archetypeId,
            SignatureActive: new BattleSkillSpec(signatureActiveId, signatureActiveId, SkillKind.Strike, 1f, 1f, ResolvedSlotKind: ActionSlotKind.SignatureActive),
            FlexActive: new BattleSkillSpec(flexActiveId, flexActiveId, SkillKind.Strike, 1f, 1f, SlotKind: CompiledSkillSlots.UtilityActive, ResolvedSlotKind: ActionSlotKind.FlexActive),
            SignaturePassive: new BattlePassiveSpec(signaturePassiveId, signaturePassiveId, ActionSlotKind.SignaturePassive),
            FlexPassive: new BattlePassiveSpec(flexPassiveId, flexPassiveId, ActionSlotKind.FlexPassive));
    }
}
