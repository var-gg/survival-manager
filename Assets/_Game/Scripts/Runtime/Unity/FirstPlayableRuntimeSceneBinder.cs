using System.Linq;
using SM.Unity.UI;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace SM.Unity;

public static class FirstPlayableRuntimeSceneBinder
{
    private static readonly ProfilerMarker RefreshLocalizedBindingsMarker = new("SM.FirstPlayableRuntimeSceneBinder.RefreshLocalizedBindings");

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
        ValidateSceneContract(scene);
        RefreshLocalizedBindings(scene);
    }

    public static void RefreshLocalizedBindings(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        using (RefreshLocalizedBindingsMarker.Auto())
        {
            EnsureSharedUiFont(scene);

            foreach (var host in scene.GetRootGameObjects()
                         .SelectMany(root => root.GetComponentsInChildren<RuntimePanelHost>(true)))
            {
                host.EnsureReady();
                host.RefreshPanel();
            }

            UiGraphicRaycastPolicy.ApplyToScene(scene);
        }

        stopwatch.Stop();
        RuntimeInstrumentation.LogDuration(
            nameof(FirstPlayableRuntimeSceneBinder) + ".RefreshLocalizedBindings",
            stopwatch.Elapsed,
            $"scene={scene.name}");
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureSceneBindings(scene);
    }

    private static void ValidateSceneContract(Scene scene)
    {
        switch (scene.name)
        {
            case SceneNames.Boot:
                ValidateBoot(scene);
                break;

            case SceneNames.Town:
                ValidateScreenScene<TownScreenController>(scene, "TownRuntimePanelHost", "TownScreenController", controller => controller.EnsureRuntimeControls());
                break;

            case SceneNames.Expedition:
                ValidateScreenScene<ExpeditionScreenController>(scene, "ExpeditionRuntimePanelHost", "ExpeditionScreenController", controller => controller.EnsureRuntimeControls());
                break;

            case SceneNames.Atlas:
                ValidateScreenScene<UI.Atlas.AtlasScreenController>(scene, "AtlasRuntimePanelHost", "AtlasScreenController", controller => controller.EnsureRuntimeControls());
                break;

            case SceneNames.Battle:
                ValidateBattle(scene);
                break;

            case SceneNames.Reward:
                ValidateScreenScene<RewardScreenController>(scene, "RewardRuntimePanelHost", "RewardScreenController", controller => controller.EnsureRuntimeControls());
                break;
        }
    }

    private static void ValidateBoot(Scene scene)
    {
        var bootstrap = ResolveComponent<GameBootstrap>(scene, "GameBootstrap");
        var controller = ResolveComponent<BootScreenController>(scene, "BootScreenController");
        var canvas = ResolveComponent<Canvas>(scene, "BootCanvas");
        var button = ResolveComponent<Button>(scene, "OfflineLocalButton");
        if (bootstrap == null || controller == null || canvas == null || button == null)
        {
            UnityEngine.Debug.LogError($"[FirstPlayableRuntimeSceneBinder] Boot scene contract is incomplete. Repair via SM/Internal/Recovery/Repair First Playable Scenes.");
            return;
        }

        if (Application.isPlaying && GameSessionRoot.Instance?.Localization.IsInitialized == true)
        {
            GlobalLocalizationOverlayView.EnsureAttached(canvas.GetComponent<RectTransform>(), GameSessionRoot.Instance.Localization);
        }
    }

    private static void ValidateBattle(Scene scene)
    {
        var host = ResolveComponent<RuntimePanelHost>(scene, "BattleRuntimePanelHost");
        var controller = ResolveComponent<BattleScreenController>(scene, "BattleScreenController");
        var presentation = ResolveComponent<BattlePresentationController>(scene, "BattlePresentationRoot");
        var cameraController = ResolveComponent<BattleCameraController>(scene, "BattleCameraRoot");
        var overlay = ResolveComponent<Canvas>(scene, "ActorOverlayCanvas");
        if (host == null || controller == null || presentation == null || cameraController == null || overlay == null)
        {
            UnityEngine.Debug.LogError($"[FirstPlayableRuntimeSceneBinder] Battle scene contract is incomplete. Repair via SM/Internal/Recovery/Repair First Playable Scenes.");
            return;
        }

        host.EnsureReady();
    }

    private static void ValidateScreenScene<TController>(
        Scene scene,
        string hostName,
        string controllerName,
        System.Action<TController> configure)
        where TController : Component
    {
        var host = ResolveComponent<RuntimePanelHost>(scene, hostName);
        var controller = ResolveComponent<TController>(scene, controllerName);
        if (host == null || controller == null)
        {
            UnityEngine.Debug.LogError($"[FirstPlayableRuntimeSceneBinder] Scene '{scene.name}' is missing '{hostName}' or '{controllerName}'. Repair via SM/Internal/Recovery/Repair First Playable Scenes.");
            return;
        }

        host.EnsureReady();
        configure(controller);
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

    private static T? ResolveComponent<T>(Scene scene, string objectName) where T : Component
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var match = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(transform => transform.name == objectName);
            if (match != null)
            {
                return match.GetComponent<T>();
            }
        }

        return null;
    }
}
