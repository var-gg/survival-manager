using System.Collections.Generic;
using System.Linq;
using SM.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SM.Editor.Bootstrap;

public static class FirstPlayableSceneInstaller
{
    private const string ScenesRoot = "Assets/_Game/Scenes";

    [MenuItem("SM/Bootstrap/Rebuild First Playable Scenes")]
    public static void RebuildFirstPlayableScenes()
    {
        RebuildBoot();
        RebuildTown();
        RebuildExpedition();
        RebuildBattle();
        RebuildReward();
        EnsureBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("First playable scenes rebuilt/repaired.");
    }

    private static void RebuildBoot()
    {
        var scene = Open("Boot");
        EnsureSceneMarker(scene, "SceneMarker_Boot");
        EnsureCamera("Main Camera", true, new Vector3(0f, 0f, -10f), Quaternion.identity);
        EnsureCanvasRoot("BootCanvas");
        var bootstrapGo = ResetRootObject("GameBootstrap");
        EnsureComponent<GameBootstrap>(bootstrapGo);
        Save(scene);
    }

    private static void RebuildTown()
    {
        var scene = Open("Town");
        EnsureSceneMarker(scene, "SceneMarker_Town");
        EnsureCamera("Main Camera", true, new Vector3(0f, 0f, -10f), Quaternion.identity);
        var canvas = EnsureCanvasRoot("TownCanvas");
        EnsureEventSystem();

        var controllerGo = ResetChild(canvas.transform, "TownScreenController");
        var controller = EnsureComponent<TownScreenController>(controllerGo);

        var title = EnsureText(canvas.transform, "TitleText", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(700f, 40f), TextAnchor.MiddleCenter);
        var roster = EnsureText(canvas.transform, "RosterText", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -80f), new Vector2(420f, 360f), TextAnchor.UpperLeft);
        var recruit = EnsureText(canvas.transform, "RecruitText", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-80f, -80f), new Vector2(320f, 220f), TextAnchor.UpperLeft);
        var squad = EnsureText(canvas.transform, "SquadText", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -80f), new Vector2(260f, 220f), TextAnchor.UpperLeft);
        var deploy = EnsureText(canvas.transform, "DeployPreviewText", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20f, 40f), new Vector2(260f, 180f), TextAnchor.UpperLeft);
        var currency = EnsureText(canvas.transform, "CurrencyText", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(760f, 30f), TextAnchor.MiddleCenter);
        var status = EnsureText(canvas.transform, "StatusText", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(760f, 30f), TextAnchor.MiddleCenter);

        EnsureButton(canvas.transform, "RecruitButton1", "Recruit 1", new Vector2(0.5f, 0.5f), new Vector2(-120f, 80f), () => controller.RecruitOffer0());
        EnsureButton(canvas.transform, "RecruitButton2", "Recruit 2", new Vector2(0.5f, 0.5f), new Vector2(0f, 80f), () => controller.RecruitOffer1());
        EnsureButton(canvas.transform, "RecruitButton3", "Recruit 3", new Vector2(0.5f, 0.5f), new Vector2(120f, 80f), () => controller.RecruitOffer2());
        EnsureButton(canvas.transform, "RerollButton", "Reroll", new Vector2(0.5f, 0.5f), new Vector2(-180f, 20f), () => controller.RerollOffers());
        EnsureButton(canvas.transform, "SaveButton", "Save", new Vector2(0.5f, 0.5f), new Vector2(-60f, 20f), () => controller.SaveProfile());
        EnsureButton(canvas.transform, "LoadButton", "Load", new Vector2(0.5f, 0.5f), new Vector2(60f, 20f), () => controller.LoadProfile());
        EnsureButton(canvas.transform, "DebugStartButton", "Debug Start", new Vector2(0.5f, 0.5f), new Vector2(180f, 20f), () => controller.DebugStartExpedition());

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
    }

    private static void RebuildExpedition()
    {
        var scene = Open("Expedition");
        EnsureSceneMarker(scene, "SceneMarker_Expedition");
        EnsureCamera("Main Camera", true, new Vector3(0f, 0f, -10f), Quaternion.identity);
        var canvas = EnsureCanvasRoot("ExpeditionCanvas");
        EnsureEventSystem();

        var controllerGo = ResetChild(canvas.transform, "ExpeditionScreenController");
        var controller = EnsureComponent<ExpeditionScreenController>(controllerGo);

        var title = EnsureText(canvas.transform, "TitleText", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(700f, 40f), TextAnchor.MiddleCenter);
        var map = EnsureText(canvas.transform, "MapText", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -80f), new Vector2(340f, 300f), TextAnchor.UpperLeft);
        var position = EnsureText(canvas.transform, "PositionText", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(300f, 30f), TextAnchor.MiddleCenter);
        var reward = EnsureText(canvas.transform, "RewardText", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -80f), new Vector2(260f, 220f), TextAnchor.UpperLeft);
        var squad = EnsureText(canvas.transform, "SquadText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(340f, 220f), TextAnchor.UpperLeft);
        var status = EnsureText(canvas.transform, "StatusText", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(760f, 30f), TextAnchor.MiddleCenter);

        EnsureButton(canvas.transform, "NextBattleButton", "Next Battle", new Vector2(0.5f, 0f), new Vector2(-80f, 25f), () => controller.NextBattleOrAdvance());
        EnsureButton(canvas.transform, "ReturnTownButton", "Return Town", new Vector2(0.5f, 0f), new Vector2(80f, 25f), () => controller.ReturnToTown());

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
    }

    private static void RebuildBattle()
    {
        var scene = Open("Battle");
        EnsureSceneMarker(scene, "SceneMarker_Battle");
        EnsureCamera("Main Camera", true, new Vector3(0f, 8f, -8f), Quaternion.Euler(35f, 0f, 0f));
        var canvas = EnsureCanvasRoot("BattleCanvas");
        EnsureEventSystem();

        var controllerGo = ResetChild(canvas.transform, "BattleScreenController");
        var controller = EnsureComponent<BattleScreenController>(controllerGo);

        var title = EnsureText(canvas.transform, "TitleText", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(700f, 40f), TextAnchor.MiddleCenter);
        var allyHp = EnsureText(canvas.transform, "AllyHpText", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -80f), new Vector2(220f, 220f), TextAnchor.UpperLeft);
        var enemyHp = EnsureText(canvas.transform, "EnemyHpText", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -80f), new Vector2(220f, 220f), TextAnchor.UpperLeft);
        var log = EnsureText(canvas.transform, "LogText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(420f, 220f), TextAnchor.UpperLeft);
        var result = EnsureText(canvas.transform, "ResultText", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(300f, 30f), TextAnchor.MiddleCenter);
        var speed = EnsureText(canvas.transform, "SpeedText", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 75f), new Vector2(300f, 30f), TextAnchor.MiddleCenter);

        EnsureButton(canvas.transform, "Speed1Button", "x1", new Vector2(0.5f, 0f), new Vector2(-120f, 25f), () => controller.SetSpeed1());
        EnsureButton(canvas.transform, "Speed2Button", "x2", new Vector2(0.5f, 0f), new Vector2(0f, 25f), () => controller.SetSpeed2());
        EnsureButton(canvas.transform, "Speed4Button", "x4", new Vector2(0.5f, 0f), new Vector2(120f, 25f), () => controller.SetSpeed4());
        EnsureButton(canvas.transform, "ContinueButton", "Continue", new Vector2(1f, 0f), new Vector2(-90f, 25f), () => controller.ContinueToReward());

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
    }

    private static void RebuildReward()
    {
        var scene = Open("Reward");
        EnsureSceneMarker(scene, "SceneMarker_Reward");
        EnsureCamera("Main Camera", true, new Vector3(0f, 0f, -10f), Quaternion.identity);
        var canvas = EnsureCanvasRoot("RewardCanvas");
        EnsureEventSystem();

        var controllerGo = ResetChild(canvas.transform, "RewardScreenController");
        var controller = EnsureComponent<RewardScreenController>(controllerGo);

        var title = EnsureText(canvas.transform, "TitleText", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(700f, 40f), TextAnchor.MiddleCenter);
        var summary = EnsureText(canvas.transform, "SummaryText", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -80f), new Vector2(360f, 240f), TextAnchor.UpperLeft);
        var choices = EnsureText(canvas.transform, "ChoicesText", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(80f, -80f), new Vector2(360f, 240f), TextAnchor.UpperLeft);
        var status = EnsureText(canvas.transform, "StatusText", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(760f, 30f), TextAnchor.MiddleCenter);

        EnsureButton(canvas.transform, "Choice1Button", "Choice 1", new Vector2(0.5f, 0f), new Vector2(-180f, 25f), () => controller.Choose0());
        EnsureButton(canvas.transform, "Choice2Button", "Choice 2", new Vector2(0.5f, 0f), new Vector2(-40f, 25f), () => controller.Choose1());
        EnsureButton(canvas.transform, "Choice3Button", "Choice 3", new Vector2(0.5f, 0f), new Vector2(100f, 25f), () => controller.Choose2());
        EnsureButton(canvas.transform, "ReturnTownButton", "Return Town", new Vector2(1f, 0f), new Vector2(-90f, 25f), () => controller.ReturnToTown());

        Bind(controller, new Dictionary<string, Object>
        {
            ["titleText"] = title,
            ["summaryText"] = summary,
            ["choicesText"] = choices,
            ["statusText"] = status,
        });

        Save(scene);
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
        var ordered = new[] { "Boot", "Town", "Expedition", "Battle", "Reward" }
            .Select(name => new EditorBuildSettingsScene($"{ScenesRoot}/{name}.unity", true))
            .ToArray();
        EditorBuildSettings.scenes = ordered;
    }

    private static void EnsureSceneMarker(Scene scene, string markerName)
    {
        EnsureRootObject(markerName);
    }

    private static GameObject EnsureRootObject(string name)
    {
        var existing = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(x => x.name == name);
        if (existing != null)
        {
            return existing;
        }

        return new GameObject(name);
    }

    private static GameObject ResetRootObject(string name)
    {
        var existing = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(x => x.name == name);
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        return new GameObject(name);
    }

    private static GameObject EnsureChild(Transform parent, string name)
    {
        var child = parent.Find(name);
        if (child != null)
        {
            return child.gameObject;
        }

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    private static GameObject ResetChild(Transform parent, string name)
    {
        var child = parent.Find(name);
        if (child != null)
        {
            Object.DestroyImmediate(child.gameObject);
        }

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
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
        return component != null ? component : go.AddComponent<T>();
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
        var eventSystem = Object.FindObjectOfType<EventSystem>();
        if (eventSystem == null)
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }
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

    private static Text EnsureText(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta, TextAnchor alignment)
    {
        var go = EnsureChild(parent, name);
        var rect = EnsureComponent<RectTransform>(go);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        var text = EnsureComponent<Text>(go);
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 18;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.color = Color.white;
        text.text = name;
        return text;
    }

    private static Button EnsureButton(Transform parent, string name, string label, Vector2 anchor, Vector2 anchoredPosition, System.Action callback)
    {
        var go = EnsureChild(parent, name);
        var rect = EnsureComponent<RectTransform>(go);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(110f, 36f);

        var image = EnsureComponent<Image>(go);
        image.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);

        var button = EnsureComponent<Button>(go);
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => callback.Invoke());

        var labelGo = EnsureChild(go.transform, "Label");
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

    private static void Bind(Object target, Dictionary<string, Object> refs)
    {
        var so = new SerializedObject(target);
        foreach (var kv in refs)
        {
            var prop = so.FindProperty(kv.Key);
            if (prop != null)
            {
                prop.objectReferenceValue = kv.Value;
            }
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }
}
