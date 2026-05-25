using System.IO;
using NUnit.Framework;

namespace SM.Tests.EditMode.FastUnit.Battle;

[Category("FastUnit")]
public sealed class BattleHudShellVisualContractFastTests
{
    [Test]
    public void BattleHudShell_UsesProductionCopy_AndHidesPrematureContinue()
    {
        var presenter = File.ReadAllText("Assets/_Game/Scripts/Runtime/Unity/UI/Battle/BattleScreenPresenter.cs");
        var localizationBootstrap = File.ReadAllText("Assets/_Game/Scripts/Editor/Bootstrap/LocalizationFoundationBootstrap.cs");
        var koreanBattleTable = File.ReadAllText("Assets/Localization/StringTables/UI_Battle_ko.asset");
        var englishBattleTable = File.ReadAllText("Assets/Localization/StringTables/UI_Battle_en.asset");

        Assert.That(presenter, Does.Contain("ui.battle.title"));
        Assert.That(presenter, Does.Contain("ui.battle.playback.ingame"));
        Assert.That(presenter, Does.Not.Contain(".production"));
        Assert.That(presenter, Does.Contain("\"원정 전투\""));
        Assert.That(presenter, Does.Contain("!isDirect && isBattleFinished"));
        Assert.That(presenter, Does.Not.Contain("Authored Expedition Battle"));
        Assert.That(localizationBootstrap, Does.Not.Contain("Authored Expedition Battle"));
        Assert.That(localizationBootstrap, Does.Not.Contain("Authored 원정 전투"));
        Assert.That(koreanBattleTable, Does.Not.Contain("Authored"));
        Assert.That(englishBattleTable, Does.Not.Contain("Authored"));
    }

    [Test]
    public void BattleHudShell_UsesExistingPortraitAssets_AsFallbackPresentation()
    {
        var resolver = File.ReadAllText("Assets/_Game/Scripts/Runtime/Unity/UI/Battle/BattleUnitPortraitResolver.cs");

        Assert.That(resolver, Does.Contain("EnumerateCharacterIds"));
        Assert.That(resolver, Does.Contain("hero_aegis_sentinel"));
        Assert.That(resolver, Does.Contain("hero_pale_executor"));
        Assert.That(resolver, Does.Contain("boss_gate_warden"));
    }

    [Test]
    public void BattleHudShell_KeepsBattlefieldVisibleUnderBibleChrome()
    {
        var uss = File.ReadAllText("Assets/_Game/UI/Screens/Battle/BattleScreen.uss");
        var uxml = File.ReadAllText("Assets/_Game/UI/Screens/Battle/BattleScreen.uxml");

        Assert.That(uss, Does.Contain("background-color: rgba(0, 0, 0, 0)"));
        Assert.That(uxml, Does.Not.Contain("BattleBibleOverlay"));
        Assert.That(uxml, Does.Not.Contain("BattleMinimapPanel"));
        Assert.That(uxml, Does.Not.Contain("BattleCommandDock"));
        Assert.That(uxml, Does.Not.Contain("BattleResourceStack"));
        Assert.That(uss, Does.Not.Contain("sm-bs-stage-signals"));
        Assert.That(uss, Does.Not.Contain("sm-bs-stage-signal--blue"));
        Assert.That(uss, Does.Not.Contain("sm-bs-minimap-panel"));
        Assert.That(uss, Does.Not.Contain("sm-bs-command-dock"));
        Assert.That(uss, Does.Not.Contain("sm-bs-resource-stack"));
        Assert.That(uxml, Does.Contain("ProgressTrack"));
        Assert.That(uxml, Does.Contain("AllyRosterList"));
        Assert.That(uxml, Does.Contain("EnemyRosterList"));
        Assert.That(uxml, Does.Contain("PlaybackActionsGroup"));
    }

    [Test]
    public void BattleHudShell_UsesRuntimeReadouts_NotStaticMockPanels()
    {
        var viewState = File.ReadAllText("Assets/_Game/Scripts/Runtime/Unity/UI/Battle/BattleScreenViewState.cs");
        var presenter = File.ReadAllText("Assets/_Game/Scripts/Runtime/Unity/UI/Battle/BattleScreenPresenter.cs");
        var view = File.ReadAllText("Assets/_Game/Scripts/Runtime/Unity/UI/Battle/BattleScreenView.cs");
        var uxml = File.ReadAllText("Assets/_Game/UI/Screens/Battle/BattleScreen.uxml");
        var uss = File.ReadAllText("Assets/_Game/UI/Screens/Battle/BattleScreen.uss");

        Assert.That(viewState, Does.Contain("BattleCombatantTokenViewState"));
        Assert.That(viewState, Does.Contain("BattleTacticalReadoutRowViewState"));
        Assert.That(presenter, Does.Contain("BuildCombatantTokens"));
        Assert.That(presenter, Does.Contain("BuildTacticalReadoutRows"));
        Assert.That(presenter, Does.Contain("BattleReadabilityFormatter.ComputePressureScore"));
        Assert.That(view, Does.Contain("RenderCombatantTokens"));
        Assert.That(view, Does.Contain("RenderTacticalReadout"));
        Assert.That(uxml, Does.Contain("TurnOrderStrip"));
        Assert.That(uxml, Does.Contain("TacticalReadoutPanel"));
        Assert.That(uss, Does.Contain("sm-bs-combatant-token"));
        Assert.That(uss, Does.Contain("sm-bs-readout-panel"));
        Assert.That(uxml, Does.Not.Contain("BattleMinimapPanel"));
        Assert.That(uxml, Does.Not.Contain("BattleCommandDock"));
    }
}
