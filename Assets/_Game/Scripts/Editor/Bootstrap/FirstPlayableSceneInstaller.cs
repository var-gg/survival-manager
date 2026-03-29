using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using SM.Unity;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SM.Editor.Bootstrap;

public static class FirstPlayableSceneInstaller
{
    private const string ScenesRoot = "Assets/_Game/Scenes";
    private static readonly string[] OrderedSceneNames = { "Boot", "Town", "Expedition", "Battle", "Reward" };

    [MenuItem("SM/Bootstrap/Repair First Playable Scenes")]
    public static void RepairFirstPlayableScenes()
    {
        RebuildBoot();
        RebuildTown();
        RebuildExpedition();
        RebuildBattle();
        RebuildReward();
        EnsureBuildSettings();
        ValidateSavedSceneContracts();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("First playable scenes repaired and validated.");
    }

    public static void RebuildFirstPlayableScenes()
    {
        RepairFirstPlayableScenes();
    }

    private static void RebuildBoot()
    {
        var scene = Open("Boot");
        EnsureSceneMarker(scene, "SceneMarker_Boot");
        EnsureCamera("Main Camera", true, new Vector3(0f, 0f, -10f), Quaternion.identity);
        var canvas = EnsureCanvasRoot("BootCanvas");
        var bootstrapGo = ResetRootObject("GameBootstrap");
        EnsureComponent<GameBootstrap>(bootstrapGo);
        EnsureText(canvas.transform, "BootTitleText", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -48f), new Vector2(720f, 44f), TextAnchor.MiddleCenter, 24, "Observer Playable Boot");
        EnsureText(canvas.transform, "BootStatusText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(760f, 80f), TextAnchor.MiddleCenter, 18, "GameBootstrap가 sample content를 확인한 뒤 Town으로 자동 진입한다.");
        EnsureText(canvas.transform, "BootHintText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -48f), new Vector2(760f, 60f), TextAnchor.MiddleCenter, 16, "block25 기준 scene repair/bootstrap 검증용 최소 UI");
        Save(scene);
        StabilizeComponentAfterReload<GameBootstrap>("Boot", "GameBootstrap");
    }

    private static void RebuildTown()
    {
        var scene = Open("Town");
        EnsureSceneMarker(scene, "SceneMarker_Town");
        EnsureCamera("Main Camera", true, new Vector3(0f, 0f, -10f), Quaternion.identity);
        var canvas = EnsureCanvasRoot("TownCanvas");
        EnsureEventSystem();

        var controllerGo = ResetUiChild(canvas.transform, "TownScreenController");
        var controller = EnsureComponent<TownScreenController>(controllerGo);

        var title = EnsureText(canvas.transform, "TitleText", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(700f, 40f), TextAnchor.MiddleCenter, 24, "Town Debug UI");
        var roster = EnsureText(canvas.transform, "RosterText", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -80f), new Vector2(420f, 360f), TextAnchor.UpperLeft);
        var recruit = EnsureText(canvas.transform, "RecruitText", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-80f, -80f), new Vector2(320f, 220f), TextAnchor.UpperLeft);
        var squad = EnsureText(canvas.transform, "SquadText", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -80f), new Vector2(260f, 220f), TextAnchor.UpperLeft);
        var deploy = EnsureText(canvas.transform, "DeployPreviewText", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20f, 40f), new Vector2(260f, 180f), TextAnchor.UpperLeft);
        var currency = EnsureText(canvas.transform, "CurrencyText", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(760f, 30f), TextAnchor.MiddleCenter);
        var status = EnsureText(canvas.transform, "StatusText", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(760f, 30f), TextAnchor.MiddleCenter);

        EnsureButton(canvas.transform, "RecruitButton1", "Recruit 1", new Vector2(0.5f, 0.5f), new Vector2(-120f, 80f), controller.RecruitOffer0);
        EnsureButton(canvas.transform, "RecruitButton2", "Recruit 2", new Vector2(0.5f, 0.5f), new Vector2(0f, 80f), controller.RecruitOffer1);
        EnsureButton(canvas.transform, "RecruitButton3", "Recruit 3", new Vector2(0.5f, 0.5f), new Vector2(120f, 80f), controller.RecruitOffer2);
        EnsureButton(canvas.transform, "RerollButton", "Reroll", new Vector2(0.5f, 0.5f), new Vector2(-180f, 20f), controller.RerollOffers);
        EnsureButton(canvas.transform, "SaveButton", "Save", new Vector2(0.5f, 0.5f), new Vector2(-60f, 20f), controller.SaveProfile);
        EnsureButton(canvas.transform, "LoadButton", "Load", new Vector2(0.5f, 0.5f), new Vector2(60f, 20f), controller.LoadProfile);
        EnsureButton(canvas.transform, "DebugStartButton", "Debug Start", new Vector2(0.5f, 0.5f), new Vector2(180f, 20f), controller.DebugStartExpedition);

        Bind(controller, new Dictionary<string, Object>
        {
            ["titleText"] = title,
            ["rosterText"] = roster,
            ["recruitText"] = recruit,
            ["squadText"] = squad,
            ["deployPreviewText"] = deploy,
            ["currencyText"] = currency,
            ["statusText"] = status,
        });

        Save(scene);
        StabilizeControllerAfterReload<TownScreenController>("Town", "TownScreenController", new Dictionary<string, string>
        {
            ["titleText"] = "TitleText",
            ["rosterText"] = "RosterText",
            ["recruitText"] = "RecruitText",
            ["squadText"] = "SquadText",
            ["deployPreviewText"] = "DeployPreviewText",
            ["currencyText"] = "CurrencyText",
            ["statusText"] = "StatusText",
        });
    }

    private static void RebuildExpedition()
    {
        var scene = Open("Expedition");
        EnsureSceneMarker(scene, "SceneMarker_Expedition");
        EnsureCamera("Main Camera", true, new Vector3(0f, 0f, -10f), Quaternion.identity);
        var canvas = EnsureCanvasRoot("ExpeditionCanvas");
        EnsureEventSystem();

        var controllerGo = ResetUiChild(canvas.transform, "ExpeditionScreenController");
        var controller = EnsureComponent<ExpeditionScreenController>(controllerGo);

        var title = EnsureText(canvas.transform, "TitleText", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(700f, 40f), TextAnchor.MiddleCenter, 24, "Expedition Debug UI");
        var map = EnsureText(canvas.transform, "MapText", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -80f), new Vector2(340f, 300f), TextAnchor.UpperLeft);
        var position = EnsureText(canvas.transform, "PositionText", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(300f, 30f), TextAnchor.MiddleCenter);
        var reward = EnsureText(canvas.transform, "RewardText", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -80f), new Vector2(260f, 220f), TextAnchor.UpperLeft);
        var squad = EnsureText(canvas.transform, "SquadText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(340f, 220f), TextAnchor.UpperLeft);
        var status = EnsureText(canvas.transform, "StatusText", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(760f, 30f), TextAnchor.MiddleCenter);

        EnsureButton(canvas.transform, "NextBattleButton", "Next Battle", new Vector2(0.5f, 0f), new Vector2(-80f, 25f), controller.NextBattleOrAdvance);
        EnsureButton(canvas.transform, "ReturnTownButton", "Return Town", new Vector2(0.5f, 0f), new Vector2(80f, 25f), controller.ReturnToTown);

        Bind(controller, new Dictionary<string, Object>
        {
            ["titleText"] = title,
            ["mapText"] = map,
            ["positionText"] = position,
            ["rewardText"] = reward,
            ["squadText"] = squad,
            ["statusText"] = status,
        });

        Save(scene);
        StabilizeControllerAfterReload<ExpeditionScreenController>("Expedition", "ExpeditionScreenController", new Dictionary<string, string>
        {
            ["titleText"] = "TitleText",
            ["mapText"] = "MapText",
            ["positionText"] = "PositionText",
            ["rewardText"] = "RewardText",
            ["squadText"] = "SquadText",
            ["statusText"] = "StatusText",
        });
    }

    private static void RebuildBattle()
    {
        var scene = Open("Battle");
        EnsureSceneMarker(scene, "SceneMarker_Battle");
        EnsureCamera("Main Camera", true, new Vector3(0f, 8f, -8f), Quaternion.Euler(35f, 0f, 0f));
        var canvas = EnsureCanvasRoot("BattleCanvas");
        EnsureEventSystem();

        var controllerGo = ResetUiChild(canvas.transform, "BattleScreenController");
        var controller = EnsureComponent<BattleScreenController>(controllerGo);

        var title = EnsureText(canvas.transform, "TitleText", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(700f, 40f), TextAnchor.MiddleCenter, 24, "Battle Debug UI");
        var allyHp = EnsureText(canvas.transform, "AllyHpText", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -80f), new Vector2(220f, 220f), TextAnchor.UpperLeft);
        var enemyHp = EnsureText(canvas.transform, "EnemyHpText", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -80f), new Vector2(220f, 220f), TextAnchor.UpperLeft);
        var log = EnsureText(canvas.transform, "LogText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(420f, 220f), TextAnchor.UpperLeft);
        var result = EnsureText(canvas.transform, "ResultText", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(300f, 30f), TextAnchor.MiddleCenter);
        var speed = EnsureText(canvas.transform, "SpeedText", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 75f), new Vector2(300f, 30f), TextAnchor.MiddleCenter);

        EnsureButton(canvas.transform, "Speed1Button", "x1", new Vector2(0.5f, 0f), new Vector2(-120f, 25f), controller.SetSpeed1);
        EnsureButton(canvas.transform, "Speed2Button", "x2", new Vector2(0.5f, 0f), new Vector2(0f, 25f), controller.SetSpeed2);
        EnsureButton(canvas.transform, "Speed4Button", "x4", new Vector2(0.5f, 0f), new Vector2(120f, 25f), controller.SetSpeed4);
        EnsureButton(canvas.transform, "ContinueButton", "Continue", new Vector2(1f, 0f), new Vector2(-90f, 25f), controller.ContinueToReward);

        Bind(controller, new Dictionary<string, Object>
        {
            ["titleText"] = title,
            ["allyHpText"] = allyHp,
            ["enemyHpText"] = enemyHp,
            ["logText"] = log,
            ["resultText"] = result,
            ["speedText"] = speed,
        });

        Save(scene);
        StabilizeControllerAfterReload<BattleScreenController>("Battle", "BattleScreenController", new Dictionary<string, string>
        {
            ["titleText"] = "TitleText",
            ["allyHpText"] = "AllyHpText",
            ["enemyHpText"] = "EnemyHpText",
            ["logText"] = "LogText",
            ["resultText"] = "ResultText",
            ["speedText"] = "SpeedText",
        });
    }

    private static void RebuildReward()
    {
        var scene = Open("Reward");
        EnsureSceneMarker(scene, "SceneMarker_Reward");
        EnsureCamera("Main Camera", true, new Vector3(0f, 0f, -10f), Quaternion.identity);
        var canvas = EnsureCanvasRoot("RewardCanvas");
        EnsureEventSystem();

        var controllerGo = ResetUiChild(canvas.transform, "RewardScreenController");
        var controller = EnsureComponent<RewardScreenController>(controllerGo);

        var title = EnsureText(canvas.transform, "TitleText", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(700f, 40f), TextAnchor.MiddleCenter, 24, "Reward Debug UI");
        var summary = EnsureText(canvas.transform, "SummaryText", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -80f), new Vector2(360f, 240f), TextAnchor.UpperLeft);
        var choices = EnsureText(canvas.transform, "ChoicesText", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(80f, -80f), new Vector2(360f, 240f), TextAnchor.UpperLeft);
        var status = EnsureText(canvas.transform, "StatusText", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(760f, 30f), TextAnchor.MiddleCenter);

        EnsureButton(canvas.transform, "Choice1Button", "Choice 1", new Vector2(0.5f, 0f), new Vector2(-180f, 25f), controller.Choose0);
        EnsureButton(canvas.transform, "Choice2Button", "Choice 2", new Vector2(0.5f, 0f), new Vector2(-40f, 25f), controller.Choose1);
        EnsureButton(canvas.transform, "Choice3Button", "Choice 3", new Vector2(0.5f, 0f), new Vector2(100f, 25f), controller.Choose2);
        EnsureButton(canvas.transform, "ReturnTownButton", "Return Town", new Vector2(1f, 0f), new Vector2(-90f, 25f), controller.ReturnToTown);

        Bind(controller, new Dictionary<string, Object>
        {
            ["titleText"] = title,
            ["summaryText"] = summary,
            ["choicesText"] = choices,
            ["statusText"] = status,
        });

        Save(scene);
        StabilizeControllerAfterReload<RewardScreenController>("Reward", "RewardScreenController", new Dictionary<string, string>
        {
            ["titleText"] = "TitleText",
            ["summaryText"] = "SummaryText",
            ["choicesText"] = "ChoicesText",
            ["statusText"] = "StatusText",
        });
    }

    private static Scene Open(string sceneName)
    {
        var path = $"{ScenesRoot}/{sceneName}.unity";
        return EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
    }

    private static void Save(Scene scene)
    {
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void EnsureBuildSettings()
    {
        var ordered = OrderedSceneNames
            .Select(name => new EditorBuildSettingsScene($"{ScenesRoot}/{name}.unity", true))
            .ToArray();
        EditorBuildSettings.scenes = ordered;
    }

    private static void EnsureSceneMarker(Scene scene, string markerName)
    {
        EnsureUniqueRootObject(markerName);
    }

    private static void ValidateSavedSceneContracts()
    {
        ValidateScene("Boot", new[] { "GameBootstrap", "BootCanvas", "Main Camera" });
        ValidateScene("Town", new[] { "TownCanvas", "EventSystem", "TownScreenController" }, typeof(TownScreenController));
        ValidateScene("Expedition", new[] { "ExpeditionCanvas", "EventSystem", "ExpeditionScreenController" }, typeof(ExpeditionScreenController));
        ValidateScene("Battle", new[] { "BattleCanvas", "EventSystem", "BattleScreenController" }, typeof(BattleScreenController));
        ValidateScene("Reward", new[] { "RewardCanvas", "EventSystem", "RewardScreenController" }, typeof(RewardScreenController));
    }

    private static void StabilizeControllerAfterReload<T>(string sceneName, string controllerObjectName, IReadOnlyDictionary<string, string> textBindings) where T : Component
    {
        var scene = Open(sceneName);
        var controllerGo = FindGameObject(scene, controllerObjectName)
            ?? throw new System.InvalidOperationException($"{controllerObjectName} object disappeared during repair.");
        var controller = EnsureComponent<T>(controllerGo);
        var refs = textBindings.ToDictionary(pair => pair.Key, pair => (Object)RequireText(scene, pair.Value));
        Bind(controller, refs);

        Save(scene);
    }

    private static void StabilizeComponentAfterReload<T>(string sceneName, string objectName) where T : Component
    {
        var scene = Open(sceneName);
        var go = FindGameObject(scene, objectName)
            ?? throw new System.InvalidOperationException($"{objectName} object disappeared during repair.");
        EnsureComponent<T>(go);
        Save(scene);
    }

    private static void ValidateScene(string sceneName, IReadOnlyList<string> requiredObjectNames, System.Type? requiredComponentType = null)
    {
        var scene = Open(sceneName);
        foreach (var objectName in requiredObjectNames)
        {
            if (FindGameObject(scene, objectName) == null)
            {
                throw new System.InvalidOperationException($"Scene repair failed. Missing '{objectName}' in {scene.path}");
            }
        }

        if (requiredComponentType != null)
        {
            var sceneText = File.ReadAllText(scene.path);
            if (!sceneText.Contains(requiredComponentType.FullName))
            {
                throw new System.InvalidOperationException($"Scene repair failed. Missing component '{requiredComponentType.Name}' in {scene.path}");
            }
        }
    }

    private static GameObject? FindGameObject(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var match = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == name);
            if (match != null)
            {
                return match.gameObject;
            }
        }

        return null;
    }

    private static Text RequireText(Scene scene, string name)
    {
        var go = FindGameObject(scene, name)
            ?? throw new System.InvalidOperationException($"Missing text object '{name}' in {scene.path}");
        var text = go.GetComponent<Text>();
        if (text == null)
        {
            throw new System.InvalidOperationException($"Missing Text component on '{name}' in {scene.path}");
        }

        return text;
    }

    private static GameObject EnsureRootObject(string name)
    {
        return EnsureUniqueRootObject(name);
    }

    private static GameObject ResetChild(Transform parent, string name)
    {
        return ResetUiChild(parent, name);
    }

    private static GameObject ResetRootObject(string name)
    {
        var existingRoots = SceneManager.GetActiveScene().GetRootGameObjects().Where(x => x.name == name).ToList();
        foreach (var existing in existingRoots)
        {
            Object.DestroyImmediate(existing);
        }

        return new GameObject(name);
    }

    private static GameObject EnsureUniqueRootObject(string name)
    {
        var existingRoots = SceneManager.GetActiveScene().GetRootGameObjects().Where(x => x.name == name).ToList();
        var go = existingRoots.FirstOrDefault();
        if (go == null)
        {
            return new GameObject(name);
        }

        for (var i = 1; i < existingRoots.Count; i++)
        {
            Object.DestroyImmediate(existingRoots[i]);
        }

        return go;
    }

    private static T EnsureComponent<T>(GameObject go) where T : Component
    {
#if UNITY_EDITOR
        if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go) > 0)
        {
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
        }
#endif
        var component = go.GetComponent<T>();
        if (component != null)
        {
            return component;
        }

#if UNITY_EDITOR
        return (T)Undo.AddComponent(go, typeof(T));
#else
        return go.AddComponent<T>();
#endif
    }

    private static Canvas EnsureCanvasRoot(string name)
    {
        var go = EnsureRootObject(name);
        var canvas = EnsureComponent<Canvas>(go);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        EnsureComponent<CanvasScaler>(go);
        EnsureComponent<GraphicRaycaster>(go);
        return canvas;
    }

    private static void EnsureEventSystem()
    {
        var existing = Object.FindObjectsOfType<EventSystem>();
        if (existing.Length == 0)
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
            return;
        }

        for (var i = 1; i < existing.Length; i++)
        {
            Object.DestroyImmediate(existing[i].gameObject);
        }

        existing[0].name = "EventSystem";
        EnsureComponent<StandaloneInputModule>(existing[0].gameObject);
    }

    private static Camera EnsureCamera(string name, bool tagMain, Vector3 position, Quaternion rotation)
    {
        var go = EnsureRootObject(name);
        var camera = EnsureComponent<Camera>(go);
        go.transform.position = position;
        go.transform.rotation = rotation;
        if (tagMain)
        {
            go.tag = "MainCamera";
        }
        return camera;
    }

    private static Text EnsureText(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta, TextAnchor alignment, int fontSize = 18, string? initialText = null)
    {
        var go = EnsureUiChild(parent, name);
        var rect = EnsureComponent<RectTransform>(go);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        var text = EnsureComponent<Text>(go);
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.color = Color.white;
        text.text = initialText ?? name;
        return text;
    }

    private static Button EnsureButton(Transform parent, string name, string label, Vector2 anchor, Vector2 anchoredPosition, UnityAction callback)
    {
        var go = EnsureUiChild(parent, name);
        var rect = EnsureComponent<RectTransform>(go);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(110f, 36f);

        var image = EnsureComponent<Image>(go);
        image.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);

        var button = EnsureComponent<Button>(go);
        BindPersistentClick(button, callback);

        var labelGo = EnsureUiChild(go.transform, "Label");
        var labelRect = EnsureComponent<RectTransform>(labelGo);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        var labelText = EnsureComponent<Text>(labelGo);
        labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        labelText.fontSize = 16;
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.color = Color.white;
        labelText.text = label;
        return button;
    }

    private static void BindPersistentClick(Button button, UnityAction callback)
    {
        button.onClick.RemoveAllListeners();
        while (button.onClick.GetPersistentEventCount() > 0)
        {
            UnityEventTools.RemovePersistentListener(button.onClick, 0);
        }

        UnityEventTools.AddPersistentListener(button.onClick, callback);
        EditorUtility.SetDirty(button);
    }

    private static GameObject EnsureUiChild(Transform parent, string name)
    {
        var matches = parent.Cast<Transform>().Where(x => x.name == name).ToList();
        var child = matches.FirstOrDefault();
        if (child == null || child.GetComponent<RectTransform>() == null)
        {
            if (child != null)
            {
                Object.DestroyImmediate(child.gameObject);
            }

            return CreateUiChild(parent, name);
        }

        for (var i = 1; i < matches.Count; i++)
        {
            Object.DestroyImmediate(matches[i].gameObject);
        }

        return child.gameObject;
    }

    private static GameObject ResetUiChild(Transform parent, string name)
    {
        foreach (var child in parent.Cast<Transform>().Where(x => x.name == name).ToList())
        {
            Object.DestroyImmediate(child.gameObject);
        }

        return CreateUiChild(parent, name);
    }

    private static GameObject CreateUiChild(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void Bind(Object target, Dictionary<string, Object> refs)
    {
        Undo.RecordObject(target, $"Bind {target.name} references");
        foreach (var kv in refs)
        {
            var field = target.GetType().GetField(kv.Key, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null && kv.Value != null && field.FieldType.IsAssignableFrom(kv.Value.GetType()))
            {
                field.SetValue(target, kv.Value);
            }
        }
        EditorUtility.SetDirty(target);
    }
}
