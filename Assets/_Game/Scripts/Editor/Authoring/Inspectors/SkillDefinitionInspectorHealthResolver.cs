using System;
using System.Collections.Generic;
using System.Linq;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Editor.Validation;
using SM.Unity;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace SM.Editor.Authoring.Inspectors;

internal enum SkillInspectorHealthLevel
{
    Green,
    Yellow,
    Red,
}

internal sealed record SkillInspectorHealthFinding(
    SkillInspectorHealthLevel Level,
    string Label,
    string Detail);

internal sealed record SkillDefinitionInspectorHealthSnapshot(
    SkillInspectorHealthLevel Health,
    string LocalizedName,
    string NameFallbackState,
    string IconState,
    string IconAssetPath,
    Texture2D? IconTexture,
    string SliceExposure,
    string EffectsState,
    string LocalizationState,
    string CompileTagsState,
    string SupportCompatibilityState,
    string RequiredWeaponTagsState,
    string RequiredClassTagsState,
    IReadOnlyList<SkillInspectorHealthFinding> Findings);

internal static class SkillDefinitionInspectorHealthResolver
{
    private const string SkillIconAssetRoot = "Assets/Resources/_Game/Art/Icons/Skill";
    private const string FirstPlayableSlicePath = "Assets/Resources/_Game/Content/Definitions/FirstPlayable/first_playable_slice.asset";

    public static SkillDefinitionInspectorHealthSnapshot Resolve(SkillDefinitionAsset skill)
    {
        var findings = new List<SkillInspectorHealthFinding>();
        var iconTexture = ResolveIconTexture(skill, out var iconState, out var iconPath);
        var localizationState = BuildLocalizationState(skill, out var localizedName, out var nameFallbackState);
        var effectsState = skill.Effects == null || skill.Effects.Count == 0
            ? "empty"
            : $"ok:{skill.Effects.Count}";
        var compileTagsState = FormatTags(skill.CompileTags, "empty");
        var supportState = BuildSupportCompatibilityState(skill);
        var weaponState = BuildRequiredWeaponTagsState(skill);
        var classState = BuildRequiredClassTagsState(skill);
        var sliceExposure = ResolveSliceExposure(skill.Id);

        AddFindingIf(findings, skill.TemplateType == SkillTemplateTypeValue.LegacyDerived, SkillInspectorHealthLevel.Red, "Template", "TemplateType is still LegacyDerived.");
        AddFindingIf(findings, effectsState == "empty", SkillInspectorHealthLevel.Red, "Effects", "Effects list is empty.");
        AddFindingIf(findings, string.IsNullOrWhiteSpace(skill.IconId), SkillInspectorHealthLevel.Red, "Icon", "IconId is missing.");
        AddFindingIf(findings, !string.IsNullOrWhiteSpace(skill.IconId) && iconTexture == null, SkillInspectorHealthLevel.Yellow, "Icon", $"IconId '{skill.IconId}' did not resolve to a texture.");
        AddFindingIf(findings, localizationState != "ok", SkillInspectorHealthLevel.Red, "Localization", localizationState);
        AddFindingIf(findings, compileTagsState == "empty", SkillInspectorHealthLevel.Yellow, "Tags", "CompileTags is empty.");
        AddFindingIf(findings, supportState is not ("ok" or "n/a"), SkillInspectorHealthLevel.Red, "Support compatibility", supportState);
        AddFindingIf(findings, weaponState.StartsWith("unsupported:", StringComparison.Ordinal), SkillInspectorHealthLevel.Red, "Required weapon tags", weaponState);
        AddFindingIf(findings, classState is "missing-class-gate" || classState.StartsWith("unsupported:", StringComparison.Ordinal), SkillInspectorHealthLevel.Red, "Required class tags", classState);
        AddFindingIf(findings, sliceExposure == "Unknown", SkillInspectorHealthLevel.Yellow, "Slice", "first_playable_slice.asset is not available.");

        var health = findings.Any(finding => finding.Level == SkillInspectorHealthLevel.Red)
            ? SkillInspectorHealthLevel.Red
            : findings.Any(finding => finding.Level == SkillInspectorHealthLevel.Yellow)
                ? SkillInspectorHealthLevel.Yellow
                : SkillInspectorHealthLevel.Green;

        return new SkillDefinitionInspectorHealthSnapshot(
            health,
            localizedName,
            nameFallbackState,
            iconState,
            iconPath,
            iconTexture,
            sliceExposure,
            effectsState,
            localizationState,
            compileTagsState,
            supportState,
            weaponState,
            classState,
            findings);
    }

    private static void AddFindingIf(
        ICollection<SkillInspectorHealthFinding> findings,
        bool condition,
        SkillInspectorHealthLevel level,
        string label,
        string detail)
    {
        if (condition)
        {
            findings.Add(new SkillInspectorHealthFinding(level, label, detail));
        }
    }

    private static Texture2D? ResolveIconTexture(SkillDefinitionAsset skill, out string state, out string assetPath)
    {
        assetPath = string.Empty;
        if (string.IsNullOrWhiteSpace(skill.IconId))
        {
            state = "missing-id";
            return null;
        }

        foreach (var iconId in EnumerateIconCandidates(skill).Distinct(StringComparer.Ordinal))
        {
            var path = $"{SkillIconAssetRoot}/{iconId}.png";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                continue;
            }

            state = $"ok:{iconId}";
            assetPath = path;
            return texture;
        }

        state = $"missing-texture:{skill.IconId}";
        return null;
    }

    private static IEnumerable<string> EnumerateIconCandidates(SkillDefinitionAsset skill)
    {
        yield return skill.IconId;

        if (!string.IsNullOrWhiteSpace(skill.Id))
        {
            yield return $"skill_icon_{StripPrefix(skill.Id, "skill_")}";
        }
    }

    private static string BuildLocalizationState(
        SkillDefinitionAsset skill,
        out string localizedName,
        out string nameFallbackState)
    {
        var fallback = !string.IsNullOrWhiteSpace(skill.LegacyDisplayName)
            ? skill.LegacyDisplayName
            : skill.Id;
        localizedName = EditorLocalizedTextResolver.Localize(ContentLocalizationTables.Skills, skill.NameKey, fallback);
        nameFallbackState = string.Equals(localizedName, fallback, StringComparison.Ordinal)
            ? "fallback"
            : "localized";

        var states = new[]
        {
            ValidateLocalizationKey("name", skill.NameKey),
            ValidateLocalizationKey("desc", skill.DescriptionKey),
        };
        return states.All(state => state == "ok")
            ? "ok"
            : string.Join(";", states.Where(state => state != "ok"));
    }

    private static string ValidateLocalizationKey(string fieldLabel, string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return $"{fieldLabel}:missing-key";
        }

        if (!LocalizationKeyPattern.IsValid(key))
        {
            return $"{fieldLabel}:invalid-key";
        }

        var collection = LocalizationEditorSettings.GetStringTableCollection(ContentLocalizationTables.Skills);
        if (collection == null)
        {
            return $"{fieldLabel}:missing-collection";
        }

        var missingLocales = ContentValidationPolicyCatalog.RequiredLocaleCodes
            .Where(locale => !HasLocalizationEntry(collection, locale, key))
            .ToList();
        return missingLocales.Count == 0
            ? "ok"
            : $"{fieldLabel}:missing-entry({string.Join(",", missingLocales)})";
    }

    private static bool HasLocalizationEntry(LocalizationTableCollection collection, string localeCode, string key)
    {
        if (collection.GetTable(new LocaleIdentifier(localeCode)) is not StringTable table)
        {
            return false;
        }

        return table.GetEntry(key) is { } entry && !string.IsNullOrWhiteSpace(entry.Value);
    }

    private static string ResolveSliceExposure(string skillId)
    {
        var slice = AssetDatabase.LoadAssetAtPath<FirstPlayableSliceDefinitionAsset>(FirstPlayableSlicePath);
        if (slice == null)
        {
            return "Unknown";
        }

        if (ContainsId(slice.SignatureActiveIds, skillId)
            || ContainsId(slice.SignaturePassiveIds, skillId)
            || ContainsId(slice.FlexActiveIds, skillId)
            || ContainsId(slice.FlexPassiveIds, skillId))
        {
            return "Live";
        }

        return ContainsId(slice.ParkingLotContentIds, skillId)
            ? "ParkingLot"
            : "Other";
    }

    private static bool ContainsId(IEnumerable<string> ids, string value)
    {
        return ids.Any(id => string.Equals(id, value, StringComparison.Ordinal));
    }

    private static string BuildSupportCompatibilityState(SkillDefinitionAsset skill)
    {
        if (!skill.Id.StartsWith("support_", StringComparison.Ordinal)
            && skill.SlotKind != SkillSlotKindValue.Support)
        {
            return "n/a";
        }

        var allowed = GetTagIds(skill.SupportAllowedTags);
        var blocked = GetTagIds(skill.SupportBlockedTags);
        var requiredWeapons = GetTagIds(skill.RequiredWeaponTags);
        var requiredClasses = GetTagIds(skill.RequiredClassTags);
        var problems = new List<string>();
        if (skill.SlotKind != SkillSlotKindValue.Support)
        {
            problems.Add("non-support-slot");
        }

        if (allowed.Count == 0)
        {
            problems.Add("missing-allowed-tags");
        }

        if (allowed.Overlaps(blocked))
        {
            problems.Add("allowed-blocked-overlap");
        }

        var isGlobalSupport = FirstPlayableAuthoringContract.GlobalSupportModifierIds.Contains(skill.Id);
        var hasGateAnchor = requiredWeapons.Count > 0 || requiredClasses.Count > 0;
        if (!isGlobalSupport && !hasGateAnchor)
        {
            problems.Add("missing-gate-anchor");
        }

        if (isGlobalSupport && hasGateAnchor)
        {
            problems.Add("global-with-gate-anchor");
        }

        return problems.Count == 0 ? "ok" : string.Join(",", problems);
    }

    private static string BuildRequiredWeaponTagsState(SkillDefinitionAsset skill)
    {
        var ids = GetTagIds(skill.RequiredWeaponTags);
        var unsupported = ids
            .Where(id => !ContentValidationPolicyCatalog.AllowedWeaponFamilyIds.Contains(id))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
        return unsupported.Count == 0
            ? FormatIds(ids, "none")
            : $"unsupported:{string.Join(",", unsupported)}";
    }

    private static string BuildRequiredClassTagsState(SkillDefinitionAsset skill)
    {
        var ids = GetTagIds(skill.RequiredClassTags);
        var unsupported = ids
            .Where(id => !ContentValidationPolicyCatalog.CanonicalClassIds.Contains(id))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
        if (unsupported.Count > 0)
        {
            return $"unsupported:{string.Join(",", unsupported)}";
        }

        if (!skill.Id.StartsWith("support_", StringComparison.Ordinal)
            && skill.SlotKind is not SkillSlotKindValue.Support
            && ids.Count == 0)
        {
            return "missing-class-gate";
        }

        return FormatIds(ids, "none");
    }

    private static string FormatTags(IEnumerable<StableTagDefinition> tags, string emptyLabel)
    {
        return FormatIds(GetTagIds(tags), emptyLabel);
    }

    private static string FormatIds(IReadOnlyCollection<string> ids, string emptyLabel)
    {
        return ids.Count == 0
            ? emptyLabel
            : string.Join(",", ids.OrderBy(id => id, StringComparer.Ordinal));
    }

    private static HashSet<string> GetTagIds(IEnumerable<StableTagDefinition> tags)
    {
        return tags
            .Where(tag => tag != null && !string.IsNullOrWhiteSpace(tag.Id))
            .Select(tag => tag.Id)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string StripPrefix(string value, string prefix)
    {
        return value.StartsWith(prefix, StringComparison.Ordinal)
            ? value[prefix.Length..]
            : value;
    }
}
