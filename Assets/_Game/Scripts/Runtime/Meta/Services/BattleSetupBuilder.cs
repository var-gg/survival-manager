using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Core.Contracts;
using SM.Core.Stats;
using SM.Meta.Model;

namespace SM.Meta.Services;

public static class BattleSetupBuilder
{
    public static BattleSetupBuildResult Build(
        IReadOnlyList<BattleParticipantSpec> allies,
        BattleEncounterPlan encounter,
        CombatContentSnapshot content)
    {
        var allyDefinitions = new List<BattleUnitLoadout>(allies.Count);
        foreach (var ally in allies)
        {
            if (!TryBuildDefinition(ally, content, out var definition, out var error))
            {
                return BattleSetupBuildResult.Fail(error);
            }

            allyDefinitions.Add(definition);
        }

        var enemyDefinitions = new List<BattleUnitLoadout>(encounter.EnemyParticipants.Count);
        foreach (var enemy in encounter.EnemyParticipants)
        {
            if (!TryBuildDefinition(enemy, content, out var definition, out var error))
            {
                return BattleSetupBuildResult.Fail(error);
            }

            enemyDefinitions.Add(definition);
        }

        // 팀 시너지 대칭 배선(오너 게이트② 채택, 2026-07-12) — 이 레인(캠페인 적군 + 샌드박스 양측)도
        // LoadoutCompiler와 같은 authored tier 평가를 받는다. 과거 TeamPackages null은 BattleFactory의
        // V1 하드코딩 폴백으로만 떨어져 authored 시너지 7계열이 적군에 영원히 미적용이었다(FM 정찰→
        // 카운터 편성 pillar 성립 조건). tier 미발화 comp은 기존 폴백과 동일 결과로 수렴한다.
        ApplyTeamPackages(allyDefinitions, content);
        ApplyTeamPackages(enemyDefinitions, content);

        return BattleSetupBuildResult.Success(allyDefinitions, enemyDefinitions, CombatStatusRuleCompiler.Compile(content));
    }

    private static void ApplyTeamPackages(List<BattleUnitLoadout> definitions, CombatContentSnapshot content)
    {
        if (definitions.Count == 0)
        {
            return;
        }

        var teamPackages = SynergyLoadoutService.BuildTeamPackages(definitions, content);
        for (var i = 0; i < definitions.Count; i++)
        {
            definitions[i] = definitions[i] with { TeamPackages = teamPackages };
        }
    }

    private static bool TryBuildDefinition(
        BattleParticipantSpec participant,
        CombatContentSnapshot content,
        out BattleUnitLoadout definition,
        out string error)
    {
        if (!content.Archetypes.TryGetValue(participant.ArchetypeId, out var archetype))
        {
            definition = null!;
            error = $"Combat archetype '{participant.ArchetypeId}'를 찾을 수 없습니다.";
            return false;
        }

        var packages = new List<CombatModifierPackage>();
        var appliedAffixTemplates = new List<AffixTemplate>();
        var deferredConditionalAffixes = new List<AffixTemplate>();
        if (!TryAddTraitPackage(participant.PositiveTraitId, content, packages, out error) ||
            !TryAddTraitPackage(participant.NegativeTraitId, content, packages, out error) ||
            !TryAddItemPackages(participant.EquippedItems, content, packages, appliedAffixTemplates, deferredConditionalAffixes, out error) ||
            !TryAddAugmentPackages(participant.TemporaryAugmentIds, content, packages, out error))
        {
            definition = null!;
            return false;
        }

        var roleInstructionId = ResolveRoleInstructionId(participant, content);
        var resolvedRoleInstruction = ResolveRoleInstruction(participant, content, roleInstructionId);
        var resolvedRoleTag = !string.IsNullOrWhiteSpace(resolvedRoleInstruction.RoleTag)
            ? resolvedRoleInstruction.RoleTag
            : string.IsNullOrWhiteSpace(participant.RoleTag) || participant.RoleTag == "auto"
                ? archetype.RoleTag
                : participant.RoleTag;

        // 조건부 affix 게이트(sandbox/적 레인 미러): 이 레인은 유닛 CompileTags를 굽지 않으므로
        // 평가용 태그 컨텍스트를 로컬로 조립한다. LoadoutCompiler와 동일하게 조건부의 자기
        // CompileTags는 컨텍스트에 넣지 않는다(자기충족 순환 차단).
        if (deferredConditionalAffixes.Count > 0)
        {
            var conditionContext = BuildAffixConditionContext(archetype, resolvedRoleTag, appliedAffixTemplates, participant.EquippedItems, content);
            foreach (var template in deferredConditionalAffixes)
            {
                if (!template.RequiredTags.All(conditionContext.Contains)
                    || template.ExcludedTags.Any(conditionContext.Contains))
                {
                    continue;
                }

                if (content.AffixPackages.TryGetValue(template.Id, out var affixPackage))
                {
                    packages.Add(affixPackage);
                }

                appliedAffixTemplates.Add(template);
            }
        }

        var rulePackages = BuildRulePackages(participant, archetype, appliedAffixTemplates);

        // 유닛 CompileTags 저작(null parity 해소) — LoadoutCompiler와 같은 축(race:/class:/raw/role/
        // 아이템/스킬/affix 태그). 시너지 tier 카운트(CountedTagId=race/class id)의 소재. 조건부 affix는
        // 위 게이트 통과분만 appliedAffixTemplates에 있어 자기충족 순환이 없고, ordinal 정렬로 결정적.
        var compileTags = BuildAffixConditionContext(archetype, resolvedRoleTag, appliedAffixTemplates, participant.EquippedItems, content)
            .OrderBy(tag => tag, StringComparer.Ordinal)
            .ToList();

        definition = new BattleUnitLoadout(
            participant.ParticipantId,
            participant.DisplayName,
            archetype.RaceId,
            archetype.ClassId,
            participant.Anchor,
            new Dictionary<SM.Core.Stats.StatKey, float>(archetype.BaseStats),
            new[] { new UnitRuleChain($"rules:{participant.ParticipantId}", archetype.Tactics.ToList()) },
            archetype.Skills.ToList(),
            new TeamTacticProfile(
                $"posture:{participant.TeamPosture}",
                participant.TeamPosture.ToString(),
                participant.TeamPosture),
            resolvedRoleInstruction,
            participant.OpeningIntent,
            packages,
            null,
            compileTags,
            resolvedRoleTag,
            RoleVariantTag.Unassigned,
            archetype.Footprint,
            archetype.Behavior,
            archetype.Mobility,
            archetype.PreferredDistance,
            archetype.ProtectRadius,
            archetype.Mana,
            rulePackages,
            null,
            archetype.BasicAttack,
            archetype.SignatureActive,
            archetype.FlexActive,
            archetype.SignaturePassive,
            archetype.FlexPassive,
            archetype.MobilityReaction,
            archetype.Energy,
            archetype.EntityKind,
            archetype.Ownership,
            archetype.SummonProfile,
            archetype.Governance,
            archetype.Id,
            string.IsNullOrWhiteSpace(participant.CharacterId) ? archetype.Id : participant.CharacterId,
            roleInstructionId,
            ResolveDominantHand(participant, content, archetype));
        error = string.Empty;
        return true;
    }

    private static IReadOnlyList<CombatRuleModifierPackage>? BuildRulePackages(
        BattleParticipantSpec participant,
        CombatArchetypeTemplate archetype,
        IReadOnlyList<AffixTemplate> appliedAffixTemplates)
    {
        var participantTags = (participant.RuleModifierTags ?? Array.Empty<string>())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(tag => tag, StringComparer.Ordinal)
            .ToList();
        var affixRulePackages = appliedAffixTemplates
            .Where(template => template.RulePackage != null)
            .Select(template => template.RulePackage!)
            .ToList();
        if (participantTags.Count == 0 && affixRulePackages.Count == 0)
        {
            return archetype.RulePackages;
        }

        var rulePackages = (archetype.RulePackages ?? Array.Empty<CombatRuleModifierPackage>()).ToList();
        if (participantTags.Count > 0)
        {
            rulePackages.Add(new CombatRuleModifierPackage(
                $"participant:{participant.ParticipantId}",
                ModifierSource.Other,
                participantTags.Select(tag => new RuleModifier(RuleModifierKind.BehaviorTag, tag)).ToList()));
        }

        rulePackages.AddRange(affixRulePackages);
        return rulePackages;
    }

    private static HashSet<string> BuildAffixConditionContext(
        CombatArchetypeTemplate archetype,
        string resolvedRoleTag,
        IReadOnlyList<AffixTemplate> appliedAffixTemplates,
        IReadOnlyList<BattleEquippedItemSpec> equippedItems,
        CombatContentSnapshot content)
    {
        var tags = new HashSet<string>(StringComparer.Ordinal)
        {
            $"race:{archetype.RaceId}",
            $"class:{archetype.ClassId}",
            archetype.RaceId,
            archetype.ClassId,
        };
        if (!string.IsNullOrWhiteSpace(resolvedRoleTag))
        {
            tags.Add(resolvedRoleTag);
        }

        if (content.ItemCatalog != null)
        {
            foreach (var item in equippedItems)
            {
                if (string.IsNullOrWhiteSpace(item.ItemBaseId)
                    || !content.ItemCatalog.TryGetValue(item.ItemBaseId, out var itemTemplate))
                {
                    continue;
                }

                foreach (var tag in itemTemplate.CompileTags)
                {
                    tags.Add(tag);
                }

                if (!string.IsNullOrWhiteSpace(itemTemplate.WeaponFamilyTag))
                {
                    tags.Add(itemTemplate.WeaponFamilyTag);
                }
            }
        }

        foreach (var skill in archetype.Skills)
        {
            foreach (var tag in skill.CompileTags ?? Array.Empty<string>())
            {
                tags.Add(tag);
            }
        }

        foreach (var template in appliedAffixTemplates)
        {
            foreach (var tag in template.CompileTags)
            {
                tags.Add(tag);
            }
        }

        return tags;
    }

    private static DominantHand ResolveDominantHand(
        BattleParticipantSpec participant,
        CombatContentSnapshot content,
        CombatArchetypeTemplate archetype)
    {
        if (!string.IsNullOrWhiteSpace(participant.CharacterId)
            && content.Characters != null
            && content.Characters.TryGetValue(participant.CharacterId, out var character))
        {
            return character.DominantHand;
        }

        return participant.DominantHand != DominantHand.Right
            ? participant.DominantHand
            : archetype.DefaultDominantHand;
    }

    private static string ResolveRoleInstructionId(BattleParticipantSpec participant, CombatContentSnapshot content)
    {
        if (!string.IsNullOrWhiteSpace(participant.RoleInstructionId))
        {
            return participant.RoleInstructionId;
        }

        if (!string.IsNullOrWhiteSpace(participant.CharacterId)
            && content.Characters != null
            && content.Characters.TryGetValue(participant.CharacterId, out var character)
            && !string.IsNullOrWhiteSpace(character.DefaultRoleInstructionId))
        {
            return character.DefaultRoleInstructionId;
        }

        return string.Empty;
    }

    private static SlotRoleInstruction ResolveRoleInstruction(BattleParticipantSpec participant, CombatContentSnapshot content, string roleInstructionId)
    {
        if (!string.IsNullOrWhiteSpace(roleInstructionId)
            && content.RoleInstructions.TryGetValue(roleInstructionId, out var role))
        {
            return role.Instruction with { Anchor = participant.Anchor };
        }

        return new SlotRoleInstruction(participant.Anchor, participant.RoleTag);
    }

    private static bool TryAddTraitPackage(
        string traitId,
        CombatContentSnapshot content,
        List<CombatModifierPackage> packages,
        out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(traitId))
        {
            return true;
        }

        if (!content.TraitPackages.TryGetValue(traitId, out var package))
        {
            error = $"Trait '{traitId}'를 찾을 수 없습니다.";
            return false;
        }

        packages.Add(package);
        return true;
    }

    private static bool TryAddItemPackages(
        IReadOnlyList<BattleEquippedItemSpec> equippedItems,
        CombatContentSnapshot content,
        List<CombatModifierPackage> packages,
        List<AffixTemplate> appliedAffixTemplates,
        List<AffixTemplate> deferredConditionalAffixes,
        out string error)
    {
        foreach (var item in equippedItems)
        {
            if (string.IsNullOrWhiteSpace(item.ItemBaseId))
            {
                continue;
            }

            if (!content.ItemPackages.TryGetValue(item.ItemBaseId, out var itemPackage))
            {
                error = $"Item '{item.ItemBaseId}'를 찾을 수 없습니다.";
                return false;
            }

            packages.Add(itemPackage);
            foreach (var affixId in item.AffixIds.Distinct())
            {
                if (!content.AffixPackages.TryGetValue(affixId, out var affixPackage))
                {
                    error = $"Affix '{affixId}'를 찾을 수 없습니다.";
                    return false;
                }

                var template = content.AffixCatalog != null && content.AffixCatalog.TryGetValue(affixId, out var resolved)
                    ? resolved
                    : null;
                if (template is { IsConditional: true })
                {
                    // 조건 평가는 role 해상 이후(TryBuildDefinition)에서 일괄 수행한다.
                    deferredConditionalAffixes.Add(template);
                    continue;
                }

                packages.Add(affixPackage);
                if (template != null)
                {
                    appliedAffixTemplates.Add(template);
                }
            }
        }

        error = string.Empty;
        return true;
    }

    private static bool TryAddAugmentPackages(
        IReadOnlyList<string> augmentIds,
        CombatContentSnapshot content,
        List<CombatModifierPackage> packages,
        out string error)
    {
        foreach (var augmentId in augmentIds.Distinct())
        {
            if (string.IsNullOrWhiteSpace(augmentId))
            {
                continue;
            }

            if (!content.AugmentPackages.TryGetValue(augmentId, out var package))
            {
                error = $"Augment '{augmentId}'를 찾을 수 없습니다.";
                return false;
            }

            packages.Add(package);
        }

        error = string.Empty;
        return true;
    }
}
