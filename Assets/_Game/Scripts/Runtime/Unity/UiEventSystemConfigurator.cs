using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SM.Unity;

public static class UiEventSystemConfigurator
{
    public static EventSystem EnsureSceneEventSystem(Scene scene)
    {
        if (!scene.IsValid())
        {
            throw new System.ArgumentException("Scene must be valid.", nameof(scene));
        }

        var eventSystems = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<EventSystem>(true))
            .ToList();

        EventSystem primary;
        if (eventSystems.Count == 0)
        {
            var go = new GameObject("EventSystem");
            SceneManager.MoveGameObjectToScene(go, scene);
            primary = go.AddComponent<EventSystem>();
        }
        else
        {
            primary = eventSystems[0];
            for (var i = 1; i < eventSystems.Count; i++)
            {
                DestroyObject(eventSystems[i].gameObject);
            }
        }

        primary.gameObject.name = "EventSystem";
        return Ensure(primary);
    }

    public static EventSystem Ensure(EventSystem eventSystem)
    {
        var go = eventSystem.gameObject;

        var inputSystemModules = go.GetComponents<InputSystemUIInputModule>();
        var inputSystemModule = inputSystemModules.FirstOrDefault();
        if (inputSystemModule == null)
        {
            inputSystemModule = go.AddComponent<InputSystemUIInputModule>();
        }

        for (var i = 1; i < inputSystemModules.Length; i++)
        {
            DestroyObject(inputSystemModules[i]);
        }

        foreach (var module in go.GetComponents<BaseInputModule>())
        {
            if (module != inputSystemModule)
            {
                DestroyObject(module);
            }
        }

        if (!UiInputSystemModuleConfigurator.TryConfigure(inputSystemModule, out var error))
        {
            Debug.LogWarning($"[UiEventSystemConfigurator] Failed to configure UI input module on '{go.name}': {error}");
        }

        return eventSystem;
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
