using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SM.Unity;

public static class UiGraphicRaycastPolicy
{
    public static void ApplyToScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        foreach (var root in scene.GetRootGameObjects())
        {
            ApplyToHierarchy(root.transform);
        }
    }

    public static void ApplyToHierarchy(Transform root)
    {
        if (root == null)
        {
            return;
        }

        foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
        {
            if (graphic == null)
            {
                continue;
            }

            graphic.raycastTarget = ShouldReceiveRaycasts(graphic);
        }
    }

    private static bool ShouldReceiveRaycasts(Graphic graphic)
    {
        var go = graphic.gameObject;
        if (go.GetComponent<Selectable>() != null)
        {
            return true;
        }

        foreach (var component in go.GetComponents<Component>())
        {
            if (component == null
                || component is Transform
                || component is CanvasRenderer
                || component is Graphic
                || component is Selectable)
            {
                continue;
            }

            if (component is IEventSystemHandler)
            {
                return true;
            }
        }

        return false;
    }
}
