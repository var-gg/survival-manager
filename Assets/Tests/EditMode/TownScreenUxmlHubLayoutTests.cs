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
        // Phase 7 잿골 hub V3 — pindoc://decision-town-hub-v3-ashglen-face-cluster.
        // 얼굴 중심 cluster + 가변 deploy + 4 NPC ambient + utility + Atlas CTA + bark layer.
        // NPC + hero face card는 View가 코드로 build (NpcStrip / WelcomeCaptainMount / DeployRow / RosterRow container만 UXML).
        var uxml = File.ReadAllText("Assets/_Game/UI/Screens/Town/TownScreen.uxml");
        // Top utility bar
        Assert.That(uxml, Does.Contain("TitleEyebrowLabel"));
        Assert.That(uxml, Does.Contain("TitleLabel"));
        Assert.That(uxml, Does.Contain("LocaleKoButton"));
        Assert.That(uxml, Does.Contain("LocaleEnButton"));
        Assert.That(uxml, Does.Contain("HelpButton"));
        Assert.That(uxml, Does.Contain("SaveButton"));
        Assert.That(uxml, Does.Contain("LoadButton"));
        Assert.That(uxml, Does.Contain("SettingsButton"));
        Assert.That(uxml, Does.Contain("ReturnToStartButton"));
        // V3 layout containers — face card는 코드 build
        Assert.That(uxml, Does.Contain("NpcStrip"));
        Assert.That(uxml, Does.Contain("WelcomeCaptainMount"));
        Assert.That(uxml, Does.Contain("WelcomeCaptainGreeting"));
        Assert.That(uxml, Does.Contain("DeployRow"));
        Assert.That(uxml, Does.Contain("RosterRow"));
        Assert.That(uxml, Does.Contain("BarkLayer"));
        // Utility entries (4)
        Assert.That(uxml, Does.Contain("RosterButton"));
        Assert.That(uxml, Does.Contain("SquadBuilderButton"));
        Assert.That(uxml, Does.Contain("PermanentAugmentButton"));
        Assert.That(uxml, Does.Contain("TheaterButton"));
        // CTA
        Assert.That(uxml, Does.Contain("QuickBattleButton"));
        Assert.That(uxml, Does.Contain("ExpeditionButton"));
        // Modal Templates — 7 (Squad/Recruit/Equipment/Passive/PermAugment/Inventory/Roster)
        Assert.That(uxml, Does.Contain("SquadBuilderTemplate"));
        Assert.That(uxml, Does.Contain("RecruitTemplate"));
        Assert.That(uxml, Does.Contain("EquipmentRefitTemplate"));
        Assert.That(uxml, Does.Contain("PassiveBoardTemplate"));
        Assert.That(uxml, Does.Contain("PermanentAugmentTemplate"));
        Assert.That(uxml, Does.Contain("InventoryTemplate"));
        Assert.That(uxml, Does.Contain("RosterTemplate"));
        // 옛 V1/V2 element 폐기 검증
        Assert.That(uxml, Does.Not.Contain("RealmSummaryLabel"));
        Assert.That(uxml, Does.Not.Contain("NpcEntry_Dalmok"));      // V2 NPC entry — 코드 build로 대체
        Assert.That(uxml, Does.Not.Contain("WelcomeHeroEntry"));      // V2 standee — Welcome captain face card로 대체
        Assert.That(uxml, Does.Not.Contain("FilterStrip"));           // V1 RosterGrid filter chip
        Assert.That(uxml, Does.Not.Contain("EquipmentRefitButton"));  // V2 toolbar — NPC click 매핑
        Assert.That(uxml, Does.Not.Contain("PassiveBoardButton"));    // V2 toolbar
        // TacticalWorkshop은 commit dbbb9d4a에서 분리 (audit §2.2 + P1-1)
        Assert.That(uxml, Does.Not.Contain("TacticalWorkshopButton"));
        Assert.That(uxml, Does.Not.Contain("TacticalWorkshopTemplate"));
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
