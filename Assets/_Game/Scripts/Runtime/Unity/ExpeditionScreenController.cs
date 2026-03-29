using System.Collections.Generic;
using System.Linq;
using System.Text;
using SM.Combat.Model;
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
    private DeploymentSetupPanelView? _deploymentPanel;

    private void Start()
    {
        _root = GameSessionRoot.Instance!;
        if (_root == null)
        {
            SetStatus("GameSessionRoot가 없습니다.");
            return;
        }

        _root.SessionState.SetCurrentScene(SceneNames.Expedition);
        EnsureRuntimeControls();
        Refresh();
    }

    public void SelectNode1() => SelectNode(0);
    public void SelectNode2() => SelectNode(1);
    public void SelectNode3() => SelectNode(2);
    public void SelectNode4() => SelectNode(3);
    public void SelectNode5() => SelectNode(4);

    public void NextBattleOrAdvance()
    {
        if (!EnsureReady()) return;

        var session = _root.SessionState;
        var selectedNode = session.GetSelectedExpeditionNode();
        if (selectedNode == null)
        {
            Refresh("다음 경로를 먼저 선택하세요.");
            return;
        }

        if (selectedNode.RequiresBattle)
        {
            session.EnsureBattleDeployReady();
            if (session.BattleDeployHeroIds.Count == 0)
            {
                Refresh("배치 가능한 영웅이 없습니다.");
                return;
            }

            _root.SceneFlow.GoToBattle();
            return;
        }

        if (session.ResolveSelectedExpeditionNode())
        {
            _root.SaveProfile();
            Refresh($"{selectedNode.Label} 경로를 전투 없이 정리했습니다.");
            return;
        }

        Refresh("다음 노드 진행에 실패했습니다.");
    }

    public void ReturnToTown()
    {
        if (!EnsureReady()) return;
        _root.SessionState.AbandonExpeditionRun();
        _root.SaveProfile();
        _root.SceneFlow.ReturnToTown();
    }

    public void EnsureRuntimeControls()
    {
        if (_deploymentPanel != null || statusText == null)
        {
            return;
        }

        var parent = statusText.transform.parent as RectTransform;
        if (parent == null)
        {
            return;
        }

        _deploymentPanel = DeploymentSetupPanelView.Create(
            "ExpeditionDeploymentPanel",
            parent,
            anchor => CycleAnchor(anchor),
            CycleTeamPosture);
    }

    public void CycleFrontTop() => CycleAnchor(DeploymentAnchorId.FrontTop);
    public void CycleFrontCenter() => CycleAnchor(DeploymentAnchorId.FrontCenter);
    public void CycleFrontBottom() => CycleAnchor(DeploymentAnchorId.FrontBottom);
    public void CycleBackTop() => CycleAnchor(DeploymentAnchorId.BackTop);
    public void CycleBackCenter() => CycleAnchor(DeploymentAnchorId.BackCenter);
    public void CycleBackBottom() => CycleAnchor(DeploymentAnchorId.BackBottom);

    public void CycleTeamPosture()
    {
        if (!EnsureReady()) return;
        _root.SessionState.CycleTeamPosture();
        Refresh($"Team posture: {_root.SessionState.SelectedTeamPosture}");
    }

    private void SelectNode(int nodeIndex)
    {
        if (!EnsureReady()) return;

        if (_root.SessionState.SelectNextExpeditionNode(nodeIndex))
        {
            Refresh($"경로 {nodeIndex + 1}을 선택했습니다.");
            return;
        }

        Refresh("현재 위치에서 선택할 수 없는 노드입니다.");
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
        var currentNode = session.GetCurrentExpeditionNode();
        var selectedNode = session.GetSelectedExpeditionNode();
        titleText.text = "Expedition Operator UI";
        positionText.text =
            $"현재 위치: {session.CurrentExpeditionNodeIndex + 1}/{session.ExpeditionNodes.Count}" +
            $" | 현재 노드: {currentNode?.Label ?? "-"}" +
            $" | 선택 경로: {selectedNode?.Label ?? "선택 필요"}";
        mapText.text = BuildMapText(session);
        rewardText.text = BuildRewardText(session);
        squadText.text = BuildSquadText(session);
        RefreshNodeTrack(session);
        _deploymentPanel?.Refresh(session);
        statusText.text = string.IsNullOrWhiteSpace(message)
            ? BuildDefaultStatus(session, selectedNode)
            : message;
    }

    private void RefreshNodeTrack(GameSessionState session)
    {
        var selectable = session.GetSelectableNextNodeIndices().ToHashSet();

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
            var button = nodeRoot.Find("SelectButton")?.GetComponent<Button>();
            var buttonLabel = nodeRoot.Find("SelectButton/Label")?.GetComponent<Text>();
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
                reward.text = $"{node.PlannedReward}\n{BuildNodeEffectTag(node)}";
            }

            if (image != null)
            {
                image.color = ResolveNodeColor(session, node, selectable);
            }

            if (button != null)
            {
                var isCurrent = node.Index == session.CurrentExpeditionNodeIndex;
                var isSelected = node.Index == session.SelectedExpeditionNodeIndex;
                var isSelectable = selectable.Contains(node.Index);
                button.gameObject.SetActive(isSelectable || isSelected);
                button.interactable = isSelectable;

                if (buttonLabel != null)
                {
                    buttonLabel.text = isSelected
                        ? "Selected"
                        : isCurrent
                            ? "Here"
                            : isSelectable
                                ? "Route"
                                : "Locked";
                }
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
            var marker = node.Index == session.CurrentExpeditionNodeIndex
                ? "[현재]"
                : node.Index == session.SelectedExpeditionNodeIndex
                    ? "[선택]"
                    : session.GetSelectableNextNodeIndices().Contains(node.Index)
                        ? "[후보]"
                        : node.Index < session.CurrentExpeditionNodeIndex
                            ? "[완료]"
                            : "[예정]";
            var battleMarker = node.RequiresBattle ? "Battle" : "Travel";
            sb.AppendLine($"- {marker} {node.Index + 1}. {node.Label} / {battleMarker}");
            sb.AppendLine($"  {node.Description}");
        }

        return sb.ToString();
    }

    private static string BuildRewardText(GameSessionState session)
    {
        var sb = new StringBuilder();
        var selected = session.GetSelectedExpeditionNode();
        sb.AppendLine("선택 경로 / 노드 효과");
        if (selected != null)
        {
            sb.AppendLine($"{selected.Label}");
            sb.AppendLine($"- 예정 보상: {selected.PlannedReward}");
            sb.AppendLine($"- 노드 효과: {BuildNodeEffectTag(selected)}");
            sb.AppendLine($"- 설명: {selected.Description}");
        }
        else
        {
            sb.AppendLine("아직 선택된 분기가 없습니다.");
        }

        if (!string.IsNullOrWhiteSpace(session.LastExpeditionEffectMessage))
        {
            sb.AppendLine();
            sb.AppendLine($"직전 적용 효과: {session.LastExpeditionEffectMessage}");
        }

        return sb.ToString();
    }

    private static string BuildSquadText(GameSessionState session)
    {
        var names = session.ExpeditionSquadHeroIds
            .Select(id => session.Profile.Heroes.FirstOrDefault(h => h.HeroId == id)?.Name ?? id);
        var tempAugments = session.Expedition.TemporaryAugmentIds.Count == 0
            ? "없음"
            : string.Join(", ", session.Expedition.TemporaryAugmentIds);
        var deploymentSummary = string.Join(
            "\n",
            session.EnumerateDeploymentAssignments().Select(entry =>
            {
                var heroName = session.Profile.Heroes.FirstOrDefault(hero => hero.HeroId == entry.HeroId)?.Name ?? "Empty";
                return $"{entry.Anchor.ToDisplayName()}: {heroName}";
            }));

        return "현재 원정 스쿼드\n" +
               string.Join("\n", names) +
               $"\n\nTeam Posture\n{session.SelectedTeamPosture}" +
               $"\n\nDeployment\n{deploymentSummary}" +
               $"\n\nTemp Augments\n{tempAugments}";
    }

    private static string BuildDefaultStatus(GameSessionState session, ExpeditionNodeViewModel? selectedNode)
    {
        if (selectedNode == null)
        {
            return "분기를 고른 뒤 Next Battle로 진행하세요.";
        }

        return selectedNode.RequiresBattle
            ? $"{selectedNode.Label} 전투에 진입할 준비가 됐습니다."
            : $"{selectedNode.Label}은 전투 없이 정리 가능한 안전 노드입니다.";
    }

    private static string BuildNodeEffectTag(ExpeditionNodeViewModel node)
    {
        return node.EffectKind switch
        {
            ExpeditionNodeEffectKind.None => "효과 없음",
            ExpeditionNodeEffectKind.Gold => $"+{node.EffectAmount} Gold",
            ExpeditionNodeEffectKind.TraitRerollCurrency => $"Trait Reroll +{node.EffectAmount}",
            ExpeditionNodeEffectKind.TemporaryAugment => $"Temp Augment: {node.EffectPayloadId}",
            ExpeditionNodeEffectKind.PermanentAugmentSlot => $"Permanent Slot +{Mathf.Max(1, node.EffectAmount)}",
            _ => "효과 없음"
        };
    }

    private static Color ResolveNodeColor(GameSessionState session, ExpeditionNodeViewModel node, HashSet<int> selectable)
    {
        if (node.Index == session.CurrentExpeditionNodeIndex)
        {
            return new Color(0.88f, 0.66f, 0.24f, 0.95f);
        }

        if (node.Index == session.SelectedExpeditionNodeIndex)
        {
            return new Color(0.22f, 0.72f, 0.90f, 0.96f);
        }

        if (selectable.Contains(node.Index))
        {
            return new Color(0.24f, 0.46f, 0.82f, 0.95f);
        }

        if (node.Index < session.CurrentExpeditionNodeIndex)
        {
            return new Color(0.26f, 0.58f, 0.34f, 0.95f);
        }

        return new Color(0.18f, 0.22f, 0.34f, 0.95f);
    }

    private void CycleAnchor(DeploymentAnchorId anchor)
    {
        if (!EnsureReady()) return;
        _root.SessionState.CycleDeploymentAssignment(anchor);
        Refresh($"{anchor.ToDisplayName()} 배치를 갱신했습니다.");
    }
}
