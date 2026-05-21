using System.IO;
using NUnit.Framework;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class TownCharacterSheetFormatterFastTests
{
    [Test]
    public void CharacterSheetProductionSurface_UsesExistingFormatterAndProfileSources()
    {
        var formatter = File.ReadAllText("Assets/_Game/Scripts/Runtime/Unity/UI/Town/TownCharacterSheetFormatter.cs");
        var presenter = File.ReadAllText("Assets/_Game/Scripts/Runtime/Unity/UI/Town/TownCharacterSheetPresenter.cs");
        var viewState = File.ReadAllText("Assets/_Game/Scripts/Runtime/Unity/UI/Town/TownCharacterSheetViewState.cs");
        var view = File.ReadAllText("Assets/_Game/Scripts/Runtime/Unity/UI/Town/TownCharacterSheetView.cs");
        var uxml = File.ReadAllText("Assets/_Game/UI/Panels/TownCharacterSheet/TownCharacterSheet.uxml");

        Assert.That(formatter, Does.Contain("session.Profile.HeroLoadouts"));
        Assert.That(formatter, Does.Contain("session.Profile.HeroProgressions"));
        Assert.That(formatter, Does.Contain("GetEquippedItems(session, hero)"));
        Assert.That(formatter, Does.Contain("BuildOverviewBody"));
        Assert.That(formatter, Does.Contain("BuildLoadoutBody"));
        Assert.That(formatter, Does.Contain("BuildProgressionBody"));
        Assert.That(presenter, Does.Contain("TownCharacterSheetFormatter"));
        Assert.That(presenter, Does.Contain("RecruitmentBalanceCatalog.DefaultRetrainCosts"));
        Assert.That(presenter, Does.Contain("DismissService.CalculateRefund"));
        Assert.That(viewState, Does.Contain("HeroId"));
        Assert.That(viewState, Does.Contain("ArchetypeLabel"));
        Assert.That(view, Does.Contain("RenderPanel(state.Overview"));
        Assert.That(uxml, Does.Contain("TownCharacterSheetRoot"));
        Assert.That(uxml, Does.Contain("TcsProgressionBody"));
    }
}
