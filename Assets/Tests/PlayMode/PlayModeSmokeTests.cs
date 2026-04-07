using System.Collections;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Meta.Model;
using SM.Unity;
using SM.Unity.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using UIButton = UnityEngine.UI.Button;
using UIText = UnityEngine.UI.Text;

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
        SceneManager.LoadScene(SceneNames.Boot);
        yield return WaitForScene(SceneNames.Boot);
        yield return WaitForCondition(() => GameSessionRoot.Instance != null, 8f);
        yield return WaitForComponent<BootScreenController>();

        var startButton = GameObject.Find("OfflineLocalButton")?.GetComponent<UIButton>();
        var startButtonLabel = GameObject.Find("OfflineLocalButtonText")?.GetComponent<UIText>();
        Assert.That(startButton, Is.Not.Null, BuildSceneDiagnostic("Boot scene should expose the Start Local Run button."));
        Assert.That(startButtonLabel, Is.Not.Null, BuildSceneDiagnostic("Boot scene should expose the Start Local Run label."));
        Assert.That(startButtonLabel!.text, Is.EqualTo("Start Local Run"));
        Assert.That(GameObject.Find("OnlineAuthoritativeButton"), Is.Null, "Boot scene should not expose the hidden future-seam online button.");

        startButton!.onClick.Invoke();
        yield return WaitForScene(SceneNames.Town);
        yield return WaitForComponent<TownScreenController>();

        var town = FindAny<TownScreenController>();
        var townHost = FindPanelHost("TownRuntimePanelHost");
        Assert.That(town, Is.Not.Null, BuildSceneDiagnostic("Town scene should contain TownScreenController after scene settle."));
        Assert.That(townHost, Is.Not.Null, BuildSceneDiagnostic("Town scene should contain TownRuntimePanelHost after scene settle."));
        Assert.That(townHost!.Root.Q<Button>("DeployButton_FrontTop"), Is.Not.Null, "Town should expose deployment anchor buttons in the runtime panel.");
        Assert.That(townHost.Root.Q<Button>("TeamPostureButton"), Is.Not.Null, "Town should expose a team posture button in the runtime panel.");
        var quickBattleButton = townHost.Root.Q<Button>("QuickBattleButton");
        Assert.That(quickBattleButton, Is.Not.Null, "Town should expose Quick Battle as a secondary smoke button.");
        Assert.That(quickBattleButton!.text, Is.EqualTo("Quick Battle (Smoke)"));
        Assert.That(quickBattleButton.enabledSelf, Is.True, "Quick Battle should be available before a normal expedition starts.");
        Assert.That(townHost.Root.Q<Label>("RealmSummaryLabel"), Is.Null, "Town should not expose a realm summary badge.");
        Assert.That(townHost.Root.Q<Button>("ReturnToStartButton"), Is.Not.Null, "Town should expose Return to Start in the active runtime panel.");
        var expeditionButton = townHost.Root.Q<Button>("ExpeditionButton");
        Assert.That(expeditionButton, Is.Not.Null, "Town should expose a single expedition action.");
        Assert.That(expeditionButton!.text, Is.EqualTo("Start Expedition"));

        var root = GameSessionRoot.Instance!;
        var heroA = root.SessionState.ExpeditionSquadHeroIds[0];
        var heroB = root.SessionState.ExpeditionSquadHeroIds[1];
        Assert.That(root.SessionState.AssignHeroToAnchor(DeploymentAnchorId.BackBottom, heroA), Is.True);
        Assert.That(root.SessionState.AssignHeroToAnchor(DeploymentAnchorId.FrontCenter, heroB), Is.True);
        while (root.SessionState.SelectedTeamPosture != TeamPostureType.AllInBackline)
        {
            root.SessionState.CycleTeamPosture();
        }

        town!.OpenExpedition();

        yield return WaitForScene(SceneNames.Expedition);
        yield return WaitForComponent<ExpeditionScreenController>();
        var expedition = FindAny<ExpeditionScreenController>();
        Assert.That(expedition, Is.Not.Null, BuildSceneDiagnostic("Expedition scene should contain ExpeditionScreenController after Start Expedition."));
        expedition!.NextBattle();

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

        battle.SetSpeed4();
        yield return WaitForCondition(() => battle.IsPlaybackFinished, 20f);
        battle.ContinueToReward();

        yield return WaitForScene(SceneNames.Reward);
        yield return WaitForComponent<RewardScreenController>();
        var reward = FindAny<RewardScreenController>();
        var rewardHost = FindPanelHost("RewardRuntimePanelHost");
        Assert.That(reward, Is.Not.Null, BuildSceneDiagnostic("Reward scene should contain RewardScreenController."));
        Assert.That(rewardHost, Is.Not.Null, BuildSceneDiagnostic("Reward scene should contain RewardRuntimePanelHost."));
        Assert.That(rewardHost!.Root.Q<Button>("ChoiceCard1Button"), Is.Not.Null, "Reward runtime panel should expose reward choices.");
        reward!.Choose0();
        reward.ReturnToTown();

        yield return WaitForScene(SceneNames.Town);
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(SceneNames.Town));
        townHost = FindPanelHost("TownRuntimePanelHost");
        expeditionButton = townHost!.Root.Q<Button>("ExpeditionButton");
        quickBattleButton = townHost.Root.Q<Button>("QuickBattleButton");
        Assert.That(expeditionButton, Is.Not.Null);
        Assert.That(expeditionButton!.text, Is.EqualTo("Resume Expedition"));
        Assert.That(townHost.Root.Q<Button>("PrevChapterButton")!.enabledSelf, Is.False, "Campaign chapter should lock while a run is active.");
        Assert.That(townHost.Root.Q<Button>("PrevSiteButton")!.enabledSelf, Is.False, "Campaign site should lock while a run is active.");
        Assert.That(quickBattleButton, Is.Not.Null);
        Assert.That(quickBattleButton!.enabledSelf, Is.False, "Quick Battle should not overwrite an active authored run.");
        Assert.That(root.SessionState.CanResumeExpedition, Is.True);
    }

    [UnityTest]
    public IEnumerator Extract_Node_Settles_Through_Reward_And_Closes_Run()
    {
        yield return EnterOfflineTownFromBoot();

        var root = GameSessionRoot.Instance!;
        var town = FindAny<TownScreenController>();
        Assert.That(town, Is.Not.Null, BuildSceneDiagnostic("Town scene should contain TownScreenController before normal run closure."));
        var siteId = root.SessionState.SelectedCampaignSiteId;
        town!.OpenExpedition();

        yield return WaitForScene(SceneNames.Expedition);
        yield return WaitForComponent<ExpeditionScreenController>();
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
            town!.OpenExpedition();

            yield return WaitForScene(SceneNames.Expedition);
            yield return WaitForComponent<ExpeditionScreenController>();
        }

        var expedition = FindAny<ExpeditionScreenController>();
        Assert.That(expedition, Is.Not.Null, BuildSceneDiagnostic("Expedition scene should contain ExpeditionScreenController at extract."));
        var selectedNode = root.SessionState.GetSelectedExpeditionNode();
        Assert.That(selectedNode, Is.Not.Null);
        Assert.That(selectedNode!.RequiresBattle, Is.False, "Final extract should be a non-battle settlement node.");
        Assert.That(selectedNode.Id, Is.EqualTo($"{siteId}:extract"));

        expedition!.NextBattle();

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
        Assert.That(expeditionButton!.text, Is.EqualTo("Start Expedition"));
        Assert.That(quickBattleButton, Is.Not.Null);
        Assert.That(quickBattleButton!.enabledSelf, Is.True);
    }

    [UnityTest]
    public IEnumerator QuickBattle_Smoke_DoesNotAffect_CampaignProgress()
    {
        yield return EnterOfflineTownFromBoot();

        var root = GameSessionRoot.Instance!;
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
        Assert.That(expeditionButton!.text, Is.EqualTo("Start Expedition"));
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
        Assert.That(root.SessionState.IsQuickBattleSmokeActive, Is.False);
        Assert.That(root.SessionState.CanResumeExpedition, Is.False);
        Assert.That(root.SessionState.SelectedCampaignChapterId, Is.EqualTo(selectedChapterId));
        Assert.That(root.SessionState.SelectedCampaignSiteId, Is.EqualTo(selectedSiteId));
        Assert.That(root.SessionState.Profile.CampaignProgress.ClearedSiteIds, Is.EqualTo(clearedSiteIds));
        Assert.That(expeditionButton, Is.Not.Null);
        Assert.That(expeditionButton!.text, Is.EqualTo("Start Expedition"));
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
