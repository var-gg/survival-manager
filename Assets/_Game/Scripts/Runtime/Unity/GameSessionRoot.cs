using UnityEngine;
using UnityEngine.SceneManagement;

namespace SM.Unity;

public sealed class GameSessionRoot : MonoBehaviour
{
    public static GameSessionRoot? Instance { get; private set; }

    public RuntimeCombatContentLookup CombatContentLookup { get; private set; } = null!;
    public GameSessionState SessionState { get; private set; } = null!;
    public PersistenceEntryPoint Persistence { get; private set; } = null!;
    public SceneFlowController SceneFlow { get; private set; } = null!;
    public string? LastBlockingError { get; private set; }
    public bool HasBlockingError => !string.IsNullOrWhiteSpace(LastBlockingError);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        CombatContentLookup = new RuntimeCombatContentLookup();
        SessionState = new GameSessionState(CombatContentLookup);
        Persistence = new PersistenceEntryPoint();
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
    }

    public void BindProfile()
    {
        var profile = Persistence.Repository.LoadOrCreate(Persistence.Config.ProfileId);
        SessionState.BindProfile(profile);
    }

    public void SaveProfile()
    {
        Persistence.Repository.Save(SessionState.Profile);
    }

    public void SetBlockingError(string message)
    {
        LastBlockingError = message;
        Debug.LogError(message);
    }

    public void ClearBlockingError()
    {
        LastBlockingError = null;
    }
}
