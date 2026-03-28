using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace SM.Unity;

public sealed class RewardScreenController : MonoBehaviour
{
    [SerializeField] private Text titleText = null!;
    [SerializeField] private Text summaryText = null!;
    [SerializeField] private Text choicesText = null!;
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

        _root.SessionState.SetCurrentScene(SceneNames.Reward);
        Refresh();
    }

    public void Choose0() => Choose(0);
    public void Choose1() => Choose(1);
    public void Choose2() => Choose(2);

    public void ReturnToTown()
    {
        _root.SaveProfile();
        _root.SceneFlow.ReturnToTown();
    }

    private void Choose(int index)
    {
        if (_root.SessionState.ApplyRewardChoice(index))
        {
            _root.SaveProfile();
            Refresh($"보상 {index + 1}을 선택했고 저장까지 반영했습니다.");
            return;
        }

        Refresh("보상 선택에 실패했습니다.");
    }

    private void Refresh(string message = "")
    {
        var session = _root.SessionState;
        titleText.text = "Reward Debug UI";
        summaryText.text = $"전투 결과: {(session.LastBattleVictory ? "승리" : "패배")}\n{session.LastBattleSummary}\nGold: {session.Profile.Currencies.Gold}\nInventory: {session.Profile.Inventory.Count}\nTemp Augments: {session.Expedition.TemporaryAugmentIds.Count}";
        choicesText.text = BuildChoices(session);
        statusText.text = string.IsNullOrWhiteSpace(message) ? "3지선다 중 하나를 고른 뒤 Town으로 돌아가세요." : message;
    }

    private static string BuildChoices(GameSessionState session)
    {
        var sb = new StringBuilder();
        sb.AppendLine("보상 카드");
        for (var i = 0; i < session.PendingRewardChoices.Count; i++)
        {
            var choice = session.PendingRewardChoices[i];
            sb.AppendLine($"{i + 1}. {choice.Title} / {choice.Description} / {choice.Kind}");
        }
        return sb.ToString();
    }
}
