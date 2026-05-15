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
        // Phase 5 잿골 hub V2 — 4 NPC menu (좌) + Welcome hero center + utility (우) + Atlas CTA.
        // pindoc://v1-scene-screen-routing-ashglen-hub-analysis 정합. 옛 RosterGrid+toolbar 폐기.
        var uxml = File.ReadAllText("Assets/_Game/UI/Screens/Town/TownScreen.uxml");
        Assert.That(uxml, Does.Not.Contain("RealmSummaryLabel"));
        Assert.That(uxml, Does.Contain("ReturnToStartButton"));
        Assert.That(uxml, Does.Contain("ExpeditionButton"));
        Assert.That(uxml, Does.Contain("QuickBattleButton"));
        // 4 NPC entry (달목/쇠매/갈마/솔길).
        Assert.That(uxml, Does.Contain("NpcEntry_Dalmok"));
        Assert.That(uxml, Does.Contain("NpcEntry_Soemae"));
        Assert.That(uxml, Does.Contain("NpcEntry_Galma"));
        Assert.That(uxml, Does.Contain("NpcEntry_Solgil"));
        // Welcome hero standee.
        Assert.That(uxml, Does.Contain("WelcomeHeroEntry"));
        Assert.That(uxml, Does.Contain("WelcomeHeroName"));
        // Utility entries — Roster (utility), PermanentAugment, SquadBuilder.
        Assert.That(uxml, Does.Contain("RosterButton"));
        Assert.That(uxml, Does.Contain("PermanentAugmentButton"));
        Assert.That(uxml, Does.Contain("SquadBuilderButton"));
        // Modal Templates — 5 modal (Squad/Recruit/Equipment/Passive/PermAugment) + Roster placeholder.
        Assert.That(uxml, Does.Contain("SquadBuilderTemplate"));
        Assert.That(uxml, Does.Contain("RecruitTemplate"));
        Assert.That(uxml, Does.Contain("EquipmentRefitTemplate"));
        Assert.That(uxml, Does.Contain("PassiveBoardTemplate"));
        Assert.That(uxml, Does.Contain("PermanentAugmentTemplate"));
        Assert.That(uxml, Does.Contain("RosterTemplate"));
        // 옛 hub element 폐기 — RosterGrid was default panel, now 별도 utility modal.
        Assert.That(uxml, Does.Not.Contain("FilterStrip"));
        Assert.That(uxml, Does.Not.Contain("EquipmentRefitButton"));   // NPC entry로 대체
        Assert.That(uxml, Does.Not.Contain("PassiveBoardButton"));     // NPC entry로 대체
        Assert.That(uxml, Does.Not.Contain("RecruitButton"));          // NPC entry로 대체
        // TacticalWorkshop은 audit §2.2 + P1-1 검토 후 hub에서 분리 (중복/모델 부재).
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
