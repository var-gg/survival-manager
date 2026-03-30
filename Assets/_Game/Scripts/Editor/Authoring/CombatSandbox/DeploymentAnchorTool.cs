using SM.Unity.Sandbox;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace SM.Editor.Authoring.CombatSandbox;

[EditorTool("Combat Sandbox Anchor Tool", typeof(CombatSandboxSceneController))]
public sealed class DeploymentAnchorTool : EditorTool
{
    public override void OnToolGUI(EditorWindow window)
    {
        if (target is not CombatSandboxSceneController controller)
        {
            return;
        }

        Handles.color = new Color(0.2f, 0.7f, 0.9f, 0.9f);
        DrawHandles(controller.AllyAnchorHandles, "Ally");
        Handles.color = new Color(0.9f, 0.45f, 0.2f, 0.9f);
        DrawHandles(controller.EnemyAnchorHandles, "Enemy");
    }

    private static void DrawHandles(Transform[] handles, string prefix)
    {
        for (var index = 0; index < handles.Length; index++)
        {
            var handle = handles[index];
            if (handle == null)
            {
                continue;
            }

            EditorGUI.BeginChangeCheck();
            var newPosition = Handles.PositionHandle(handle.position, handle.rotation);
            Handles.Label(newPosition + Vector3.up * 0.2f, $"{prefix} {index + 1}");
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(handle, "Move Sandbox Anchor");
                handle.position = newPosition;
                EditorUtility.SetDirty(handle);
            }
        }
    }
}
