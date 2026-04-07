using System;
using System.Linq;
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

    public void DismissFirstHero()
    {
        if (!EnsureSessionReady())
        {
            return;
        }

        var heroId = _root.SessionState.Profile.Heroes.Count > 1
            ? _root.SessionState.Profile.Heroes.Last().HeroId
            : null;
        if (heroId == null)
        {
            _presenter?.Refresh("Dismiss할 유닛이 없습니다.");
            return;
        }

        var result = _root.SessionState.DismissHero(heroId);
        _presenter?.Refresh(result.IsSuccess
            ? Localize(GameLocalizationTables.UITown, "ui.town.status.dismiss_success", "Hero dismissed.")
            : result.Error ?? "Dismiss failed.");
    }

    public void RetrainFirstHero()
    {
        if (!EnsureSessionReady())
        {
            return;
        }

        var heroId = _root.SessionState.Profile.Heroes.FirstOrDefault()?.HeroId;
        if (heroId == null)
        {
            _presenter?.Refresh("Retrain할 유닛이 없습니다.");
            return;
        }

        var result = _root.SessionState.RetrainHero(heroId, SM.Core.Contracts.RetrainOperationKind.RerollFlexActive);
        _presenter?.Refresh(result.IsSuccess
            ? Localize(GameLocalizationTables.UITown, "ui.town.status.retrain_success", "Hero retrained.")
            : result.Error ?? "Retrain failed.");
    }

    public void RefitFirstItem()
    {
        if (!EnsureSessionReady())
        {
            return;
        }

        var item = _root.SessionState.Profile.Inventory.FirstOrDefault(i => i.AffixIds != null && i.AffixIds.Count > 0);
        if (item == null)
        {
            _presenter?.Refresh("Refit할 아이템이 없습니다.");
            return;
        }

        var result = _root.SessionState.RefitItem(item.ItemInstanceId, 0);
        _presenter?.Refresh(result.IsSuccess
            ? Localize(GameLocalizationTables.UITown, "ui.town.status.refit_success", "Item refit complete.")
            : result.Error ?? "Refit failed.");
    }

    public void EquipPermanentAugmentDebug()
    {
        if (!EnsureSessionReady())
        {
            return;
        }

        var slice = _root.CombatContentLookup.GetFirstPlayableSlice();
        var augmentId = slice?.PermanentAugmentIds.FirstOrDefault(
            id => !(_root.SessionState.Profile.PermanentAugmentLoadouts
                .SelectMany(r => r.EquippedAugmentIds)
                .Contains(id, StringComparer.Ordinal)));
        if (string.IsNullOrWhiteSpace(augmentId))
        {
            _presenter?.Refresh("장착할 영구 증강이 없습니다.");
            return;
        }

        var result = _root.SessionState.EquipPermanentAugment(augmentId);
        _presenter?.Refresh(result.IsSuccess
            ? Localize(GameLocalizationTables.UITown, "ui.town.status.perm_augment_equipped", "Permanent augment equipped: {0}", augmentId)
            : result.Error ?? "Equip failed.");
    }

    public void SelectBoardDebug()
    {
        if (!EnsureSessionReady())
        {
            return;
        }

        var heroId = _root.SessionState.Profile.Heroes.FirstOrDefault()?.HeroId;
        if (heroId == null)
        {
            _presenter?.Refresh("보드 선택할 유닛이 없습니다.");
            return;
        }

        var slice = _root.CombatContentLookup.GetFirstPlayableSlice();
        var boardId = slice?.PassiveBoardIds.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(boardId))
        {
            _presenter?.Refresh("선택할 패시브 보드가 없습니다.");
            return;
        }

        var result = _root.SessionState.SelectPassiveBoard(heroId, boardId);
        _presenter?.Refresh(result.IsSuccess
            ? Localize(GameLocalizationTables.UITown, "ui.town.status.board_selected", "Passive board selected: {0}", boardId)
            : result.Error ?? "Board selection failed.");
    }

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
