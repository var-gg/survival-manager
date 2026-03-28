using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace SM.Unity;

public sealed class ExpeditionScreenController : MonoBehaviour
{
    [SerializeField] private Text titleText = null!;
    [SerializeField] private Text mapText = null!;
    [SerializeField] private Text positionText = null!;
    [SerializeField] private Text rewardText = null!;
    [SerializeField] private Text squadText = null!;
    [SerializeField] private Text statusText = null!;

    private GameSessionRoot _root = null!;

    private void Start()
    {
        _root = GameSessionRoot.Instance!;
        if (_root == null)
        {
            statusText.text = "GameSessionRoot가 없습니다.";
            return;
        }

        _root.SessionState.SetCurrentScene(SceneNames.Expedition);
        Refresh();
    }

    public void NextBattleOrAdvance()
    {
        var session = _root.SessionState;
        if (session.CurrentExpeditionNodeIndex >= session.ExpeditionNodes.Count - 1)
        {
            statusText.text = "마지막 노드입니다. 귀환하세요.";
            return;
        }

        _root.SceneFlow.GoToBattle();
    }

    public void ReturnToTown()
    {
        _root.SceneFlow.ReturnToTown();
    }

    private void Refresh(string message = "")
    {
        var session = _root.SessionState;
        titleText.text = "Expedition Debug UI";
        positionText.text = $"현재 위치: {session.CurrentExpeditionNodeIndex + 1}/{session.ExpeditionNodes.Count}";
        mapText.text = BuildMapText(session);
        rewardText.text = BuildRewardText(session);
        squadText.text = BuildSquadText(session);
        statusText.text = string.IsNullOrWhiteSpace(message) ? "다음 전투를 시작하거나 귀환하세요." : message;
    }

    private static string BuildMapText(GameSessionState session)
    {
        var sb = new StringBuilder();
        sb.AppendLine("5노드 분기형 맵");
        foreach (var node in session.ExpeditionNodes)
        {
            var marker = node.Index == session.CurrentExpeditionNodeIndex ? "[현재]" : node.Index < session.CurrentExpeditionNodeIndex ? "[완료]" : "[예정]";
            sb.AppendLine($"- {marker} {node.Index + 1}. {node.Label}");
        }
        return sb.ToString();
    }

    private static string BuildRewardText(GameSessionState session)
    {
        var remaining = session.ExpeditionNodes.Where(x => x.Index >= session.CurrentExpeditionNodeIndex)
            .Select(x => $"{x.Index + 1}. {x.PlannedReward}");
        return "예정 보상\n" + string.Join("\n", remaining);
    }

    private static string BuildSquadText(GameSessionState session)
    {
        var names = session.ExpeditionSquadHeroIds
            .Select(id => session.Profile.Heroes.FirstOrDefault(h => h.HeroId == id)?.Name ?? id);
        return "현재 원정 스쿼드\n" + string.Join("\n", names);
    }
}
