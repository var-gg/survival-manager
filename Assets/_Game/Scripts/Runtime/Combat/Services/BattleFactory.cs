using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Core.Ids;

namespace SM.Combat.Services;

public static class BattleFactory
{
    public static BattleState Create(
        IReadOnlyList<BattleUnitLoadout> allyDefinitions,
        IReadOnlyList<BattleUnitLoadout> enemyDefinitions,
        TeamPostureType allyPosture = TeamPostureType.StandardAdvance,
        TeamPostureType enemyPosture = TeamPostureType.StandardAdvance,
        float fixedStepSeconds = BattleSimulator.DefaultFixedStepSeconds,
        int seed = 7,
        BattlefieldLayout? layout = null)
    {
        var resolved = layout ?? BattlefieldLayout.Default;
        var allyPackages = ResolveTeamPackages(allyDefinitions);
        var enemyPackages = ResolveTeamPackages(enemyDefinitions);
        var allyTactic = ResolveTeamTactic(allyDefinitions, allyPosture);
        var enemyTactic = ResolveTeamTactic(enemyDefinitions, enemyPosture);

        var allies = allyDefinitions.Select((def, index) =>
        {
            var merged = MergePackages(def, allyPackages);
            var formation = ResolveFormationPosition(resolved, TeamSide.Ally, merged, allyTactic);
            return new UnitSnapshot(new EntityId($"ally_{index}_{merged.Id}"), TeamSide.Ally, merged, formation.AnchorPosition, formation.SpawnPosition);
        }).ToList();

        var enemies = enemyDefinitions.Select((def, index) =>
        {
            var merged = MergePackages(def, enemyPackages);
            var formation = ResolveFormationPosition(resolved, TeamSide.Enemy, merged, enemyTactic);
            return new UnitSnapshot(new EntityId($"enemy_{index}_{merged.Id}"), TeamSide.Enemy, merged, formation.AnchorPosition, formation.SpawnPosition);
        }).ToList();

        var state = new BattleState(
            allies,
            enemies,
            allyTactic.Posture,
            enemyTactic.Posture,
            fixedStepSeconds,
            seed,
            allyTactic: allyTactic,
            enemyTactic: enemyTactic);
        RecordFormationTelemetry(state, resolved, TeamSide.Ally, allyTactic);
        RecordFormationTelemetry(state, resolved, TeamSide.Enemy, enemyTactic);
        return state;
    }

    private static IReadOnlyList<CombatModifierPackage> ResolveTeamPackages(IReadOnlyList<BattleUnitLoadout> definitions)
    {
        var precompiled = definitions
            .SelectMany(definition => definition.TeamPackages ?? new List<CombatModifierPackage>())
            .GroupBy(package => $"{package.Source}:{package.SourceId}")
            .Select(group => group.First())
            .ToList();
        return precompiled.Count > 0 ? precompiled : SynergyService.BuildForTeam(definitions);
    }

    private static BattleUnitLoadout MergePackages(BattleUnitLoadout definition, IReadOnlyList<CombatModifierPackage> teamPackages)
    {
        var merged = definition.NumericPackages.Concat(teamPackages).ToList();
        return definition with
        {
            Packages = merged,
            TeamPackages = teamPackages
        };
    }

    public static CombatVector2 ResolveAnchorPosition(TeamSide side, DeploymentAnchorId anchor)
        => BattlefieldLayout.Default.ResolveAnchorPosition(side, anchor);

    public static CombatVector2 ResolveSpawnPosition(TeamSide side, DeploymentAnchorId anchor)
        => BattlefieldLayout.Default.ResolveSpawnPosition(side, anchor);

    private static TeamTacticProfile ResolveTeamTactic(IReadOnlyList<BattleUnitLoadout> definitions, TeamPostureType fallbackPosture)
    {
        return definitions.FirstOrDefault(definition => definition.TeamTactic != null)?.TeamTactic
               ?? new TeamTacticProfile($"posture:{fallbackPosture}", fallbackPosture.ToString(), fallbackPosture);
    }

    private static FormationPosition ResolveFormationPosition(
        BattlefieldLayout layout,
        TeamSide side,
        BattleUnitLoadout definition,
        TeamTacticProfile teamTactic)
    {
        var baseAnchor = layout.ResolveAnchorPosition(side, definition.PreferredAnchor);
        var baseSpawn = layout.ResolveSpawnPosition(side, definition.PreferredAnchor);
        var offset = ResolveFormationOffset(side, definition, teamTactic, baseAnchor);
        return new FormationPosition(baseAnchor + offset, baseSpawn + offset);
    }

    private static CombatVector2 ResolveFormationOffset(
        TeamSide side,
        BattleUnitLoadout definition,
        TeamTacticProfile teamTactic,
        CombatVector2 baseAnchor)
    {
        var forward = side == TeamSide.Ally ? 1f : -1f;
        var compactness = Clamp01(teamTactic.Compactness);
        var width = ResolvePositive(teamTactic.Width, 1f);
        var depth = ResolvePositive(teamTactic.Depth, 1f);
        var lineSpacing = ResolvePositive(teamTactic.LineSpacing, 1f);
        var laneIndex = definition.PreferredAnchor.LaneIndex();
        var rowSign = definition.PreferredAnchor.IsFrontRow() ? 1f : -1f;

        var widthFactor = Lerp(1.24f, 0.62f, compactness) * width;
        var yOffset = (baseAnchor.Y * (widthFactor - 1f))
                      + (laneIndex == 0 ? ResolveDeterministicSigned(definition.Id, "lane") * 0.12f * (1f - compactness) : 0f)
                      + (ResolveDeterministicSigned(definition.Id, "role") * 0.08f * width)
                      + (teamTactic.FlankBias * laneIndex * 0.42f);

        var lineFactor = Lerp(1.18f, 0.72f, compactness) * lineSpacing;
        var xOffset = forward * rowSign * (lineFactor - 1f) * 0.42f * depth;
        xOffset += forward * (definition.PreferredAnchor.IsFrontRow() ? teamTactic.FrontSpacingBias : -teamTactic.BackSpacingBias);

        var roleInstruction = definition.RoleInstruction;
        if (roleInstruction != null)
        {
            xOffset += forward * ((roleInstruction.BacklinePressureBias * 0.28f) - (roleInstruction.RetreatBias * 0.24f));
            xOffset -= forward * roleInstruction.ProtectCarryBias * 0.18f;
            yOffset *= 1f - (Clamp01(roleInstruction.ProtectCarryBias + teamTactic.ProtectCarryBias) * 0.24f);
        }

        switch (definition.RoleVariant)
        {
            case RoleVariantTag.Diver:
            case RoleVariantTag.Executioner:
            case RoleVariantTag.Harrier:
                xOffset += forward * 0.16f;
                yOffset += ResolveDeterministicSigned(definition.Id, "flank") * (0.16f + MathF.Abs(teamTactic.FlankBias) * 0.12f);
                break;
            case RoleVariantTag.Peeler:
                xOffset -= forward * 0.12f;
                yOffset *= 0.86f;
                break;
            case RoleVariantTag.Sniper:
            case RoleVariantTag.Battery:
            case RoleVariantTag.Controller:
                xOffset -= forward * 0.14f;
                break;
        }

        return new CombatVector2(xOffset, yOffset);
    }

    private static void RecordFormationTelemetry(BattleState state, BattlefieldLayout layout, TeamSide side, TeamTacticProfile tactic)
    {
        foreach (var unit in state.GetTeam(side))
        {
            var baseAnchor = layout.ResolveAnchorPosition(unit.Side, unit.Anchor);
            var offset = unit.AnchorPosition - baseAnchor;
            state.AddTelemetry(new TelemetryEventRecord
            {
                Domain = TelemetryDomain.Combat,
                EventKind = TelemetryEventKind.PositioningIntentUpdated,
                TimeSeconds = state.ElapsedSeconds,
                Actor = BattleTelemetryRecorder.BuildEntityRef(unit),
                Explain = new ExplainStamp
                {
                    SourceKind = ExplainedSourceKind.SystemRule,
                    SourceContentId = tactic.Id,
                    SourceDisplayName = tactic.DisplayName,
                    ReasonCode = DecisionReasonCode.DefaultCadence,
                    Salience = SalienceClass.Ambient,
                },
                ValueA = offset.X,
                ValueB = offset.Y,
                IntValueA = unit.Anchor.LaneIndex(),
                StringValueA = $"formation:{tactic.Id}",
                StringValueB = $"{unit.Anchor}:{unit.Definition.RoleTag}:{unit.Definition.RoleVariant}",
            });
        }
    }

    private static float ResolvePositive(float value, float fallback)
    {
        return value > 0f ? value : fallback;
    }

    private static float Clamp01(float value)
    {
        return MathF.Min(1f, MathF.Max(0f, value));
    }

    private static float Lerp(float from, float to, float t)
    {
        return from + ((to - from) * Clamp01(t));
    }

    private static float ResolveDeterministicSigned(string id, string salt)
    {
        unchecked
        {
            var hash = 17;
            foreach (var ch in id)
            {
                hash = (hash * 31) + ch;
            }

            foreach (var ch in salt)
            {
                hash = (hash * 31) + ch;
            }

            var normalized = MathF.Abs(hash % 10000) / 9999f;
            return (normalized * 2f) - 1f;
        }
    }

    private readonly record struct FormationPosition(CombatVector2 AnchorPosition, CombatVector2 SpawnPosition);
}
