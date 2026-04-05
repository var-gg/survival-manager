using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SM.Unity;

/// <summary>
/// Temporary diagnostic: logs all UI elements hit by the EventSystem on each left-click.
/// Uses Legacy Input API (guaranteed to work in Both mode) to detect clicks,
/// then checks what the EventSystem's raycaster sees at that position.
/// Remove after debugging.
/// </summary>
public sealed class UiRaycastDebugger : MonoBehaviour
{
    private void Update()
    {
        // Use Legacy API — we know this works in Both mode
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
        var moduleEnabled = module != null && module.enabled;

        var ped = new PointerEventData(es) { position = new Vector2(pointer.x, pointer.y) };
        var results = new List<RaycastResult>();
        es.RaycastAll(ped, results);

        Debug.Log($"[UiRaycastDebugger] Click at ({pointer.x:0}, {pointer.y:0}), " +
                  $"InputModule={moduleName} (enabled={moduleEnabled}), hits={results.Count}");

        for (var i = 0; i < results.Count; i++)
        {
            var r = results[i];
            var path = GetHierarchyPath(r.gameObject);
            Debug.Log($"  [{i}] {path} (depth={r.depth}, sortOrder={r.sortingOrder})");
        }

        if (results.Count == 0)
        {
            Debug.LogWarning("[UiRaycastDebugger] No UI hits at pointer position");
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
