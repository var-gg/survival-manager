using SM.Meta.Model;
using SM.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SM.Editor.Bootstrap;

/// <summary>
/// 씬별 직접 진입 메뉴 — 전체테스트로 풀 루프를 한 번 돌릴 필요 없이
/// 특정 단계의 UI/UX 만 보고 싶을 때 사용.
///
/// 흐름:
///   1. 메뉴 클릭 → SessionState에 target step 표시 + Boot.unity 열기 + Play 진입
///   2. Boot가 bootstrap 완료하면 update 후킹이 자동으로
///      StartRealm(OfflineLocal) + 적절한 SceneFlow 단계로 진행
///
/// 정상 흐름 (사용자 클릭 path)을 그대로 재사용해서 production 진입과 똑같은 상태 보장.
/// </summary>
[InitializeOnLoad]
public static class FirstPlayableSceneTestMenu
{
    private const string BootScenePath = "Assets/_Game/Scenes/Boot.unity";
    private const string TargetStepKey = "SM.FirstPlayableSceneTest.TargetStep";
    private const string FramesWaitedKey = "SM.FirstPlayableSceneTest.FramesWaited";
    private const int FramesBeforeAutoAdvance = 30; // 약 0.5초 — bootstrap + scene binder 안정화

    private const string StepTown = "town";
    private const string StepExpedition = "expedition";

    static FirstPlayableSceneTestMenu()
    {
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
    }

    [MenuItem("SM/Town 테스트", false, 5)]
    public static void PlayTown()
    {
        SessionState.SetString(TargetStepKey, StepTown);
        SessionState.SetInt(FramesWaitedKey, 0);
        OpenBootAndEnterPlay();
    }

    [MenuItem("SM/Expedition 테스트", false, 6)]
    public static void PlayExpedition()
    {
        SessionState.SetString(TargetStepKey, StepExpedition);
        SessionState.SetInt(FramesWaitedKey, 0);
        OpenBootAndEnterPlay();
    }

    private static void OpenBootAndEnterPlay()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[FirstPlayableSceneTest] 이미 Play 상태 — 종료 후 다시 시도하세요.");
            return;
        }

        var active = SceneManager.GetActiveScene();
        if (!active.IsValid() || active.path != BootScenePath)
        {
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
        }
        EditorApplication.EnterPlaymode();
    }

    private static void OnEditorUpdate()
    {
        var target = SessionState.GetString(TargetStepKey, string.Empty);
        if (string.IsNullOrEmpty(target)) return;
        if (!EditorApplication.isPlaying) return;

        var frames = SessionState.GetInt(FramesWaitedKey, 0) + 1;
        SessionState.SetInt(FramesWaitedKey, frames);
        if (frames < FramesBeforeAutoAdvance) return;

        var root = GameSessionRoot.Instance;
        if (root == null) return;
        if (root.HasBlockingError)
        {
            SessionState.EraseString(TargetStepKey);
            SessionState.EraseInt(FramesWaitedKey);
            Debug.LogError(
                $"[FirstPlayableSceneTest] Bootstrap 차단됨 — {root.LastBlockingError}\n" +
                "복구 메뉴 (SM/Internal/Content/Ensure Sample Content 등)로 먼저 환경을 정상화하세요.");
            return;
        }

        SessionState.EraseString(TargetStepKey);
        SessionState.EraseInt(FramesWaitedKey);

        if (!root.StartRealm(SessionRealm.OfflineLocal, out var error))
        {
            Debug.LogError($"[FirstPlayableSceneTest] StartRealm 실패: {error}");
            return;
        }
        root.ClearBlockingError();

        switch (target)
        {
            case StepTown:
                root.SceneFlow.GoToTown();
                Debug.Log("[FirstPlayableSceneTest] Town으로 자동 진입.");
                break;

            case StepExpedition:
                // Town을 거쳐서 Expedition으로 — Town presenter의 정상 시작 path를 그대로 따른다.
                root.SessionState.BeginNewExpedition();
                var checkpoint = root.SaveProfile(SessionCheckpointKind.TownExit);
                if (!checkpoint.IsSuccessful)
                {
                    Debug.LogError($"[FirstPlayableSceneTest] Expedition checkpoint 실패: {checkpoint.Message}");
                    return;
                }
                root.SceneFlow.GoToExpedition();
                Debug.Log("[FirstPlayableSceneTest] Expedition으로 자동 진입 (Town 거치지 않음 — checkpoint만 처리).");
                break;
        }
    }
}
