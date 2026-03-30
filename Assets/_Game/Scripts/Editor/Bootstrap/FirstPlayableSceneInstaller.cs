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
        var scene = CreateFreshScene("Boot");
        EnsureSceneMarker(scene, "SceneMarker_Boot");
        EnsureCamera("Main Camera", true, new Vector3(0f, 0f, -10f), Quaternion.identity);
        var canvas = EnsureCanvasRoot("BootCanvas");
        var bootstrapGo = ResetRootObject("GameBootstrap");
        EnsureComponent<GameBootstrap>(bootstrapGo);
        EnsureText(canvas.transform, "BootTitleText", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -48f), new Vector2(720f, 44f), TextAnchor.MiddleCenter, 24, "Observer Playable Boot");
        EnsureText(canvas.transform, "BootStatusText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(760f, 80f), TextAnchor.MiddleCenter, 18, "GameBootstrap가 sample content를 확인한 뒤 Town으로 자동 진입한다.");
        EnsureText(canvas.transform, "BootHintText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -48f), new Vector2(760f, 60f), TextAnchor.MiddleCenter, 16, "block25 기준 scene repair/bootstrap 검증용 최소 UI");
        Save(scene);
    }

    private static void RebuildTown()
    {
        var scene = CreateFreshScene("Town");
        EnsureSceneMarker(scene, "SceneMarker_Town");
        EnsureCamera("Main Camera", true, new Vector3(0f, 0f, -10f), Quaternion.identity);
        var canvas = EnsureCanvasRoot("TownCanvas");
        EnsureEventSystem();

        var controllerGo = ResetUiChild(canvas.transform, "TownScreenController");
        var controller = EnsureComponent<TownScreenController>(controllerGo);

        EnsurePanel(canvas.transform, "RosterPanel", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(180f, 10f), new Vector2(340f, 440f), new Color(0.12f, 0.15f, 0.22f, 0.92f));
        EnsurePanel(canvas.transform, "SquadPanel", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-160f, 120f), new Vector2(300f, 220f), new Color(0.14f, 0.15f, 0.26f, 0.92f));
        EnsurePanel(canvas.transform, "DeployPanel", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-160f, -95f), new Vector2(300f, 190f), new Color(0.14f, 0.15f, 0.26f, 0.92f));

        var title = EnsureText(canvas.transform, "TitleText", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(700f, 40f), TextAnchor.MiddleCenter, 24, "Town Operator UI");
        var roster = EnsureText(canvas.transform, "RosterText", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(180f, 10f), new Vector2(300f, 400f), TextAnchor.UpperLeft);
        var recruit = EnsureText(canvas.transform, "RecruitText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 165f), new Vector2(400f, 44f), TextAnchor.MiddleCenter, 18, "Recruit 후보 3개");
        var recruitCardsRoot = EnsurePanel(canvas.transform, "RecruitCardsRoot", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(450f, 360f), new Color(0.13f, 0.17f, 0.21f, 0.9f)).rectTransform;
        var squad = EnsureText(canvas.transform, "SquadText", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-160f, 120f), new Vector2(260f, 180f), TextAnchor.UpperLeft);
        var deploy = EnsureText(canvas.transform, "DeployPreviewText", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-160f, -95f), new Vector2(260f, 150f), TextAnchor.UpperLeft);
        var currency = EnsureText(canvas.transform, "CurrencyText", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(840f, 30f), TextAnchor.MiddleCenter);
        var status = EnsureText(canvas.transform, "StatusText", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(840f, 30f), TextAnchor.MiddleCenter);

        var recruitCard1 = EnsurePanel(recruitCardsRoot, "RecruitCard1", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-140f, -160f), new Vector2(124f, 250f), new Color(0.19f, 0.21f, 0.29f, 0.96f)).rectTransform;
        var recruitCard2 = EnsurePanel(recruitCardsRoot, "RecruitCard2", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(124f, 250f), new Color(0.19f, 0.21f, 0.29f, 0.96f)).rectTransform;
        var recruitCard3 = EnsurePanel(recruitCardsRoot, "RecruitCard3", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(140f, -160f), new Vector2(124f, 250f), new Color(0.19f, 0.21f, 0.29f, 0.96f)).rectTransform;

        EnsureText(recruitCard1, "TitleText", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(110f, 40f), TextAnchor.MiddleCenter, 16, "Recruit 1");
        EnsureText(recruitCard1, "BodyText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 18f), new Vector2(110f, 120f), TextAnchor.UpperCenter, 14, "Body");
        EnsureButton(recruitCard1, "RecruitButton", "Recruit 1", new Vector2(0.5f, 0f), new Vector2(0f, 26f), controller.RecruitOffer0);

        EnsureText(recruitCard2, "TitleText", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(110f, 40f), TextAnchor.MiddleCenter, 16, "Recruit 2");
        EnsureText(recruitCard2, "BodyText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 18f), new Vector2(110f, 120f), TextAnchor.UpperCenter, 14, "Body");
        EnsureButton(recruitCard2, "RecruitButton", "Recruit 2", new Vector2(0.5f, 0f), new Vector2(0f, 26f), controller.RecruitOffer1);

        EnsureText(recruitCard3, "TitleText", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(110f, 40f), TextAnchor.MiddleCenter, 16, "Recruit 3");
        EnsureText(recruitCard3, "BodyText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 18f), new Vector2(110f, 120f), TextAnchor.UpperCenter, 14, "Body");
        EnsureButton(recruitCard3, "RecruitButton", "Recruit 3", new Vector2(0.5f, 0f), new Vector2(0f, 26f), controller.RecruitOffer2);

        EnsureButton(canvas.transform, "RerollButton", "Reroll", new Vector2(0.5f, 0f), new Vector2(-240f, 115f), controller.RerollOffers);
        EnsureButton(canvas.transform, "SaveButton", "Save", new Vector2(0.5f, 0f), new Vector2(-120f, 115f), controller.SaveProfile);
        EnsureButton(canvas.transform, "LoadButton", "Load", new Vector2(0.5f, 0f), new Vector2(0f, 115f), controller.LoadProfile);
        EnsureButton(canvas.transform, "DebugStartButton", "Debug Start", new Vector2(0.5f, 0f), new Vector2(120f, 115f), controller.DebugStartExpedition);
        EnsureButton(canvas.transform, "QuickBattleButton", "Quick Battle", new Vector2(0.5f, 0f), new Vector2(240f, 115f), controller.QuickBattle);

        Bind(controller, new Dictionary<string, Object>
        {
            ["titleText"] = title,
            ["rosterText"] = roster,
            ["recruitText"] = recruit,
            ["recruitCardsRoot"] = recruitCardsRoot,
            ["squadText"] = squad,
            ["deployPreviewText"] = deploy,
            ["currencyText"] = currency,
            ["statusText"] = status,
        });

        Save(scene);
    }

    private static void RebuildExpedition()
    {
        var scene = CreateFreshScene("Expedition");
        EnsureSceneMarker(scene, "SceneMarker_Expedition");
        EnsureCamera("Main Camera", true, new Vector3(0f, 0f, -10f), Quaternion.identity);
        var canvas = EnsureCanvasRoot("ExpeditionCanvas");
        EnsureEventSystem();

        var controllerGo = ResetUiChild(canvas.transform, "ExpeditionScreenController");
        var controller = EnsureComponent<ExpeditionScreenController>(controllerGo);

        EnsurePanel(canvas.transform, "MapPanel", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(170f, 0f), new Vector2(300f, 380f), new Color(0.12f, 0.16f, 0.23f, 0.92f));
        var nodeTrackRoot = EnsurePanel(canvas.transform, "NodeTrackRoot", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 115f), new Vector2(440f, 120f), new Color(0.12f, 0.16f, 0.23f, 0.92f)).rectTransform;
        EnsurePanel(canvas.transform, "SquadPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -75f), new Vector2(360f, 220f), new Color(0.12f, 0.16f, 0.23f, 0.92f));
        EnsurePanel(canvas.transform, "RewardPanel", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-150f, 0f), new Vector2(260f, 380f), new Color(0.12f, 0.16f, 0.23f, 0.92f));

        var title = EnsureText(canvas.transform, "TitleText", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(700f, 40f), TextAnchor.MiddleCenter, 24, "Expedition Operator UI");
        var map = EnsureText(canvas.transform, "MapText", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(170f, 0f), new Vector2(260f, 330f), TextAnchor.UpperLeft);
        var position = EnsureText(canvas.transform, "PositionText", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(420f, 30f), TextAnchor.MiddleCenter);
        var reward = EnsureText(canvas.transform, "RewardText", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-150f, 0f), new Vector2(220f, 330f), TextAnchor.UpperLeft);
        var squad = EnsureText(canvas.transform, "SquadText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -75f), new Vector2(320f, 180f), TextAnchor.UpperLeft);
        var status = EnsureText(canvas.transform, "StatusText", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(840f, 30f), TextAnchor.MiddleCenter);

        for (var i = 0; i < 5; i++)
        {
            var nodeBox = EnsurePanel(nodeTrackRoot, $"NodeBox{i + 1}", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-176f + (i * 88f), 0f), new Vector2(78f, 108f), new Color(0.18f, 0.22f, 0.34f, 0.95f)).rectTransform;
            EnsureText(nodeBox, "TitleText", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(70f, 32f), TextAnchor.MiddleCenter, 13, $"Node {i + 1}");
            EnsureText(nodeBox, "RewardText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(70f, 42f), TextAnchor.MiddleCenter, 11, "Reward");
            var selectButton = EnsureButton(nodeBox, "SelectButton", "Route", new Vector2(0.5f, 0f), new Vector2(0f, 18f), i switch
            {
                0 => controller.SelectNode1,
                1 => controller.SelectNode2,
                2 => controller.SelectNode3,
                3 => controller.SelectNode4,
                _ => controller.SelectNode5
            });
            selectButton.GetComponent<RectTransform>().sizeDelta = new Vector2(62f, 24f);
            var selectLabel = selectButton.transform.Find("Label")?.GetComponent<Text>();
            if (selectLabel != null)
            {
                selectLabel.fontSize = 12;
            }
        }

        EnsureButton(canvas.transform, "NextBattleButton", "Next Battle", new Vector2(0.5f, 0f), new Vector2(-80f, 25f), controller.NextBattleOrAdvance);
        EnsureButton(canvas.transform, "ReturnTownButton", "Return Town", new Vector2(0.5f, 0f), new Vector2(80f, 25f), controller.ReturnToTown);

        Bind(controller, new Dictionary<string, Object>
        {
            ["titleText"] = title,
            ["mapText"] = map,
            ["nodeTrackRoot"] = nodeTrackRoot,
            ["positionText"] = position,
            ["rewardText"] = reward,
            ["squadText"] = squad,
            ["statusText"] = status,
        });

        Save(scene);
    }

    private static void RebuildBattle()
    {
        var scene = CreateFreshScene("Battle");
        EnsureSceneMarker(scene, "SceneMarker_Battle");
        EnsureCamera("Main Camera", true, new Vector3(0f, 8f, -8f), Quaternion.Euler(35f, 0f, 0f));
        var canvas = EnsureCanvasRoot("BattleCanvas");
        EnsureEventSystem();
        var battleStageRoot = EnsureRootObject("BattleStageRoot");

        var controllerGo = ResetUiChild(canvas.transform, "BattleScreenController");
        var controller = EnsureComponent<BattleScreenController>(controllerGo);
        var presentationGo = ResetUiChild(canvas.transform, "BattlePresentationRoot");
        var presentation = EnsureComponent<BattlePresentationController>(presentationGo);
        var settingsControllerGo = ResetUiChild(canvas.transform, "BattleSettingsController");
        var settingsController = EnsureComponent<BattleSettingsPanelController>(settingsControllerGo);

        var actorOverlayRoot = EnsurePanel(presentationGo.transform, "ActorOverlayRoot", new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0f)).rectTransform;
        actorOverlayRoot.offsetMin = Vector2.zero;
        actorOverlayRoot.offsetMax = Vector2.zero;

        var leftPanel = EnsurePanel(canvas.transform, "LeftPanel", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(150f, 170f), new Vector2(240f, 165f), new Color(0.12f, 0.16f, 0.22f, 0.92f));
        var rightPanel = EnsurePanel(canvas.transform, "RightPanel", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-150f, 170f), new Vector2(240f, 165f), new Color(0.12f, 0.16f, 0.22f, 0.92f));
        EnsurePanel(canvas.transform, "LogPanel", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(540f, 150f), new Color(0.12f, 0.16f, 0.22f, 0.92f));
        var settingsPanel = EnsurePanel(canvas.transform, "SettingsPanel", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-130f, -118f), new Vector2(230f, 172f), new Color(0.10f, 0.13f, 0.18f, 0.96f)).rectTransform;
        var progressTrack = EnsurePanel(canvas.transform, "ProgressTrack", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 145f), new Vector2(360f, 18f), new Color(0.08f, 0.08f, 0.08f, 0.9f));
        var progressFill = EnsurePanel(progressTrack.transform, "ProgressFill", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(348f, 10f), new Color(0.85f, 0.58f, 0.22f, 0.95f));
        progressFill.type = Image.Type.Filled;
        progressFill.fillMethod = Image.FillMethod.Horizontal;
        progressFill.fillOrigin = 0;
        progressFill.fillAmount = 0f;

        var title = EnsureText(canvas.transform, "TitleText", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(700f, 40f), TextAnchor.MiddleCenter, 24, "Battle Observer UI");
        var allyHp = EnsureText(canvas.transform, "AllyHpText", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(150f, 170f), new Vector2(200f, 140f), TextAnchor.UpperLeft);
        var enemyHp = EnsureText(canvas.transform, "EnemyHpText", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-150f, 170f), new Vector2(200f, 140f), TextAnchor.UpperLeft);
        var log = EnsureText(canvas.transform, "LogText", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(500f, 120f), TextAnchor.UpperLeft, 14);
        var result = EnsureText(canvas.transform, "ResultText", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(320f, 30f), TextAnchor.MiddleCenter);
        var speed = EnsureText(canvas.transform, "SpeedText", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 75f), new Vector2(320f, 30f), TextAnchor.MiddleCenter);
        var status = EnsureText(canvas.transform, "StatusText", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 180f), new Vector2(640f, 30f), TextAnchor.MiddleCenter);
        EnsureText(settingsPanel, "SettingsTitleText", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(200f, 24f), TextAnchor.MiddleCenter, 16, "Battle View Settings");
        var worldHpButton = EnsureButton(settingsPanel, "ToggleWorldHpButton", "Actor HP ON", new Vector2(0.5f, 1f), new Vector2(0f, -55f), settingsController.ToggleWorldActorHp);
        var overlayHpButton = EnsureButton(settingsPanel, "ToggleOverlayHpButton", "Overlay HP OFF", new Vector2(0.5f, 1f), new Vector2(0f, -94f), settingsController.ToggleOverlayActorHp);
        var teamSummaryButton = EnsureButton(settingsPanel, "ToggleTeamSummaryButton", "Team Summary OFF", new Vector2(0.5f, 1f), new Vector2(0f, -133f), settingsController.ToggleTeamSummary);
        var settingsStatus = EnsureText(settingsPanel, "SettingsStatusText", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(200f, 32f), TextAnchor.MiddleCenter, 13, "전투 표시 옵션");
        var settingsButton = EnsureButton(canvas.transform, "SettingsButton", "Settings", new Vector2(1f, 1f), new Vector2(-88f, -26f), settingsController.TogglePanel);
        settingsButton.GetComponent<RectTransform>().sizeDelta = new Vector2(100f, 32f);

        EnsureButton(canvas.transform, "Speed1Button", "x1", new Vector2(0.5f, 0f), new Vector2(-120f, 25f), controller.SetSpeed1);
        EnsureButton(canvas.transform, "Speed2Button", "x2", new Vector2(0.5f, 0f), new Vector2(0f, 25f), controller.SetSpeed2);
        EnsureButton(canvas.transform, "Speed4Button", "x4", new Vector2(0.5f, 0f), new Vector2(120f, 25f), controller.SetSpeed4);
        EnsureButton(canvas.transform, "PauseButton", "Pause", new Vector2(0.5f, 0f), new Vector2(240f, 25f), controller.TogglePause);
        EnsureButton(canvas.transform, "ContinueButton", "Continue", new Vector2(1f, 0f), new Vector2(-90f, 25f), controller.ContinueToReward);

        Bind(controller, new Dictionary<string, Object>
        {
            ["titleText"] = title,
            ["allyHpText"] = allyHp,
            ["enemyHpText"] = enemyHp,
            ["logText"] = log,
            ["resultText"] = result,
            ["speedText"] = speed,
            ["statusText"] = status,
            ["progressFill"] = progressFill,
            ["allySummaryPanel"] = leftPanel,
            ["enemySummaryPanel"] = rightPanel,
            ["presentationController"] = presentation,
            ["settingsPanelController"] = settingsController,
        });

        Bind(presentation, new Dictionary<string, Object>
        {
            ["battleStageRoot"] = battleStageRoot.transform,
            ["actorOverlayRoot"] = actorOverlayRoot,
        });

        Bind(settingsController, new Dictionary<string, Object>
        {
            ["panelRoot"] = settingsPanel,
            ["worldHpButtonLabel"] = worldHpButton.transform.Find("Label")?.GetComponent<Text>(),
            ["overlayHpButtonLabel"] = overlayHpButton.transform.Find("Label")?.GetComponent<Text>(),
            ["teamSummaryButtonLabel"] = teamSummaryButton.transform.Find("Label")?.GetComponent<Text>(),
            ["statusText"] = settingsStatus,
        });

        Save(scene);
    }

    private static void RebuildReward()
    {
        var scene = CreateFreshScene("Reward");
        EnsureSceneMarker(scene, "SceneMarker_Reward");
        EnsureCamera("Main Camera", true, new Vector3(0f, 0f, -10f), Quaternion.identity);
        var canvas = EnsureCanvasRoot("RewardCanvas");
        EnsureEventSystem();

        var controllerGo = ResetUiChild(canvas.transform, "RewardScreenController");
        var controller = EnsureComponent<RewardScreenController>(controllerGo);

        EnsurePanel(canvas.transform, "SummaryPanel", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(180f, 40f), new Vector2(320f, 320f), new Color(0.12f, 0.16f, 0.23f, 0.92f));
        var rewardCardsRoot = EnsurePanel(canvas.transform, "RewardCardsRoot", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(120f, 20f), new Vector2(520f, 340f), new Color(0.12f, 0.16f, 0.23f, 0.92f)).rectTransform;

        var title = EnsureText(canvas.transform, "TitleText", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(700f, 40f), TextAnchor.MiddleCenter, 24, "Reward Operator UI");
        var summary = EnsureText(canvas.transform, "SummaryText", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(180f, 40f), new Vector2(280f, 280f), TextAnchor.UpperLeft);
        var choices = EnsureText(canvas.transform, "ChoicesText", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(120f, -80f), new Vector2(420f, 30f), TextAnchor.MiddleCenter, 18, "3지선다 보상 카드");
        var status = EnsureText(canvas.transform, "StatusText", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(840f, 30f), TextAnchor.MiddleCenter);

        var choice1 = EnsurePanel(rewardCardsRoot, "ChoiceCard1", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-160f, -10f), new Vector2(140f, 240f), new Color(0.18f, 0.21f, 0.30f, 0.96f)).rectTransform;
        var choice2 = EnsurePanel(rewardCardsRoot, "ChoiceCard2", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(140f, 240f), new Color(0.18f, 0.21f, 0.30f, 0.96f)).rectTransform;
        var choice3 = EnsurePanel(rewardCardsRoot, "ChoiceCard3", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(160f, -10f), new Vector2(140f, 240f), new Color(0.18f, 0.21f, 0.30f, 0.96f)).rectTransform;

        EnsureText(choice1, "TitleText", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(120f, 34f), TextAnchor.MiddleCenter, 16, "Choice 1");
        EnsureText(choice1, "BodyText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 28f), new Vector2(120f, 88f), TextAnchor.MiddleCenter, 14, "Body");
        EnsureText(choice1, "KindText", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 72f), new Vector2(120f, 26f), TextAnchor.MiddleCenter, 13, "Kind");
        EnsureButton(choice1, "ChooseButton", "Pick 1", new Vector2(0.5f, 0f), new Vector2(0f, 24f), controller.Choose0);

        EnsureText(choice2, "TitleText", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(120f, 34f), TextAnchor.MiddleCenter, 16, "Choice 2");
        EnsureText(choice2, "BodyText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 28f), new Vector2(120f, 88f), TextAnchor.MiddleCenter, 14, "Body");
        EnsureText(choice2, "KindText", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 72f), new Vector2(120f, 26f), TextAnchor.MiddleCenter, 13, "Kind");
        EnsureButton(choice2, "ChooseButton", "Pick 2", new Vector2(0.5f, 0f), new Vector2(0f, 24f), controller.Choose1);

        EnsureText(choice3, "TitleText", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(120f, 34f), TextAnchor.MiddleCenter, 16, "Choice 3");
        EnsureText(choice3, "BodyText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 28f), new Vector2(120f, 88f), TextAnchor.MiddleCenter, 14, "Body");
        EnsureText(choice3, "KindText", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 72f), new Vector2(120f, 26f), TextAnchor.MiddleCenter, 13, "Kind");
        EnsureButton(choice3, "ChooseButton", "Pick 3", new Vector2(0.5f, 0f), new Vector2(0f, 24f), controller.Choose2);

        EnsureButton(canvas.transform, "ReturnTownButton", "Return Town", new Vector2(1f, 0f), new Vector2(-90f, 25f), controller.ReturnToTown);

        Bind(controller, new Dictionary<string, Object>
        {
            ["titleText"] = title,
            ["summaryText"] = summary,
            ["choicesText"] = choices,
            ["rewardCardsRoot"] = rewardCardsRoot,
            ["statusText"] = status,
        });

        Save(scene);
    }

    private static Scene Open(string sceneName)
    {
        var path = $"{ScenesRoot}/{sceneName}.unity";
        return EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
    }

    private static Scene CreateFreshScene(string sceneName)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var path = $"{ScenesRoot}/{sceneName}.unity";
        EditorSceneManager.SaveScene(scene, path);
        return scene;
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
        ValidateScene("Town", new[] { "TownCanvas", "EventSystem", "TownScreenController", "RecruitCardsRoot", "QuickBattleButton" }, typeof(TownScreenController));
        ValidateScene("Expedition", new[] { "ExpeditionCanvas", "EventSystem", "ExpeditionScreenController", "NodeTrackRoot", "SelectButton" }, typeof(ExpeditionScreenController));
        ValidateScene("Battle", new[] { "BattleCanvas", "EventSystem", "BattleScreenController", "BattlePresentationRoot", "BattleStageRoot", "ActorOverlayRoot", "PauseButton", "SettingsButton", "SettingsPanel", "ProgressTrack", "ProgressFill", "StatusText" }, typeof(BattleScreenController));
        ValidateScene("Reward", new[] { "RewardCanvas", "EventSystem", "RewardScreenController", "RewardCardsRoot" }, typeof(RewardScreenController));
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

    private static void BindByName(Object target, Scene scene, IReadOnlyDictionary<string, string> refsByFieldName)
    {
        var resolved = new Dictionary<string, Object>();
        foreach (var pair in refsByFieldName)
        {
            var field = target.GetType().GetField(pair.Key, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null)
            {
                continue;
            }

            var go = FindGameObject(scene, pair.Value)
                ?? throw new System.InvalidOperationException($"Missing object '{pair.Value}' while binding {target.name} in {scene.path}");
            var reference = ResolveReference(go, field.FieldType);
            if (reference == null)
            {
                throw new System.InvalidOperationException($"Unable to bind field '{pair.Key}' ({field.FieldType.Name}) from '{pair.Value}' in {scene.path}");
            }

            resolved[pair.Key] = reference;
        }

        Bind(target, resolved);
    }

    private static Object? ResolveReference(GameObject go, System.Type fieldType)
    {
        if (fieldType == typeof(GameObject))
        {
            return go;
        }

        if (typeof(Component).IsAssignableFrom(fieldType))
        {
            return EnsureComponent(go, fieldType);
        }

        return null;
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

        return go.AddComponent<T>();
    }

    private static Component EnsureComponent(GameObject go, System.Type componentType)
    {
#if UNITY_EDITOR
        if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go) > 0)
        {
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
        }
#endif
        var component = go.GetComponent(componentType);
        if (component != null)
        {
            return component;
        }

        return go.AddComponent(componentType);
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
        var inputSystemUiType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (existing.Length == 0)
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            if (inputSystemUiType != null)
            {
                EnsureComponent(go, inputSystemUiType);
            }
            else
            {
                go.AddComponent<StandaloneInputModule>();
            }
            return;
        }

        for (var i = 1; i < existing.Length; i++)
        {
            Object.DestroyImmediate(existing[i].gameObject);
        }

        existing[0].name = "EventSystem";
        if (inputSystemUiType != null)
        {
            var standalone = existing[0].gameObject.GetComponent<StandaloneInputModule>();
            if (standalone != null)
            {
                Object.DestroyImmediate(standalone);
            }

            EnsureComponent(existing[0].gameObject, inputSystemUiType);
            return;
        }

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
        text.font = LocalizationFoundationBootstrap.GetSharedUiFont();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.color = Color.white;
        text.text = initialText ?? name;
        return text;
    }

    private static Image EnsurePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta, Color color)
    {
        var go = EnsureUiChild(parent, name);
        var rect = EnsureComponent<RectTransform>(go);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        var image = EnsureComponent<Image>(go);
        image.color = color;
        return image;
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
        labelText.font = LocalizationFoundationBootstrap.GetSharedUiFont();
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
