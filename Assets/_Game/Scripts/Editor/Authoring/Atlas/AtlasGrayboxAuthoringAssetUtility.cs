using System.IO;
using SM.Atlas.Model;
using SM.Atlas.Services;
using SM.Unity;
using SM.Unity.UI;
using SM.Unity.UI.Atlas;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SM.Editor.Authoring.Atlas;

public static class AtlasGrayboxAuthoringAssetUtility
{
    private const string AtlasScenePath = "Assets/_Game/Scenes/Atlas.unity";

    [MenuItem("SM/Internal/Recovery/Rebuild Atlas Graybox Scene")]
    public static void RebuildAtlasSceneMenu()
    {
        RebuildAtlasScene();
    }

    public static void EnsureAtlasScene()
    {
        if (File.Exists(AtlasScenePath))
        {
            return;
        }

        RebuildAtlasScene();
    }

    public static void RebuildAtlasScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EnsureRootObject("SceneMarker_Atlas");
        EnsureCamera();
        CreateGrayboxDiorama();
        UiEventSystemConfigurator.EnsureSceneEventSystem(scene);
        EnsureRuntimeRoot();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, AtlasScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[AtlasGraybox] Atlas scene rebuilt.");
    }

    private static void EnsureRuntimeRoot()
    {
        var root = EnsureRootObject("AtlasRuntimeRoot");
        var hostGo = CreateChild(root.transform, "AtlasRuntimePanelHost");
        var host = EnsureComponent<RuntimePanelHost>(hostGo);
        RuntimePanelAssetRegistry.ConfigureHost(host, SceneNames.Atlas);

        var controllerGo = CreateChild(root.transform, "AtlasScreenController");
        var controller = EnsureComponent<AtlasScreenController>(controllerGo);
        var serialized = new SerializedObject(controller);
        serialized.FindProperty("panelHost").objectReferenceValue = host;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureCamera()
    {
        var go = EnsureRootObject("Main Camera");
        var camera = EnsureComponent<Camera>(go);
        go.tag = "MainCamera";
        go.transform.position = new Vector3(0f, 0f, -10f);
        go.transform.rotation = Quaternion.identity;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.08f, 0.09f, 0.10f, 1f);
        camera.orthographic = true;
        camera.orthographicSize = 5.6f;
        go.transform.position = new Vector3(0f, 8f, 0f);
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }

    private static void CreateGrayboxDiorama()
    {
        var existing = GameObject.Find("AtlasGrayboxDiorama");
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        var root = new GameObject("AtlasGrayboxDiorama");
        var region = AtlasGrayboxDataFactory.CreateRegion();
        foreach (var node in region.Nodes)
        {
            var tile = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tile.name = $"Hex_{node.NodeId}_{node.Kind}";
            tile.transform.SetParent(root.transform, false);
            tile.transform.position = ToWorld(node.Hex);
            tile.transform.localScale = new Vector3(0.78f, 0.08f, 0.78f);

            var renderer = tile.GetComponent<Renderer>();
            renderer.sharedMaterial = CreateMaterial(ResolveColor(node.Kind));
        }
    }

    private static Vector3 ToWorld(AtlasHexCoordinate hex)
    {
        const float width = 1.04f;
        const float depth = 0.90f;
        var x = (hex.Q * width) + (hex.R * width * 0.5f);
        var z = hex.R * depth;
        return new Vector3(x, 0f, z);
    }

    private static Color ResolveColor(AtlasNodeKind kind)
    {
        return kind switch
        {
            AtlasNodeKind.Skirmish => new Color(0.42f, 0.31f, 0.24f, 1f),
            AtlasNodeKind.Elite => new Color(0.52f, 0.27f, 0.32f, 1f),
            AtlasNodeKind.Boss => new Color(0.68f, 0.20f, 0.26f, 1f),
            AtlasNodeKind.Extract => new Color(0.22f, 0.48f, 0.34f, 1f),
            AtlasNodeKind.Reward => new Color(0.65f, 0.52f, 0.22f, 1f),
            AtlasNodeKind.Event => new Color(0.28f, 0.36f, 0.60f, 1f),
            AtlasNodeKind.SigilAnchor => new Color(0.18f, 0.50f, 0.62f, 1f),
            _ => new Color(0.30f, 0.36f, 0.34f, 1f),
        };
    }

    private static Material CreateMaterial(Color color)
    {
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"))
        {
            color = color,
        };
        return material;
    }

    private static GameObject EnsureRootObject(string name)
    {
        var scene = SceneManager.GetActiveScene();
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == name)
            {
                return root;
            }
        }

        return new GameObject(name);
    }

    private static GameObject CreateChild(Transform parent, string name)
    {
        var existing = parent.Find(name);
        if (existing != null)
        {
            return existing.gameObject;
        }

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    private static T EnsureComponent<T>(GameObject go) where T : Component
    {
        var component = go.GetComponent<T>();
        return component != null ? component : go.AddComponent<T>();
    }
}
