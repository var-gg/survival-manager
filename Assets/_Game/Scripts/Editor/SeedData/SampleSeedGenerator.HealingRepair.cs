using System.Collections.Generic;
using SM.Content;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Core.Contracts;
using UnityEditor;

namespace SM.Editor.SeedData;

public static partial class SampleSeedGenerator
{
    private static void PatchHealingRepairSkills(IReadOnlyDictionary<string, SkillDefinitionAsset> skills)
    {
        PatchHealingSkill(skills, "skill_minor_heal", TargetSelector.LowestHpPercentAlly);
        PatchHealingSkill(skills, "skill_shaman_utility", TargetSelector.LowestHpPercentAlly);
        PatchHealingSkill(skills, "skill_memory_tuning", TargetSelector.LowestCurrentHpAlly);

        if (!skills.TryGetValue("skill_hexer_utility", out var hexerUtility))
        {
            return;
        }

        // This skill's silence status, effect family, and recruit tags all describe an enemy debuff.
        // Keep its authored flat power unchanged; only remove the contradictory Heal semantics.
        hexerUtility.Kind = SkillKindValue.Debuff;
        hexerUtility.DamageType = DamageTypeValue.Magical;
        hexerUtility.TargetRule = SkillTargetRuleValue.NearestEnemy;
        hexerUtility.PhysCoeff = 0f;
        hexerUtility.HealCoeff = 0f;
        hexerUtility.TargetRuleData = new TargetRule
        {
            Domain = TargetDomain.EnemyUnit,
            PrimarySelector = TargetSelector.NearestReachableEnemy,
            FallbackPolicy = TargetFallbackPolicy.NearestReachableEnemy,
            Filters = TargetFilterFlags.InRange | TargetFilterFlags.ExcludeUntargetable,
            ReevaluateIntervalSeconds = 0.25f,
            MinimumCommitSeconds = 0.75f,
            MaxAcquireRange = 0f,
            PreferredMinTargets = 1,
            ClusterRadius = 2.5f,
            LockTargetAtCastStart = true,
            RetargetLockMode = RetargetLockMode.UntilCastComplete,
        };
        UpsertStringEntry(ContentLocalizationTables.Skills, hexerUtility.NameKey, "침묵의 저주", "Hex Silence");
        UpsertStringEntry(ContentLocalizationTables.Skills, hexerUtility.DescriptionKey, "적을 침묵시키는 저주입니다.", "Silences an enemy.");
        EditorUtility.SetDirty(hexerUtility);
    }

    private static void PatchHealingSkill(
        IReadOnlyDictionary<string, SkillDefinitionAsset> skills,
        string skillId,
        TargetSelector selector)
    {
        if (!skills.TryGetValue(skillId, out var skill))
        {
            return;
        }

        skill.Kind = SkillKindValue.Heal;
        skill.DamageType = DamageTypeValue.Healing;
        skill.TargetRule = SkillTargetRuleValue.LowestHpAlly;
        skill.PhysCoeff = 0f;
        skill.HealCoeff = 1f;
        skill.TargetRuleData = new TargetRule
        {
            Domain = TargetDomain.AlliedUnit,
            PrimarySelector = selector,
            FallbackPolicy = TargetFallbackPolicy.Self,
            Filters = TargetFilterFlags.InRange | TargetFilterFlags.ExcludeUntargetable | TargetFilterFlags.ExcludeFullHealthAllies,
            ReevaluateIntervalSeconds = 0.25f,
            MinimumCommitSeconds = 0.75f,
            MaxAcquireRange = skill.Range,
            PreferredMinTargets = 1,
            ClusterRadius = 2.5f,
            LockTargetAtCastStart = true,
            RetargetLockMode = RetargetLockMode.UntilCastComplete,
        };
        EditorUtility.SetDirty(skill);
    }
}
