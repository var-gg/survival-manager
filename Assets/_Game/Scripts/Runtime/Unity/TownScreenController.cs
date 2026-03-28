using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace SM.Unity;

public sealed class TownScreenController : MonoBehaviour
{
    [SerializeField] private Text titleText = null!;
    [SerializeField] private Text rosterText = null!;
    [SerializeField] private Text recruitText = null!;
    [SerializeField] private Text squadText = null!;
    [SerializeField] private Text deployPreviewText = null!;
    [SerializeField] private Text currencyText = null!;
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

        _root.SessionState.SetCurrentScene(SceneNames.Town);
        Refresh();
    }

    public void RecruitOffer0() => Recruit(0);
    public void RecruitOffer1() => Recruit(1);
    public void RecruitOffer2() => Recruit(2);

    public void RerollOffers()
    {
        _root.SessionState.RerollRecruitOffers();
        Refresh("Recruit 후보를 리롤했습니다.");
    }

    public void SaveProfile()
    {
        _root.SaveProfile();
        Refresh("프로필을 저장했습니다.");
    }

    public void LoadProfile()
    {
        _root.BindProfile();
        Refresh("프로필을 다시 불러왔습니다.");
    }

    public void DebugStartExpedition()
    {
        _root.SessionState.BeginNewExpedition();
        _root.SceneFlow.GoToExpedition();
    }

    private void Recruit(int index)
    {
        if (_root.SessionState.Recruit(index))
        {
            Refresh($"후보 {index + 1}을 영입했습니다.");
            return;
        }

        Refresh("영입에 실패했습니다.");
    }

    private void Refresh(string message = "")
    {
        var session = _root.SessionState;
        titleText.text = "Town Debug UI";
        currencyText.text = $"Gold: {session.Profile.Currencies.Gold} | Perm Slots: {session.PermanentAugmentSlotCount} | Trait Reroll: {session.Profile.Currencies.TraitRerollCurrency}";
        rosterText.text = BuildRosterText(session);
        recruitText.text = BuildRecruitText(session);
        squadText.text = BuildSquadText(session);
        deployPreviewText.text = BuildDeployPreviewText(session);
        statusText.text = string.IsNullOrWhiteSpace(message) ? "원정 준비 상태" : message;
    }

    private static string BuildRosterText(GameSessionState session)
    {
        var sb = new StringBuilder();
        sb.AppendLine("보유 로스터");
        foreach (var hero in session.Profile.Heroes)
        {
            var inSquad = session.ExpeditionSquadHeroIds.Contains(hero.HeroId) ? "[원정]" : "[대기]";
            sb.AppendLine($"- {inSquad} {hero.Name} / {hero.RaceId} / {hero.ClassId} / +{hero.PositiveTraitId} / -{hero.NegativeTraitId}");
        }
        return sb.ToString();
    }

    private static string BuildRecruitText(GameSessionState session)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Recruit 후보 3개");
        for (var i = 0; i < session.RecruitOffers.Count; i++)
        {
            var offer = session.RecruitOffers[i];
            sb.AppendLine($"{i + 1}. {offer.Name} / {offer.RaceId} / {offer.ClassId} / +{offer.PositiveTraitId} / -{offer.NegativeTraitId}");
        }
        return sb.ToString();
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
        sb.AppendLine($"Battle Deploy Preview ({session.BattleDeployHeroIds.Count}/4)");
        foreach (var heroId in session.BattleDeployHeroIds)
        {
            var hero = session.Profile.Heroes.FirstOrDefault(x => x.HeroId == heroId);
            if (hero != null)
            {
                sb.AppendLine($"- {hero.Name}");
            }
        }
        return sb.ToString();
    }
}
