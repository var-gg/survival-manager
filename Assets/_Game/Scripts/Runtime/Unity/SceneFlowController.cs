using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SM.Unity;

public sealed class SceneFlowController
{
    private readonly MonoBehaviour _host;
    private readonly GameSessionState _sessionState;

    public SceneFlowController(MonoBehaviour host, GameSessionState sessionState)
    {
        _host = host;
        _sessionState = sessionState;
    }

    public void GoToBoot() => Load(SceneNames.Boot);
    public void GoToTown() => Load(SceneNames.Town);
    public void GoToAtlas() => Load(SceneNames.Atlas);
    public void GoToExpedition() => Load(SceneNames.Expedition);
    public void GoToBattle() => Load(SceneNames.Battle);
    public void GoToReward() => Load(SceneNames.Reward);
    public void ReturnToTown() => Load(SceneNames.Town);

    public void Load(string sceneName)
    {
        _host.StartCoroutine(LoadRoutine(sceneName));
    }

    private IEnumerator LoadRoutine(string sceneName)
    {
        var operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        while (operation is { isDone: false })
        {
            yield return null;
        }

        _sessionState.SetCurrentScene(sceneName);
    }
}
