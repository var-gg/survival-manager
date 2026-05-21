using System;
using System.Linq;
using SM.Core.Contracts;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Persistence.Abstractions.Models;

namespace SM.Unity.UI.Town;

public sealed class TownCharacterSheetPresenter
{
    private readonly GameSessionRoot _root;
    private readonly TownCharacterSheetView _view;
    private readonly TownCharacterSheetFormatter _formatter;
    private string _selectedHeroId = string.Empty;

    public TownCharacterSheetPresenter(
        GameSessionRoot root,
        GameLocalizationController localization,
        ContentTextResolver contentText,
        TownCharacterSheetView view)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _formatter = new TownCharacterSheetFormatter(localization, contentText, root.CombatContentLookup);
    }

    public void Initialize()
    {
        _view.Bind(this);
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

        return _formatter.Build(
            session,
            hero,
            selectedItem,
            selectedNode: null,
            retrainActiveCost: costs.GetTotalCost(RetrainOperationKind.RerollFlexActive, retrainState),
            retrainPassiveCost: costs.GetTotalCost(RetrainOperationKind.RerollFlexPassive, retrainState),
            fullRetrainCost: costs.GetTotalCost(RetrainOperationKind.FullRetrain, retrainState),
            dismissRefund);
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
}
