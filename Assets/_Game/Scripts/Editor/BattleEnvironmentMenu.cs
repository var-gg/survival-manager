using SM.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SM.Editor;

/// <summary>
/// SM/Battle/* 메뉴 — 전투 씬 환경 셋업/프리셋 적용을 클릭 한 번으로.
/// </summary>
public static class BattleEnvironmentMenu
{
    private const string BattleScenePath = "Assets/_Game/Scenes/Battle.unity";
    private const string EnvironmentGameObjectName = "BattleRenderEnvironment";

    [MenuItem("SM/Battle/환경 셋업 (BattleRenderEnvironment 생성·선택)", priority = 0)]
    public static void SetupBattleEnvironment()
    {
        EnsureBattleSceneOpen();

        var go = GameObject.Find(EnvironmentGameObjectName);
        var created = false;
        if (go == null)
        {
            go = new GameObject(EnvironmentGameObjectName);
            Undo.RegisterCreatedObjectUndo(go, "Create BattleRenderEnvironment");
            created = true;
        }

        var authoring = go.GetComponent<BattleRenderEnvironmentAuthoring>();
        var addedComponent = false;
        if (authoring == null)
        {
            authoring = Undo.AddComponent<BattleRenderEnvironmentAuthoring>(go);
            authoring.ApplyGameplayPreset();
            addedComponent = true;
        }

        Selection.activeGameObject = go;
        EditorGUIUtility.PingObject(go);

        if (created || addedComponent)
        {
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[SM/Battle] '{EnvironmentGameObjectName}' GameObject 셋업 완료 — Inspector에서 슬라이더로 튜닝하세요.");
        }
        else
        {
            Debug.Log($"[SM/Battle] '{EnvironmentGameObjectName}'는 이미 존재합니다. Hierarchy에서 선택했습니다.");
        }
    }

    [MenuItem("SM/Battle/Battle 씬 열기", priority = 1)]
    public static void OpenBattleScene()
    {
        EnsureBattleSceneOpen();
    }

    [MenuItem("SM/Battle/프리셋/전투 (Gameplay)", priority = 20)]
    public static void ApplyGameplayPreset() => ApplyPreset(a => a.ApplyGameplayPreset(), "전투 프리셋");

    [MenuItem("SM/Battle/프리셋/시네마틱 (Cinematic)", priority = 21)]
    public static void ApplyCinematicPreset() => ApplyPreset(a => a.ApplyCinematicPreset(), "시네마틱 프리셋");

    [MenuItem("SM/Battle/프리셋/디버그 중립 (Debug Neutral)", priority = 22)]
    public static void ApplyDebugNeutralPreset() => ApplyPreset(a => a.ApplyDebugNeutralPreset(), "디버그 중립 프리셋");

    [MenuItem("SM/Battle/프리뷰 새로고침 (Force Apply)", priority = 40)]
    public static void ForceRefreshPreview()
    {
        var auth = FindAuthoring();
        if (auth == null)
        {
            Debug.LogWarning("[SM/Battle] BattleRenderEnvironment가 씬에 없습니다. '환경 셋업'을 먼저 실행하세요.");
            return;
        }
        auth.Apply();
        SceneView.RepaintAll();
        Debug.Log("[SM/Battle] 프리뷰 새로고침 완료.");
    }

    [MenuItem("SM/Battle/환경 GameObject 선택", priority = 41)]
    public static void SelectEnvironment()
    {
        var auth = FindAuthoring();
        if (auth == null)
        {
            Debug.LogWarning("[SM/Battle] BattleRenderEnvironment가 씬에 없습니다. '환경 셋업'을 먼저 실행하세요.");
            return;
        }
        Selection.activeGameObject = auth.gameObject;
        EditorGUIUtility.PingObject(auth.gameObject);
    }

    private static void ApplyPreset(System.Action<BattleRenderEnvironmentAuthoring> apply, string presetName)
    {
        var auth = FindAuthoring();
        if (auth == null)
        {
            if (EditorUtility.DisplayDialog(
                    "BattleRenderEnvironment 없음",
                    $"씬에 BattleRenderEnvironment가 없습니다. 지금 셋업한 다음 '{presetName}'을 적용할까요?",
                    "셋업하고 적용",
                    "취소"))
            {
                SetupBattleEnvironment();
                auth = FindAuthoring();
                if (auth == null) return;
            }
            else
            {
                return;
            }
        }

        Undo.RecordObject(auth, $"{presetName} 적용");
        apply(auth);
        EditorUtility.SetDirty(auth);
        SceneView.RepaintAll();
        Debug.Log($"[SM/Battle] {presetName} 적용 완료.");
    }

    private static BattleRenderEnvironmentAuthoring? FindAuthoring()
    {
        var go = GameObject.Find(EnvironmentGameObjectName);
        return go != null ? go.GetComponent<BattleRenderEnvironmentAuthoring>() : null;
    }

    private static void EnsureBattleSceneOpen()
    {
        var active = SceneManager.GetActiveScene();
        if (active.IsValid() && active.path == BattleScenePath)
        {
            return;
        }
        EditorSceneManager.OpenScene(BattleScenePath, OpenSceneMode.Single);
    }
}
