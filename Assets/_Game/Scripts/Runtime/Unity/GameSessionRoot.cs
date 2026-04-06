using SM.Meta.Model;
using SM.Meta.Services;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SM.Unity;

public sealed class GameSessionRoot : MonoBehaviour
{
    public static GameSessionRoot? Instance { get; private set; }

    public RuntimeCombatContentLookup CombatContentLookup { get; private set; } = null!;
    public GameSessionState SessionState { get; private set; } = null!;
    public SessionRealmCoordinator Sessions { get; private set; } = null!;
    public SceneFlowController SceneFlow { get; private set; } = null!;
    public GameLocalizationController Localization { get; private set; } = null!;
    public string? LastBlockingError { get; private set; }
    public bool HasBlockingError => !string.IsNullOrWhiteSpace(LastBlockingError);
    public bool HasActiveSession => Sessions.HasActiveSession;
    public SessionRealm? CurrentRealm => Sessions.CurrentRealm;
    public SessionCapabilities CurrentCapabilities => Sessions.CurrentCapabilities;
    public string ActiveProfileId => Sessions.ActiveProfileId;
    public IProfileQueryService ProfileQueries => Sessions;
    public IProfileCommandService ProfileCommands => Sessions;
    public IArenaQueryService ArenaQueries => Sessions;
    public IArenaCommandService ArenaCommands => Sessions;
    public IBattleAuthority BattleAuthority => Sessions;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        Localization = GetComponent<GameLocalizationController>() ?? gameObject.AddComponent<GameLocalizationController>();
        CombatContentLookup = new RuntimeCombatContentLookup();
        SessionState = new GameSessionState(CombatContentLookup);
        Sessions = new SessionRealmCoordinator(SessionState, new PersistenceEntryPoint());
        SceneFlow = new SceneFlowController(this, SessionState);
        FirstPlayableRuntimeSceneBinder.EnsureSceneBindings(SceneManager.GetActiveScene());
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        FirstPlayableRuntimeSceneBinder.EnsureSceneBindings(scene);
        if (Instance?.Localization.IsInitialized == true)
        {
            FirstPlayableRuntimeSceneBinder.RefreshLocalizedBindings(scene);
        }
    }

    public void BindProfile()
    {
        Sessions.ReloadActiveSession();
    }

    public void SaveProfile()
    {
        Sessions.SaveActiveSession();
    }

    public bool StartRealm(SessionRealm realm, out string error)
    {
        return Sessions.StartRealm(realm, out error);
    }

    public bool CanStartRealm(SessionRealm realm, out string reason)
    {
        return Sessions.CanStartRealm(realm, out reason);
    }

    public void EnsureOfflineLocalSession()
    {
        Sessions.EnsureOfflineLocalSession();
    }

    public void ReturnToSessionMenu()
    {
        SaveProfile();
        Sessions.EndSession();
        SceneFlow.GoToBoot();
    }

    public void SetBlockingError(string message)
    {
        LastBlockingError = message;
        Debug.LogError(message);
    }

    public void SetBlockingError(string tableCollection, string entryKey, string fallback, params object[] arguments)
    {
        SetBlockingError(Localization.LocalizeOrFallback(tableCollection, entryKey, fallback, arguments));
    }

    public void ClearBlockingError()
    {
        LastBlockingError = null;
    }

    /// <summary>
    /// Returns existing Instance or creates a minimal emergency root.
    /// Use when a scene is played directly without going through Boot.
    /// </summary>
    public static GameSessionRoot EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        Debug.LogWarning("[GameSessionRoot] Boot 씬을 거치지 않고 직접 실행됨. 최소 초기화를 수행합니다.");
        var go = new GameObject("GameSessionRoot");
        var root = go.AddComponent<GameSessionRoot>();
        if (SessionRealmAutoStartPolicy.ShouldForceOfflineLocalForScene(SceneManager.GetActiveScene().name))
        {
            root.EnsureOfflineLocalSession();
        }

        return root;
    }
}
