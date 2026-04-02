using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Meta.Model;

namespace SM.Editor.Validation;

/// <summary>
/// Validates exact id-set equality between the first-playable slice and the
/// combat content snapshot for all six core content kinds:
/// archetypes, synergy families, affixes, temporary augments, permanent augments,
/// and passive boards.
/// </summary>
internal static class FirstPlayableClosureValidator
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

        ValidateContentKind(
            issues,
            ContentKind.UnitBlueprint,
            slice.UnitBlueprintIds,
            snapshot.Archetypes.Keys,
            slice.UnitBlueprintCap);

        ValidateSynergyFamilies(snapshot, slice, issues);

        ValidateContentKind(
            issues,
            ContentKind.Affix,
            slice.AffixIds,
            snapshot.AffixPackages.Keys,
            slice.AffixCap);

        ValidateAugments(snapshot, slice, issues);

        ValidatePassiveBoards(slice, issues);
    }

    private static void ValidateContentKind(
        ICollection<ContentValidationIssue> issues,
        ContentKind kind,
        IReadOnlyList<string> sliceIds,
        IEnumerable<string> snapshotKeys,
        int cap)
    {
        var kindLabel = kind.ToString();
        var codePrefix = KindToCodePrefix(kind);
        var snapshotSet = new HashSet<string>(snapshotKeys, StringComparer.Ordinal);

        // Duplicates
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in sliceIds)
        {
            if (!seen.Add(id))
            {
                ContentValidationIssueFactory.AddError(
                    issues,
                    $"closure.{codePrefix}.duplicate",
                    $"{kindLabel} '{id}' is duplicated in the slice.",
                    SliceAssetPath,
                    $"{kindLabel}Ids");
            }
        }

        // Cap check
        if (cap > 0 && sliceIds.Count > cap)
        {
            ContentValidationIssueFactory.AddError(
                issues,
                $"closure.{codePrefix}.over_cap",
                $"{kindLabel} count {sliceIds.Count} exceeds cap {cap}.",
                SliceAssetPath,
                $"{kindLabel}Cap");
        }

        // Dangling: declared in slice but absent from snapshot
        foreach (var id in sliceIds)
        {
            if (!snapshotSet.Contains(id))
            {
                ContentValidationIssueFactory.AddError(
                    issues,
                    $"closure.{codePrefix}.dangling",
                    $"{kindLabel} '{id}' is declared in slice but not found in snapshot.",
                    SliceAssetPath,
                    $"{kindLabel}Ids.{id}");
            }
        }

        // Missing: present in snapshot but absent from slice
        var sliceSet = new HashSet<string>(sliceIds, StringComparer.Ordinal);
        foreach (var id in snapshotSet)
        {
            if (!sliceSet.Contains(id))
            {
                ContentValidationIssueFactory.AddWarning(
                    issues,
                    $"closure.{codePrefix}.unlisted",
                    $"{kindLabel} '{id}' exists in snapshot but is not declared in slice.",
                    SliceAssetPath,
                    $"{kindLabel}Ids");
            }
        }
    }

    private static void ValidateSynergyFamilies(
        CombatContentSnapshot snapshot,
        FirstPlayableSliceDefinition slice,
        ICollection<ContentValidationIssue> issues)
    {
        // SynergyCatalog contains tier-level entries ("human:2", "human:4").
        // Extract unique family ids by grouping on SynergyId.
        var snapshotFamilyIds = snapshot.SynergyCatalog.Values
            .Select(tier => tier.Rule.SynergyId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal);

        ValidateContentKind(
            issues,
            ContentKind.SynergyFamily,
            slice.SynergyFamilyIds,
            snapshotFamilyIds,
            slice.SynergyFamilyCap);
    }

    private static void ValidateAugments(
        CombatContentSnapshot snapshot,
        FirstPlayableSliceDefinition slice,
        ICollection<ContentValidationIssue> issues)
    {
        var tempKeys = snapshot.AugmentCatalog.Values
            .Where(entry => !entry.IsPermanent)
            .Select(entry => entry.Id);

        var permKeys = snapshot.AugmentCatalog.Values
            .Where(entry => entry.IsPermanent)
            .Select(entry => entry.Id);

        ValidateContentKind(
            issues,
            ContentKind.TemporaryAugment,
            slice.TemporaryAugmentIds,
            tempKeys,
            slice.TemporaryAugmentCap);

        ValidateContentKind(
            issues,
            ContentKind.PermanentAugment,
            slice.PermanentAugmentIds,
            permKeys,
            slice.PermanentAugmentCap);
    }

    private static void ValidatePassiveBoards(
        FirstPlayableSliceDefinition slice,
        ICollection<ContentValidationIssue> issues)
    {
        // PassiveBoards have no board-level collection in CombatContentSnapshot.
        // Validate duplicates and cap only.
        var codePrefix = KindToCodePrefix(ContentKind.PassiveBoard);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in slice.PassiveBoardIds)
        {
            if (!seen.Add(id))
            {
                ContentValidationIssueFactory.AddError(
                    issues,
                    $"closure.{codePrefix}.duplicate",
                    $"PassiveBoard '{id}' is duplicated in the slice.",
                    SliceAssetPath,
                    "PassiveBoardIds");
            }
        }

        if (slice.PassiveBoardCap > 0 && slice.PassiveBoardIds.Count > slice.PassiveBoardCap)
        {
            ContentValidationIssueFactory.AddError(
                issues,
                $"closure.{codePrefix}.over_cap",
                $"PassiveBoard count {slice.PassiveBoardIds.Count} exceeds cap {slice.PassiveBoardCap}.",
                SliceAssetPath,
                "PassiveBoardCap");
        }
    }

    private static string KindToCodePrefix(ContentKind kind)
    {
        return kind switch
        {
            ContentKind.UnitBlueprint => "unit_blueprint",
            ContentKind.SynergyFamily => "synergy_family",
            ContentKind.Affix => "affix",
            ContentKind.TemporaryAugment => "temporary_augment",
            ContentKind.PermanentAugment => "permanent_augment",
            ContentKind.PassiveBoard => "passive_board",
            _ => kind.ToString().ToLowerInvariant(),
        };
    }
}
