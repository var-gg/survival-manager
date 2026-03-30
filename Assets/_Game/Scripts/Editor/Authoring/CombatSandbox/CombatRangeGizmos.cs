using SM.Unity.Sandbox;
using UnityEditor;
using UnityEngine;

namespace SM.Editor.Authoring.CombatSandbox;

public static class CombatRangeGizmos
{
    [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected, typeof(CombatSandboxSceneController))]
    private static void DrawRanges(CombatSandboxSceneController controller, GizmoType gizmoType)
    {
        if (controller == null)
        {
            return;
        }

        Handles.color = new Color(0.3f, 0.8f, 0.8f, gizmoType.HasFlag(GizmoType.Selected) ? 0.9f : 0.35f);
        DrawRingSet(controller.AllyAnchorHandles, controller.RangePreviewRadius);
        Handles.color = new Color(0.9f, 0.55f, 0.25f, gizmoType.HasFlag(GizmoType.Selected) ? 0.9f : 0.35f);
        DrawRingSet(controller.EnemyAnchorHandles, controller.RangePreviewRadius);
    }

    private static void DrawRingSet(Transform[] handles, float radius)
    {
        foreach (var handle in handles)
        {
            if (handle == null)
            {
                continue;
            }

            Handles.DrawWireDisc(handle.position, Vector3.up, radius);
        }
    }
}
