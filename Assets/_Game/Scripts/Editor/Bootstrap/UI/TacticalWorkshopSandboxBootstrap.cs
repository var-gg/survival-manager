using System.IO;
using SM.Unity.UI;
using SM.Unity.UI.TacticalWorkshop;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SM.Editor.Bootstrap.UI;

public static class TacticalWorkshopSandboxBootstrap
{
    private const string ScenePath = "Assets/_Game/Scenes/TacticalWorkshopSandbox.unity";
    private const string VisualTreePath = "Assets/_Game/UI/TacticalWorkshop/TacticalWorkshopSandbox.uxml";
    private const string PreviewStylePath = "Assets/_Game/UI/TacticalWorkshop/TacticalWorkshopSandbox.uss";
    private const string MenuPath = "SM/Internal/UI/Tactical Workshop Preview";
    private const string RebuildMenuPath = "SM/Internal/Recovery/Rebuild Tactical Workshop Sandbox Scene";

    [MenuItem(MenuPath, false, 41)]
    public static void PlayPreview()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[TacticalWorkshopSandbox] 이미 Play 중입니다.");
            return;
        }

        EnsureSandboxScene();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    [MenuItem(RebuildMenuPath)]
    public static void RebuildPreviewSceneMenu()
    {
        RebuildPreviewScene();
    }

    public static void EnsureSandboxScene()
    {
        RuntimePanelFoundationBootstrap.EnsureFoundationAssets();

        if (!File.Exists(ScenePath))
        {
            RebuildPreviewScene();
        }
    }

    public static void RebuildPreviewScene()
    {
        RuntimePanelFoundationBootstrap.EnsureFoundationAssets();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EnsureRootObject("SceneMarker_TacticalWorkshopSandbox");
        EnsureCamera();
        UiEventSystemConfigurator.EnsureSceneEventSystem(scene);
        EnsureRuntimeRoot();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[TacticalWorkshopSandbox] Preview scene rebuilt.");
    }

    private static void EnsureRuntimeRoot()
    {
        var root = EnsureRootObject("TacticalWorkshopSandboxRuntimeRoot");
        var hostGo = CreateChild(root.transform, "TacticalWorkshopSandboxRuntimePanelHost");
        var host = EnsureComponent<RuntimePanelHost>(hostGo);
        EnsureComponent<UnityEngine.UIElements.UIDocument>(hostGo);
        host.Configure(
            RuntimePanelAssetRegistry.LoadSharedPanelSettings(),
            RuntimePanelAssetRegistry.LoadVisualTree(VisualTreePath),
            new[]
            {
                RuntimePanelAssetRegistry.LoadStyleSheet(RuntimePanelAssetRegistry.ThemeTokensStylePath),
                RuntimePanelAssetRegistry.LoadStyleSheet(RuntimePanelAssetRegistry.RuntimeThemeStylePath),
                RuntimePanelAssetRegistry.LoadStyleSheet(PreviewStylePath),
            },
            0,
            "TacticalWorkshopSandboxRuntimePanelHost");

        var controllerGo = CreateChild(root.transform, "TacticalWorkshopSandboxController");
        var controller = EnsureComponent<TacticalWorkshopSandboxController>(controllerGo);
        var serialized = new SerializedObject(controller);
        serialized.FindProperty("panelHost").objectReferenceValue = host;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);
    }

    private static void EnsureCamera()
    {
        var go = EnsureRootObject("Main Camera");
        var camera = EnsureComponent<Camera>(go);
        go.tag = "MainCamera";
        go.transform.position = new Vector3(0f, 0f, -10f);
        go.transform.rotation = Quaternion.identity;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.07f, 0.09f, 0.14f, 1f);
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 50f;
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
