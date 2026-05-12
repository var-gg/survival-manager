using SM.Unity;
using UnityEditor;
using UnityEngine;

namespace SM.Editor;

[CustomEditor(typeof(BattleRenderEnvironmentAuthoring))]
public sealed class BattleRenderEnvironmentAuthoringEditor : UnityEditor.Editor
{
    private static readonly GUIContent ButtonGameplay = new(
        "전투 (Gameplay)",
        "전투 가독성 우선 baseline — 캐릭터 silhouette 명확, 후처리 절제, 포그 끔.");

    private static readonly GUIContent ButtonCinematic = new(
        "시네마틱 (Cinematic)",
        "마케팅 / 캡쳐용 — Bloom 강함, ACES 톤매핑, 따뜻한 노을 포그, 채도 ↑.");

    private static readonly GUIContent ButtonDebug = new(
        "디버그 중립 (Debug Neutral)",
        "트러블슈팅용 — 후처리 0, 톤매핑 Neutral, 포그 끔. 머티리얼/라이트만 평가할 때.");

    private static readonly GUIContent ButtonForceApply = new(
        "지금 강제 적용 (프리뷰 새로고침)",
        "에디트 모드에서 슬라이더 변경이 즉시 반영 안 됐을 때 강제 적용.");

    public override void OnInspectorGUI()
    {
        var auth = (BattleRenderEnvironmentAuthoring)target;

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("기본 프리셋 (한 번 클릭 → 베이스라인 적용)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "프리셋 적용 후 아래 슬라이더로 fine-tune.\n" +
            "슬라이더를 움직이면 Scene view에 즉시 반영됩니다.",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(ButtonGameplay, GUILayout.Height(30)))
            {
                Undo.RecordObject(auth, "전투 프리셋 적용");
                auth.ApplyGameplayPreset();
                EditorUtility.SetDirty(auth);
                SceneView.RepaintAll();
            }
            if (GUILayout.Button(ButtonCinematic, GUILayout.Height(30)))
            {
                Undo.RecordObject(auth, "시네마틱 프리셋 적용");
                auth.ApplyCinematicPreset();
                EditorUtility.SetDirty(auth);
                SceneView.RepaintAll();
            }
            if (GUILayout.Button(ButtonDebug, GUILayout.Height(30)))
            {
                Undo.RecordObject(auth, "디버그 중립 프리셋 적용");
                auth.ApplyDebugNeutralPreset();
                EditorUtility.SetDirty(auth);
                SceneView.RepaintAll();
            }
        }

        EditorGUILayout.Space(2);
        if (GUILayout.Button(ButtonForceApply, GUILayout.Height(22)))
        {
            auth.Apply();
            SceneView.RepaintAll();
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("설정 슬라이더", EditorStyles.boldLabel);

        // ─── BeginChangeCheck 패턴: 슬라이더 변경 직후 즉시 Apply + Scene repaint
        EditorGUI.BeginChangeCheck();
        DrawLocalizedInspector();
        if (EditorGUI.EndChangeCheck())
        {
            auth.Apply();
            EditorUtility.SetDirty(auth);
            SceneView.RepaintAll();
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox(
            "[ Foreground 트리 그림자만 추가 ]\n" +
            "1. Tree prefab을 Project에서 Scene view에 drag-drop\n" +
            "2. 그 GameObject 선택 → Inspector → Add Component → BattleShadowOnlyMarker\n" +
            "3. 메쉬는 안 보이고 그림자만 ground에 떨어짐",
            MessageType.None);
    }

    private static readonly (string PropName, string KoreanLabel)[] FieldLabels = new (string, string)[]
    {
        ("autoApplyInEditMode", "에디트 모드 즉시 적용"),
        ("ambientSky", "환경광 — 하늘 쪽"),
        ("ambientEquator", "환경광 — 수평 쪽"),
        ("ambientGround", "환경광 — 지면 쪽"),
        ("ambientIntensity", "환경광 세기"),
        ("skybox", "스카이박스 머티리얼"),
        ("forceCameraClearToSkybox", "카메라 Clear = Skybox"),
        ("fogEnabled", "포그 켜기"),
        ("fogMode", "포그 모드"),
        ("fogColor", "포그 색"),
        ("fogStart", "포그 시작 거리"),
        ("fogEnd", "포그 끝 거리"),
        ("fogDensity", "포그 밀도 (Exp 모드)"),
        ("sunRotationEuler", "햇빛 방향 (X=피치, Y=요)"),
        ("sunColor", "햇빛 색"),
        ("sunIntensity", "햇빛 세기"),
        ("sunShadowType", "햇빛 그림자 종류"),
        ("sunShadowStrength", "햇빛 그림자 진하기"),
        ("sunShadowBias", "그림자 바이어스"),
        ("sunShadowNormalBias", "그림자 법선 바이어스"),
        ("fillRotationEuler", "필 라이트 방향"),
        ("fillColor", "필 라이트 색"),
        ("fillIntensity", "필 라이트 세기"),
        ("forceCameraUACD", "카메라 UACD 강제"),
        ("renderPostProcessing", "후처리 활성"),
        ("allowHDR", "카메라 HDR 렌더링"),
        ("allowHDROutput", "HDR 디스플레이 출력"),
        ("sourceVolumeProfile", "Volume 원본 프로파일"),
        ("bloomIntensity", "Bloom 세기"),
        ("bloomThreshold", "Bloom 임계 밝기"),
        ("bloomScatter", "Bloom 퍼짐"),
        ("bloomTint", "Bloom 색조"),
        ("postExposure", "노출 보정 (EV)"),
        ("contrast", "대비"),
        ("saturation", "채도"),
        ("colorFilter", "컬러 필터 (전역 곱)"),
        ("tonemapMode", "톤매핑 모드"),
        ("vignetteIntensity", "비네팅 세기"),
        ("vignetteSmoothness", "비네팅 부드러움"),
        ("overrideRuntimeLightCreation", "런타임 자동 라이트 비활성 (충돌 방지)"),
    };

    private void DrawLocalizedInspector()
    {
        serializedObject.Update();
        var iterator = serializedObject.GetIterator();
        var enterChildren = true;
        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (iterator.name == "m_Script")
            {
                continue;
            }

            var korean = FindKoreanLabel(iterator.name);
            if (korean != null)
            {
                EditorGUILayout.PropertyField(iterator, new GUIContent(korean, iterator.tooltip));
            }
            else
            {
                EditorGUILayout.PropertyField(iterator, true);
            }
        }
        serializedObject.ApplyModifiedProperties();
    }

    private static string? FindKoreanLabel(string propName)
    {
        foreach (var entry in FieldLabels)
        {
            if (entry.PropName == propName)
            {
                return entry.KoreanLabel;
            }
        }
        return null;
    }
}
