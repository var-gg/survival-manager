using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SM.Combat.Model;
using SM.Content.Definitions;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Persistence.Abstractions.Models;
using SM.Unity.UI;
using UnityEngine;

namespace SM.Unity.UI.Town;

public sealed class TownScreenPresenter
{
    private const string HelpPrefsKey = "SM.Help.Town";

    private readonly GameSessionRoot _root;
    private readonly GameLocalizationController _localization;
    private readonly ContentTextResolver _contentText;
    private readonly TownScreenView _view;
    private readonly BuildIdentityFormatter _buildFormatter;
    private readonly ScreenHelpState _helpState;
    private int _selectedHeroIndex;
    private int _selectedItemIndex;
    private int _selectedPassiveNodeIndex;
    private int _selectedPermanentIndex;

    public TownScreenPresenter(
        GameSessionRoot root,
        GameLocalizationController localization,
        ContentTextResolver contentText,
        TownScreenView view)
    {
        _root = root;
        _localization = localization;
        _contentText = contentText;
        _view = view;
        _buildFormatter = new BuildIdentityFormatter(contentText);
        _helpState = new ScreenHelpState(HelpPrefsKey);
    }

    public void Initialize()
    {
        _view.Bind(this);
        Refresh();
    }

    public void SelectKorean() => _localization.TrySetLocale("ko");
    public void SelectEnglish() => _localization.TrySetLocale("en");
    public void RecruitOffer0() => Recruit(0);
    public void RecruitOffer1() => Recruit(1);
    public void RecruitOffer2() => Recruit(2);
    public void RecruitOffer3() => Recruit(3);
    public void PreviousChapter() => CycleCampaignChapter(-1);
    public void NextChapter() => CycleCampaignChapter(1);
    public void PreviousSite() => CycleCampaignSite(-1);
    public void NextSite() => CycleCampaignSite(1);
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

    public void RerollOffers()
    {
        var refreshCost = _root.SessionState.CurrentRecruitRefreshCost;
        var result = _root.SessionState.RerollRecruitOffers();
        Refresh(result.IsSuccess
            ? Localize(GameLocalizationTables.UITown, "ui.town.status.reroll_success", "Recruit offers rerolled. (-{0} Gold)", refreshCost)
            : result.Error ?? Localize(GameLocalizationTables.UITown, "ui.town.error.reroll_failed", "Failed to reroll recruit offers."));
    }

    public void SaveProfile()
    {
        var checkpoint = _root.SaveProfile(SessionCheckpointKind.ManualSave);
        Refresh(checkpoint.Status == SessionCheckpointStatus.Success
            ? Localize(GameLocalizationTables.UITown, "ui.town.status.profile_saved", "Profile saved.")
            : checkpoint.Message);
    }

    public void LoadProfile()
    {
        if (!_root.SessionState.CanManualProfileReload(out var reason))
        {
            _root.SessionState.RecordOperationalTelemetry(RuntimeOperationalTelemetry.CreateManualReloadBlocked(
                _root.ActiveProfileId,
                _root.ActiveProfileId,
                reason));
            Refresh(reason);
            return;
        }

        var checkpoint = _root.BindProfile(SessionCheckpointKind.ManualLoad);
        Refresh(checkpoint.Status == SessionCheckpointStatus.Success
            ? Localize(GameLocalizationTables.UITown, "ui.town.status.profile_loaded", "Profile reloaded.")
            : checkpoint.Message);
    }

    public void ReturnToStart()
    {
        if (IsReturnToStartBlocked(_root.SessionState))
        {
            Refresh(Localize(
                GameLocalizationTables.UITown,
                "ui.town.error.return_start_locked",
                "진행 중인 런이 있어 지금은 시작 화면으로 돌아갈 수 없습니다."));
            return;
        }

        var checkpoint = _root.ReturnToSessionMenu();
        if (!checkpoint.IsSuccessful)
        {
            Refresh(checkpoint.Message);
        }
    }

    public void OpenExpedition()
    {
        if (_root.SessionState.HasPendingRewardSettlement)
        {
            var checkpoint = _root.SaveProfile(SessionCheckpointKind.TownExit);
            if (!checkpoint.IsSuccessful)
            {
                Refresh(checkpoint.Message);
                return;
            }

            _root.SceneFlow.GoToReward();
            return;
        }

        if (_root.SessionState.CanResumeExpedition)
        {
            var checkpoint = _root.SaveProfile(SessionCheckpointKind.TownExit);
            if (!checkpoint.IsSuccessful)
            {
                Refresh(checkpoint.Message);
                return;
            }

            _root.SceneFlow.GoToExpedition();
            return;
        }

        _root.SessionState.BeginNewExpedition();
        var newRunCheckpoint = _root.SaveProfile(SessionCheckpointKind.TownExit);
        if (!newRunCheckpoint.IsSuccessful)
        {
            Refresh(newRunCheckpoint.Message);
            return;
        }

        _root.SceneFlow.GoToExpedition();
    }

    public void QuickBattle()
    {
        if (!_root.SessionState.CanStartQuickBattleSmoke)
        {
            Refresh(Localize(
                GameLocalizationTables.UITown,
                "ui.town.error.quick_battle_locked",
                "Quick Battle (Smoke) is unavailable while a reward settlement or expedition run is active."));
            return;
        }

        var checkpoint = _root.SaveProfile(SessionCheckpointKind.TownExit);
        if (!checkpoint.IsSuccessful)
        {
            Refresh(checkpoint.Message);
            return;
        }

        _root.BeginTransientTownSmoke();
        _root.SessionState.PrepareTownQuickBattleSmoke();
        _root.SceneFlow.GoToBattle();
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
        Refresh(Localize(GameLocalizationTables.UITown, "ui.town.status.team_posture", "Team posture: {0}", _root.SessionState.SelectedTeamPosture));
    }

    public void CycleHero()
    {
        var heroes = _root.SessionState.Profile.Heroes;
        if (heroes.Count == 0)
        {
            Refresh("선택할 유닛이 없습니다.");
            return;
        }

        _selectedHeroIndex = WrapIndex(_selectedHeroIndex + 1, heroes.Count);
        _selectedPassiveNodeIndex = 0;
        Refresh();
    }

    public void CycleItem()
    {
        var items = GetSelectableItems();
        if (items.Count == 0)
        {
            Refresh("선택할 아이템이 없습니다.");
            return;
        }

        _selectedItemIndex = WrapIndex(_selectedItemIndex + 1, items.Count);
        Refresh();
    }

    public void UseScout()
    {
        var directive = ResolveScoutDirective();
        var result = _root.SessionState.UseScout(directive);
        Refresh(result.IsSuccess
            ? Localize(GameLocalizationTables.UITown, "ui.town.status.scout_used", "Scout used: {0}", directive.Kind)
            : result.Error ?? Localize(GameLocalizationTables.UITown, "ui.town.error.scout_failed", "Scout failed."));
    }

    public void RetrainFlexActive() => RetrainSelectedHero(SM.Core.Contracts.RetrainOperationKind.RerollFlexActive);
    public void RetrainFlexPassive() => RetrainSelectedHero(SM.Core.Contracts.RetrainOperationKind.RerollFlexPassive);
    public void FullRetrain() => RetrainSelectedHero(SM.Core.Contracts.RetrainOperationKind.FullRetrain);

    public void DismissSelectedHero()
    {
        var hero = GetSelectedHero();
        if (hero == null)
        {
            Refresh("Dismiss할 유닛이 없습니다.");
            return;
        }

        var result = _root.SessionState.DismissHero(hero.HeroId);
        Refresh(result.IsSuccess
            ? Localize(GameLocalizationTables.UITown, "ui.town.status.dismiss_success", "Hero dismissed.")
            : result.Error ?? "Dismiss failed.");
    }

    public void RefitSelectedItem()
    {
        var item = GetSelectedItem();
        if (item == null)
        {
            Refresh("Refit할 아이템이 없습니다.");
            return;
        }

        var result = _root.SessionState.RefitItem(item.ItemInstanceId, 0);
        Refresh(result.IsSuccess
            ? Localize(GameLocalizationTables.UITown, "ui.town.status.refit_success", "Item refit complete.")
            : result.Error ?? "Refit failed.");
    }

    public void CycleBoard()
    {
        var hero = GetSelectedHero();
        if (hero == null)
        {
            Refresh("패시브 보드를 바꿀 유닛이 없습니다.");
            return;
        }

        var boardIds = GetAvailableBoardIds();
        if (boardIds.Count == 0)
        {
            Refresh("선택 가능한 패시브 보드가 없습니다.");
            return;
        }

        var currentBoardId = _root.SessionState.Profile.HeroLoadouts
            .FirstOrDefault(record => string.Equals(record.HeroId, hero.HeroId, StringComparison.Ordinal))
            ?.PassiveBoardId ?? string.Empty;
        var currentIndex = boardIds.FindIndex(id => string.Equals(id, currentBoardId, StringComparison.Ordinal));
        var nextBoardId = boardIds[WrapIndex(currentIndex + 1, boardIds.Count)];
        var result = _root.SessionState.SelectPassiveBoard(hero.HeroId, nextBoardId);
        _selectedPassiveNodeIndex = 0;
        Refresh(result.IsSuccess
            ? Localize(GameLocalizationTables.UITown, "ui.town.status.board_selected", "Passive board selected: {0}", _contentText.GetPassiveBoardName(nextBoardId))
            : result.Error ?? "Board selection failed.");
    }

    public void CyclePassiveNode()
    {
        var nodes = GetCurrentPassiveNodes();
        if (nodes.Count == 0)
        {
            Refresh("순환할 패시브 노드가 없습니다.");
            return;
        }

        _selectedPassiveNodeIndex = WrapIndex(_selectedPassiveNodeIndex + 1, nodes.Count);
        Refresh();
    }

    public void TogglePassiveNode()
    {
        var hero = GetSelectedHero();
        var node = GetSelectedPassiveNode();
        if (hero == null || node == null)
        {
            Refresh("토글할 패시브 노드가 없습니다.");
            return;
        }

        var result = _root.SessionState.TogglePassiveNode(hero.HeroId, node.Id);
        Refresh(result.IsSuccess
            ? Localize(GameLocalizationTables.UITown, "ui.town.status.node_toggled", "Passive node toggled: {0}", _contentText.GetPassiveNodeName(node.Id))
            : result.Error ?? "Passive node toggle failed.");
    }

    public void CyclePermanentCandidate()
    {
        var permanentIds = GetUnlockedPermanentAugmentIds();
        if (permanentIds.Count == 0)
        {
            Refresh("순환할 영구 증강 후보가 없습니다.");
            return;
        }

        _selectedPermanentIndex = WrapIndex(_selectedPermanentIndex + 1, permanentIds.Count);
        Refresh();
    }

    public void EquipSelectedPermanentAugment()
    {
        var augmentId = GetSelectedPermanentAugmentId();
        if (string.IsNullOrWhiteSpace(augmentId))
        {
            Refresh("장착할 영구 증강 후보가 없습니다.");
            return;
        }

        var result = _root.SessionState.EquipPermanentAugment(augmentId);
        Refresh(result.IsSuccess
            ? Localize(GameLocalizationTables.UITown, "ui.town.status.perm_augment_equipped", "Permanent augment equipped: {0}", _contentText.GetAugmentName(augmentId))
            : result.Error ?? "Equip failed.");
    }

    public void Refresh(string message = "")
    {
        var session = _root.SessionState;
        session.EnsureBattleDeployReady();
        _view.Render(BuildState(session, message));
    }

    private void Recruit(int index)
    {
        var offerCost = index >= 0 && index < _root.SessionState.RecruitOffers.Count
            ? _root.SessionState.RecruitOffers[index].Metadata.GoldCost
            : 0;
        var result = _root.SessionState.Recruit(index);
        Refresh(result.IsSuccess
            ? Localize(GameLocalizationTables.UITown, "ui.town.status.recruit_success", "Recruited offer {0}. (-{1} Gold)", index + 1, offerCost)
            : result.Error ?? Localize(GameLocalizationTables.UITown, "ui.town.error.recruit_failed", "Failed to recruit the selected offer."));
    }

    private void CycleAnchor(DeploymentAnchorId anchor)
    {
        _root.SessionState.CycleDeploymentAssignment(anchor);
        Refresh(Localize(GameLocalizationTables.UITown, "ui.town.status.anchor_cycled", "{0} deployment updated.", LocalizeAnchor(anchor)));
    }

    private TownScreenViewState BuildState(GameSessionState session, string message)
    {
        NormalizeUiSelections(session);
        var playerId = ResolvePlayerId();
        var profile = _root.ProfileQueries.GetProfileView(playerId);
        var loadout = _root.ProfileQueries.GetLoadoutView(playerId);
        var chapterIds = GetOrderedChapterIds();
        var siteIds = GetOrderedSiteIds(session.SelectedCampaignChapterId);
        var canCycleChapter = session.CanChangeCampaignSelection && chapterIds.Count > 1;
        var canCycleSite = session.CanChangeCampaignSelection && siteIds.Count > 1;
        var selectedHero = GetSelectedHero();
        var selectedItem = GetSelectedItem();
        var selectedNode = GetSelectedPassiveNode();
        var selectedPermanentId = GetSelectedPermanentAugmentId();
        var retrainActiveCost = selectedHero == null
            ? 0
            : RecruitmentBalanceCatalog.DefaultRetrainCosts.GetTotalCost(SM.Core.Contracts.RetrainOperationKind.RerollFlexActive, selectedHero.RetrainState);
        var retrainPassiveCost = selectedHero == null
            ? 0
            : RecruitmentBalanceCatalog.DefaultRetrainCosts.GetTotalCost(SM.Core.Contracts.RetrainOperationKind.RerollFlexPassive, selectedHero.RetrainState);
        var fullRetrainCost = selectedHero == null
            ? 0
            : RecruitmentBalanceCatalog.DefaultRetrainCosts.GetTotalCost(SM.Core.Contracts.RetrainOperationKind.FullRetrain, selectedHero.RetrainState);
        var dismissRefund = selectedHero == null
            ? new DismissRefundResult(0, 0)
            : DismissService.CalculateRefund(selectedHero.EconomyFootprint);
        var canScout = session.CanUseScout && session.Profile.Currencies.Echo >= RecruitmentBalanceCatalog.ScoutEchoCost;
        var canRetrainActive = selectedHero != null && session.Profile.Currencies.Echo >= retrainActiveCost;
        var canRetrainPassive = selectedHero != null && session.Profile.Currencies.Echo >= retrainPassiveCost;
        var canFullRetrain = selectedHero != null && session.Profile.Currencies.Echo >= fullRetrainCost;
        var canRefit = selectedItem != null && session.Profile.Currencies.Echo >= MetaBalanceDefaults.RefitEchoCost;
        var canEquipPermanent = !string.IsNullOrWhiteSpace(selectedPermanentId)
                                && !string.Equals(selectedPermanentId, GetEquippedPermanentAugmentId(session), StringComparison.Ordinal);
        var statusText = string.IsNullOrWhiteSpace(message) ? string.Empty : message;
        var showDebugActions = Application.isEditor || Debug.isDebugBuild;

        return new TownScreenViewState(
            Localize(GameLocalizationTables.UITown, "ui.town.title", "Town Operator UI"),
            BuildLocaleStatus(),
            GetLocaleButtonLabel("ko", "한국어"),
            GetLocaleButtonLabel("en", "English"),
            Localize(GameLocalizationTables.UICommon, "ui.common.help", "Help"),
            BuildHelpState(),
            Localize(GameLocalizationTables.UITown, "ui.town.panel.campaign", "Campaign"),
            BuildCampaignSummary(session),
            Localize(GameLocalizationTables.UITown, "ui.town.action.prev_chapter", "Prev Chapter"),
            canCycleChapter,
            Localize(GameLocalizationTables.UITown, "ui.town.action.next_chapter", "Next Chapter"),
            canCycleChapter,
            Localize(GameLocalizationTables.UITown, "ui.town.action.prev_site", "Prev Site"),
            canCycleSite,
            Localize(GameLocalizationTables.UITown, "ui.town.action.next_site", "Next Site"),
            canCycleSite,
            Localize(GameLocalizationTables.UITown, "ui.town.panel.economy", "Economy"),
            BuildEconomyRailText(session, profile),
            Localize(GameLocalizationTables.UITown, "ui.town.panel.roster", "Roster"),
            BuildRosterText(profile, selectedHero?.HeroId ?? string.Empty),
            Localize(GameLocalizationTables.UITown, "ui.town.panel.blueprint", "Build"),
            _buildFormatter.BuildBlueprintSummary(session),
            Localize(GameLocalizationTables.UITown, "ui.town.panel.recruit", "Recruit"),
            BuildRecruitSummary(session, profile),
            BuildRecruitCards(session),
            Localize(GameLocalizationTables.UITown, "ui.town.panel.selected_hero", "Selected Hero"),
            BuildSelectedHeroSummary(session, selectedHero, selectedItem, selectedNode?.Id ?? string.Empty, retrainActiveCost, retrainPassiveCost, fullRetrainCost, dismissRefund),
            Localize(GameLocalizationTables.UITown, "ui.town.panel.deploy", "Squad / Deploy"),
            BuildDeployPreviewText(session, profile, loadout),
            BuildDeployButtons(profile, loadout),
            Localize(GameLocalizationTables.UICommon, "ui.common.posture", "Posture") + "\n" + session.SelectedTeamPosture,
            Localize(GameLocalizationTables.UITown, "ui.town.tooltip.team_posture", "Sets the current team posture used for deploy and battle readiness."),
            BuildCycleHeroLabel(selectedHero),
            session.Profile.Heroes.Count > 0,
            BuildCycleItemLabel(selectedItem),
            GetSelectableItems().Count > 0,
            Localize(GameLocalizationTables.UITown, "ui.town.action.scout", "Scout") + $" (-{RecruitmentBalanceCatalog.ScoutEchoCost} Echo)",
            canScout,
            Localize(GameLocalizationTables.UITown, "ui.town.action.retrain_active", "Retrain Flex Active") + $" (-{retrainActiveCost} Echo)",
            canRetrainActive,
            Localize(GameLocalizationTables.UITown, "ui.town.action.retrain_passive", "Retrain Flex Passive") + $" (-{retrainPassiveCost} Echo)",
            canRetrainPassive,
            Localize(GameLocalizationTables.UITown, "ui.town.action.full_retrain", "Full Retrain") + $" (-{fullRetrainCost} Echo)",
            canFullRetrain,
            Localize(GameLocalizationTables.UITown, "ui.town.action.dismiss", "Dismiss") + $" (+{dismissRefund.GoldRefund} Gold / +{dismissRefund.EchoRefund} Echo)",
            selectedHero != null,
            Localize(GameLocalizationTables.UITown, "ui.town.action.refit", "Refit Selected Item") + $" (-{MetaBalanceDefaults.RefitEchoCost} Echo)",
            canRefit,
            BuildCycleBoardLabel(selectedHero),
            selectedHero != null && GetAvailableBoardIds().Count > 0,
            BuildCycleNodeLabel(selectedNode),
            GetCurrentPassiveNodes().Count > 0,
            Localize(GameLocalizationTables.UITown, "ui.town.action.toggle_node", "Toggle Node"),
            selectedNode != null,
            BuildCyclePermanentLabel(selectedPermanentId),
            GetUnlockedPermanentAugmentIds().Count > 0,
            Localize(GameLocalizationTables.UITown, "ui.town.action.equip_permanent", "Equip Permanent"),
            canEquipPermanent,
            statusText,
            Localize(GameLocalizationTables.UITown, "ui.town.action.reroll", "Reroll"),
            Localize(GameLocalizationTables.UICommon, "ui.common.save", "Save"),
            Localize(GameLocalizationTables.UICommon, "ui.common.load", "Load"),
            Localize(GameLocalizationTables.UICommon, "ui.common.return_start", "Return to Start"),
            BuildReturnToStartTooltip(session),
            !IsReturnToStartBlocked(session),
            session.HasPendingRewardSettlement
                ? Localize(GameLocalizationTables.UIReward, "ui.reward.action.open", "Open Reward")
                : session.CanResumeExpedition
                    ? Localize(GameLocalizationTables.UITown, "ui.town.action.resume_expedition", "Resume Expedition")
                    : Localize(GameLocalizationTables.UITown, "ui.town.action.start_expedition", "Start Expedition"),
            BuildExpeditionTooltip(session),
            Localize(GameLocalizationTables.UITown, "ui.town.group.primary", "Primary"),
            Localize(GameLocalizationTables.UITown, "ui.town.group.gameplay", "Gameplay"),
            Localize(GameLocalizationTables.UITown, "ui.town.group.utility", "Utility"),
            Localize(GameLocalizationTables.UITown, "ui.town.group.debug", "Debug / Smoke"),
            Localize(GameLocalizationTables.UITown, "ui.town.action.quick_battle_smoke", "Quick Battle (Smoke)"),
            Localize(GameLocalizationTables.UITown, "ui.town.tooltip.quick_battle_smoke", "Open an integration smoke battle using the current Town build, then return through Reward or direct Town restore."),
            showDebugActions,
            showDebugActions && session.CanStartQuickBattleSmoke);
    }

    private void CycleCampaignChapter(int direction)
    {
        var session = _root.SessionState;
        if (!session.TryCycleCampaignChapter(direction))
        {
            Refresh(session.CanChangeCampaignSelection
                ? Localize(GameLocalizationTables.UITown, "ui.town.error.chapter_cycle_failed", "Chapter selection could not be changed.")
                : Localize(GameLocalizationTables.UITown, "ui.town.error.chapter_locked", "Chapter and site are locked while an expedition run is active."));
            return;
        }

        var checkpoint = _root.SaveProfile(SessionCheckpointKind.ManualSave);
        Refresh(checkpoint.IsSuccessful
            ? Localize(GameLocalizationTables.UITown, "ui.town.status.chapter_changed", "Campaign chapter updated.")
            : checkpoint.Message);
    }

    private void CycleCampaignSite(int direction)
    {
        var session = _root.SessionState;
        if (!session.TryCycleCampaignSite(direction))
        {
            Refresh(session.CanChangeCampaignSelection
                ? Localize(GameLocalizationTables.UITown, "ui.town.error.site_cycle_failed", "Site selection could not be changed.")
                : Localize(GameLocalizationTables.UITown, "ui.town.error.site_locked", "Chapter and site are locked while an expedition run is active."));
            return;
        }

        var checkpoint = _root.SaveProfile(SessionCheckpointKind.ManualSave);
        Refresh(checkpoint.IsSuccessful
            ? Localize(GameLocalizationTables.UITown, "ui.town.status.site_changed", "Expedition site updated.")
            : checkpoint.Message);
    }

    private string BuildCampaignSummary(GameSessionState session)
    {
        var chapterName = _contentText.GetCampaignChapterName(session.SelectedCampaignChapterId);
        var siteName = _contentText.GetExpeditionSiteName(session.SelectedCampaignSiteId);
        var sb = new StringBuilder();
        sb.AppendLine(Localize(GameLocalizationTables.UITown, "ui.town.campaign.chapter", "Chapter: {0}", chapterName));
        sb.AppendLine(Localize(GameLocalizationTables.UITown, "ui.town.campaign.site", "Site: {0}", siteName));
        sb.AppendLine(Localize(
            GameLocalizationTables.UITown,
            "ui.town.campaign.selection_state",
            "Selection: {0}",
            session.CanChangeCampaignSelection
                ? Localize(GameLocalizationTables.UITown, "ui.town.campaign.unlocked", "Unlocked for a new authored route.")
                : Localize(GameLocalizationTables.UITown, "ui.town.campaign.locked", "Locked while the current run stays active.")));
        sb.AppendLine(Localize(
            GameLocalizationTables.UITown,
            "ui.town.campaign.objective",
            "Objective: {0}",
            _contentText.GetExpeditionSiteDescription(session.SelectedCampaignSiteId)));

        if (session.HasPendingRewardSettlement)
        {
            sb.AppendLine(Localize(GameLocalizationTables.UITown, "ui.town.campaign.pending_reward", "Pending settlement: open Reward before resuming the run."));
        }
        else if (session.HasActiveExpeditionRun)
        {
            sb.AppendLine(Localize(
                GameLocalizationTables.UITown,
                "ui.town.campaign.active_run",
                "Active run: node {0}/{1} is ready for resume.",
                session.CurrentExpeditionNodeIndex + 1,
                Mathf.Max(1, session.ExpeditionNodes.Count)));
            sb.AppendLine(Localize(
                GameLocalizationTables.UITown,
                "ui.town.campaign.quick_battle_locked",
                "Quick Battle (Smoke) is locked until the current expedition run is closed."));
        }
        else
        {
            sb.AppendLine(Localize(
                GameLocalizationTables.UITown,
                "ui.town.campaign.quick_battle_ready",
                "Quick Battle (Smoke) is available as a secondary debug lane."));
        }

        return sb.ToString();
    }

    private IReadOnlyList<string> GetOrderedChapterIds()
    {
        return _root.CombatContentLookup.GetOrderedCampaignChapters()
            .Where(chapter => !string.IsNullOrWhiteSpace(chapter.Id))
            .Select(chapter => chapter.Id)
            .ToList();
    }

    private IReadOnlyList<string> GetOrderedSiteIds(string chapterId)
    {
        if (!_root.CombatContentLookup.TryGetCampaignChapterDefinition(chapterId, out var chapter))
        {
            return Array.Empty<string>();
        }

        return chapter.SiteIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id =>
            {
                var hasDefinition = _root.CombatContentLookup.TryGetExpeditionSiteDefinition(id, out var site);
                return new
                {
                    Id = id,
                    Order = hasDefinition ? site.SiteOrder : int.MaxValue,
                };
            })
            .OrderBy(entry => entry.Order)
            .ThenBy(entry => entry.Id, StringComparer.Ordinal)
            .Select(entry => entry.Id)
            .ToList();
    }

    private IReadOnlyList<TownRecruitCardViewState> BuildRecruitCards(GameSessionState session)
    {
        var cards = new List<TownRecruitCardViewState>(4);
        for (var i = 0; i < 4; i++)
        {
            var offer = i < session.RecruitOffers.Count ? session.RecruitOffers[i] : null;
            cards.Add(new TownRecruitCardViewState(
                offer == null ? Localize(GameLocalizationTables.UITown, "ui.town.recruit.empty", "Empty Slot") : _contentText.GetArchetypeName(offer.UnitBlueprintId),
                BuildRecruitCardBody(offer),
                Localize(GameLocalizationTables.UITown, "ui.town.action.recruit", "Recruit"),
                BuildRecruitCardTooltip(offer),
                offer != null));
        }

        return cards;
    }

    private IReadOnlyList<TownDeployButtonViewState> BuildDeployButtons(ProfileView profile, LoadoutView loadout)
    {
        return loadout.Deployments
            .Select(entry =>
            {
                var hero = profile.Heroes.FirstOrDefault(x => x.HeroId == entry.HeroId);
                var heroName = hero?.DisplayName ?? Localize(GameLocalizationTables.UICommon, "ui.common.empty", "Empty");
                return new TownDeployButtonViewState(entry.Anchor, $"{LocalizeAnchor(entry.Anchor)}\n{heroName}");
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
                GameLocalizationTables.UITown,
                "ui.town.help.body",
                "1) Check chapter/site 2) confirm recruit and deploy 3) set posture 4) start the expedition."),
            Localize(GameLocalizationTables.UICommon, "ui.common.hide", "Hide"));
    }

    private string BuildReturnToStartTooltip(GameSessionState session)
    {
        return IsReturnToStartBlocked(session)
            ? Localize(GameLocalizationTables.UITown, "ui.town.tooltip.return_start_locked", "Blocked while an authored run or smoke battle is still active.")
            : Localize(GameLocalizationTables.UITown, "ui.town.tooltip.return_start", "Leave the local run shell and go back to Boot.");
    }

    private string BuildExpeditionTooltip(GameSessionState session)
    {
        return session.HasPendingRewardSettlement
            ? Localize(GameLocalizationTables.UITown, "ui.town.tooltip.expedition_reward", "Open Reward to settle the previous node before continuing.")
            : session.CanResumeExpedition
                ? Localize(GameLocalizationTables.UITown, "ui.town.tooltip.expedition_resume", "Resume the authored expedition from the currently selected route.")
                : Localize(GameLocalizationTables.UITown, "ui.town.tooltip.expedition_start", "Begin the authored expedition loop with the current preparation.");
    }

    private void NormalizeUiSelections(GameSessionState session)
    {
        _selectedHeroIndex = session.Profile.Heroes.Count == 0
            ? 0
            : WrapIndex(_selectedHeroIndex, session.Profile.Heroes.Count);

        var selectableItems = GetSelectableItems();
        _selectedItemIndex = selectableItems.Count == 0
            ? 0
            : WrapIndex(_selectedItemIndex, selectableItems.Count);

        var passiveNodes = GetCurrentPassiveNodes();
        _selectedPassiveNodeIndex = passiveNodes.Count == 0
            ? 0
            : WrapIndex(_selectedPassiveNodeIndex, passiveNodes.Count);

        var permanentIds = GetUnlockedPermanentAugmentIds();
        _selectedPermanentIndex = permanentIds.Count == 0
            ? 0
            : WrapIndex(_selectedPermanentIndex, permanentIds.Count);
    }

    private HeroInstanceRecord? GetSelectedHero()
    {
        var heroes = _root.SessionState.Profile.Heroes;
        return heroes.Count == 0 ? null : heroes[WrapIndex(_selectedHeroIndex, heroes.Count)];
    }

    private IReadOnlyList<InventoryItemRecord> GetSelectableItems()
    {
        if (_root.SessionState.Profile.Inventory.Count == 0)
        {
            return Array.Empty<InventoryItemRecord>();
        }

        var selectedHero = GetSelectedHero();
        var orderedItems = _root.SessionState.Profile.Inventory
            .OrderBy(item => item.ItemBaseId, StringComparer.Ordinal)
            .ThenBy(item => item.ItemInstanceId, StringComparer.Ordinal)
            .ToList();
        if (selectedHero == null)
        {
            return orderedItems;
        }

        var equippedIds = new HashSet<string>(
            selectedHero.EquippedItemIds.Where(id => !string.IsNullOrWhiteSpace(id)),
            StringComparer.Ordinal);
        return orderedItems
            .OrderByDescending(item => equippedIds.Contains(item.ItemInstanceId))
            .ThenBy(item => item.ItemBaseId, StringComparer.Ordinal)
            .ThenBy(item => item.ItemInstanceId, StringComparer.Ordinal)
            .ToList();
    }

    private InventoryItemRecord? GetSelectedItem()
    {
        var items = GetSelectableItems();
        return items.Count == 0 ? null : items[WrapIndex(_selectedItemIndex, items.Count)];
    }

    private IReadOnlyList<string> GetAvailableBoardIds()
    {
        var boardIds = _root.CombatContentLookup.GetCanonicalPassiveBoardIds()
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var selectedHero = GetSelectedHero();
        if (selectedHero == null || boardIds.Count == 0)
        {
            return boardIds;
        }

        var classMatched = boardIds
            .Where(boardId => !_root.CombatContentLookup.TryGetPassiveBoardDefinition(boardId, out var board)
                              || string.IsNullOrWhiteSpace(board.ClassId)
                              || string.Equals(board.ClassId, selectedHero.ClassId, StringComparison.Ordinal))
            .ToList();
        return classMatched.Count > 0 ? classMatched : boardIds;
    }

    private IReadOnlyList<PassiveNodeDefinition> GetCurrentPassiveNodes()
    {
        var selectedHero = GetSelectedHero();
        if (selectedHero == null)
        {
            return Array.Empty<PassiveNodeDefinition>();
        }

        var loadout = _root.SessionState.Profile.HeroLoadouts.FirstOrDefault(record =>
            string.Equals(record.HeroId, selectedHero.HeroId, StringComparison.Ordinal));
        if (loadout == null
            || string.IsNullOrWhiteSpace(loadout.PassiveBoardId)
            || !_root.CombatContentLookup.TryGetPassiveBoardDefinition(loadout.PassiveBoardId, out var board))
        {
            return Array.Empty<PassiveNodeDefinition>();
        }

        return board.Nodes
            .Where(node => node != null
                           && !string.IsNullOrWhiteSpace(node.Id)
                           && string.Equals(node.BoardId, board.Id, StringComparison.Ordinal))
            .OrderBy(node => node.BoardDepth)
            .ThenBy(node => node.Id, StringComparer.Ordinal)
            .ToList();
    }

    private PassiveNodeDefinition? GetSelectedPassiveNode()
    {
        var nodes = GetCurrentPassiveNodes();
        return nodes.Count == 0 ? null : nodes[WrapIndex(_selectedPassiveNodeIndex, nodes.Count)];
    }

    private IReadOnlyList<string> GetUnlockedPermanentAugmentIds()
    {
        var unlockedIds = new List<string>();
        foreach (var augmentId in _root.SessionState.Profile.UnlockedPermanentAugmentIds)
        {
            if (string.IsNullOrWhiteSpace(augmentId)
                || unlockedIds.Contains(augmentId, StringComparer.Ordinal)
                || !_root.CombatContentLookup.TryGetAugmentDefinition(augmentId, out var augment)
                || !augment.IsPermanent)
            {
                continue;
            }

            unlockedIds.Add(augmentId);
        }

        return unlockedIds;
    }

    private string GetSelectedPermanentAugmentId()
    {
        var augmentIds = GetUnlockedPermanentAugmentIds();
        return augmentIds.Count == 0 ? string.Empty : augmentIds[WrapIndex(_selectedPermanentIndex, augmentIds.Count)];
    }

    private string GetEquippedPermanentAugmentId(GameSessionState session)
    {
        return session.Profile.PermanentAugmentLoadouts
            .FirstOrDefault(record => string.Equals(record.BlueprintId, session.Profile.ActiveBlueprintId, StringComparison.Ordinal))
            ?.EquippedAugmentIds.FirstOrDefault() ?? string.Empty;
    }

    private ScoutDirective ResolveScoutDirective()
    {
        var teamPlan = _root.SessionState.CurrentTeamPlan;
        if (teamPlan.NeedsFrontline)
        {
            return new ScoutDirective { Kind = SM.Core.Contracts.ScoutDirectiveKind.Frontline };
        }

        if (teamPlan.NeedsSupport)
        {
            return new ScoutDirective { Kind = SM.Core.Contracts.ScoutDirectiveKind.Support };
        }

        if (teamPlan.NeedsBackline)
        {
            return new ScoutDirective { Kind = SM.Core.Contracts.ScoutDirectiveKind.Backline };
        }

        if (teamPlan.PrefersMagical)
        {
            return new ScoutDirective { Kind = SM.Core.Contracts.ScoutDirectiveKind.Magical };
        }

        if (teamPlan.PrefersPhysical)
        {
            return new ScoutDirective { Kind = SM.Core.Contracts.ScoutDirectiveKind.Physical };
        }

        return _root.SessionState.SelectedTeamPosture switch
        {
            TeamPostureType.HoldLine => new ScoutDirective { Kind = SM.Core.Contracts.ScoutDirectiveKind.Frontline },
            TeamPostureType.ProtectCarry => new ScoutDirective { Kind = SM.Core.Contracts.ScoutDirectiveKind.Support },
            TeamPostureType.AllInBackline => new ScoutDirective { Kind = SM.Core.Contracts.ScoutDirectiveKind.Backline },
            _ => new ScoutDirective { Kind = SM.Core.Contracts.ScoutDirectiveKind.Physical },
        };
    }

    private void RetrainSelectedHero(SM.Core.Contracts.RetrainOperationKind operation)
    {
        var selectedHero = GetSelectedHero();
        if (selectedHero == null)
        {
            Refresh("재훈련할 유닛이 없습니다.");
            return;
        }

        var result = _root.SessionState.RetrainHero(selectedHero.HeroId, operation);
        var successMessage = operation switch
        {
            SM.Core.Contracts.RetrainOperationKind.RerollFlexActive => Localize(GameLocalizationTables.UITown, "ui.town.status.retrain_active", "Flex active retrained."),
            SM.Core.Contracts.RetrainOperationKind.RerollFlexPassive => Localize(GameLocalizationTables.UITown, "ui.town.status.retrain_passive", "Flex passive retrained."),
            _ => Localize(GameLocalizationTables.UITown, "ui.town.status.retrain_full", "Full retrain complete."),
        };
        Refresh(result.IsSuccess
            ? successMessage
            : result.Error ?? "Retrain failed.");
    }

    private string BuildEconomyRailText(GameSessionState session, ProfileView profile)
    {
        var recruitPhase = session.RecruitPhase;
        var recruitPity = session.RecruitPity;
        var scoutDirective = ResolveScoutDirective();
        var builder = new StringBuilder();
        builder.AppendLine(Localize(GameLocalizationTables.UITown, "ui.town.economy.gold", "Gold: {0}", profile.Gold));
        builder.AppendLine(Localize(GameLocalizationTables.UITown, "ui.town.economy.echo", "Echo: {0}", profile.Echo));
        builder.AppendLine(Localize(
            GameLocalizationTables.UITown,
            "ui.town.economy.refresh_state",
            "Refresh: {0}",
            recruitPhase.FreeRefreshesRemaining > 0
                ? Localize(GameLocalizationTables.UITown, "ui.town.economy.refresh_available", "Free refresh available")
                : Localize(GameLocalizationTables.UITown, "ui.town.economy.refresh_used", "Free refresh used")));
        builder.AppendLine(Localize(GameLocalizationTables.UITown, "ui.town.economy.refresh_cost", "Next reroll: {0} Gold", session.CurrentRecruitRefreshCost));
        builder.AppendLine(Localize(
            GameLocalizationTables.UITown,
            "ui.town.economy.scout",
            "Scout: {0} / {1}",
            session.CanUseScout
                ? Localize(GameLocalizationTables.UITown, "ui.town.economy.available", "Available")
                : Localize(GameLocalizationTables.UITown, "ui.town.economy.used", "Used"),
            LocalizeScoutDirective(scoutDirective.Kind)));
        builder.AppendLine(Localize(GameLocalizationTables.UITown, "ui.town.economy.rare_pity", "Rare pity: {0}", recruitPity.PacksSinceRarePlusSeen));
        builder.AppendLine(Localize(GameLocalizationTables.UITown, "ui.town.economy.epic_pity", "Epic pity: {0}", recruitPity.PacksSinceEpicSeen));
        return builder.ToString();
    }

    private string BuildRosterText(ProfileView profile, string selectedHeroId)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Localize(GameLocalizationTables.UITown, "ui.town.roster.header", "Roster"));
        foreach (var hero in profile.Heroes)
        {
            var inSquad = hero.IsInExpeditionSquad
                ? Localize(GameLocalizationTables.UITown, "ui.town.roster.tag.expedition", "[Expedition]")
                : Localize(GameLocalizationTables.UITown, "ui.town.roster.tag.reserve", "[Reserve]");
            var selectionMarker = string.Equals(hero.HeroId, selectedHeroId, StringComparison.Ordinal) ? ">" : "-";
            sb.AppendLine($"{selectionMarker} {inSquad} {hero.DisplayName} / {_contentText.GetRaceName(hero.RaceId)} / {_contentText.GetClassName(hero.ClassId)}");
        }

        return sb.ToString();
    }

    private string BuildRecruitSummary(GameSessionState session, ProfileView profile)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Localize(GameLocalizationTables.UITown, "ui.town.recruit.header", "Recruit Offers"));
        sb.AppendLine(Localize(GameLocalizationTables.UITown, "ui.town.recruit.current_count", "Current offers: {0}", session.RecruitOffers.Count));
        sb.AppendLine(Localize(GameLocalizationTables.UITown, "ui.town.recruit.reroll_cost", "Reroll cost: {0} Gold", session.CurrentRecruitRefreshCost));
        sb.AppendLine(Localize(GameLocalizationTables.UITown, "ui.town.recruit.roster_count", "Town roster: {0}/{1}", profile.HeroCount, SM.Meta.Model.MetaBalanceDefaults.TownRosterCap));
        return sb.ToString();
    }

    private string BuildSquadText(ProfileView profile, LoadoutView loadout)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Localize(GameLocalizationTables.UITown, "ui.town.squad.header", "Expedition Squad ({0}/8)", loadout.ExpeditionSquadCount));
        foreach (var heroId in loadout.ExpeditionSquadHeroIds)
        {
            var hero = profile.Heroes.FirstOrDefault(x => x.HeroId == heroId);
            if (hero != null)
            {
                sb.AppendLine($"- {hero.DisplayName}");
            }
        }

        return sb.ToString();
    }

    private string BuildDeployPreviewText(GameSessionState session, ProfileView profile, LoadoutView loadout)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Localize(GameLocalizationTables.UITown, "ui.town.deploy.squad_count", "Squad: {0}/8", loadout.ExpeditionSquadCount));
        sb.AppendLine(Localize(GameLocalizationTables.UITown, "ui.town.deploy.header", "Deploy Preview ({0}/4)", loadout.BattleDeployCount));
        sb.AppendLine(Localize(GameLocalizationTables.UITown, "ui.town.deploy.posture", "Team Posture: {0}", loadout.TeamPosture));
        sb.AppendLine(Localize(
            GameLocalizationTables.UITown,
            "ui.town.deploy.readiness",
            "Readiness: {0}",
            loadout.BattleDeployCount >= 4
                ? Localize(GameLocalizationTables.UITown, "ui.town.deploy.ready", "Ready for the next battle handoff.")
                : Localize(GameLocalizationTables.UITown, "ui.town.deploy.not_ready", "Assign more heroes before a full battle deploy.")));
        foreach (var deployment in loadout.Deployments)
        {
            var hero = profile.Heroes.FirstOrDefault(x => x.HeroId == deployment.HeroId);
            var heroName = hero?.DisplayName ?? Localize(GameLocalizationTables.UICommon, "ui.common.empty", "Empty");
            sb.AppendLine($"- {LocalizeAnchor(deployment.Anchor)}: {heroName}");
        }

        sb.AppendLine();
        if (session.HasPendingRewardSettlement)
        {
            sb.AppendLine(Localize(GameLocalizationTables.UITown, "ui.town.deploy.primary_reward", "Primary Action: Open Reward"));
            sb.AppendLine(Localize(GameLocalizationTables.UITown, "ui.town.deploy.pending_reward", "Reward settlement is pending. Open Reward before moving to the next node."));
        }
        else if (session.CanResumeExpedition)
        {
            var nextNode = session.GetSelectedExpeditionNode();
            var routeLabel = nextNode == null
                ? Localize(GameLocalizationTables.UITown, "ui.town.deploy.route_needed", "Route selection required")
                : $"{Localize(GameLocalizationTables.UIExpedition, nextNode.LabelKey, nextNode.Id)} -> {Localize(GameLocalizationTables.UIExpedition, nextNode.PlannedRewardKey, nextNode.Id)}";
            sb.AppendLine(Localize(GameLocalizationTables.UITown, "ui.town.deploy.primary_resume", "Primary Action: Resume Expedition"));
            sb.AppendLine(Localize(GameLocalizationTables.UITown, "ui.town.deploy.resume", "Resume Expedition: {0}", routeLabel));
        }
        else
        {
            sb.AppendLine(Localize(GameLocalizationTables.UITown, "ui.town.deploy.primary_start", "Primary Action: Start Expedition"));
            sb.AppendLine(Localize(GameLocalizationTables.UITown, "ui.town.deploy.start", "Start Expedition: begin the authored route from camp."));
        }

        if (session.LastRewardApplicationSummary.HasValue)
        {
            sb.AppendLine(Localize(
                GameLocalizationTables.UITown,
                "ui.town.deploy.last_reward",
                "Last Reward: {0}",
                session.LastRewardApplicationSummary.Resolve(_localization, _contentText)));
        }

        if (session.LastExpeditionEffectMessage.HasValue)
        {
            sb.AppendLine(Localize(
                GameLocalizationTables.UITown,
                "ui.town.deploy.last_effect",
                "Last Node Effect: {0}",
                session.LastExpeditionEffectMessage.Resolve(_localization, _contentText)));
        }

        if (session.LastPermanentUnlockSummary.HasValue)
        {
            sb.AppendLine(Localize(
                GameLocalizationTables.UITown,
                "ui.town.deploy.last_permanent_unlock",
                "Permanent Progress: {0}",
                session.LastPermanentUnlockSummary.Resolve(_localization, _contentText)));
        }

        return sb.ToString();
    }

    private string BuildRecruitCardBody(RecruitUnitPreview? offer)
    {
        if (offer == null)
        {
            return Localize(GameLocalizationTables.UITown, "ui.town.recruit.none", "No recruit offer is available.");
        }

        _root.CombatContentLookup.TryGetArchetype(offer.UnitBlueprintId, out var archetype);
        CombatArchetypeTemplate? template = null;
        if (_root.CombatContentLookup.TryGetCombatSnapshot(out var snapshot, out _))
        {
            snapshot.Archetypes.TryGetValue(offer.UnitBlueprintId, out template);
        }

        var slotBadge = offer.Metadata.SlotType switch
        {
            SM.Core.Contracts.RecruitOfferSlotType.OnPlan => Localize(GameLocalizationTables.UITown, "ui.town.recruit.badge.on_plan", "[On Plan]"),
            SM.Core.Contracts.RecruitOfferSlotType.Protected => offer.Metadata.BiasedByScout
                ? Localize(GameLocalizationTables.UITown, "ui.town.recruit.badge.protected_scout", "[Protected][Scout]")
                : Localize(GameLocalizationTables.UITown, "ui.town.recruit.badge.protected", "[Protected]"),
            _ => offer.Metadata.BiasedByScout
                ? Localize(GameLocalizationTables.UITown, "ui.town.recruit.badge.scout", "[Scout]")
                : $"[{LocalizeRecruitSlotType(offer.Metadata.SlotType)}]",
        };
        var formation = archetype?.BehaviorProfile?.FormationLine is { } line
            ? LocalizeFormationLine(line)
            : Localize(GameLocalizationTables.UICommon, "ui.common.unknown", "Unknown");
        var roleFantasy = string.IsNullOrWhiteSpace(archetype?.RoleTag)
            ? formation
            : $"{archetype.RoleTag} / {formation}";
        var keyTags = template?.RecruitPlanTags?
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Take(3)
            .ToArray() ?? Array.Empty<string>();
        var counterHints = template?.Governance?.DeclaredCounterTools?
            .Select(tool => tool.Tool)
            .Where(tool => !string.IsNullOrWhiteSpace(tool))
            .Take(2)
            .ToArray() ?? Array.Empty<string>();

        return string.Join(
            "\n",
            $"{slotBadge} {LocalizeRecruitTier(offer.Metadata.Tier)} / {LocalizePlanFit(offer.Metadata.PlanFit)} / {offer.Metadata.GoldCost} Gold",
            $"{_contentText.GetRaceName(archetype?.Race.Id ?? string.Empty)} / {_contentText.GetClassName(archetype?.Class.Id ?? string.Empty)} / {roleFantasy}",
            $"{Localize(GameLocalizationTables.UITown, "ui.town.recruit.tags", "Tags")}: {(keyTags.Length == 0 ? Localize(GameLocalizationTables.UICommon, "ui.common.none", "None") : string.Join(", ", keyTags))}",
            $"{Localize(GameLocalizationTables.UITown, "ui.town.recruit.counter", "Counter")}: {(counterHints.Length == 0 ? Localize(GameLocalizationTables.UICommon, "ui.common.none", "None") : string.Join(", ", counterHints))}");
    }

    private string BuildRecruitCardTooltip(RecruitUnitPreview? offer)
    {
        if (offer == null)
        {
            return Localize(GameLocalizationTables.UITown, "ui.town.recruit.tooltip.empty", "No recruit offer is available in this slot.");
        }

        var slotMeaning = offer.Metadata.SlotType switch
        {
            SM.Core.Contracts.RecruitOfferSlotType.OnPlan => Localize(GameLocalizationTables.UITown, "ui.town.recruit.tooltip.on_plan", "On-plan offer that matches the current build direction."),
            SM.Core.Contracts.RecruitOfferSlotType.Protected => Localize(GameLocalizationTables.UITown, "ui.town.recruit.tooltip.protected", "Protected offer kept stable for this town visit."),
            _ => Localize(GameLocalizationTables.UITown, "ui.town.recruit.tooltip.standard", "Standard recruit offer for the current town visit."),
        };

        return Localize(
            GameLocalizationTables.UITown,
            "ui.town.recruit.tooltip.summary",
            "{0} Cost: {1} Gold.",
            slotMeaning,
            offer.Metadata.GoldCost);
    }

    private string ResolvePlayerId()
    {
        return _root.ActiveProfileId;
    }

    private string BuildSelectedHeroSummary(
        GameSessionState session,
        HeroInstanceRecord? selectedHero,
        InventoryItemRecord? selectedItem,
        string selectedNodeId,
        int retrainActiveCost,
        int retrainPassiveCost,
        int fullRetrainCost,
        DismissRefundResult dismissRefund)
    {
        var builder = new StringBuilder();
        builder.AppendLine(_buildFormatter.BuildSelectedHeroSummary(session, selectedHero, selectedItem, selectedNodeId));
        if (selectedHero == null)
        {
            return builder.ToString().TrimEnd();
        }

        if (selectedItem != null)
        {
            builder.AppendLine(Localize(GameLocalizationTables.UITown, "ui.town.selected_hero.refit_preview", "Refit Preview: {0} Echo", MetaBalanceDefaults.RefitEchoCost));
        }

        builder.AppendLine(Localize(GameLocalizationTables.UITown, "ui.town.selected_hero.retrain_active", "Retrain Active: {0} Echo", retrainActiveCost));
        builder.AppendLine(Localize(GameLocalizationTables.UITown, "ui.town.selected_hero.retrain_passive", "Retrain Passive: {0} Echo", retrainPassiveCost));
        builder.AppendLine(Localize(GameLocalizationTables.UITown, "ui.town.selected_hero.retrain_full", "Full Retrain: {0} Echo", fullRetrainCost));
        builder.AppendLine(Localize(GameLocalizationTables.UITown, "ui.town.selected_hero.dismiss_refund", "Dismiss Refund: +{0} Gold / +{1} Echo", dismissRefund.GoldRefund, dismissRefund.EchoRefund));

        var loadout = session.Profile.HeroLoadouts.FirstOrDefault(record =>
            string.Equals(record.HeroId, selectedHero.HeroId, StringComparison.Ordinal));
        if (loadout != null)
        {
            builder.AppendLine(Localize(
                GameLocalizationTables.UITown,
                "ui.town.selected_hero.passive_nodes",
                "Passive Nodes: {0}/{1}",
                loadout.SelectedPassiveNodeIds.Count,
                PassiveBoardSelectionValidator.MaxActiveNodeCount));
        }

        return builder.ToString().TrimEnd();
    }

    private string BuildCycleHeroLabel(HeroInstanceRecord? selectedHero)
        => selectedHero == null
            ? Localize(GameLocalizationTables.UITown, "ui.town.action.cycle_hero", "Cycle Hero")
            : $"{Localize(GameLocalizationTables.UITown, "ui.town.action.cycle_hero", "Cycle Hero")}\n{selectedHero.Name}";

    private string BuildCycleItemLabel(InventoryItemRecord? selectedItem)
        => selectedItem == null
            ? Localize(GameLocalizationTables.UITown, "ui.town.action.cycle_item", "Cycle Item")
            : $"{Localize(GameLocalizationTables.UITown, "ui.town.action.cycle_item", "Cycle Item")}\n{_contentText.GetItemName(selectedItem.ItemBaseId)}";

    private string BuildCycleBoardLabel(HeroInstanceRecord? selectedHero)
    {
        if (selectedHero == null)
        {
            return Localize(GameLocalizationTables.UITown, "ui.town.action.cycle_board", "Cycle Board");
        }

        var loadout = _root.SessionState.Profile.HeroLoadouts.FirstOrDefault(record =>
            string.Equals(record.HeroId, selectedHero.HeroId, StringComparison.Ordinal));
        var boardName = string.IsNullOrWhiteSpace(loadout?.PassiveBoardId)
            ? Localize(GameLocalizationTables.UICommon, "ui.common.none", "None")
            : _contentText.GetPassiveBoardName(loadout.PassiveBoardId);
        return $"{Localize(GameLocalizationTables.UITown, "ui.town.action.cycle_board", "Cycle Board")}\n{boardName}";
    }

    private string BuildCycleNodeLabel(PassiveNodeDefinition? selectedNode)
        => selectedNode == null
            ? Localize(GameLocalizationTables.UITown, "ui.town.action.cycle_node", "Cycle Node")
            : $"{Localize(GameLocalizationTables.UITown, "ui.town.action.cycle_node", "Cycle Node")}\n{_contentText.GetPassiveNodeName(selectedNode.Id)}";

    private string BuildCyclePermanentLabel(string selectedPermanentId)
    {
        if (string.IsNullOrWhiteSpace(selectedPermanentId))
        {
            return Localize(GameLocalizationTables.UITown, "ui.town.action.cycle_permanent", "Cycle Permanent");
        }

        var equippedPermanentId = GetEquippedPermanentAugmentId(_root.SessionState);
        var equippedBadge = string.Equals(selectedPermanentId, equippedPermanentId, StringComparison.Ordinal)
            ? $" {Localize(GameLocalizationTables.UITown, "ui.town.permanent.equipped", "[Equipped]")}"
            : string.Empty;
        return $"{Localize(GameLocalizationTables.UITown, "ui.town.action.cycle_permanent", "Cycle Permanent")}\n{_contentText.GetAugmentName(selectedPermanentId)}{equippedBadge}";
    }

    private string BuildDefaultStatus(GameSessionState session)
    {
        if (session.LastPermanentUnlockSummary.HasValue)
        {
            return session.LastPermanentUnlockSummary.Resolve(_localization, _contentText);
        }

        if (session.HasPendingRewardSettlement)
        {
            return Localize(GameLocalizationTables.UITown, "ui.town.status.default.reward", "Reward settlement is pending. Finish the settlement or return to Town.");
        }

        return session.CanResumeExpedition
            ? Localize(GameLocalizationTables.UITown, "ui.town.status.default.resume", "Recruit, save, and resume the expedition.")
            : Localize(GameLocalizationTables.UITown, "ui.town.status.default.start", "Recruit, save, choose a chapter/site, and start the expedition.");
    }

    private static bool IsReturnToStartBlocked(GameSessionState session)
    {
        return session.HasActiveExpeditionRun || session.IsQuickBattleSmokeActive;
    }

    private static int WrapIndex(int index, int count)
    {
        if (count <= 0)
        {
            return 0;
        }

        var wrapped = index % count;
        return wrapped < 0 ? wrapped + count : wrapped;
    }

    private string LocalizeScoutDirective(SM.Core.Contracts.ScoutDirectiveKind kind)
    {
        return kind switch
        {
            SM.Core.Contracts.ScoutDirectiveKind.Frontline => Localize(GameLocalizationTables.UITown, "ui.town.scout.frontline", "Frontline"),
            SM.Core.Contracts.ScoutDirectiveKind.Backline => Localize(GameLocalizationTables.UITown, "ui.town.scout.backline", "Backline"),
            SM.Core.Contracts.ScoutDirectiveKind.Magical => Localize(GameLocalizationTables.UITown, "ui.town.scout.magical", "Magical"),
            SM.Core.Contracts.ScoutDirectiveKind.Support => Localize(GameLocalizationTables.UITown, "ui.town.scout.support", "Support"),
            _ => Localize(GameLocalizationTables.UITown, "ui.town.scout.physical", "Physical"),
        };
    }

    private string LocalizeRecruitSlotType(SM.Core.Contracts.RecruitOfferSlotType slotType)
    {
        return slotType switch
        {
            SM.Core.Contracts.RecruitOfferSlotType.OnPlan => Localize(GameLocalizationTables.UITown, "ui.town.recruit.slot.on_plan", "On Plan"),
            SM.Core.Contracts.RecruitOfferSlotType.Protected => Localize(GameLocalizationTables.UITown, "ui.town.recruit.slot.protected", "Protected"),
            SM.Core.Contracts.RecruitOfferSlotType.StandardB => Localize(GameLocalizationTables.UITown, "ui.town.recruit.slot.standard_b", "Standard B"),
            _ => Localize(GameLocalizationTables.UITown, "ui.town.recruit.slot.standard_a", "Standard A"),
        };
    }

    private string LocalizeRecruitTier(SM.Core.Contracts.RecruitTier tier)
    {
        return tier switch
        {
            SM.Core.Contracts.RecruitTier.Rare => Localize(GameLocalizationTables.UITown, "ui.town.recruit.tier.rare", "Rare"),
            SM.Core.Contracts.RecruitTier.Epic => Localize(GameLocalizationTables.UITown, "ui.town.recruit.tier.epic", "Epic"),
            _ => Localize(GameLocalizationTables.UITown, "ui.town.recruit.tier.common", "Common"),
        };
    }

    private string LocalizePlanFit(SM.Core.Contracts.CandidatePlanFit planFit)
    {
        return planFit switch
        {
            SM.Core.Contracts.CandidatePlanFit.OnPlan => Localize(GameLocalizationTables.UITown, "ui.town.recruit.plan.on_plan", "On Plan"),
            SM.Core.Contracts.CandidatePlanFit.Bridge => Localize(GameLocalizationTables.UITown, "ui.town.recruit.plan.bridge", "Bridge"),
            _ => Localize(GameLocalizationTables.UITown, "ui.town.recruit.plan.off_plan", "Off Plan"),
        };
    }

    private string LocalizeFormationLine(SM.Core.Contracts.FormationLine line)
    {
        return line switch
        {
            SM.Core.Contracts.FormationLine.Backline => Localize(GameLocalizationTables.UITown, "ui.town.recruit.formation.backline", "Backline"),
            SM.Core.Contracts.FormationLine.Midline => Localize(GameLocalizationTables.UITown, "ui.town.recruit.formation.midline", "Midline"),
            _ => Localize(GameLocalizationTables.UITown, "ui.town.recruit.formation.frontline", "Frontline"),
        };
    }

    private string LocalizeAnchor(DeploymentAnchorId anchor)
    {
        return Localize(GameLocalizationTables.UICommon, anchor.ToLocalizationKey(), anchor.ToDisplayName());
    }

    private string Localize(string table, string key, string fallback, params object[] args)
    {
        return _localization.LocalizeOrFallback(table, key, fallback, args);
    }
}
