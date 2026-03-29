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
    private DeploymentSetupPanelView? _deploymentPanel;

    private void Start()
    {
        _root = GameSessionRoot.Instance!;
        if (_root == null)
        {
            SetStatus("GameSessionRoot가 없습니다.");
            return;
        }

        _root.SessionState.SetCurrentScene(SceneNames.Town);
        EnsureRuntimeControls();
        Refresh();
    }

    public void RecruitOffer0() => Recruit(0);
    public void RecruitOffer1() => Recruit(1);
    public void RecruitOffer2() => Recruit(2);

    public void RerollOffers()
    {
        if (!EnsureReady()) return;
        _root.SessionState.RerollRecruitOffers();
        Refresh("Recruit 후보를 리롤했습니다.");
    }

    public void SaveProfile()
    {
        if (!EnsureReady()) return;
        _root.SaveProfile();
        Refresh("프로필을 저장했습니다.");
    }

    public void LoadProfile()
    {
        if (!EnsureReady()) return;
        _root.BindProfile();
        Refresh("프로필을 다시 불러왔습니다.");
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
        Refresh($"Team posture: {_root.SessionState.SelectedTeamPosture}");
    }

    private void Recruit(int index)
    {
        if (!EnsureReady()) return;

        if (_root.SessionState.Recruit(index))
        {
            Refresh($"후보 {index + 1}을 영입했습니다.");
            return;
        }

        Refresh("영입에 실패했습니다.");
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

        titleText.text = "Town Operator UI";
        currencyText.text = $"Gold: {session.Profile.Currencies.Gold} | Perm Slots: {session.PermanentAugmentSlotCount} | Trait Reroll: {session.Profile.Currencies.TraitRerollCurrency}";
        rosterText.text = BuildRosterText(session);
        recruitText.text = BuildRecruitSummary(session);
        squadText.text = BuildSquadText(session);
        deployPreviewText.text = BuildDeployPreviewText(session);
        RefreshRecruitCards(session);
        _deploymentPanel?.Refresh(session);
        statusText.text = string.IsNullOrWhiteSpace(message)
            ? session.CanResumeExpedition
                ? "영입/저장/Debug Start(원정 재개)/Quick Battle을 바로 눌러 확인하세요."
                : "영입/저장/원정/Quick Battle을 바로 눌러 확인하세요."
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
            SetCardText(card, "TitleText", offer?.Name ?? "빈 슬롯");
            SetCardText(card, "BodyText", offer == null
                ? "후보가 없습니다."
                : $"{offer.RaceId} / {offer.ClassId}\n+{offer.PositiveTraitId}\n-{offer.NegativeTraitId}");
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

    private static string BuildRosterText(GameSessionState session)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Roster");
        foreach (var hero in session.Profile.Heroes)
        {
            var inSquad = session.ExpeditionSquadHeroIds.Contains(hero.HeroId) ? "[원정]" : "[대기]";
            sb.AppendLine($"- {inSquad} {hero.Name} / {hero.RaceId} / {hero.ClassId}");
        }

        return sb.ToString();
    }

    private static string BuildRecruitSummary(GameSessionState session)
    {
        return $"Recruit 후보 3개\n현재 후보 수: {session.RecruitOffers.Count}\n카드 버튼으로 즉시 영입";
    }

    private static string BuildSquadText(GameSessionState session)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"원정 스쿼드 ({session.ExpeditionSquadHeroIds.Count}/8)");
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

    private static string BuildDeployPreviewText(GameSessionState session)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Deploy Preview ({session.BattleDeployHeroIds.Count}/4)");
        sb.AppendLine($"Team Posture: {session.SelectedTeamPosture}");
        foreach (var (anchor, heroId) in session.EnumerateDeploymentAssignments())
        {
            var hero = session.Profile.Heroes.FirstOrDefault(x => x.HeroId == heroId);
            var heroName = hero?.Name ?? "Empty";
            sb.AppendLine($"- {anchor.ToDisplayName()}: {heroName}");
        }

        sb.AppendLine();
        if (session.IsQuickBattleSmokeActive)
        {
            sb.AppendLine("Quick Battle smoke 준비 완료");
        }
        else if (session.CanResumeExpedition)
        {
            var nextNode = session.GetSelectedExpeditionNode();
            var routeLabel = nextNode == null ? "경로 선택 필요" : $"{nextNode.Label} -> {nextNode.PlannedReward}";
            sb.AppendLine($"Resume Expedition: {routeLabel}");
        }
        else
        {
            sb.AppendLine("Quick Battle은 Expedition 진행도를 건드리지 않음");
        }

        if (!string.IsNullOrWhiteSpace(session.LastRewardApplicationSummary))
        {
            sb.AppendLine($"Last Reward: {session.LastRewardApplicationSummary}");
        }

        if (!string.IsNullOrWhiteSpace(session.LastExpeditionEffectMessage))
        {
            sb.AppendLine($"Last Node Effect: {session.LastExpeditionEffectMessage}");
        }

        return sb.ToString();
    }

    private void CycleAnchor(DeploymentAnchorId anchor)
    {
        if (!EnsureReady()) return;
        _root.SessionState.CycleDeploymentAssignment(anchor);
        Refresh($"{anchor.ToDisplayName()} 배치를 갱신했습니다.");
    }
}
