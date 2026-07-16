using System;
using System.Collections.Generic;
using System.Linq;

namespace SM.HeadlessPolicies;

/// <summary>catalog 없이 현재 player-visible roster, synergy, skill card에서 자체 의도를 형성한다.</summary>
internal static class ConceptIntentDiscovery
{
    public static HeadlessConceptIntent Form(HeadlessPolicyObservation observation)
    {
        var synergyIntent = TryFormReachableSynergyIntent(observation);
        if (synergyIntent != null)
        {
            return synergyIntent;
        }

        var statusIntent = TryFormStatusIntent(observation);
        return statusIntent ?? FormRosterTagFallback(observation);
    }

    private static HeadlessConceptIntent TryFormReachableSynergyIntent(HeadlessPolicyObservation observation)
    {
        var currentCounts = observation.SynergyCounts
            .GroupBy(value => value.CountedTagId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Max(value => value.CurrentCount), StringComparer.Ordinal);
        var candidates = observation.SynergyCatalog
            .SelectMany(synergy => synergy.Tiers.Select(tier => new
            {
                Synergy = synergy,
                Tier = tier,
                Current = currentCounts.TryGetValue(synergy.CountedTagId, out var count) ? count : 0,
                Available = observation.Roster.Count(hero => HasTag(hero, synergy.CountedTagId)),
            }))
            .Where(value => value.Tier.Threshold > value.Current && value.Available >= value.Tier.Threshold)
            .OrderBy(value => value.Tier.Threshold - value.Current)
            .ThenBy(value => value.Tier.Threshold)
            .ThenBy(value => value.Synergy.CountedTagId, StringComparer.Ordinal)
            .ThenBy(value => value.Synergy.SynergyId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (candidates == null)
        {
            return null;
        }

        var tag = candidates.Synergy.CountedTagId;
        var threshold = candidates.Tier.Threshold;
        var milestones = Enumerable.Range(candidates.Current + 1, threshold - candidates.Current)
            .Select(count => $"build.count_tag({tag})={count}/{threshold}")
            .ToList();
        if (!string.IsNullOrWhiteSpace(candidates.Tier.GrantedTeamRuleId))
        {
            milestones.Add($"build.team_rule={candidates.Tier.GrantedTeamRuleId}");
        }

        var payoff = !string.IsNullOrWhiteSpace(candidates.Tier.GrantedTeamRuleId)
            ? $"team_rule:{candidates.Tier.GrantedTeamRuleId}"
            : $"synergy_tier:{candidates.Synergy.SynergyId}@{threshold}";
        return new HeadlessConceptIntent(
            $"discovery-synergy-{tag}-{threshold}",
            "discovery",
            new[] { $"build.count_tag({tag})>={threshold}" },
            milestones,
            payoff,
            observation.Roster.Where(hero => HasTag(hero, tag))
                .Select(hero => $"archetype:{hero.ArchetypeId}").ToArray(),
            new[] { "formation:any_legal" },
            CounterAffordances(observation),
            threshold >= 3 ? "aspirational" : "core",
            new[]
            {
                $"visible_roster_count({tag})<{threshold}",
                "visible_track_has_no_progress_offer:2",
            });
    }

    private static HeadlessConceptIntent TryFormStatusIntent(HeadlessPolicyObservation observation)
    {
        var statuses = observation.Roster
            .SelectMany(hero => hero.SkillCards.SelectMany(skill => skill.AppliedStatuses)
                .Select(status => new { Hero = hero, Status = status }))
            .Where(value => !string.IsNullOrWhiteSpace(value.Status.StatusId))
            .GroupBy(value => value.Status.StatusId, StringComparer.Ordinal)
            .Select(group => new
            {
                StatusId = group.Key,
                Count = group.Select(value => value.Hero.HeroId).Distinct(StringComparer.Ordinal).Count(),
                SkillIds = group.SelectMany(value => value.Hero.SkillCards)
                    .Where(skill => skill.AppliedStatuses.Any(status =>
                        string.Equals(status.StatusId, group.Key, StringComparison.Ordinal)))
                    .Select(skill => skill.SkillId)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray(),
            })
            .OrderByDescending(value => value.Count)
            .ThenBy(value => value.StatusId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (statuses == null)
        {
            return null;
        }

        return new HeadlessConceptIntent(
            $"discovery-status-{statuses.StatusId}",
            "discovery",
            new[] { $"build.contains_status:{statuses.StatusId}" },
            new[] { $"deploy.status:{statuses.StatusId}", $"activate:status:{statuses.StatusId}" },
            $"status:{statuses.StatusId}",
            statuses.SkillIds.Select(value => $"skill:{value}").ToArray(),
            new[] { "formation:any_legal" },
            CounterAffordances(observation),
            "core",
            new[]
            {
                $"visible_status_source_unavailable:{statuses.StatusId}",
                "visible_track_has_no_progress_offer:2",
            });
    }

    private static HeadlessConceptIntent FormRosterTagFallback(HeadlessPolicyObservation observation)
    {
        var tag = observation.Roster
            .SelectMany(hero => new[] { hero.RaceId, hero.ClassId })
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(value => value, StringComparer.Ordinal)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .First().Key;
        var reachable = observation.Roster.Count(hero => HasTag(hero, tag));
        var threshold = Math.Min(observation.DeployCapacity, Math.Max(2, reachable));
        return new HeadlessConceptIntent(
            $"discovery-roster-{tag}-{threshold}",
            "discovery",
            new[] { $"build.count_tag({tag})>={threshold}" },
            Enumerable.Range(1, threshold)
                .Select(count => $"build.count_tag({tag})={count}/{threshold}").ToArray(),
            $"visible_identity:{tag}",
            observation.Roster.Where(hero => HasTag(hero, tag))
                .Select(hero => $"archetype:{hero.ArchetypeId}").ToArray(),
            new[] { "formation:any_legal" },
            CounterAffordances(observation),
            "core",
            new[] { $"visible_roster_count({tag})<{threshold}" });
    }

    private static IReadOnlyList<string> CounterAffordances(HeadlessPolicyObservation observation)
    {
        if (!observation.EnemyPreview.IsAvailable)
        {
            return new[] { "enemy_preview_unavailable -> keep:identity" };
        }

        var threat = observation.EnemyPreview.Units
            .Select(unit => unit.RoleTag)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .OrderBy(value => value, StringComparer.Ordinal)
            .FirstOrDefault() ?? observation.EnemyPreview.DifficultyBand;
        return new[] { $"enemy_threat:{threat} -> flex:preserve_visible_identity" };
    }

    private static bool HasTag(HeadlessHeroObservation hero, string tag)
        => string.Equals(hero.RaceId, tag, StringComparison.Ordinal)
           || string.Equals(hero.ClassId, tag, StringComparison.Ordinal)
           || string.Equals(hero.ArchetypeId, tag, StringComparison.Ordinal)
           || string.Equals(hero.RoleTag, tag, StringComparison.Ordinal);
}
