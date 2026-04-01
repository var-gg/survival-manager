using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;
using SM.Core.Stats;
using SM.Meta.Model;

namespace SM.Meta.Services;

public static class TeamPlanEvaluator
{
    public static TeamPlanProfile Evaluate(
        IReadOnlyList<HeroRecord> roster,
        IReadOnlyDictionary<string, CombatArchetypeTemplate> archetypes,
        CombatContentSnapshot content,
        IReadOnlyList<string> temporaryAugmentIds,
        IReadOnlyList<string> permanentAugmentIds)
    {
        var profile = new TeamPlanProfile();
        if (roster.Count == 0)
        {
            profile.NeedsFrontline = true;
            profile.NeedsSupport = true;
            return profile;
        }

        var tagCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var frontlineCount = 0;
        var backlineCount = 0;
        var supportCount = 0;
        var physicalCount = 0;
        var magicalCount = 0;

        foreach (var hero in roster)
        {
            if (!archetypes.TryGetValue(hero.ArchetypeId, out var template))
            {
                continue;
            }

            AddCount(tagCounts, hero.RaceId);
            AddCount(tagCounts, hero.ClassId);
            foreach (var tag in template.RecruitPlanTags ?? Array.Empty<string>())
            {
                AddCount(tagCounts, tag);
            }

            switch (template.Behavior?.FormationLine)
            {
                case FormationLine.Frontline:
                    frontlineCount++;
                    break;
                case FormationLine.Backline:
                    backlineCount++;
                    break;
            }

            if (string.Equals(template.RoleTag, "support", StringComparison.Ordinal))
            {
                supportCount++;
            }

            if (template.BaseStats.TryGetValue(StatKey.PhysPower, out var phys) && phys > 0f)
            {
                physicalCount++;
            }

            if (template.BaseStats.TryGetValue(StatKey.MagPower, out var mag) && mag > 0f)
            {
                magicalCount++;
            }
        }

        profile.TopSynergyTagIds = tagCounts
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .Take(2)
            .Select(pair => pair.Key)
            .ToList();

        foreach (var synergy in content.SynergyCatalog.Values)
        {
            var currentCount = tagCounts.TryGetValue(synergy.Rule.CountedTagId, out var count) ? count : 0;
            var gap = Math.Max(0, synergy.Rule.Threshold - currentCount);
            if (!profile.BreakpointGapsByTag.TryGetValue(synergy.Rule.CountedTagId, out var existingGap) || gap < existingGap)
            {
                profile.BreakpointGapsByTag[synergy.Rule.CountedTagId] = gap;
            }
        }

        profile.NeedsFrontline = frontlineCount < 2;
        profile.NeedsBackline = backlineCount < 2;
        profile.NeedsSupport = supportCount < 1;
        profile.PrefersPhysical = physicalCount > magicalCount + 1;
        profile.PrefersMagical = magicalCount > physicalCount + 1;

        foreach (var augmentId in temporaryAugmentIds.Concat(permanentAugmentIds).Distinct(StringComparer.Ordinal))
        {
            if (!content.AugmentCatalog.TryGetValue(augmentId, out var augment))
            {
                continue;
            }

            foreach (var tag in augment.Tags)
            {
                if (!string.IsNullOrWhiteSpace(tag) && !profile.AugmentHookTags.Contains(tag))
                {
                    profile.AugmentHookTags.Add(tag);
                }
            }
        }

        profile.CounterCoverage = CounterCoverageAggregationService.AggregateSummaries(
            roster
                .Select(hero => archetypes.TryGetValue(hero.ArchetypeId, out var template) ? template : null)
                .Where(template => template != null)
                .SelectMany(template => EnumerateGovernance(template!)));

        return profile;
    }

    private static void AddCount(Dictionary<string, int> counts, string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return;
        }

        counts[tag] = counts.TryGetValue(tag, out var count) ? count + 1 : 1;
    }

    private static IEnumerable<ContentGovernanceSummary?> EnumerateGovernance(CombatArchetypeTemplate template)
    {
        yield return template.Governance;

        foreach (var skill in template.Skills ?? Array.Empty<BattleSkillSpec>())
        {
            yield return skill.Governance;
        }

        yield return template.SignaturePassive?.Governance;
        yield return template.FlexPassive?.Governance;
        yield return template.MobilityReaction?.Governance;
    }
}
