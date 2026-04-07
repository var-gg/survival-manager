using System.Collections.Generic;
using System.Linq;
using System.Text;
using SM.Combat.Model;
using UnityEngine;

namespace SM.Unity.UI.Expedition;

public sealed class ExpeditionScreenPresenter
{
    private const string HelpPrefsKey = "SM.Help.Expedition";

    private readonly GameSessionRoot _root;
    private readonly GameLocalizationController _localization;
    private readonly ContentTextResolver _contentText;
    private readonly ExpeditionScreenView _view;
    private readonly ScreenHelpState _helpState;

    public ExpeditionScreenPresenter(
        GameSessionRoot root,
        GameLocalizationController localization,
        ContentTextResolver contentText,
        ExpeditionScreenView view)
    {
        _root = root;
        _localization = localization;
        _contentText = contentText;
        _view = view;
        _helpState = new ScreenHelpState(HelpPrefsKey);
    }

    public void Initialize()
    {
        _view.Bind(this);
        Refresh();
    }

    public void SelectKorean() => _localization.TrySetLocale("ko");
    public void SelectEnglish() => _localization.TrySetLocale("en");
    public void SelectNode1() => SelectNode(0);
    public void SelectNode2() => SelectNode(1);
    public void SelectNode3() => SelectNode(2);
    public void SelectNode4() => SelectNode(3);
    public void SelectNode5() => SelectNode(4);
    public void ToggleHelp()
    {
        _helpState.Toggle();
        Refresh();
    }

    public void DismissHelp()
    {
        _helpState.Dismiss();
        Refresh();
    }

    public void NextBattleOrAdvance()
    {
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

            if (!session.PrepareSelectedBattleNodeHandoff())
            {
                Refresh(Localize(GameLocalizationTables.UIExpedition, "ui.expedition.error.advance_failed", "Failed to advance the selected node."));
                return;
            }

            var checkpoint = _root.SaveProfile(SessionCheckpointKind.ManualSave);
            if (!checkpoint.IsSuccessful)
            {
                Refresh(checkpoint.Message);
                return;
            }

            _root.SceneFlow.GoToBattle();
            return;
        }

        if (session.ResolveSelectedNodeToRewardSettlement())
        {
            var checkpoint = _root.SaveProfile(SessionCheckpointKind.ManualSave);
            if (!checkpoint.IsSuccessful)
            {
                Refresh(checkpoint.Message);
                return;
            }

            _root.SceneFlow.GoToReward();
            return;
        }

        Refresh(Localize(GameLocalizationTables.UIExpedition, "ui.expedition.error.advance_failed", "Failed to advance the selected node."));
    }

    public void ReturnToTown()
    {
        _root.SessionState.AbandonExpeditionRun();
        var checkpoint = _root.SaveProfile(SessionCheckpointKind.ManualSave);
        if (!checkpoint.IsSuccessful)
        {
            Refresh(checkpoint.Message);
            return;
        }

        _root.SceneFlow.ReturnToTown();
    }

    public void CycleFrontTop() => CycleAnchor(DeploymentAnchorId.FrontTop);
    public void CycleFrontCenter() => CycleAnchor(DeploymentAnchorId.FrontCenter);
    public void CycleFrontBottom() => CycleAnchor(DeploymentAnchorId.FrontBottom);
    public void CycleBackTop() => CycleAnchor(DeploymentAnchorId.BackTop);
    public void CycleBackCenter() => CycleAnchor(DeploymentAnchorId.BackCenter);
    public void CycleBackBottom() => CycleAnchor(DeploymentAnchorId.BackBottom);

    public void CycleTeamPosture()
    {
        _root.SessionState.CycleTeamPosture();
        Refresh(Localize(GameLocalizationTables.UIExpedition, "ui.expedition.status.team_posture", "Team posture: {0}", _root.SessionState.SelectedTeamPosture));
    }

    public void Refresh(string message = "")
    {
        _view.Render(BuildState(_root.SessionState, message));
    }

    private void SelectNode(int nodeIndex)
    {
        if (_root.SessionState.SelectNextExpeditionNode(nodeIndex))
        {
            Refresh(Localize(GameLocalizationTables.UIExpedition, "ui.expedition.status.route_selected", "Route {0} selected.", nodeIndex + 1));
            return;
        }

        Refresh(Localize(GameLocalizationTables.UIExpedition, "ui.expedition.error.invalid_route", "That node cannot be selected from the current position."));
    }

    private void CycleAnchor(DeploymentAnchorId anchor)
    {
        _root.SessionState.CycleDeploymentAssignment(anchor);
        Refresh(Localize(GameLocalizationTables.UIExpedition, "ui.expedition.status.anchor_cycled", "{0} deployment updated.", LocalizeAnchor(anchor)));
    }

    private ExpeditionScreenViewState BuildState(GameSessionState session, string message)
    {
        var currentNode = session.GetCurrentExpeditionNode();
        var selectedNode = session.GetSelectedExpeditionNode();
        return new ExpeditionScreenViewState(
            Localize(GameLocalizationTables.UIExpedition, "ui.expedition.title", "Expedition Operator UI"),
            BuildLocaleStatus(),
            GetLocaleButtonLabel("ko", "한국어"),
            GetLocaleButtonLabel("en", "English"),
            Localize(GameLocalizationTables.UICommon, "ui.common.help", "Help"),
            BuildHelpState(),
            Localize(GameLocalizationTables.UIExpedition, "ui.expedition.panel.map", "Map / Position"),
            BuildPositionText(session, currentNode, selectedNode),
            BuildMapText(session),
            Localize(GameLocalizationTables.UIExpedition, "ui.expedition.panel.selected_route", "Selected Route"),
            BuildRewardText(session),
            Localize(GameLocalizationTables.UIExpedition, "ui.expedition.panel.squad", "Squad / Deploy"),
            BuildSquadText(session),
            Localize(GameLocalizationTables.UIExpedition, "ui.expedition.panel.routes", "Routes"),
            BuildNodeCards(session),
            BuildDeployButtons(session),
            Localize(GameLocalizationTables.UICommon, "ui.common.posture", "Posture") + "\n" + session.SelectedTeamPosture,
            Localize(GameLocalizationTables.UIExpedition, "ui.expedition.group.primary", "Primary"),
            Localize(GameLocalizationTables.UIExpedition, "ui.expedition.group.warning", "Return / Warning"),
            string.IsNullOrWhiteSpace(message) ? string.Empty : message,
            selectedNode != null && !selectedNode.RequiresBattle
                ? Localize(GameLocalizationTables.UIExpedition, "ui.expedition.action.resolve_settlement", "Resolve Settlement")
                : Localize(GameLocalizationTables.UIExpedition, "ui.expedition.action.next_battle", "Enter Battle"),
            BuildNextBattleTooltip(selectedNode),
            Localize(GameLocalizationTables.UICommon, "ui.common.return_town", "Return Town"),
            Localize(GameLocalizationTables.UIExpedition, "ui.expedition.tooltip.return_town", "Abandon the current expedition and return to Town."));
    }

    private IReadOnlyList<ExpeditionNodeCardViewState> BuildNodeCards(GameSessionState session)
    {
        var selectable = session.GetSelectableNextNodeIndices().ToHashSet();
        var cards = new List<ExpeditionNodeCardViewState>(5);
        for (var i = 0; i < 5; i++)
        {
            if (i >= session.ExpeditionNodes.Count)
            {
                cards.Add(new ExpeditionNodeCardViewState(string.Empty, string.Empty, string.Empty, string.Empty, false, false, false, false, false));
                continue;
            }

            var node = session.ExpeditionNodes[i];
            var isCurrent = node.Index == session.CurrentExpeditionNodeIndex;
            var isSelected = node.Index == session.SelectedExpeditionNodeIndex;
            var isSelectable = selectable.Contains(node.Index);
            cards.Add(new ExpeditionNodeCardViewState(
                $"{i + 1}. {ResolveNodeLabel(node)}",
                string.Join(
                    "\n",
                    Localize(GameLocalizationTables.UIExpedition, "ui.expedition.node.type", "Type: {0}", ResolveNodeType(node)),
                    Localize(GameLocalizationTables.UIExpedition, "ui.expedition.node.reward", "Planned Reward: {0}", ResolveNodeReward(node)),
                    Localize(GameLocalizationTables.UIExpedition, "ui.expedition.node.effect", "Node Effect: {0}", BuildNodeEffectTag(node))),
                isSelected
                    ? Localize(GameLocalizationTables.UICommon, "ui.common.selected", "Selected")
                    : isCurrent
                        ? Localize(GameLocalizationTables.UICommon, "ui.common.here", "Here")
                        : isSelectable
                            ? Localize(GameLocalizationTables.UIExpedition, "ui.expedition.action.route", "Route")
                            : Localize(GameLocalizationTables.UICommon, "ui.common.locked", "Locked"),
                BuildNodeTooltip(node),
                true,
                isSelectable,
                isSelected,
                isCurrent,
                node.Index < session.CurrentExpeditionNodeIndex));
        }

        return cards;
    }

    private IReadOnlyList<ExpeditionDeployButtonViewState> BuildDeployButtons(GameSessionState session)
    {
        return session.EnumerateDeploymentAssignments()
            .Select(entry =>
            {
                var hero = session.Profile.Heroes.FirstOrDefault(x => x.HeroId == entry.HeroId);
                var heroName = hero?.Name ?? Localize(GameLocalizationTables.UICommon, "ui.common.empty", "Empty");
                return new ExpeditionDeployButtonViewState(entry.Anchor, $"{LocalizeAnchor(entry.Anchor)}\n{heroName}");
            })
            .ToArray();
    }

    private string BuildLocaleStatus()
    {
        var locale = _localization.CurrentLocale;
        if (locale == null)
        {
            return "-";
        }

        return $"{Localize(GameLocalizationTables.UICommon, "ui.common.current_language", "Current")}: {_localization.GetLocaleButtonLabel(locale)}";
    }

    private string GetLocaleButtonLabel(string localeCode, string fallback)
    {
        var locale = UnityEngine.Localization.Settings.LocalizationSettings.AvailableLocales?.GetLocale(localeCode);
        if (locale != null)
        {
            return _localization.GetLocaleButtonLabel(locale);
        }

        return fallback;
    }

    private HelpStripViewState BuildHelpState()
    {
        return new HelpStripViewState(
            _helpState.IsVisible,
            Localize(
                GameLocalizationTables.UIExpedition,
                "ui.expedition.help.body",
                "Choose a node, then read the selected route summary before entering battle or resolving settlement."),
            Localize(GameLocalizationTables.UICommon, "ui.common.hide", "Hide"));
    }

    private string BuildNextBattleTooltip(ExpeditionNodeViewModel? selectedNode)
    {
        if (selectedNode == null)
        {
            return Localize(GameLocalizationTables.UIExpedition, "ui.expedition.tooltip.next_action_locked", "Choose a route first. The summary panel explains the reward and risk.");
        }

        return selectedNode.RequiresBattle
            ? Localize(GameLocalizationTables.UIExpedition, "ui.expedition.tooltip.enter_battle", "Enter the selected battle node with the current deploy and posture.")
            : Localize(GameLocalizationTables.UIExpedition, "ui.expedition.tooltip.resolve_settlement", "Resolve this settlement node and move to Reward without a battle.");
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
        if (selected != null)
        {
            sb.AppendLine(ResolveNodeLabel(selected));
            sb.AppendLine(Localize(GameLocalizationTables.UIExpedition, "ui.expedition.reward.type", "Type: {0}", ResolveNodeType(selected)));
            sb.AppendLine(Localize(GameLocalizationTables.UIExpedition, "ui.expedition.reward.planned", "Planned Reward: {0}", ResolveNodeReward(selected)));
            sb.AppendLine(Localize(GameLocalizationTables.UIExpedition, "ui.expedition.reward.effect", "Node Effect / Risk: {0}", BuildNodeEffectTag(selected)));
            sb.AppendLine(Localize(GameLocalizationTables.UIExpedition, "ui.expedition.reward.description", "Route Note: {0}", ResolveNodeDescription(selected)));
            sb.AppendLine(Localize(GameLocalizationTables.UIExpedition, "ui.expedition.reward.return_town", "Return to Town: abandon this expedition and lose the current route commitment."));
        }
        else
        {
            sb.AppendLine(Localize(GameLocalizationTables.UIExpedition, "ui.expedition.reward.none", "No branch is selected yet."));
            sb.AppendLine(Localize(GameLocalizationTables.UIExpedition, "ui.expedition.reward.return_town", "Return to Town: abandon this expedition and lose the current route commitment."));
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

    private string ResolveNodeType(ExpeditionNodeViewModel node)
    {
        return node.RequiresBattle
            ? Localize(GameLocalizationTables.UIExpedition, "ui.expedition.map.battle", "Battle")
            : Localize(GameLocalizationTables.UIExpedition, "ui.expedition.position.node_type.settlement", "Settlement");
    }

    private string BuildNodeTooltip(ExpeditionNodeViewModel node)
    {
        return Localize(
            GameLocalizationTables.UIExpedition,
            "ui.expedition.tooltip.route_card",
            "{0}. Planned reward: {1}. Node effect: {2}",
            ResolveNodeType(node),
            ResolveNodeReward(node),
            BuildNodeEffectTag(node));
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

    private string BuildDefaultStatus(ExpeditionNodeViewModel? selectedNode)
    {
        if (selectedNode == null)
        {
            return Localize(GameLocalizationTables.UIExpedition, "ui.expedition.status.default", "Choose a branch, then continue to the next battle.");
        }

        return selectedNode.RequiresBattle
            ? Localize(GameLocalizationTables.UIExpedition, "ui.expedition.status.ready_battle", "{0} is ready for battle.", ResolveNodeLabel(selectedNode))
            : Localize(GameLocalizationTables.UIExpedition, "ui.expedition.status.safe_node", "{0} will settle through Reward before returning to Town.", ResolveNodeLabel(selectedNode));
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

    private string ResolveNodeLabel(ExpeditionNodeViewModel node) => Localize(GameLocalizationTables.UIExpedition, node.LabelKey, node.Id);
    private string ResolveNodeReward(ExpeditionNodeViewModel node) => Localize(GameLocalizationTables.UIExpedition, node.PlannedRewardKey, node.Id);
    private string ResolveNodeDescription(ExpeditionNodeViewModel node) => Localize(GameLocalizationTables.UIExpedition, node.DescriptionKey, node.Id);
    private string LocalizeAnchor(DeploymentAnchorId anchor) => Localize(GameLocalizationTables.UICommon, anchor.ToLocalizationKey(), anchor.ToDisplayName());
    private string Localize(string table, string key, string fallback, params object[] args) => _localization.LocalizeOrFallback(table, key, fallback, args);

    private string BuildPositionText(
        GameSessionState session,
        ExpeditionNodeViewModel? currentNode,
        ExpeditionNodeViewModel? selectedNode)
    {
        var chapterName = _contentText.GetCampaignChapterName(session.SelectedCampaignChapterId);
        var siteName = _contentText.GetExpeditionSiteName(session.SelectedCampaignSiteId);
        var currentNodeType = currentNode == null
            ? "-"
            : currentNode.RequiresBattle
                ? Localize(GameLocalizationTables.UIExpedition, "ui.expedition.position.node_type.battle", "Battle")
                : Localize(GameLocalizationTables.UIExpedition, "ui.expedition.position.node_type.settlement", "Settlement");
        var selectedNodeText = selectedNode == null
            ? Localize(GameLocalizationTables.UIExpedition, "ui.expedition.position.none", "Selection Needed")
            : $"{ResolveNodeLabel(selectedNode)} / {ResolveNodeReward(selectedNode)}";

        return string.Join(
            "\n",
            Localize(GameLocalizationTables.UIExpedition, "ui.expedition.position.chapter", "Chapter: {0}", chapterName),
            Localize(GameLocalizationTables.UIExpedition, "ui.expedition.position.site", "Site: {0}", siteName),
            Localize(
                GameLocalizationTables.UIExpedition,
                "ui.expedition.position.current",
                "Current Node: {0}/{1} / {2}",
                session.CurrentExpeditionNodeIndex + 1,
                Mathf.Max(1, session.ExpeditionNodes.Count),
                currentNode == null ? "-" : ResolveNodeLabel(currentNode)),
            Localize(GameLocalizationTables.UIExpedition, "ui.expedition.position.type", "Node Type: {0}", currentNodeType),
            Localize(GameLocalizationTables.UIExpedition, "ui.expedition.position.selected", "Selected: {0}", selectedNodeText));
    }
}
