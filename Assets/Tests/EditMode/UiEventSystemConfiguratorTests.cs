using System;
using System.Linq;
using NUnit.Framework;
using SM.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace SM.Tests.EditMode;

public sealed class UiEventSystemConfiguratorTests
{
    [SetUp]
    public void CreateEmptyScene()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    [Test]
    public void EnsureSceneEventSystem_Uses_InputSystem_Module_With_Canonical_Bindings()
    {
        var scene = SceneManager.GetActiveScene();
        var go = new GameObject("TemporaryEventSystem");
        SceneManager.MoveGameObjectToScene(go, scene);

        go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();

        var inputSystemModuleType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        Assert.That(inputSystemModuleType, Is.Not.Null, "InputSystemUIInputModule type should resolve when the Input System package is installed.");

        var inputSystemModule = go.AddComponent(inputSystemModuleType!);
        var actionsAsset = AssetDatabase.LoadMainAssetAtPath("Assets/InputSystem_Actions.inputactions");
        Assert.That(actionsAsset, Is.Not.Null, "Project UI input actions asset should exist.");

        inputSystemModuleType!.GetProperty("actionsAsset")!.SetValue(inputSystemModule, actionsAsset);

        UiEventSystemConfigurator.EnsureSceneEventSystem(scene);

        var eventSystems = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<EventSystem>(true))
            .ToArray();

        Assert.That(eventSystems, Has.Length.EqualTo(1), "A playable scene should keep exactly one EventSystem.");
        Assert.That(go.name, Is.EqualTo("EventSystem"));
        Assert.That(go.GetComponent<StandaloneInputModule>(), Is.Null, "StandaloneInputModule should not survive canonical UI input setup.");

        var configuredModule = go.GetComponent(inputSystemModuleType!);
        Assert.That(configuredModule, Is.Not.Null);
        AssertBoundAction(configuredModule!, inputSystemModuleType!, "point", "Point", "UI");
        AssertBoundAction(configuredModule!, inputSystemModuleType!, "leftClick", "Click", "UI");
        AssertBoundAction(configuredModule!, inputSystemModuleType!, "move", "Navigate", "UI");
        AssertBoundAction(configuredModule!, inputSystemModuleType!, "submit", "Submit", "UI");
        AssertBoundAction(configuredModule!, inputSystemModuleType!, "cancel", "Cancel", "UI");
    }

    [Test]
    public void EnsureSceneEventSystem_Removes_Duplicate_EventSystems()
    {
        var scene = SceneManager.GetActiveScene();

        var first = new GameObject("PrimaryEventSystem");
        SceneManager.MoveGameObjectToScene(first, scene);
        first.AddComponent<EventSystem>();

        var second = new GameObject("SecondaryEventSystem");
        SceneManager.MoveGameObjectToScene(second, scene);
        second.AddComponent<EventSystem>();

        UiEventSystemConfigurator.EnsureSceneEventSystem(scene);

        var eventSystems = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<EventSystem>(true))
            .ToArray();

        Assert.That(eventSystems, Has.Length.EqualTo(1), "Duplicate EventSystems should be collapsed to a single canonical instance.");
        Assert.That(eventSystems[0].gameObject.name, Is.EqualTo("EventSystem"));
    }

    private static void AssertBoundAction(Component module, Type moduleType, string propertyName, string expectedActionName, string expectedMapName)
    {
        var actionReference = moduleType.GetProperty(propertyName)!.GetValue(module);
        Assert.That(actionReference, Is.Not.Null, $"Expected '{propertyName}' to be bound.");

        var action = actionReference!.GetType().GetProperty("action")!.GetValue(actionReference);
        Assert.That(action, Is.Not.Null, $"Expected '{propertyName}' to resolve to an action.");

        var actionName = action!.GetType().GetProperty("name")!.GetValue(action) as string;
        Assert.That(actionName, Is.EqualTo(expectedActionName), $"'{propertyName}' should bind '{expectedActionName}'.");

        var actionMap = action.GetType().GetProperty("actionMap")!.GetValue(action);
        var actionMapName = actionMap!.GetType().GetProperty("name")!.GetValue(actionMap) as string;
        Assert.That(actionMapName, Is.EqualTo(expectedMapName), $"'{propertyName}' should bind the '{expectedMapName}' action map.");
    }
}
