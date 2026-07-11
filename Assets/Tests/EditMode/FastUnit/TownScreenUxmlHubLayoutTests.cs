using System.IO;
using NUnit.Framework;

namespace SM.Tests.EditMode;

/// <summary>
/// Town V1 hub UXML element 검증 (audit §2.1) — RosterGrid 12 hero grid + bottom toolbar.
/// SceneIntegrityTests의 OneTimeSetUp(Battle scene validator)에 묶이지 않도록 분리. setup-free file 텍스트 검증.
/// 옛 dashboard element (DeployButton_*/TeamPostureButton/PrevChapter/PrevSite)는 후속 phase에서
/// TacticalSetup/CharacterSheet/Recruit modal로 분리.
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
        Assert.That(uxml, Does.Contain("TownBoardFrame"));
        Assert.That(uxml, Does.Contain("TownFeatureArt"));
        Assert.That(uxml, Does.Contain("town-hub__utility-icon"));
        Assert.That(uxml, Does.Contain("town-hub__pip-row"));
        Assert.That(uxml, Does.Contain("sm-frame-corner-cap--tl"));
        Assert.That(uxml, Does.Contain("BarkLayer"));
        // Utility entries
        Assert.That(uxml, Does.Contain("RosterButton"));
        Assert.That(uxml, Does.Contain("CompendiumButton"));
        Assert.That(uxml, Does.Contain("TacticalSetupButton"));
        Assert.That(uxml, Does.Contain("TacticalSetupEntryLabel"));
        Assert.That(uxml, Does.Not.Contain("SquadBuilderButton"));
        // TacticalWorkshop wire cycle (2026-07): 전술 공방은 프로덕션 hub entry로 승격됨.
        Assert.That(uxml, Does.Contain("TacticalWorkshopButton"));
        Assert.That(uxml, Does.Contain("TacticalWorkshopEntryLabel"));
        Assert.That(uxml, Does.Contain("전술 공방"));
        Assert.That(uxml, Does.Not.Contain("전열 편성"));
        Assert.That(uxml, Does.Contain("PermanentAugmentButton"));
        Assert.That(uxml, Does.Contain("TheaterButton"));
        // CTA
        Assert.That(uxml, Does.Contain("QuickBattleButton"));
        Assert.That(uxml, Does.Contain("ExpeditionButton"));
        // Modal Templates — production Town uses Assets/_Game/UI/Panels/** as ArtBible source.
        Assert.That(uxml, Does.Contain("TacticalSetupTemplate"));
        Assert.That(uxml, Does.Not.Contain("SquadBuilderTemplate"));
        Assert.That(uxml, Does.Contain("RecruitTemplate"));
        Assert.That(uxml, Does.Contain("EquipmentRefitTemplate"));
        Assert.That(uxml, Does.Contain("PassiveBoardTemplate"));
        Assert.That(uxml, Does.Contain("PermanentAugmentTemplate"));
        Assert.That(uxml, Does.Contain("InventoryTemplate"));
        Assert.That(uxml, Does.Contain("RosterTemplate"));
        Assert.That(uxml, Does.Contain("CharacterSheetTemplate"));
        Assert.That(uxml, Does.Contain("CompendiumTemplate"));
        Assert.That(uxml, Does.Contain("TacticalWorkshopTemplate"));
        Assert.That(uxml, Does.Contain("../../Panels/TownSquadBuilder/TownSquadBuilder.uxml"));
        Assert.That(uxml, Does.Contain("../../Panels/RecruitPack/RecruitPack.uxml"));
        Assert.That(uxml, Does.Contain("../../Panels/EquipmentRefit/EquipmentRefit.uxml"));
        Assert.That(uxml, Does.Contain("../../Panels/InventoryTab/InventoryTab.uxml"));
        Assert.That(uxml, Does.Contain("../../Panels/TownCharacterSheet/TownCharacterSheet.uxml"));
        Assert.That(uxml, Does.Contain("../../Panels/SkillCompendium/SkillCompendium.uxml"));
        Assert.That(uxml, Does.Contain("../../Panels/TacticalWorkshop/TacticalWorkshop.uxml"));
        var characterSheetUxml = File.ReadAllText("Assets/_Game/UI/Panels/TownCharacterSheet/TownCharacterSheet.uxml");
        Assert.That(characterSheetUxml, Does.Contain("TownCharacterSheetRoot"));
        Assert.That(characterSheetUxml, Does.Contain("TcsHeroNameLabel"));
        Assert.That(characterSheetUxml, Does.Contain("TcsProgressionBody"));
        Assert.That(characterSheetUxml, Does.Contain("tcs-portrait-column"));
        Assert.That(characterSheetUxml, Does.Contain("TcsHeroRail"));
        Assert.That(characterSheetUxml, Does.Contain("TcsPortraitImage"));
        Assert.That(characterSheetUxml, Does.Contain("TcsStatGrid"));
        Assert.That(characterSheetUxml, Does.Contain("TcsProgressionTrack"));
        Assert.That(characterSheetUxml, Does.Contain("TcsSkillList"));
        Assert.That(characterSheetUxml, Does.Contain("TcsEquipmentRow"));
        // 장식 tcs-tab-strip(라벨·핸들러 0, false affordance)은 제거됨 — 실제 스킬 패널 존재로 검증 대체.
        Assert.That(characterSheetUxml, Does.Contain("tcs-panel--skill-list"));
        Assert.That(characterSheetUxml, Does.Contain("tcs-panel--equipment"));
        var townPresenter = File.ReadAllText("Assets/_Game/Scripts/Runtime/Unity/UI/Town/TownScreenPresenter.cs");
        Assert.That(townPresenter, Does.Contain("SetHeroOpener"));
        var townController = File.ReadAllText("Assets/_Game/Scripts/Runtime/Unity/TownScreenController.cs");
        Assert.That(townController, Does.Contain("TryWireCharacterSheet"));
        Assert.That(townController, Does.Contain("TryWireTacticalSetup"));
        Assert.That(townController, Does.Contain("TryWireTacticalWorkshop"));
        Assert.That(townController, Does.Contain("RosterGridPresenter"));
        Assert.That(townController, Does.Contain("BindRosterOpen"));
        var townView = File.ReadAllText("Assets/_Game/Scripts/Runtime/Unity/UI/Town/TownScreenView.cs");
        Assert.That(townView, Does.Contain("BindTacticalSetupOpen"));
        Assert.That(townView, Does.Contain("BindTacticalWorkshopOpen"));
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
    public void TownScreenUss_Uses_ServiceHub_Bible_Board_Frame()
    {
        var uss = File.ReadAllText("Assets/_Game/UI/Screens/Town/TownScreen.uss");
        Assert.That(uss, Does.Contain("Town Service Hub Bible pass"));
        Assert.That(uss, Does.Contain(".town-hub__board-frame"));
        Assert.That(uss, Does.Contain("ui_panel_frame_outer.png"));
        Assert.That(uss, Does.Contain(".town-hub__npc-strip .sm-face-card--lg"));
        Assert.That(uss, Does.Contain(".town-hub__center"));
        Assert.That(uss, Does.Contain(".town-hub__feature-art"));
        Assert.That(uss, Does.Contain(".town-hub__utility-icon"));
        Assert.That(uss, Does.Contain(".town-hub__pip-row"));
        Assert.That(uss, Does.Contain(".town-hub__utility-strip"));
        Assert.That(uss, Does.Contain(".town-hub__hero-cluster"));
        Assert.That(uss, Does.Contain(".town-hub__cta-bar"));
    }

    [Test]
    public void TacticalSetupUxml_Declares_Formation_Posture_Operation_And_Response_Sections()
    {
        // 전술 설정 production surface — TownSquadBuilder/SquadBuilderPresenter는 implementation alias.
        var uxml = File.ReadAllText("Assets/_Game/UI/Panels/TownSquadBuilder/TownSquadBuilder.uxml");
        Assert.That(uxml, Does.Contain("SquadBuilderRoot"));
        Assert.That(uxml, Does.Contain("SquadBuilderCloseButton"));
        // wave-58 mockup-align: title을 "전술 설정" → "전술 편성"으로 변경 (시안 갤러리 squad workshop v1).
        Assert.That(uxml, Does.Contain("전술 편성"));
        Assert.That(uxml, Does.Not.Contain("전열 편성"));
        Assert.That(uxml, Does.Contain("SquadBuilderThreeZoneLayout"));
        Assert.That(uxml, Does.Contain("SquadBuilderRosterRail"));
        Assert.That(uxml, Does.Contain("SquadBuilderRosterCountLabel"));
        Assert.That(uxml, Does.Contain("SquadBuilderRosterList"));
        Assert.That(uxml, Does.Contain("SquadBuilderFormationBoard"));
        Assert.That(uxml, Does.Contain("SquadBuilderSelectedDetail"));
        Assert.That(uxml, Does.Contain("SquadBuilderSelectedHeroName"));
        Assert.That(uxml, Does.Contain("SquadBuilderSelectedHeroLoadout"));
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
        Assert.That(uxml, Does.Contain("팀 태세"));
        Assert.That(uxml, Does.Contain("TacticalSetupFormationSection"));
        Assert.That(uxml, Does.Contain("TacticalSetupPostureSection"));
        Assert.That(uxml, Does.Contain("TacticalSetupOperationSection"));
        Assert.That(uxml, Does.Contain("TacticalSetupResponseSection"));
        Assert.That(uxml, Does.Contain("SquadBuilderOperationRows"));
        Assert.That(uxml, Does.Contain("SquadBuilderResponseSummaryLabel"));
        Assert.That(uxml, Does.Contain("SquadBuilderSynergyChips"));
        Assert.That(uxml, Does.Contain("전열"));
        Assert.That(uxml, Does.Contain("운용"));
        Assert.That(uxml, Does.Contain("대응"));
        Assert.That(uxml, Does.Not.Contain("자세 (Team Posture)"));

        // 헤드리스-순수화 3파일 분리: presenter(순수 BuildState) / SquadBuilderView(UITK DOM) / ViewState(record).
        var presenter = File.ReadAllText("Assets/_Game/Scripts/Runtime/Unity/UI/Town/SquadBuilderPresenter.cs");
        Assert.That(presenter, Does.Contain("Profile.Heroes"));
        Assert.That(presenter, Does.Contain("BuildHeroRow"));
        Assert.That(presenter, Does.Contain("BuildOperationRows"));
        Assert.That(presenter, Does.Contain("ResolveRoleInstructionId"));
        Assert.That(presenter, Does.Contain("ResolveBehaviorProfile"));
        // "가짜 수치 없음" 문자열 마커는 c58d64a4(시너지 실연결)에서 소스가 사라져 stale — 실배선 마커로 교체.
        Assert.That(presenter, Does.Contain("SquadSynergyPreview.Evaluate"));
        Assert.That(presenter, Does.Contain("ResolveRosterPipCount"));
        Assert.That(presenter, Does.Contain("팀 태세 갱신"));
        Assert.That(presenter, Does.Not.Contain("자세 갱신"));
        // 순수성 가드 — presenter가 다시 UITK/씬 루트에 물리면 실패해야 한다.
        Assert.That(presenter, Does.Not.Contain("UnityEngine.UIElements"));
        Assert.That(presenter, Does.Not.Contain("GameSessionRoot"));

        var squadBuilderView = File.ReadAllText("Assets/_Game/Scripts/Runtime/Unity/UI/Town/SquadBuilderView.cs");
        Assert.That(squadBuilderView, Does.Contain("ISquadBuilderView"));
        Assert.That(squadBuilderView, Does.Contain("sm-sqb-modal__roster-icon"));
        Assert.That(squadBuilderView, Does.Contain("SquadBuilderTargetDirectiveButton"));

        // LoadoutView/save seam은 조립부(TownScreenController)가 delegate로 주입한다.
        var townController = File.ReadAllText("Assets/_Game/Scripts/Runtime/Unity/TownScreenController.cs");
        Assert.That(townController, Does.Contain("ProfileQueries.GetLoadoutView"));
        Assert.That(townController, Does.Contain("new SquadBuilderView"));

        var uss = File.ReadAllText("Assets/_Game/UI/Panels/TownSquadBuilder/TownSquadBuilder.uss");
        Assert.That(uss, Does.Contain("dense formation board"));
        Assert.That(uss, Does.Contain(".sm-sqb-modal__roster-pips"));
        Assert.That(uss, Does.Contain(".sm-sqb-modal__anchor-button--selected"));
    }

    [Test]
    public void TacticalWorkshopUxml_Is_Production_Tactics_Surface()
    {
        // TacticalWorkshop wire cycle (2026-07): legacy-preview → 프로덕션 전술 표면 승격.
        // 배치 편집은 전술 설정(SquadBuilder), 태세·유닛 지시는 전술 공방 — panel-responsibility-matrix §2.
        var townUxml = File.ReadAllText("Assets/_Game/UI/Screens/Town/TownScreen.uxml");
        var townController = File.ReadAllText("Assets/_Game/Scripts/Runtime/Unity/TownScreenController.cs");
        Assert.That(townUxml, Does.Contain("TacticalWorkshopTemplate"));
        Assert.That(townUxml, Does.Contain("TacticalWorkshopButton"));
        Assert.That(townController, Does.Contain("TryWireTacticalWorkshop"));
        Assert.That(townController, Does.Contain("TacticalWorkshopPresenter"));
        Assert.That(townController, Does.Contain("TacticalWorkshopView"));

        var uxml = File.ReadAllText("Assets/_Game/UI/Panels/TacticalWorkshop/TacticalWorkshop.uxml");
        // zone labels = 책임 매트릭스 §2 (팀 태세 / 발동 시너지 / 위협 답수 / 유닛 전술) + 헤더 "전술 공방".
        Assert.That(uxml, Does.Contain("전술 공방"));
        Assert.That(uxml, Does.Contain("팀 태세"));
        Assert.That(uxml, Does.Contain("발동 시너지"));
        Assert.That(uxml, Does.Contain("위협 답수"));
        Assert.That(uxml, Does.Contain("유닛 전술"));
        Assert.That(uxml, Does.Not.Contain("팀 자세"));
        Assert.That(uxml, Does.Contain("PostureCardRow"));
        Assert.That(uxml, Does.Contain("TacticPresetRows"));
        // 유닛 전술 strip은 가시 컨테이너 — hidden shim 금지, command chip은 동적 렌더(정적 수치 금지).
        Assert.That(uxml, Does.Not.Contain("twp-tactic-rows-hidden"));
        Assert.That(uxml, Does.Contain("TwpDeployChip"));
        Assert.That(uxml, Does.Contain("TwpCloseButton"));
        Assert.That(uxml, Does.Not.Contain("배치 4 / 6 슬롯"));

        var presenter = File.ReadAllText("Assets/_Game/Scripts/Runtime/Unity/UI/Town/Preview/TacticalWorkshopPresenter.cs");
        Assert.That(presenter, Does.Not.Contain("Array.Empty<TacticalWorkshopHeroTacticViewState>()"));
        Assert.That(presenter, Does.Contain("new TacticalWorkshopHeroTacticViewState"));
        Assert.That(presenter, Does.Contain("ResolveDefaultRoleInstructionId"));
        // 시너지/위협은 SquadBuilder와 같은 SoT — 정적 사본 catalog 재도입 금지.
        Assert.That(presenter, Does.Contain("SquadSynergyPreview.Evaluate"));
        Assert.That(presenter, Does.Contain("SquadCounterCoveragePreview.Dimensions"));
        Assert.That(presenter, Does.Not.Contain("SynergyCatalogEntry"));
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
        Assert.That(theme, Does.Contain("ui_button_gold.png"));
        Assert.That(theme, Does.Contain("ui_button_dark.png"));

        var panelPaths = new[]
        {
            "Assets/_Game/UI/Panels/RecruitPack/RecruitPack.uss",
            "Assets/_Game/UI/Panels/PermanentAugment/PermanentAugment.uss",
            "Assets/_Game/UI/Panels/PassiveBoard/PassiveBoard.uss",
            "Assets/_Game/UI/Panels/SkillCompendium/SkillCompendium.uss",
            "Assets/_Game/UI/Panels/InventoryTab/InventoryTab.uss",
            "Assets/_Game/UI/Panels/EquipmentRefit/EquipmentRefit.uss",
            "Assets/_Game/UI/Panels/TownCharacterSheet/TownCharacterSheet.uss",
            "Assets/_Game/UI/Panels/TownSquadBuilder/TownSquadBuilder.uss",
            "Assets/_Game/UI/Panels/TownRosterGrid/TownRosterGrid.uss",
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

        var recruitUxml = File.ReadAllText("Assets/_Game/UI/Panels/RecruitPack/RecruitPack.uxml");
        var permanentUxml = File.ReadAllText("Assets/_Game/UI/Panels/PermanentAugment/PermanentAugment.uxml");
        var passiveBoardUxml = File.ReadAllText("Assets/_Game/UI/Panels/PassiveBoard/PassiveBoard.uxml");
        var inventoryUxml = File.ReadAllText("Assets/_Game/UI/Panels/InventoryTab/InventoryTab.uxml");
        var equipmentRefitUxml = File.ReadAllText("Assets/_Game/UI/Panels/EquipmentRefit/EquipmentRefit.uxml");
        Assert.That(recruitUxml, Does.Contain("sm-cta--primary"));
        Assert.That(permanentUxml, Does.Contain("sm-cta--primary"));
        Assert.That(passiveBoardUxml, Does.Contain("sm-cta--primary"));
        Assert.That(inventoryUxml, Does.Contain("sm-cta--primary"));
        Assert.That(equipmentRefitUxml, Does.Contain("sm-cta--primary"));
    }
}
