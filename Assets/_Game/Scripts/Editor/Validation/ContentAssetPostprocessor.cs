using System.IO;
using System.Linq;
using SM.Unity.ContentConversion;
using UnityEditor;
using UnityEngine;

namespace SM.Editor.Validation;

public sealed class ContentAssetPostprocessor : AssetPostprocessor
{
    private const string DefinitionsPath = "Assets/Resources/_Game/Content/Definitions/";
    private const string ExportedDataPath = "Assets/Resources/_Game/Content/ExportedData/";
    private static ContentDefinitionRegistry? _cachedRegistry;
    private static bool _exporting;

    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        if (_exporting) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;

        var contentImports = importedAssets
            .Where(IsContentDefinitionPath)
            .Where(p => !p.StartsWith(ExportedDataPath))
            .ToArray();
        var contentDeletes = deletedAssets
            .Where(IsContentDefinitionPath)
            .Where(p => !p.StartsWith(ExportedDataPath))
            .ToArray();

        if (contentImports.Length == 0 && contentDeletes.Length == 0) return;

        _exporting = true;
        try
        {
            if (contentImports.Length > 0)
            {
                _cachedRegistry = null;
            }

            foreach (var path in contentImports)
            {
                ExportSingleAsset(path);
            }

            foreach (var path in contentDeletes)
            {
                RemoveExportedJson(path);
            }
        }
        finally
        {
            _exporting = false;
        }
    }

    private static void ExportSingleAsset(string assetPath)
    {
        var asset = AssetDatabase.LoadMainAssetAtPath(assetPath) as ScriptableObject;
        if (asset == null) return;

        var result = IndividualAssetExporter.TryExportSingle(asset);
        if (result == null) return;

        var (json, relativePath) = result.Value;
        var directory = Path.GetDirectoryName(relativePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(relativePath, json);
    }

    private static void RemoveExportedJson(string assetPath)
    {
        var fileName = Path.GetFileNameWithoutExtension(assetPath);
        if (string.IsNullOrWhiteSpace(fileName)) return;

        var subfolder = InferSubfolderFromPath(assetPath);
        if (subfolder == null) return;

        var jsonPath = Path.Combine(IndividualAssetExporter.OutputRoot, subfolder, $"{fileName}.json");
        if (File.Exists(jsonPath))
        {
            File.Delete(jsonPath);
            var metaPath = jsonPath + ".meta";
            if (File.Exists(metaPath))
            {
                File.Delete(metaPath);
            }
        }
    }

    private static bool IsContentDefinitionPath(string path)
    {
        return path.StartsWith(DefinitionsPath) && path.EndsWith(".asset");
    }

    private static string? InferSubfolderFromPath(string assetPath)
    {
        var relative = assetPath[DefinitionsPath.Length..];
        var slashIndex = relative.IndexOf('/');
        return slashIndex > 0 ? relative[..slashIndex] : null;
    }
}
