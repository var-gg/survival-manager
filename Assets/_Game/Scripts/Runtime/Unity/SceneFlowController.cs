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
    // GoToExpedition()은 decision-expedition-screen-deprecation-atlas-absorption (2단계, 2026-05-18)으로 제거.
    // AtlasScreenController.ContinueToExpedition가 NextBattleOrAdvance 분기를 inline 처리해 Battle/Reward로 직진입.
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
