using SM.Meta.Model;
using SM.Meta.Services;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

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
    public string ActiveProfileId => Sessions.ActiveProfileId;
    public SessionCheckpointResult LastCheckpointResult => Sessions.LastCheckpointResult;
    public bool IsTransientTownSmokeActive => Sessions.IsTransientTownSmokeActive;
    public bool IsDedicatedSmokeNamespace => Sessions.IsDedicatedSmokeNamespace;
    public IProfileQueryService ProfileQueries => Sessions;
    public IProfileCommandService ProfileCommands => Sessions;

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

    public SessionCheckpointResult BindProfile(SessionCheckpointKind kind = SessionCheckpointKind.ManualLoad)
    {
        var result = Sessions.ReloadActiveSession(kind);
        HandleCheckpointResult(result, blockOnFailure: true);
        return result;
    }

    public SessionCheckpointResult SaveProfile(SessionCheckpointKind kind = SessionCheckpointKind.ManualSave)
    {
        var result = Sessions.SaveActiveSession(kind);
        HandleCheckpointResult(result, blockOnFailure: false);
        return result;
    }

    public bool StartRealm(SessionRealm realm, out string error)
    {
        var started = Sessions.StartRealm(realm, out error);
        if (started)
        {
            ClearBlockingError();
            return true;
        }

        if (!string.IsNullOrWhiteSpace(error))
        {
            SetBlockingError(error);
        }

        return false;
    }

    public void EnsureOfflineLocalSession()
    {
        Sessions.EnsureOfflineLocalSession();
    }

    public void UseDedicatedSmokeNamespace()
    {
        Sessions.UseDedicatedSmokeNamespace();
    }

    public void BeginTransientTownSmoke()
    {
        Sessions.BeginTransientTownSmoke();
    }

    public SessionCheckpointResult RestoreCanonicalProfileAfterTransientSmoke()
    {
        Sessions.ReturnToCanonicalLane();
        var result = BindProfile(SessionCheckpointKind.QuickBattleRestore);
        if (result.IsSuccessful)
        {
            SessionState.RecordOperationalTelemetry(RuntimeOperationalTelemetry.CreateSmokeRestoreFromDisk(
                ActiveProfileId,
                ActiveProfileId,
                "Transient Town smoke overlay discarded and canonical profile restored."));
        }

        return result;
    }

    public SessionCheckpointResult ReturnToSessionMenu()
    {
        var result = SaveProfile(SessionCheckpointKind.ReturnToStart);
        if (!result.IsSuccessful)
        {
            return result;
        }

        Sessions.EndSession();
        SceneFlow.GoToBoot();
        return result;
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

    public static GameSessionRoot EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        Debug.LogWarning("[GameSessionRoot] Boot authored loop를 거치지 않은 direct-scene debug fallback을 수행합니다.");
        var go = new GameObject("GameSessionRoot");
        var root = go.AddComponent<GameSessionRoot>();
        if (SessionRealmAutoStartPolicy.ShouldForceOfflineLocalForScene(SceneManager.GetActiveScene().name))
        {
#if UNITY_EDITOR
            if (EditorPrefs.GetBool("SM.QuickBattleRequested", false))
            {
                root.UseDedicatedSmokeNamespace();
            }
#endif
            root.EnsureOfflineLocalSession();
        }

        return root;
    }

    private void HandleCheckpointResult(SessionCheckpointResult result, bool blockOnFailure)
    {
        if (result.IsSuccessful)
        {
            if (blockOnFailure)
            {
                ClearBlockingError();
            }

            return;
        }

        if (result.Status == SessionCheckpointStatus.Blocked)
        {
            return;
        }

        if (blockOnFailure)
        {
            SetBlockingError(result.Message);
            return;
        }

        if (!string.IsNullOrWhiteSpace(result.Message))
        {
            Debug.LogError(result.Message);
        }
    }
}
