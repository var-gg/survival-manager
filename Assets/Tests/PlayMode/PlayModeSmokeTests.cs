using System.Collections;
using System.Linq;
using NUnit.Framework;
using SM.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

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
        Assert.That(town, Is.Not.Null, BuildSceneDiagnostic("Town scene should contain TownScreenController after scene settle."));
        town!.QuickBattle();

        yield return WaitForScene(SceneNames.Battle);
        yield return WaitForComponent<BattleScreenController>();
        yield return WaitForComponent<BattlePresentationController>();
        var battle = FindAny<BattleScreenController>();
        var presentation = FindAny<BattlePresentationController>();
        Assert.That(battle, Is.Not.Null, BuildSceneDiagnostic("Battle scene should contain BattleScreenController after Quick Battle."));
        Assert.That(presentation, Is.Not.Null, BuildSceneDiagnostic("Battle scene should contain BattlePresentationController."));
        Assert.That(GameObject.Find("BattlePresentationRoot"), Is.Not.Null, "BattlePresentationRoot should be present.");
        Assert.That(GameObject.Find("ActorOverlayRoot"), Is.Not.Null, "ActorOverlayRoot should be present.");
        Assert.That(FindObjectByName("SettingsButton"), Is.Not.Null, "SettingsButton should be present.");
        Assert.That(FindObjectByName("SettingsPanel"), Is.Not.Null, "SettingsPanel should be present even when hidden by default.");

        battle!.SetSpeed4();
        yield return WaitForCondition(() => battle!.IsPlaybackFinished, 20f);
        battle!.ContinueToReward();

        yield return WaitForScene(SceneNames.Reward);
        yield return WaitForComponent<RewardScreenController>();
        var reward = FindAny<RewardScreenController>();
        Assert.That(reward, Is.Not.Null, BuildSceneDiagnostic("Reward scene should contain RewardScreenController."));
        reward!.Choose0();
        reward.ReturnToTown();

        yield return WaitForScene(SceneNames.Town);
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(SceneNames.Town));
    }

    [UnityTest]
    public IEnumerator Expedition_Branch_Selection_Applies_Node_Effect_And_Preserves_Resume_State()
    {
        SceneManager.LoadScene(SceneNames.Boot);
        yield return WaitForScene(SceneNames.Town);
        yield return WaitForComponent<TownScreenController>();

        var root = GameSessionRoot.Instance;
        Assert.That(root, Is.Not.Null, "Boot scene should create GameSessionRoot before Town.");
        var initialTraitReroll = root!.SessionState.Profile.Currencies.TraitRerollCurrency;

        var town = FindAny<TownScreenController>();
        Assert.That(town, Is.Not.Null, BuildSceneDiagnostic("Town scene should contain TownScreenController before expedition routing."));
        town!.DebugStartExpedition();

        yield return WaitForScene(SceneNames.Expedition);
        yield return WaitForComponent<ExpeditionScreenController>();
        var expedition = FindAny<ExpeditionScreenController>();
        Assert.That(expedition, Is.Not.Null, BuildSceneDiagnostic("Expedition scene should contain ExpeditionScreenController."));
        expedition!.SelectNode3();
        expedition.NextBattleOrAdvance();

        yield return WaitForScene(SceneNames.Battle);
        yield return WaitForComponent<BattleScreenController>();
        var battle = FindAny<BattleScreenController>();
        Assert.That(battle, Is.Not.Null, BuildSceneDiagnostic("Battle scene should contain BattleScreenController for expedition route."));
        battle!.SetSpeed4();
        yield return WaitForCondition(() => battle!.IsPlaybackFinished, 20f);
        battle.ContinueToReward();

        yield return WaitForScene(SceneNames.Reward);
        yield return WaitForComponent<RewardScreenController>();
        var reward = FindAny<RewardScreenController>();
        Assert.That(reward, Is.Not.Null, BuildSceneDiagnostic("Reward scene should contain RewardScreenController after expedition battle."));
        reward!.Choose0();
        reward.ReturnToTown();

        yield return WaitForScene(SceneNames.Town);
        Assert.That(root.SessionState.CurrentExpeditionNodeIndex, Is.EqualTo(2), "Relay route should advance expedition state to node index 2.");
        Assert.That(root.SessionState.Profile.Currencies.TraitRerollCurrency, Is.EqualTo(initialTraitReroll + 1), "Relay route node effect should grant Trait Reroll +1.");
        Assert.That(root.SessionState.CanResumeExpedition, Is.True, "Town should allow resuming the expedition after a successful route battle.");
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

    private static GameObject? FindObjectByName(string objectName)
    {
        var active = GameObject.Find(objectName);
        if (active != null)
        {
            return active;
        }

        return Resources.FindObjectsOfTypeAll<Transform>()
            .FirstOrDefault(transform => transform.name == objectName && transform.gameObject.scene.IsValid())
            ?.gameObject;
    }

    private static string BuildSceneDiagnostic(string prefix)
    {
        var scene = SceneManager.GetActiveScene();
        var rootNames = string.Join(", ", scene.GetRootGameObjects().Select(x => x.name));
        var townControllerObjects = string.Join(", ", scene
            .GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Where(transform => transform.name.Contains("Controller"))
            .Select(transform => transform.name)
            .Distinct());

        return $"{prefix} ActiveScene={scene.name}; Roots=[{rootNames}]; ControllerObjects=[{townControllerObjects}]";
    }
}
