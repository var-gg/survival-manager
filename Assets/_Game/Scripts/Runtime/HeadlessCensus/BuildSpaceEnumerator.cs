using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Stats;

namespace SM.HeadlessCensus;

/// <summary>12 archetype의 C(12,4) build와 P(6,4) labelled placement를 결정적으로 열거한다.</summary>
public static class BuildSpaceEnumerator
{
    public const int ArchetypeCount = 12;
    public const int SquadSize = 4;
    public const int AnchorCount = 6;
    public const int DefaultMedoidCount = 8;

    public static BuildSpaceCensus Generate(
        IEnumerable<BuildArchetype> archetypes,
        int medoidCount = DefaultMedoidCount)
    {
        var roster = ValidateAndMaterialize(archetypes);
        var combinations = EnumerateCombinations(roster).ToArray();
        var formations = EnumerateFormations().ToArray();
        var medoids = FormationMedoidSelector.Select(formations, medoidCount);
        var summary = BuildSpaceCensusAnalyzer.Analyze(combinations, formations, medoids);
        return new BuildSpaceCensus(combinations, formations, medoids, summary);
    }

    private static BuildArchetype[] ValidateAndMaterialize(IEnumerable<BuildArchetype> archetypes)
    {
        if (archetypes == null)
        {
            throw new ArgumentNullException(nameof(archetypes));
        }

        var roster = archetypes.ToArray();
        if (roster.Length != ArchetypeCount)
        {
            throw new ArgumentException($"Canonical census requires {ArchetypeCount} archetypes (actual={roster.Length}).", nameof(archetypes));
        }

        if (roster.Any(entry => entry == null
                                || string.IsNullOrWhiteSpace(entry.ArchetypeId)
                                || string.IsNullOrWhiteSpace(entry.RaceId)
                                || string.IsNullOrWhiteSpace(entry.ClassId)))
        {
            throw new ArgumentException("Census archetype identity fields must be non-empty.", nameof(archetypes));
        }

        var duplicateIds = roster.GroupBy(entry => entry.ArchetypeId, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        if (duplicateIds.Length > 0)
        {
            throw new ArgumentException($"Duplicate census archetypes: {string.Join(", ", duplicateIds)}", nameof(archetypes));
        }

        return roster;
    }

    private static IEnumerable<BuildCombination> EnumerateCombinations(IReadOnlyList<BuildArchetype> roster)
    {
        var buffer = new BuildArchetype[SquadSize];
        var buildIndex = 0;
        foreach (var members in Enumerate(sourceIndex: 0, targetIndex: 0))
        {
            var synergy = BuildSynergySignature(members);
            var roles = new RoleDistribution(
                members.Count(member => member.Role == BuildRole.Tank),
                members.Count(member => member.Role == BuildRole.Damage),
                members.Count(member => member.Role == BuildRole.Ranged),
                members.Count(member => member.Role == BuildRole.Healer));
            yield return new BuildCombination(
                buildIndex++,
                string.Join("+", members.Select(member => member.ArchetypeId)),
                members,
                synergy,
                roles,
                members.Select(member => member.RaceId).Distinct(StringComparer.Ordinal).Count(),
                members.Select(member => member.ClassId).Distinct(StringComparer.Ordinal).Count());
        }

        IEnumerable<IReadOnlyList<BuildArchetype>> Enumerate(int sourceIndex, int targetIndex)
        {
            if (targetIndex == SquadSize)
            {
                yield return buffer.ToArray();
                yield break;
            }

            var remainingNeeded = SquadSize - targetIndex;
            for (var index = sourceIndex; index <= roster.Count - remainingNeeded; index++)
            {
                buffer[targetIndex] = roster[index];
                foreach (var result in Enumerate(index + 1, targetIndex + 1))
                {
                    yield return result;
                }
            }
        }
    }

    private static SynergySignature BuildSynergySignature(IReadOnlyList<BuildArchetype> members)
    {
        var loadouts = members.Select(member => new BattleUnitLoadout(
            member.ArchetypeId,
            member.ArchetypeId,
            member.RaceId,
            member.ClassId,
            member.PreferredAnchor,
            new Dictionary<StatKey, float>(),
            Array.Empty<UnitRuleChain>(),
            Array.Empty<BattleSkillSpec>(),
            CompileTags: new[] { member.RaceId, member.ClassId },
            ArchetypeId: member.ArchetypeId)).ToArray();
        var packages = SynergyService.BuildForTeam(loadouts);
        var tierIds = new List<string>();
        var raceTier2Count = 0;
        var raceTier4Count = 0;
        foreach (var group in members.GroupBy(member => member.RaceId, StringComparer.Ordinal)
                     .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var count = group.Count();
            if (count >= 2)
            {
                tierIds.Add($"race:{group.Key}@2");
                raceTier2Count++;
            }

            if (count >= 4)
            {
                tierIds.Add($"race:{group.Key}@4");
                raceTier4Count++;
            }
        }

        var classTier2Count = 0;
        var classTier3Count = 0;
        foreach (var group in members.GroupBy(member => member.ClassId, StringComparer.Ordinal)
                     .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var count = group.Count();
            if (count >= 2)
            {
                tierIds.Add($"class:{group.Key}@2");
                classTier2Count++;
            }

            if (count >= 3)
            {
                tierIds.Add($"class:{group.Key}@3");
                classTier3Count++;
            }
        }

        var doctrineRuleIds = packages.Select(package => package.GrantedTeamRuleId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        return new SynergySignature(
            tierIds.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
            doctrineRuleIds,
            raceTier2Count,
            raceTier4Count,
            classTier2Count,
            classTier3Count);
    }

    private static IEnumerable<FormationPlacement> EnumerateFormations()
    {
        var anchors = Enum.GetValues(typeof(DeploymentAnchorId)).Cast<DeploymentAnchorId>()
            .OrderBy(anchor => anchor)
            .ToArray();
        if (anchors.Length != AnchorCount)
        {
            throw new InvalidOperationException($"Expected {AnchorCount} deployment anchors (actual={anchors.Length}).");
        }

        var used = new bool[anchors.Length];
        var buffer = new DeploymentAnchorId[SquadSize];
        var placementIndex = 0;
        foreach (var selected in Enumerate(memberIndex: 0))
        {
            var signature = string.Join("|", selected.Select((anchor, index) =>
                $"{((BuildRole)index).ToString().ToLowerInvariant()}:{(int)anchor}"));
            yield return new FormationPlacement(
                placementIndex++,
                signature,
                selected,
                FormationFeatureClassifier.Classify(selected));
        }

        IEnumerable<IReadOnlyList<DeploymentAnchorId>> Enumerate(int memberIndex)
        {
            if (memberIndex == SquadSize)
            {
                yield return buffer.ToArray();
                yield break;
            }

            for (var anchorIndex = 0; anchorIndex < anchors.Length; anchorIndex++)
            {
                if (used[anchorIndex])
                {
                    continue;
                }

                used[anchorIndex] = true;
                buffer[memberIndex] = anchors[anchorIndex];
                foreach (var result in Enumerate(memberIndex + 1))
                {
                    yield return result;
                }

                used[anchorIndex] = false;
            }
        }
    }
}
