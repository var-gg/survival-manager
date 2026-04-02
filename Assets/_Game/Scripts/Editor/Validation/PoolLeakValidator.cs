using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Meta.Model;

namespace SM.Editor.Validation;

/// <summary>
/// Verifies that recruit, flex-skill, augment, and affix pools in the combat
/// content snapshot do not expose any content outside the first-playable slice
/// boundary.
/// </summary>
internal static class PoolLeakValidator
{
    private const string SliceAssetPath = "Assets/Resources/_Game/Content/Definitions/FirstPlayable/first_playable_slice.asset";

    public static void Validate(
        CombatContentSnapshot snapshot,
        FirstPlayableSliceDefinition slice,
        ICollection<ContentValidationIssue> issues)
    {
        if (snapshot == null || slice == null)
        {
            return;
        }

        ValidateRecruitPoolLeak(snapshot, slice, issues);
        ValidateFlexPoolLeak(snapshot, slice, issues);
        ValidateAugmentPoolLeak(snapshot, slice, issues);
        ValidateAffixPoolLeak(snapshot, slice, issues);
    }

    /// <summary>
    /// Every recruitable archetype in the snapshot must appear in the slice's
    /// UnitBlueprintIds. A recruitable archetype outside the slice means the
    /// recruit pool can surface unapproved content.
    /// </summary>
    private static void ValidateRecruitPoolLeak(
        CombatContentSnapshot snapshot,
        FirstPlayableSliceDefinition slice,
        ICollection<ContentValidationIssue> issues)
    {
        var sliceUnits = new HashSet<string>(slice.UnitBlueprintIds, StringComparer.Ordinal);

        foreach (var (id, archetype) in snapshot.Archetypes)
        {
            if (archetype.IsRecruitable && !sliceUnits.Contains(id))
            {
                ContentValidationIssueFactory.AddError(
                    issues,
                    "pool_leak.recruit",
                    $"Recruitable archetype '{id}' is in snapshot but not in slice "
                    + "UnitBlueprintIds — recruit pool leak.",
                    SliceAssetPath,
                    $"Archetypes.{id}");
            }
        }
    }

    /// <summary>
    /// For each slice archetype, every skill in its flex active/passive pool
    /// must appear in the slice's FlexActiveIds / FlexPassiveIds respectively.
    /// </summary>
    private static void ValidateFlexPoolLeak(
        CombatContentSnapshot snapshot,
        FirstPlayableSliceDefinition slice,
        ICollection<ContentValidationIssue> issues)
    {
        var sliceFlexActives = new HashSet<string>(slice.FlexActiveIds, StringComparer.Ordinal);
        var sliceFlexPassives = new HashSet<string>(slice.FlexPassiveIds, StringComparer.Ordinal);

        foreach (var unitId in slice.UnitBlueprintIds)
        {
            if (!snapshot.Archetypes.TryGetValue(unitId, out var archetype))
            {
                continue;
            }

            CheckSkillPool(
                issues,
                archetype.RecruitFlexActivePool,
                sliceFlexActives,
                "pool_leak.flex_active",
                unitId,
                "RecruitFlexActivePool",
                "FlexActiveIds");

            CheckSkillPool(
                issues,
                archetype.RecruitFlexPassivePool,
                sliceFlexPassives,
                "pool_leak.flex_passive",
                unitId,
                "RecruitFlexPassivePool",
                "FlexPassiveIds");
        }
    }

    /// <summary>
    /// Every augment in the snapshot's AugmentCatalog must be listed in the
    /// slice's TemporaryAugmentIds or PermanentAugmentIds (based on IsPermanent).
    /// </summary>
    private static void ValidateAugmentPoolLeak(
        CombatContentSnapshot snapshot,
        FirstPlayableSliceDefinition slice,
        ICollection<ContentValidationIssue> issues)
    {
        var sliceTempAugments = new HashSet<string>(slice.TemporaryAugmentIds, StringComparer.Ordinal);
        var slicePermAugments = new HashSet<string>(slice.PermanentAugmentIds, StringComparer.Ordinal);

        foreach (var (id, entry) in snapshot.AugmentCatalog)
        {
            if (entry.IsPermanent)
            {
                if (!slicePermAugments.Contains(id))
                {
                    ContentValidationIssueFactory.AddError(
                        issues,
                        "pool_leak.permanent_augment",
                        $"Permanent augment '{id}' is in snapshot AugmentCatalog but not "
                        + "in slice PermanentAugmentIds — augment pool leak.",
                        SliceAssetPath,
                        $"AugmentCatalog.{id}");
                }
            }
            else
            {
                if (!sliceTempAugments.Contains(id))
                {
                    ContentValidationIssueFactory.AddError(
                        issues,
                        "pool_leak.temporary_augment",
                        $"Temporary augment '{id}' is in snapshot AugmentCatalog but not "
                        + "in slice TemporaryAugmentIds — augment pool leak.",
                        SliceAssetPath,
                        $"AugmentCatalog.{id}");
                }
            }
        }
    }

    /// <summary>
    /// Every affix in the snapshot's AffixPackages must be listed in the
    /// slice's AffixIds.
    /// </summary>
    private static void ValidateAffixPoolLeak(
        CombatContentSnapshot snapshot,
        FirstPlayableSliceDefinition slice,
        ICollection<ContentValidationIssue> issues)
    {
        var sliceAffixes = new HashSet<string>(slice.AffixIds, StringComparer.Ordinal);

        foreach (var id in snapshot.AffixPackages.Keys)
        {
            if (!sliceAffixes.Contains(id))
            {
                ContentValidationIssueFactory.AddError(
                    issues,
                    "pool_leak.affix",
                    $"Affix '{id}' is in snapshot AffixPackages but not in slice "
                    + "AffixIds — affix pool leak.",
                    SliceAssetPath,
                    $"AffixPackages.{id}");
            }
        }
    }

    private static void CheckSkillPool(
        ICollection<ContentValidationIssue> issues,
        IReadOnlyList<BattleSkillSpec>? pool,
        HashSet<string> sliceSkillIds,
        string code,
        string unitId,
        string poolName,
        string sliceFieldName)
    {
        if (pool == null)
        {
            return;
        }

        foreach (var skill in pool)
        {
            if (!string.IsNullOrWhiteSpace(skill.Id) && !sliceSkillIds.Contains(skill.Id))
            {
                ContentValidationIssueFactory.AddError(
                    issues,
                    code,
                    $"Archetype '{unitId}' {poolName} contains '{skill.Id}' which "
                    + $"is not in slice {sliceFieldName} — flex pool leak.",
                    SliceAssetPath,
                    $"Archetypes.{unitId}.{poolName}");
            }
        }
    }
}
