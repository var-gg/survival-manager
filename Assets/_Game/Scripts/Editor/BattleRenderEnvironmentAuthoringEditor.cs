using SM.Unity;
using UnityEditor;
using UnityEngine;

namespace SM.Editor;

[CustomEditor(typeof(BattleRenderEnvironmentAuthoring))]
public sealed class BattleRenderEnvironmentAuthoringEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        var auth = (BattleRenderEnvironmentAuthoring)target;

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Quick Presets", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "프리셋 한 번 적용 후 아래 슬라이더로 fine-tune. 모든 값이 Edit/Play 모드 즉시 반영.",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Gameplay", GUILayout.Height(28)))
            {
                Undo.RecordObject(auth, "Apply Gameplay Preset");
                auth.ApplyGameplayPreset();
                EditorUtility.SetDirty(auth);
            }
            if (GUILayout.Button("Cinematic", GUILayout.Height(28)))
            {
                Undo.RecordObject(auth, "Apply Cinematic Preset");
                auth.ApplyCinematicPreset();
                EditorUtility.SetDirty(auth);
            }
            if (GUILayout.Button("Debug Neutral", GUILayout.Height(28)))
            {
                Undo.RecordObject(auth, "Apply Debug Neutral Preset");
                auth.ApplyDebugNeutralPreset();
                EditorUtility.SetDirty(auth);
            }
        }

        EditorGUILayout.Space(2);
        if (GUILayout.Button("Force Apply Now (refresh preview)", GUILayout.Height(22)))
        {
            auth.Apply();
            SceneView.RepaintAll();
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
        DrawDefaultInspector();

        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox(
            "Foreground tree 그림자: tree prefab을 Scene view에 drop → 그 GameObject에 " +
            "'BattleShadowOnlyMarker' 컴포넌트 attach. Mesh는 안 보이고 그림자만 ground에 떨어짐.",
            MessageType.None);
    }
}
