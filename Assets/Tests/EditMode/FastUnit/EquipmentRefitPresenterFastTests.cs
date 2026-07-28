using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SM.Core.Content;
using SM.Persistence.Abstractions.Models;
using SM.Tests.EditMode.Fakes;
using SM.Unity;
using SM.Unity.UI.Town.Preview;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class EquipmentRefitPresenterFastTests
{
    [Test]
    public void LocalizationKeys_AreSeededAndProductionUxmlContainsNoRawKeys()
    {
        var seed = File.ReadAllText(
            "Assets/_Game/Scripts/Editor/Bootstrap/LocalizationFoundationBootstrap.cs");
        var uxml = File.ReadAllText(
            "Assets/_Game/UI/Panels/EquipmentRefit/EquipmentRefit.uxml");
        var requiredKeys = new[]
        {
            "ui.town.refit.operation.reforge",
            "ui.town.refit.operation.seal",
            "ui.town.refit.lock.open",
            "ui.town.refit.quote.cost",
            "ui.town.refit.confirmation.body",
            "ui.town.refit.reason.seal_not_allowed",
            "ui.town.refit.reason.all_affixes_locked",
            "ui.town.refit.reason.seal_unaffordable",
        };

        foreach (var key in requiredKeys)
        {
            Assert.That(seed, Does.Contain($"[\"{key}\"]"));
        }

        Assert.That(uxml, Does.Not.Contain("ui.town.refit."));
        Assert.That(uxml, Does.Not.Contain("품질 계산 중"));
    }

    [Test]
    public void BuildState_ShowsItemQualityNextFloorAndDynamicCost()
    {
        var lookup = RefitTestFixture.CreateLookup();
        var session = CreateSession(lookup);
        var presenter = CreatePresenter(session, lookup, out _);

        var state = presenter.BuildState();

        Assert.That(state.SelectedItemName, Is.EqualTo(RefitTestFixture.WeaponItemId));
        Assert.That(state.Affixes, Is.Not.Empty);
        Assert.That(state.CurrentQualityPercent, Is.GreaterThanOrEqualTo(0d));
        Assert.That(state.NextFloorPercent, Is.GreaterThan(state.CurrentQualityPercent));
        Assert.That(state.RefitCost, Is.GreaterThan(0));
        Assert.That(state.RefitCost, Is.Not.EqualTo(15));
        Assert.That(state.RefitStatusMessage, Does.Contain("guaranteed floor"));
        Assert.That(state.SelectedOperation, Is.EqualTo(CraftOperationKindValue.Reforge));
        Assert.That(state.SelectedOperationCost, Is.EqualTo(state.RefitCost));
        Assert.That(state.SelectedOperationCanPurchase, Is.True);
    }

    [Test]
    public void OnPoolItemSelected_RareItemAlsoOffersRollQualityRefit()
    {
        var lookup = RefitTestFixture.CreateLookup();
        var session = CreateSession(lookup);
        var presenter = CreatePresenter(session, lookup, out var view);

        ((IEquipmentRefitActions)presenter).OnPoolItemSelected("inv-rare");

        Assert.That(view.RenderCount, Is.GreaterThan(0));
        var state = presenter.BuildState();
        Assert.That(state.SelectedItemName, Is.EqualTo(RefitTestFixture.ArmorItemId));
        Assert.That(state.RefitMaxed, Is.False);
        Assert.That(state.RefitCost, Is.GreaterThan(0));
        Assert.That(state.SelectedItemCanRefit, Is.True);
        Assert.That(state.RefitStatusMessage, Does.Contain("guaranteed floor"));
        Assert.That(state.Pool.Single(row => row.ItemInstanceId == "inv-rare").IsSelected, Is.True);
    }

    [Test]
    public void OperationSelector_SealUsesServiceQuoteAndEnablesPerAffixLocks()
    {
        var lookup = RefitTestFixture.CreateLookup();
        var session = CreateSession(lookup);
        var presenter = CreatePresenter(session, lookup, out _);

        var initial = presenter.BuildState();
        var selectedItem = session.Profile.Inventory.Single(item =>
            item.ItemInstanceId == "inv-epic");

        Assert.That(initial.SealOperationSelectable, Is.True);
        ((IEquipmentRefitActions)presenter).OnOperationSelected(
            CraftOperationKindValue.Seal);
        var state = presenter.BuildState();
        var serviceQuote = session.GetSealQuote(
            selectedItem.ItemInstanceId,
            Array.Empty<string>());

        Assert.That(state.SelectedOperation, Is.EqualTo(CraftOperationKindValue.Seal));
        Assert.That(state.SelectedOperationCost, Is.EqualTo(serviceQuote.EchoCost));
        Assert.That(state.SelectedOperationCanPurchase, Is.EqualTo(serviceQuote.CanPurchase));
        Assert.That(state.Affixes.All(row => row.LockToggleEnabled), Is.True);
        Assert.That(state.Affixes.All(row => !row.IsLocked), Is.True);
    }

    [Test]
    public void Seal_RequiresConfirmationThenPreservesLockedMagnitudeAndMovesUnlockedRoll()
    {
        var lookup = RefitTestFixture.CreateLookup();
        var session = CreateSession(lookup);
        var presenter = CreatePresenter(session, lookup, out _);
        var actions = (IEquipmentRefitActions)presenter;
        var initial = presenter.BuildState();
        var item = session.Profile.Inventory.Single(candidate =>
            candidate.ItemInstanceId == "inv-epic");
        var lockedAffixId = initial.Affixes[0].AffixId;
        var beforeBits = item.AffixMagnitudeRolls.ToDictionary(
            roll => roll.AffixId,
            roll => BitConverter.SingleToInt32Bits(roll.Magnitude),
            StringComparer.Ordinal);

        actions.OnOperationSelected(CraftOperationKindValue.Seal);
        actions.OnAffixLockToggled(lockedAffixId);
        var quote = session.GetSealQuote(
            item.ItemInstanceId,
            new[] { lockedAffixId });
        var echoBefore = session.Profile.Currencies.Echo;

        actions.OnCraftRequested();

        Assert.That(presenter.BuildState().ConfirmationVisible, Is.True);
        Assert.That(session.Profile.Currencies.Echo, Is.EqualTo(echoBefore));
        Assert.That(session.Profile.ItemCraftOperations, Is.Empty);

        actions.OnCraftConfirmed();

        var afterBits = item.AffixMagnitudeRolls.ToDictionary(
            roll => roll.AffixId,
            roll => BitConverter.SingleToInt32Bits(roll.Magnitude),
            StringComparer.Ordinal);
        Assert.That(afterBits[lockedAffixId], Is.EqualTo(beforeBits[lockedAffixId]));
        Assert.That(
            item.AffixIds
                .Where(id => !string.Equals(id, lockedAffixId, StringComparison.Ordinal))
                .Any(id => afterBits[id] != beforeBits[id]),
            Is.True,
            "At least one unlocked affix magnitude must move.");
        Assert.That(
            session.Profile.Currencies.Echo,
            Is.EqualTo(echoBefore - quote.EchoCost));
        Assert.That(session.Profile.ItemCraftOperations, Has.Count.EqualTo(1));
        Assert.That(
            session.Profile.ItemCraftOperations[0].SealedAffixIds,
            Is.EqualTo(new[] { lockedAffixId }));
    }

    [Test]
    public void Seal_AllAffixesLockedDisablesCraftWithServiceReason()
    {
        var lookup = RefitTestFixture.CreateLookup();
        var session = CreateSession(lookup);
        var presenter = CreatePresenter(session, lookup, out _);
        var actions = (IEquipmentRefitActions)presenter;
        var state = presenter.BuildState();

        actions.OnOperationSelected(CraftOperationKindValue.Seal);
        foreach (var affix in state.Affixes)
        {
            actions.OnAffixLockToggled(affix.AffixId);
        }

        state = presenter.BuildState();
        actions.OnCraftRequested();

        Assert.That(state.SelectedOperationCanPurchase, Is.False);
        Assert.That(
            state.SelectedOperationStatusMessage,
            Does.Contain("leave at least one affix unlocked"));
        Assert.That(presenter.BuildState().ConfirmationVisible, Is.False);
        Assert.That(session.Profile.ItemCraftOperations, Is.Empty);
    }

    [Test]
    public void Seal_UnaffordableQuoteKeepsServiceCostAndShowsSharedBlockReason()
    {
        var lookup = RefitTestFixture.CreateLookup();
        var session = CreateSession(lookup, echo: 0);
        var presenter = CreatePresenter(session, lookup, out _);
        var actions = (IEquipmentRefitActions)presenter;
        var initial = presenter.BuildState();
        var item = session.Profile.Inventory.Single(candidate =>
            candidate.ItemInstanceId == "inv-epic");
        var lockedAffixId = initial.Affixes[0].AffixId;

        actions.OnOperationSelected(CraftOperationKindValue.Seal);
        actions.OnAffixLockToggled(lockedAffixId);
        var quote = session.GetSealQuote(
            item.ItemInstanceId,
            new[] { lockedAffixId });
        var state = presenter.BuildState();

        Assert.That(quote.CanPurchase, Is.True);
        Assert.That(state.SelectedOperationCost, Is.EqualTo(quote.EchoCost));
        Assert.That(state.SelectedOperationCanPurchase, Is.False);
        Assert.That(state.SelectedOperationStatusMessage, Does.Contain("Not enough Echo"));
        Assert.That(
            session.GetSealPurchaseBlockFailure(
                item.ItemInstanceId,
                new[] { lockedAffixId })?.Code,
            Is.EqualTo(SessionOperationFailureCodes.RefitUnaffordable));
    }

    [Test]
    public void Seal_DisallowedItemKeepsVisibleOptionDisabledWithServiceReason()
    {
        var source = RefitTestFixture.CreateLookup();
        var itemCatalog = source.Snapshot.ItemCatalog!.ToDictionary(
            pair => pair.Key,
            pair => pair.Value with
            {
                AllowedCraftOperations = new[] { CraftOperationKindValue.Reforge },
            },
            StringComparer.Ordinal);
        var lookup = new FakeCombatContentLookup(
            snapshot: source.Snapshot with { ItemCatalog = itemCatalog },
            firstPlayableSlice: source.GetFirstPlayableSlice());
        var session = CreateSession(lookup);
        var presenter = CreatePresenter(session, lookup, out _);

        var state = presenter.BuildState();

        Assert.That(state.ReforgeOperationSelectable, Is.True);
        Assert.That(state.SealOperationSelectable, Is.False);
        Assert.That(state.SealOperationReason, Does.Contain("does not allow Seal"));
    }

    [Test]
    public void Reforge_ThroughUiUsesLegacyCommandAndProducesIdenticalResult()
    {
        var lookup = RefitTestFixture.CreateLookup();
        var uiSession = CreateSession(lookup);
        var baselineSession = CreateSession(lookup);
        var presenter = CreatePresenter(uiSession, lookup, out _);
        var actions = (IEquipmentRefitActions)presenter;
        var uiItem = uiSession.Profile.Inventory.Single(item =>
            item.ItemInstanceId == "inv-epic");
        var baselineItem = baselineSession.Profile.Inventory.Single(item =>
            item.ItemInstanceId == "inv-epic");
        var echoBefore = uiSession.Profile.Currencies.Echo;

        presenter.BuildState();
        actions.OnCraftRequested();

        Assert.That(presenter.BuildState().ConfirmationVisible, Is.True);
        Assert.That(uiSession.Profile.Currencies.Echo, Is.EqualTo(echoBefore));

        actions.OnCraftConfirmed();
        var baselineResult = baselineSession.RefitItem(baselineItem.ItemInstanceId);

        Assert.That(baselineResult.IsSuccess, Is.True, baselineResult.Error);
        Assert.That(uiItem.RefitLevel, Is.EqualTo(baselineItem.RefitLevel));
        Assert.That(uiItem.AffixIds, Is.EqualTo(baselineItem.AffixIds));
        Assert.That(
            uiItem.AffixMagnitudeRolls.Select(roll =>
                (roll.AffixId, BitConverter.SingleToInt32Bits(roll.Magnitude))),
            Is.EqualTo(baselineItem.AffixMagnitudeRolls.Select(roll =>
                (roll.AffixId, BitConverter.SingleToInt32Bits(roll.Magnitude)))));
        Assert.That(
            uiSession.Profile.Currencies.Echo,
            Is.EqualTo(baselineSession.Profile.Currencies.Echo));
        Assert.That(uiSession.Profile.ItemCraftOperations, Is.Empty);
    }

    private static EquipmentRefitPresenter CreatePresenter(
        GameSessionState session,
        FakeCombatContentLookup lookup,
        out RecordingEquipmentRefitView view)
    {
        view = new RecordingEquipmentRefitView();
        return new EquipmentRefitPresenter(
            session,
            lookup,
            view,
            itemId => itemId,
            affixId => affixId,
            (characterId, archetypeId) => string.IsNullOrEmpty(characterId) ? archetypeId : characterId);
    }

    private static GameSessionState CreateSession(
        FakeCombatContentLookup lookup,
        int echo = 10_000)
    {
        var epicAffixes = RefitTestFixture.SelectAtSupportIndex(
            lookup,
            RefitTestFixture.WeaponItemId,
            ItemRarityTierValue.Epic,
            0);
        var rareAffixes = RefitTestFixture.SelectAtSupportIndex(
            lookup,
            RefitTestFixture.ArmorItemId,
            ItemRarityTierValue.Rare,
            0);
        var epicMagnitudes = RefitTestFixture.CreateMagnitudes(
            lookup,
            epicAffixes,
            0.05d);
        var rareMagnitudes = RefitTestFixture.CreateMagnitudes(
            lookup,
            rareAffixes,
            0.05d);
        var session = GameSessionTestFactory.Create(lookup);
        session.BindProfile(new SaveProfile
        {
            ProfileId = "equipment-refit-presenter-test",
            Currencies = new CurrencyRecord { Echo = echo },
            ItemCraftOperations = new List<ItemCraftOperationRecord>(),
            Heroes = new List<HeroInstanceRecord>
            {
                new()
                {
                    HeroId = "refit-presenter-hero",
                    Name = "Refit Presenter Hero",
                    ArchetypeId = "refit_archetype",
                    RaceId = "human",
                    ClassId = "vanguard",
                    EquippedItemIds = new List<string>(),
                },
            },
            CampaignProgress = new CampaignProgressRecord
            {
                SelectedChapterId = RefitTestFixture.ChapterId,
                SelectedSiteId = "site_alpha_gate",
            },
            Inventory = new List<InventoryItemRecord>
            {
                new()
                {
                    ItemInstanceId = "inv-epic",
                    ItemBaseId = RefitTestFixture.WeaponItemId,
                    RolledRarityTier = (int)ItemRarityTierValue.Epic,
                    AffixIds = epicAffixes.ToList(),
                    AffixMagnitudeRolls = epicAffixes.Select(affixId =>
                        new InventoryAffixMagnitudeRecord
                        {
                            AffixId = affixId,
                            Magnitude = epicMagnitudes[affixId],
                        }).ToList(),
                },
                new()
                {
                    ItemInstanceId = "inv-rare",
                    ItemBaseId = RefitTestFixture.ArmorItemId,
                    RolledRarityTier = (int)ItemRarityTierValue.Rare,
                    AffixIds = rareAffixes.ToList(),
                    AffixMagnitudeRolls = rareAffixes.Select(affixId =>
                        new InventoryAffixMagnitudeRecord
                        {
                            AffixId = affixId,
                            Magnitude = rareMagnitudes[affixId],
                        }).ToList(),
                },
            },
        });
        session.SetCurrentScene(SceneNames.Town);
        return session;
    }
}
