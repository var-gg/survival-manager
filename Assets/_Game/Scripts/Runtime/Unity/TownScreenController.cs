using SM.Unity.UI;
using SM.Unity.UI.Town;
using UnityEngine;

namespace SM.Unity;

public sealed class TownScreenController : MonoBehaviour
{
    [SerializeField] private RuntimePanelHost panelHost = null!;

    private GameSessionRoot _root = null!;
    private GameLocalizationController _localization = null!;
    private ContentTextResolver _contentText = null!;
    private TownScreenPresenter? _presenter;

    private void Start()
    {
        if (!EnsureViewReady())
        {
            return;
        }

        _localization.LocaleChanged += HandleLocaleChanged;
        _root.SessionState.SetCurrentScene(SceneNames.Town);
        _presenter!.Initialize();
    }

    private void OnDestroy()
    {
        if (_localization != null)
        {
            _localization.LocaleChanged -= HandleLocaleChanged;
        }
    }

    public void RecruitOffer0() => _presenter?.RecruitOffer0();
    public void RecruitOffer1() => _presenter?.RecruitOffer1();
    public void RecruitOffer2() => _presenter?.RecruitOffer2();
    public void RecruitOffer3() => _presenter?.RecruitOffer3();
    public void RerollOffers() => _presenter?.RerollOffers();
    public void SaveProfile() => _presenter?.SaveProfile();
    public void LoadProfile() => _presenter?.LoadProfile();
    public void ReturnToStart() => _presenter?.ReturnToStart();
    public void OpenExpedition() => _presenter?.OpenExpedition();
    public void QuickBattle() => _presenter?.QuickBattle();
    public void PreviousChapter() => _presenter?.PreviousChapter();
    public void NextChapter() => _presenter?.NextChapter();
    public void PreviousSite() => _presenter?.PreviousSite();
    public void NextSite() => _presenter?.NextSite();
    public void CycleHero() => _presenter?.CycleHero();
    public void CycleItem() => _presenter?.CycleItem();
    public void UseScout() => _presenter?.UseScout();
    public void RetrainFlexActive() => _presenter?.RetrainFlexActive();
    public void RetrainFlexPassive() => _presenter?.RetrainFlexPassive();
    public void FullRetrain() => _presenter?.FullRetrain();
    public void DismissSelectedHero() => _presenter?.DismissSelectedHero();
    public void RefitSelectedItem() => _presenter?.RefitSelectedItem();
    public void CycleBoard() => _presenter?.CycleBoard();
    public void CyclePassiveNode() => _presenter?.CyclePassiveNode();
    public void TogglePassiveNode() => _presenter?.TogglePassiveNode();
    public void CyclePermanentCandidate() => _presenter?.CyclePermanentCandidate();
    public void EquipSelectedPermanentAugment() => _presenter?.EquipSelectedPermanentAugment();

    public void EnsureRuntimeControls()
    {
        if (panelHost != null)
        {
            panelHost.EnsureReady();
        }

        if (!Application.isPlaying)
        {
            return;
        }

        EnsureViewReady();
    }

    public void CycleFrontTop() => _presenter?.CycleFrontTop();
    public void CycleFrontCenter() => _presenter?.CycleFrontCenter();
    public void CycleFrontBottom() => _presenter?.CycleFrontBottom();
    public void CycleBackTop() => _presenter?.CycleBackTop();
    public void CycleBackCenter() => _presenter?.CycleBackCenter();
    public void CycleBackBottom() => _presenter?.CycleBackBottom();
    public void CycleTeamPosture() => _presenter?.CycleTeamPosture();

    private bool EnsureViewReady()
    {
        if (!EnsureSessionReady())
        {
            return false;
        }

        if (panelHost == null)
        {
            Debug.LogError("[TownScreenController] Missing RuntimePanelHost reference.");
            return false;
        }

        if (_presenter != null)
        {
            panelHost.EnsureReady();
            return true;
        }

        panelHost.EnsureReady();
        var view = new TownScreenView(panelHost.Root);
        _presenter = new TownScreenPresenter(_root, _localization, _contentText, view);
        return true;
    }

    private bool EnsureSessionReady()
    {
        if (_root != null)
        {
            return true;
        }

        _root = GameSessionRoot.EnsureInstance();
        if (_root == null)
        {
            Debug.LogError("[TownScreenController] GameSessionRoot가 없습니다.");
            return false;
        }

        _localization = _root.Localization;
        _contentText = new ContentTextResolver(_localization, _root.CombatContentLookup);
        return true;
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
        _presenter?.Refresh();
    }
}
