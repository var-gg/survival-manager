using System;
using System.Collections.Generic;
using System.Linq;
using SM.HeadlessCensus;
using SM.HeadlessMetrics;
using SM.HeadlessPolicies;
using SM.Meta.Model;
using SM.Persistence.Abstractions.Models;

namespace SM.Editor.Validation;

/// <summary>Stage 3 census build와 medoid를 현재 보유 hero id에 결합한다.</summary>
internal static class H100SunkenOracleCaseFactory
{
    public static IReadOnlyList<H100SunkenOracleCase> Build(
        BuildSpaceCensus census,
        CombatContentSnapshot combatSnapshot,
        SaveProfile profile,
        int medoidCount,
        int buildLimit,
        string scope,
        string stateVariantId,
        string addedRosterArchetypeId = "",
        int rewardOptionIndex = -1,
        string rewardPayloadId = "",
        HeadlessDeploymentDecision? policyChoice = null)
    {
        var heroByArchetype = profile.Heroes
            .Where(value => !string.IsNullOrWhiteSpace(value.HeroId)
                            && !string.IsNullOrWhiteSpace(value.ArchetypeId))
            .GroupBy(value => value.ArchetypeId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(value => value.HeroId, StringComparer.Ordinal).First(),
                StringComparer.Ordinal);
        var available = census.Combinations
            .Where(build => build.Members.All(member => heroByArchetype.ContainsKey(member.ArchetypeId)))
            .Where(build => string.IsNullOrWhiteSpace(addedRosterArchetypeId)
                            || build.Members.Any(member => member.ArchetypeId == addedRosterArchetypeId))
            .ToArray();
        var selectedBuilds = SelectBuilds(available, combatSnapshot, buildLimit);
        var medoids = census.Medoids
            .OrderBy(value => value.Placement.Signature, StringComparer.Ordinal)
            .Take(medoidCount)
            .ToArray();
        var cases = new List<H100SunkenOracleCase>(selectedBuilds.Count * medoids.Length + 1);
        foreach (var build in selectedBuilds)
        {
            var formationMembers = build.FormationMembers;
            var family = H100SunkenCounterFamilyClassifier.Classify(build.Members, combatSnapshot);
            foreach (var medoid in medoids)
            {
                var members = formationMembers.Select((member, index) => new H100SunkenOracleMember(
                    heroByArchetype[member.ArchetypeId].HeroId,
                    member.ArchetypeId,
                    medoid.Placement.AnchorsByMemberIndex[index])).ToArray();
                cases.Add(new H100SunkenOracleCase(
                    $"oracle|{build.BuildId}|{medoid.Placement.Signature}",
                    build.BuildId,
                    medoid.Placement.Signature,
                    family,
                    members,
                    scope,
                    stateVariantId,
                    IsPolicyChoice: false,
                    addedRosterArchetypeId,
                    rewardOptionIndex,
                    rewardPayloadId));
            }
        }

        if (policyChoice != null)
        {
            cases.Insert(0, BuildPolicyChoice(profile, combatSnapshot, policyChoice, scope, stateVariantId));
        }

        return cases;
    }

    private static H100SunkenOracleCase BuildPolicyChoice(
        SaveProfile profile,
        CombatContentSnapshot combatSnapshot,
        HeadlessDeploymentDecision decision,
        string scope,
        string stateVariantId)
    {
        var heroById = profile.Heroes.ToDictionary(value => value.HeroId, StringComparer.Ordinal);
        var members = decision.Placements
            .OrderBy(value => value.Anchor)
            .Select(value => new H100SunkenOracleMember(
                value.HeroId,
                heroById[value.HeroId].ArchetypeId,
                value.Anchor))
            .ToArray();
        var archetypes = H100BuildSpaceContentAdapter.BuildCanonicalRosterFromSnapshot(combatSnapshot)
            .Where(value => members.Any(member => member.ArchetypeId == value.ArchetypeId))
            .ToArray();
        var buildId = string.Join("+", members.Select(value => value.ArchetypeId).OrderBy(value => value, StringComparer.Ordinal));
        var placementId = string.Join("|", members.Select(value => $"{(int)value.Anchor}:{value.HeroId}"));
        return new H100SunkenOracleCase(
            $"policy|{buildId}|{placementId}",
            buildId,
            placementId,
            H100SunkenCounterFamilyClassifier.Classify(archetypes, combatSnapshot),
            members,
            scope,
            stateVariantId,
            IsPolicyChoice: true,
            AddedRosterArchetypeId: string.Empty,
            RewardOptionIndex: -1,
            RewardPayloadId: string.Empty);
    }

    private static IReadOnlyList<BuildCombination> SelectBuilds(
        IReadOnlyList<BuildCombination> builds,
        CombatContentSnapshot snapshot,
        int buildLimit)
    {
        if (buildLimit <= 0 || builds.Count <= buildLimit)
        {
            return builds.OrderBy(value => value.BuildId, StringComparer.Ordinal).ToArray();
        }

        var queues = builds
            .GroupBy(value => H100SunkenCounterFamilyClassifier.Classify(value.Members, snapshot), StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new Queue<BuildCombination>(group
                .OrderByDescending(value => value.Roles.IsRoleComplete)
                .ThenByDescending(value => value.Synergy.ClassTier3Count + value.Synergy.RaceTier4Count)
                .ThenBy(value => value.BuildId, StringComparer.Ordinal)))
            .ToArray();
        var selected = new List<BuildCombination>(buildLimit);
        while (selected.Count < buildLimit && queues.Any(queue => queue.Count > 0))
        {
            foreach (var queue in queues)
            {
                if (queue.Count > 0 && selected.Count < buildLimit)
                {
                    selected.Add(queue.Dequeue());
                }
            }
        }

        return selected.OrderBy(value => value.BuildId, StringComparer.Ordinal).ToArray();
    }
}
