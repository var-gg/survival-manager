using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.HeadlessCensus;
using SM.HeadlessMetrics;
using SM.HeadlessPolicies;
using SM.Meta.Model;

namespace SM.Editor.Validation;

/// <summary>E01 fact projection이 실제로 운반하는 의미만 audit DTO로 재투영한다.</summary>
internal static class H100BuildGrammarVisibleSurfaceProjector
{
    public static InformationSurfaceAuditInput Project(
        CombatContentSnapshot snapshot,
        BuildGrammarTruthGraph truthGraph)
    {
        if (snapshot == null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        if (truthGraph == null)
        {
            throw new ArgumentNullException(nameof(truthGraph));
        }

        var observation = H100BuildGrammarCatalogObservationBuilder.Build(snapshot);
        var projection = H100PlayerVisibleFactProjector.Project(
            "h100-surface-audit",
            "catalog",
            new PlayerVisibleTimelinePoint(0, 0, 0),
            observation);
        var semantics = new List<PlayerVisibleBuildGrammarSemantic>();
        var tokens = new List<PlayerVisibleTokenUse>();
        AddHeroSurface(projection.Observation, semantics, tokens);
        AddAugmentSurface(projection.Observation, semantics, tokens);
        AddSynergySurface(projection.Observation, semantics, tokens);
        AddRewardSurface(projection.Observation, semantics, tokens);

        var edges = truthGraph.Edges.Select(edge => new BuildGrammarAuditEdge(
                edge.EdgeId,
                edge.SubjectKind,
                edge.SubjectId,
                edge.Relation,
                edge.TargetKind,
                edge.TargetId,
                edge.TruthValue,
                edge.Actionable,
                edge.FeedbackRequired,
                edge.ExpectedFeedbackWitness))
            .OrderBy(edge => edge.EdgeId, StringComparer.Ordinal)
            .ToArray();
        return new InformationSurfaceAuditInput(
            edges,
            semantics.Distinct()
                .OrderBy(semantic => semantic.SubjectKind, StringComparer.Ordinal)
                .ThenBy(semantic => semantic.SubjectId, StringComparer.Ordinal)
                .ThenBy(semantic => semantic.Relation, StringComparer.Ordinal)
                .ThenBy(semantic => semantic.TargetKind, StringComparer.Ordinal)
                .ThenBy(semantic => semantic.TargetId, StringComparer.Ordinal)
                .ThenBy(semantic => semantic.SourceFactId, StringComparer.Ordinal)
                .ToArray(),
            tokens.Distinct()
                .OrderBy(token => token.TokenKind, StringComparer.Ordinal)
                .ThenBy(token => token.TokenId, StringComparer.Ordinal)
                .ThenBy(token => token.SourceFactId, StringComparer.Ordinal)
                .ToArray(),
            FeedbackWitnessVocabulary());
    }

    private static void AddHeroSurface(
        HeadlessPolicyObservation observation,
        ICollection<PlayerVisibleBuildGrammarSemantic> semantics,
        ICollection<PlayerVisibleTokenUse> tokens)
    {
        var visibleSkills = new List<VisibleSubstitute>();
        var visibleArchetypes = new List<VisibleSubstitute>();
        foreach (var hero in observation.Roster.OrderBy(hero => hero.HeroId, StringComparer.Ordinal))
        {
            var heroFactId = FactId(observation, $"hero.{hero.HeroId}");
            visibleArchetypes.Add(new VisibleSubstitute(
                BuildGrammarSubjectKind.Archetype,
                hero.ArchetypeId,
                string.IsNullOrWhiteSpace(hero.RoleTag) ? hero.ClassId : hero.RoleTag,
                heroFactId,
                AvailableBeforeChoice: false));
            AddToken(tokens, heroFactId, "tag", hero.RaceId, isDefinition: true);
            AddToken(tokens, heroFactId, "tag", hero.ClassId, isDefinition: true);

            foreach (var skill in hero.SkillCards.OrderBy(skill => skill.SkillId, StringComparer.Ordinal))
            {
                var factId = FactId(observation, $"hero.{hero.HeroId}.skill.{skill.SkillId}");
                AddSkillSemantics(
                    semantics,
                    tokens,
                    factId,
                    PlayerVisibleUiSource.RosterSheetSkill,
                    skill,
                    AvailableBeforeChoice: false);
                visibleSkills.Add(new VisibleSubstitute(
                    BuildGrammarSubjectKind.Skill,
                    skill.SkillId,
                    skill.SlotKind,
                    factId,
                    AvailableBeforeChoice: false));
            }

            foreach (var item in hero.EquippedItems.OrderBy(item => item.ItemId, StringComparer.Ordinal)
                         .ThenBy(item => item.ItemInstanceId, StringComparer.Ordinal))
            {
                var factId = FactId(
                    observation,
                    $"hero.{hero.HeroId}.item.{item.ItemInstanceId}.{item.ItemId}");
                AddItemSemantics(
                    semantics,
                    tokens,
                    visibleSkills,
                    factId,
                    PlayerVisibleUiSource.RosterSheetItem,
                    item,
                    AvailableBeforeChoice: false,
                    includeAcquisition: false);
            }
        }

        AddSubstitutes(semantics, visibleArchetypes);
        AddSubstitutes(semantics, visibleSkills);
    }

    private static void AddAugmentSurface(
        HeadlessPolicyObservation observation,
        ICollection<PlayerVisibleBuildGrammarSemantic> semantics,
        ICollection<PlayerVisibleTokenUse> tokens)
    {
        foreach (var augment in observation.TemporaryAugments.OrderBy(value => value.AugmentId, StringComparer.Ordinal))
        {
            var factId = FactId(observation, $"augment.{augment.AugmentId}");
            AddAugmentSemantics(
                semantics,
                tokens,
                factId,
                PlayerVisibleUiSource.RunAugmentPanel,
                augment,
                AvailableBeforeChoice: false,
                includeAcquisition: false);
        }
    }

    private static void AddSynergySurface(
        HeadlessPolicyObservation observation,
        ICollection<PlayerVisibleBuildGrammarSemantic> semantics,
        ICollection<PlayerVisibleTokenUse> tokens)
    {
        foreach (var synergy in observation.SynergyCatalog.OrderBy(value => value.SynergyId, StringComparer.Ordinal))
        {
            var factId = FactId(observation, $"synergy.catalog.{synergy.SynergyId}");
            AddToken(tokens, factId, "tag", synergy.CountedTagId, isDefinition: false);
            foreach (var tier in synergy.Tiers.OrderBy(value => value.Threshold)
                         .ThenBy(value => value.GrantedTeamRuleId, StringComparer.Ordinal))
            {
                var subjectId = $"{synergy.SynergyId}@{tier.Threshold}";
                AddSemantic(
                    semantics,
                    factId,
                    PlayerVisibleUiSource.CompendiumSynergy,
                    BuildGrammarSubjectKind.Synergy,
                    subjectId,
                    BuildGrammarRelation.AcquiredBy,
                    "acquisition",
                    "squad_composition",
                    string.Empty,
                    availableBeforeChoice: true);
                AddSemantic(
                    semantics,
                    factId,
                    PlayerVisibleUiSource.CompendiumSynergy,
                    BuildGrammarSubjectKind.Synergy,
                    subjectId,
                    BuildGrammarRelation.Requires,
                    "tag",
                    synergy.CountedTagId,
                    BuildGrammarTruthValue.Threshold(tier.Threshold),
                    availableBeforeChoice: true);
                AddModifierSemantics(
                    semantics,
                    factId,
                    PlayerVisibleUiSource.CompendiumSynergy,
                    BuildGrammarSubjectKind.Synergy,
                    subjectId,
                    tier.StatModifiers,
                    availableBeforeChoice: true);
                if (!string.IsNullOrWhiteSpace(tier.GrantedTeamRuleId))
                {
                    AddSemantic(
                        semantics,
                        factId,
                        PlayerVisibleUiSource.CompendiumSynergy,
                        BuildGrammarSubjectKind.Synergy,
                        subjectId,
                        BuildGrammarRelation.PaysOff,
                        "team_rule",
                        tier.GrantedTeamRuleId,
                        string.Empty,
                        availableBeforeChoice: true);
                    AddToken(tokens, factId, "team_rule", tier.GrantedTeamRuleId, isDefinition: false);
                }
            }
        }
    }

    private static void AddRewardSurface(
        HeadlessPolicyObservation observation,
        ICollection<PlayerVisibleBuildGrammarSemantic> semantics,
        ICollection<PlayerVisibleTokenUse> tokens)
    {
        var visibleSkills = new List<VisibleSubstitute>();
        var visibleAugments = new List<VisibleSubstitute>();
        foreach (var option in observation.RewardOptions.OrderBy(option => option.Index))
        {
            var factId = FactId(observation, $"reward.option.{option.Index}");
            if (option.Mechanics.Item != null)
            {
                AddItemSemantics(
                    semantics,
                    tokens,
                    visibleSkills,
                    factId,
                    PlayerVisibleUiSource.RewardCard,
                    option.Mechanics.Item,
                    AvailableBeforeChoice: true,
                    includeAcquisition: true);
            }

            if (option.Mechanics.TemporaryAugment != null)
            {
                var augment = option.Mechanics.TemporaryAugment;
                AddAugmentSemantics(
                    semantics,
                    tokens,
                    factId,
                    PlayerVisibleUiSource.RewardCard,
                    augment,
                    AvailableBeforeChoice: true,
                    includeAcquisition: true);
                visibleAugments.Add(new VisibleSubstitute(
                    BuildGrammarSubjectKind.Augment,
                    augment.AugmentId,
                    augment.FamilyId,
                    factId,
                    AvailableBeforeChoice: true));
            }
        }

        AddSubstitutes(semantics, visibleSkills);
        AddSubstitutes(semantics, visibleAugments);
    }

    private static void AddItemSemantics(
        ICollection<PlayerVisibleBuildGrammarSemantic> semantics,
        ICollection<PlayerVisibleTokenUse> tokens,
        ICollection<VisibleSubstitute> visibleSkills,
        string factId,
        string uiSource,
        HeadlessItemMechanicsObservation item,
        bool AvailableBeforeChoice,
        bool includeAcquisition)
    {
        if (includeAcquisition)
        {
            AddSemantic(
                semantics,
                factId,
                uiSource,
                BuildGrammarSubjectKind.Item,
                item.ItemId,
                BuildGrammarRelation.AcquiredBy,
                "acquisition",
                "reward",
                string.Empty,
                AvailableBeforeChoice);
        }

        AddModifierSemantics(
            semantics,
            factId,
            uiSource,
            BuildGrammarSubjectKind.Item,
            item.ItemId,
            item.StatModifiers,
            AvailableBeforeChoice);
        foreach (var tag in item.Tags.OrderBy(value => value, StringComparer.Ordinal))
        {
            AddToken(tokens, factId, "tag", tag, isDefinition: false);
        }

        AddToken(tokens, factId, "tag", item.WeaponFamilyTag, isDefinition: false);
        foreach (var skill in item.GrantedSkills.OrderBy(skill => skill.SkillId, StringComparer.Ordinal))
        {
            AddSemantic(
                semantics,
                factId,
                uiSource,
                BuildGrammarSubjectKind.Item,
                item.ItemId,
                BuildGrammarRelation.Produces,
                "skill",
                skill.SkillId,
                string.Empty,
                AvailableBeforeChoice);
            AddSkillSemantics(semantics, tokens, factId, uiSource, skill, AvailableBeforeChoice);
            AddSemantic(
                semantics,
                factId,
                uiSource,
                BuildGrammarSubjectKind.Skill,
                skill.SkillId,
                BuildGrammarRelation.AcquiredBy,
                "acquisition",
                "reward",
                string.Empty,
                AvailableBeforeChoice);
            visibleSkills.Add(new VisibleSubstitute(
                BuildGrammarSubjectKind.Skill,
                skill.SkillId,
                skill.SlotKind,
                factId,
                AvailableBeforeChoice));
        }

        foreach (var affix in item.Affixes.OrderBy(value => value.AffixId, StringComparer.Ordinal))
        {
            AddModifierSemantics(
                semantics,
                factId,
                uiSource,
                BuildGrammarSubjectKind.Affix,
                affix.AffixId,
                affix.StatModifiers,
                AvailableBeforeChoice);
            AddRuleModifierSemantics(
                semantics,
                factId,
                uiSource,
                BuildGrammarSubjectKind.Affix,
                affix.AffixId,
                affix.RuleModifiers,
                AvailableBeforeChoice);
            foreach (var required in affix.RequiredTags.OrderBy(value => value, StringComparer.Ordinal))
            {
                AddSemantic(
                    semantics,
                    factId,
                    uiSource,
                    BuildGrammarSubjectKind.Affix,
                    affix.AffixId,
                    BuildGrammarRelation.Requires,
                    "tag",
                    required,
                    string.Empty,
                    AvailableBeforeChoice);
                AddToken(tokens, factId, "tag", required, isDefinition: false);
            }

            foreach (var excluded in affix.ExcludedTags.OrderBy(value => value, StringComparer.Ordinal))
            {
                AddSemantic(
                    semantics,
                    factId,
                    uiSource,
                    BuildGrammarSubjectKind.Affix,
                    affix.AffixId,
                    BuildGrammarRelation.Conflicts,
                    "tag",
                    excluded,
                    string.Empty,
                    AvailableBeforeChoice);
                AddToken(tokens, factId, "tag", excluded, isDefinition: false);
            }
        }
    }

    private static void AddAugmentSemantics(
        ICollection<PlayerVisibleBuildGrammarSemantic> semantics,
        ICollection<PlayerVisibleTokenUse> tokens,
        string factId,
        string uiSource,
        HeadlessAugmentMechanicsObservation augment,
        bool AvailableBeforeChoice,
        bool includeAcquisition)
    {
        if (includeAcquisition)
        {
            AddSemantic(
                semantics,
                factId,
                uiSource,
                BuildGrammarSubjectKind.Augment,
                augment.AugmentId,
                BuildGrammarRelation.AcquiredBy,
                "acquisition",
                "reward",
                string.Empty,
                AvailableBeforeChoice);
        }

        AddModifierSemantics(
            semantics,
            factId,
            uiSource,
            BuildGrammarSubjectKind.Augment,
            augment.AugmentId,
            augment.StatModifiers,
            AvailableBeforeChoice);
        AddRuleModifierSemantics(
            semantics,
            factId,
            uiSource,
            BuildGrammarSubjectKind.Augment,
            augment.AugmentId,
            augment.RuleModifiers,
            AvailableBeforeChoice);
        foreach (var effect in augment.TriggeredEffects.OrderBy(effect => effect.Trigger, StringComparer.Ordinal)
                     .ThenBy(effect => effect.Operation, StringComparer.Ordinal)
                     .ThenBy(effect => effect.Scope, StringComparer.Ordinal)
                     .ThenBy(effect => effect.StatusId, StringComparer.Ordinal))
        {
            var relation = string.Equals(effect.Operation, "ApplyStatus", StringComparison.Ordinal)
                ? BuildGrammarRelation.Produces
                : BuildGrammarRelation.PaysOff;
            var targetKind = relation == BuildGrammarRelation.Produces ? "status" : "combat_effect";
            var targetId = relation == BuildGrammarRelation.Produces ? effect.StatusId : effect.Operation;
            AddSemantic(
                semantics,
                factId,
                uiSource,
                BuildGrammarSubjectKind.Augment,
                augment.AugmentId,
                relation,
                targetKind,
                targetId,
                BuildGrammarTruthValue.Trigger(
                    effect.Trigger,
                    effect.Operation,
                    effect.Scope,
                    effect.Magnitude,
                    effect.ThresholdRatio,
                    effect.DurationSeconds,
                    effect.MaxStacks),
                AvailableBeforeChoice);
            if (relation == BuildGrammarRelation.Produces)
            {
                AddToken(tokens, factId, "status", effect.StatusId, isDefinition: false);
            }
        }

        foreach (var tag in augment.Tags.Concat(augment.BuildBiasTags)
                     .Where(value => !string.IsNullOrWhiteSpace(value))
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(value => value, StringComparer.Ordinal))
        {
            AddToken(tokens, factId, "tag", tag, isDefinition: false);
        }
    }

    private static void AddSkillSemantics(
        ICollection<PlayerVisibleBuildGrammarSemantic> semantics,
        ICollection<PlayerVisibleTokenUse> tokens,
        string factId,
        string uiSource,
        HeadlessSkillObservation skill,
        bool AvailableBeforeChoice)
    {
        foreach (var status in skill.AppliedStatuses.OrderBy(value => value.StatusId, StringComparer.Ordinal)
                     .ThenBy(value => value.ApplicationId, StringComparer.Ordinal))
        {
            AddSemantic(
                semantics,
                factId,
                uiSource,
                BuildGrammarSubjectKind.Skill,
                skill.SkillId,
                BuildGrammarRelation.Produces,
                "status",
                status.StatusId,
                BuildGrammarTruthValue.Status(status.DurationSeconds, status.Magnitude, status.MaxStacks),
                AvailableBeforeChoice);
            AddToken(tokens, factId, "status", status.StatusId, isDefinition: false);
        }

        var payoffId = skill.Kind switch
        {
            SkillKind.Strike => "damage",
            SkillKind.Heal => "healing",
            SkillKind.Shield => "barrier",
            _ => string.Empty,
        };
        if (!string.IsNullOrWhiteSpace(payoffId))
        {
            AddSemantic(
                semantics,
                factId,
                uiSource,
                BuildGrammarSubjectKind.Skill,
                skill.SkillId,
                BuildGrammarRelation.PaysOff,
                "combat_effect",
                payoffId,
                BuildGrammarTruthValue.SkillPayoff(
                    skill.PowerFlat == 0f ? skill.Power : skill.PowerFlat,
                    skill.PhysicalCoefficient,
                    skill.MagicalCoefficient,
                    skill.HealingCoefficient,
                    skill.HealthCoefficient,
                    skill.CanCrit),
                AvailableBeforeChoice);
        }
    }

    private static void AddModifierSemantics(
        ICollection<PlayerVisibleBuildGrammarSemantic> semantics,
        string factId,
        string uiSource,
        string subjectKind,
        string subjectId,
        IEnumerable<HeadlessStatModifierObservation> modifiers,
        bool availableBeforeChoice)
    {
        foreach (var modifier in modifiers.OrderBy(value => value.StatId, StringComparer.Ordinal)
                     .ThenBy(value => value.Operation, StringComparer.Ordinal)
                     .ThenBy(value => value.Value)
                     .ThenBy(value => value.TagId, StringComparer.Ordinal))
        {
            AddSemantic(
                semantics,
                factId,
                uiSource,
                subjectKind,
                subjectId,
                BuildGrammarRelation.Amplifies,
                "stat",
                modifier.StatId,
                BuildGrammarTruthValue.Modifier(
                    modifier.StatId,
                    modifier.Operation,
                    modifier.Value,
                    modifier.TagId),
                availableBeforeChoice);
        }
    }

    private static void AddRuleModifierSemantics(
        ICollection<PlayerVisibleBuildGrammarSemantic> semantics,
        string factId,
        string uiSource,
        string subjectKind,
        string subjectId,
        IEnumerable<HeadlessRuleModifierObservation> modifiers,
        bool availableBeforeChoice)
    {
        foreach (var modifier in modifiers.OrderBy(value => value.Kind, StringComparer.Ordinal)
                     .ThenBy(value => value.Value, StringComparer.Ordinal)
                     .ThenBy(value => value.Magnitude))
        {
            AddSemantic(
                semantics,
                factId,
                uiSource,
                subjectKind,
                subjectId,
                BuildGrammarRelation.Amplifies,
                "rule_modifier",
                $"{modifier.Kind}:{modifier.Value}",
                BuildGrammarTruthValue.RuleModifier(modifier.Kind, modifier.Value, modifier.Magnitude),
                availableBeforeChoice);
        }
    }

    private static void AddSubstitutes(
        ICollection<PlayerVisibleBuildGrammarSemantic> semantics,
        IEnumerable<VisibleSubstitute> values)
    {
        foreach (var group in values.Where(value => !string.IsNullOrWhiteSpace(value.GroupId))
                     .GroupBy(value => $"{value.SubjectKind}|{value.GroupId}", StringComparer.Ordinal)
                     .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var members = group.GroupBy(value => value.SubjectId, StringComparer.Ordinal)
                .Select(memberGroup => memberGroup.OrderByDescending(value => value.AvailableBeforeChoice)
                    .ThenBy(value => value.SourceFactId, StringComparer.Ordinal)
                    .First())
                .OrderBy(value => value.SubjectId, StringComparer.Ordinal)
                .ToArray();
            foreach (var left in members)
            {
                foreach (var right in members.Where(right => !string.Equals(
                             right.SubjectId,
                             left.SubjectId,
                             StringComparison.Ordinal)))
                {
                    AddSemantic(
                        semantics,
                        left.SourceFactId,
                        string.Empty,
                        left.SubjectKind,
                        left.SubjectId,
                        BuildGrammarRelation.Substitutes,
                        right.SubjectKind,
                        right.SubjectId,
                        string.Empty,
                        left.AvailableBeforeChoice && right.AvailableBeforeChoice);
                }
            }
        }
    }

    private static void AddSemantic(
        ICollection<PlayerVisibleBuildGrammarSemantic> semantics,
        string factId,
        string uiSource,
        string subjectKind,
        string subjectId,
        string relation,
        string targetKind,
        string targetId,
        string value,
        bool availableBeforeChoice)
    {
        if (string.IsNullOrWhiteSpace(factId)
            || string.IsNullOrWhiteSpace(subjectId)
            || string.IsNullOrWhiteSpace(targetId))
        {
            return;
        }

        semantics.Add(new PlayerVisibleBuildGrammarSemantic(
            factId,
            uiSource ?? string.Empty,
            subjectKind,
            subjectId,
            relation,
            targetKind,
            targetId,
            value ?? string.Empty,
            availableBeforeChoice));
    }

    private static void AddToken(
        ICollection<PlayerVisibleTokenUse> tokens,
        string factId,
        string tokenKind,
        string tokenId,
        bool isDefinition)
    {
        if (string.IsNullOrWhiteSpace(factId) || string.IsNullOrWhiteSpace(tokenId))
        {
            return;
        }

        tokens.Add(new PlayerVisibleTokenUse(factId, tokenKind, tokenId, isDefinition));
    }

    private static string FactId(HeadlessPolicyObservation observation, string key)
        => observation.EvidenceFactIdsBySignal.TryGetValue(key, out var factId) ? factId : string.Empty;

    private static IReadOnlyList<string> FeedbackWitnessVocabulary()
        => new[]
        {
            "beat.ally_death_effect",
            "beat.battle_start_effect",
            "beat.combo_consumed",
            "beat.hp_threshold_effect",
            "beat.on_kill_effect",
            "beat.synergy_activated",
            "telemetry.barrier_applied",
            "telemetry.damage_applied",
            "telemetry.healing_applied",
            "telemetry.skill_cast_resolved",
            "telemetry.status_applied",
            "telemetry.status_removed",
        };

    private sealed record VisibleSubstitute(
        string SubjectKind,
        string SubjectId,
        string GroupId,
        string SourceFactId,
        bool AvailableBeforeChoice);
}
