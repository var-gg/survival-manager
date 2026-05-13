using SM.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SM.Editor;

/// <summary>
/// Battle.unity가 열릴 때마다 BattleRenderEnvironment + Authoring 컴포넌트가 있는지 검증한다.
/// 누락 시 LOUD ERROR — Play 진입 전에 root cause 인지하게 한다.
///
/// 정책: 라이팅 셋업 누락은 silent fallback으로 감추지 않는다. fallback은 실제 버그를
/// "그럭저럭 보이는 결과"로 마스킹해 root cause 추적을 지연시킨다.
/// </summary>
[InitializeOnLoad]
public static class BattleSceneEnvironmentValidator
{
    private const string BattleScenePath = "Assets/_Game/Scenes/Battle.unity";
    private const string EnvironmentGameObjectName = "BattleRenderEnvironment";

    static BattleSceneEnvironmentValidator()
    {
        EditorSceneManager.sceneOpened -= OnSceneOpened;
        EditorSceneManager.sceneOpened += OnSceneOpened;
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (scene.path != BattleScenePath) return;
        ValidateBattleScene(scene);
    }

    private static void ValidateBattleScene(Scene scene)
    {
        GameObject? envGo = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == EnvironmentGameObjectName)
            {
                envGo = root;
                break;
            }
        }

        if (envGo == null)
        {
            Debug.LogError(
                $"[BattleSceneValidator] '{EnvironmentGameObjectName}' GameObject가 Battle.unity에 없습니다. " +
                "라이팅이 정의되지 않은 상태로 Play 됩니다. " +
                "메뉴: SM/Battle/맵 + 캐릭터 미리보기 셋업 — 자동 셋업.");
            return;
        }

        var auth = envGo.GetComponent<BattleRenderEnvironmentAuthoring>();
        if (auth == null)
        {
            Debug.LogError(
                $"[BattleSceneValidator] '{EnvironmentGameObjectName}' GameObject는 있는데 " +
                "BattleRenderEnvironmentAuthoring 컴포넌트가 빠져 있습니다. " +
                "Inspector에서 Add Component → BattleRenderEnvironmentAuthoring 추가 후 " +
                "전투 (Gameplay) 프리셋 버튼 클릭. 그래도 안 보이면 메뉴: SM/Battle/맵 + 캐릭터 미리보기 셋업.");
        }
    }
}
