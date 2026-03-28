using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SM.Unity;

public sealed class GameBootstrap : MonoBehaviour
{
    [SerializeField] private bool autoEnterTown = true;

    private void Start()
    {
        var root = EnsureRoot();
        root.SessionState.SetCurrentScene(SceneNames.Boot);
        root.BindProfile();

        if (!HasSeedContent())
        {
            HandleMissingSampleContent(root);
            return;
        }

        root.ClearBlockingError();

        if (autoEnterTown)
        {
            root.SceneFlow.GoToTown();
        }
    }

    private static GameSessionRoot EnsureRoot()
    {
        if (GameSessionRoot.Instance != null)
        {
            return GameSessionRoot.Instance;
        }

        var go = new GameObject("GameSessionRoot");
        return go.AddComponent<GameSessionRoot>();
    }

    private static bool HasSeedContent()
    {
        var stats = Resources.LoadAll<Object>("_Game/Content/Definitions/Stats");
        return stats != null && stats.Length > 0;
    }

    private static void HandleMissingSampleContent(GameSessionRoot root)
    {
#if UNITY_EDITOR
        const string message = "샘플 콘텐츠가 없습니다. SM/Seed/Generate Sample Content를 먼저 실행한 뒤 다시 Play 하세요.";
        root.SetBlockingError(message);
        Debug.LogWarning(message);
        EditorApplication.isPaused = true;
#else
        root.SetBlockingError("필수 샘플 콘텐츠가 없어 시작할 수 없습니다. 콘텐츠 데이터를 확인하세요.");
#endif
    }
}
