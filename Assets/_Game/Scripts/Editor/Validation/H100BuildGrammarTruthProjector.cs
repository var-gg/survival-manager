using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.HeadlessCensus;
using SM.Meta.Model;

namespace SM.Editor.Validation;

/// <summary>CombatContentSnapshot을 evaluator-only Census 입력으로 낮추고 truth graph를 만든다.</summary>
internal static class H100BuildGrammarTruthProjector
{
    public static BuildGrammarTruthGraph Project(CombatContentSnapshot snapshot)
    {
        if (snapshot == null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        var sources = new List<BuildGrammarTruthSource>();
        var skills = new Dictionary<string, SkillSourceAccumulator>(StringComparer.Ordinal);
        AddArchetypes(snapshot, sources, skills);
        AddItems(snapshot, sources, skills);
        AddAffixes(snapshot, sources);
        AddAugments(snapshot, sources);
        AddPassives(snapshot, sources, skills);
        AddSynergies(snapshot, sources);
        sources.AddRange(skills.Values
            .OrderBy(value => value.Skill.Id, StringComparer.Ordinal)
            .Select(value => new BuildGrammarTruthSource(
                BuildGrammarSubjectKind.Skill,
                value.Skill.Id,
                value.AcquisitionPaths.Count > 0,
                SlotId: CompiledSkillSlots.Normalize(value.Skill.SlotKind),
                AcquisitionPaths: value.AcquisitionPaths.OrderBy(path => path, StringComparer.Ordinal).ToArray(),
                Skill: value.Skill)));
        return BuildGrammarTruthGraphBuilder.Build(sources);
    }

    private static void AddArchetypes(
        CombatContentSnapshot snapshot,
        ICollection<BuildGrammarTruthSource> sources,
        IDictionary<string, SkillSourceAccumulator> skills)
    {
        foreach (var archetype in snapshot.Archetypes.Values
                     .Where(archetype => archetype != null && !string.IsNullOrWhiteSpace(archetype.Id))
                     .OrderBy(archetype => archetype.Id, StringComparer.Ordinal))
        {
            var recruitable = archetype.IsRecruitable;
            sources.Add(new BuildGrammarTruthSource(
                BuildGrammarSubjectKind.Archetype,
                archetype.Id,
                recruitable,
                RoleId: string.IsNullOrWhiteSpace(archetype.RoleTag) ? archetype.ClassId : archetype.RoleTag,
                Tags: StableIds(new[] { archetype.RaceId, archetype.ClassId }
                    .Concat(archetype.RecruitPlanTags ?? Array.Empty<string>())),
                AcquisitionPaths: recruitable ? new[] { "recruit" } : Array.Empty<string>()));

            foreach (var skill in CollectArchetypeSkills(archetype))
            {
                AddSkill(skills, skill, recruitable ? "recruit" : string.Empty);
            }
        }
    }

    private static void AddItems(
        CombatContentSnapshot snapshot,
        ICollection<BuildGrammarTruthSource> sources,
        IDictionary<string, SkillSourceAccumulator> skills)
    {
        var itemIds = snapshot.ItemPackages.Keys
            .Concat(snapshot.ItemCatalog?.Keys ?? Array.Empty<string>())
            .Concat(snapshot.ItemGrantedSkills?.Keys ?? Array.Empty<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal);
        foreach (var itemId in itemIds)
        {
            snapshot.ItemPackages.TryGetValue(itemId, out var package);
            ItemTemplate? item = null;
            snapshot.ItemCatalog?.TryGetValue(itemId, out item);
            IReadOnlyList<BattleSkillSpec>? grantedSkills = null;
            snapshot.ItemGrantedSkills?.TryGetValue(itemId, out grantedSkills);
            var granted = (grantedSkills ?? Array.Empty<BattleSkillSpec>())
                .Where(skill => skill != null && !string.IsNullOrWhiteSpace(skill.Id))
                .OrderBy(skill => skill.Id, StringComparer.Ordinal)
                .ToArray();
            sources.Add(new BuildGrammarTruthSource(
                BuildGrammarSubjectKind.Item,
                itemId,
                Actionable: true,
                Tags: StableIds((item?.CompileTags ?? Array.Empty<string>())
                    .Append(item?.WeaponFamilyTag ?? string.Empty)),
                AcquisitionPaths: new[] { "reward" },
                GrantedSkillIds: granted.Select(skill => skill.Id).ToArray(),
                ModifierPackage: package));
            foreach (var skill in granted)
            {
                AddSkill(skills, skill, "reward");
            }
        }
    }

    private static void AddAffixes(
        CombatContentSnapshot snapshot,
        ICollection<BuildGrammarTruthSource> sources)
    {
        var affixIds = snapshot.AffixPackages.Keys
            .Concat(snapshot.AffixCatalog?.Keys ?? Array.Empty<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal);
        foreach (var affixId in affixIds)
        {
            snapshot.AffixPackages.TryGetValue(affixId, out var package);
            AffixTemplate? affix = null;
            snapshot.AffixCatalog?.TryGetValue(affixId, out affix);
            sources.Add(new BuildGrammarTruthSource(
                BuildGrammarSubjectKind.Affix,
                affixId,
                Actionable: true,
                Tags: StableIds(affix?.CompileTags),
                RequiredTags: StableIds(affix?.RequiredTags),
                ExcludedTags: StableIds(affix?.ExcludedTags),
                AcquisitionPaths: new[] { "refit" },
                ModifierPackage: package,
                RulePackage: affix?.RulePackage));
        }
    }

    private static void AddAugments(
        CombatContentSnapshot snapshot,
        ICollection<BuildGrammarTruthSource> sources)
    {
        var augmentIds = snapshot.AugmentPackages.Keys
            .Concat(snapshot.AugmentCatalog.Keys)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal);
        foreach (var augmentId in augmentIds)
        {
            snapshot.AugmentPackages.TryGetValue(augmentId, out var package);
            snapshot.AugmentCatalog.TryGetValue(augmentId, out var augment);
            sources.Add(new BuildGrammarTruthSource(
                BuildGrammarSubjectKind.Augment,
                augmentId,
                Actionable: true,
                SlotId: augment?.FamilyId ?? string.Empty,
                Tags: StableIds((augment?.Tags ?? Array.Empty<string>())
                    .Concat(augment?.BuildBiasTags ?? Array.Empty<string>())),
                ConflictIds: StableIds(augment?.MutualExclusionTags),
                AcquisitionPaths: new[] { "reward" },
                ModifierPackage: package,
                RulePackage: augment?.RulePackage,
                TriggeredEffects: augment?.TriggeredEffects));
        }
    }

    private static void AddPassives(
        CombatContentSnapshot snapshot,
        ICollection<BuildGrammarTruthSource> sources,
        IDictionary<string, SkillSourceAccumulator> skills)
    {
        foreach (var passive in snapshot.PassiveNodes.Values
                     .Where(passive => passive != null && !string.IsNullOrWhiteSpace(passive.Id))
                     .OrderBy(passive => passive.Id, StringComparer.Ordinal))
        {
            var grantedSkillIds = string.IsNullOrWhiteSpace(passive.GrantedSkillId)
                ? Array.Empty<string>()
                : new[] { passive.GrantedSkillId };
            sources.Add(new BuildGrammarTruthSource(
                BuildGrammarSubjectKind.Passive,
                passive.Id,
                Actionable: true,
                Tags: StableIds(passive.CompileTags),
                PrerequisiteIds: StableIds(passive.PrerequisiteNodeIds),
                ConflictIds: StableIds(passive.MutualExclusionTagIds),
                AcquisitionPaths: new[] { "level_node" },
                GrantedSkillIds: grantedSkillIds,
                ModifierPackage: passive.Package,
                RulePackage: passive.RulePackage));
            if (!string.IsNullOrWhiteSpace(passive.GrantedSkillId)
                && snapshot.SkillCatalog.TryGetValue(passive.GrantedSkillId, out var grantedSkill))
            {
                AddSkill(skills, grantedSkill, "level_node");
            }
        }
    }

    private static void AddSynergies(
        CombatContentSnapshot snapshot,
        ICollection<BuildGrammarTruthSource> sources)
    {
        foreach (var rule in snapshot.SynergyCatalog.Values
                     .Where(template => template?.Rule != null
                                        && !string.IsNullOrWhiteSpace(template.Rule.SynergyId)
                                        && template.Rule.Threshold > 0)
                     .Select(template => template.Rule)
                     .OrderBy(rule => rule.SynergyId, StringComparer.Ordinal)
                     .ThenBy(rule => rule.Threshold)
                     .ThenBy(rule => rule.CountedTagId, StringComparer.Ordinal))
        {
            sources.Add(new BuildGrammarTruthSource(
                BuildGrammarSubjectKind.Synergy,
                $"{rule.SynergyId}@{rule.Threshold}",
                Actionable: true,
                AcquisitionPaths: new[] { "squad_composition" },
                SynergyRule: rule));
        }
    }

    private static IEnumerable<BattleSkillSpec> CollectArchetypeSkills(CombatArchetypeTemplate archetype)
    {
        var candidates = (archetype.Skills ?? Array.Empty<BattleSkillSpec>())
            .Concat(archetype.RecruitFlexActivePool ?? Array.Empty<BattleSkillSpec>())
            .Concat(archetype.RecruitFlexPassivePool ?? Array.Empty<BattleSkillSpec>());
        if (archetype.SignatureActive != null)
        {
            candidates = candidates.Append(archetype.SignatureActive);
        }

        if (archetype.FlexActive != null)
        {
            candidates = candidates.Append(archetype.FlexActive);
        }

        return candidates
            .Where(skill => skill != null && !string.IsNullOrWhiteSpace(skill.Id))
            .GroupBy(skill => skill.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(skill => skill.Id, StringComparer.Ordinal);
    }

    private static void AddSkill(
        IDictionary<string, SkillSourceAccumulator> skills,
        BattleSkillSpec skill,
        string acquisitionPath)
    {
        if (skill == null || string.IsNullOrWhiteSpace(skill.Id))
        {
            return;
        }

        if (!skills.TryGetValue(skill.Id, out var source))
        {
            source = new SkillSourceAccumulator(skill);
            skills.Add(skill.Id, source);
        }

        if (!string.IsNullOrWhiteSpace(acquisitionPath))
        {
            source.AcquisitionPaths.Add(acquisitionPath);
        }
    }

    private static string[] StableIds(IEnumerable<string>? ids)
        => (ids ?? Array.Empty<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

    private sealed class SkillSourceAccumulator
    {
        public SkillSourceAccumulator(BattleSkillSpec skill)
        {
            Skill = skill;
        }

        public BattleSkillSpec Skill { get; }

        public HashSet<string> AcquisitionPaths { get; } = new(StringComparer.Ordinal);
    }
}
