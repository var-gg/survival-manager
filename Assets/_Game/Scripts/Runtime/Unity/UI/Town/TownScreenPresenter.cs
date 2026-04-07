using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SM.Combat.Model;
using SM.Meta.Model;
using UnityEngine;

namespace SM.Unity.UI.Town;

public sealed class TownScreenPresenter
{
    private readonly GameSessionRoot _root;
    private readonly GameLocalizationController _localization;
    private readonly ContentTextResolver _contentText;
    private readonly TownScreenView _view;

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
        _root.SaveProfile();
        Refresh(Localize(GameLocalizationTables.UITown, "ui.town.status.profile_saved", "Profile saved."));
    }

    public void LoadProfile()
    {
        _root.BindProfile();
        Refresh(Localize(GameLocalizationTables.UITown, "ui.town.status.profile_loaded", "Profile reloaded."));
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

        _root.ReturnToSessionMenu();
    }

    public void OpenExpedition()
    {
        if (_root.SessionState.HasPendingRewardSettlement)
        {
            _root.SaveProfile();
            _root.SceneFlow.GoToReward();
            return;
        }

        if (_root.SessionState.CanResumeExpedition)
        {
            _root.SaveProfile();
            _root.SceneFlow.GoToExpedition();
            return;
        }

        _root.SessionState.BeginNewExpedition();
        _root.SaveProfile();
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

        _root.SessionState.PrepareQuickBattleSmoke();
        _root.SaveProfile();
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
        var playerId = ResolvePlayerId();
        var profile = _root.ProfileQueries.GetProfileView(playerId);
        var loadout = _root.ProfileQueries.GetLoadoutView(playerId);
        var chapterIds = GetOrderedChapterIds();
        var siteIds = GetOrderedSiteIds(session.SelectedCampaignChapterId);
        var canCycleChapter = session.CanChangeCampaignSelection && chapterIds.Count > 1;
        var canCycleSite = session.CanChangeCampaignSelection && siteIds.Count > 1;

        return new TownScreenViewState(
            Localize(GameLocalizationTables.UITown, "ui.town.title", "Town Operator UI"),
            BuildLocaleStatus(),
            GetLocaleButtonLabel("ko", "한국어"),
            GetLocaleButtonLabel("en", "English"),
            BuildCampaignSummary(session),
            Localize(GameLocalizationTables.UITown, "ui.town.action.prev_chapter", "Prev Chapter"),
            canCycleChapter,
            Localize(GameLocalizationTables.UITown, "ui.town.action.next_chapter", "Next Chapter"),
            canCycleChapter,
            Localize(GameLocalizationTables.UITown, "ui.town.action.prev_site", "Prev Site"),
            canCycleSite,
            Localize(GameLocalizationTables.UITown, "ui.town.action.next_site", "Next Site"),
            canCycleSite,
            Localize(
                GameLocalizationTables.UITown,
                "ui.town.currency.summary",
                "Gold: {0} | Echo: {1} | Perm Slots: {2}",
                profile.Gold,
                profile.Echo,
                profile.PermanentAugmentSlotCount),
            BuildRosterText(profile),
            BuildRecruitSummary(session, profile),
            BuildRecruitCards(session),
            BuildSquadText(profile, loadout),
            BuildDeployPreviewText(session, profile, loadout),
            BuildDeployButtons(profile, loadout),
            Localize(GameLocalizationTables.UICommon, "ui.common.posture", "Posture") + "\n" + session.SelectedTeamPosture,
            string.IsNullOrWhiteSpace(message)
                ? session.HasPendingRewardSettlement
                    ? Localize(GameLocalizationTables.UITown, "ui.town.status.default.reward", "Reward settlement is pending. Finish the settlement or return to Town.")
                    : session.CanResumeExpedition
                        ? Localize(GameLocalizationTables.UITown, "ui.town.status.default.resume", "Recruit, save, and resume the expedition.")
                        : Localize(GameLocalizationTables.UITown, "ui.town.status.default.start", "Recruit, save, choose a chapter/site, and start the expedition.")
                : message,
            Localize(GameLocalizationTables.UITown, "ui.town.action.reroll", "Reroll"),
            Localize(GameLocalizationTables.UICommon, "ui.common.save", "Save"),
            Localize(GameLocalizationTables.UICommon, "ui.common.load", "Load"),
            Localize(GameLocalizationTables.UICommon, "ui.common.return_start", "Return to Start"),
            !IsReturnToStartBlocked(session),
            session.HasPendingRewardSettlement
                ? Localize(GameLocalizationTables.UIReward, "ui.reward.action.open", "Open Reward")
                : session.CanResumeExpedition
                    ? Localize(GameLocalizationTables.UITown, "ui.town.action.resume_expedition", "Resume Expedition")
                    : Localize(GameLocalizationTables.UITown, "ui.town.action.start_expedition", "Start Expedition"),
            Localize(GameLocalizationTables.UITown, "ui.town.action.quick_battle_smoke", "Quick Battle (Smoke)"),
            session.CanStartQuickBattleSmoke);
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

        _root.SaveProfile();
        Refresh(Localize(GameLocalizationTables.UITown, "ui.town.status.chapter_changed", "Campaign chapter updated."));
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

        _root.SaveProfile();
        Refresh(Localize(GameLocalizationTables.UITown, "ui.town.status.site_changed", "Expedition site updated."));
    }

    private string BuildCampaignSummary(GameSessionState session)
    {
        var chapterName = _contentText.GetCampaignChapterName(session.SelectedCampaignChapterId);
        var siteName = _contentText.GetExpeditionSiteName(session.SelectedCampaignSiteId);
        var sb = new StringBuilder();
        sb.AppendLine(Localize(GameLocalizationTables.UITown, "ui.town.campaign.header", "Campaign Selection"));
        sb.AppendLine(Localize(GameLocalizationTables.UITown, "ui.town.campaign.chapter", "Chapter: {0}", chapterName));
        sb.AppendLine(Localize(GameLocalizationTables.UITown, "ui.town.campaign.site", "Site: {0}", siteName));
        sb.AppendLine(_contentText.GetExpeditionSiteDescription(session.SelectedCampaignSiteId));
        sb.AppendLine();
        sb.AppendLine(session.CanChangeCampaignSelection
            ? Localize(GameLocalizationTables.UITown, "ui.town.campaign.unlocked", "Selection unlocked: choose the next chapter and site before departure.")
            : Localize(GameLocalizationTables.UITown, "ui.town.campaign.locked", "Selection locked: active expedition run must keep the same chapter and site."));

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

    private string BuildRosterText(ProfileView profile)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Localize(GameLocalizationTables.UITown, "ui.town.roster.header", "Roster"));
        foreach (var hero in profile.Heroes)
        {
            var inSquad = hero.IsInExpeditionSquad
                ? Localize(GameLocalizationTables.UITown, "ui.town.roster.tag.expedition", "[Expedition]")
                : Localize(GameLocalizationTables.UITown, "ui.town.roster.tag.reserve", "[Reserve]");
            sb.AppendLine($"- {inSquad} {hero.DisplayName} / {_contentText.GetRaceName(hero.RaceId)} / {_contentText.GetClassName(hero.ClassId)}");
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
        sb.AppendLine(Localize(GameLocalizationTables.UITown, "ui.town.deploy.header", "Deploy Preview ({0}/4)", loadout.BattleDeployCount));
        sb.AppendLine(Localize(GameLocalizationTables.UITown, "ui.town.deploy.posture", "Team Posture: {0}", loadout.TeamPosture));
        foreach (var deployment in loadout.Deployments)
        {
            var hero = profile.Heroes.FirstOrDefault(x => x.HeroId == deployment.HeroId);
            var heroName = hero?.DisplayName ?? Localize(GameLocalizationTables.UICommon, "ui.common.empty", "Empty");
            sb.AppendLine($"- {LocalizeAnchor(deployment.Anchor)}: {heroName}");
        }

        sb.AppendLine();
        if (session.HasPendingRewardSettlement)
        {
            sb.AppendLine(Localize(GameLocalizationTables.UITown, "ui.town.deploy.pending_reward", "Reward settlement is pending. Open Reward before moving to the next node."));
        }
        else if (session.CanResumeExpedition)
        {
            var nextNode = session.GetSelectedExpeditionNode();
            var routeLabel = nextNode == null
                ? Localize(GameLocalizationTables.UITown, "ui.town.deploy.route_needed", "Route selection required")
                : $"{Localize(GameLocalizationTables.UIExpedition, nextNode.LabelKey, nextNode.Id)} -> {Localize(GameLocalizationTables.UIExpedition, nextNode.PlannedRewardKey, nextNode.Id)}";
            sb.AppendLine(Localize(GameLocalizationTables.UITown, "ui.town.deploy.resume", "Resume Expedition: {0}", routeLabel));
        }
        else
        {
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
            SM.Core.Contracts.RecruitOfferSlotType.OnPlan => "[OnPlan]",
            SM.Core.Contracts.RecruitOfferSlotType.Protected => offer.Metadata.BiasedByScout ? "[Protected][Scout]" : "[Protected]",
            _ => offer.Metadata.BiasedByScout ? "[Scout]" : $"[{offer.Metadata.SlotType}]",
        };
        var formation = archetype?.BehaviorProfile?.FormationLine.ToString() ?? "Unknown";
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
            $"{slotBadge} {offer.Metadata.Tier} / {offer.Metadata.PlanFit} / {offer.Metadata.GoldCost} Gold",
            $"{_contentText.GetRaceName(archetype?.Race.Id ?? string.Empty)} / {_contentText.GetClassName(archetype?.Class.Id ?? string.Empty)} / {roleFantasy}",
            $"Tags: {(keyTags.Length == 0 ? "None" : string.Join(", ", keyTags))}",
            $"Counter: {(counterHints.Length == 0 ? "None" : string.Join(", ", counterHints))}");
    }

    private string ResolvePlayerId()
    {
        return _root.ActiveProfileId;
    }

    private static bool IsReturnToStartBlocked(GameSessionState session)
    {
        return session.HasActiveExpeditionRun || session.IsQuickBattleSmokeActive;
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
