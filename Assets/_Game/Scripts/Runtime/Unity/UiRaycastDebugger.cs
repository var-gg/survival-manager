using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SM.Unity;

/// <summary>
/// Temporary diagnostic. Remove after debugging.
/// </summary>
public sealed class UiRaycastDebugger : MonoBehaviour
{
    private void Update()
    {
        if (!UnityEngine.Input.GetMouseButtonDown(0)) return;

        var es = EventSystem.current;
        if (es == null) return;

        var pointer = UnityEngine.Input.mousePosition;
        var module = es.currentInputModule;
        var isOver = es.IsPointerOverGameObject();
        var selected = es.currentSelectedGameObject;

        Debug.Log($"[UiDbg] pos=({pointer.x:0},{pointer.y:0}) module={module?.GetType().Name} " +
                  $"IsPointerOver={isOver} selected={selected?.name ?? "null"}");

        // Check all modules on EventSystem
        var modules = es.gameObject.GetComponents<BaseInputModule>();
        foreach (var m in modules)
        {
            Debug.Log($"  Module: {m.GetType().Name} enabled={m.enabled} isActiveAndEnabled={m.isActiveAndEnabled}");
        }

        var ped = new PointerEventData(es) { position = new Vector2(pointer.x, pointer.y) };
        var results = new List<RaycastResult>();
        es.RaycastAll(ped, results);

        foreach (var r in results)
        {
            var btn = r.gameObject.GetComponentInParent<Button>();
            if (btn != null)
            {
                var runtimeListeners = btn.onClick.GetType()
                    .GetField("m_Calls", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var persistentCount = btn.onClick.GetPersistentEventCount();

                Debug.Log($"  Button '{btn.name}': interactable={btn.interactable} " +
                          $"persistent={persistentCount} image={btn.image?.name ?? "null"} " +
                          $"targetGraphic={btn.targetGraphic?.name ?? "null"}");

                // Test: manually invoke onClick
                Debug.Log($"  >> Manually invoking {btn.name}.onClick...");
                btn.onClick.Invoke();
            }
        }
    }
}
