using System;
using System.Linq;
using SM.Content.Definitions;
using UnityEditor;
using UnityEngine;

namespace SM.Editor.SeedData;

public static partial class SampleSeedGenerator
{
    private static void PatchStatusChannelMagnitudes()
    {
        // Fractional channels consume magnitude as a rate, not as an enabled/one-stack flag.
        PatchAppliedStatusMagnitude("skill_cinder_overrun", "slow", 0.20f);
        PatchAppliedStatusMagnitude("skill_fracture_step", "slow", 0.25f);
        PatchAppliedStatusMagnitude("skill_phase_tether", "slow", 0.15f);
        PatchTriggeredStatusMagnitude("skill_lattice_listener", "slow", 0.15f);

        PatchAppliedStatusMagnitude("skill_iron_pelt_maul", "wound", 0.25f);
        PatchAppliedStatusMagnitude("skill_shardblade_sever", "wound", 0.25f);

        PatchTriggeredStatusMagnitude("skill_glass_pathfinder", "marked", 0.20f);
        PatchAppliedStatusMagnitude("skill_prism_lance", "marked", 0.20f);
        PatchAppliedStatusMagnitude("skill_signal_flare", "marked", 0.20f);
        PatchSupportStatusMagnitude("support_hunter_mark", "marked", 0.20f);

        PatchAppliedStatusMagnitude("skill_iron_pelt_roar", "exposed", 0.30f);
        PatchAppliedStatusMagnitude("skill_mirror_cut", "exposed", 0.30f);
        PatchTriggeredStatusMagnitude("skill_shard_memory", "exposed", 0.30f);
        PatchAppliedStatusMagnitude("skill_signal_flare", "exposed", 0.30f);
    }

    private static void PatchAppliedStatusMagnitude(string skillId, string statusId, float magnitude)
    {
        PatchSkillStatusMagnitude(
            skillId,
            statusId,
            magnitude,
            "AppliedStatuses",
            static skill => skill.AppliedStatuses);
    }

    private static void PatchTriggeredStatusMagnitude(string skillId, string statusId, float magnitude)
    {
        var skill = LoadDefinition<SkillDefinitionAsset>($"{ResourcesRoot}/Skills/{skillId}.asset");
        if (skill == null)
        {
            Debug.LogWarning($"[SampleSeedGenerator] status magnitude patch skipped: skill '{skillId}' is missing.");
            return;
        }

        var effect = skill.TriggeredEffects.FirstOrDefault(candidate =>
            candidate.Op == SM.Core.Contracts.TriggeredEffectOp.ApplyStatus
            && string.Equals(candidate.StatusId, statusId, StringComparison.Ordinal));
        if (effect == null)
        {
            Debug.LogWarning(
                $"[SampleSeedGenerator] status magnitude patch skipped: skill '{skillId}' has no TriggeredEffects ApplyStatus entry for '{statusId}'.");
            return;
        }

        if (Mathf.Approximately(effect.Magnitude, magnitude))
        {
            return;
        }

        effect.Magnitude = magnitude;
        EditorUtility.SetDirty(skill);
    }

    private static void PatchSupportStatusMagnitude(string skillId, string statusId, float magnitude)
    {
        PatchSkillStatusMagnitude(
            skillId,
            statusId,
            magnitude,
            "SupportModifier.AddedStatuses",
            static skill => skill.SupportModifier.AddedStatuses);
    }

    private static void PatchSkillStatusMagnitude(
        string skillId,
        string statusId,
        float magnitude,
        string carrier,
        Func<SkillDefinitionAsset, System.Collections.Generic.IEnumerable<StatusApplicationRule>> selectRules)
    {
        var skill = LoadDefinition<SkillDefinitionAsset>($"{ResourcesRoot}/Skills/{skillId}.asset");
        if (skill == null)
        {
            Debug.LogWarning($"[SampleSeedGenerator] status magnitude patch skipped: skill '{skillId}' is missing.");
            return;
        }

        var rule = selectRules(skill).FirstOrDefault(candidate =>
            string.Equals(candidate.StatusId, statusId, StringComparison.Ordinal));
        if (rule == null)
        {
            Debug.LogWarning(
                $"[SampleSeedGenerator] status magnitude patch skipped: skill '{skillId}' has no {carrier} entry for '{statusId}'.");
            return;
        }

        if (Mathf.Approximately(rule.Magnitude, magnitude))
        {
            return;
        }

        rule.Magnitude = magnitude;
        EditorUtility.SetDirty(skill);
    }
}
