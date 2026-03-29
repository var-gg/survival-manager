using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace SM.Unity;

public sealed class ExpeditionScreenController : MonoBehaviour
{
    [SerializeField] private Text titleText = null!;
    [SerializeField] private Text mapText = null!;
    [SerializeField] private RectTransform nodeTrackRoot = null!;
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
            SetStatus("GameSessionRoot가 없습니다.");
            return;
        }

        _root.SessionState.SetCurrentScene(SceneNames.Expedition);
        Refresh();
    }

    public void NextBattleOrAdvance()
    {
        if (!EnsureReady()) return;

        var session = _root.SessionState;
        session.EnsureBattleDeployReady();
        if (session.BattleDeployHeroIds.Count == 0)
        {
            Refresh("배치 가능한 영웅이 없습니다.");
            return;
        }

        if (session.CurrentExpeditionNodeIndex >= session.ExpeditionNodes.Count - 1)
        {
            Refresh("마지막 노드입니다. 귀환하세요.");
            return;
        }

        _root.SceneFlow.GoToBattle();
    }

    public void ReturnToTown()
    {
        if (!EnsureReady()) return;
        _root.SessionState.EndOperatorRunToTown();
        _root.SaveProfile();
        _root.SceneFlow.ReturnToTown();
    }

    private bool EnsureReady()
    {
        ValidateReferences();
        if (_root != null)
        {
            return true;
        }

        _root = GameSessionRoot.Instance!;
        if (_root == null)
        {
            SetStatus("GameSessionRoot가 없습니다.");
            return false;
        }

        return true;
    }

    private void ValidateReferences()
    {
        AssertText(titleText, nameof(titleText));
        AssertText(mapText, nameof(mapText));
        AssertRect(nodeTrackRoot, nameof(nodeTrackRoot));
        AssertText(positionText, nameof(positionText));
        AssertText(rewardText, nameof(rewardText));
        AssertText(squadText, nameof(squadText));
        AssertText(statusText, nameof(statusText));
    }

    private static void AssertText(Text text, string fieldName)
    {
        if (text == null)
        {
            Debug.LogError($"[ExpeditionScreenController] Missing Text reference: {fieldName}");
        }
    }

    private static void AssertRect(RectTransform rectTransform, string fieldName)
    {
        if (rectTransform == null)
        {
            Debug.LogError($"[ExpeditionScreenController] Missing RectTransform reference: {fieldName}");
        }
    }

    private void Refresh(string message = "")
    {
        if (!EnsureReady()) return;

        var session = _root.SessionState;
        titleText.text = "Expedition Operator UI";
        positionText.text = $"현재 위치: {session.CurrentExpeditionNodeIndex + 1}/{session.ExpeditionNodes.Count}";
        mapText.text = BuildMapText(session);
        rewardText.text = BuildRewardText(session);
        squadText.text = BuildSquadText(session);
        RefreshNodeTrack(session);
        statusText.text = string.IsNullOrWhiteSpace(message)
            ? "현재 노드를 보고 Next Battle 또는 Return Town을 선택하세요."
            : message;
    }

    private void RefreshNodeTrack(GameSessionState session)
    {
        for (var i = 0; i < 5; i++)
        {
            var nodeRoot = nodeTrackRoot.Find($"NodeBox{i + 1}");
            if (nodeRoot == null)
            {
                continue;
            }

            var image = nodeRoot.GetComponent<Image>();
            var label = nodeRoot.Find("TitleText")?.GetComponent<Text>();
            var reward = nodeRoot.Find("RewardText")?.GetComponent<Text>();
            if (i >= session.ExpeditionNodes.Count)
            {
                nodeRoot.gameObject.SetActive(false);
                continue;
            }

            nodeRoot.gameObject.SetActive(true);
            var node = session.ExpeditionNodes[i];
            if (label != null)
            {
                label.text = $"{i + 1}. {node.Label}";
            }

            if (reward != null)
            {
                reward.text = node.PlannedReward;
            }

            if (image != null)
            {
                image.color = node.Index == session.CurrentExpeditionNodeIndex
                    ? new Color(0.88f, 0.66f, 0.24f, 0.95f)
                    : node.Index < session.CurrentExpeditionNodeIndex
                        ? new Color(0.26f, 0.58f, 0.34f, 0.95f)
                        : new Color(0.18f, 0.22f, 0.34f, 0.95f);
            }
        }
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }

        Debug.LogError($"[ExpeditionScreenController] {message}");
    }

    private static string BuildMapText(GameSessionState session)
    {
        var sb = new StringBuilder();
        sb.AppendLine("5노드 운영자 맵");
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
