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
    private GameLocalizationController _localization = null!;
    private ContentTextResolver _contentText = null!;
    private DeploymentSetupPanelView? _deploymentPanel;

    private void Start()
    {
        _root = GameSessionRoot.EnsureInstance();
        if (_root == null)
        {
            SetStatus("GameSessionRoot가 없습니다.");
            return;
        }

        _localization = _root.Localization;
        _contentText = new ContentTextResolver(_localization, _root.CombatContentLookup);
        _localization.LocaleChanged += HandleLocaleChanged;
        _root.SessionState.SetCurrentScene(SceneNames.Expedition);
        EnsureRuntimeControls();
        Refresh();
    }

    private void OnDestroy()
    {
        if (_localization != null)
        {
            _localization.LocaleChanged -= HandleLocaleChanged;
        }
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
            Refresh(Localize(GameLocalizationTables.UIExpedition, "ui.expedition.status.select_route_first", "Select the next route first."));
            return;
        }

        if (selectedNode.RequiresBattle)
        {
            session.EnsureBattleDeployReady();
            if (session.BattleDeployHeroIds.Count == 0)
            {
                Refresh(Localize(GameLocalizationTables.UIExpedition, "ui.expedition.error.no_deployable_heroes", "No hero is available for deployment."));
                return;
            }

            _root.SceneFlow.GoToBattle();
            return;
        }

        if (session.ResolveSelectedExpeditionNode())
        {
            _root.SaveProfile();
            Refresh(Localize(GameLocalizationTables.UIExpedition, "ui.expedition.status.node_cleared", "{0} was resolved without a battle.", ResolveNodeLabel(selectedNode)));
            return;
        }

        Refresh(Localize(GameLocalizationTables.UIExpedition, "ui.expedition.error.advance_failed", "Failed to advance the selected node."));
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
        Refresh(Localize(GameLocalizationTables.UIExpedition, "ui.expedition.status.team_posture", "Team posture: {0}", _root.SessionState.SelectedTeamPosture));
    }

    private void SelectNode(int nodeIndex)
    {
        if (!EnsureReady()) return;

        if (_root.SessionState.SelectNextExpeditionNode(nodeIndex))
        {
            Refresh(Localize(GameLocalizationTables.UIExpedition, "ui.expedition.status.route_selected", "Route {0} selected.", nodeIndex + 1));
            return;
        }

        Refresh(Localize(GameLocalizationTables.UIExpedition, "ui.expedition.error.invalid_route", "That node cannot be selected from the current position."));
    }

    private bool EnsureReady()
    {
        ValidateReferences();
        if (_root != null)
        {
            return true;
        }

        _root = GameSessionRoot.EnsureInstance();
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
        titleText.text = Localize(GameLocalizationTables.UIExpedition, "ui.expedition.title", "Expedition Operator UI");
        positionText.text =
            Localize(
                GameLocalizationTables.UIExpedition,
                "ui.expedition.position.summary",
                "Position: {0}/{1} | Current: {2} | Selected: {3}",
                session.CurrentExpeditionNodeIndex + 1,
                session.ExpeditionNodes.Count,
                currentNode == null ? "-" : ResolveNodeLabel(currentNode),
                selectedNode == null ? Localize(GameLocalizationTables.UIExpedition, "ui.expedition.position.none", "Selection Needed") : ResolveNodeLabel(selectedNode));
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
                label.text = $"{i + 1}. {ResolveNodeLabel(node)}";
            }

            if (reward != null)
            {
                reward.text = $"{ResolveNodeReward(node)}\n{BuildNodeEffectTag(node)}";
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
                        ? Localize(GameLocalizationTables.UICommon, "ui.common.selected", "Selected")
                        : isCurrent
                            ? Localize(GameLocalizationTables.UICommon, "ui.common.here", "Here")
                            : isSelectable
                                ? Localize(GameLocalizationTables.UIExpedition, "ui.expedition.action.route", "Route")
                                : Localize(GameLocalizationTables.UICommon, "ui.common.locked", "Locked");
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

    private string BuildMapText(GameSessionState session)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Localize(GameLocalizationTables.UIExpedition, "ui.expedition.map.header", "Five-node operator map"));
        foreach (var node in session.ExpeditionNodes)
        {
            var marker = node.Index == session.CurrentExpeditionNodeIndex
                ? Localize(GameLocalizationTables.UIExpedition, "ui.expedition.map.marker.current", "[Current]")
                : node.Index == session.SelectedExpeditionNodeIndex
                    ? Localize(GameLocalizationTables.UIExpedition, "ui.expedition.map.marker.selected", "[Selected]")
                    : session.GetSelectableNextNodeIndices().Contains(node.Index)
                        ? Localize(GameLocalizationTables.UIExpedition, "ui.expedition.map.marker.candidate", "[Candidate]")
                        : node.Index < session.CurrentExpeditionNodeIndex
                            ? Localize(GameLocalizationTables.UIExpedition, "ui.expedition.map.marker.completed", "[Done]")
                            : Localize(GameLocalizationTables.UIExpedition, "ui.expedition.map.marker.upcoming", "[Upcoming]");
            var battleMarker = node.RequiresBattle
                ? Localize(GameLocalizationTables.UIExpedition, "ui.expedition.map.battle", "Battle")
                : Localize(GameLocalizationTables.UIExpedition, "ui.expedition.map.travel", "Travel");
            sb.AppendLine($"- {marker} {node.Index + 1}. {ResolveNodeLabel(node)} / {battleMarker}");
            sb.AppendLine($"  {ResolveNodeDescription(node)}");
        }

        return sb.ToString();
    }

    private string BuildRewardText(GameSessionState session)
    {
        var sb = new StringBuilder();
        var selected = session.GetSelectedExpeditionNode();
        sb.AppendLine(Localize(GameLocalizationTables.UIExpedition, "ui.expedition.reward.header", "Selected Route / Node Effect"));
        if (selected != null)
        {
            sb.AppendLine(ResolveNodeLabel(selected));
            sb.AppendLine(Localize(GameLocalizationTables.UIExpedition, "ui.expedition.reward.planned", "- Planned Reward: {0}", ResolveNodeReward(selected)));
            sb.AppendLine(Localize(GameLocalizationTables.UIExpedition, "ui.expedition.reward.effect", "- Node Effect: {0}", BuildNodeEffectTag(selected)));
            sb.AppendLine(Localize(GameLocalizationTables.UIExpedition, "ui.expedition.reward.description", "- Description: {0}", ResolveNodeDescription(selected)));
        }
        else
        {
            sb.AppendLine(Localize(GameLocalizationTables.UIExpedition, "ui.expedition.reward.none", "No branch is selected yet."));
        }

        if (session.LastExpeditionEffectMessage.HasValue)
        {
            sb.AppendLine();
            sb.AppendLine(Localize(
                GameLocalizationTables.UIExpedition,
                "ui.expedition.reward.last_effect",
                "Last Applied Effect: {0}",
                session.LastExpeditionEffectMessage.Resolve(_localization, _contentText)));
        }

        return sb.ToString();
    }

    private string BuildSquadText(GameSessionState session)
    {
        var names = session.ExpeditionSquadHeroIds
            .Select(id => session.Profile.Heroes.FirstOrDefault(h => h.HeroId == id)?.Name ?? id);
        var tempAugments = session.Expedition.TemporaryAugmentIds.Count == 0
            ? Localize(GameLocalizationTables.UICommon, "ui.common.none", "None")
            : string.Join(", ", session.Expedition.TemporaryAugmentIds.Select(_contentText.GetAugmentName));
        var deploymentSummary = string.Join(
            "\n",
            session.EnumerateDeploymentAssignments().Select(entry =>
            {
                var heroName = session.Profile.Heroes.FirstOrDefault(hero => hero.HeroId == entry.HeroId)?.Name ?? Localize(GameLocalizationTables.UICommon, "ui.common.empty", "Empty");
                return $"{LocalizeAnchor(entry.Anchor)}: {heroName}";
            }));

        return Localize(GameLocalizationTables.UIExpedition, "ui.expedition.squad.header", "Current Expedition Squad") + "\n" +
               string.Join("\n", names) +
               $"\n\n{Localize(GameLocalizationTables.UIExpedition, "ui.expedition.squad.posture", "Team Posture")}\n{session.SelectedTeamPosture}" +
               $"\n\n{Localize(GameLocalizationTables.UIExpedition, "ui.expedition.squad.deployment", "Deployment")}\n{deploymentSummary}" +
               $"\n\n{Localize(GameLocalizationTables.UIExpedition, "ui.expedition.squad.temp_augments", "Temp Augments")}\n{tempAugments}";
    }

    private string BuildDefaultStatus(GameSessionState session, ExpeditionNodeViewModel? selectedNode)
    {
        if (selectedNode == null)
        {
            return Localize(GameLocalizationTables.UIExpedition, "ui.expedition.status.default", "Choose a branch, then continue to the next battle.");
        }

        return selectedNode.RequiresBattle
            ? Localize(GameLocalizationTables.UIExpedition, "ui.expedition.status.ready_battle", "{0} is ready for battle.", ResolveNodeLabel(selectedNode))
            : Localize(GameLocalizationTables.UIExpedition, "ui.expedition.status.safe_node", "{0} can be cleared without battle.", ResolveNodeLabel(selectedNode));
    }

    private string BuildNodeEffectTag(ExpeditionNodeViewModel node)
    {
        return node.EffectKind switch
        {
            ExpeditionNodeEffectKind.None => Localize(GameLocalizationTables.UIExpedition, "ui.expedition.effect.none", "No effect"),
            ExpeditionNodeEffectKind.Gold => Localize(GameLocalizationTables.UIExpedition, "ui.expedition.effect.gold", "+{0} Gold", node.EffectAmount),
            ExpeditionNodeEffectKind.Echo => Localize(GameLocalizationTables.UIExpedition, "ui.expedition.effect.echo", "Echo +{0}", node.EffectAmount),
            ExpeditionNodeEffectKind.TemporaryAugment => Localize(GameLocalizationTables.UIExpedition, "ui.expedition.effect.temp_augment", "Temp Augment: {0}", _contentText.GetAugmentName(node.EffectPayloadId)),
            ExpeditionNodeEffectKind.PermanentAugmentSlot => Localize(GameLocalizationTables.UIExpedition, "ui.expedition.effect.permanent_slot", "Permanent Slot +{0}", Mathf.Max(1, node.EffectAmount)),
            _ => Localize(GameLocalizationTables.UIExpedition, "ui.expedition.effect.none", "No effect")
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
        Refresh(Localize(GameLocalizationTables.UIExpedition, "ui.expedition.status.anchor_cycled", "{0} deployment updated.", LocalizeAnchor(anchor)));
    }

    private string ResolveNodeLabel(ExpeditionNodeViewModel node)
    {
        return Localize(GameLocalizationTables.UIExpedition, node.LabelKey, node.Id);
    }

    private string ResolveNodeReward(ExpeditionNodeViewModel node)
    {
        return Localize(GameLocalizationTables.UIExpedition, node.PlannedRewardKey, node.Id);
    }

    private string ResolveNodeDescription(ExpeditionNodeViewModel node)
    {
        return Localize(GameLocalizationTables.UIExpedition, node.DescriptionKey, node.Id);
    }

    private string LocalizeAnchor(DeploymentAnchorId anchor)
    {
        return Localize(GameLocalizationTables.UICommon, anchor.ToLocalizationKey(), anchor.ToDisplayName());
    }

    private string Localize(string table, string key, string fallback, params object[] args)
    {
        return _localization != null
            ? _localization.LocalizeOrFallback(table, key, fallback, args)
            : args.Length == 0
                ? fallback
                : string.Format(fallback, args);
    }

    private void HandleLocaleChanged(UnityEngine.Localization.Locale _)
    {
        Refresh();
    }
}
