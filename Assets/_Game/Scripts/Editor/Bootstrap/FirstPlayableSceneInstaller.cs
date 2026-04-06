using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using SM.Editor.Bootstrap.UI;
using SM.Unity;
using SM.Unity.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SM.Editor.Bootstrap;

public static class FirstPlayableSceneInstaller
{
    private const string ScenesRoot = "Assets/_Game/Scenes";
    private static readonly string[] OrderedSceneNames = { "Boot", "Town", "Expedition", "Battle", "Reward" };

    [MenuItem("SM/Setup/Repair First Playable Scenes")]
    public static void RepairFirstPlayableScenes()
    {
        LocalizationFoundationBootstrap.EnsureFoundationAssets();
        RuntimePanelFoundationBootstrap.EnsureFoundationAssets();

        RebuildBoot();
        RebuildTown();
        RebuildExpedition();
        RebuildBattle();
        RebuildReward();
        EnsureBuildSettings();
        ValidateSavedSceneContracts();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("First playable scenes repaired and validated.");
    }

    public static void RebuildFirstPlayableScenes()
    {
        RepairFirstPlayableScenes();
    }

    private static void RebuildBoot()
    {
        var scene = CreateFreshScene("Boot");
        EnsureRootObject("SceneMarker_Boot");
        EnsureCamera("Main Camera", true, new Vector3(0f, 0f, -10f), Quaternion.identity);
        EnsureEventSystem();

        var canvas = EnsureCanvasRoot("BootCanvas", sortingOrder: 0, withRaycaster: true);
        var bootstrapGo = EnsureRootObject("GameBootstrap");
        EnsureComponent<GameBootstrap>(bootstrapGo);
        var controllerGo = EnsureRootObject("BootScreenController");
        var controller = EnsureComponent<BootScreenController>(controllerGo);

        var title = EnsureText(canvas.transform, "BootTitleText", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -48f), new Vector2(720f, 44f), TextAnchor.MiddleCenter, 24, "Session Realm");
        var status = EnsureText(canvas.transform, "BootStatusText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 36f), new Vector2(760f, 80f), TextAnchor.MiddleCenter, 18, "OfflineLocal 또는 OnlineAuthoritative 세션 영역을 선택하세요.");
        var hint = EnsureText(canvas.transform, "BootHintText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -44f), new Vector2(760f, 80f), TextAnchor.MiddleCenter, 16, "공식 온라인 세션은 후속 패스에서 개방됩니다.");
        var offlineButton = EnsureButton(canvas.transform, "OfflineLocalButton", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-126f, -144f), new Vector2(220f, 44f), "OfflineLocal");
        var onlineButton = EnsureButton(canvas.transform, "OnlineAuthoritativeButton", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(126f, -144f), new Vector2(220f, 44f), "OnlineAuthoritative");
        onlineButton.interactable = false;

        Bind(controller, new Dictionary<string, Object>
        {
            ["titleText"] = title,
            ["statusText"] = status,
            ["hintText"] = hint,
            ["offlineLocalButton"] = offlineButton,
            ["onlineAuthoritativeButton"] = onlineButton,
        });
        Save(scene);
    }

    private static void RebuildTown()
    {
        var scene = CreateFreshScene("Town");
        EnsureRootObject("SceneMarker_Town");
        EnsureCamera("Main Camera", true, new Vector3(0f, 0f, -10f), Quaternion.identity);
        EnsureEventSystem();

        var runtimeRoot = EnsureRootObject("TownRuntimeRoot");
        var host = EnsureRuntimePanelHost(runtimeRoot.transform, SceneNames.Town);
        var controllerGo = CreateChild(runtimeRoot.transform, "TownScreenController");
        var controller = EnsureComponent<TownScreenController>(controllerGo);
        Bind(controller, new Dictionary<string, Object>
        {
            ["panelHost"] = host,
        });

        Save(scene);
    }

    private static void RebuildExpedition()
    {
        var scene = CreateFreshScene("Expedition");
        EnsureRootObject("SceneMarker_Expedition");
        EnsureCamera("Main Camera", true, new Vector3(0f, 0f, -10f), Quaternion.identity);
        EnsureEventSystem();

        var runtimeRoot = EnsureRootObject("ExpeditionRuntimeRoot");
        var host = EnsureRuntimePanelHost(runtimeRoot.transform, SceneNames.Expedition);
        var controllerGo = CreateChild(runtimeRoot.transform, "ExpeditionScreenController");
        var controller = EnsureComponent<ExpeditionScreenController>(controllerGo);
        Bind(controller, new Dictionary<string, Object>
        {
            ["panelHost"] = host,
        });

        Save(scene);
    }

    private static void RebuildBattle()
    {
        var scene = CreateFreshScene("Battle");
        EnsureRootObject("SceneMarker_Battle");
        EnsureCamera("Main Camera", true, new Vector3(0f, 8f, -8f), Quaternion.Euler(35f, 0f, 0f));
        EnsureEventSystem();

        var runtimeRoot = EnsureRootObject("BattleRuntimeRoot");
        var host = EnsureRuntimePanelHost(runtimeRoot.transform, SceneNames.Battle);
        var stageRoot = CreateChild(runtimeRoot.transform, "BattleStageRoot");
        var presentationGo = CreateChild(runtimeRoot.transform, "BattlePresentationRoot");
        var presentation = EnsureComponent<BattlePresentationController>(presentationGo);

        var overlayCanvas = EnsureCanvasRoot("ActorOverlayCanvas", sortingOrder: 0, withRaycaster: false);
        var overlayRootGo = CreateUiChild(overlayCanvas.transform, "ActorOverlayRoot");
        var overlayRoot = overlayRootGo.GetComponent<RectTransform>();
        overlayRoot.anchorMin = Vector2.zero;
        overlayRoot.anchorMax = Vector2.one;
        overlayRoot.offsetMin = Vector2.zero;
        overlayRoot.offsetMax = Vector2.zero;
        var overlayImage = EnsureComponent<UnityEngine.UI.Image>(overlayRootGo);
        overlayImage.color = new Color(0f, 0f, 0f, 0f);
        overlayImage.raycastTarget = false;

        var cameraRoot = CreateChild(runtimeRoot.transform, "BattleCameraRoot");
        var cameraController = EnsureComponent<BattleCameraController>(cameraRoot);

        var controllerGo = CreateChild(runtimeRoot.transform, "BattleScreenController");
        var controller = EnsureComponent<BattleScreenController>(controllerGo);

        Bind(presentation, new Dictionary<string, Object>
        {
            ["battleStageRoot"] = stageRoot.transform,
            ["actorOverlayRoot"] = overlayRoot,
        });

        Bind(controller, new Dictionary<string, Object>
        {
            ["panelHost"] = host,
            ["presentationController"] = presentation,
            ["cameraController"] = cameraController,
        });

        Save(scene);
    }

    private static void RebuildReward()
    {
        var scene = CreateFreshScene("Reward");
        EnsureRootObject("SceneMarker_Reward");
        EnsureCamera("Main Camera", true, new Vector3(0f, 0f, -10f), Quaternion.identity);
        EnsureEventSystem();

        var runtimeRoot = EnsureRootObject("RewardRuntimeRoot");
        var host = EnsureRuntimePanelHost(runtimeRoot.transform, SceneNames.Reward);
        var controllerGo = CreateChild(runtimeRoot.transform, "RewardScreenController");
        var controller = EnsureComponent<RewardScreenController>(controllerGo);
        Bind(controller, new Dictionary<string, Object>
        {
            ["panelHost"] = host,
        });

        Save(scene);
    }

    private static Scene CreateFreshScene(string sceneName)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var path = $"{ScenesRoot}/{sceneName}.unity";
        EditorSceneManager.SaveScene(scene, path);
        return scene;
    }

    private static void Save(Scene scene)
    {
        UiGraphicRaycastPolicy.ApplyToScene(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void EnsureBuildSettings()
    {
        EditorBuildSettings.scenes = OrderedSceneNames
            .Select(name => new EditorBuildSettingsScene($"{ScenesRoot}/{name}.unity", true))
            .ToArray();
    }

    private static void ValidateSavedSceneContracts()
    {
        ValidateScene(
            "Boot",
            new[] { "SceneMarker_Boot", "GameBootstrap", "BootScreenController", "BootCanvas", "BootTitleText", "BootStatusText", "BootHintText", "OfflineLocalButton", "OnlineAuthoritativeButton", "Main Camera", "EventSystem" },
            new Dictionary<string, System.Type[]>
            {
                ["GameBootstrap"] = new[] { typeof(GameBootstrap) },
                ["BootCanvas"] = new[] { typeof(Canvas) },
                ["BootScreenController"] = new[] { typeof(BootScreenController) },
            });

        ValidateScene(
            "Town",
            new[] { "SceneMarker_Town", "TownRuntimeRoot", "TownRuntimePanelHost", "TownScreenController", "Main Camera", "EventSystem" },
            new Dictionary<string, System.Type[]>
            {
                ["TownRuntimePanelHost"] = new[] { typeof(RuntimePanelHost), typeof(UIDocument) },
                ["TownScreenController"] = new[] { typeof(TownScreenController) },
            });

        ValidateScene(
            "Expedition",
            new[] { "SceneMarker_Expedition", "ExpeditionRuntimeRoot", "ExpeditionRuntimePanelHost", "ExpeditionScreenController", "Main Camera", "EventSystem" },
            new Dictionary<string, System.Type[]>
            {
                ["ExpeditionRuntimePanelHost"] = new[] { typeof(RuntimePanelHost), typeof(UIDocument) },
                ["ExpeditionScreenController"] = new[] { typeof(ExpeditionScreenController) },
            });

        ValidateScene(
            "Battle",
            new[] { "SceneMarker_Battle", "BattleRuntimeRoot", "BattleRuntimePanelHost", "BattleScreenController", "BattlePresentationRoot", "BattleStageRoot", "BattleCameraRoot", "ActorOverlayCanvas", "ActorOverlayRoot", "Main Camera", "EventSystem" },
            new Dictionary<string, System.Type[]>
            {
                ["BattleRuntimePanelHost"] = new[] { typeof(RuntimePanelHost), typeof(UIDocument) },
                ["BattleScreenController"] = new[] { typeof(BattleScreenController) },
                ["BattlePresentationRoot"] = new[] { typeof(BattlePresentationController) },
                ["BattleCameraRoot"] = new[] { typeof(BattleCameraController) },
                ["ActorOverlayCanvas"] = new[] { typeof(Canvas) },
            });

        ValidateScene(
            "Reward",
            new[] { "SceneMarker_Reward", "RewardRuntimeRoot", "RewardRuntimePanelHost", "RewardScreenController", "Main Camera", "EventSystem" },
            new Dictionary<string, System.Type[]>
            {
                ["RewardRuntimePanelHost"] = new[] { typeof(RuntimePanelHost), typeof(UIDocument) },
                ["RewardScreenController"] = new[] { typeof(RewardScreenController) },
            });
    }

    private static void ValidateScene(string sceneName, IReadOnlyList<string> requiredObjectNames, IReadOnlyDictionary<string, System.Type[]> requiredComponents)
    {
        var scene = EditorSceneManager.OpenScene($"{ScenesRoot}/{sceneName}.unity", OpenSceneMode.Single);
        foreach (var objectName in requiredObjectNames)
        {
            var go = FindGameObject(scene, objectName);
            if (go == null)
            {
                throw new System.InvalidOperationException($"Scene repair failed. Missing '{objectName}' in {scene.path}");
            }
        }

        foreach (var pair in requiredComponents)
        {
            var go = FindGameObject(scene, pair.Key)
                ?? throw new System.InvalidOperationException($"Scene repair failed. Missing '{pair.Key}' in {scene.path}");
            foreach (var componentType in pair.Value)
            {
                if (go.GetComponent(componentType) == null)
                {
                    throw new System.InvalidOperationException($"Scene repair failed. Missing component '{componentType.Name}' on '{pair.Key}' in {scene.path}");
                }
            }
        }
    }

    private static RuntimePanelHost EnsureRuntimePanelHost(Transform parent, string sceneName)
    {
        if (!RuntimePanelAssetRegistry.TryGetScreenDescriptor(sceneName, out var descriptor))
        {
            throw new System.InvalidOperationException($"Missing runtime panel descriptor for '{sceneName}'.");
        }

        var hostGo = CreateChild(parent, descriptor.HostObjectName);
        EnsureComponent<UIDocument>(hostGo);
        var host = EnsureComponent<RuntimePanelHost>(hostGo);
        RuntimePanelAssetRegistry.ConfigureHost(host, sceneName);
        return host;
    }

    private static GameObject EnsureRootObject(string name)
    {
        var scene = SceneManager.GetActiveScene();
        var existing = scene.GetRootGameObjects().FirstOrDefault(go => go.name == name);
        if (existing != null)
        {
            return existing;
        }

        return new GameObject(name);
    }

    private static GameObject CreateChild(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    private static GameObject CreateUiChild(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static T EnsureComponent<T>(GameObject go) where T : Component
    {
#if UNITY_EDITOR
        if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go) > 0)
        {
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
        }
#endif
        var component = go.GetComponent<T>();
        if (component != null)
        {
            return component;
        }

        return go.AddComponent<T>();
    }

    private static Camera EnsureCamera(string name, bool tagMain, Vector3 position, Quaternion rotation)
    {
        var go = EnsureRootObject(name);
        var camera = EnsureComponent<Camera>(go);
        go.transform.position = position;
        go.transform.rotation = rotation;
        if (tagMain)
        {
            go.tag = "MainCamera";
        }
        return camera;
    }

    private static Canvas EnsureCanvasRoot(string name, int sortingOrder, bool withRaycaster)
    {
        var scene = SceneManager.GetActiveScene();
        var existing = scene.GetRootGameObjects().FirstOrDefault(go => go.name == name);
        var go = existing ?? new GameObject(name, typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>();

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var canvas = EnsureComponent<Canvas>(go);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        EnsureComponent<CanvasScaler>(go);
        if (withRaycaster)
        {
            EnsureComponent<GraphicRaycaster>(go);
        }

        return canvas;
    }

    private static void EnsureEventSystem()
    {
        UiEventSystemConfigurator.EnsureSceneEventSystem(SceneManager.GetActiveScene());
    }

    private static Text EnsureText(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta, TextAnchor alignment, int fontSize, string initialText)
    {
        var go = CreateUiChild(parent, name);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        var text = EnsureComponent<Text>(go);
        text.font = LocalizationFoundationBootstrap.GetSharedUiFont();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.color = Color.white;
        text.text = initialText;
        return text;
    }

    private static UnityEngine.UI.Button EnsureButton(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta, string label)
    {
        var go = CreateUiChild(parent, name);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        var image = EnsureComponent<UnityEngine.UI.Image>(go);
        image.color = new Color(0.16f, 0.24f, 0.32f, 0.94f);
        var button = EnsureComponent<UnityEngine.UI.Button>(go);
        var labelText = EnsureText(go.transform, $"{name}Text", new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter, 18, label);
        labelText.resizeTextForBestFit = true;
        labelText.resizeTextMinSize = 12;
        labelText.resizeTextMaxSize = 18;
        return button;
    }

    private static void Bind(Object target, IReadOnlyDictionary<string, Object> refs)
    {
        Undo.RecordObject(target, $"Bind {target.name} references");
        foreach (var pair in refs)
        {
            var field = target.GetType().GetField(pair.Key, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null && field.FieldType.IsAssignableFrom(pair.Value.GetType()))
            {
                field.SetValue(target, pair.Value);
            }
        }

        EditorUtility.SetDirty(target);
    }

    private static GameObject? FindGameObject(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var match = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == name);
            if (match != null)
            {
                return match.gameObject;
            }
        }

        return null;
    }
}
