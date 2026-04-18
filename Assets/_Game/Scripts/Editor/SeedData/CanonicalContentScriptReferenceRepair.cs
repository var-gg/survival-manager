using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace SM.Editor.SeedData;

internal static class CanonicalContentScriptReferenceRepair
{
    private const string MissingScriptReference = "m_Script: {fileID: 0}";
    private static readonly Regex MissingScriptBlockPattern = new(
        "m_Script: \\{fileID: 0\\}(?<tail>[\\s\\S]*?m_EditorClassIdentifier:\\s*(?<identifier>[^\\r\\n]+))",
        RegexOptions.Compiled);

    [MenuItem("SM/Internal/Content/Repair Canonical Script References")]
    public static void RepairResourcesRootFromMenu()
    {
        var fixedCount = RepairResourcesRoot();
        Debug.Log($"SM canonical script reference repair finished. Root={SampleSeedGenerator.ResourcesRoot}, Fixed={fixedCount}");
    }

    internal static int RepairResourcesRoot()
    {
        return RepairMissingScriptReferences(SampleSeedGenerator.ResourcesRoot);
    }

    internal static int RepairMissingScriptReferences(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
        {
            return 0;
        }

        var scriptGuidsByClassName = BuildScriptGuidIndex();
        var fixedCount = 0;

        foreach (var assetPath in Directory.EnumerateFiles(rootPath, "*.asset", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(assetPath);
            if (!text.Contains(MissingScriptReference, StringComparison.Ordinal))
            {
                continue;
            }

            var pathFixedCount = 0;
            var updated = MissingScriptBlockPattern.Replace(text, match =>
            {
                var className = ReadClassName(match.Groups["identifier"].Value);
                if (string.IsNullOrWhiteSpace(className) ||
                    !scriptGuidsByClassName.TryGetValue(className, out var scriptGuid))
                {
                    Debug.LogWarning($"SM canonical script reference repair skipped {ToUnityPath(assetPath)}. Class={className}");
                    return match.Value;
                }

                pathFixedCount++;
                return $"m_Script: {{fileID: 11500000, guid: {scriptGuid}, type: 3}}" + match.Groups["tail"].Value;
            });

            if (pathFixedCount == 0)
            {
                continue;
            }

            File.WriteAllText(assetPath, updated);
            AssetDatabase.ImportAsset(ToUnityPath(assetPath), ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            fixedCount += pathFixedCount;
        }

        return fixedCount;
    }

    private static Dictionary<string, string> BuildScriptGuidIndex()
    {
        var index = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var guid in AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets/_Game/Scripts" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            var className = Path.GetFileNameWithoutExtension(path);
            if (string.IsNullOrWhiteSpace(className))
            {
                continue;
            }

            index.TryAdd(className, guid);
        }

        return index;
    }

    private static string ReadClassName(string identifier)
    {
        var normalized = identifier.Trim();
        var lastSeparator = normalized.LastIndexOf(':');
        return lastSeparator >= 0 && lastSeparator + 1 < normalized.Length
            ? normalized[(lastSeparator + 1)..].Trim()
            : normalized;
    }

    private static string ToUnityPath(string path)
    {
        return path.Replace('\\', '/');
    }
}
