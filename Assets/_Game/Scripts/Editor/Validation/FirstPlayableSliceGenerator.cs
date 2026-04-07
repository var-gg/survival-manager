using System;
using System.IO;
using System.Linq;
using System.Text;
using SM.Combat.Model;
using SM.Meta.Model;
using SM.Unity;
using UnityEditor;
using UnityEngine;

namespace SM.Editor.Validation;

public static class FirstPlayableSliceGenerator
{
    public const string AssetPath = "Assets/Resources/_Game/Content/Definitions/FirstPlayable/first_playable_slice.asset";
    private const string ArtifactFolderName = "Logs/loop-d-balance";
    private const string SliceMarkdownFileName = "first_playable_slice.md";

    [MenuItem("SM/Validation/Generate First Playable Slice")]
    public static void GenerateMenu()
    {
        var result = GenerateAndWriteArtifacts();
        Debug.Log($"[LoopD] First playable slice generated. asset={AssetPath} markdown={result.MarkdownPath}");
    }

    public static FirstPlayableSliceGenerationResult GenerateAndWriteArtifacts()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

        var lookup = new RuntimeCombatContentLookup(allowEditorRecoveryFallback: true);
        if (!lookup.TryGetCombatSnapshot(out var snapshot, out var error))
        {
            throw new InvalidOperationException(error);
        }

        var slice = snapshot.FirstPlayableSlice ?? new FirstPlayableSliceDefinition();
        var asset = LoadOrCreateAsset();
        CopySlice(slice, asset);
        EnsureDefaultCoverageQuotas(asset);
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

        var reportDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ArtifactFolderName));
        Directory.CreateDirectory(reportDirectory);
        var markdownPath = Path.Combine(reportDirectory, SliceMarkdownFileName);
        File.WriteAllText(markdownPath, BuildMarkdown(slice), Encoding.UTF8);

        return new FirstPlayableSliceGenerationResult(asset, slice, markdownPath);
    }

    private static FirstPlayableSliceDefinitionAsset LoadOrCreateAsset()
    {
        var asset = AssetDatabase.LoadAssetAtPath<FirstPlayableSliceDefinitionAsset>(AssetPath);
        if (asset != null)
        {
            return asset;
        }

        var directory = Path.GetDirectoryName(AssetPath)?.Replace('\\', '/');
        if (!string.IsNullOrWhiteSpace(directory) && !AssetDatabase.IsValidFolder(directory))
        {
            Directory.CreateDirectory(Path.GetFullPath(directory));
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        }

        asset = ScriptableObject.CreateInstance<FirstPlayableSliceDefinitionAsset>();
        AssetDatabase.CreateAsset(asset, AssetPath);
        return asset;
    }

    private static void CopySlice(FirstPlayableSliceDefinition source, FirstPlayableSliceDefinitionAsset target)
    {
        target.UnitBlueprintCap = source.UnitBlueprintCap;
        target.SignatureActiveCap = source.SignatureActiveCap;
        target.SignaturePassiveCap = source.SignaturePassiveCap;
        target.FlexActiveCap = source.FlexActiveCap;
        target.FlexPassiveCap = source.FlexPassiveCap;
        target.AffixCap = source.AffixCap;
        target.SynergyFamilyCap = source.SynergyFamilyCap;
        target.TemporaryAugmentCap = source.TemporaryAugmentCap;
        target.PermanentAugmentCap = source.PermanentAugmentCap;
        target.PassiveBoardCap = source.PassiveBoardCap;
        target.RequireAllThreatPatternsCovered = source.RequireAllThreatPatternsCovered;
        target.RequireAllCounterToolsCovered = source.RequireAllCounterToolsCovered;
        target.UnitBlueprintIds = source.UnitBlueprintIds.ToList();
        target.SignatureActiveIds = source.SignatureActiveIds.ToList();
        target.SignaturePassiveIds = source.SignaturePassiveIds.ToList();
        target.FlexActiveIds = source.FlexActiveIds.ToList();
        target.FlexPassiveIds = source.FlexPassiveIds.ToList();
        target.AffixIds = source.AffixIds.ToList();
        target.SynergyFamilyIds = source.SynergyFamilyIds.ToList();
        target.TemporaryAugmentIds = source.TemporaryAugmentIds.ToList();
        target.PermanentAugmentIds = source.PermanentAugmentIds.ToList();
        target.PassiveBoardIds = source.PassiveBoardIds.ToList();
        target.ParkingLotContentIds = source.ParkingLotContentIds.ToList();
        target.SynergyGrammar = source.SynergyGrammar
            .Select(e => new SynergyGrammarEntry
            {
                FamilyId = e.FamilyId,
                FamilyType = e.FamilyType,
                MinorThreshold = e.MinorThreshold,
                MajorThreshold = e.MajorThreshold,
            })
            .ToList();
        target.ClassLabelMappings = source.ClassLabelMappings
            .Select(m => new ClassLabelMapping
            {
                CanonicalId = m.CanonicalId,
                PlayerFacingLabel = m.PlayerFacingLabel,
            })
            .ToList();
        target.CoverageQuotas = source.CoverageQuotas
            .Select(quota => new SliceCoverageQuota
            {
                Kind = quota.Kind,
                MinimumCount = quota.MinimumCount,
            })
            .ToList();
    }

    private static void EnsureDefaultCoverageQuotas(FirstPlayableSliceDefinitionAsset asset)
    {
        if (asset.CoverageQuotas.Count > 0)
        {
            return;
        }

        asset.CoverageQuotas = new[]
        {
            MakeQuota(SliceCoverageQuotaKind.FrontlineAnchor, 2),
            MakeQuota(SliceCoverageQuotaKind.MeleePressure, 2),
            MakeQuota(SliceCoverageQuotaKind.BacklineCarry, 2),
            MakeQuota(SliceCoverageQuotaKind.MagicSource, 2),
            MakeQuota(SliceCoverageQuotaKind.SupportSource, 2),
            MakeQuota(SliceCoverageQuotaKind.DiveSource, 1),
            MakeQuota(SliceCoverageQuotaKind.SummonSource, 1),
            MakeQuota(SliceCoverageQuotaKind.AntiSwarmSource, 1),
            MakeQuota(SliceCoverageQuotaKind.AntiSustainSource, 1),
            MakeQuota(SliceCoverageQuotaKind.AntiControlSource, 1),
        }.ToList();
    }

    private static SliceCoverageQuota MakeQuota(SliceCoverageQuotaKind kind, int minimumCount)
    {
        return new SliceCoverageQuota
        {
            Kind = kind,
            MinimumCount = minimumCount,
        };
    }

    private static string BuildMarkdown(FirstPlayableSliceDefinition slice)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# First Playable Slice");
        builder.AppendLine();
        builder.AppendLine($"- generatedAtUtc: `{DateTime.UtcNow:O}`");
        builder.AppendLine($"- unitBlueprints: `{slice.UnitBlueprintIds.Count}/{slice.UnitBlueprintCap}`");
        builder.AppendLine($"- signatureActives: `{slice.SignatureActiveIds.Count}/{slice.SignatureActiveCap}`");
        builder.AppendLine($"- signaturePassives: `{slice.SignaturePassiveIds.Count}/{slice.SignaturePassiveCap}`");
        builder.AppendLine($"- flexActives: `{slice.FlexActiveIds.Count}/{slice.FlexActiveCap}`");
        builder.AppendLine($"- flexPassives: `{slice.FlexPassiveIds.Count}/{slice.FlexPassiveCap}`");
        builder.AppendLine($"- affixes: `{slice.AffixIds.Count}/{slice.AffixCap}`");
        builder.AppendLine($"- synergyFamilies: `{slice.SynergyFamilyIds.Count}/{slice.SynergyFamilyCap}`");
        builder.AppendLine($"- temporaryAugments: `{slice.TemporaryAugmentIds.Count}/{slice.TemporaryAugmentCap}`");
        builder.AppendLine($"- permanentAugments: `{slice.PermanentAugmentIds.Count}/{slice.PermanentAugmentCap}`");
        builder.AppendLine($"- passiveBoards: `{slice.PassiveBoardIds.Count}/{slice.PassiveBoardCap}`");
        builder.AppendLine($"- parkingLotContent: `{slice.ParkingLotContentIds.Count}`");
        builder.AppendLine();
        AppendList(builder, "UnitBlueprintIds", slice.UnitBlueprintIds);
        AppendList(builder, "SignatureActiveIds", slice.SignatureActiveIds);
        AppendList(builder, "SignaturePassiveIds", slice.SignaturePassiveIds);
        AppendList(builder, "FlexActiveIds", slice.FlexActiveIds);
        AppendList(builder, "FlexPassiveIds", slice.FlexPassiveIds);
        AppendList(builder, "AffixIds", slice.AffixIds);
        AppendList(builder, "SynergyFamilyIds", slice.SynergyFamilyIds);
        AppendList(builder, "TemporaryAugmentIds", slice.TemporaryAugmentIds);
        AppendList(builder, "PermanentAugmentIds", slice.PermanentAugmentIds);
        AppendList(builder, "PassiveBoardIds", slice.PassiveBoardIds);
        AppendList(builder, "ParkingLotContentIds", slice.ParkingLotContentIds);
        return builder.ToString();
    }

    private static void AppendList(StringBuilder builder, string title, System.Collections.Generic.IReadOnlyList<string> values)
    {
        builder.AppendLine($"## {title}");
        if (values.Count == 0)
        {
            builder.AppendLine("- (empty)");
            builder.AppendLine();
            return;
        }

        foreach (var value in values)
        {
            builder.AppendLine($"- `{value}`");
        }

        builder.AppendLine();
    }
}

public sealed record FirstPlayableSliceGenerationResult(
    FirstPlayableSliceDefinitionAsset Asset,
    FirstPlayableSliceDefinition Slice,
    string MarkdownPath);
