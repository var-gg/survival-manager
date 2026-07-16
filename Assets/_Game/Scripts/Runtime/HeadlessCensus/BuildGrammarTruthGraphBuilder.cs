using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;
using SM.Core.Stats;

namespace SM.HeadlessCensus;

/// <summary>pure content projection에서 actionable build 문법 관계를 결정적으로 파생한다.</summary>
public static class BuildGrammarTruthGraphBuilder
{
    public static BuildGrammarTruthGraph Build(IEnumerable<BuildGrammarTruthSource> sources)
    {
        if (sources == null)
        {
            throw new ArgumentNullException(nameof(sources));
        }

        var orderedSources = sources
            .Where(source => source != null)
            .OrderBy(source => source.SubjectKind, StringComparer.Ordinal)
            .ThenBy(source => source.SubjectId, StringComparer.Ordinal)
            .ToArray();
        if (orderedSources.Any(source => string.IsNullOrWhiteSpace(source.SubjectKind)
                                         || string.IsNullOrWhiteSpace(source.SubjectId)))
        {
            throw new ArgumentException("Build grammar source identity must be non-empty.", nameof(sources));
        }

        var duplicate = orderedSources.GroupBy(
                source => $"{source.SubjectKind}|{source.SubjectId}",
                StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate != null)
        {
            throw new ArgumentException($"Duplicate build grammar source: {duplicate.Key}", nameof(sources));
        }

        var drafts = new List<EdgeDraft>();
        foreach (var source in orderedSources)
        {
            AddSourceEdges(drafts, source);
        }

        AddSubstitutionEdges(drafts, orderedSources);
        var normalized = drafts
            .Distinct()
            .OrderBy(edge => edge.SubjectKind, StringComparer.Ordinal)
            .ThenBy(edge => edge.SubjectId, StringComparer.Ordinal)
            .ThenBy(edge => edge.Relation, StringComparer.Ordinal)
            .ThenBy(edge => edge.TargetKind, StringComparer.Ordinal)
            .ThenBy(edge => edge.TargetId, StringComparer.Ordinal)
            .ThenBy(edge => edge.TruthValue, StringComparer.Ordinal)
            .ThenBy(edge => edge.ExpectedFeedbackWitness, StringComparer.Ordinal)
            .ToArray();
        return new BuildGrammarTruthGraph(normalized.Select((edge, index) => new BuildGrammarTruthEdge(
            $"edge-{index:D5}",
            edge.SubjectKind,
            edge.SubjectId,
            edge.Relation,
            edge.TargetKind,
            edge.TargetId,
            edge.TruthValue,
            edge.Actionable,
            edge.FeedbackRequired,
            edge.ExpectedFeedbackWitness)));
    }

    private static void AddSourceEdges(ICollection<EdgeDraft> edges, BuildGrammarTruthSource source)
    {
        foreach (var path in StableIds(source.AcquisitionPaths))
        {
            Add(edges, source, BuildGrammarRelation.AcquiredBy, "acquisition", path);
        }

        foreach (var requiredTag in StableIds(source.RequiredTags))
        {
            Add(edges, source, BuildGrammarRelation.Requires, "tag", requiredTag, feedbackRequired: true);
        }

        foreach (var prerequisiteId in StableIds(source.PrerequisiteIds))
        {
            Add(edges, source, BuildGrammarRelation.Requires, "passive_node", prerequisiteId);
        }

        foreach (var excludedTag in StableIds(source.ExcludedTags))
        {
            Add(edges, source, BuildGrammarRelation.Conflicts, "tag", excludedTag);
        }

        foreach (var conflictId in StableIds(source.ConflictIds))
        {
            Add(edges, source, BuildGrammarRelation.Conflicts, "conflict_group", conflictId);
        }

        foreach (var skillId in StableIds(source.GrantedSkillIds))
        {
            Add(
                edges,
                source,
                BuildGrammarRelation.Produces,
                "skill",
                skillId,
                feedbackRequired: true,
                expectedFeedbackWitness: "telemetry.skill_cast_resolved");
        }

        AddModifierEdges(edges, source, source.ModifierPackage?.Modifiers);
        AddRuleModifierEdges(edges, source, source.RulePackage?.Modifiers);
        AddTriggeredEffectEdges(edges, source, source.TriggeredEffects);
        if (source.Skill != null)
        {
            AddSkillEdges(edges, source, source.Skill);
        }

        if (source.SynergyRule != null)
        {
            AddSynergyEdges(edges, source, source.SynergyRule);
        }
    }

    private static void AddSkillEdges(
        ICollection<EdgeDraft> edges,
        BuildGrammarTruthSource source,
        BattleSkillSpec skill)
    {
        foreach (var status in (skill.AppliedStatuses ?? Array.Empty<StatusApplicationSpec>())
                     .Where(status => status != null && !string.IsNullOrWhiteSpace(status.StatusId))
                     .OrderBy(status => status.StatusId, StringComparer.Ordinal)
                     .ThenBy(status => status.Id, StringComparer.Ordinal))
        {
            Add(
                edges,
                source,
                BuildGrammarRelation.Produces,
                "status",
                status.StatusId,
                BuildGrammarTruthValue.Status(status),
                feedbackRequired: true,
                expectedFeedbackWitness: "telemetry.status_applied");
        }

        foreach (var status in (skill.SupportModifier?.AddedStatuses ?? Array.Empty<StatusApplicationSpec>())
                     .Where(status => status != null && !string.IsNullOrWhiteSpace(status.StatusId))
                     .OrderBy(status => status.StatusId, StringComparer.Ordinal)
                     .ThenBy(status => status.Id, StringComparer.Ordinal))
        {
            Add(
                edges,
                source,
                BuildGrammarRelation.Produces,
                "status",
                status.StatusId,
                BuildGrammarTruthValue.Status(status),
                feedbackRequired: true,
                expectedFeedbackWitness: "telemetry.status_applied");
        }

        AddModifierEdges(edges, source, skill.SupportModifier?.OwnerModifiers);
        foreach (var tag in StableIds(skill.RequiredWeaponTags).Concat(StableIds(skill.RequiredClassTags))
                     .Concat(StableIds(skill.SupportAllowedTags))
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(value => value, StringComparer.Ordinal))
        {
            Add(edges, source, BuildGrammarRelation.Requires, "tag", tag, feedbackRequired: true);
        }

        foreach (var tag in StableIds(skill.SupportBlockedTags))
        {
            Add(edges, source, BuildGrammarRelation.Conflicts, "tag", tag);
        }

        if (!string.IsNullOrWhiteSpace(skill.MutuallyExclusiveGroupId))
        {
            Add(edges, source, BuildGrammarRelation.Conflicts, "conflict_group", skill.MutuallyExclusiveGroupId);
        }

        if (!string.IsNullOrWhiteSpace(skill.CleanseProfileId))
        {
            Add(
                edges,
                source,
                BuildGrammarRelation.PaysOff,
                "cleanse_profile",
                skill.CleanseProfileId,
                feedbackRequired: true,
                expectedFeedbackWitness: "telemetry.status_removed");
        }

        var payoff = skill.Kind switch
        {
            SkillKind.Strike => (Id: "damage", Witness: "telemetry.damage_applied"),
            SkillKind.Heal => (Id: "healing", Witness: "telemetry.healing_applied"),
            SkillKind.Shield => (Id: "barrier", Witness: "telemetry.barrier_applied"),
            _ => (Id: string.Empty, Witness: string.Empty),
        };
        if (!string.IsNullOrWhiteSpace(payoff.Id))
        {
            Add(
                edges,
                source,
                BuildGrammarRelation.PaysOff,
                "combat_effect",
                payoff.Id,
                BuildGrammarTruthValue.SkillPayoff(skill),
                feedbackRequired: true,
                expectedFeedbackWitness: payoff.Witness);
        }

        AddTriggeredEffectEdges(edges, source, skill.TriggeredEffects);
    }

    private static void AddModifierEdges(
        ICollection<EdgeDraft> edges,
        BuildGrammarTruthSource source,
        IEnumerable<StatModifier>? modifiers)
    {
        foreach (var modifier in (modifiers ?? Array.Empty<StatModifier>())
                     .Where(modifier => modifier != null)
                     .OrderBy(modifier => modifier.Stat.ToString(), StringComparer.Ordinal)
                     .ThenBy(modifier => modifier.Op)
                     .ThenBy(modifier => modifier.Value)
                     .ThenBy(modifier => modifier.Tag?.Value, StringComparer.Ordinal))
        {
            Add(
                edges,
                source,
                BuildGrammarRelation.Amplifies,
                "stat",
                modifier.Stat.ToString(),
                BuildGrammarTruthValue.Modifier(modifier),
                feedbackRequired: true,
                expectedFeedbackWitness: ResolveModifierWitness(modifier.Stat));
        }
    }

    private static void AddRuleModifierEdges(
        ICollection<EdgeDraft> edges,
        BuildGrammarTruthSource source,
        IEnumerable<RuleModifier>? modifiers)
    {
        foreach (var modifier in (modifiers ?? Array.Empty<RuleModifier>())
                     .Where(modifier => modifier != null)
                     .OrderBy(modifier => modifier.Kind)
                     .ThenBy(modifier => modifier.Value, StringComparer.Ordinal)
                     .ThenBy(modifier => modifier.Magnitude))
        {
            Add(
                edges,
                source,
                BuildGrammarRelation.Amplifies,
                "rule_modifier",
                $"{modifier.Kind}:{modifier.Value}",
                BuildGrammarTruthValue.RuleModifier(modifier),
                feedbackRequired: true);
        }
    }

    private static void AddTriggeredEffectEdges(
        ICollection<EdgeDraft> edges,
        BuildGrammarTruthSource source,
        IEnumerable<CombatTriggeredEffect>? effects)
    {
        foreach (var effect in (effects ?? Array.Empty<CombatTriggeredEffect>())
                     .Where(effect => effect != null)
                     .OrderBy(effect => effect.Trigger)
                     .ThenBy(effect => effect.Op)
                     .ThenBy(effect => effect.Scope)
                     .ThenBy(effect => effect.StatusId, StringComparer.Ordinal)
                     .ThenBy(effect => effect.Magnitude))
        {
            var relation = effect.Op == TriggeredEffectOp.ApplyStatus
                ? BuildGrammarRelation.Produces
                : BuildGrammarRelation.PaysOff;
            var targetKind = effect.Op == TriggeredEffectOp.ApplyStatus ? "status" : "combat_effect";
            var targetId = effect.Op == TriggeredEffectOp.ApplyStatus ? effect.StatusId : effect.Op.ToString();
            if (string.IsNullOrWhiteSpace(targetId))
            {
                continue;
            }

            Add(
                edges,
                source,
                relation,
                targetKind,
                targetId,
                BuildGrammarTruthValue.Trigger(effect),
                feedbackRequired: true,
                expectedFeedbackWitness: ResolveTriggerWitness(effect.Trigger));
        }
    }

    private static void AddSynergyEdges(
        ICollection<EdgeDraft> edges,
        BuildGrammarTruthSource source,
        TeamSynergyTierRule rule)
    {
        if (string.IsNullOrWhiteSpace(rule.CountedTagId) || rule.Threshold <= 0)
        {
            return;
        }

        Add(
            edges,
            source,
            BuildGrammarRelation.Requires,
            "tag",
            rule.CountedTagId,
            BuildGrammarTruthValue.Threshold(rule.Threshold),
            feedbackRequired: true,
            expectedFeedbackWitness: "beat.synergy_activated");
        AddModifierEdges(edges, source, rule.Modifiers);

        var package = ResolveSynergyPackage(rule);
        if (!string.IsNullOrWhiteSpace(package?.GrantedTeamRuleId))
        {
            Add(
                edges,
                source,
                BuildGrammarRelation.PaysOff,
                "team_rule",
                package.GrantedTeamRuleId,
                feedbackRequired: true,
                expectedFeedbackWitness: ResolveTeamRuleWitness(package.GrantedTeamRuleId));
        }
    }

    private static CombatModifierPackage? ResolveSynergyPackage(TeamSynergyTierRule rule)
    {
        var units = Enumerable.Range(0, rule.Threshold)
            .Select(index => new BattleUnitLoadout(
                $"grammar-{index:D2}",
                $"Grammar {index:D2}",
                rule.CountedTagId,
                rule.CountedTagId,
                DeploymentAnchorId.FrontCenter,
                new Dictionary<StatKey, float>(),
                Array.Empty<UnitRuleChain>(),
                Array.Empty<BattleSkillSpec>(),
                CompileTags: new[] { rule.CountedTagId }))
            .ToArray();
        return SynergyService.BuildForTeam(units, new[] { rule })
            .OrderBy(package => package.SourceId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static void AddSubstitutionEdges(
        ICollection<EdgeDraft> edges,
        IReadOnlyList<BuildGrammarTruthSource> sources)
    {
        var groups = sources
            .Where(source => source.Actionable)
            .Select(source => new
            {
                Source = source,
                Key = !string.IsNullOrWhiteSpace(source.SlotId)
                    ? $"slot:{source.SubjectKind}:{source.SlotId}"
                    : !string.IsNullOrWhiteSpace(source.RoleId)
                        ? $"role:{source.SubjectKind}:{source.RoleId}"
                        : string.Empty,
            })
            .Where(value => !string.IsNullOrWhiteSpace(value.Key))
            .GroupBy(value => value.Key, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal);
        foreach (var group in groups)
        {
            var members = group.Select(value => value.Source)
                .OrderBy(source => source.SubjectId, StringComparer.Ordinal)
                .ToArray();
            for (var left = 0; left < members.Length; left++)
            {
                for (var right = 0; right < members.Length; right++)
                {
                    if (left == right)
                    {
                        continue;
                    }

                    Add(
                        edges,
                        members[left],
                        BuildGrammarRelation.Substitutes,
                        members[right].SubjectKind,
                        members[right].SubjectId);
                }
            }
        }
    }

    private static string ResolveModifierWitness(StatKey stat)
    {
        var id = stat.Canonicalized.Value;
        if (id is "phys_power" or "mag_power" or "crit_chance" or "crit_multiplier" or "phys_pen" or "mag_pen")
        {
            return "telemetry.damage_applied";
        }

        return id switch
        {
            "heal_power" => "telemetry.healing_applied",
            "barrier_power" => "telemetry.barrier_applied",
            "status_potency" => "telemetry.status_applied",
            _ => string.Empty,
        };
    }

    private static string ResolveTriggerWitness(CombatTriggerKind trigger)
        => trigger switch
        {
            CombatTriggerKind.OnKill => "beat.on_kill_effect",
            CombatTriggerKind.OnHpBelow => "beat.hp_threshold_effect",
            CombatTriggerKind.OnAllyDeath => "beat.ally_death_effect",
            _ => "beat.battle_start_effect",
        };

    private static string ResolveTeamRuleWitness(string ruleId)
    {
        if (string.Equals(ruleId, TeamRuleSet.ExecuteRuleId, StringComparison.Ordinal))
        {
            return "beat.combo_consumed";
        }

        if (string.Equals(ruleId, TeamRuleSet.BulwarkRuleId, StringComparison.Ordinal)
            || string.Equals(ruleId, TeamRuleSet.ResonanceRuleId, StringComparison.Ordinal))
        {
            return "beat.battle_start_effect";
        }

        if (string.Equals(ruleId, TeamRuleSet.BloodrushRuleId, StringComparison.Ordinal)
            || string.Equals(ruleId, TeamRuleSet.DeathTollRuleId, StringComparison.Ordinal)
            || string.Equals(ruleId, TeamRuleSet.KillzoneRuleId, StringComparison.Ordinal))
        {
            return "beat.on_kill_effect";
        }

        return "beat.synergy_activated";
    }

    private static IEnumerable<string> StableIds(IEnumerable<string>? values)
        => (values ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal);

    private static void Add(
        ICollection<EdgeDraft> edges,
        BuildGrammarTruthSource source,
        string relation,
        string targetKind,
        string targetId,
        string truthValue = "",
        bool feedbackRequired = false,
        string expectedFeedbackWitness = "")
    {
        if (string.IsNullOrWhiteSpace(targetId))
        {
            return;
        }

        edges.Add(new EdgeDraft(
            source.SubjectKind,
            source.SubjectId,
            relation,
            targetKind,
            targetId,
            truthValue ?? string.Empty,
            source.Actionable,
            feedbackRequired,
            expectedFeedbackWitness ?? string.Empty));
    }

    private sealed record EdgeDraft(
        string SubjectKind,
        string SubjectId,
        string Relation,
        string TargetKind,
        string TargetId,
        string TruthValue,
        bool Actionable,
        bool FeedbackRequired,
        string ExpectedFeedbackWitness);
}
