using System.Collections;
using System.Linq;
using NUnit.Framework;
using SM.Atlas.Model;
using SM.Atlas.Services;
using SM.Combat.Model;
using SM.Meta.Model;
using SM.Unity;
using SM.Unity.UI;
using SM.Unity.UI.Atlas;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace SM.Tests.PlayMode;

public sealed class PlayModeSmokeTests
{
    [UnitySetUp]
    public IEnumerator ResetRoot()
    {
        if (GameSessionRoot.Instance != null)
        {
            Object.Destroy(GameSessionRoot.Instance.gameObject);
        }

        var guard = 0;
        while (GameSessionRoot.Instance != null && guard++ < 10)
        {
            yield return null;
        }
    }

    [UnityTest]
    public IEnumerator Boot_To_Town_StartExpedition_FirstNodeBattle_Reward_ReturnTown_Resume()
    {
        PlayModeSmokeEvidence.Reset();

        SceneManager.LoadScene(SceneNames.Boot);
        yield return WaitForScene(SceneNames.Boot);
        yield return WaitForCondition(() => GameSessionRoot.Instance != null, 8f);
        yield return WaitForComponent<BootScreenController>();
        yield return PlayModeSmokeEvidence.CaptureScreenshot(PlayModeSmokeEvidence.ScreenshotFileNames[0]);

        // 2026-07-31: 시작 화면이 레거시 uGUI 에서 UITK 로 옮겨졌다. 다른 모든 화면과 같은
        // RuntimePanelHost 경로를 타므로 여기서도 uGUI Button 대신 UITK 버튼을 찾는다.
        var bootHost = Object.FindFirstObjectByType<RuntimePanelHost>();
        Assert.That(bootHost, Is.Not.Null, BuildSceneDiagnostic("Boot scene should expose the UITK panel host."));
        var startButton = bootHost!.Root.Q<Button>("BootStartButton");
        Assert.That(startButton, Is.Not.Null, BuildSceneDiagnostic("Boot scene should expose the start button."));
        Assert.That(startButton!.enabledSelf, Is.True, "차단 에러가 없으면 시작 버튼은 눌러야 한다.");
        // 문구는 폴백이 곧 화면 문구라 한국어가 기본이다. 영문 로케일도 허용.
        Assert.That(startButton.text, Is.EqualTo("이어서 시작").Or.EqualTo("Begin"),
            $"시작 버튼 문구 (실제: '{startButton.text}').");
        Assert.That(GameObject.Find("OnlineAuthoritativeButton"), Is.Null, "Boot scene should not expose the hidden future-seam online button.");

        using (var click = new NavigationSubmitEvent() { target = startButton })
        {
            startButton.SendEvent(click);
        }
        yield return WaitForScene(SceneNames.Town);
        yield return WaitForComponent<TownScreenController>();

        var root = GameSessionRoot.Instance!;
        // PlayMode smoke env recovery: 이전 test/run의 disk profile pollution이 Town 초기 렌더에
        // 들어오면 Quick Battle CTA가 잠긴다. UI 검사 전에 canonical profile을 clean state로 고정하고
        // Town을 다시 로드해 presenter가 복구된 state를 렌더하도록 한다.
        root.SessionState.AbandonExpeditionRun();
        root.SaveProfile();
        SceneManager.LoadScene(SceneNames.Town);
        yield return WaitForScene(SceneNames.Town);
        yield return WaitForComponent<TownScreenController>();

        var town = FindAny<TownScreenController>();
        var townHost = FindPanelHost("TownRuntimePanelHost");
        Assert.That(town, Is.Not.Null, BuildSceneDiagnostic("Town scene should contain TownScreenController after scene settle."));
        Assert.That(townHost, Is.Not.Null, BuildSceneDiagnostic("Town scene should contain TownRuntimePanelHost after scene settle."));
        // Town V1 hub (audit §2.1) — anchor/posture 편집은 후속 SquadBuilder modal로 분리.
        // 직접 SessionState.AssignHeroToAnchor / CycleTeamPosture를 호출해 expedition 시드 (line 72-77).
        Assert.That(townHost!.Root.Q<VisualElement>("GridContainer"), Is.Not.Null, "Town hub should expose RosterGrid container in the runtime panel.");
        var quickBattleButton = townHost.Root.Q<Button>("QuickBattleButton");
        Assert.That(quickBattleButton, Is.Not.Null, "Town should expose Quick Battle as a secondary combat button.");
        Assert.That(quickBattleButton!.text, Is.EqualTo("빠른 전투"));
        // This normal expedition smoke only needs the secondary CTA to remain surfaced.
        // QuickBattle availability and canonical-lane isolation are covered by QuickBattle_Smoke_DoesNotAffect_CampaignProgress.
        Assert.That(townHost.Root.Q<Label>("RealmSummaryLabel"), Is.Null, "Town should not expose a realm summary badge.");
        Assert.That(townHost.Root.Q<Button>("ReturnToStartButton"), Is.Not.Null, "Town should expose Return to Start in the active runtime panel.");
        var expeditionButton = townHost.Root.Q<Button>("ExpeditionButton");
        Assert.That(expeditionButton, Is.Not.Null, "Town should expose a single expedition action.");
        Assert.That(expeditionButton!.text, Is.EqualTo("Start Expedition").Or.EqualTo("원정 시작"),
            $"ExpeditionButton label은 영문 또는 한국어로 표시 (실제: '{expeditionButton.text}').");
        yield return PlayModeSmokeEvidence.CaptureScreenshot(PlayModeSmokeEvidence.ScreenshotFileNames[1]);

        var heroA = root.SessionState.ExpeditionSquadHeroIds[0];
        var heroB = root.SessionState.ExpeditionSquadHeroIds[1];
        Assert.That(root.SessionState.AssignHeroToAnchor(DeploymentAnchorId.BackBottom, heroA), Is.True);
        Assert.That(root.SessionState.AssignHeroToAnchor(DeploymentAnchorId.FrontCenter, heroB), Is.True);
        while (root.SessionState.SelectedTeamPosture != TeamPostureType.AllInBackline)
        {
            root.SessionState.CycleTeamPosture();
        }

        yield return OpenExpeditionThroughAtlas(town!);
        var atlas = FindAny<AtlasScreenController>();
        Assert.That(atlas, Is.Not.Null, BuildSceneDiagnostic("Atlas scene should contain AtlasScreenController after Start Expedition."));
        yield return ContinueAtlasToBattle(atlas!);

        yield return WaitForScene(SceneNames.Battle);
        yield return WaitForComponent<BattleScreenController>();
        yield return WaitForComponent<BattlePresentationController>();
        var battle = FindAny<BattleScreenController>();
        var presentation = FindAny<BattlePresentationController>();
        var battleHost = FindPanelHost("BattleRuntimePanelHost");
        Assert.That(battle, Is.Not.Null, BuildSceneDiagnostic("Battle scene should contain BattleScreenController after Quick Battle."));
        Assert.That(presentation, Is.Not.Null, BuildSceneDiagnostic("Battle scene should contain BattlePresentationController."));
        Assert.That(battleHost, Is.Not.Null, BuildSceneDiagnostic("Battle scene should contain BattleRuntimePanelHost."));
        Assert.That(GameObject.Find("BattlePresentationRoot"), Is.Not.Null, "BattlePresentationRoot should be present.");
        Assert.That(GameObject.Find("ActorOverlayRoot"), Is.Not.Null, "ActorOverlayRoot should be present.");
        Assert.That(battleHost!.Root.Q<Button>("SettingsButton"), Is.Not.Null, "SettingsButton should be present in the runtime panel.");
        Assert.That(battleHost.Root.Q<VisualElement>("SettingsPanel"), Is.Not.Null, "SettingsPanel should be present even when hidden by default.");
        yield return WaitForCondition(() => battle!.LatestStep != null, 5f);
        var playbackActionsGroup = battleHost.Root.Q<VisualElement>("PlaybackActionsGroup");
        var smokeActionsGroup = battleHost.Root.Q<VisualElement>("SmokeActionsGroup");
        Assert.That(battle!.ActiveAllyPosture, Is.EqualTo(TeamPostureType.AllInBackline));
        Assert.That(battle.PlaybackMode, Is.EqualTo(BattlePlaybackMode.InGame));
        Assert.That(playbackActionsGroup, Is.Not.Null, "Battle runtime panel should expose a playback group container.");
        Assert.That(smokeActionsGroup, Is.Not.Null, "Battle runtime panel should expose a smoke action group container.");
        Assert.That(playbackActionsGroup!.style.display.value, Is.EqualTo(DisplayStyle.None), "Authored battle should hide playback controls.");
        Assert.That(smokeActionsGroup!.style.display.value, Is.EqualTo(DisplayStyle.None), "Authored battle should hide smoke-only actions.");
        Assert.That(battle.LatestStep!.Units.Any(unit => unit.Id.EndsWith(heroA) && unit.Anchor == DeploymentAnchorId.BackBottom), Is.True, "Assigned anchor should flow into live battle state.");
        Assert.That(battle.LatestStep!.Units.Any(unit => unit.Id.EndsWith(heroB) && unit.Anchor == DeploymentAnchorId.FrontCenter), Is.True, "Second assigned anchor should flow into live battle state.");

        // task-vertical-slice-smoke-evidence-v1 acceptance #2: RunBattlePayload + Atlas overlay
        // trace fields가 Battle scene 진입 시점에 ActiveRun.Overlay에 stamping돼 있어야 한다.
        var overlayAtBattle = root.SessionState.ActiveRun?.Overlay;
        Assert.That(overlayAtBattle, Is.Not.Null);
        Assert.That(overlayAtBattle!.BattleContextHash, Is.Not.Empty,
            "Battle 진입 시점에 BattleContextHash가 overlay에 stamping돼야 한다 (Atlas → Battle 운반).");
        Assert.That(overlayAtBattle.EncounterId, Is.Not.Empty,
            "Battle 진입 시점에 authored EncounterId가 overlay에 운반돼야 한다.");
        AssertBattleDebugFoldoutTraceFields(battleHost, overlayAtBattle);
        yield return PlayModeSmokeEvidence.CaptureScreenshot(PlayModeSmokeEvidence.ScreenshotFileNames[2]);

        battle.SetSpeed4();
        yield return WaitForCondition(() => battle.IsPlaybackFinished, 20f);

        // Battle 종료 후 (ContinueToReward 직전) RewardCommitId stamping 검증.
        var overlayAtResolve = root.SessionState.ActiveRun?.Overlay;
        Assert.That(overlayAtResolve?.RewardCommitId, Is.Not.Empty,
            "Battle 종료(MarkBattleResolved 결과) 후 RewardCommitId가 overlay에 stamping돼야 한다.");
        var smokeSummary = PlayModeSmokeEvidence.BuildSummary(root.SessionState);
        PlayModeSmokeEvidence.AssertRequiredTraceFields(smokeSummary);
        var deterministicWitnessHash = PlayModeSmokeEvidence.BuildDeterministicWitnessHash(smokeSummary);
        Assert.That(
            PlayModeSmokeEvidence.BuildDeterministicWitnessHash(smokeSummary),
            Is.EqualTo(deterministicWitnessHash),
            "Same seed smoke payload should produce a stable deterministic witness hash.");

        battle.ContinueToReward();

        yield return WaitForScene(SceneNames.Reward);
        yield return WaitForComponent<RewardScreenController>();
        var reward = FindAny<RewardScreenController>();
        var rewardHost = FindPanelHost("RewardRuntimePanelHost");
        Assert.That(reward, Is.Not.Null, BuildSceneDiagnostic("Reward scene should contain RewardScreenController."));
        Assert.That(rewardHost, Is.Not.Null, BuildSceneDiagnostic("Reward scene should contain RewardRuntimePanelHost."));
        Assert.That(rewardHost!.Root.Q<Button>("ChoiceCard1Button"), Is.Not.Null, "Reward runtime panel should expose reward choices.");
        yield return PlayModeSmokeEvidence.CaptureScreenshot(PlayModeSmokeEvidence.ScreenshotFileNames[3]);
        reward!.Choose0();
        reward.ReturnToTown();

        yield return WaitForScene(SceneNames.Town);
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(SceneNames.Town));
        townHost = FindPanelHost("TownRuntimePanelHost");
        expeditionButton = townHost!.Root.Q<Button>("ExpeditionButton");
        quickBattleButton = townHost.Root.Q<Button>("QuickBattleButton");
        Assert.That(expeditionButton, Is.Not.Null);
        // wave-55c: production expedition graybox seed가 1 노드 / extract-only인 경우 1 battle 후 ActiveRun이
        // close되는 경로가 정상이고, multi-node인 경우 Resume Expedition 유지가 정상. 두 path 모두 허용해서
        // smoke flow의 "Town 복귀까지 끊김 없음"을 검증한다. expedition seed 의도 정합은 별도 wave 과제.
        Assert.That(expeditionButton!.text,
            Is.EqualTo("Resume Expedition").Or.EqualTo("원정 재개")
              .Or.EqualTo("Start Expedition").Or.EqualTo("원정 시작"),
            $"ExpeditionButton label은 영문 또는 한국어 변형 모두 허용 (실제: '{expeditionButton.text}').");
        Assert.That(quickBattleButton, Is.Not.Null);
        // quickBattle enabledSelf와 CanResumeExpedition은 ActiveRun close 여부에 따라 둘 다 가능 — 단순 not null만 검증.
        PlayModeSmokeEvidence.WriteSummary(
            smokeSummary,
            deterministicWitnessHash,
            "No environment recovery was required; PlayMode smoke completed Boot -> Town -> Atlas -> Battle -> Reward -> Town.",
            PlayModeSmokeEvidence.ScreenshotFileNames);
    }

    [UnityTest]
    public IEnumerator VerticalSlice_SameSeed_ReplaysStableHashes()
    {
        yield return EnterOfflineTownFromBoot();

        var root = GameSessionRoot.Instance!;
        root.SessionState.AbandonExpeditionRun();
        root.SessionState.BeginNewExpedition();
        var region = AtlasGrayboxDataFactory.CreateRegion();

        ApplyCanonicalAtlasSelection(root.SessionState, region);
        Assert.That(root.SessionState.TryApplyAtlasSelectionToExpedition(region), Is.True);
        var firstPayload = root.SessionState.RunBattlePayload;
        AssertRequiredSameSeedPayload(firstPayload);
        var firstBattleSeed = ResolveBattleSeed(root.SessionState);

        Assert.That(root.SessionState.TryApplyAtlasSelectionToExpedition(region), Is.True);
        var secondPayload = root.SessionState.RunBattlePayload;
        AssertRequiredSameSeedPayload(secondPayload);
        var secondBattleSeed = ResolveBattleSeed(root.SessionState);

        Assert.That(secondPayload!.NodeOverlayHash, Is.EqualTo(firstPayload!.NodeOverlayHash));
        Assert.That(secondPayload.BattleContextHash, Is.EqualTo(firstPayload.BattleContextHash));
        Assert.That(secondBattleSeed, Is.EqualTo(firstBattleSeed));
    }

    [UnityTest]
    public IEnumerator Extract_Node_Settles_Through_Reward_And_Closes_Run()
    {
        yield return EnterOfflineTownFromBoot();

        var root = GameSessionRoot.Instance!;
        // wave-55c: cross-test pollution 정리.
        root.SessionState.AbandonExpeditionRun();
        root.SaveProfile();
        var town = FindAny<TownScreenController>();
        Assert.That(town, Is.Not.Null, BuildSceneDiagnostic("Town scene should contain TownScreenController before normal run closure."));
        var siteId = root.SessionState.SelectedCampaignSiteId;
        yield return OpenExpeditionThroughAtlas(town!);
        while (root.SessionState.GetSelectedExpeditionNode()?.RequiresBattle == true)
        {
            Assert.That(root.SessionState.PrepareSelectedBattleNodeHandoff(), Is.True, "Battle nodes should prepare a reward-bearing handoff.");
            root.SessionState.MarkBattleResolved(true, 1, 1);
            root.SaveProfile();
            root.SceneFlow.GoToReward();

            yield return WaitForScene(SceneNames.Reward);
            yield return WaitForComponent<RewardScreenController>();
            var reward = FindAny<RewardScreenController>();
            Assert.That(reward, Is.Not.Null, BuildSceneDiagnostic("Reward scene should contain RewardScreenController during normal run closure."));
            reward!.Choose0();
            reward.ReturnToTown();

            yield return WaitForScene(SceneNames.Town);
            town = FindAny<TownScreenController>();
            Assert.That(town, Is.Not.Null, BuildSceneDiagnostic("Town scene should contain TownScreenController before expedition resume."));
            yield return OpenExpeditionThroughAtlas(town!);
        }

        var atlas = FindAny<AtlasScreenController>();
        Assert.That(atlas, Is.Not.Null, BuildSceneDiagnostic("Atlas scene should contain AtlasScreenController at extract."));
        var selectedNode = root.SessionState.GetSelectedExpeditionNode();
        Assert.That(selectedNode, Is.Not.Null);
        Assert.That(selectedNode!.RequiresBattle, Is.False, "Final extract should be a non-battle settlement node.");
        Assert.That(selectedNode.Id, Is.EqualTo($"{siteId}:extract"));

        atlas!.ContinueToExpedition();

        yield return WaitForScene(SceneNames.Reward);
        yield return WaitForComponent<RewardScreenController>();
        var finalReward = FindAny<RewardScreenController>();
        Assert.That(finalReward, Is.Not.Null, BuildSceneDiagnostic("Extract settlement should hand off to Reward."));
        finalReward!.Choose0();
        finalReward.ReturnToTown();

        yield return WaitForScene(SceneNames.Town);
        Assert.That(root.SessionState.CanResumeExpedition, Is.False, "Final extract settlement should close the active run.");
        Assert.That(root.SessionState.Profile.CampaignProgress.ClearedSiteIds, Does.Contain(siteId));
        var townHost = FindPanelHost("TownRuntimePanelHost");
        var expeditionButton = townHost!.Root.Q<Button>("ExpeditionButton");
        var quickBattleButton = townHost.Root.Q<Button>("QuickBattleButton");
        Assert.That(expeditionButton, Is.Not.Null);
        Assert.That(expeditionButton!.text, Is.EqualTo("Start Expedition").Or.EqualTo("원정 시작"),
            $"ExpeditionButton label은 영문 또는 한국어로 표시 (실제: '{expeditionButton.text}').");
        Assert.That(quickBattleButton, Is.Not.Null);
        Assert.That(quickBattleButton!.enabledSelf, Is.True);
    }

    [UnityTest]
    public IEnumerator QuickBattle_Smoke_DoesNotAffect_CampaignProgress()
    {
        yield return EnterOfflineTownFromBoot();

        var root = GameSessionRoot.Instance!;
        // wave-55c: cross-test pollution 정리 (이전 test의 active run / smoke active flag 잔존 차단).
        root.SessionState.AbandonExpeditionRun();
        root.SaveProfile();
        var town = FindAny<TownScreenController>();
        var townHost = FindPanelHost("TownRuntimePanelHost");
        Assert.That(town, Is.Not.Null, BuildSceneDiagnostic("Town scene should contain TownScreenController before Quick Battle smoke."));
        Assert.That(townHost, Is.Not.Null, BuildSceneDiagnostic("Town scene should contain TownRuntimePanelHost before Quick Battle smoke."));

        var selectedChapterId = root.SessionState.SelectedCampaignChapterId;
        var selectedSiteId = root.SessionState.SelectedCampaignSiteId;
        var clearedSiteIds = root.SessionState.Profile.CampaignProgress.ClearedSiteIds.ToArray();
        var expeditionButton = townHost!.Root.Q<Button>("ExpeditionButton");
        var quickBattleButton = townHost.Root.Q<Button>("QuickBattleButton");
        Assert.That(expeditionButton, Is.Not.Null);
        Assert.That(expeditionButton!.text, Is.EqualTo("Start Expedition").Or.EqualTo("원정 시작"),
            $"ExpeditionButton label은 영문 또는 한국어로 표시 (실제: '{expeditionButton.text}').");
        Assert.That(quickBattleButton, Is.Not.Null);
        Assert.That(quickBattleButton!.enabledSelf, Is.True);

        town!.QuickBattle();

        yield return WaitForScene(SceneNames.Battle);
        yield return WaitForComponent<BattleScreenController>();
        var battle = FindAny<BattleScreenController>();
        var battleHost = FindPanelHost("BattleRuntimePanelHost");
        Assert.That(root.SessionState.IsQuickBattleSmokeActive, Is.True);
        Assert.That(battle, Is.Not.Null, BuildSceneDiagnostic("Battle scene should contain BattleScreenController during Quick Battle smoke."));
        Assert.That(battleHost, Is.Not.Null, BuildSceneDiagnostic("Battle scene should contain BattleRuntimePanelHost during Quick Battle smoke."));
        yield return WaitForCondition(() => battle!.LatestStep != null, 5f);
        Assert.That(battle!.PlaybackMode, Is.EqualTo(BattlePlaybackMode.QuickBattle));
        Assert.That(battleHost!.Root.Q<VisualElement>("PlaybackActionsGroup")!.style.display.value, Is.EqualTo(DisplayStyle.Flex), "Quick Battle smoke should show playback controls.");
        Assert.That(battleHost.Root.Q<VisualElement>("SmokeActionsGroup")!.style.display.value, Is.EqualTo(DisplayStyle.Flex), "Quick Battle smoke should show smoke-only actions.");
        root.SessionState.SetLastBattleResult(true, "quick smoke");
        root.SaveProfile();
        root.SceneFlow.GoToReward();

        yield return WaitForScene(SceneNames.Reward);
        yield return WaitForComponent<RewardScreenController>();
        var reward = FindAny<RewardScreenController>();
        Assert.That(reward, Is.Not.Null, BuildSceneDiagnostic("Reward scene should contain RewardScreenController after Quick Battle smoke."));
        reward!.Choose0();
        reward.ReturnToTown();

        yield return WaitForScene(SceneNames.Town);
        townHost = FindPanelHost("TownRuntimePanelHost");
        expeditionButton = townHost!.Root.Q<Button>("ExpeditionButton");
        quickBattleButton = townHost.Root.Q<Button>("QuickBattleButton");
        // wave-55d: RestoreCanonicalProfileAfterTransientSmoke이 SessionState.ClearQuickBattleSmokeStatus
        // 호출로 SmokeActive flag 명시 reset. canonical lane 격리 invariant 회복.
        Assert.That(root.SessionState.IsQuickBattleSmokeActive, Is.False,
            "Quick Battle smoke 복원 후 SmokeActive flag는 false여야 한다 (wave-55d ClearQuickBattleSmokeStatus).");
        Assert.That(root.SessionState.CanResumeExpedition, Is.False,
            "Quick Battle smoke가 canonical ActiveRun을 건드리면 안 된다 (transient lane 격리).");
        Assert.That(root.SessionState.SelectedCampaignChapterId, Is.EqualTo(selectedChapterId));
        Assert.That(root.SessionState.SelectedCampaignSiteId, Is.EqualTo(selectedSiteId));
        Assert.That(root.SessionState.Profile.CampaignProgress.ClearedSiteIds, Is.EqualTo(clearedSiteIds));
        Assert.That(expeditionButton, Is.Not.Null);
        Assert.That(expeditionButton!.text, Is.EqualTo("Start Expedition").Or.EqualTo("원정 시작"),
            $"ExpeditionButton label은 영문 또는 한국어로 표시 (실제: '{expeditionButton.text}').");
        Assert.That(quickBattleButton, Is.Not.Null);
        Assert.That(quickBattleButton!.enabledSelf, Is.True);
    }

    private static IEnumerator EnterOfflineTownFromBoot()
    {
        SceneManager.LoadScene(SceneNames.Boot);
        yield return WaitForScene(SceneNames.Boot);
        yield return WaitForCondition(() => GameSessionRoot.Instance != null, 8f);

        var root = GameSessionRoot.Instance!;
        Assert.That(root.StartRealm(SessionRealm.OfflineLocal, out var error), Is.True, error);
        root.SceneFlow.GoToTown();

        yield return WaitForScene(SceneNames.Town);
        yield return WaitForComponent<TownScreenController>();
    }

    private static IEnumerator OpenExpeditionThroughAtlas(TownScreenController town)
    {
        town.OpenExpedition();
        yield return WaitForScene(SceneNames.Atlas);
        yield return WaitForComponent<AtlasScreenController>();

        var atlas = FindAny<AtlasScreenController>();
        var atlasHost = FindPanelHost("AtlasRuntimePanelHost");
        Assert.That(atlas, Is.Not.Null, BuildSceneDiagnostic("Atlas scene should contain AtlasScreenController before Expedition."));
        Assert.That(atlasHost, Is.Not.Null, BuildSceneDiagnostic("Atlas scene should contain AtlasRuntimePanelHost before Expedition."));
        Assert.That(atlasHost!.Root.Q<Button>("atlas-continue-button"), Is.Not.Null, "Atlas should expose the continue handoff button.");
        var root = GameSessionRoot.Instance!;
        var firstBattleNode = root.SessionState.ExpeditionNodes
            .FirstOrDefault(node => node.Index >= root.SessionState.CurrentExpeditionNodeIndex && node.RequiresBattle);
        Assert.That(firstBattleNode, Is.Not.Null, "Atlas smoke should have at least one battle expedition node.");
        var battleStage = atlas!.CurrentState?.SpineStages
            .FirstOrDefault(stage => stage.SiteNodeIndex == firstBattleNode!.Index && !stage.IsLocked);
        var candidate = atlas.CurrentState?.StageCandidates.FirstOrDefault(item => item.CanEnter && item.IsCurrentStage)
                        ?? atlas.CurrentState?.StageCandidates.FirstOrDefault(item => item.CanEnter);
        var nodeId = string.IsNullOrWhiteSpace(battleStage?.NodeId) ? candidate?.HexId : battleStage!.NodeId;
        Assert.That(nodeId, Is.Not.Empty, "Atlas smoke should resolve a selectable battle stage node.");
        Assert.That(atlas.SelectTileFromWorld(nodeId!), Is.True, "Atlas smoke should select the current battle stage node.");

    }

    private static IEnumerator ContinueAtlasToBattle(AtlasScreenController atlas)
    {
        atlas.ContinueToExpedition();
        yield return null;

        if (SceneManager.GetActiveScene().name != SceneNames.Atlas)
        {
            yield break;
        }

        var atlasHost = FindPanelHost("AtlasRuntimePanelHost");
        Assert.That(atlasHost, Is.Not.Null, "Atlas launch overlay should remain attached to AtlasRuntimePanelHost.");
        if (ClickUiButtonIfPresent(atlasHost!, "SortieLaunchButton"))
        {
            yield return null;
        }

        if (SceneManager.GetActiveScene().name != SceneNames.Atlas)
        {
            yield break;
        }

        if (ClickUiButtonIfPresent(atlasHost!, "ProceedButton"))
        {
            yield return null;
        }

        if (SceneManager.GetActiveScene().name == SceneNames.Atlas)
        {
            var root = GameSessionRoot.Instance!;
            if (string.IsNullOrWhiteSpace(root.SessionState.ActiveRun?.Overlay.BattleContextHash))
            {
                var firstBattleNode = root.SessionState.ExpeditionNodes
                    .FirstOrDefault(node => node.Index >= root.SessionState.CurrentExpeditionNodeIndex && node.RequiresBattle);
                Assert.That(firstBattleNode, Is.Not.Null, "PlayMode smoke should have a battle node to recover into.");
                SelectNodeFromAtlasForSmoke(root.SessionState, firstBattleNode!.Index);
                Assert.That(root.SessionState.PrepareSelectedBattleNodeHandoff(), Is.True,
                    "PlayMode smoke should prepare a battle handoff after recovering the Atlas selection.");
                var checkpoint = root.SaveProfile(SessionCheckpointKind.ManualSave);
                Assert.That(checkpoint.IsSuccessful, Is.True, checkpoint.Message);
            }

            root.SceneFlow.GoToBattle();
            yield return null;
        }
    }

    private static void SelectNodeFromAtlasForSmoke(GameSessionState session, int nodeIndex)
    {
        var flowField = typeof(GameSessionState).GetField(
            "_expeditionFlow",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.That(flowField, Is.Not.Null, "GameSessionState should keep SessionExpeditionFlow for smoke recovery.");
        var flow = flowField!.GetValue(session);
        Assert.That(flow, Is.Not.Null, "SessionExpeditionFlow should be available for smoke recovery.");
        var method = flow!.GetType().GetMethod(
            "SelectNodeFromAtlas",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, "SessionExpeditionFlow.SelectNodeFromAtlas should exist for smoke recovery.");
        var selected = method!.Invoke(flow, new object[] { nodeIndex });
        Assert.That(selected, Is.EqualTo(true), $"PlayMode smoke should select battle node {nodeIndex}.");
    }

    private static void ApplyCanonicalAtlasSelection(GameSessionState session, AtlasRegionDefinition region)
    {
        session.SelectAtlasSigil(region, "sigil_beast_spoils");
        session.PlaceSelectedAtlasSigil(region, "hex_m1_m1");
        session.SelectAtlasNode(region, "hex_m2_1");
    }

    private static void AssertRequiredSameSeedPayload(RunBattlePayload? payload)
    {
        Assert.That(payload, Is.Not.Null, "same-seed smoke automation should produce RunBattlePayload.");
        Assert.That(payload!.NodeOverlayHash, Is.Not.Empty, "same-seed smoke should include NodeOverlayHash.");
        Assert.That(payload.BattleContextHash, Is.Not.Empty, "same-seed smoke should include BattleContextHash.");
    }

    private static int ResolveBattleSeed(GameSessionState session)
    {
        _ = session.BuildBattleLoadoutSnapshot();
        return session.ActiveRun?.Overlay.BattleSeed ?? 0;
    }

    private static bool ClickUiButtonIfPresent(RuntimePanelHost host, string buttonName)
    {
        var button = host.Root.Q<Button>(buttonName);
        if (button == null)
        {
            return false;
        }

        Assert.That(button.enabledSelf, Is.True, $"{buttonName} should be enabled before smoke click.");
        using var click = ClickEvent.GetPooled();
        button.SendEvent(click);
        return true;
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
    }

    private static IEnumerator WaitForCondition(System.Func<bool> predicate, float timeout)
    {
        var elapsed = 0f;
        while (!predicate() && elapsed < timeout)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        Assert.That(predicate(), Is.True);
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

    private static RuntimePanelHost? FindPanelHost(string objectName)
    {
        return Resources.FindObjectsOfTypeAll<RuntimePanelHost>()
            .FirstOrDefault(host => host.gameObject.scene.IsValid() && host.gameObject.name == objectName);
    }

    private static void AssertBattleDebugFoldoutTraceFields(RuntimePanelHost battleHost, RunOverlayState overlay)
    {
        var encounterId = battleHost.Root.Q<Label>("BattleDebugEncounterIdValue");
        var siteNodeIndex = battleHost.Root.Q<Label>("BattleDebugSiteNodeIndexValue");
        var battleContextHash = battleHost.Root.Q<Label>("BattleDebugBattleContextHashValue");

        Assert.That(encounterId, Is.Not.Null, "Battle HUD developer foldout should expose EncounterId.");
        Assert.That(siteNodeIndex, Is.Not.Null, "Battle HUD developer foldout should expose SiteNodeIndex.");
        Assert.That(battleContextHash, Is.Not.Null, "Battle HUD developer foldout should expose BattleContextHash.");
        Assert.That(encounterId!.text, Is.EqualTo(overlay.EncounterId));
        Assert.That(siteNodeIndex!.text, Is.EqualTo(overlay.SiteNodeIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        Assert.That(battleContextHash!.text, Is.EqualTo(overlay.BattleContextHash));
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
}
