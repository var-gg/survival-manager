using System.Collections;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Unity;
using SM.Unity.UI;
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
    public IEnumerator Boot_To_QuickBattle_Smoke_Loop_Reaches_Battle_And_Returns_To_Town()
    {
        SceneManager.LoadScene(SceneNames.Boot);
        yield return WaitForScene(SceneNames.Town);
        Assert.That(GameSessionRoot.Instance, Is.Not.Null, "Boot scene should create GameSessionRoot before Town.");

        yield return WaitForComponent<TownScreenController>();

        var town = FindAny<TownScreenController>();
        var townHost = FindPanelHost("TownRuntimePanelHost");
        Assert.That(town, Is.Not.Null, BuildSceneDiagnostic("Town scene should contain TownScreenController after scene settle."));
        Assert.That(townHost, Is.Not.Null, BuildSceneDiagnostic("Town scene should contain TownRuntimePanelHost after scene settle."));
        Assert.That(townHost!.Root.Q<Button>("DeployButton_FrontTop"), Is.Not.Null, "Town should expose deployment anchor buttons in the runtime panel.");
        Assert.That(townHost.Root.Q<Button>("TeamPostureButton"), Is.Not.Null, "Town should expose a team posture button in the runtime panel.");
        Assert.That(townHost.Root.Q<Button>("QuickBattleButton"), Is.Not.Null, "Town should expose a quick battle button in the runtime panel.");

        var root = GameSessionRoot.Instance!;
        var heroA = root.SessionState.ExpeditionSquadHeroIds[0];
        var heroB = root.SessionState.ExpeditionSquadHeroIds[1];
        Assert.That(root.SessionState.AssignHeroToAnchor(DeploymentAnchorId.BackBottom, heroA), Is.True);
        Assert.That(root.SessionState.AssignHeroToAnchor(DeploymentAnchorId.FrontCenter, heroB), Is.True);
        while (root.SessionState.SelectedTeamPosture != TeamPostureType.AllInBackline)
        {
            root.SessionState.CycleTeamPosture();
        }

        town!.QuickBattle();

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
        Assert.That(battle!.ActiveAllyPosture, Is.EqualTo(TeamPostureType.AllInBackline));
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
    }

    [UnityTest]
    public IEnumerator Expedition_Branch_Selection_Resolves_Current_Node_And_Preserves_Resume_State()
    {
        SceneManager.LoadScene(SceneNames.Boot);
        yield return WaitForScene(SceneNames.Town);
        yield return WaitForComponent<TownScreenController>();

        var root = GameSessionRoot.Instance;
        Assert.That(root, Is.Not.Null, "Boot scene should create GameSessionRoot before Town.");

        var town = FindAny<TownScreenController>();
        Assert.That(town, Is.Not.Null, BuildSceneDiagnostic("Town scene should contain TownScreenController before expedition routing."));
        town!.DebugStartExpedition();

        yield return WaitForScene(SceneNames.Expedition);
        yield return WaitForComponent<ExpeditionScreenController>();
        var expedition = FindAny<ExpeditionScreenController>();
        var expeditionHost = FindPanelHost("ExpeditionRuntimePanelHost");
        Assert.That(expedition, Is.Not.Null, BuildSceneDiagnostic("Expedition scene should contain ExpeditionScreenController."));
        Assert.That(expeditionHost, Is.Not.Null, BuildSceneDiagnostic("Expedition scene should contain ExpeditionRuntimePanelHost."));
        Assert.That(expeditionHost!.Root.Q<Button>("DeployButton_BackCenter"), Is.Not.Null, "Expedition scene should expose anchor buttons in the runtime panel.");
        Assert.That(expeditionHost.Root.Q<Button>("TeamPostureButton"), Is.Not.Null, "Expedition scene should expose a posture button in the runtime panel.");

        var selectedNode = root!.SessionState.GetSelectedExpeditionNode() ?? root.SessionState.GetCurrentExpeditionNode();
        Assert.That(selectedNode, Is.Not.Null, "Expedition should expose a resolvable node.");
        var expectedResolvedIndex = selectedNode!.Index;
        var expectedCanResume = expectedResolvedIndex < root.SessionState.ExpeditionNodes.Count - 1;

        Assert.That(root.SessionState.ResolveSelectedExpeditionNode(), Is.True, "Selected expedition node should resolve in smoke state.");
        root.SceneFlow.ReturnToTown();

        yield return WaitForScene(SceneNames.Town);
        Assert.That(root.SessionState.CurrentExpeditionNodeIndex, Is.EqualTo(expectedResolvedIndex), "Resolved expedition node index should persist after returning to Town.");
        Assert.That(root.SessionState.CanResumeExpedition, Is.EqualTo(expectedCanResume), "Town should preserve authored expedition resume availability.");

        town = FindAny<TownScreenController>();
        Assert.That(town, Is.Not.Null, BuildSceneDiagnostic("Town scene should contain TownScreenController before expedition resume."));
        town!.DebugStartExpedition();

        yield return WaitForScene(SceneNames.Expedition);
        yield return WaitForComponent<ExpeditionScreenController>();
        Assert.That(root.SessionState.CurrentExpeditionNodeIndex, Is.EqualTo(expectedResolvedIndex), "Debug Start should resume the current authored expedition node instead of starting from camp.");
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
