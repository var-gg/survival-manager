using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Meta.Model;
using SM.Persistence.Abstractions.Models;

namespace SM.Unity.Sandbox;

public static class CombatSandboxCompilationContextFactory
{
    private static readonly DeploymentAnchorId[] DefaultAnchorOrder =
    {
        DeploymentAnchorId.FrontTop,
        DeploymentAnchorId.FrontCenter,
        DeploymentAnchorId.FrontBottom,
        DeploymentAnchorId.BackTop,
        DeploymentAnchorId.BackCenter,
        DeploymentAnchorId.BackBottom
    };

    public static CombatSandboxCompilationContext Create(
        SaveProfile profile,
        IReadOnlyDictionary<DeploymentAnchorId, string?> currentDeploymentAssignments,
        IReadOnlyList<string> currentSquadHeroIds,
        TeamPostureType currentTeamPosture,
        string currentTeamTacticId,
        IReadOnlyList<string> currentTemporaryAugmentIds,
        int currentNodeIndex)
    {
        return new CombatSandboxCompilationContext(
            profile,
            currentDeploymentAssignments,
            currentSquadHeroIds,
            currentTeamPosture,
            currentTeamTacticId ?? string.Empty,
            currentTemporaryAugmentIds,
            currentNodeIndex);
    }

    public static CombatSandboxCompilationContext CreatePreviewContext(SaveProfile? profile)
    {
        var effectiveProfile = profile ?? new SaveProfile();
        var blueprint = ResolvePreferredBlueprint(effectiveProfile);
        var deploymentAssignments = ResolveDeploymentAssignments(effectiveProfile, blueprint);
        var squadHeroIds = ResolveSquadHeroIds(effectiveProfile, blueprint, deploymentAssignments);
        var teamPosture = ResolveTeamPosture(blueprint);
        var teamTacticId = blueprint?.TeamTacticId ?? string.Empty;
        var temporaryAugmentIds = effectiveProfile.ActiveRun?.TemporaryAugmentIds?
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList() ?? new List<string>();
        var currentNodeIndex = Math.Max(0, effectiveProfile.ActiveRun?.CurrentNodeIndex ?? 0);

        return new CombatSandboxCompilationContext(
            effectiveProfile,
            deploymentAssignments,
            squadHeroIds,
            teamPosture,
            teamTacticId,
            temporaryAugmentIds,
            currentNodeIndex);
    }

    private static SquadBlueprintRecord? ResolvePreferredBlueprint(SaveProfile profile)
    {
        if (!string.IsNullOrWhiteSpace(profile.ActiveBlueprintId))
        {
            var active = profile.SquadBlueprints.FirstOrDefault(candidate =>
                string.Equals(candidate.BlueprintId, profile.ActiveBlueprintId, StringComparison.Ordinal));
            if (active != null)
            {
                return active;
            }
        }

        if (!string.IsNullOrWhiteSpace(profile.ActiveRun?.BlueprintId))
        {
            var fromRun = profile.SquadBlueprints.FirstOrDefault(candidate =>
                string.Equals(candidate.BlueprintId, profile.ActiveRun.BlueprintId, StringComparison.Ordinal));
            if (fromRun != null)
            {
                return fromRun;
            }
        }

        return profile.SquadBlueprints.FirstOrDefault();
    }

    private static IReadOnlyDictionary<DeploymentAnchorId, string?> ResolveDeploymentAssignments(
        SaveProfile profile,
        SquadBlueprintRecord? blueprint)
    {
        var assignments = new Dictionary<DeploymentAnchorId, string?>(EqualityComparer<DeploymentAnchorId>.Default);
        if (blueprint?.DeploymentAssignments != null)
        {
            foreach (var pair in blueprint.DeploymentAssignments)
            {
                if (string.IsNullOrWhiteSpace(pair.Value) || !Enum.TryParse(pair.Key, out DeploymentAnchorId anchor))
                {
                    continue;
                }

                assignments[anchor] = pair.Value;
            }
        }

        if (assignments.Count > 0)
        {
            return assignments;
        }

        var fallbackHeroIds = profile.ActiveRun?.BattleDeployHeroIds
            ?.Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (fallbackHeroIds == null || fallbackHeroIds.Count == 0)
        {
            fallbackHeroIds = profile.Heroes
                .Where(hero => !string.IsNullOrWhiteSpace(hero.HeroId))
                .Select(hero => hero.HeroId)
                .Distinct(StringComparer.Ordinal)
                .Take(DefaultAnchorOrder.Length)
                .ToList();
        }

        for (var index = 0; index < fallbackHeroIds.Count && index < DefaultAnchorOrder.Length; index++)
        {
            assignments[DefaultAnchorOrder[index]] = fallbackHeroIds[index];
        }

        return assignments;
    }

    private static IReadOnlyList<string> ResolveSquadHeroIds(
        SaveProfile profile,
        SquadBlueprintRecord? blueprint,
        IReadOnlyDictionary<DeploymentAnchorId, string?> deploymentAssignments)
    {
        var squadHeroIds = blueprint?.ExpeditionSquadHeroIds?
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (squadHeroIds != null && squadHeroIds.Count > 0)
        {
            return squadHeroIds;
        }

        var activeRunHeroes = profile.ActiveRun?.BattleDeployHeroIds?
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (activeRunHeroes != null && activeRunHeroes.Count > 0)
        {
            return activeRunHeroes;
        }

        return deploymentAssignments.Values
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static TeamPostureType ResolveTeamPosture(SquadBlueprintRecord? blueprint)
    {
        return blueprint != null && Enum.TryParse(blueprint.TeamPosture, out TeamPostureType posture)
            ? posture
            : TeamPostureType.StandardAdvance;
    }
}
