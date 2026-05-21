using System.IO;
using NUnit.Framework;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class TownServiceHubFastTests
{
    [Test]
    public void TownHub_Declares_Service_Decision_Readout()
    {
        var state = File.ReadAllText("Assets/_Game/Scripts/Runtime/Unity/UI/Town/TownScreenViewState.cs");
        Assert.That(state, Does.Contain("TownServiceDecisionViewState"));
        Assert.That(state, Does.Contain("SelectedHeroLabel"));
        Assert.That(state, Does.Contain("WalletLabel"));
        Assert.That(state, Does.Contain("RosterPressureLabel"));
        Assert.That(state, Does.Contain("ModalAvailabilityLabel"));

        var presenter = File.ReadAllText("Assets/_Game/Scripts/Runtime/Unity/UI/Town/TownScreenPresenter.cs");
        Assert.That(presenter, Does.Contain("BuildServiceDecision"));
        Assert.That(presenter, Does.Contain("Profile.Currencies.Gold"));
        Assert.That(presenter, Does.Contain("Profile.Inventory.Count"));
        Assert.That(presenter, Does.Contain("_selectedHeroId = heroId"));
    }

    [Test]
    public void TownHub_Uxml_And_View_Render_Service_Decision_Panel()
    {
        var uxml = File.ReadAllText("Assets/_Game/UI/Screens/Town/TownScreen.uxml");
        Assert.That(uxml, Does.Contain("ServiceDecisionPanel"));
        Assert.That(uxml, Does.Contain("ServiceSelectedHeroLabel"));
        Assert.That(uxml, Does.Contain("ServiceWalletLabel"));
        Assert.That(uxml, Does.Contain("ServiceInventoryLabel"));
        Assert.That(uxml, Does.Contain("ServiceRosterPressureLabel"));
        Assert.That(uxml, Does.Contain("ServiceAvailabilityLabel"));

        var view = File.ReadAllText("Assets/_Game/Scripts/Runtime/Unity/UI/Town/TownScreenView.cs");
        Assert.That(view, Does.Contain("ServiceDecision.SelectedHeroLabel"));
        Assert.That(view, Does.Contain("ServiceDecision.WalletLabel"));
        Assert.That(view, Does.Contain("ServiceDecision.ModalAvailabilityLabel"));
    }
}
