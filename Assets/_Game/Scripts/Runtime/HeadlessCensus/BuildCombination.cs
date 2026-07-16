using System;
using System.Collections.Generic;
using System.Linq;

namespace SM.HeadlessCensus;

public sealed record BuildCombination(
    int BuildIndex,
    string BuildId,
    IReadOnlyList<BuildArchetype> Members,
    SynergySignature Synergy,
    RoleDistribution Roles,
    int DistinctRaceCount,
    int DistinctClassCount)
{
    public string ArchetypeSignature => string.Join("+", Members.Select(member => member.ArchetypeId));

    /// <summary>
    /// generic formation role slot(Tank, Damage, Ranged, Healer)에 build member를 결정적으로 대응한다.
    /// 같은 역할이 중복되면 canonical roster 순서를 보존한다.
    /// </summary>
    public IReadOnlyList<BuildArchetype> FormationMembers => Members.Select((member, index) => new { member, index })
        .OrderBy(value => value.member.Role)
        .ThenBy(value => value.index)
        .Select(value => value.member)
        .ToArray();

    public string RaceSignature => CountSignature(Members.Select(member => member.RaceId));

    public string ClassSignature => CountSignature(Members.Select(member => member.ClassId));

    public bool HasExactRaceThree => SortedCounts(Members.Select(member => member.RaceId)).SequenceEqual(new[] { 3, 1 });

    public bool IsRaceTwoPlusTwo => SortedCounts(Members.Select(member => member.RaceId)).SequenceEqual(new[] { 2, 2 });

    public bool IsClassTwoPlusTwo => SortedCounts(Members.Select(member => member.ClassId)).SequenceEqual(new[] { 2, 2 });

    private static string CountSignature(IEnumerable<string> ids)
    {
        return string.Join(
            "+",
            ids.GroupBy(id => id, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => $"{group.Key}:{group.Count()}"));
    }

    private static int[] SortedCounts(IEnumerable<string> ids)
    {
        return ids.GroupBy(id => id, StringComparer.Ordinal)
            .Select(group => group.Count())
            .OrderByDescending(count => count)
            .ToArray();
    }
}
