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

        var strongAlpha = gizmoType.HasFlag(GizmoType.Selected) ? 0.9f : 0.35f;
        DrawPreviewSet(
            controller.AllyAnchorHandles,
            controller,
            new Color(0.3f, 0.8f, 0.8f, strongAlpha),
            new Color(0.22f, 0.62f, 1f, strongAlpha * 0.9f));
        DrawPreviewSet(
            controller.EnemyAnchorHandles,
            controller,
            new Color(0.9f, 0.55f, 0.25f, strongAlpha),
            new Color(1f, 0.36f, 0.28f, strongAlpha * 0.9f));
    }

    private static void DrawPreviewSet(Transform[] handles, CombatSandboxSceneController controller, Color primaryColor, Color accentColor)
    {
        foreach (var handle in handles)
        {
            if (handle == null)
            {
                continue;
            }

            Handles.color = primaryColor;
            Handles.DrawWireDisc(handle.position, Vector3.up, controller.NavigationPreviewRadius);
            Handles.DrawWireDisc(handle.position, Vector3.up, controller.RangePreviewRadius);
            Handles.color = new Color(primaryColor.r, primaryColor.g, primaryColor.b, primaryColor.a * 0.7f);
            Handles.DrawWireDisc(handle.position, Vector3.up, controller.SeparationPreviewRadius);

            Handles.color = accentColor;
            Handles.DrawWireDisc(handle.position, Vector3.up, controller.PreferredRangeMinPreview);
            Handles.DrawWireDisc(handle.position, Vector3.up, controller.PreferredRangeMaxPreview);

            Handles.color = new Color(0.2f, 0.75f, 0.65f, primaryColor.a * 0.6f);
            Handles.DrawWireDisc(handle.position, Vector3.up, controller.FrontlineGuardRadiusPreview);

            DrawHeadAnchor(handle.position, controller.HeadAnchorHeightPreview, accentColor);
            DrawSlotRing(handle.position, controller.EngagementSlotRadiusPreview, controller.EngagementSlotCountPreview, accentColor);
            DrawEntityKindLabel(handle.position, controller.HeadAnchorHeightPreview, primaryColor);
        }
    }

    private static void DrawHeadAnchor(Vector3 basePosition, float height, Color color)
    {
        Handles.color = color;
        var head = basePosition + Vector3.up * height;
        Handles.DrawLine(basePosition, head);
        Handles.SphereHandleCap(0, head, Quaternion.identity, 0.08f, EventType.Repaint);
    }

    private static void DrawEntityKindLabel(Vector3 basePosition, float headHeight, Color color)
    {
        var labelPosition = basePosition + Vector3.up * (headHeight + 0.2f);
        var style = new GUIStyle(GUI.skin.label)
        {
            normal = { textColor = color },
            fontSize = 10,
            alignment = TextAnchor.MiddleCenter
        };
        Handles.Label(labelPosition, "Unit", style);
    }

    private static void DrawSlotRing(Vector3 center, float radius, int slotCount, Color color)
    {
        Handles.color = new Color(color.r, color.g, color.b, color.a * 0.7f);
        Handles.DrawWireDisc(center, Vector3.up, radius);

        if (slotCount <= 0)
        {
            return;
        }

        var spread = slotCount == 1 ? 0f : 160f;
        var start = 180f - (spread * 0.5f);
        var step = slotCount == 1 ? 0f : spread / (slotCount - 1);
        for (var index = 0; index < slotCount; index++)
        {
            var angle = (start + (step * index)) * Mathf.Deg2Rad;
            var slotPosition = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            Handles.SphereHandleCap(0, slotPosition, Quaternion.identity, 0.06f, EventType.Repaint);
        }
    }
}
