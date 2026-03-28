using UnityEngine;

namespace SM.Unity;

public sealed class GameSessionRoot : MonoBehaviour
{
    public static GameSessionRoot? Instance { get; private set; }

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

        SessionState = new GameSessionState();
        Persistence = new PersistenceEntryPoint();
        SceneFlow = new SceneFlowController(this, SessionState);
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
