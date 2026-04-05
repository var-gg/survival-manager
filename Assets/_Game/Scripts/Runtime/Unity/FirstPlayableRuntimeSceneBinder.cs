using System.Linq;
using System.Reflection;
using SM.Unity.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SM.Unity;

public static class FirstPlayableRuntimeSceneBinder
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneLoadHook()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInitialSceneBindings()
    {
        EnsureSceneBindings(SceneManager.GetActiveScene());
    }

    public static void EnsureSceneBindings(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        EnsureEventSystem(scene);
        EnsureSharedUiFont(scene);

        switch (scene.name)
        {
            case SceneNames.Boot:
                EnsureBoot(scene);
                break;
            case SceneNames.Town:
                EnsureTown(scene);
                break;
            case SceneNames.Expedition:
                EnsureExpedition(scene);
                break;
            case SceneNames.Battle:
                EnsureBattle(scene);
                break;
            case SceneNames.Reward:
                EnsureReward(scene);
                break;
        }

        RefreshLocalizedBindings(scene);
    }

    public static void RefreshLocalizedBindings(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        EnsureSharedUiFont(scene);

        foreach (var host in scene.GetRootGameObjects()
                     .SelectMany(root => root.GetComponentsInChildren<RuntimePanelHost>(true)))
        {
            host.EnsureReady();
            host.RefreshPanel();
        }

        UiGraphicRaycastPolicy.ApplyToScene(scene);
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureSceneBindings(scene);
    }

    private static void EnsureBoot(Scene scene)
    {
        var bootstrapGo = FindGameObject(scene, "GameBootstrap");
        if (bootstrapGo == null)
        {
            return;
        }

        var bootstrap = EnsureComponent<GameBootstrap>(bootstrapGo);
        if (Application.isPlaying)
        {
            bootstrap.RunBootstrap();
        }
    }

    private static void EnsureTown(Scene scene)
    {
        var runtimeRoot = EnsureRootObject(scene, "TownRuntimeRoot");
        var host = EnsureRuntimePanelHost(scene, SceneNames.Town, runtimeRoot.transform);
        var controllerGo = EnsureChild(runtimeRoot.transform, "TownScreenController");
        var controller = EnsureComponent<TownScreenController>(controllerGo);
        Bind(controller, new System.Collections.Generic.Dictionary<string, Object?>
        {
            ["panelHost"] = host,
        });
        controller.EnsureRuntimeControls();
    }

    private static void EnsureExpedition(Scene scene)
    {
        var runtimeRoot = EnsureRootObject(scene, "ExpeditionRuntimeRoot");
        var host = EnsureRuntimePanelHost(scene, SceneNames.Expedition, runtimeRoot.transform);
        var controllerGo = EnsureChild(runtimeRoot.transform, "ExpeditionScreenController");
        var controller = EnsureComponent<ExpeditionScreenController>(controllerGo);
        Bind(controller, new System.Collections.Generic.Dictionary<string, Object?>
        {
            ["panelHost"] = host,
        });
        controller.EnsureRuntimeControls();
    }

    private static void EnsureBattle(Scene scene)
    {
        EnsureMainCamera(scene, new Vector3(0f, 8f, -8f), Quaternion.Euler(35f, 0f, 0f));

        var runtimeRoot = EnsureRootObject(scene, "BattleRuntimeRoot");
        var host = EnsureRuntimePanelHost(scene, SceneNames.Battle, runtimeRoot.transform);
        var stageRoot = EnsureChild(runtimeRoot.transform, "BattleStageRoot");
        var presentationGo = EnsureChild(runtimeRoot.transform, "BattlePresentationRoot");
        var presentation = EnsureComponent<BattlePresentationController>(presentationGo);

        var overlayCanvasGo = EnsureUiChild(runtimeRoot.transform, "ActorOverlayCanvas");
        var overlayCanvas = EnsureCanvas(overlayCanvasGo, sortingOrder: 0, withRaycaster: false);
        var overlayRootGo = EnsureUiChild(overlayCanvas.transform, "ActorOverlayRoot");
        var overlayRoot = overlayRootGo.GetComponent<RectTransform>();
        overlayRoot.anchorMin = Vector2.zero;
        overlayRoot.anchorMax = Vector2.one;
        overlayRoot.offsetMin = Vector2.zero;
        overlayRoot.offsetMax = Vector2.zero;
        var overlayImage = EnsureComponent<UnityEngine.UI.Image>(overlayRootGo);
        overlayImage.color = new Color(0f, 0f, 0f, 0f);
        overlayImage.raycastTarget = false;

        Bind(presentation, new System.Collections.Generic.Dictionary<string, Object?>
        {
            ["battleStageRoot"] = stageRoot.transform,
            ["actorOverlayRoot"] = overlayRoot,
        });

        var cameraRoot = EnsureChild(runtimeRoot.transform, "BattleCameraRoot");
        var cameraController = EnsureComponent<BattleCameraController>(cameraRoot);

        var controllerGo = EnsureChild(runtimeRoot.transform, "BattleScreenController");
        var controller = EnsureComponent<BattleScreenController>(controllerGo);
        Bind(controller, new System.Collections.Generic.Dictionary<string, Object?>
        {
            ["panelHost"] = host,
            ["presentationController"] = presentation,
            ["cameraController"] = cameraController,
        });
    }

    private static void EnsureReward(Scene scene)
    {
        var runtimeRoot = EnsureRootObject(scene, "RewardRuntimeRoot");
        var host = EnsureRuntimePanelHost(scene, SceneNames.Reward, runtimeRoot.transform);
        var controllerGo = EnsureChild(runtimeRoot.transform, "RewardScreenController");
        var controller = EnsureComponent<RewardScreenController>(controllerGo);
        Bind(controller, new System.Collections.Generic.Dictionary<string, Object?>
        {
            ["panelHost"] = host,
        });
    }

    private static void EnsureSharedUiFont(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            GameFontCatalog.ApplyToHierarchy(root.transform);
        }
    }

    private static void EnsureEventSystem(Scene scene)
    {
        UiEventSystemConfigurator.EnsureSceneEventSystem(scene);
    }

    private static RuntimePanelHost EnsureRuntimePanelHost(Scene scene, string sceneName, Transform parent)
    {
        if (!RuntimePanelAssetRegistry.TryGetScreenDescriptor(sceneName, out var descriptor))
        {
            throw new System.InvalidOperationException($"Missing runtime panel descriptor for scene '{sceneName}'.");
        }

        var hostGo = EnsureChild(parent, descriptor.HostObjectName);
        EnsureComponent<UnityEngine.UIElements.UIDocument>(hostGo);
        var host = EnsureComponent<RuntimePanelHost>(hostGo);

#if UNITY_EDITOR
        RuntimePanelAssetRegistry.ConfigureHost(host, sceneName);
#endif

        host.EnsureReady();
        return host;
    }

    private static void EnsureMainCamera(Scene scene, Vector3 position, Quaternion rotation)
    {
        var go = EnsureRootObject(scene, "Main Camera");
        var camera = EnsureComponent<Camera>(go);
        go.tag = "MainCamera";
        go.transform.position = position;
        go.transform.rotation = rotation;
        camera.clearFlags = CameraClearFlags.Skybox;
    }

    private static void Bind(Component target, System.Collections.Generic.Dictionary<string, Object?> refs)
    {
        foreach (var pair in refs)
        {
            if (pair.Value == null)
            {
                continue;
            }

            var field = target.GetType().GetField(pair.Key, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null || !field.FieldType.IsAssignableFrom(pair.Value.GetType()))
            {
                continue;
            }

            field.SetValue(target, pair.Value);
        }
    }

    private static GameObject EnsureRootObject(Scene scene, string name)
    {
        var existing = scene.GetRootGameObjects().FirstOrDefault(root => root.name == name);
        if (existing != null)
        {
            return existing;
        }

        var created = new GameObject(name);
        SceneManager.MoveGameObjectToScene(created, scene);
        return created;
    }

    private static GameObject EnsureChild(Transform parent, string name)
    {
        var existing = parent.Cast<Transform>().FirstOrDefault(child => child.name == name);
        if (existing != null)
        {
            return existing.gameObject;
        }

        var created = new GameObject(name);
        created.transform.SetParent(parent, false);
        return created;
    }

    private static GameObject EnsureUiChild(Transform parent, string name)
    {
        var existing = parent.Cast<Transform>().FirstOrDefault(child => child.name == name);
        if (existing != null)
        {
            return existing.gameObject;
        }

        var created = new GameObject(name, typeof(RectTransform));
        created.transform.SetParent(parent, false);
        return created;
    }

    private static Canvas EnsureCanvas(GameObject go, int sortingOrder, bool withRaycaster)
    {
        var canvas = EnsureComponent<Canvas>(go);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        EnsureComponent<UnityEngine.UI.CanvasScaler>(go);
        if (withRaycaster)
        {
            EnsureComponent<UnityEngine.UI.GraphicRaycaster>(go);
        }
        else
        {
            var raycaster = go.GetComponent<UnityEngine.UI.GraphicRaycaster>();
            if (raycaster != null)
            {
                DestroyObject(raycaster);
            }
        }

        return canvas;
    }

    private static GameObject? FindGameObject(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var match = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(transform => transform.name == name);
            if (match != null)
            {
                return match.gameObject;
            }
        }

        return null;
    }

    private static T EnsureComponent<T>(GameObject go) where T : Component
    {
        var existing = go.GetComponent<T>();
        if (existing != null)
        {
            return existing;
        }

        return go.AddComponent<T>();
    }

    private static void DestroyObject(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Object.Destroy(target);
            return;
        }

        Object.DestroyImmediate(target);
    }
}
