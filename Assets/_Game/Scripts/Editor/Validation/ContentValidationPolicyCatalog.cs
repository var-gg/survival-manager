using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using SM.Core.Stats;

namespace SM.Editor.Validation;

internal sealed record ClassRoleFamilyMapping(string CanonicalClassId, string PlayerFacingRoleFamily);

internal static class ContentValidationPolicyCatalog
{
    internal const string ReportFolderName = "Logs/content-validation";
    internal const string JsonReportFileName = "content-validation-report.json";
    internal const string MarkdownSummaryFileName = "content-validation-summary.md";
    internal const string BudgetAuditJsonFileName = "content_budget_audit.json";
    internal const string BudgetAuditMarkdownFileName = "content_budget_audit.md";
    internal const string CounterCoverageMatrixMarkdownFileName = "counter_coverage_matrix.md";
    internal const string ForbiddenFeatureReportMarkdownFileName = "v1_forbidden_feature_report.md";

    internal static readonly string[] RequiredLocaleCodes = { "ko", "en" };
    internal static readonly BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    internal static readonly Regex CanonicalIdPattern = new(
        "^[a-z0-9]+([._][a-z0-9]+)*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    internal static readonly IReadOnlyDictionary<string, ClassRoleFamilyMapping> ClassRoleFamilyMappings =
        new Dictionary<string, ClassRoleFamilyMapping>(StringComparer.Ordinal)
        {
            ["vanguard"] = new("vanguard", "vanguard"),
            ["duelist"] = new("duelist", "striker"),
            ["ranger"] = new("ranger", "ranger"),
            ["mystic"] = new("mystic", "mystic"),
        };

    private static readonly IReadOnlyDictionary<string, ClassRoleFamilyMapping> RoleFamilyMappingsByPlayerFacingTag =
        ClassRoleFamilyMappings.Values.ToDictionary(mapping => mapping.PlayerFacingRoleFamily, StringComparer.Ordinal);

    internal static readonly HashSet<string> CanonicalClassIds =
        ClassRoleFamilyMappings.Keys.ToHashSet(StringComparer.Ordinal);

    internal static readonly HashSet<string> AllowedRoleFamilyTags =
        ClassRoleFamilyMappings.Values
            .Select(mapping => mapping.PlayerFacingRoleFamily)
            .ToHashSet(StringComparer.Ordinal);

    internal static readonly HashSet<string> AllowedWeaponFamilyIds = new(StringComparer.Ordinal)
    {
        "shield",
        "blade",
        "bow",
        "focus",
        "greatblade",
        "polearm",
    };

    internal static readonly HashSet<string> AllowedCraftCurrencyIds = new(StringComparer.Ordinal)
    {
        "gold",
        "ember_dust",
        "echo_crystal",
        "boss_sigil",
    };

    internal static readonly HashSet<string> RequiredLaunchStatusIds = new(StringComparer.Ordinal)
    {
        "stun", "root", "silence", "slow",
        "burn", "bleed", "wound", "sunder",
        "marked", "exposed",
        "barrier", "guarded", "unstoppable",
    };

    internal static readonly HashSet<string> RequiredCleanseProfileIds = new(StringComparer.Ordinal)
    {
        "cleanse_basic",
        "cleanse_control",
        "break_and_unstoppable",
    };

    internal static readonly HashSet<string> RequiredTraitTokenIds = new(StringComparer.Ordinal)
    {
        "trait_reroll_token",
        "trait_lock_token",
        "trait_purge_token",
    };

    internal static readonly HashSet<string> AllowedRoleInstructionTags = new(StringComparer.Ordinal)
    {
        "anchor",
        "bruiser",
        "carry",
        "support",
        "frontline",
        "backline",
    };

    internal static readonly HashSet<int> AllowedSynergyTierThresholds = new() { 2, 3, 4 };

    internal static LaunchScopeThreshold CurrentMvpMinimum { get; } = new()
    {
        Label = "current MVP minimum",
        ArchetypeCount = 8,
        TemporaryAugmentCount = 9,
        SynergyFamilyCount = 7,
    };

    internal static LaunchScopeThreshold PaidLaunchFloor { get; } = new()
    {
        Label = "paid launch floor",
        ArchetypeCount = 12,
        CoreArchetypeCount = 12,
        SkillCount = 40,
        EquippableCount = 36,
        AffixCount = 24,
        PassiveBoardCount = 4,
        PassiveNodeCount = 72,
        TemporaryAugmentCount = 18,
        PermanentAugmentCount = 9,
        SynergyFamilyCount = 7,
    };

    internal static LaunchScopeThreshold PaidLaunchSafeTarget { get; } = new()
    {
        Label = "paid launch safe target",
        ArchetypeCount = 16,
        CoreArchetypeCount = 12,
        SpecialistArchetypeCount = 4,
        SkillCount = 40,
        EquippableCount = 42,
        AffixCount = 30,
        PassiveBoardCount = 4,
        PassiveNodeCount = 96,
        TemporaryAugmentCount = 24,
        PermanentAugmentCount = 12,
        SynergyFamilyCount = 7,
    };

    internal static bool TryGetRoleFamilyMapping(string canonicalClassId, out ClassRoleFamilyMapping mapping)
    {
        return ClassRoleFamilyMappings.TryGetValue(canonicalClassId, out mapping!);
    }

    internal static bool TryGetRoleFamilyForCanonicalClassId(string canonicalClassId, out string roleFamilyTag)
    {
        if (TryGetRoleFamilyMapping(canonicalClassId, out var mapping))
        {
            roleFamilyTag = mapping.PlayerFacingRoleFamily;
            return true;
        }

        roleFamilyTag = string.Empty;
        return false;
    }

    internal static bool TryGetCanonicalClassIdForRoleFamily(string roleFamilyTag, out string canonicalClassId)
    {
        if (RoleFamilyMappingsByPlayerFacingTag.TryGetValue(roleFamilyTag, out var mapping))
        {
            canonicalClassId = mapping.CanonicalClassId;
            return true;
        }

        canonicalClassId = string.Empty;
        return false;
    }

    internal static StatIdValidationStatus GetStatIdStatus(string statId)
    {
        return StatKey.TryResolve(statId, out _, out var isLegacyAlias)
            ? isLegacyAlias ? StatIdValidationStatus.LegacyAlias : StatIdValidationStatus.Canonical
            : StatIdValidationStatus.Unsupported;
    }
}
