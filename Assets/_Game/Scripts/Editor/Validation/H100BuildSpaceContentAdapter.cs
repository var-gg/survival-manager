using System;
using System.Collections.Generic;
using System.Linq;
using SM.HeadlessCensus;
using SM.Unity;

namespace SM.Editor.Validation;

/// <summary>RuntimeCombatContentLookup canonical order를 authored-object-free census DTO로 투영한다.</summary>
internal static class H100BuildSpaceContentAdapter
{
    public static IReadOnlyList<BuildArchetype> BuildCanonicalRoster(RuntimeCombatContentLookup lookup)
    {
        if (!lookup.TryGetCombatSnapshot(out var snapshot, out var error))
        {
            throw new InvalidOperationException($"Cannot build census roster: {error}");
        }

        var canonicalIds = lookup.GetCanonicalArchetypeIds().ToArray();
        if (canonicalIds.Length != BuildSpaceEnumerator.ArchetypeCount)
        {
            throw new InvalidOperationException(
                $"Canonical census roster must contain {BuildSpaceEnumerator.ArchetypeCount} archetypes (actual={canonicalIds.Length}).");
        }

        return canonicalIds.Select(id =>
        {
            if (!snapshot.Archetypes.TryGetValue(id, out var archetype))
            {
                throw new InvalidOperationException($"Canonical census archetype is absent from snapshot: {id}");
            }

            return new BuildArchetype(
                archetype.Id,
                archetype.RaceId,
                archetype.ClassId,
                ResolveRole(archetype.ClassId),
                archetype.DefaultAnchor);
        }).ToArray();
    }

    private static BuildRole ResolveRole(string classId)
        => classId switch
        {
            "vanguard" => BuildRole.Tank,
            "duelist" => BuildRole.Damage,
            "ranger" => BuildRole.Ranged,
            "mystic" => BuildRole.Healer,
            _ => throw new InvalidOperationException($"Unknown canonical census class: {classId}"),
        };
}
