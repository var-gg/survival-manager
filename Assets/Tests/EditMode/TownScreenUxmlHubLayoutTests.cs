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
[Category("FastUnit")]
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
        // TacticalWorkshop은 audit §2.2 + P1-1 검토 후 hub에서 분리 (중복/모델 부재).
        Assert.That(uxml, Does.Not.Contain("TacticalWorkshopButton"));
        Assert.That(uxml, Does.Not.Contain("TacticalWorkshopTemplate"));
        Assert.That(uxml, Does.Contain("SquadBuilderButton"));
        Assert.That(uxml, Does.Contain("SquadBuilderTemplate"));
    }

    [Test]
    public void SquadBuilderUxml_Declares_All_Anchor_And_Posture_Controls()
    {
        // audit §2.2 SquadBuilder modal — anchor 6 (Front 3 + Back 3) + posture 5.
        var uxml = File.ReadAllText("Assets/_Game/UI/Screens/Town/SquadBuilder.uxml");
        Assert.That(uxml, Does.Contain("SquadBuilderRoot"));
        Assert.That(uxml, Does.Contain("SquadBuilderCloseButton"));
        // 6 anchor
        Assert.That(uxml, Does.Contain("SquadBuilderAnchor_FrontTop"));
        Assert.That(uxml, Does.Contain("SquadBuilderAnchor_FrontCenter"));
        Assert.That(uxml, Does.Contain("SquadBuilderAnchor_FrontBottom"));
        Assert.That(uxml, Does.Contain("SquadBuilderAnchor_BackTop"));
        Assert.That(uxml, Does.Contain("SquadBuilderAnchor_BackCenter"));
        Assert.That(uxml, Does.Contain("SquadBuilderAnchor_BackBottom"));
        // 5 posture
        Assert.That(uxml, Does.Contain("SquadBuilderPosture_HoldLine"));
        Assert.That(uxml, Does.Contain("SquadBuilderPosture_StandardAdvance"));
        Assert.That(uxml, Does.Contain("SquadBuilderPosture_ProtectCarry"));
        Assert.That(uxml, Does.Contain("SquadBuilderPosture_CollapseWeakSide"));
        Assert.That(uxml, Does.Contain("SquadBuilderPosture_AllInBackline"));
    }
}
