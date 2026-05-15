using System.IO;
using NUnit.Framework;

namespace SM.Tests.EditMode;

/// <summary>
/// Town V1 hub UXML element 검증 (audit §2.1) — RosterGrid 12 hero grid + bottom toolbar.
/// SceneIntegrityTests의 OneTimeSetUp(Battle scene validator)에 묶이지 않도록 분리. setup-free file 텍스트 검증.
/// 옛 dashboard element (DeployButton_*/TeamPostureButton/PrevChapter/PrevSite)는 후속 phase에서
/// SquadBuilder/CharacterSheet/Recruit modal로 분리.
/// </summary>
[TestFixture]
public sealed class TownScreenUxmlHubLayoutTests
{
    [Test]
    public void TownScreenUxml_Declares_Hub_Layout_Controls()
    {
        var uxml = File.ReadAllText("Assets/_Game/UI/Screens/Town/TownScreen.uxml");
        Assert.That(uxml, Does.Not.Contain("RealmSummaryLabel"));
        Assert.That(uxml, Does.Contain("ReturnToStartButton"));
        Assert.That(uxml, Does.Contain("ExpeditionButton"));
        Assert.That(uxml, Does.Contain("QuickBattleButton"));
        Assert.That(uxml, Does.Contain("GridContainer"));
        Assert.That(uxml, Does.Contain("FilterStrip"));
        Assert.That(uxml, Does.Contain("HeroCount"));
        Assert.That(uxml, Does.Contain("TacticalWorkshopButton"));
    }
}
