using System.Linq;
using System.Text;
using SM.Combat.Model;
using UnityEngine;
using UnityEngine.UI;

namespace SM.Unity;

public sealed class TownScreenController : MonoBehaviour
{
    [SerializeField] private Text titleText = null!;
    [SerializeField] private Text rosterText = null!;
    [SerializeField] private Text recruitText = null!;
    [SerializeField] private RectTransform recruitCardsRoot = null!;
    [SerializeField] private Text squadText = null!;
    [SerializeField] private Text deployPreviewText = null!;
    [SerializeField] private Text currencyText = null!;
    [SerializeField] private Text statusText = null!;

    private GameSessionRoot _root = null!;
    private GameLocalizationController _localization = null!;
    private ContentTextResolver _contentText = null!;
    private DeploymentSetupPanelView? _deploymentPanel;

    private void Start()
    {
        _root = GameSessionRoot.Instance!;
        if (_root == null)
        {
            SetStatus("GameSessionRoot가 없습니다.");
            return;
        }

        _localization = _root.Localization;
        _contentText = new ContentTextResolver(_localization, _root.CombatContentLookup);
        _localization.LocaleChanged += HandleLocaleChanged;
        _root.SessionState.SetCurrentScene(SceneNames.Town);
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

    public void RecruitOffer0() => Recruit(0);
    public void RecruitOffer1() => Recruit(1);
    public void RecruitOffer2() => Recruit(2);

    public void RerollOffers()
    {
        if (!EnsureReady()) return;
        var result = _root.SessionState.RerollRecruitOffers();
        Refresh(result.IsSuccess
            ? Localize(GameLocalizationTables.UITown, "ui.town.status.reroll_success", "Recruit offers rerolled. (-{0} Gold)", SM.Meta.Model.MetaBalanceDefaults.RecruitRerollCost)
            : result.Error ?? Localize(GameLocalizationTables.UITown, "ui.town.error.reroll_failed", "Failed to reroll recruit offers."));
    }

    public void SaveProfile()
    {
        if (!EnsureReady()) return;
        _root.SaveProfile();
        Refresh(Localize(GameLocalizationTables.UITown, "ui.town.status.profile_saved", "Profile saved."));
    }

    public void LoadProfile()
    {
        if (!EnsureReady()) return;
        _root.BindProfile();
        Refresh(Localize(GameLocalizationTables.UITown, "ui.town.status.profile_loaded", "Profile reloaded."));
    }

    public void DebugStartExpedition()
    {
        if (!EnsureReady()) return;
        if (_root.SessionState.CanResumeExpedition)
        {
            _root.SceneFlow.GoToExpedition();
            return;
        }

        _root.SessionState.BeginNewExpedition();
        _root.SaveProfile();
        _root.SceneFlow.GoToExpedition();
    }

    public void QuickBattle()
    {
        if (!EnsureReady()) return;
        _root.SessionState.PrepareQuickBattleSmoke();
        _root.SaveProfile();
        _root.SceneFlow.GoToBattle();
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
            "TownDeploymentPanel",
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
        Refresh(Localize(GameLocalizationTables.UITown, "ui.town.status.team_posture", "Team posture: {0}", _root.SessionState.SelectedTeamPosture));
    }

    private void Recruit(int index)
    {
        if (!EnsureReady()) return;

        var result = _root.SessionState.Recruit(index);
        if (result.IsSuccess)
        {
            Refresh(Localize(
                GameLocalizationTables.UITown,
                "ui.town.status.recruit_success",
                "Recruited offer {0}. (-{1} Gold)",
                index + 1,
                SM.Meta.Model.MetaBalanceDefaults.RecruitCost));
            return;
        }

        Refresh(result.Error ?? Localize(GameLocalizationTables.UITown, "ui.town.error.recruit_failed", "Failed to recruit the selected offer."));
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
        AssertText(rosterText, nameof(rosterText));
        AssertText(recruitText, nameof(recruitText));
        AssertRect(recruitCardsRoot, nameof(recruitCardsRoot));
        AssertText(squadText, nameof(squadText));
        AssertText(deployPreviewText, nameof(deployPreviewText));
        AssertText(currencyText, nameof(currencyText));
        AssertText(statusText, nameof(statusText));
    }

    private static void AssertText(Text text, string fieldName)
    {
        if (text == null)
        {
            Debug.LogError($"[TownScreenController] Missing Text reference: {fieldName}");
        }
    }

    private static void AssertRect(RectTransform rectTransform, string fieldName)
    {
        if (rectTransform == null)
        {
            Debug.LogError($"[TownScreenController] Missing RectTransform reference: {fieldName}");
        }
    }

    private void Refresh(string message = "")
    {
        if (!EnsureReady()) return;

        var session = _root.SessionState;
        session.EnsureBattleDeployReady();

        titleText.text = Localize(GameLocalizationTables.UITown, "ui.town.title", "Town Operator UI");
        currencyText.text = Localize(
            GameLocalizationTables.UITown,
            "ui.town.currency.summary",
            "Gold: {0} | Perm Slots: {1} | Trait Reroll: {2}",
            session.Profile.Currencies.Gold,
            session.PermanentAugmentSlotCount,
            session.Profile.Currencies.TraitRerollCurrency);
        rosterText.text = BuildRosterText(session);
        recruitText.text = BuildRecruitSummary(session);
        squadText.text = BuildSquadText(session);
        deployPreviewText.text = BuildDeployPreviewText(session);
        RefreshRecruitCards(session);
        _deploymentPanel?.Refresh(session);
        statusText.text = string.IsNullOrWhiteSpace(message)
            ? session.CanResumeExpedition
                ? Localize(GameLocalizationTables.UITown, "ui.town.status.default.resume", "Recruit, save, resume the expedition, or run a quick battle.")
                : Localize(GameLocalizationTables.UITown, "ui.town.status.default.start", "Recruit, save, start an expedition, or run a quick battle.")
            : message;
    }

    private void RefreshRecruitCards(GameSessionState session)
    {
        for (var i = 0; i < 3; i++)
        {
            var card = recruitCardsRoot.Find($"RecruitCard{i + 1}");
            if (card == null)
            {
                continue;
            }

            var offer = i < session.RecruitOffers.Count ? session.RecruitOffers[i] : null;
            SetCardText(card, "TitleText", offer == null ? Localize(GameLocalizationTables.UITown, "ui.town.recruit.empty", "Empty Slot") : _contentText.GetArchetypeName(offer.ArchetypeId));
            SetCardText(card, "BodyText", BuildRecruitCardBody(offer));
            SetCardText(card, "RecruitButton/Label", Localize(GameLocalizationTables.UITown, "ui.town.action.recruit", "Recruit"));
        }
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }

        Debug.LogError($"[TownScreenController] {message}");
    }

    private static void SetCardText(Transform cardRoot, string childName, string value)
    {
        var child = cardRoot.Find(childName);
        if (child == null)
        {
            return;
        }

        var text = child.GetComponent<Text>();
        if (text != null)
        {
            text.text = value;
        }
    }

    private string BuildRosterText(GameSessionState session)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Localize(GameLocalizationTables.UITown, "ui.town.roster.header", "Roster"));
        foreach (var hero in session.Profile.Heroes)
        {
            var inSquad = session.ExpeditionSquadHeroIds.Contains(hero.HeroId)
                ? Localize(GameLocalizationTables.UITown, "ui.town.roster.tag.expedition", "[Expedition]")
                : Localize(GameLocalizationTables.UITown, "ui.town.roster.tag.reserve", "[Reserve]");
            sb.AppendLine($"- {inSquad} {hero.Name} / {_contentText.GetRaceName(hero.RaceId)} / {_contentText.GetClassName(hero.ClassId)}");
        }

        return sb.ToString();
    }

    private string BuildRecruitSummary(GameSessionState session)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Localize(GameLocalizationTables.UITown, "ui.town.recruit.header", "Recruit Offers"));
        sb.AppendLine(Localize(GameLocalizationTables.UITown, "ui.town.recruit.current_count", "Current offers: {0}", session.RecruitOffers.Count));
        sb.AppendLine(Localize(GameLocalizationTables.UITown, "ui.town.recruit.cost", "Recruit cost: {0} Gold", SM.Meta.Model.MetaBalanceDefaults.RecruitCost));
        sb.AppendLine(Localize(GameLocalizationTables.UITown, "ui.town.recruit.reroll_cost", "Reroll cost: {0} Gold", SM.Meta.Model.MetaBalanceDefaults.RecruitRerollCost));
        sb.Append(Localize(GameLocalizationTables.UITown, "ui.town.recruit.roster_count", "Town roster: {0}/{1}", session.Profile.Heroes.Count, SM.Meta.Model.MetaBalanceDefaults.TownRosterCap));
        return sb.ToString();
    }

    private string BuildSquadText(GameSessionState session)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Localize(GameLocalizationTables.UITown, "ui.town.squad.header", "Expedition Squad ({0}/8)", session.ExpeditionSquadHeroIds.Count));
        foreach (var heroId in session.ExpeditionSquadHeroIds)
        {
            var hero = session.Profile.Heroes.FirstOrDefault(x => x.HeroId == heroId);
            if (hero != null)
            {
                sb.AppendLine($"- {hero.Name}");
            }
        }

        return sb.ToString();
    }

    private string BuildDeployPreviewText(GameSessionState session)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Localize(GameLocalizationTables.UITown, "ui.town.deploy.header", "Deploy Preview ({0}/4)", session.BattleDeployHeroIds.Count));
        sb.AppendLine(Localize(GameLocalizationTables.UITown, "ui.town.deploy.posture", "Team Posture: {0}", session.SelectedTeamPosture));
        foreach (var (anchor, heroId) in session.EnumerateDeploymentAssignments())
        {
            var hero = session.Profile.Heroes.FirstOrDefault(x => x.HeroId == heroId);
            var heroName = hero?.Name ?? Localize(GameLocalizationTables.UICommon, "ui.common.empty", "Empty");
            sb.AppendLine($"- {LocalizeAnchor(anchor)}: {heroName}");
        }

        sb.AppendLine();
        if (session.IsQuickBattleSmokeActive)
        {
            sb.AppendLine(Localize(GameLocalizationTables.UITown, "ui.town.deploy.quick_battle_ready", "Quick battle smoke is ready."));
        }
        else if (session.CanResumeExpedition)
        {
            var nextNode = session.GetSelectedExpeditionNode();
            var routeLabel = nextNode == null
                ? Localize(GameLocalizationTables.UITown, "ui.town.deploy.route_needed", "Route selection required")
                : $"{ResolveExpeditionNodeLabel(nextNode)} -> {ResolveExpeditionNodeReward(nextNode)}";
            sb.AppendLine(Localize(GameLocalizationTables.UITown, "ui.town.deploy.resume", "Resume Expedition: {0}", routeLabel));
        }
        else
        {
            sb.AppendLine(Localize(GameLocalizationTables.UITown, "ui.town.deploy.quick_battle_safe", "Quick battle does not consume expedition progress."));
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

    private void CycleAnchor(DeploymentAnchorId anchor)
    {
        if (!EnsureReady()) return;
        _root.SessionState.CycleDeploymentAssignment(anchor);
        Refresh(Localize(GameLocalizationTables.UITown, "ui.town.status.anchor_cycled", "{0} deployment updated.", LocalizeAnchor(anchor)));
    }

    private string BuildRecruitCardBody(RecruitOffer? offer)
    {
        if (offer == null)
        {
            return Localize(GameLocalizationTables.UITown, "ui.town.recruit.none", "No recruit offer is available.");
        }

        return string.Join(
            "\n",
            $"{_contentText.GetRaceName(offer.RaceId)} / {_contentText.GetClassName(offer.ClassId)}",
            $"+ {_contentText.GetTraitName(offer.ArchetypeId, offer.PositiveTraitId)}",
            $"- {_contentText.GetTraitName(offer.ArchetypeId, offer.NegativeTraitId)}");
    }

    private string ResolveExpeditionNodeLabel(ExpeditionNodeViewModel node)
    {
        return Localize(GameLocalizationTables.UIExpedition, node.LabelKey, node.Id);
    }

    private string ResolveExpeditionNodeReward(ExpeditionNodeViewModel node)
    {
        return Localize(GameLocalizationTables.UIExpedition, node.PlannedRewardKey, node.Id);
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
