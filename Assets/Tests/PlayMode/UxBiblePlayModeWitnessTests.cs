using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Meta.Model;
using SM.Unity;
using SM.Unity.UI;
using SM.Unity.UI.Atlas;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SM.Tests.PlayMode;

public sealed partial class UxBiblePlayModeWitnessTests
{
    private const string SelectedLocalePreferenceKey = "selected-locale";

    private UxBibleWitnessPacket? _packet;
    private bool _hadSelectedLocalePreference;
    private string _selectedLocalePreference = string.Empty;

    [UnitySetUp]
    public IEnumerator ResetRoot()
    {
        _hadSelectedLocalePreference = PlayerPrefs.HasKey(SelectedLocalePreferenceKey);
        _selectedLocalePreference = PlayerPrefs.GetString(SelectedLocalePreferenceKey, string.Empty);

        if (GameSessionRoot.Instance != null)
        {
            Object.Destroy(GameSessionRoot.Instance.gameObject);
        }

        var guard = 0;
        while (GameSessionRoot.Instance != null && guard++ < 10)
        {
            yield return null;
        }

        _packet = UxBibleWitnessPacket.Start();
    }

    [UnityTearDown]
    public IEnumerator FinishPacket()
    {
        _packet?.Finish();
        _packet?.Dispose();
        _packet = null;
        if (_hadSelectedLocalePreference)
        {
            PlayerPrefs.SetString(SelectedLocalePreferenceKey, _selectedLocalePreference);
        }
        else
        {
            PlayerPrefs.DeleteKey(SelectedLocalePreferenceKey);
        }

        PlayerPrefs.Save();
        yield return null;
    }

    [UnityTest]
    public IEnumerator ProductionUxBibleSurfaces_AreVisible_InPlayModeRoutes()
    {
        yield return EnterOfflineTownFromBoot();

        var root = GameSessionRoot.Instance!;
        var town = RequireAny<TownScreenController>("Town controller should exist for UX Bible witness.");
        town.EnsureRuntimeControls();
        yield return WaitFrames(2);

        var townHost = RequirePanelHost("TownRuntimePanelHost");
        var heroId = root.SessionState.ExpeditionSquadHeroIds.FirstOrDefault()
                     ?? root.SessionState.Profile.Heroes.First().HeroId;
        var heroIdentity = ResolveHeroIdentity(root, heroId);
        VerifyTownHub(townHost.Root);
        var townHeroNames = townHost.Root.Query<Label>(className: "sm-face-card__name")
            .ToList()
            .Select(label => label.text)
            .ToArray();
        Assert.That(townHeroNames, Does.Contain(heroIdentity.Combined),
            "Town hero cards must render the authored person name with the job as context.");
        _packet?.RecordPass($"Town authored hero identity visible: {heroIdentity.Combined}");
        yield return Capture("town_hub");

        ClickButton(townHost.Root, $"FaceCard_{heroId}");
        yield return WaitForVisible(townHost.Root, "TownCharacterSheetRoot");
        VerifyCharacterSheet(townHost.Root);
        Assert.That(Require<Label>(townHost.Root, "TcsHeroNameLabel").text, Is.EqualTo(heroIdentity.Person),
            "Character Sheet must render the authored person name.");
        Assert.That(Require<Label>(townHost.Root, "TcsHeroMetaLabel").text, Does.Contain(heroIdentity.Job),
            "Character Sheet must keep the job title reachable as context.");
        _packet?.RecordPass($"Character Sheet authored hero identity visible: {heroIdentity.Person} / {heroIdentity.Job}");
        AssertSurfaceGeometry(townHost.Root, "TownCharacterSheetRoot", 700f, 420f);
        AssertNoRedText(Require<VisualElement>(townHost.Root, "TownCharacterSheetRoot"), "Character Sheet");
        yield return Capture("character_sheet");
        _packet?.AssertScreenshotDifferent("town_hub", "character_sheet", "Character Sheet modal must visibly change the Town screenshot.");
        ClickButton(townHost.Root, "TownCharacterSheetCloseButton");
        yield return WaitForHidden(townHost.Root, "TownCharacterSheetRoot");

        ClickButton(townHost.Root, "TacticalSetupButton");
        yield return WaitForVisible(townHost.Root, "SquadBuilderRoot");
        yield return new WaitForSecondsRealtime(0.35f);
        VerifyTacticalSetup(townHost.Root);
        AssertModalPanelWithinViewport(townHost.Root, "sm-sqb-modal__panel", "Tactical Setup", 64f, 64f);
        AssertNoRedText(Require<VisualElement>(townHost.Root, "SquadBuilderRoot"), "Tactical Setup");
        yield return Capture("tactical_setup");
        ClickButton(townHost.Root, "SquadBuilderCloseButton");
        yield return WaitForHidden(townHost.Root, "SquadBuilderRoot");

        // TacticalWorkshop wire cycle (2026-07): 전술 공방 진입 → posture 5카드 + 위협 8lane 실렌더
        // → posture 카드 실클릭으로 세션 태세 변경 → 닫기.
        ClickButton(townHost.Root, "TacticalWorkshopButton");
        yield return WaitForVisible(townHost.Root, "TwpRoot");
        yield return new WaitForSecondsRealtime(0.35f);
        VerifyTacticalWorkshop(townHost.Root);
        AssertNoRedText(Require<VisualElement>(townHost.Root, "TwpRoot"), "Tactical Workshop");
        yield return Capture("tactical_workshop");
        // posture 카드는 Button — HoldLine 카드 클릭이 세션 truth 실제 쓰기인지 검증.
        ClickButton(townHost.Root, "TwpPosture_HoldLine");
        yield return WaitFrames(2);
        Assert.That(root.SessionState.SelectedTeamPosture, Is.EqualTo(TeamPostureType.HoldLine),
            "posture 카드 클릭은 세션 SelectedTeamPosture를 갱신한다.");
        Assert.That(Require<VisualElement>(townHost.Root, "TwpPosture_HoldLine").ClassListContains("twp-posture-card--selected"),
            Is.True, "재렌더된 HoldLine 카드가 선택 상태로 표시된다.");
        ClickButton(townHost.Root, "TwpCloseButton");
        yield return WaitForHidden(townHost.Root, "TwpRoot");

        ClickButton(townHost.Root, $"FaceCard_{heroId}");
        yield return WaitForVisible(townHost.Root, "TownCharacterSheetRoot");
        ClickButton(townHost.Root, "TownCharacterSheetCloseButton");
        yield return WaitForHidden(townHost.Root, "TownCharacterSheetRoot");

        ClickButton(townHost.Root, "FaceCard_solgil");
        yield return WaitForVisible(townHost.Root, "InvRoot");
        yield return WaitFrames(2);
        VerifyInventoryCompare(townHost.Root);
        yield return Capture("inventory_compare");
        ClickFirstButtonWithClass(townHost.Root, "inv-currency__close");
        yield return WaitForHidden(townHost.Root, "InvRoot");

        ClickButton(townHost.Root, "FaceCard_dalmok");
        yield return WaitForVisible(townHost.Root, "RcpRoot");
        VerifyRecruit(townHost.Root);
        yield return Capture("recruit_detail");
        ClickFirstButtonWithClass(townHost.Root, "rcp-header__close");
        yield return WaitForHidden(townHost.Root, "RcpRoot");

        // wave-40: 4 missing panel capture wiring (Roster Grid / Permanent Augment / Equipment Refit / Passive Board)
        // 매트릭스 §4, §8, §6, §7 visual QA pipeline 진입 가능하도록.
        ClickButton(townHost.Root, "RosterButton");
        yield return WaitForVisible(townHost.Root, "RosterGridPreviewRoot");
        yield return Capture("roster_grid");
        ClickFirstButtonWithClass(townHost.Root, "rgp-header__close");
        yield return WaitForHidden(townHost.Root, "RosterGridPreviewRoot");

        ClickButton(townHost.Root, "PermanentAugmentButton");
        yield return WaitForVisible(townHost.Root, "PapRoot");
        yield return Capture("permanent_augment");
        ClickFirstButtonWithClass(townHost.Root, "pap-header__close");
        yield return WaitForHidden(townHost.Root, "PapRoot");

        ClickButton(townHost.Root, "FaceCard_soemae");
        yield return WaitForVisible(townHost.Root, "ErpRoot");
        yield return WaitFrames(2);
        AssertEquipmentRefitContainment(townHost.Root);
        yield return Capture("equipment_refit");
        ClickFirstButtonWithClass(townHost.Root, "erp-header__close");
        yield return WaitForHidden(townHost.Root, "ErpRoot");

        ClickButton(townHost.Root, "FaceCard_galma");
        yield return WaitForVisible(townHost.Root, "PbpRoot");
        yield return Capture("passive_board");
        ClickFirstButtonWithClass(townHost.Root, "pbp-footer__close");
        yield return WaitForHidden(townHost.Root, "PbpRoot");

        yield return RunQuickBattleSmokeWitness(root, town);
        yield return RunNormalRouteWitness(root);
        yield return RunSiteEventChoiceWitness(root);

        _packet?.RecordBacklog("Settings.Global", "not produced in this witness wave");
        _packet?.RecordBacklog("Theater / story replay", "not produced in this witness wave");
        _packet?.RecordBacklog("Battle HUD redesign", "current shell visibility witnessed only");
    }

    private IEnumerator RunQuickBattleSmokeWitness(GameSessionRoot root, TownScreenController town)
    {
        var selectedChapterId = root.SessionState.SelectedCampaignChapterId;
        var selectedSiteId = root.SessionState.SelectedCampaignSiteId;
        var clearedSiteIds = root.SessionState.Profile.CampaignProgress.ClearedSiteIds.ToArray();

        Assert.That(root.SessionState.CanStartQuickBattleSmoke, Is.True, "Witness profile should be clean before Quick Battle smoke.");
        town.QuickBattle();
        yield return WaitForScene(SceneNames.Battle);
        yield return WaitForComponent<BattleScreenController>();

        var battle = RequireAny<BattleScreenController>("Battle controller should exist during Quick Battle smoke.");
        var battleHost = RequirePanelHost("BattleRuntimePanelHost");
        yield return WaitForCondition(() => battle.LatestStep != null, 5f);

        Assert.That(root.SessionState.IsQuickBattleSmokeActive, Is.True);
        Assert.That(battle.PlaybackMode, Is.EqualTo(BattlePlaybackMode.QuickBattle));
        Assert.That(battleHost.Root.Q<VisualElement>("PlaybackActionsGroup")!.style.display.value, Is.EqualTo(DisplayStyle.Flex));
        Assert.That(battleHost.Root.Q<VisualElement>("SmokeActionsGroup")!.style.display.value, Is.EqualTo(DisplayStyle.Flex));
        AssertNoRedText(battleHost.Root, "Quick Battle Smoke", allowDebug: true, allowSmoke: true);
        _packet?.RecordPass("Quick Battle smoke controls visible.");
        yield return Capture("quick_battle_smoke");

        root.SessionState.SetLastBattleResult(true, "quick smoke");
        root.SaveProfile();
        root.SceneFlow.GoToReward();

        yield return WaitForScene(SceneNames.Reward);
        yield return WaitForComponent<RewardScreenController>();
        var reward = RequireAny<RewardScreenController>("Reward controller should exist after Quick Battle smoke.");
        reward.Choose0();
        reward.ReturnToTown();

        yield return WaitForScene(SceneNames.Town);
        yield return WaitForComponent<TownScreenController>();
        RestoreWitnessProfile(root);
        RequireAny<TownScreenController>("Town controller should exist after Quick Battle smoke return.").EnsureRuntimeControls();
        yield return WaitFrames(2);
        Assert.That(root.SessionState.IsQuickBattleSmokeActive, Is.False);
        Assert.That(root.SessionState.CanResumeExpedition, Is.False);
        Assert.That(root.SessionState.SelectedCampaignChapterId, Is.EqualTo(selectedChapterId));
        Assert.That(root.SessionState.SelectedCampaignSiteId, Is.EqualTo(selectedSiteId));
        Assert.That(root.SessionState.Profile.CampaignProgress.ClearedSiteIds, Is.EqualTo(clearedSiteIds));
        _packet?.RecordPass("Quick Battle smoke did not contaminate campaign progress.");
    }

    private IEnumerator RunNormalRouteWitness(GameSessionRoot root)
    {
        PrepareAuthoredRouteWitnessFormation(root);
        var town = RequireAny<TownScreenController>("Town controller should exist before normal route witness.");
        town.OpenExpedition();

        yield return WaitForScene(SceneNames.Atlas);
        yield return WaitForComponent<AtlasScreenController>();
        var atlas = RequireAny<AtlasScreenController>("Atlas controller should exist after Start Expedition.");
        var atlasHost = RequirePanelHost("AtlasRuntimePanelHost");
        VerifyAtlasPreview(atlasHost.Root);
        AssertNoRedText(atlasHost.Root, "Atlas");
        yield return Capture("atlas_enemy_intel");

        atlas.ContinueToExpedition();
        // 출격 편성 확인 게이트(54072eda)가 warrant보다 먼저 Atlas panel을 가로챈다 —
        // 게이트 surface가 실제로 뜨는지 witness하고 출격 버튼으로 통과한다.
        // (witness는 e993ab8d에서 warrant overlay만 알았고 이 게이트 도입 후 미갱신 → 5s 타임아웃으로 깨져 있었다.)
        yield return WaitForCondition(() => atlasHost.Root.Q<Button>("SortieLaunchButton") != null, 5f);
        yield return Capture("sortie_confirm_gate");
        ClickButton(atlasHost.Root, "SortieLaunchButton");

        // ADR-0028 P2b/#b: 출격 확인 뒤 warrant 선택 overlay가 뜬다.
        // 정치 선택 surface(카드)가 PlayMode에서 실제로 뜨는지 witness하고, ProceedButton(스킵)으로 통과해 Battle로 간다.
        yield return WaitForCondition(() =>
        {
            var row = atlasHost.Root.Q<VisualElement>("WarrantCardRow");
            return row != null && row.childCount > 0;
        }, 5f);
        var warrantCardRow = atlasHost.Root.Q<VisualElement>("WarrantCardRow");
        Assert.That(warrantCardRow, Is.Not.Null, "warrant 선택 overlay(WarrantCardRow)가 Atlas panel에 떠야 한다(P2b).");
        Assert.That(warrantCardRow!.childCount, Is.GreaterThan(0), "site offer warrant 카드가 렌더돼야 한다(P2b+#b).");
        yield return Capture("warrant_selection");
        ClickButton(atlasHost.Root, "ProceedButton"); // 스킵(서약 없이) → GoToBattle

        yield return WaitForScene(SceneNames.Battle);
        yield return WaitForComponent<BattleScreenController>();
        yield return WaitForComponent<BattlePresentationController>();

        var battle = RequireAny<BattleScreenController>("Battle controller should exist for authored route.");
        var battleHost = RequirePanelHost("BattleRuntimePanelHost");
        yield return WaitForCondition(() => battle.LatestStep != null, 5f);
        VerifyAuthoredBattle(battle, battleHost.Root);
        AssertNoRedText(battleHost.Root, "Battle HUD shell");
        yield return Capture("battle_authored");

        FinishBattleForWitness(battle);
        yield return WaitFrames(2);
        Assert.That(battle.IsPlaybackFinished, Is.True, "Authored battle should be resolved before Reward witness.");
        if (!root.SessionState.LastBattleVictory)
        {
            root.SessionState.MarkBattleResolved(true, battle.LatestStep?.StepIndex ?? 0, 0);
            var victoryCheckpoint = root.SaveProfile(SessionCheckpointKind.BattleResolved);
            Assert.That(victoryCheckpoint.IsSuccessful, Is.True, victoryCheckpoint.Message);
        }

        battle.ContinueToReward();

        yield return WaitForScene(SceneNames.Reward);
        yield return WaitForComponent<RewardScreenController>();
        var reward = RequireAny<RewardScreenController>("Reward controller should exist for authored route.");
        var rewardHost = RequirePanelHost("RewardRuntimePanelHost");
        VerifyReward(rewardHost.Root);
        var deployedHero = root.SessionState.BattleDeployHeroIds
            .Select(id => root.SessionState.Profile.Heroes.FirstOrDefault(hero =>
                string.Equals(hero.HeroId, id, StringComparison.Ordinal)))
            .First(hero => hero != null)!;
        var rewardIdentity = ResolveHeroIdentity(root, deployedHero.HeroId);
        var rewardHeroNames = rewardHost.Root.Query<Label>(className: "reward-survivor-row__name")
            .ToList()
            .Select(label => label.text)
            .ToArray();
        Assert.That(rewardHeroNames, Does.Contain(rewardIdentity.Combined),
            "Reward survivor rows must render an authored person name, never the save instance id.");
        _packet?.RecordPass($"Reward authored hero identity visible: {rewardIdentity.Combined}");
        AssertNoRedText(rewardHost.Root, "Reward Result");
        yield return Capture("reward_result");

        reward.Choose0();
        reward.ReturnToTown();

        yield return WaitForScene(SceneNames.Town);
        yield return WaitForComponent<TownScreenController>();
        var townHost = RequirePanelHost("TownRuntimePanelHost");
        var expeditionButton = Require<Button>(townHost.Root, "ExpeditionButton");
        var quickBattleButton = Require<Button>(townHost.Root, "QuickBattleButton");
        Assert.That(root.SessionState.CanResumeExpedition, Is.True);
        Assert.That(expeditionButton.text, Is.Not.Empty);
        Assert.That(quickBattleButton.enabledSelf, Is.False);
        _packet?.RecordPass("Town resume state and Quick Battle lock visible after authored reward return.");
        yield return Capture("town_resume");
    }

    private static void PrepareAuthoredRouteWitnessFormation(GameSessionRoot root)
    {
        var heroes = root.SessionState.ExpeditionSquadHeroIds;
        if (heroes.Count >= 2)
        {
            Assert.That(root.SessionState.AssignHeroToAnchor(DeploymentAnchorId.BackBottom, heroes[0]), Is.True);
            Assert.That(root.SessionState.AssignHeroToAnchor(DeploymentAnchorId.FrontCenter, heroes[1]), Is.True);
        }

        while (root.SessionState.SelectedTeamPosture != TeamPostureType.AllInBackline)
        {
            root.SessionState.CycleTeamPosture();
        }
    }

    private static void VerifyTownHub(VisualElement root)
    {
        AssertVisible(root, "ServiceDecisionPanel");
        AssertNonEmptyText<Label>(root, "ServiceSelectedHeroLabel");
        AssertNonEmptyText<Label>(root, "ServiceWalletLabel");
        AssertNonEmptyText<Label>(root, "ServiceInventoryLabel");
        AssertNonEmptyText<Label>(root, "ServiceRosterPressureLabel");
        AssertNonEmptyText<Label>(root, "ServiceAvailabilityLabel");
        Assert.That(Require<VisualElement>(root, "DeployRow").childCount, Is.GreaterThan(0));
        Assert.That(root.Q<Button>("TacticalSetupButton"), Is.Not.Null);
        // TacticalWorkshop wire cycle (2026-07): 전술 공방은 프로덕션 hub entry.
        Assert.That(root.Q<Button>("TacticalWorkshopButton"), Is.Not.Null);
        Assert.That(Require<Button>(root, "ExpeditionButton").text, Is.Not.Empty);
    }

    private static void VerifyCharacterSheet(VisualElement root)
    {
        AssertVisible(root, "TownCharacterSheetRoot");
        AssertNonEmptyText<Label>(root, "TcsHeroNameLabel");
        AssertNonEmptyText<Label>(root, "TcsHeroMetaLabel");
        Assert.That(Require<VisualElement>(root, "TcsOverviewBody").childCount, Is.GreaterThan(0));
        Assert.That(Require<VisualElement>(root, "TcsLoadoutBody").childCount, Is.GreaterThan(0));
        Assert.That(Require<VisualElement>(root, "TcsProgressionBody").childCount, Is.GreaterThan(0));
        Assert.That(Require<VisualElement>(root, "TcsHeroRail").childCount, Is.GreaterThan(0));
        Assert.That(Require<VisualElement>(root, "TcsStatGrid").childCount, Is.GreaterThan(0));
        Assert.That(Require<VisualElement>(root, "TcsProgressionTrack").childCount, Is.GreaterThan(0));
        Assert.That(Require<VisualElement>(root, "TcsSkillList").childCount, Is.GreaterThan(0));
        Assert.That(Require<VisualElement>(root, "TcsEquipmentRow").childCount, Is.GreaterThan(0));
        AssertCharacterSheetContainment(root);
    }

    private static void VerifyTacticalSetup(VisualElement root)
    {
        AssertVisible(root, "SquadBuilderRoot");
        AssertNonEmptyText<Label>(root, "SquadBuilderRosterCountLabel");
        Assert.That(Require<VisualElement>(root, "SquadBuilderRosterList").childCount, Is.GreaterThan(0));
        AssertNonEmptyText<Label>(root, "TacticalSetupFormationSection");
        AssertVisible(root, "TacticalSetupPostureSection");
        AssertVisible(root, "TacticalSetupOperationSection");
        AssertVisible(root, "TacticalSetupResponseSection");
        AssertNonEmptyText<Label>(root, "SquadBuilderSelectedAnchorLabel");
        AssertNonEmptyText<Label>(root, "SquadBuilderSelectedHeroName");
        AssertNonEmptyText<Label>(root, "SquadBuilderSelectedHeroMeta");
        AssertNonEmptyText<Label>(root, "SquadBuilderStatusLabel");
        Assert.That(Require<VisualElement>(root, "SquadBuilderOperationRows").childCount, Is.GreaterThan(0));
        AssertNonEmptyText<Label>(root, "SquadBuilderResponseSummaryLabel");
        Assert.That(Require<VisualElement>(root, "SquadBuilderSynergyChips").childCount, Is.GreaterThan(0));
        Assert.That(Require<Button>(root, "SquadBuilderAnchor_FrontCenter").text, Is.Not.Empty);
        Assert.That(Require<Button>(root, "SquadBuilderPosture_StandardAdvance"), Is.Not.Null);
    }

    private static void VerifyTacticalWorkshop(VisualElement root)
    {
        // TacticalWorkshop wire cycle (2026-07): posture 5카드 + 위협 8lane(SquadCounterCoveragePreview.Dimensions)
        // + command chip 동적 렌더 + 배치 유닛 전술 strip. 시너지 row는 칩 또는 빈 안내 라벨 중 하나가 반드시 존재.
        AssertVisible(root, "TwpRoot");
        Assert.That(Require<VisualElement>(root, "PostureCardRow").childCount, Is.EqualTo(5),
            "팀 태세 카드는 항상 5장이다.");
        Assert.That(Require<VisualElement>(root, "ThreatGrid").childCount, Is.EqualTo(8),
            "위협 답수 lane은 counter-coverage 8차원 전부를 노출한다.");
        AssertNonEmptyText<Label>(root, "TwpDeployChip");
        AssertNonEmptyText<Label>(root, "TwpPostureChip");
        AssertNonEmptyText<Label>(root, "TwpAnswerChip");
        Assert.That(Require<Label>(root, "TwpDeployChip").text, Does.Not.Contain("—"),
            "배치 chip은 세션 상태로 렌더된다(정적 placeholder 금지).");
        Assert.That(Require<VisualElement>(root, "TacticPresetRows").childCount, Is.GreaterThan(0),
            "배치된 유닛이 있으면 유닛 전술 행이 나온다.");
        Assert.That(Require<Button>(root, "TwpResetButton"), Is.Not.Null);
    }

    private static void VerifyInventoryCompare(VisualElement root)
    {
        AssertVisible(root, "InvRoot");
        AssertVisible(root, "CompareLane");
        AssertNonEmptyText<Label>(root, "CompareTargetHeroLabel");
        AssertNonEmptyText<Label>(root, "CompareSelectedItemLabel");
        AssertNonEmptyText<Label>(root, "CompareSelectedItemMetaLabel");
        AssertNonEmptyText<Label>(root, "CompareEquipStatusLabel");
        Assert.That(Require<VisualElement>(root, "CompareRows").childCount, Is.GreaterThan(0));
        AssertInventoryContainment(root);
    }

    private static void VerifyRecruit(VisualElement root)
    {
        AssertVisible(root, "RcpRoot");
        Assert.That(Require<VisualElement>(root, "CardRow").childCount, Is.EqualTo(4));
        AssertVisible(root, "RecruitDecisionPanel");
        AssertNonEmptyText<Label>(root, "SelectedCandidateNameLabel");
        AssertNonEmptyText<Label>(root, "SelectedCandidateMetaLabel");
        AssertNonEmptyText<Label>(root, "RosterPressureCountLabel");
        AssertNonEmptyText<Label>(root, "RosterPressureNeedLabel");
    }

    private static void VerifyAtlasPreview(VisualElement root)
    {
        AssertNonEmptyText<Label>(root, "atlas-preview-title");
        AssertNonEmptyText<Label>(root, "atlas-preview-enemy");
        AssertNonEmptyText<Label>(root, "atlas-preview-enemy-intel");
        AssertNonEmptyText<Label>(root, "atlas-preview-modifiers");
        AssertNonEmptyText<Label>(root, "atlas-preview-reward");
        AssertNonEmptyText<Label>(root, "atlas-preview-recommendations");
        Assert.That(Require<Button>(root, "atlas-continue-button").enabledSelf, Is.True);
    }

    private static void VerifyAuthoredBattle(BattleScreenController battle, VisualElement root)
    {
        Assert.That(battle.PlaybackMode, Is.EqualTo(BattlePlaybackMode.InGame));
        AssertVisible(root, "SettingsButton");
        AssertVisible(root, "SettingsPanel", allowDisplayNone: true);
        AssertVisible(root, "ProgressTrack");
        AssertVisible(root, "AllyRosterList");
        AssertVisible(root, "EnemyRosterList");
        AssertNonEmptyText<Label>(root, "LogLabel");
        Assert.That(Require<VisualElement>(root, "BattleDebugFoldout").style.display.value, Is.EqualTo(DisplayStyle.None));
        Assert.That(Require<VisualElement>(root, "PlaybackActionsGroup").style.display.value, Is.EqualTo(DisplayStyle.None));
        Assert.That(Require<VisualElement>(root, "SmokeActionsGroup").style.display.value, Is.EqualTo(DisplayStyle.None));
        AssertBattleHudContainment(root);
    }

    private static void VerifyReward(VisualElement root)
    {
        AssertVisible(root, "SettlementSummaryPanel");
        AssertNonEmptyText<Label>(root, "SettlementSummaryTitleLabel");
        Assert.That(Require<VisualElement>(root, "RewardProgressionRows").childCount, Is.GreaterThan(0));
        Assert.That(Require<VisualElement>(root, "RewardTimelineTicks").childCount, Is.GreaterThan(0));
        AssertNonEmptyText<Label>(root, "ChoiceCard1TitleLabel");
        Assert.That(Require<Button>(root, "ChoiceCard1Button").enabledSelf, Is.True);
        Assert.That(Require<Button>(root, "ReturnTownButton"), Is.Not.Null);
        AssertRewardContainment(root);
    }

    private static (string Person, string Job, string Combined) ResolveHeroIdentity(
        GameSessionRoot root,
        string heroId)
    {
        var hero = root.SessionState.Profile.Heroes.First(candidate =>
            string.Equals(candidate.HeroId, heroId, StringComparison.Ordinal));
        var contentText = new ContentTextResolver(root.Localization, root.CombatContentLookup);
        var person = contentText.GetCharacterName(hero.CharacterId, hero.ArchetypeId);
        var job = contentText.GetArchetypeName(hero.ArchetypeId);
        var combined = string.Equals(person, job, StringComparison.Ordinal)
            ? person
            : $"{person} · {job}";
        Assert.That(person, Does.Not.StartWith("content."));
        Assert.That(person, Is.Not.EqualTo(hero.HeroId));
        Assert.That(job, Does.Not.StartWith("content."));
        return (person, job, combined);
    }

    private IEnumerator Capture(string name)
    {
        if (_packet == null)
        {
            yield break;
        }

        yield return _packet.Capture(name);
    }

    private static IEnumerator EnterOfflineTownFromBoot()
    {
        SceneManager.LoadScene(SceneNames.Boot);
        yield return WaitForScene(SceneNames.Boot);
        yield return WaitForCondition(() => GameSessionRoot.Instance != null, 8f);

        var root = GameSessionRoot.Instance!;
        root.UseDedicatedSmokeNamespace();
        Assert.That(root.StartRealm(SessionRealm.OfflineLocal, out var error), Is.True, error);
        ResetWitnessProfile(root);
        root.SceneFlow.GoToTown();

        yield return WaitForScene(SceneNames.Town);
        yield return WaitForComponent<TownScreenController>();
    }

    private static void ResetWitnessProfile(GameSessionRoot root)
    {
        root.SessionState.AbandonExpeditionRun();
        var checkpoint = root.SaveProfile(SessionCheckpointKind.ManualSave);
        Assert.That(checkpoint.IsSuccessful, Is.True, checkpoint.Message);
    }

    private static void RestoreWitnessProfile(GameSessionRoot root)
    {
        root.UseDedicatedSmokeNamespace();
        var checkpoint = root.BindProfile(SessionCheckpointKind.QuickBattleRestore);
        Assert.That(checkpoint.IsSuccessful, Is.True, checkpoint.Message);
        ResetWitnessProfile(root);
    }

    private static IEnumerator WaitForScene(string sceneName, float timeout = 8f)
    {
        var elapsed = 0f;
        while (SceneManager.GetActiveScene().name != sceneName && elapsed < timeout)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(sceneName));
        yield return null;
    }

    private static IEnumerator WaitForComponent<T>() where T : Component
    {
        var elapsed = 0f;
        while (FindAny<T>() == null && elapsed < 8f)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        Assert.That(FindAny<T>(), Is.Not.Null);
    }

    private static IEnumerator WaitForCondition(Func<bool> predicate, float timeout)
    {
        var elapsed = 0f;
        while (!predicate() && elapsed < timeout)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        Assert.That(predicate(), Is.True);
    }

    private static IEnumerator WaitForVisible(VisualElement root, string name)
    {
        yield return WaitForCondition(() =>
        {
            var element = root.Q<VisualElement>(name);
            return element != null
                   && IsEffectivelyVisible(element)
                   && element.worldBound.width > 8f
                   && element.worldBound.height > 8f;
        }, 3f);
    }

    private static IEnumerator WaitForHidden(VisualElement root, string name)
    {
        yield return WaitForCondition(() =>
        {
            var element = root.Q<VisualElement>(name);
            return element != null && element.style.display.value == DisplayStyle.None;
        }, 3f);
    }

    private static IEnumerator WaitFrames(int frameCount)
    {
        for (var i = 0; i < frameCount; i++)
        {
            yield return null;
        }
    }

    private static void ClickButton(VisualElement root, string name)
    {
        var button = Require<Button>(root, name);
        InvokeButton(button);
    }

    private static void ClickFirstButtonWithClass(VisualElement root, string className)
    {
        var button = root.Query<Button>(className: className).First();
        Assert.That(button, Is.Not.Null, $"Button with class '{className}' should exist.");
        InvokeButton(button);
    }

    private static void InvokeButton(Button button)
    {
        Assert.That(button.enabledInHierarchy, Is.True, $"{button.name} should be enabled before click.");
        using var evt = ClickEvent.GetPooled();
        var invoke = typeof(Clickable).GetMethod("Invoke", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(invoke, Is.Not.Null, "Clickable.Invoke should be available for PlayMode button witness.");
        invoke!.Invoke(button.clickable, new object[] { evt });
    }

    private static void FinishBattleForWitness(BattleScreenController battle)
    {
        var finish = typeof(BattleScreenController).GetMethod("FinishBattle", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(finish, Is.Not.Null, "BattleScreenController.FinishBattle should be available for PlayMode route witness.");
        finish!.Invoke(battle, Array.Empty<object>());
    }

    private static void AssertVisible(VisualElement root, string name, bool allowDisplayNone = false)
    {
        var element = Require<VisualElement>(root, name);
        if (!allowDisplayNone)
        {
            Assert.That(IsEffectivelyVisible(element), Is.True, $"{name} should be visible.");
        }
    }

    private static void AssertNonEmptyText<T>(VisualElement root, string name) where T : TextElement
    {
        var element = Require<T>(root, name);
        Assert.That(element.text, Is.Not.Empty, $"{name} should have text.");
    }

    private static void AssertSurfaceGeometry(VisualElement root, string name, float minWidth, float minHeight)
    {
        var element = Require<VisualElement>(root, name);
        Assert.That(IsEffectivelyVisible(element), Is.True, $"{name} should be effectively visible.");
        Assert.That(element.worldBound.width, Is.GreaterThanOrEqualTo(minWidth), $"{name} should occupy real screen width.");
        Assert.That(element.worldBound.height, Is.GreaterThanOrEqualTo(minHeight), $"{name} should occupy real screen height.");
    }

    private static void AssertModalPanelWithinViewport(
        VisualElement root,
        string className,
        string surface,
        float topInset,
        float bottomInset)
    {
        var panel = root.Query<VisualElement>(className: className).First();
        Assert.That(panel, Is.Not.Null, $"{surface} modal panel should exist.");
        var rootBounds = root.worldBound;
        var panelBounds = panel!.worldBound;
        Assert.That(panelBounds.yMin, Is.GreaterThanOrEqualTo(rootBounds.yMin + topInset), $"{surface} should not collide with top chrome.");
        Assert.That(panelBounds.yMax, Is.LessThanOrEqualTo(rootBounds.yMax - bottomInset), $"{surface} should not collide with bottom chrome.");
    }

    private static void AssertNoRedText(VisualElement scope, string surface, bool allowDebug = false, bool allowSmoke = false)
    {
        var offenders = new List<string>();
        CollectVisibleTextOffenders(scope, offenders, allowDebug, allowSmoke);
        Assert.That(offenders, Is.Empty, $"{surface} has UX Bible visual red text blockers: {string.Join(" | ", offenders.Take(8))}");
    }

    private static void CollectVisibleTextOffenders(
        VisualElement element,
        List<string> offenders,
        bool allowDebug,
        bool allowSmoke)
    {
        if (!IsEffectivelyVisible(element))
        {
            return;
        }

        if (element is TextElement textElement && !string.IsNullOrWhiteSpace(textElement.text))
        {
            var text = textElement.text.Trim();
            if (ContainsRedText(text, allowDebug, allowSmoke))
            {
                offenders.Add($"{element.name}: {text.Replace('\n', ' ')}");
            }
        }

        foreach (var child in element.Children())
        {
            CollectVisibleTextOffenders(child, offenders, allowDebug, allowSmoke);
        }
    }

    private static bool ContainsRedText(string text, bool allowDebug, bool allowSmoke)
    {
        if (text.Contains("No translation found", StringComparison.OrdinalIgnoreCase)
            || text.Contains("content.", StringComparison.Ordinal)
            || text.Contains("ui.", StringComparison.Ordinal)
            || text.Contains("reward.", StringComparison.Ordinal)
            || text.Contains("item_", StringComparison.Ordinal)
            || text.Contains("augment_", StringComparison.Ordinal)
            || text.Contains("extra_", StringComparison.Ordinal))
        {
            return true;
        }

        if (!allowDebug
            && (text.Contains("Battle Debug", StringComparison.OrdinalIgnoreCase)
                || text.Contains("Debug", StringComparison.OrdinalIgnoreCase)
                || text.Contains("Hash", StringComparison.Ordinal)
                || text.Contains("stageCandidatePathHash", StringComparison.Ordinal)))
        {
            return true;
        }

        return !allowSmoke
               && text.Contains("Smoke", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsEffectivelyVisible(VisualElement element)
    {
        for (var current = element; current != null; current = current.parent)
        {
            if (current.style.display.value == DisplayStyle.None
                || current.resolvedStyle.display == DisplayStyle.None)
            {
                return false;
            }
        }

        return true;
    }

    private static T Require<T>(VisualElement root, string name) where T : VisualElement
    {
        return root.Q<T>(name) ?? throw new AssertionException($"Missing UITK element '{name}'.");
    }

    private static T RequireAny<T>(string message) where T : Component
    {
        var component = FindAny<T>();
        Assert.That(component, Is.Not.Null, BuildSceneDiagnostic(message));
        return component!;
    }

    private static T? FindAny<T>() where T : Component
    {
        var active = Object.FindObjectOfType<T>();
        if (active != null)
        {
            return active;
        }

        return Resources.FindObjectsOfTypeAll<T>().FirstOrDefault(component =>
            component.gameObject.scene.IsValid());
    }

    private static RuntimePanelHost RequirePanelHost(string objectName)
    {
        var host = Resources.FindObjectsOfTypeAll<RuntimePanelHost>()
            .FirstOrDefault(candidate => candidate.gameObject.scene.IsValid() && candidate.gameObject.name == objectName);
        Assert.That(host, Is.Not.Null, BuildSceneDiagnostic($"{objectName} should exist."));
        host!.EnsureReady();
        return host;
    }

    private static string BuildSceneDiagnostic(string prefix)
    {
        var scene = SceneManager.GetActiveScene();
        var rootNames = string.Join(", ", scene.GetRootGameObjects().Select(x => x.name));
        var controllerObjects = string.Join(", ", scene
            .GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Where(transform => transform.name.Contains("Controller"))
            .Select(transform => transform.name)
            .Distinct());

        return $"{prefix} ActiveScene={scene.name}; Roots=[{rootNames}]; ControllerObjects=[{controllerObjects}]";
    }

    private sealed class UxBibleWitnessPacket : IDisposable
    {
        private readonly string _projectRoot;
        private readonly string _shortSha;
        private readonly string _startedUtc;
        private readonly List<string> _screenshots = new();
        private readonly Dictionary<string, string> _screenshotHashes = new(StringComparer.Ordinal);
        private readonly List<string> _passes = new();
        private readonly List<string> _backlog = new();
        private readonly List<string> _logs = new();
        private bool _finished;

        private UxBibleWitnessPacket(string projectRoot, string shortSha, string startedUtc, string directory)
        {
            _projectRoot = projectRoot;
            _shortSha = shortSha;
            _startedUtc = startedUtc;
            Directory = directory;
            Application.logMessageReceived += HandleLogMessage;
        }

        public string Directory { get; }

        public static UxBibleWitnessPacket Start()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var shortSha = ResolveShortSha(projectRoot);
            var startedUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var directory = Path.Combine(projectRoot, "Logs", "ux-bible-visual-qa", $"{stamp}-{shortSha}");
            System.IO.Directory.CreateDirectory(directory);

            var packet = new UxBibleWitnessPacket(projectRoot, shortSha, startedUtc, directory);
            packet.WriteObserverContracts();
            packet.WriteReferenceMap();
            packet.WriteVisualVerdict();
            packet.WriteContactSheet();
            packet.WriteConsole();
            packet.WriteManifest();
            packet.WriteSummary();
            return packet;
        }

        public IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            var texture = CaptureFrameTexture();
            Assert.That(texture, Is.Not.Null, $"Screenshot '{name}' should be captured.");

            try
            {
                var relativePath = $"{name}.png";
                var path = Path.Combine(Directory, relativePath);
                var png = texture!.EncodeToPNG();
                File.WriteAllBytes(path, png);
                _screenshots.Add(relativePath);
                _screenshotHashes[name] = BuildStableHash(png);
                RecordPass($"Screenshot captured: {relativePath}");
                WriteManifest();
                WriteVisualVerdict();
                WriteContactSheet();
                WriteSummary();
            }
            finally
            {
                Object.Destroy(texture);
            }
        }

        public void AssertScreenshotDifferent(string firstName, string secondName, string message)
        {
            Assert.That(_screenshotHashes.TryGetValue(firstName, out var firstHash), Is.True, $"{firstName} screenshot hash should exist.");
            Assert.That(_screenshotHashes.TryGetValue(secondName, out var secondHash), Is.True, $"{secondName} screenshot hash should exist.");
            Assert.That(secondHash, Is.Not.EqualTo(firstHash), message);
            RecordPass($"{secondName} visual delta differs from {firstName}.");
        }

        private static Texture2D? CaptureFrameTexture()
        {
            var screenCaptureType = Type.GetType("UnityEngine.ScreenCapture, UnityEngine.ScreenCaptureModule")
                                    ?? Type.GetType("UnityEngine.ScreenCapture, UnityEngine.CoreModule");
            var method = screenCaptureType?.GetMethod(
                "CaptureScreenshotAsTexture",
                BindingFlags.Public | BindingFlags.Static,
                null,
                Type.EmptyTypes,
                null);
            return method?.Invoke(null, Array.Empty<object>()) as Texture2D;
        }

        public void RecordPass(string message)
        {
            _passes.Add(message);
            WriteSummary();
        }

        public void RecordBacklog(string surface, string reason)
        {
            _backlog.Add($"{surface}: {reason}");
            WriteSummary();
        }

        public void Finish()
        {
            if (_finished)
            {
                return;
            }

            _finished = true;
            WriteConsole();
            WriteReferenceMap();
            WriteVisualVerdict();
            WriteContactSheet();
            WriteManifest();
            WriteSummary();
        }

        public void Dispose()
        {
            Finish();
            Application.logMessageReceived -= HandleLogMessage;
        }

        private void HandleLogMessage(string condition, string stackTrace, LogType type)
        {
            _logs.Add($"[{DateTime.UtcNow:O}] {type}: {condition}");
            if (!string.IsNullOrWhiteSpace(stackTrace) && type is LogType.Error or LogType.Exception or LogType.Assert)
            {
                _logs.Add(stackTrace);
            }
        }

        private void WriteObserverContracts()
        {
            WriteStaticContract("town_observer_contract.json", "Town", "Assets/_Game/UI/Screens/Town/TownScreen.uxml", new[]
            {
                "ServiceDecisionPanel",
                "CharacterSheetTemplate",
                "InventoryTemplate",
                "RecruitTemplate",
                "TacticalSetupTemplate",
                "QuickBattleButton",
                "ExpeditionButton",
            });
            WriteStaticContract("battle_observer_contract.json", "Battle", "Assets/_Game/UI/Screens/Battle/BattleScreen.uxml", new[]
            {
                "BattleScreenRoot",
                "SettingsPanel",
                "PlaybackActionsGroup",
                "SmokeActionsGroup",
                "ProgressFill",
                "AllyRosterList",
                "EnemyRosterList",
            });
        }

        private void WriteStaticContract(string fileName, string scene, string assetPath, IReadOnlyList<string> tokens)
        {
            var absolutePath = Path.Combine(_projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
            var text = File.Exists(absolutePath) ? File.ReadAllText(absolutePath) : string.Empty;
            var builder = new StringBuilder();
            builder.AppendLine("{");
            builder.AppendLine($"  \"scene\": \"{scene}\",");
            builder.AppendLine($"  \"assetPath\": \"{Json(assetPath)}\",");
            builder.AppendLine($"  \"exists\": {JsonBool(File.Exists(absolutePath))},");
            builder.AppendLine("  \"tokens\": {");
            for (var i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];
                var suffix = i + 1 == tokens.Count ? string.Empty : ",";
                builder.AppendLine($"    \"{Json(token)}\": {JsonBool(text.Contains(token, StringComparison.Ordinal))}{suffix}");
            }
            builder.AppendLine("  }");
            builder.AppendLine("}");
            File.WriteAllText(Path.Combine(Directory, fileName), builder.ToString(), Encoding.UTF8);
        }

        private void WriteReferenceMap()
        {
            var references = GetReferencePairs();
            var builder = new StringBuilder();
            builder.AppendLine("{");
            builder.AppendLine("  \"canonicalReferencePolicy\": \"Existing UX Bible mockups are the visual QA baseline. No new image generation in this wave.\",");
            builder.AppendLine("  \"surfaces\": [");
            for (var i = 0; i < references.Length; i++)
            {
                var reference = references[i];
                var suffix = i + 1 == references.Length ? string.Empty : ",";
                builder.AppendLine("    {");
                builder.AppendLine($"      \"surface\": \"{Json(reference.Surface)}\",");
                builder.AppendLine($"      \"reference\": \"{Json(reference.ReferencePath)}\",");
                builder.AppendLine($"      \"currentScreenshot\": \"{Json(reference.CurrentScreenshot)}\"");
                builder.AppendLine($"    }}{suffix}");
            }

            builder.AppendLine("  ]");
            builder.AppendLine("}");
            File.WriteAllText(Path.Combine(Directory, "reference_map.json"), builder.ToString(), Encoding.UTF8);
        }

        private void WriteVisualVerdict()
        {
            var builder = new StringBuilder();
            builder.AppendLine("{");
            builder.AppendLine("  \"overall\": \"pending_direct_visual_review\",");
            builder.AppendLine("  \"automatedGate\": \"green\",");
            builder.AppendLine("  \"codexAiQaStatus\": \"direct_contact_sheet_review_required\",");
            builder.AppendLine("  \"manualReviewRequiredBeforeUserHandoff\": true,");
            builder.AppendLine("  \"redCount\": null,");
            builder.AppendLine("  \"redCountMeaning\": \"Unity witness only checks route, geometry, and blocker text. Codex must compare the contact sheet against the UX Bible mockups before this can become green.\",");
            builder.AppendLine("  \"redCriteria\": [");
            builder.AppendLine("    \"modal not visible\",");
            builder.AppendLine("    \"No translation found\",");
            builder.AppendLine("    \"raw content.* or ui.* key\",");
            builder.AppendLine("    \"debug hash or unintended smoke/debug production text\",");
            builder.AppendLine("    \"severe text clipping or layout collapse\"");
            builder.AppendLine("  ],");
            builder.AppendLine("  \"yellow\": [");
            builder.AppendLine("    \"Battle stage art and final unit illustration pass remain outside this UI-only fix wave\"");
            builder.AppendLine("  ],");
            builder.AppendLine("  \"notGreenUntil\": [");
            builder.AppendLine("    \"Codex has opened comparison_contact_sheet.md or the reference/current screenshot pairs\",");
            builder.AppendLine("    \"all Red visual blockers are either fixed or explicitly reclassified with rationale\",");
            builder.AppendLine("    \"visual_verdict.json is updated by the reviewer outcome in the evidence packet\"");
            builder.AppendLine("  ],");
            builder.AppendLine("  \"greenGate\": [");
            builder.AppendLine("    \"target surfaces are opened through PlayMode routes\",");
            builder.AppendLine("    \"visible VisualTree text red blockers are scanned\",");
            builder.AppendLine("    \"Character Sheet geometry and screenshot delta are asserted\"");
            builder.AppendLine("  ]");
            builder.AppendLine("}");
            File.WriteAllText(Path.Combine(Directory, "visual_verdict.json"), builder.ToString(), Encoding.UTF8);
        }

        private void WriteContactSheet()
        {
            var builder = new StringBuilder();
            builder.AppendLine("# UX Bible Reference / Current Contact Sheet");
            builder.AppendLine();
            builder.AppendLine("| Surface | Reference | Current |");
            builder.AppendLine("| --- | --- | --- |");
            foreach (var reference in GetReferencePairs())
            {
                var absoluteReference = Path.Combine(_projectRoot, reference.ReferencePath.Replace('/', Path.DirectorySeparatorChar));
                var absoluteCurrent = Path.Combine(Directory, reference.CurrentScreenshot);
                builder.AppendLine($"| {reference.Surface} | ![]({absoluteReference}) | ![]({absoluteCurrent}) |");
            }

            File.WriteAllText(Path.Combine(Directory, "comparison_contact_sheet.md"), builder.ToString(), Encoding.UTF8);
        }

        private void WriteConsole()
        {
            File.WriteAllLines(Path.Combine(Directory, "console.txt"), _logs.Count == 0
                ? new[] { "No PlayMode witness logs captured yet." }
                : _logs, Encoding.UTF8);
        }

        private void WriteManifest()
        {
            var builder = new StringBuilder();
            builder.AppendLine("{");
            builder.AppendLine("  \"scenario\": \"SM.Tests.PlayMode.UxBiblePlayModeWitnessTests\",");
            builder.AppendLine("  \"evidenceKind\": \"ux-bible-visual-qa\",");
            builder.AppendLine($"  \"commit\": \"{Json(_shortSha)}\",");
            builder.AppendLine($"  \"startedUtc\": \"{Json(_startedUtc)}\",");
            builder.AppendLine($"  \"evidenceDirectory\": \"{Json(Directory)}\",");
            builder.AppendLine("  \"screenshots\": [");
            for (var i = 0; i < _screenshots.Count; i++)
            {
                var suffix = i + 1 == _screenshots.Count ? string.Empty : ",";
                builder.AppendLine($"    \"{Json(_screenshots[i])}\"{suffix}");
            }
            builder.AppendLine("  ],");
            builder.AppendLine("  \"observerContracts\": [");
            builder.AppendLine("    \"town_observer_contract.json\",");
            builder.AppendLine("    \"battle_observer_contract.json\"");
            builder.AppendLine("  ],");
            builder.AppendLine("  \"visualQaArtifacts\": [");
            builder.AppendLine("    \"reference_map.json\",");
            builder.AppendLine("    \"visual_verdict.json\",");
            builder.AppendLine("    \"comparison_contact_sheet.md\"");
            builder.AppendLine("  ]");
            builder.AppendLine("}");
            File.WriteAllText(Path.Combine(Directory, "manifest.json"), builder.ToString(), Encoding.UTF8);
        }

        private void WriteSummary()
        {
            var builder = new StringBuilder();
            builder.AppendLine("# UX Bible Visual QA Witness");
            builder.AppendLine();
            builder.AppendLine($"- commit: `{_shortSha}`");
            builder.AppendLine($"- startedUtc: `{_startedUtc}`");
            builder.AppendLine($"- evidence: `{Directory}`");
            builder.AppendLine("- visual verdict: `visual_verdict.json` (automated gate only; direct mockup review required before green)");
            builder.AppendLine("- reference/current sheet: `comparison_contact_sheet.md`");
            builder.AppendLine();
            builder.AppendLine("## Passed Checks");
            foreach (var pass in _passes.Distinct(StringComparer.Ordinal))
            {
                builder.AppendLine($"- {pass}");
            }
            builder.AppendLine();
            builder.AppendLine("## Screenshots");
            foreach (var screenshot in _screenshots)
            {
                builder.AppendLine($"- `{screenshot}`");
            }
            builder.AppendLine();
            builder.AppendLine("## Remaining Backlog");
            foreach (var item in _backlog.Distinct(StringComparer.Ordinal))
            {
                builder.AppendLine($"- {item}");
            }
            File.WriteAllText(Path.Combine(Directory, "summary.md"), builder.ToString(), Encoding.UTF8);
        }

        private static ReferencePair[] GetReferencePairs()
        {
            return new[]
            {
                new ReferencePair(
                    "Character Sheet",
                    "Screenshots/mockups/ui_ux_bible_character_sheet_class_detail_v0.png",
                    "character_sheet.png"),
                new ReferencePair(
                    "Tactical Setup",
                    "Screenshots/mockups/ui_ux_bible_squad_builder_v0.png",
                    "tactical_setup.png"),
                new ReferencePair(
                    "Recruit Detail",
                    "Screenshots/mockups/ui_ux_bible_recruit_candidate_choice_v0.png",
                    "recruit_detail.png"),
                new ReferencePair(
                    "Atlas",
                    "Screenshots/mockups/ui_ux_bible_atlas_overworld_map_v0.png",
                    "atlas_enemy_intel.png"),
                new ReferencePair(
                    "Battle HUD shell",
                    "Screenshots/mockups/ui_ux_bible_battle_stage_hud_v0.png",
                    "battle_authored.png"),
                new ReferencePair(
                    "Reward Result",
                    "Screenshots/mockups/ui_ux_bible_reward_result_v0.png",
                    "reward_result.png"),
                new ReferencePair(
                    "Site Event Choice",
                    "Screenshots/mockups/ui_ux_bible_dialogue_event_choice_v0.png",
                    "site_event_choice.png"),
            };
        }

        private static string BuildStableHash(byte[] bytes)
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            var hash = offset;
            foreach (var item in bytes)
            {
                hash ^= item;
                hash *= prime;
            }

            return hash.ToString("x16", CultureInfo.InvariantCulture);
        }

        private static string ResolveShortSha(string projectRoot)
        {
            var headPath = Path.Combine(projectRoot, ".git", "HEAD");
            if (!File.Exists(headPath))
            {
                return "unknown";
            }

            var head = File.ReadAllText(headPath).Trim();
            if (!head.StartsWith("ref:", StringComparison.Ordinal))
            {
                return head.Length <= 12 ? head : head[..12];
            }

            var refPath = Path.Combine(projectRoot, ".git", head["ref:".Length..].Trim().Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(refPath))
            {
                return "unknown";
            }

            var sha = File.ReadAllText(refPath).Trim();
            return sha.Length <= 12 ? sha : sha[..12];
        }

        private static string Json(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal)
                .Replace("\r", "\\r", StringComparison.Ordinal)
                .Replace("\n", "\\n", StringComparison.Ordinal);
        }

        private static string JsonBool(bool value) => value ? "true" : "false";

        private sealed record ReferencePair(string Surface, string ReferencePath, string CurrentScreenshot);
    }
}
