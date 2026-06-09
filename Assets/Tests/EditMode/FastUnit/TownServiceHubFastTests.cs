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

    [Test]
    public void TownHub_Separates_Production_Core_Panels_From_Deferred_Settings_And_Theater()
    {
        var state = File.ReadAllText("Assets/_Game/Scripts/Runtime/Unity/UI/Town/TownScreenViewState.cs");
        Assert.That(state, Does.Contain("bool ShowSettings"));
        Assert.That(state, Does.Contain("bool ShowTheater"));

        var view = File.ReadAllText("Assets/_Game/Scripts/Runtime/Unity/UI/Town/TownScreenView.cs");
        Assert.That(view, Does.Contain("_settingsButton.style.display = state.ShowSettings"));
        Assert.That(view, Does.Contain("_theaterButton.style.display = state.ShowTheater"));

        var presenter = File.ReadAllText("Assets/_Game/Scripts/Runtime/Unity/UI/Town/TownScreenPresenter.cs");
        Assert.That(presenter, Does.Contain("SetCorePanelReadiness"));
        Assert.That(presenter, Does.Contain("ShowSettings: _settingsOpener != null"));
        Assert.That(presenter, Does.Contain("ShowTheater: _theaterOpener != null"));
        // dev 텔레메트리 "핵심 패널 N/M 준비" → 플레이어 의미 "시설 … 이용 가능"으로 교정됨.
        Assert.That(presenter, Does.Contain("이용 가능"));
        Assert.That(presenter, Does.Not.Contain("Settings/Theater 후속"));

        var controller = File.ReadAllText("Assets/_Game/Scripts/Runtime/Unity/TownScreenController.cs");
        Assert.That(controller, Does.Contain("SetCorePanelReadiness(corePanelReadyCount, 9)"));
        Assert.That(controller, Does.Contain("TryWireTacticalSetup"));
        Assert.That(controller, Does.Not.Contain("TryWireTacticalWorkshop"));
        Assert.That(controller, Does.Not.Contain("BindTheaterOpen(() => _presenter?.Refresh"));

        var registry = File.ReadAllText("Assets/_Game/UI/Foundation/ArtBible/artbible-role-registry.json");
        Assert.That(registry, Does.Not.Contain("Assets/_Game/UI/Panels/SettingsGlobal/SettingsGlobal.uss"));
        var panelSot = File.ReadAllText("Assets/_Game/UI/Foundation/ArtBible/game-panel-sot-registry.json");
        Assert.That(panelSot, Does.Contain("\"id\": \"town.tactical_setup\""));
        Assert.That(panelSot, Does.Contain("\"state\": \"legacy-preview\""));
    }

    [Test]
    public void HelpStrip_Defaults_To_Hidden_For_User_Qa_Surface()
    {
        var helpState = File.ReadAllText("Assets/_Game/Scripts/Runtime/Unity/UI/ScreenHelpState.cs");
        Assert.That(helpState, Does.Contain("PlayerPrefs.HasKey(_prefsKey)"));
        Assert.That(helpState, Does.Contain("PlayerPrefs.GetInt(_prefsKey, 1) == 0"));
    }

    [Test]
    public void TownCompendium_Readout_Localization_Keys_Are_Authored()
    {
        var shared = File.ReadAllText("Assets/Localization/StringTables/UI_Town Shared Data.asset");
        var ko = File.ReadAllText("Assets/Localization/StringTables/UI_Town_ko.asset");
        var en = File.ReadAllText("Assets/Localization/StringTables/UI_Town_en.asset");
        var formatter = File.ReadAllText("Assets/_Game/Scripts/Runtime/Unity/UI/Town/Preview/CompendiumSkillReadoutFormatter.cs");

        foreach (var key in new[]
                 {
                     "ui.town.compendium.card.quick_stats",
                     "ui.town.compendium.damage.physical",
                     "ui.town.compendium.damage.magical",
                     "ui.town.compendium.damage.healing",
                     "ui.town.compendium.delivery.melee",
                     "ui.town.compendium.delivery.ranged",
                     "ui.town.compendium.delivery.projectile",
                     "ui.town.compendium.delivery.aura",
                     "ui.town.compendium.target.self",
                     "ui.town.compendium.target.marked",
                     "ui.town.compendium.intent.recovery",
                     "ui.town.compendium.intent.protection",
                     "ui.town.compendium.intent.mobility",
                     "ui.town.compendium.intent.control",
                     "ui.town.compendium.intent.damage",
                 })
        {
            Assert.That(formatter, Does.Contain(key), key);
            Assert.That(shared, Does.Contain("m_Key: " + key), key);
        }

        Assert.That(ko, Does.Contain("m_Id: 17841341873852486"));
        Assert.That(en, Does.Contain("m_Id: 17841341873852486"));
    }
}
