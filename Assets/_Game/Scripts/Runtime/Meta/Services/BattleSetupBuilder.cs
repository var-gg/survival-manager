using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
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

        return BattleSetupBuildResult.Success(allyDefinitions, enemyDefinitions);
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
        if (!TryAddTraitPackage(participant.PositiveTraitId, content, packages, out error) ||
            !TryAddTraitPackage(participant.NegativeTraitId, content, packages, out error) ||
            !TryAddItemPackages(participant.EquippedItems, content, packages, out error) ||
            !TryAddAugmentPackages(participant.TemporaryAugmentIds, content, packages, out error))
        {
            definition = null!;
            return false;
        }

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
            new SlotRoleInstruction(participant.Anchor, participant.RoleTag),
            participant.OpeningIntent,
            packages);
        error = string.Empty;
        return true;
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

                packages.Add(affixPackage);
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
