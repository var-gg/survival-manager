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
        Assert.That(uxml, Does.Contain("../../Foundation/Components/HeroFaceCard.uss"));
        Assert.That(uxml, Does.Contain("NpcStrip"));
        Assert.That(uxml, Does.Contain("WelcomeCaptainMount"));
        Assert.That(uxml, Does.Contain("WelcomeCaptainGreeting"));
        Assert.That(uxml, Does.Contain("DeployRow"));
        Assert.That(uxml, Does.Contain("RosterRow"));
        Assert.That(uxml, Does.Contain("BarkLayer"));
        // Utility entries
        Assert.That(uxml, Does.Contain("RosterButton"));
        Assert.That(uxml, Does.Contain("CompendiumButton"));
        Assert.That(uxml, Does.Contain("SquadBuilderButton"));
        Assert.That(uxml, Does.Contain("TacticalWorkshopButton"));
        Assert.That(uxml, Does.Contain("PermanentAugmentButton"));
        Assert.That(uxml, Does.Contain("TheaterButton"));
        // CTA
        Assert.That(uxml, Does.Contain("QuickBattleButton"));
        Assert.That(uxml, Does.Contain("ExpeditionButton"));
        // Modal Templates — production Town uses Assets/_Game/UI/Panels/** as ArtBible source.
        Assert.That(uxml, Does.Contain("SquadBuilderTemplate"));
        Assert.That(uxml, Does.Contain("RecruitTemplate"));
        Assert.That(uxml, Does.Contain("EquipmentRefitTemplate"));
        Assert.That(uxml, Does.Contain("PassiveBoardTemplate"));
        Assert.That(uxml, Does.Contain("PermanentAugmentTemplate"));
        Assert.That(uxml, Does.Contain("InventoryTemplate"));
        Assert.That(uxml, Does.Contain("RosterTemplate"));
        Assert.That(uxml, Does.Contain("CompendiumTemplate"));
        Assert.That(uxml, Does.Contain("TacticalWorkshopTemplate"));
        Assert.That(uxml, Does.Contain("../../Panels/RecruitPack/RecruitPack.uxml"));
        Assert.That(uxml, Does.Contain("../../Panels/EquipmentRefit/EquipmentRefit.uxml"));
        Assert.That(uxml, Does.Contain("../../Panels/InventoryTab/InventoryTab.uxml"));
        Assert.That(uxml, Does.Contain("../../Panels/SkillCompendium/SkillCompendium.uxml"));
        Assert.That(uxml, Does.Contain("../../Panels/TacticalWorkshop/TacticalWorkshop.uxml"));
        var compendiumUxml = File.ReadAllText("Assets/_Game/UI/Panels/SkillCompendium/SkillCompendium.uxml");
        Assert.That(compendiumUxml, Does.Contain("CompendiumSearchField"));
        Assert.That(compendiumUxml, Does.Contain("CompendiumClassFilter"));
        Assert.That(compendiumUxml, Does.Contain("CompendiumSlotFilter"));
        Assert.That(compendiumUxml, Does.Contain("CompendiumVfxFamilyFilter"));
        Assert.That(compendiumUxml, Does.Contain("CompendiumVfxPreviewStage"));
        Assert.That(compendiumUxml, Does.Contain("CompendiumVfxPrefabPreviewImage"));
        Assert.That(compendiumUxml, Does.Contain("CompendiumVfxReplayButton"));
        Assert.That(compendiumUxml, Does.Contain("CompendiumVfxProjectile"));
        Assert.That(compendiumUxml, Does.Contain("CompendiumVfxPatternLabel"));
        Assert.That(compendiumUxml, Does.Contain("CompendiumVfxCasterLabel"));
        var compendiumUss = File.ReadAllText("Assets/_Game/UI/Screens/Town/Preview/CompendiumPreview.uss");
        Assert.That(compendiumUss, Does.Contain("cmp-entry-list--skill-grid"));
        Assert.That(compendiumUss, Does.Contain("cmp-skill-card"));
        Assert.That(compendiumUss, Does.Contain("cmp-entry-list--character-grid"));
        Assert.That(compendiumUss, Does.Contain("cmp-character-card"));
        Assert.That(compendiumUss, Does.Contain("cmp-character-card__silhouette"));
        Assert.That(compendiumUss, Does.Contain("cmp-vfx-hud"));
        Assert.That(compendiumUss, Does.Contain("cmp-vfx-actor-label"));
        var compendiumView = File.ReadAllText("Assets/_Game/Scripts/Runtime/Unity/UI/Town/Preview/CompendiumView.cs");
        Assert.That(compendiumView, Does.Contain("BuildSkillCard"));
        Assert.That(compendiumView, Does.Contain("BuildCharacterCard"));
        // 옛 V1/V2 element 폐기 검증
        Assert.That(uxml, Does.Not.Contain("RealmSummaryLabel"));
        Assert.That(uxml, Does.Not.Contain("NpcEntry_Dalmok"));      // V2 NPC entry — 코드 build로 대체
        Assert.That(uxml, Does.Not.Contain("WelcomeHeroEntry"));      // V2 standee — Welcome captain face card로 대체
        Assert.That(uxml, Does.Not.Contain("FilterStrip"));           // V1 RosterGrid filter chip
        Assert.That(uxml, Does.Not.Contain("EquipmentRefitButton"));  // V2 toolbar — NPC click 매핑
        Assert.That(uxml, Does.Not.Contain("PassiveBoardButton"));    // V2 toolbar
    }

    [Test]
    public void SquadBuilderUxml_Declares_All_Anchor_And_Posture_Controls()
    {
        // audit §2.2 SquadBuilder modal — anchor 6 (Front 3 + Back 3) + posture 5.
        var uxml = File.ReadAllText("Assets/_Game/UI/Panels/TownSquadBuilder/TownSquadBuilder.uxml");
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

    [Test]
    public void CommonDetailContracts_Declare_Skill_Item_Status_Surfaces()
    {
        var uss = File.ReadAllText("Assets/_Game/UI/Foundation/USS/common_detail.uss");
        Assert.That(uss, Does.Contain("sm-cd-modal"));
        Assert.That(uss, Does.Contain("sm-cd-bottom-sheet"));
        Assert.That(uss, Does.Contain("sm-cd-card"));
        Assert.That(uss, Does.Contain("sm-cd-tag"));

        var skill = File.ReadAllText("Assets/_Game/UI/Foundation/Details/SkillDetailModal.uxml");
        Assert.That(skill, Does.Contain("SkillDetailNameLabel"));
        Assert.That(skill, Does.Contain("SkillDetailTimingLabel"));
        Assert.That(skill, Does.Contain("SkillDetailScalingLabel"));

        var item = File.ReadAllText("Assets/_Game/UI/Foundation/Details/ItemDetailModal.uxml");
        Assert.That(item, Does.Contain("ItemDetailMetaLabel"));
        Assert.That(item, Does.Contain("ItemDetailBudgetLabel"));
        Assert.That(item, Does.Contain("ItemDetailCrossLinks"));

        var status = File.ReadAllText("Assets/_Game/UI/Foundation/Details/StatusEffectTooltipPanel.uxml");
        Assert.That(status, Does.Contain("StatusEffectDurationLabel"));
        Assert.That(status, Does.Contain("StatusEffectOwnerLabel"));
        Assert.That(status, Does.Contain("StatusEffectCleanseLabel"));

        var battle = File.ReadAllText("Assets/_Game/UI/Screens/Battle/BattleScreen.uxml");
        Assert.That(battle, Does.Contain("sm-cd-modal"));
        Assert.That(battle, Does.Contain("sm-cd-panel"));

        var inventory = File.ReadAllText("Assets/_Game/UI/Panels/InventoryTab/InventoryTab.uxml");
        Assert.That(inventory, Does.Contain("DetailCrossLinks"));
        Assert.That(inventory, Does.Contain("sm-cd-panel"));
    }

    [Test]
    public void ProductionTownPanels_Do_Not_Use_Floating_Frame_As_Inner_Card_Chrome()
    {
        // decision-ui-chrome-hierarchy-slice-discipline:
        // inner panel/card/icon chrome must be L2/L3/L4 line work, not repeated ornate 9-slice.
        var theme = File.ReadAllText("Assets/_Game/UI/Foundation/Styles/RuntimePanelTheme.uss");
        Assert.That(theme, Does.Contain(".sm-chrome-l2"));
        Assert.That(theme, Does.Contain(".sm-chrome-l3"));
        Assert.That(theme, Does.Contain(".sm-chrome-l4"));

        var panelPaths = new[]
        {
            "Assets/_Game/UI/Panels/RecruitPack/RecruitPack.uss",
            "Assets/_Game/UI/Panels/PermanentAugment/PermanentAugment.uss",
            "Assets/_Game/UI/Panels/PassiveBoard/PassiveBoard.uss",
            "Assets/_Game/UI/Panels/SkillCompendium/SkillCompendium.uss",
            "Assets/_Game/UI/Panels/InventoryTab/InventoryTab.uss",
            "Assets/_Game/UI/Panels/EquipmentRefit/EquipmentRefit.uss",
            "Assets/_Game/UI/Panels/TacticalWorkshop/TacticalWorkshop.uss",
        };

        foreach (var path in panelPaths)
        {
            var uss = File.ReadAllText(path);
            Assert.That(uss, Does.Contain("ui_panel_frame_outer.png"), path);
            Assert.That(uss, Does.Not.Contain("ui_panel_frame_inner.png"), path);
            Assert.That(uss, Does.Not.Contain("ui_card_frame_normal.png"), path);
            Assert.That(uss, Does.Not.Contain("ui_card_frame_selected_base.png"), path);
            Assert.That(uss, Does.Not.Contain("ui_card_frame_locked.png"), path);
            Assert.That(uss, Does.Not.Contain("ui_icon_slot_frame.png"), path);
        }

        var recruit = File.ReadAllText(panelPaths[0]);
        var permanent = File.ReadAllText(panelPaths[1]);
        var passiveBoard = File.ReadAllText(panelPaths[2]);
        var inventory = File.ReadAllText(panelPaths[4]);
        var equipmentRefit = File.ReadAllText(panelPaths[5]);
        Assert.That(recruit, Does.Contain("ui_button_gold.png"));
        Assert.That(permanent, Does.Contain("ui_button_gold.png"));
        Assert.That(passiveBoard, Does.Contain("ui_button_gold.png"));
        Assert.That(inventory, Does.Contain("ui_button_gold.png"));
        Assert.That(equipmentRefit, Does.Contain("ui_button_gold.png"));
    }
}
