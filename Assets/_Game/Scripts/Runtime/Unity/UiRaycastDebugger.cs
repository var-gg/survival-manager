using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SM.Unity;

/// <summary>
/// Temporary diagnostic: logs UI raycast results + Button component state on each left-click.
/// Remove after debugging.
/// </summary>
public sealed class UiRaycastDebugger : MonoBehaviour
{
    private void Update()
    {
        if (!UnityEngine.Input.GetMouseButtonDown(0)) return;

        var es = EventSystem.current;
        if (es == null)
        {
            Debug.LogWarning("[UiRaycastDebugger] No EventSystem.current");
            return;
        }

        var pointer = UnityEngine.Input.mousePosition;
        var module = es.currentInputModule;
        var moduleName = module != null ? module.GetType().Name : "NULL";

        var ped = new PointerEventData(es) { position = new Vector2(pointer.x, pointer.y) };
        var results = new List<RaycastResult>();
        es.RaycastAll(ped, results);

        Debug.Log($"[UiRaycastDebugger] Click at ({pointer.x:0}, {pointer.y:0}), " +
                  $"InputModule={moduleName}, hits={results.Count}");

        for (var i = 0; i < results.Count; i++)
        {
            var r = results[i];
            var path = GetHierarchyPath(r.gameObject);

            // Check Button state on the hit object and its parents
            var btn = r.gameObject.GetComponentInParent<Button>();
            if (btn != null)
            {
                var listenerCount = btn.onClick.GetPersistentEventCount();
                var cg = btn.GetComponentInParent<CanvasGroup>();
                var cgInfo = cg != null
                    ? $", CanvasGroup(interactable={cg.interactable}, blocksRaycasts={cg.blocksRaycasts})"
                    : "";
                Debug.Log($"  [{i}] {path} | Button: interactable={btn.interactable}, " +
                          $"persistentListeners={listenerCount}, transition={btn.transition}{cgInfo}");
            }
            else
            {
                Debug.Log($"  [{i}] {path} (depth={r.depth})");
            }
        }
    }

    private static string GetHierarchyPath(GameObject go)
    {
        var path = go.name;
        var t = go.transform.parent;
        while (t != null)
        {
            path = t.name + "/" + path;
            t = t.parent;
        }

        return path;
    }
}
