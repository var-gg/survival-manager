using System;
using System.Linq;
using SM.Core.Contracts;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Persistence.Abstractions.Models;
using UnityEngine;

namespace SM.Unity.UI.Town;

public sealed class TownCharacterSheetPresenter
{
    public delegate Texture2D? SpriteLoader(string spriteKey);

    private readonly GameSessionRoot _root;
    private readonly TownCharacterSheetView _view;
    private readonly TownCharacterSheetFormatter _formatter;
    private readonly SpriteLoader _portraitSprite;
    private readonly SpriteLoader _railPortraitSprite;
    private readonly SpriteLoader _skillSprite;
    private readonly SpriteLoader _itemSprite;
    // wave-50 P2 action bar callback. controller가 cross-presenter open / service wire.
    // null이면 no-op + Debug.Log (사용자에게 안전한 placeholder).
    private Action<string>? _passiveBoardOpener;
    private Action<string>? _refitOpener;
    private Action<string>? _dismissHandler;
    private Action<string>? _retrainHandler;
    private string _selectedHeroId = string.Empty;

    public TownCharacterSheetPresenter(
        GameSessionRoot root,
        GameLocalizationController localization,
        ContentTextResolver contentText,
        TownCharacterSheetView view,
        SpriteLoader? portraitSprite = null,
        SpriteLoader? railPortraitSprite = null,
        SpriteLoader? skillSprite = null,
        SpriteLoader? itemSprite = null)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _formatter = new TownCharacterSheetFormatter(localization, contentText, root.CombatContentLookup);
        _portraitSprite = portraitSprite ?? (_ => null);
        _railPortraitSprite = railPortraitSprite ?? _portraitSprite;
        _skillSprite = skillSprite ?? (_ => null);
        _itemSprite = itemSprite ?? (_ => null);
    }

    public void Initialize()
    {
        _view.Bind(this);
    }

    /// <summary>wave-50 P2 action bar — cross-presenter Open과 service wire를 controller에서 주입.</summary>
    public void BindActionBar(
        Action<string>? passiveBoardOpener = null,
        Action<string>? refitOpener = null,
        Action<string>? dismissHandler = null,
        Action<string>? retrainHandler = null)
    {
        _passiveBoardOpener = passiveBoardOpener;
        _refitOpener = refitOpener;
        _dismissHandler = dismissHandler;
        _retrainHandler = retrainHandler;
    }

    public void OnDismissClicked()
    {
        if (string.IsNullOrEmpty(_selectedHeroId)) return;
        if (_dismissHandler != null)
        {
            _dismissHandler(_selectedHeroId);
        }
        else
        {
            Debug.Log($"[TownCharacterSheet] 해고 wire 미연결 — hero='{_selectedHeroId}' (DismissService.CalculateRefund는 BuildState에서 계산됨, 실제 dismiss commit은 후속 wave).");
        }
    }

    public void OnRetrainClicked()
    {
        if (string.IsNullOrEmpty(_selectedHeroId)) return;
        if (_retrainHandler != null)
        {
            _retrainHandler(_selectedHeroId);
        }
        else
        {
            Debug.Log($"[TownCharacterSheet] 재훈련 wire 미연결 — hero='{_selectedHeroId}' (RetrainOperationKind cost는 BuildState에서 계산됨, 실제 retrain commit은 후속 wave).");
        }
    }

    public void OnPassiveBoardClicked()
    {
        if (string.IsNullOrEmpty(_selectedHeroId)) return;
        _passiveBoardOpener?.Invoke(_selectedHeroId);
    }

    public void OnRefitClicked()
    {
        if (string.IsNullOrEmpty(_selectedHeroId)) return;
        _refitOpener?.Invoke(_selectedHeroId);
    }

    public void Open(string heroId)
    {
        if (!string.IsNullOrWhiteSpace(heroId))
        {
            _selectedHeroId = heroId;
        }

        _view.Open();
        Refresh();
    }

    public void Close()
    {
        _view.Close();
    }

    public void Refresh()
    {
        _view.Render(BuildState());
    }

    private TownCharacterSheetViewState BuildState()
    {
        var session = _root.SessionState;
        var hero = session.Profile.Heroes.FirstOrDefault(candidate =>
            string.Equals(candidate.HeroId, _selectedHeroId, StringComparison.Ordinal));
        var selectedItem = ResolveSelectedEquippedItem(session, hero);
        var retrainState = hero?.RetrainState;
        var costs = RecruitmentBalanceCatalog.DefaultRetrainCosts;
        var dismissRefund = DismissService.CalculateRefund(hero?.EconomyFootprint ?? new UnitEconomyFootprint());

        var state = _formatter.Build(
            session,
            hero,
            selectedItem,
            selectedNode: null,
            retrainActiveCost: costs.GetTotalCost(RetrainOperationKind.RerollFlexActive, retrainState),
            retrainPassiveCost: costs.GetTotalCost(RetrainOperationKind.RerollFlexPassive, retrainState),
            fullRetrainCost: costs.GetTotalCost(RetrainOperationKind.FullRetrain, retrainState),
            dismissRefund);
        return HydrateSprites(state, hero);
    }

    private TownCharacterSheetViewState HydrateSprites(
        TownCharacterSheetViewState state,
        HeroInstanceRecord? selectedHero)
    {
        var portraitKey = ResolvePortraitKey(selectedHero);
        return state with
        {
            PortraitSprite = _portraitSprite(portraitKey),
            HeroRail = state.HeroRail
                .Select(entry => entry with { PortraitSprite = _railPortraitSprite(ResolvePortraitKey(entry.HeroId)) })
                .ToList(),
            Skills = state.Skills
                .Select(skill => skill with { IconSprite = _skillSprite(FirstNonEmpty(skill.IconKey, skill.SkillId)) })
                .ToList(),
            Equipment = state.Equipment
                .Select(slot => slot with { IconSprite = _itemSprite(FirstNonEmpty(slot.IconKey, slot.SlotKey)) })
                .ToList(),
        };
    }

    private string ResolvePortraitKey(HeroInstanceRecord? hero)
    {
        return hero == null
            ? string.Empty
            : FirstNonEmpty(hero.CharacterId, hero.ArchetypeId);
    }

    private string ResolvePortraitKey(string heroId)
    {
        var hero = _root.SessionState.Profile.Heroes.FirstOrDefault(candidate =>
            string.Equals(candidate.HeroId, heroId, StringComparison.Ordinal));
        return ResolvePortraitKey(hero);
    }

    private static InventoryItemRecord? ResolveSelectedEquippedItem(GameSessionState session, HeroInstanceRecord? hero)
    {
        if (hero == null)
        {
            return null;
        }

        return session.Profile.Inventory.FirstOrDefault(item =>
            string.Equals(item.EquippedHeroId, hero.HeroId, StringComparison.Ordinal)
            || hero.EquippedItemIds.Contains(item.ItemInstanceId, StringComparer.Ordinal));
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }
}
