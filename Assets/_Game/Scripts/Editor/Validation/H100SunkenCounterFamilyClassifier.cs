using System;
using System.Collections.Generic;
using System.Linq;
using SM.HeadlessCensus;
using SM.Meta.Model;

namespace SM.Editor.Validation;

/// <summary>player-readable class/archetype/recruit tags만으로 oracle build의 counter family를 붙인다.</summary>
internal static class H100SunkenCounterFamilyClassifier
{
    public static string Classify(
        IReadOnlyList<BuildArchetype> members,
        CombatContentSnapshot snapshot)
    {
        var classes = members.Select(value => value.ClassId).ToArray();
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var member in members)
        {
            tags.Add(member.ArchetypeId);
            tags.Add(member.ClassId);
            tags.Add(member.RaceId);
            if (!snapshot.Archetypes.TryGetValue(member.ArchetypeId, out var archetype))
            {
                continue;
            }

            foreach (var tag in archetype.RecruitPlanTags ?? Array.Empty<string>())
            {
                tags.Add(tag);
            }

            foreach (var skill in archetype.Skills ?? Array.Empty<SM.Combat.Model.BattleSkillSpec>())
            {
                tags.Add(skill.Id);
            }
        }

        if (ContainsAny(tags, "anti-heal", "anti_heal", "wound", "silence"))
        {
            return "anti-heal";
        }

        if (classes.Count(value => value == "ranger") >= 2)
        {
            return "ranged";
        }

        if (classes.Any(value => value is "vanguard" or "duelist")
            && classes.Contains("ranger", StringComparer.Ordinal)
            && classes.Contains("mystic", StringComparer.Ordinal))
        {
            return "mixed";
        }

        if (classes.Contains("duelist", StringComparer.Ordinal)
            && ContainsAny(tags, "dive", "mark", "exposed"))
        {
            return "dive";
        }

        if (classes.Count(value => value == "duelist") >= 2
            || ContainsAny(tags, "burst", "execute"))
        {
            return "burst";
        }

        if (classes.Contains("mystic", StringComparer.Ordinal)
            && (classes.Contains("vanguard", StringComparer.Ordinal)
                || ContainsAny(tags, "guard", "shield", "support", "heal")))
        {
            return "sustain";
        }

        return "frontline";
    }

    private static bool ContainsAny(IEnumerable<string> values, params string[] tokens)
        => values.Any(value => tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase)));
}
