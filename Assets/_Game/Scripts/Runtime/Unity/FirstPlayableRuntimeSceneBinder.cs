using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SM.Unity;

public static class FirstPlayableRuntimeSceneBinder
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneLoadHook()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInitialSceneBindings()
    {
        EnsureSceneBindings(SceneManager.GetActiveScene());
    }

    public static void EnsureSceneBindings(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        EnsureEventSystem(scene);
        EnsureSharedUiFont(scene);

        switch (scene.name)
        {
            case SceneNames.Boot:
                EnsureBoot(scene);
                break;
            case SceneNames.Town:
                EnsureTown(scene);
                break;
            case SceneNames.Expedition:
                EnsureExpedition(scene);
                break;
            case SceneNames.Battle:
                EnsureBattle(scene);
                break;
            case SceneNames.Reward:
                EnsureReward(scene);
                break;
        }

        RefreshLocalizedBindings(scene);
    }

    public static void RefreshLocalizedBindings(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        EnsureSharedUiFont(scene);
        EnsureStaticLocalizedText(scene);
        EnsureLocalizationOverlay(scene);
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureSceneBindings(scene);
    }

    private static void EnsureBoot(Scene scene)
    {
        var bootstrapGo = FindGameObject(scene, "GameBootstrap");
        if (bootstrapGo == null)
        {
            return;
        }

        var bootstrap = EnsureComponent<GameBootstrap>(bootstrapGo);
        if (Application.isPlaying)
        {
            bootstrap.RunBootstrap();
        }
    }

    private static void EnsureTown(Scene scene)
    {
        var controllerGo = FindGameObject(scene, "TownScreenController");
        if (controllerGo == null)
        {
            return;
        }

        var controller = EnsureComponent<TownScreenController>(controllerGo);
        Bind(controller, new Dictionary<string, Object?>
        {
            ["titleText"] = GetComponentFromNamedObject<Text>(scene, "TitleText"),
            ["rosterText"] = GetComponentFromNamedObject<Text>(scene, "RosterText"),
            ["recruitText"] = GetComponentFromNamedObject<Text>(scene, "RecruitText"),
            ["recruitCardsRoot"] = GetComponentFromNamedObject<RectTransform>(scene, "RecruitCardsRoot"),
            ["squadText"] = GetComponentFromNamedObject<Text>(scene, "SquadText"),
            ["deployPreviewText"] = GetComponentFromNamedObject<Text>(scene, "DeployPreviewText"),
            ["currencyText"] = GetComponentFromNamedObject<Text>(scene, "CurrencyText"),
            ["statusText"] = GetComponentFromNamedObject<Text>(scene, "StatusText"),
        });
        controller.EnsureRuntimeControls();

        BindButton(scene, "RerollButton", controller.RerollOffers);
        BindButton(scene, "SaveButton", controller.SaveProfile);
        BindButton(scene, "LoadButton", controller.LoadProfile);
        BindButton(scene, "DebugStartButton", controller.DebugStartExpedition);
        BindButton(scene, "QuickBattleButton", controller.QuickBattle);
        BindButton(scene, "RecruitButton", controller.RecruitOffer0, "RecruitCard1");
        BindButton(scene, "RecruitButton", controller.RecruitOffer1, "RecruitCard2");
        BindButton(scene, "RecruitButton", controller.RecruitOffer2, "RecruitCard3");
        BindButton(scene, "RecruitButton", controller.RecruitOffer3, "RecruitCard4");
    }

    private static void EnsureExpedition(Scene scene)
    {
        var controllerGo = FindGameObject(scene, "ExpeditionScreenController");
        if (controllerGo == null)
        {
            return;
        }

        var controller = EnsureComponent<ExpeditionScreenController>(controllerGo);
        Bind(controller, new Dictionary<string, Object?>
        {
            ["titleText"] = GetComponentFromNamedObject<Text>(scene, "TitleText"),
            ["mapText"] = GetComponentFromNamedObject<Text>(scene, "MapText"),
            ["nodeTrackRoot"] = GetComponentFromNamedObject<RectTransform>(scene, "NodeTrackRoot"),
            ["positionText"] = GetComponentFromNamedObject<Text>(scene, "PositionText"),
            ["rewardText"] = GetComponentFromNamedObject<Text>(scene, "RewardText"),
            ["squadText"] = GetComponentFromNamedObject<Text>(scene, "SquadText"),
            ["statusText"] = GetComponentFromNamedObject<Text>(scene, "StatusText"),
        });
        controller.EnsureRuntimeControls();

        BindButton(scene, "NextBattleButton", controller.NextBattleOrAdvance);
        BindButton(scene, "ReturnTownButton", controller.ReturnToTown);
        BindButton(scene, "SelectButton", controller.SelectNode1, "NodeBox1");
        BindButton(scene, "SelectButton", controller.SelectNode2, "NodeBox2");
        BindButton(scene, "SelectButton", controller.SelectNode3, "NodeBox3");
        BindButton(scene, "SelectButton", controller.SelectNode4, "NodeBox4");
        BindButton(scene, "SelectButton", controller.SelectNode5, "NodeBox5");
    }

    private static void EnsureBattle(Scene scene)
    {
        var presentationGo = FindGameObject(scene, "BattlePresentationRoot");
        var controllerGo = FindGameObject(scene, "BattleScreenController");
        var settingsGo = FindGameObject(scene, "BattleSettingsController");
        if (presentationGo == null || controllerGo == null || settingsGo == null)
        {
            return;
        }

        var presentation = EnsureComponent<BattlePresentationController>(presentationGo);
        Bind(presentation, new Dictionary<string, Object?>
        {
            ["battleStageRoot"] = GetComponentFromNamedObject<Transform>(scene, "BattleStageRoot"),
            ["actorOverlayRoot"] = GetComponentFromNamedObject<RectTransform>(scene, "ActorOverlayRoot"),
        });

        var settingsController = EnsureComponent<BattleSettingsPanelController>(settingsGo);
        Bind(settingsController, new Dictionary<string, Object?>
        {
            ["panelRoot"] = GetComponentFromNamedObject<RectTransform>(scene, "SettingsPanel"),
            ["worldHpButtonLabel"] = FindChildGameObject(scene, "ToggleWorldHpButton", "Label")?.GetComponent<Text>(),
            ["overlayHpButtonLabel"] = FindChildGameObject(scene, "ToggleOverlayHpButton", "Label")?.GetComponent<Text>(),
            ["teamSummaryButtonLabel"] = FindChildGameObject(scene, "ToggleTeamSummaryButton", "Label")?.GetComponent<Text>(),
            ["statusText"] = GetComponentFromNamedObject<Text>(scene, "SettingsStatusText"),
        });

        var cameraGo = FindGameObject(scene, "BattleCameraRoot");
        BattleCameraController? cameraCtrl = null;
        if (cameraGo != null)
        {
            cameraCtrl = EnsureComponent<BattleCameraController>(cameraGo);
        }

        var controller = EnsureComponent<BattleScreenController>(controllerGo);
        Bind(controller, new Dictionary<string, Object?>
        {
            ["titleText"] = GetComponentFromNamedObject<Text>(scene, "TitleText"),
            ["allyHpText"] = GetComponentFromNamedObject<Text>(scene, "AllyHpText"),
            ["enemyHpText"] = GetComponentFromNamedObject<Text>(scene, "EnemyHpText"),
            ["logText"] = GetComponentFromNamedObject<Text>(scene, "LogText"),
            ["resultText"] = GetComponentFromNamedObject<Text>(scene, "ResultText"),
            ["speedText"] = GetComponentFromNamedObject<Text>(scene, "SpeedText"),
            ["statusText"] = GetComponentFromNamedObject<Text>(scene, "StatusText"),
            ["progressFill"] = GetComponentFromNamedObject<Image>(scene, "ProgressFill"),
            ["allySummaryPanel"] = GetComponentFromNamedObject<Image>(scene, "LeftPanel"),
            ["enemySummaryPanel"] = GetComponentFromNamedObject<Image>(scene, "RightPanel"),
            ["presentationController"] = presentation,
            ["settingsPanelController"] = settingsController,
            ["cameraController"] = cameraCtrl,
        });

        BindButton(scene, "Speed1Button", controller.SetSpeed1);
        BindButton(scene, "Speed2Button", controller.SetSpeed2);
        BindButton(scene, "Speed4Button", controller.SetSpeed4);
        BindButton(scene, "PauseButton", controller.TogglePause);
        BindButton(scene, "ContinueButton", controller.ContinueToReward);
        BindButton(scene, "RebattleButton", controller.RebattleNewSeed);
        BindButton(scene, "ReturnTownButton", controller.ReturnToTownDirect);
        BindButton(scene, "SettingsButton", settingsController.TogglePanel);
        BindButton(scene, "ToggleWorldHpButton", settingsController.ToggleWorldActorHp);
        BindButton(scene, "ToggleOverlayHpButton", settingsController.ToggleOverlayActorHp);
        BindButton(scene, "ToggleTeamSummaryButton", settingsController.ToggleTeamSummary);
    }

    private static void EnsureReward(Scene scene)
    {
        var controllerGo = FindGameObject(scene, "RewardScreenController");
        if (controllerGo == null)
        {
            return;
        }

        var controller = EnsureComponent<RewardScreenController>(controllerGo);
        Bind(controller, new Dictionary<string, Object?>
        {
            ["titleText"] = GetComponentFromNamedObject<Text>(scene, "TitleText"),
            ["summaryText"] = GetComponentFromNamedObject<Text>(scene, "SummaryText"),
            ["choicesText"] = GetComponentFromNamedObject<Text>(scene, "ChoicesText"),
            ["rewardCardsRoot"] = GetComponentFromNamedObject<RectTransform>(scene, "RewardCardsRoot"),
            ["statusText"] = GetComponentFromNamedObject<Text>(scene, "StatusText"),
        });

        BindButton(scene, "ChooseButton", controller.Choose0, "ChoiceCard1");
        BindButton(scene, "ChooseButton", controller.Choose1, "ChoiceCard2");
        BindButton(scene, "ChooseButton", controller.Choose2, "ChoiceCard3");
        BindButton(scene, "ReturnTownButton", controller.ReturnToTown);
    }

    private static void EnsureLocalizationOverlay(Scene scene)
    {
        if (!Application.isPlaying || GameSessionRoot.Instance?.Localization == null)
        {
            return;
        }

        var canvas = scene.GetRootGameObjects()
            .Select(root => root.GetComponentInChildren<Canvas>(true))
            .FirstOrDefault(candidate => candidate != null);
        if (canvas == null)
        {
            return;
        }

        GlobalLocalizationOverlayView.EnsureAttached(
            canvas.GetComponent<RectTransform>(),
            GameSessionRoot.Instance.Localization);
    }

    private static void EnsureStaticLocalizedText(Scene scene)
    {
        switch (scene.name)
        {
            case SceneNames.Town:
                EnsureLocalizedText(scene, "RerollButton", "Label", GameLocalizationTables.UITown, "ui.town.action.reroll");
                EnsureLocalizedText(scene, "SaveButton", "Label", GameLocalizationTables.UICommon, "ui.common.save");
                EnsureLocalizedText(scene, "LoadButton", "Label", GameLocalizationTables.UICommon, "ui.common.load");
                EnsureLocalizedText(scene, "DebugStartButton", "Label", GameLocalizationTables.UITown, "ui.town.action.debug_start");
                EnsureLocalizedText(scene, "QuickBattleButton", "Label", GameLocalizationTables.UITown, "ui.town.action.quick_battle");
                break;
            case SceneNames.Expedition:
                EnsureLocalizedText(scene, "NextBattleButton", "Label", GameLocalizationTables.UIExpedition, "ui.expedition.action.next_battle");
                EnsureLocalizedText(scene, "ReturnTownButton", "Label", GameLocalizationTables.UICommon, "ui.common.return_town");
                break;
            case SceneNames.Battle:
                EnsureLocalizedText(scene, "SettingsTitleText", null, GameLocalizationTables.UIBattle, "ui.battle.settings.title");
                EnsureLocalizedText(scene, "SettingsButton", "Label", GameLocalizationTables.UICommon, "ui.common.settings");
                EnsureLocalizedText(scene, "PauseButton", "Label", GameLocalizationTables.UICommon, "ui.common.pause");
                EnsureLocalizedText(scene, "ContinueButton", "Label", GameLocalizationTables.UICommon, "ui.common.continue");
                EnsureLocalizedText(scene, "RebattleButton", "Label", GameLocalizationTables.UIBattle, "ui.battle.action.rebattle");
                EnsureLocalizedText(scene, "ReturnTownButton", "Label", GameLocalizationTables.UICommon, "ui.common.return_town");
                break;
            case SceneNames.Reward:
                EnsureLocalizedText(scene, "ReturnTownButton", "Label", GameLocalizationTables.UICommon, "ui.common.return_town");
                break;
        }
    }

    private static void EnsureSharedUiFont(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            GameFontCatalog.ApplyToHierarchy(root.transform);
        }
    }

    private static void EnsureLocalizedText(Scene scene, string objectName, string? childName, string table, string entryKey)
    {
        var go = string.IsNullOrWhiteSpace(childName)
            ? FindGameObject(scene, objectName)
            : FindChildGameObject(scene, objectName, childName);
        if (go == null)
        {
            return;
        }

        var binder = EnsureComponent<UguiTextLocalizerBinder>(go);
        binder.Configure(table, entryKey);
    }

    private static void EnsureEventSystem(Scene scene)
    {
        var existing = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<EventSystem>(true))
            .FirstOrDefault();
        if (existing != null)
        {
            return;
        }

        var go = new GameObject("EventSystem");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<EventSystem>();

        var inputSystemUiType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputSystemUiType != null)
        {
            go.AddComponent(inputSystemUiType);
        }
        else
        {
            go.AddComponent<StandaloneInputModule>();
        }
    }

    private static void Bind(Component target, IReadOnlyDictionary<string, Object?> refs)
    {
        foreach (var pair in refs)
        {
            if (pair.Value == null)
            {
                continue;
            }

            var field = target.GetType().GetField(pair.Key, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null || !field.FieldType.IsAssignableFrom(pair.Value.GetType()))
            {
                continue;
            }

            field.SetValue(target, pair.Value);
        }
    }

    private static void BindButton(Scene scene, string buttonName, UnityAction action, string? parentName = null)
    {
        var buttonGo = parentName == null
            ? FindGameObject(scene, buttonName)
            : FindChildGameObject(scene, parentName, buttonName);
        if (buttonGo == null)
        {
            return;
        }

        var button = buttonGo.GetComponent<Button>();
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private static GameObject? FindGameObject(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var match = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(transform => transform.name == name);
            if (match != null)
            {
                return match.gameObject;
            }
        }

        return null;
    }

    private static GameObject? FindChildGameObject(Scene scene, string parentName, string childName)
    {
        var parent = FindGameObject(scene, parentName);
        if (parent == null)
        {
            return null;
        }

        var child = parent.transform.Find(childName);
        return child != null ? child.gameObject : null;
    }

    private static T? GetComponentFromNamedObject<T>(Scene scene, string objectName) where T : Component
    {
        return FindGameObject(scene, objectName)?.GetComponent<T>();
    }

    private static T EnsureComponent<T>(GameObject go) where T : Component
    {
        var existing = go.GetComponent<T>();
        if (existing != null)
        {
            return existing;
        }

        return go.AddComponent<T>();
    }
}
