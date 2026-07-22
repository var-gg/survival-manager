using System.Collections.Generic;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Core.Contracts;
using UnityEditor;

namespace SM.Editor.SeedData;

public static partial class SampleSeedGenerator
{
    private static void PatchWrongSidePayloadSkills(IReadOnlyDictionary<string, SkillDefinitionAsset> skills)
    {
        PatchSelfTargetSkill(skills, "skill_warden_utility");
    }

    private static void PatchSelfTargetSkill(
        IReadOnlyDictionary<string, SkillDefinitionAsset> skills,
        string skillId)
    {
        if (!skills.TryGetValue(skillId, out var skill))
        {
            return;
        }

        skill.TargetRule = SkillTargetRuleValue.Self;
        skill.TargetRuleData = new TargetRule
        {
            Domain = TargetDomain.Self,
            PrimarySelector = TargetSelector.Self,
            FallbackPolicy = TargetFallbackPolicy.Self,
            Filters = TargetFilterFlags.None,
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
