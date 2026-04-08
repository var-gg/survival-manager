using System.IO;
using SM.Meta.Serialization;
using SM.Unity;
using UnityEditor;
using UnityEngine;

namespace SM.Editor.Validation;

public static class ContentSnapshotExporter
{
    public const string OutputPath = "Assets/Resources/_Game/Content/content-snapshot.json";

    [MenuItem("SM/Internal/Content/Export Content Snapshot")]
    public static void ExportSnapshot()
    {
        var lookup = new RuntimeCombatContentLookup(allowEditorRecoveryFallback: true);
        if (!lookup.TryGetCombatSnapshot(out var snapshot, out var error))
        {
            Debug.LogError($"[ContentSnapshotExporter] Failed to build snapshot: {error}");
            return;
        }

        var json = ContentSnapshotJsonSerializer.Serialize(snapshot);
        var directory = Path.GetDirectoryName(OutputPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(OutputPath, json);
        AssetDatabase.Refresh();
        Debug.Log($"[ContentSnapshotExporter] Exported snapshot to {OutputPath} ({json.Length:N0} chars)");
    }

    [MenuItem("SM/Internal/Content/Export Content Snapshot", true)]
    private static bool CanExport() => !EditorApplication.isCompiling;

    [MenuItem("SM/Internal/Content/Export Individual Assets")]
    public static void ExportIndividualAssets()
    {
        IndividualAssetExporter.ExportAll();
        AssetDatabase.Refresh();
    }

    [MenuItem("SM/Internal/Content/Export Individual Assets", true)]
    private static bool CanExportIndividual() => !EditorApplication.isCompiling;
}
