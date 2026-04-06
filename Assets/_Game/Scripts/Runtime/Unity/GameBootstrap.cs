using System.Collections;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using System.IO;
#endif

namespace SM.Unity;

public sealed class GameBootstrap : MonoBehaviour
{
    private const string ResourcesDefinitionsStatsPath = "_Game/Content/Definitions/Stats";
#if UNITY_EDITOR
    private const string EditorDefinitionsStatsPath = "Assets/Resources/_Game/Content/Definitions/Stats";
#endif

    private bool _hasRun;
    private Coroutine? _bootstrapRoutine;

    private void Start()
    {
        RunBootstrap();
    }

    public void RunBootstrap()
    {
        if (_hasRun || _bootstrapRoutine != null)
        {
            return;
        }

        _bootstrapRoutine = StartCoroutine(RunBootstrapRoutine());
    }

    private IEnumerator RunBootstrapRoutine()
    {
        var root = EnsureRoot();
        yield return root.Localization.EnsureInitialized();

        _hasRun = true;
        root.SessionState.SetCurrentScene(SceneNames.Boot);
        FirstPlayableRuntimeSceneBinder.RefreshLocalizedBindings(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        if (!HasSeedContent())
        {
            HandleMissingSampleContent(root);
            RefreshBootScreen();
            _bootstrapRoutine = null;
            yield break;
        }

        root.ClearBlockingError();

        if (SessionRealmAutoStartPolicy.ShouldForceOfflineLocalForQuickBattle(ConsumeQuickBattleRequest()))
        {
            root.EnsureOfflineLocalSession();
            root.SessionState.PrepareQuickBattleSmoke();
            root.SaveProfile();
            root.SceneFlow.GoToBattle();
        }
        else
        {
            RefreshBootScreen();
        }

        _bootstrapRoutine = null;
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

    private static bool ConsumeQuickBattleRequest()
    {
#if UNITY_EDITOR
        if (!EditorPrefs.GetBool("SM.QuickBattleRequested", false))
        {
            return false;
        }

        EditorPrefs.DeleteKey("SM.QuickBattleRequested");
        Debug.Log("[GameBootstrap] Quick Battle 요청 감지. Town을 건너뛰고 Battle로 직행합니다.");
        return true;
#else
        return false;
#endif
    }

    private static bool HasSeedContent()
    {
        var stats = Resources.LoadAll<Object>(ResourcesDefinitionsStatsPath);
        if (stats != null && stats.Length > 0)
        {
            return true;
        }

#if UNITY_EDITOR
        AssetDatabase.Refresh();
        return Directory.Exists(EditorDefinitionsStatsPath) && Directory.GetFiles(EditorDefinitionsStatsPath, "*.asset").Length > 0;
#else
        return false;
#endif
    }

    private static void HandleMissingSampleContent(GameSessionRoot root)
    {
#if UNITY_EDITOR
        const string fallback = "샘플 콘텐츠 canonical root가 비어 있습니다. 먼저 SM/Bootstrap/Ensure Sample Content를 실행하고, 복구가 안 되면 SM/Seed/Generate Sample Content를 repair 용도로 실행한 뒤 다시 Play 하세요. 계약 경로: Assets/Resources/_Game/Content/Definitions/**";
        root.SetBlockingError(GameLocalizationTables.SystemMessages, "system.bootstrap.missing_sample_content.editor", fallback);
        Debug.LogWarning(root.LastBlockingError);
        EditorApplication.isPaused = true;
#else
        const string fallback = "필수 샘플 콘텐츠 canonical root가 비어 있어 시작할 수 없습니다. Resources content contract를 확인하세요.";
        root.SetBlockingError(GameLocalizationTables.SystemMessages, "system.bootstrap.missing_sample_content.player", fallback);
#endif
    }

    private static void RefreshBootScreen()
    {
        FindAnyObjectByType<BootScreenController>()?.Refresh();
    }
}
