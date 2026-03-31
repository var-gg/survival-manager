using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using SM.Content.Definitions;
using SM.Core.Stats;
using SM.Editor.SeedData;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace SM.Editor.Validation;

public enum StatIdValidationStatus
{
    Unsupported = 0,
    LegacyAlias = 1,
    Canonical = 2,
}

public sealed record LaunchScopeThreshold
{
    public string Label { get; init; } = string.Empty;
    public int? ArchetypeCount { get; init; }
    public int? CoreArchetypeCount { get; init; }
    public int? SpecialistArchetypeCount { get; init; }
    public int? SkillCount { get; init; }
    public int? EquippableCount { get; init; }
    public int? AffixCount { get; init; }
    public int? PassiveBoardCount { get; init; }
    public int? PassiveNodeCount { get; init; }
    public int? TemporaryAugmentCount { get; init; }
    public int? PermanentAugmentCount { get; init; }
    public int? SynergyFamilyCount { get; init; }
}

public sealed record LaunchScopeCountReport
{
    public int ArchetypeCount { get; init; }
    public int CoreArchetypeCount { get; init; }
    public int SpecialistArchetypeCount { get; init; }
    public int SkillCount { get; init; }
    public int EquippableCount { get; init; }
    public int AffixCount { get; init; }
    public int PassiveBoardCount { get; init; }
    public int PassiveNodeCount { get; init; }
    public int TemporaryAugmentCount { get; init; }
    public int PermanentAugmentCount { get; init; }
    public int SynergyFamilyCount { get; init; }
    public int TeamTacticCount { get; init; }
    public int RoleInstructionCount { get; init; }
}

public static class ContentDefinitionValidator
{
    private const string ReportFolderName = "Logs/content-validation";
    private const string JsonReportFileName = "content-validation-report.json";
    private const string MarkdownSummaryFileName = "content-validation-summary.md";

    private static readonly string[] RequiredLocaleCodes = { "ko", "en" };
    private static readonly BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly Regex CanonicalIdPattern = new(
        "^[a-z0-9]+([._][a-z0-9]+)*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly HashSet<string> CanonicalClassIds = new(StringComparer.Ordinal) { "vanguard", "duelist", "ranger", "mystic" };
    private static readonly HashSet<string> AllowedRoleFamilyTags = new(StringComparer.Ordinal) { "vanguard", "striker", "ranger", "mystic" };
    private static readonly HashSet<string> AllowedRoleInstructionTags = new(StringComparer.Ordinal) { "anchor", "bruiser", "carry", "support", "frontline", "backline" };
    private static readonly HashSet<int> RequiredSynergyThresholds = new() { 2, 3, 4 };

    public static LaunchScopeThreshold CurrentMvpMinimum { get; } = new()
    {
        Label = "current MVP minimum",
        ArchetypeCount = 8,
        TemporaryAugmentCount = 9,
        SynergyFamilyCount = 7,
    };

    public static LaunchScopeThreshold PaidLaunchFloor { get; } = new()
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

    public static LaunchScopeThreshold PaidLaunchSafeTarget { get; } = new()
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

    public static StatIdValidationStatus GetStatIdStatus(string statId)
    {
        return StatKey.TryResolve(statId, out _, out var isLegacyAlias)
            ? isLegacyAlias ? StatIdValidationStatus.LegacyAlias : StatIdValidationStatus.Canonical
            : StatIdValidationStatus.Unsupported;
    }

    public static LaunchScopeCountReport BuildLaunchScopeCountReport()
    {
        return BuildLaunchScopeCountReport(LoadAllDefinitionAssets());
    }

    public static LaunchScopeCountReport BuildLaunchScopeCountReport(IEnumerable<ScriptableObject> assets)
    {
        return BuildLaunchScopeCountReportInternal(assets);
    }

    public static string GetDefaultReportDirectory()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", ReportFolderName));
    }

    [MenuItem("SM/Validation/Validate Content Definitions")]
    public static void Validate()
    {
        var report = ValidateAndWriteReport();
        LogReport(report);
        if (report.Issues.Any(issue => issue.Severity == ContentValidationSeverity.Error))
        {
            throw new Exception($"SM content validation failed with {report.Issues.Count(issue => issue.Severity == ContentValidationSeverity.Error)} issue(s).");
        }

        Debug.Log($"SM content validation passed. Report: {report.JsonReportPath}");
    }

    [MenuItem("SM/Validation/Write Content Validation Report")]
    public static void WriteReportMenu()
    {
        var report = WriteValidationReport(BuildValidationReport());
        LogReport(report);
        Debug.Log($"SM content validation report written. Json={report.JsonReportPath} Markdown={report.MarkdownSummaryPath}");
    }

    public static ContentValidationReport ValidateAndWriteReport()
    {
        return WriteValidationReport(BuildValidationReport());
    }

    public static ContentValidationReport BuildValidationReport()
    {
#if UNITY_EDITOR
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
#endif
        return BuildValidationReport(LoadAllDefinitionAssets());
    }

    public static ContentValidationReport BuildValidationReport(IEnumerable<ScriptableObject> assets)
    {
        var allAssets = assets
            .Where(asset => asset != null)
            .ToList();
        var ids = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var localizationKeys = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var passiveBoardsByClassId = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var issues = new List<ContentValidationIssue>();

        foreach (var asset in allAssets)
        {
            var assetPath = AssetDatabase.GetAssetPath(asset);
            ValidateLocalizationAsset(asset, assetPath, localizationKeys, issues);

            switch (asset)
            {
                case StatDefinition stat:
                    RegisterId(ids, nameof(StatDefinition), stat.Id, assetPath);
                    ValidateCanonicalId(stat.Id, assetPath, "StatDefinition.Id", issues);
                    break;
                case RaceDefinition race:
                    RegisterId(ids, nameof(RaceDefinition), race.Id, assetPath);
                    ValidateCanonicalId(race.Id, assetPath, "RaceDefinition.Id", issues);
                    break;
                case ClassDefinition @class:
                    RegisterId(ids, nameof(ClassDefinition), @class.Id, assetPath);
                    ValidateCanonicalId(@class.Id, assetPath, "ClassDefinition.Id", issues);
                    if (string.Equals(@class.Id, "striker", StringComparison.Ordinal))
                    {
                        AddError(issues, "glossary.class_id", "Class canonical id must remain 'duelist'; 'Striker' is player-facing only.", assetPath, "ClassDefinition.Id");
                    }
                    break;
                case TraitPoolDefinition traitPool:
                    RegisterId(ids, nameof(TraitPoolDefinition), traitPool.Id, assetPath);
                    ValidateCanonicalId(traitPool.Id, assetPath, "TraitPoolDefinition.Id", issues);
                    if (traitPool.PositiveTraits.Count < 3 || traitPool.NegativeTraits.Count < 3)
                    {
                        AddError(issues, "traitpool.structure", "Trait pool must keep the 3+3 positive/negative structure.", assetPath);
                    }
                    break;
                case UnitArchetypeDefinition archetype:
                    RegisterId(ids, nameof(UnitArchetypeDefinition), archetype.Id, assetPath);
                    ValidateCanonicalId(archetype.Id, assetPath, "UnitArchetypeDefinition.Id", issues);
                    ValidateArchetype(archetype, assetPath, issues);
                    break;
                case SkillDefinitionAsset skill:
                    RegisterId(ids, nameof(SkillDefinitionAsset), skill.Id, assetPath);
                    ValidateCanonicalId(skill.Id, assetPath, "SkillDefinitionAsset.Id", issues);
                    ValidateSkill(skill, assetPath, issues);
                    break;
                case AugmentDefinition augment:
                    RegisterId(ids, nameof(AugmentDefinition), augment.Id, assetPath);
                    ValidateCanonicalId(augment.Id, assetPath, "AugmentDefinition.Id", issues);
                    ValidateAugment(augment, assetPath, issues);
                    break;
                case ItemBaseDefinition item:
                    RegisterId(ids, nameof(ItemBaseDefinition), item.Id, assetPath);
                    ValidateCanonicalId(item.Id, assetPath, "ItemBaseDefinition.Id", issues);
                    ValidateItem(item, assetPath, issues);
                    break;
                case AffixDefinition affix:
                    RegisterId(ids, nameof(AffixDefinition), affix.Id, assetPath);
                    ValidateCanonicalId(affix.Id, assetPath, "AffixDefinition.Id", issues);
                    ValidateAffix(affix, assetPath, issues);
                    break;
                case StableTagDefinition tag:
                    RegisterId(ids, nameof(StableTagDefinition), tag.Id, assetPath);
                    ValidateCanonicalId(tag.Id, assetPath, "StableTagDefinition.Id", issues);
                    break;
                case TeamTacticDefinition teamTactic:
                    RegisterId(ids, nameof(TeamTacticDefinition), teamTactic.Id, assetPath);
                    ValidateCanonicalId(teamTactic.Id, assetPath, "TeamTacticDefinition.Id", issues);
                    break;
                case RoleInstructionDefinition roleInstruction:
                    RegisterId(ids, nameof(RoleInstructionDefinition), roleInstruction.Id, assetPath);
                    ValidateCanonicalId(roleInstruction.Id, assetPath, "RoleInstructionDefinition.Id", issues);
                    ValidateRoleInstruction(roleInstruction, assetPath, issues);
                    break;
                case PassiveBoardDefinition board:
                    RegisterId(ids, nameof(PassiveBoardDefinition), board.Id, assetPath);
                    ValidateCanonicalId(board.Id, assetPath, "PassiveBoardDefinition.Id", issues);
                    RegisterId(passiveBoardsByClassId, "PassiveBoardClass", board.ClassId, assetPath);
                    ValidatePassiveBoard(board, assetPath, issues);
                    break;
                case PassiveNodeDefinition passiveNode:
                    RegisterId(ids, nameof(PassiveNodeDefinition), passiveNode.Id, assetPath);
                    ValidateCanonicalId(passiveNode.Id, assetPath, "PassiveNodeDefinition.Id", issues);
                    ValidatePassiveNode(passiveNode, assetPath, issues);
                    break;
                case SynergyDefinition synergy:
                    RegisterId(ids, nameof(SynergyDefinition), synergy.Id, assetPath);
                    ValidateCanonicalId(synergy.Id, assetPath, "SynergyDefinition.Id", issues);
                    ValidateSynergy(synergy, assetPath, issues);
                    break;
                case SynergyTierDefinition tier:
                    RegisterId(ids, nameof(SynergyTierDefinition), tier.Id, assetPath);
                    ValidateCanonicalId(tier.Id, assetPath, "SynergyTierDefinition.Id", issues);
                    ValidateSynergyTier(tier, assetPath, issues);
                    break;
                case ExpeditionDefinition expedition:
                    RegisterId(ids, nameof(ExpeditionDefinition), expedition.Id, assetPath);
                    ValidateCanonicalId(expedition.Id, assetPath, "ExpeditionDefinition.Id", issues);
                    if (expedition.Nodes.Any(node => node.RewardTable == null))
                    {
                        AddError(issues, "expedition.reward_table", "Expedition node is missing a reward table reference.", assetPath);
                    }
                    break;
                case RewardTableDefinition rewardTable:
                    RegisterId(ids, nameof(RewardTableDefinition), rewardTable.Id, assetPath);
                    ValidateCanonicalId(rewardTable.Id, assetPath, "RewardTableDefinition.Id", issues);
                    break;
            }
        }

        foreach (var pair in ids.Where(x => !string.IsNullOrWhiteSpace(x.Key) && x.Value.Count > 1))
        {
            AddError(issues, "id.duplicate", $"Duplicate id '{pair.Key}': {string.Join(", ", pair.Value)}", string.Join(", ", pair.Value));
        }

        foreach (var pair in passiveBoardsByClassId.Where(x => !string.IsNullOrWhiteSpace(x.Key) && x.Value.Count > 1))
        {
            AddError(issues, "passive_board.duplicate_class", $"Duplicate passive board class '{pair.Key}': {string.Join(", ", pair.Value)}", string.Join(", ", pair.Value));
        }

        foreach (var pair in localizationKeys.Where(x => x.Value.Count > 1))
        {
            AddError(issues, "localization.duplicate_key", $"Duplicate localization key '{pair.Key}': {string.Join(", ", pair.Value)}", string.Join(", ", pair.Value));
        }

        var passiveBoardReports = BuildPassiveBoardReports(allAssets);
        foreach (var boardReport in passiveBoardReports)
        {
            ValidatePassiveBoardShape(boardReport, issues);
        }

        var launchScope = BuildLaunchScopeCountReportInternal(allAssets);
        var floorGaps = BuildLaunchScopeGapList(launchScope, PaidLaunchFloor);
        foreach (var gap in floorGaps)
        {
            AddError(issues, "launch_floor.gap", $"Paid launch floor requirement not met: {gap}", ReportFolderName);
        }

        var safeTargetGaps = BuildLaunchScopeGapList(launchScope, PaidLaunchSafeTarget);
        foreach (var gap in BuildLaunchScopeGapList(launchScope, CurrentMvpMinimum))
        {
            AddError(issues, "launch_scope.mvp_gap", $"Current MVP minimum requirement not met: {gap}", ReportFolderName);
        }

        return new ContentValidationReport
        {
            ValidationPhase = ContentLocalizationPolicy.CurrentPhase,
            LaunchScope = launchScope,
            FloorGaps = floorGaps,
            SafeTargetGaps = safeTargetGaps,
            PassiveBoards = passiveBoardReports,
            Issues = issues
                .OrderByDescending(issue => issue.Severity)
                .ThenBy(issue => issue.Code, StringComparer.Ordinal)
                .ThenBy(issue => issue.AssetPath, StringComparer.Ordinal)
                .ToList(),
        };
    }

    public static ContentValidationReport WriteValidationReport(ContentValidationReport report)
    {
        var reportDirectory = GetDefaultReportDirectory();
        Directory.CreateDirectory(reportDirectory);
        var jsonPath = Path.Combine(reportDirectory, JsonReportFileName);
        var markdownPath = Path.Combine(reportDirectory, MarkdownSummaryFileName);
        var withPaths = report with
        {
            JsonReportPath = jsonPath,
            MarkdownSummaryPath = markdownPath,
        };

        File.WriteAllText(jsonPath, JsonConvert.SerializeObject(withPaths, Formatting.Indented));
        File.WriteAllText(markdownPath, BuildMarkdownSummary(withPaths));
        return withPaths;
    }

    private static void ValidateLocalizationAsset(
        ScriptableObject asset,
        string assetPath,
        Dictionary<string, List<string>> localizationKeys,
        ICollection<ContentValidationIssue> issues)
    {
        ValidateLocalizationObject(asset, assetPath, asset.GetType().Name, localizationKeys, issues);

        switch (asset)
        {
            case TraitPoolDefinition traitPool:
                ValidateNestedObjects(traitPool.PositiveTraits, assetPath, "PositiveTraits", localizationKeys, issues);
                ValidateNestedObjects(traitPool.NegativeTraits, assetPath, "NegativeTraits", localizationKeys, issues);
                break;
            case RewardTableDefinition rewardTable:
                ValidateNestedObjects(rewardTable.Rewards, assetPath, "Rewards", localizationKeys, issues);
                break;
            case ExpeditionDefinition expedition:
                ValidateNestedObjects(expedition.Nodes, assetPath, "Nodes", localizationKeys, issues);
                break;
            case SynergyDefinition synergy:
                ValidateNestedObjects(synergy.Tiers, assetPath, "Tiers", localizationKeys, issues);
                break;
        }
    }

    private static void ValidateNestedObjects(
        IEnumerable objects,
        string assetPath,
        string scope,
        Dictionary<string, List<string>> localizationKeys,
        ICollection<ContentValidationIssue> issues)
    {
        var index = 0;
        foreach (var item in objects)
        {
            if (item != null && item is not ScriptableObject)
            {
                ValidateLocalizationObject(item, assetPath, $"{scope}[{index}]", localizationKeys, issues);
            }

            index++;
        }
    }

    private static void ValidateLocalizationObject(
        object target,
        string assetPath,
        string scope,
        Dictionary<string, List<string>> localizationKeys,
        ICollection<ContentValidationIssue> issues)
    {
        var type = target.GetType();
        var tableName = ContentLocalizationTables.GetTableName(type);

        foreach (var fieldName in EnumerateLocalizationFieldNames(type))
        {
            var key = type.GetField(fieldName, InstanceFlags)?.GetValue(target) as string ?? string.Empty;
            if (string.IsNullOrWhiteSpace(key))
            {
                AddError(issues, "localization.missing_key", $"Missing localization key '{fieldName}' on {scope}.", assetPath, $"{scope}.{fieldName}");
                continue;
            }

            if (!LocalizationKeyPattern.IsValid(key))
            {
                AddError(issues, "localization.invalid_key", $"Invalid localization key '{key}' on {scope}.", assetPath, $"{scope}.{fieldName}");
                continue;
            }

            if (ShouldValidateTableEntries(tableName))
            {
                RegisterLocalizationKey(localizationKeys, tableName, key, $"{assetPath} ({scope}.{fieldName})");
                ValidateTableEntries(tableName, key, assetPath, scope, fieldName, issues);
            }
        }

        ValidateLegacyText(target, assetPath, scope, issues);
    }

    private static IEnumerable<string> EnumerateLocalizationFieldNames(Type type)
    {
        if (type.GetField("NameKey", InstanceFlags) != null)
        {
            yield return "NameKey";
        }

        if (type.GetField("DescriptionKey", InstanceFlags) != null)
        {
            yield return "DescriptionKey";
        }

        if (type.GetField("LabelKey", InstanceFlags) != null)
        {
            yield return "LabelKey";
        }

        if (type.GetField("RewardSummaryKey", InstanceFlags) != null)
        {
            yield return "RewardSummaryKey";
        }
    }

    private static void ValidateLegacyText(object target, string assetPath, string scope, ICollection<ContentValidationIssue> issues)
    {
        foreach (var (propertyName, fieldName) in new[]
                 {
                     ("LegacyDisplayName", "legacyDisplayName"),
                     ("LegacyDescription", "legacyDescription"),
                     ("LegacyLabel", "legacyLabel"),
                 })
        {
            var property = target.GetType().GetProperty(propertyName, InstanceFlags);
            var value = property?.PropertyType == typeof(string)
                ? property.GetValue(target) as string
                : null;

            if (string.IsNullOrWhiteSpace(value))
            {
                var field = target.GetType().GetField(fieldName, InstanceFlags);
                if (field?.FieldType == typeof(string))
                {
                    value = field.GetValue(target) as string;
                }
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (ContentLocalizationPolicy.TreatsLegacyTextAsError)
            {
                AddError(issues, "localization.legacy_text", $"Legacy localized prose remains in {scope}.{propertyName}.", assetPath, $"{scope}.{propertyName}");
            }
            else
            {
                AddWarning(issues, "localization.legacy_text", $"Legacy localized prose remains in {scope}.{propertyName}.", assetPath, $"{scope}.{propertyName}");
            }
        }
    }

    private static bool ShouldValidateTableEntries(string tableName)
    {
        return !string.IsNullOrWhiteSpace(tableName) && !string.Equals(tableName, ContentLocalizationTables.SystemMessages, StringComparison.Ordinal);
    }

    private static void RegisterLocalizationKey(Dictionary<string, List<string>> keys, string tableName, string key, string owner)
    {
        var composite = $"{tableName}:{key}";
        if (!keys.TryGetValue(composite, out var owners))
        {
            owners = new List<string>();
            keys[composite] = owners;
        }

        owners.Add(owner);
    }

    private static void ValidateTableEntries(
        string tableName,
        string key,
        string assetPath,
        string scope,
        string fieldName,
        ICollection<ContentValidationIssue> issues)
    {
        var collection = LocalizationEditorSettings.GetStringTableCollection(tableName);
        if (collection == null)
        {
            AddError(issues, "localization.missing_collection", $"Missing string table collection '{tableName}' for key '{key}'.", assetPath, $"{scope}.{fieldName}");
            return;
        }

        foreach (var localeCode in RequiredLocaleCodes)
        {
            var table = collection.GetTable(new LocaleIdentifier(localeCode)) as StringTable;
            if (table == null)
            {
                AddError(issues, "localization.missing_locale_table", $"Missing locale table '{tableName}/{localeCode}' for key '{key}'.", assetPath, $"{scope}.{fieldName}");
                continue;
            }

            if (table.GetEntry(key) == null)
            {
                var severity = ContentLocalizationPolicy.TreatsMissingLocalizationAsError
                    ? ContentValidationSeverity.Error
                    : ContentValidationSeverity.Warning;
                AddIssue(issues, severity, "localization.missing_entry", $"Missing localized entry '{tableName}/{localeCode}:{key}'.", assetPath, $"{scope}.{fieldName}");
            }
        }
    }

    private static void RegisterId(Dictionary<string, List<string>> ids, string kind, string id, string path)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        var composite = $"{kind}:{id}";
        if (!ids.TryGetValue(composite, out var list))
        {
            list = new List<string>();
            ids[composite] = list;
        }

        list.Add(path);
    }

    private static void ValidateCanonicalId(string id, string assetPath, string scope, ICollection<ContentValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            AddError(issues, "id.missing", $"Missing canonical id on {scope}.", assetPath, scope);
            return;
        }

        if (!CanonicalIdPattern.IsMatch(id))
        {
            AddError(issues, "id.invalid_pattern", $"Canonical id '{id}' must stay lower-case with '.' or '_' separators.", assetPath, scope);
        }
    }

    private static void ValidateModifiers(
        ICollection<ContentValidationIssue> issues,
        IEnumerable<SerializableStatModifier> modifiers,
        string path,
        string scope)
    {
        foreach (var modifier in modifiers)
        {
            switch (GetStatIdStatus(modifier.StatId))
            {
                case StatIdValidationStatus.Unsupported:
                    AddError(issues, "stat.unsupported", $"Unsupported stat id '{modifier.StatId}'.", path, scope);
                    break;
                case StatIdValidationStatus.LegacyAlias:
                    AddWarning(issues, "stat.legacy_alias", $"Legacy stat alias '{modifier.StatId}' should migrate to canonical id.", path, scope);
                    break;
            }
        }
    }

    private static List<ScriptableObject> LoadAllDefinitionAssets()
    {
        var results = new List<ScriptableObject>();
        var seenPaths = new HashSet<string>(StringComparer.Ordinal);
        var root = SampleSeedGenerator.ResourcesRoot.Replace('\\', '/');

        AddLoadedAssets(
            results,
            seenPaths,
            Resources.LoadAll<ScriptableObject>("_Game/Content/Definitions")
                .Where(asset => asset != null)
                .Select(asset => (asset, AssetDatabase.GetAssetPath(asset))));

#if UNITY_EDITOR
        if (AssetDatabase.IsValidFolder(root))
        {
            AddLoadedAssets(
                results,
                seenPaths,
                AssetDatabase.FindAssets(string.Empty, new[] { root })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(path => path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                    .Select(path => (AssetDatabase.LoadMainAssetAtPath(path) as ScriptableObject, path)));
        }

        if (results.Count == 0)
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var diskRoot = Path.Combine(projectRoot, root.Replace('/', Path.DirectorySeparatorChar));
            if (Directory.Exists(diskRoot))
            {
                AddLoadedAssets(
                    results,
                    seenPaths,
                    Directory.EnumerateFiles(diskRoot, "*.asset", SearchOption.AllDirectories)
                        .Select(path => path.Replace('\\', '/'))
                        .Select(path => path.StartsWith(projectRoot.Replace('\\', '/') + "/", StringComparison.Ordinal)
                            ? path.Substring(projectRoot.Replace('\\', '/').Length + 1)
                            : path)
                        .Select(path => (AssetDatabase.LoadMainAssetAtPath(path) as ScriptableObject, path)));
            }
        }
#endif

        return results;
    }

    private static void AddLoadedAssets(
        ICollection<ScriptableObject> destination,
        ISet<string> seenPaths,
        IEnumerable<(ScriptableObject? Asset, string Path)> candidates)
    {
        foreach (var (asset, path) in candidates)
        {
            if (asset == null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(path))
            {
                if (!seenPaths.Add(path))
                {
                    continue;
                }
            }

            destination.Add(asset);
        }
    }

    private static LaunchScopeCountReport BuildLaunchScopeCountReportInternal(IEnumerable<ScriptableObject> assets)
    {
        var assetList = assets.ToList();
        var archetypes = assetList.OfType<UnitArchetypeDefinition>().ToList();
        var augments = assetList.OfType<AugmentDefinition>().ToList();

        return new LaunchScopeCountReport
        {
            ArchetypeCount = archetypes.Count,
            CoreArchetypeCount = archetypes.Count(archetype => archetype.ScopeKind == ArchetypeScopeValue.Core),
            SpecialistArchetypeCount = archetypes.Count(archetype => archetype.ScopeKind == ArchetypeScopeValue.Specialist),
            SkillCount = assetList.OfType<SkillDefinitionAsset>().Count(),
            EquippableCount = assetList.OfType<ItemBaseDefinition>().Count(),
            AffixCount = assetList.OfType<AffixDefinition>().Count(),
            PassiveBoardCount = assetList.OfType<PassiveBoardDefinition>().Count(),
            PassiveNodeCount = assetList.OfType<PassiveNodeDefinition>().Count(),
            TemporaryAugmentCount = augments.Count(augment => !augment.IsPermanent),
            PermanentAugmentCount = augments.Count(augment => augment.IsPermanent),
            SynergyFamilyCount = assetList.OfType<SynergyDefinition>().Count(),
            TeamTacticCount = assetList.OfType<TeamTacticDefinition>().Count(),
            RoleInstructionCount = assetList.OfType<RoleInstructionDefinition>().Count(),
        };
    }

    private static List<string> BuildLaunchScopeGapList(LaunchScopeCountReport report, LaunchScopeThreshold threshold)
    {
        var gaps = new List<string>();
        AddThresholdGap(gaps, "archetypes", report.ArchetypeCount, threshold.ArchetypeCount);
        AddThresholdGap(gaps, "coreArchetypes", report.CoreArchetypeCount, threshold.CoreArchetypeCount);
        AddThresholdGap(gaps, "specialists", report.SpecialistArchetypeCount, threshold.SpecialistArchetypeCount);
        AddThresholdGap(gaps, "skills", report.SkillCount, threshold.SkillCount);
        AddThresholdGap(gaps, "equippables", report.EquippableCount, threshold.EquippableCount);
        AddThresholdGap(gaps, "affixes", report.AffixCount, threshold.AffixCount);
        AddThresholdGap(gaps, "passiveBoards", report.PassiveBoardCount, threshold.PassiveBoardCount);
        AddThresholdGap(gaps, "passiveNodes", report.PassiveNodeCount, threshold.PassiveNodeCount);
        AddThresholdGap(gaps, "tempAugments", report.TemporaryAugmentCount, threshold.TemporaryAugmentCount);
        AddThresholdGap(gaps, "permAugments", report.PermanentAugmentCount, threshold.PermanentAugmentCount);
        AddThresholdGap(gaps, "synergyFamilies", report.SynergyFamilyCount, threshold.SynergyFamilyCount);
        return gaps;
    }

    private static void AddThresholdGap(ICollection<string> gaps, string label, int current, int? target)
    {
        if (!target.HasValue || current >= target.Value)
        {
            return;
        }

        gaps.Add($"{label} {current}/{target.Value}");
    }

    private static IReadOnlyList<PassiveBoardShapeReport> BuildPassiveBoardReports(IEnumerable<ScriptableObject> assets)
    {
        return assets
            .OfType<PassiveBoardDefinition>()
            .OrderBy(board => board.ClassId, StringComparer.Ordinal)
            .Select(board => new PassiveBoardShapeReport
            {
                BoardId = board.Id,
                ClassId = board.ClassId,
                SmallCount = board.Nodes.Count(node => node != null && node.NodeKind == PassiveNodeKindValue.Small),
                NotableCount = board.Nodes.Count(node => node != null && node.NodeKind == PassiveNodeKindValue.Notable),
                KeystoneCount = board.Nodes.Count(node => node != null && node.NodeKind == PassiveNodeKindValue.Keystone),
            })
            .ToList();
    }

    private static string BuildMarkdownSummary(ContentValidationReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Content Validation Summary");
        builder.AppendLine();
        builder.AppendLine($"- generatedAtUtc: `{report.GeneratedAtUtc}`");
        builder.AppendLine($"- validationPhase: `{report.ValidationPhase}`");
        builder.AppendLine($"- errors: `{report.Issues.Count(issue => issue.Severity == ContentValidationSeverity.Error)}`");
        builder.AppendLine($"- warnings: `{report.Issues.Count(issue => issue.Severity == ContentValidationSeverity.Warning)}`");
        builder.AppendLine();
        builder.AppendLine("## Launch Scope");
        builder.AppendLine();
        builder.AppendLine($"- archetypes: `{report.LaunchScope.ArchetypeCount}` (core `{report.LaunchScope.CoreArchetypeCount}`, specialist `{report.LaunchScope.SpecialistArchetypeCount}`)");
        builder.AppendLine($"- skills: `{report.LaunchScope.SkillCount}`");
        builder.AppendLine($"- equippables: `{report.LaunchScope.EquippableCount}`");
        builder.AppendLine($"- affixes: `{report.LaunchScope.AffixCount}`");
        builder.AppendLine($"- passiveBoards: `{report.LaunchScope.PassiveBoardCount}`");
        builder.AppendLine($"- passiveNodes: `{report.LaunchScope.PassiveNodeCount}`");
        builder.AppendLine($"- tempAugments: `{report.LaunchScope.TemporaryAugmentCount}`");
        builder.AppendLine($"- permAugments: `{report.LaunchScope.PermanentAugmentCount}`");
        builder.AppendLine($"- synergyFamilies: `{report.LaunchScope.SynergyFamilyCount}`");
        builder.AppendLine($"- teamTactics: `{report.LaunchScope.TeamTacticCount}`");
        builder.AppendLine($"- roleInstructions: `{report.LaunchScope.RoleInstructionCount}`");
        builder.AppendLine();
        builder.AppendLine("## Floor Gaps");
        builder.AppendLine();
        if (report.FloorGaps.Count == 0)
        {
            builder.AppendLine("- none");
        }
        else
        {
            foreach (var gap in report.FloorGaps)
            {
                builder.AppendLine($"- {gap}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Safe Target Gaps");
        builder.AppendLine();
        if (report.SafeTargetGaps.Count == 0)
        {
            builder.AppendLine("- none");
        }
        else
        {
            foreach (var gap in report.SafeTargetGaps)
            {
                builder.AppendLine($"- {gap}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Passive Boards");
        builder.AppendLine();
        foreach (var board in report.PassiveBoards)
        {
            builder.AppendLine($"- `{board.ClassId}`: small `{board.SmallCount}`, notable `{board.NotableCount}`, keystone `{board.KeystoneCount}`");
        }

        builder.AppendLine();
        builder.AppendLine("## Issues");
        builder.AppendLine();
        if (report.Issues.Count == 0)
        {
            builder.AppendLine("- none");
        }
        else
        {
            foreach (var issue in report.Issues)
            {
                builder.AppendLine($"- `{issue.Severity}` `{issue.Code}` {issue.Message} ({issue.AssetPath}{(string.IsNullOrWhiteSpace(issue.Scope) ? string.Empty : $" / {issue.Scope}")})");
            }
        }

        return builder.ToString();
    }

    private static void LogReport(ContentValidationReport report)
    {
        foreach (var issue in report.Issues)
        {
            if (issue.Severity == ContentValidationSeverity.Error)
            {
                Debug.LogError($"{issue.Code}: {issue.Message} [{issue.AssetPath}]");
                continue;
            }

            if (issue.Severity == ContentValidationSeverity.Warning)
            {
                Debug.LogWarning($"{issue.Code}: {issue.Message} [{issue.AssetPath}]");
            }
        }
    }

    private static void ValidateArchetype(UnitArchetypeDefinition archetype, string assetPath, ICollection<ContentValidationIssue> issues)
    {
        if (archetype.Race == null || archetype.Class == null || archetype.TraitPool == null)
        {
            AddError(issues, "archetype.references", "Archetype is missing race/class/trait pool references.", assetPath);
        }

        if (archetype.TacticPreset == null || archetype.TacticPreset.Count == 0)
        {
            AddError(issues, "archetype.tactic_preset", "Archetype is missing a tactic preset.", assetPath);
        }

        ValidateDefinedEnum(archetype.ScopeKind, "Archetype scope", assetPath, issues);
        if (string.IsNullOrWhiteSpace(archetype.RoleFamilyTag))
        {
            AddError(issues, "archetype.role_family", "Archetype is missing RoleFamilyTag.", assetPath);
        }
        else if (!AllowedRoleFamilyTags.Contains(archetype.RoleFamilyTag))
        {
            AddError(issues, "archetype.role_family", $"Archetype role family tag must be one of [{string.Join(", ", AllowedRoleFamilyTags)}].", assetPath);
        }

        if (string.Equals(archetype.Class?.Id, "duelist", StringComparison.Ordinal)
            && !string.Equals(archetype.RoleFamilyTag, "striker", StringComparison.Ordinal))
        {
            AddError(issues, "glossary.duelist_role_family", "Duelist archetypes must expose the player-facing role family tag 'striker'.", assetPath);
        }

        if (string.IsNullOrWhiteSpace(archetype.PrimaryWeaponFamilyTag))
        {
            AddError(issues, "archetype.weapon_family", "Archetype is missing PrimaryWeaponFamilyTag.", assetPath);
        }

        if (archetype.Skills.Count != 4)
        {
            AddError(issues, "archetype.skill_contract", "Archetype must carry exactly 4 compiled skills (core_active, utility_active, passive, support).", assetPath);
        }
        else
        {
            var slotKinds = archetype.Skills
                .Where(skill => skill != null)
                .Select(skill => skill.SlotKind)
                .ToList();
            var hasExpectedSlots = slotKinds.Contains(SkillSlotKindValue.CoreActive)
                                   && slotKinds.Contains(SkillSlotKindValue.UtilityActive)
                                   && slotKinds.Contains(SkillSlotKindValue.Passive)
                                   && slotKinds.Contains(SkillSlotKindValue.Support);
            if (!hasExpectedSlots)
            {
                AddError(issues, "archetype.skill_slots", "Archetype skill loadout must include all 4 canonical slot kinds.", assetPath);
            }
        }

        ValidateStableTags(issues, archetype.SupportModifierBiasTags, assetPath, "Archetype support modifier bias");
    }

    private static void ValidateSkill(SkillDefinitionAsset skill, string assetPath, ICollection<ContentValidationIssue> issues)
    {
        ValidateDefinedEnum(skill.Kind, "Skill kind", assetPath, issues);
        ValidateDefinedEnum(skill.SlotKind, "Skill slot kind", assetPath, issues);
        ValidateDefinedEnum(skill.DamageType, "Skill damage type", assetPath, issues);
        ValidateDefinedEnum(skill.Delivery, "Skill delivery", assetPath, issues);
        ValidateDefinedEnum(skill.TargetRule, "Skill target rule", assetPath, issues);
        ValidateStableTags(issues, skill.CompileTags, assetPath, "Skill compile");
        ValidateStableTags(issues, skill.RuleModifierTags, assetPath, "Skill rule modifier");
        ValidateStableTags(issues, skill.SupportAllowedTags, assetPath, "Skill support allowed");
        ValidateStableTags(issues, skill.RequiredWeaponTags, assetPath, "Skill required weapon");
        ValidateStableTags(issues, skill.RequiredClassTags, assetPath, "Skill required class");
    }

    private static void ValidateAugment(AugmentDefinition augment, string assetPath, ICollection<ContentValidationIssue> issues)
    {
        ValidateModifiers(issues, augment.Modifiers, assetPath, "AugmentDefinition.Modifiers");
        if (string.IsNullOrWhiteSpace(augment.FamilyId))
        {
            AddError(issues, "augment.family_id", "Augment is missing FamilyId.", assetPath);
        }

        if (augment.RuleModifierTags.Any(tag => tag == null || string.IsNullOrWhiteSpace(tag.Id)))
        {
            AddError(issues, "augment.rule_tag", "Augment has an empty rule modifier tag.", assetPath);
        }

        if (augment.MutualExclusionTags.Select(tag => tag == null ? string.Empty : tag.Id).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().Count()
            != augment.MutualExclusionTags.Count(tag => tag != null && !string.IsNullOrWhiteSpace(tag.Id)))
        {
            AddError(issues, "augment.mutual_exclusion", "Augment has duplicate mutual exclusion tags.", assetPath);
        }
    }

    private static void ValidateItem(ItemBaseDefinition item, string assetPath, ICollection<ContentValidationIssue> issues)
    {
        ValidateDefinedEnum(item.SlotType, "Item slot type", assetPath, issues);
        ValidateDefinedEnum(item.IdentityKind, "Item identity kind", assetPath, issues);
        ValidateModifiers(issues, item.BaseModifiers, assetPath, "ItemBaseDefinition.BaseModifiers");
        ValidateStableTags(issues, item.CompileTags, assetPath, "Item compile");
        ValidateStableTags(issues, item.RuleModifierTags, assetPath, "Item rule modifier");
        ValidateStableTags(issues, item.AllowedClassTags, assetPath, "Item allowed class");
        ValidateStableTags(issues, item.AllowedArchetypeTags, assetPath, "Item allowed archetype");
        ValidateStableTags(issues, item.UniqueRuleTags, assetPath, "Item unique rule");
        if (item.IdentityKind == ItemIdentityValue.Unique
            && item.GrantedSkills.Count == 0
            && item.RuleModifierTags.Count == 0
            && item.UniqueRuleTags.Count == 0)
        {
            AddError(issues, "item.unique_payload", "Unique item must define granted skill or rule payload.", assetPath);
        }
    }

    private static void ValidateAffix(AffixDefinition affix, string assetPath, ICollection<ContentValidationIssue> issues)
    {
        ValidateDefinedEnum(affix.Category, "Affix category", assetPath, issues);
        ValidateModifiers(issues, affix.Modifiers, assetPath, "AffixDefinition.Modifiers");
        ValidateStableTags(issues, affix.CompileTags, assetPath, "Affix compile");
        ValidateStableTags(issues, affix.RuleModifierTags, assetPath, "Affix rule modifier");
    }

    private static void ValidateRoleInstruction(RoleInstructionDefinition roleInstruction, string assetPath, ICollection<ContentValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(roleInstruction.RoleTag))
        {
            AddError(issues, "role_instruction.role_tag", "Role instruction is missing RoleTag.", assetPath);
            return;
        }

        if (!AllowedRoleInstructionTags.Contains(roleInstruction.RoleTag))
        {
            AddError(issues, "role_instruction.role_tag", $"Role instruction tag must be one of [{string.Join(", ", AllowedRoleInstructionTags)}].", assetPath);
        }
    }

    private static void ValidatePassiveBoard(PassiveBoardDefinition board, string assetPath, ICollection<ContentValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(board.ClassId))
        {
            AddError(issues, "passive_board.class_id", "Passive board is missing ClassId.", assetPath);
        }
        else if (!CanonicalClassIds.Contains(board.ClassId))
        {
            AddError(issues, "passive_board.class_id", $"Passive board class id must be one of [{string.Join(", ", CanonicalClassIds)}].", assetPath);
        }

        if (board.Nodes.Any(node => node == null))
        {
            AddError(issues, "passive_board.node_ref", "Passive board has a missing node reference.", assetPath);
        }
    }

    private static void ValidatePassiveNode(PassiveNodeDefinition passiveNode, string assetPath, ICollection<ContentValidationIssue> issues)
    {
        ValidateDefinedEnum(passiveNode.NodeKind, "Passive node kind", assetPath, issues);
        ValidateModifiers(issues, passiveNode.Modifiers, assetPath, "PassiveNodeDefinition.Modifiers");
        ValidateStableTags(issues, passiveNode.CompileTags, assetPath, "Passive node compile");
        ValidateStableTags(issues, passiveNode.RuleModifierTags, assetPath, "Passive node rule modifier");
        ValidateStableTags(issues, passiveNode.MutualExclusionTags, assetPath, "Passive node mutual exclusion");
        if (string.IsNullOrWhiteSpace(passiveNode.BoardId))
        {
            AddError(issues, "passive_node.board_id", "Passive node is missing BoardId.", assetPath);
        }
        if (passiveNode.BoardDepth < 0)
        {
            AddError(issues, "passive_node.board_depth", "Passive node BoardDepth cannot be negative.", assetPath);
        }
        if (passiveNode.PrerequisiteNodeIds.Any(string.IsNullOrWhiteSpace))
        {
            AddError(issues, "passive_node.prerequisite", "Passive node has an empty prerequisite id.", assetPath);
        }
    }

    private static void ValidateSynergy(SynergyDefinition synergy, string assetPath, ICollection<ContentValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(synergy.CountedTagId))
        {
            AddError(issues, "synergy.counted_tag", "Synergy is missing CountedTagId.", assetPath);
        }

        if (synergy.Tiers.Any(tier => tier == null))
        {
            AddError(issues, "synergy.tier_ref", "Synergy has a missing tier reference.", assetPath);
            return;
        }

        var thresholds = synergy.Tiers
            .Where(tier => tier != null)
            .Select(tier => tier.Threshold)
            .OrderBy(value => value)
            .ToList();
        if (!RequiredSynergyThresholds.SetEquals(thresholds))
        {
            AddError(issues, "synergy.thresholds", "Synergy must define exact 2/3/4 breakpoint tiers.", assetPath);
        }
    }

    private static void ValidateSynergyTier(SynergyTierDefinition tier, string assetPath, ICollection<ContentValidationIssue> issues)
    {
        ValidateModifiers(issues, tier.Modifiers, assetPath, "SynergyTierDefinition.Modifiers");
        if (!RequiredSynergyThresholds.Contains(tier.Threshold))
        {
            AddError(issues, "synergy_tier.threshold", "Synergy tier threshold must be one of 2, 3, or 4.", assetPath);
        }
    }

    private static void ValidatePassiveBoardShape(PassiveBoardShapeReport board, ICollection<ContentValidationIssue> issues)
    {
        if (board.SmallCount != 12 || board.NotableCount != 5 || board.KeystoneCount != 1)
        {
            AddError(
                issues,
                "passive_board.shape",
                $"Passive board '{board.BoardId}' must match the paid launch floor shape 12/5/1. Found {board.SmallCount}/{board.NotableCount}/{board.KeystoneCount}.",
                board.BoardId,
                board.ClassId);
        }
    }

    private static void ValidateDefinedEnum<TEnum>(TEnum value, string label, string assetPath, ICollection<ContentValidationIssue> issues)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(typeof(TEnum), value))
        {
            AddError(issues, "enum.undefined", $"{label} is not a defined enum value.", assetPath);
        }
    }

    private static void ValidateStableTags(
        ICollection<ContentValidationIssue> issues,
        IEnumerable<StableTagDefinition> tags,
        string assetPath,
        string scope)
    {
        if (tags == null)
        {
            return;
        }

        var ids = new List<string>();
        foreach (var tag in tags)
        {
            if (tag == null || string.IsNullOrWhiteSpace(tag.Id))
            {
                AddError(issues, "tag.missing_id", $"{scope} tag is missing id.", assetPath);
                continue;
            }

            ids.Add(tag.Id);
        }

        if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Count)
        {
            AddError(issues, "tag.duplicate", $"{scope} tags contain duplicates.", assetPath);
        }
    }

    private static void AddError(ICollection<ContentValidationIssue> issues, string code, string message, string assetPath, string scope = "")
    {
        AddIssue(issues, ContentValidationSeverity.Error, code, message, assetPath, scope);
    }

    private static void AddWarning(ICollection<ContentValidationIssue> issues, string code, string message, string assetPath, string scope = "")
    {
        AddIssue(issues, ContentValidationSeverity.Warning, code, message, assetPath, scope);
    }

    private static void AddIssue(
        ICollection<ContentValidationIssue> issues,
        ContentValidationSeverity severity,
        string code,
        string message,
        string assetPath,
        string scope = "")
    {
        issues.Add(new ContentValidationIssue(severity, code, message, assetPath, scope));
    }
}
