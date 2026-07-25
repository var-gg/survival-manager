using System.Collections.Generic;
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
        Assert.That(state.RefitStatusMessage, Does.Contain("보장 바닥"));
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
        Assert.That(state.RefitStatusMessage, Does.Contain("보장 바닥"));
        Assert.That(state.Pool.Single(row => row.ItemInstanceId == "inv-rare").IsSelected, Is.True);
    }

    [Test]
    public void AffixRows_AreInformationalForItemLevelRefit()
    {
        var lookup = RefitTestFixture.CreateLookup();
        var session = CreateSession(lookup);
        var presenter = CreatePresenter(session, lookup, out _);

        var state = presenter.BuildState();

        Assert.That(state.Affixes.Select(row => row.AffixId), Is.EqualTo(
            session.Profile.Inventory.Single(item => item.ItemInstanceId == "inv-epic").AffixIds));
        Assert.That(typeof(IEquipmentRefitActions).GetMethod("OnAffixSelected"), Is.Null);
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

    private static GameSessionState CreateSession(FakeCombatContentLookup lookup)
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
        var session = GameSessionTestFactory.Create(lookup);
        session.BindProfile(new SaveProfile
        {
            ProfileId = "equipment-refit-presenter-test",
            Currencies = new CurrencyRecord { Echo = 10_000 },
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
                },
                new()
                {
                    ItemInstanceId = "inv-rare",
                    ItemBaseId = RefitTestFixture.ArmorItemId,
                    RolledRarityTier = (int)ItemRarityTierValue.Rare,
                    AffixIds = rareAffixes.ToList(),
                },
            },
        });
        session.SetCurrentScene(SceneNames.Town);
        return session;
    }
}
