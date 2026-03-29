using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityCliConnector;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SM.Editor.UnityCliTools;

[UnityCliTool(
    Name = "missing_reference_scan",
    Description = "Scans first-playable scenes and project-owned prefabs for missing MonoBehaviour scripts.",
    Group = "report")]
public static class MissingReferenceScan
{
    private static readonly string[] FirstPlayableScenePaths =
    {
        "Assets/_Game/Scenes/Boot.unity",
        "Assets/_Game/Scenes/Town.unity",
        "Assets/_Game/Scenes/Expedition.unity",
        "Assets/_Game/Scenes/Battle.unity",
        "Assets/_Game/Scenes/Reward.unity",
    };

    public sealed class Parameters
    {
        [ToolParameter("Scan scope: first_playable, scenes, prefabs, or all. Default: first_playable.")]
        public string Scope { get; set; } = "first_playable";
    }

    public static object HandleCommand(JObject parameters)
    {
        var toolParams = new ToolParams(parameters);
        var scope = (toolParams.Get("scope", "first_playable") ?? "first_playable").Trim().ToLowerInvariant();

        var data = scope switch
        {
            "first_playable" => BuildReport(includeScenes: true, includePrefabs: true),
            "scenes" => BuildReport(includeScenes: true, includePrefabs: false),
            "prefabs" => BuildReport(includeScenes: false, includePrefabs: true),
            "all" => BuildReport(includeScenes: true, includePrefabs: true),
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "scope must be one of: first_playable, scenes, prefabs, all"),
        };

        return new SuccessResponse("Missing reference scan completed.", data);
    }

    private static object BuildReport(bool includeScenes, bool includePrefabs)
    {
        var sceneResults = includeScenes ? ScanScenes() : Array.Empty<ScanItem>();
        var prefabResults = includePrefabs ? ScanPrefabs() : Array.Empty<ScanItem>();

        return new
        {
            scope = includeScenes && includePrefabs ? "first_playable" : includeScenes ? "scenes" : "prefabs",
            scenes = sceneResults,
            prefabs = prefabResults,
            totalMissingScripts = sceneResults.Sum(item => item.missingScripts) + prefabResults.Sum(item => item.missingScripts),
            assetsWithIssues = sceneResults.Concat(prefabResults).Where(item => item.missingScripts > 0).Select(item => item.path).ToArray(),
        };
    }

    private static ScanItem[] ScanScenes()
    {
        var previousSetup = EditorSceneManager.GetSceneManagerSetup();
        var results = new List<ScanItem>();

        try
        {
            foreach (var scenePath in FirstPlayableScenePaths.Where(path => AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null))
            {
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                try
                {
                    var missingScripts = scene.GetRootGameObjects().Sum(CountMissingScriptsInTree);
                    results.Add(new ScanItem(scenePath, missingScripts));
                }
                finally
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }
        finally
        {
            EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
        }

        return results.ToArray();
    }

    private static ScanItem[] ScanPrefabs()
    {
        var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Game/Prefabs" });
        var results = new List<ScanItem>();

        foreach (var guid in prefabGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                results.Add(new ScanItem(path, CountMissingScriptsInTree(root)));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        return results.ToArray();
    }

    private static int CountMissingScriptsInTree(GameObject root)
    {
        return root
            .GetComponentsInChildren<Transform>(true)
            .Select(transform => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject))
            .Sum();
    }

    private sealed class ScanItem
    {
        public ScanItem(string path, int missingScripts)
        {
            this.path = path;
            this.missingScripts = missingScripts;
        }

        public string path { get; }

        public int missingScripts { get; }
    }
}
