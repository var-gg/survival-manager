using System;
using System.Collections.Generic;
using System.Linq;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Unity;

namespace SM.Editor.Validation;

internal sealed class SkillCatalogHealthReportBuilder
{
    private readonly ILocalizationEntryLookup _localizationLookup;

    public SkillCatalogHealthReportBuilder(ILocalizationEntryLookup localizationLookup)
    {
        _localizationLookup = localizationLookup;
    }

    internal IReadOnlyList<SkillCatalogHealthEntry> Build(ValidationAssetCatalog catalog)
    {
        var context = new CatalogValidationContext(catalog);
        var liveIds = BuildLiveSkillIds(context.FirstPlayableSliceAsset);
        var parkingIds = BuildParkingIds(context.FirstPlayableSliceAsset);

        return context.Skills
            .OrderBy(skill => skill.Id, StringComparer.Ordinal)
            .Select(skill => BuildEntry(context, skill, liveIds, parkingIds))
            .ToList();
    }

    private SkillCatalogHealthEntry BuildEntry(
        CatalogValidationContext context,
        SkillDefinitionAsset skill,
        ISet<string> liveIds,
        ISet<string> parkingIds)
    {
        var compileTagsState = FormatTags(skill.CompileTags, emptyLabel: "empty");
        var supportState = BuildSupportCompatibilityState(skill);
        var weaponState = BuildRequiredWeaponTagsState(skill);
        var classState = BuildRequiredClassTagsState(skill);
        var localizationState = BuildLocalizationState(skill);
        var effectsState = skill.Effects == null || skill.Effects.Count == 0
            ? "empty"
            : $"ok:{skill.Effects.Count}";
        var exposure = liveIds.Contains(skill.Id)
            ? "Live"
            : parkingIds.Contains(skill.Id)
                ? "ParkingLot"
                : "Other";

        return new SkillCatalogHealthEntry(
            skill.Id,
            skill.SlotKind.ToString(),
            skill.Kind.ToString(),
            skill.Delivery.ToString(),
            skill.TargetRule.ToString(),
            skill.TemplateType.ToString(),
            exposure,
            effectsState,
            localizationState,
            compileTagsState,
            supportState,
            weaponState,
            classState,
            ResolveHealth(skill, effectsState, localizationState, compileTagsState, supportState, weaponState, classState),
            context.GetPath(skill));
    }

    private string BuildLocalizationState(SkillDefinitionAsset skill)
    {
        var states = new[]
        {
            ValidateLocalizationKey("name", skill.NameKey),
            ValidateLocalizationKey("desc", skill.DescriptionKey),
        };
        return states.All(state => state == "ok")
            ? "ok"
            : string.Join(";", states.Where(state => state != "ok"));
    }

    private string ValidateLocalizationKey(string fieldLabel, string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return $"{fieldLabel}:missing-key";
        }

        if (!LocalizationKeyPattern.IsValid(key))
        {
            return $"{fieldLabel}:invalid-key";
        }

        var collection = _localizationLookup.GetCollection(ContentLocalizationTables.Skills);
        if (collection == null)
        {
            return $"{fieldLabel}:missing-collection";
        }

        var missingLocales = ContentValidationPolicyCatalog.RequiredLocaleCodes
            .Where(locale => !collection.HasLocaleTable(locale) || !collection.HasEntry(locale, key))
            .ToList();
        return missingLocales.Count == 0
            ? "ok"
            : $"{fieldLabel}:missing-entry({string.Join(",", missingLocales)})";
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
        var ids = GetTagIds(tags);
        return FormatIds(ids, emptyLabel);
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

    private static string ResolveHealth(
        SkillDefinitionAsset skill,
        string effectsState,
        string localizationState,
        string compileTagsState,
        string supportState,
        string weaponState,
        string classState)
    {
        if (skill.TemplateType == SkillTemplateTypeValue.LegacyDerived
            || effectsState == "empty"
            || localizationState != "ok"
            || supportState is not ("ok" or "n/a")
            || weaponState.StartsWith("unsupported:", StringComparison.Ordinal)
            || classState is "missing-class-gate"
            || classState.StartsWith("unsupported:", StringComparison.Ordinal))
        {
            return "red";
        }

        return compileTagsState == "empty" ? "yellow" : "green";
    }

    private static ISet<string> BuildLiveSkillIds(FirstPlayableSliceDefinitionAsset? sliceAsset)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (sliceAsset == null)
        {
            return ids;
        }

        AddIds(ids, sliceAsset.SignatureActiveIds);
        AddIds(ids, sliceAsset.SignaturePassiveIds);
        AddIds(ids, sliceAsset.FlexActiveIds);
        AddIds(ids, sliceAsset.FlexPassiveIds);
        return ids;
    }

    private static ISet<string> BuildParkingIds(FirstPlayableSliceDefinitionAsset? sliceAsset)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (sliceAsset != null)
        {
            AddIds(ids, sliceAsset.ParkingLotContentIds);
        }

        return ids;
    }

    private static void AddIds(ISet<string> target, IEnumerable<string> ids)
    {
        foreach (var id in ids.Where(id => !string.IsNullOrWhiteSpace(id)))
        {
            target.Add(id);
        }
    }
}
