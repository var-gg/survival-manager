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

    /// <summary>
    /// pindoc://decision-expedition-screen-deprecation-atlas-absorption — Atlas screen이 5-node site track
    /// UI surface를 흡수. 본 메서드는 transition 단계 동안 유지되며, ExpeditionScreenController.Start가
    /// 자동 NextBattleOrAdvance로 즉시 Battle/Reward로 진행시킨다. 다음 sprint에 호출자 (현재
    /// AtlasScreenController.ContinueToExpedition) 모두 GoToBattle 또는 in-Atlas confirm으로 redirect 후 제거.
    /// </summary>
    [System.Obsolete("Expedition scene UI surface는 Atlas screen으로 흡수됨 — decision-expedition-screen-deprecation-atlas-absorption. 다음 sprint에 호출자 GoToBattle redirect + 본 메서드 제거.")]
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
