using SM.Unity.Narrative;
using SM.Unity.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace SM.Editor.Narrative;

/// <summary>
/// 극장 모드 scene을 코드로 생성하는 1회성 setup 메뉴.
/// scene이 깨지거나 재생성이 필요하면 메뉴를 다시 실행하면 된다.
/// 구성: TheaterPanelHost(UIDocument + RuntimePanelHost) / StoryPresentationRunner /
/// TheaterModeController. SerializeField는 명시 연결한다.
/// </summary>
public static class TheaterModeSceneBuilder
{
    private const string ScenePath = "Assets/_Game/Scenes/Theater.unity";
    private const string TheaterTreePath = "Assets/_Game/UI/Narrative/TheaterMode.uxml";
    private const string TheaterStylePath = "Assets/_Game/UI/Narrative/TheaterMode.uss";

    [MenuItem("SM/내러티브/극장 모드 Scene 생성")]
    public static void BuildTheaterScene()
    {
        var activeScene = SceneManager.GetActiveScene();
        if (activeScene.isDirty)
        {
            Debug.LogWarning("[TheaterModeSceneBuilder] 현재 scene에 저장 안 된 변경이 있어 중단합니다. 저장하거나 닫은 뒤 다시 실행하세요.");
            return;
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        var panelHostGo = new GameObject("TheaterPanelHost");
        panelHostGo.AddComponent<UIDocument>();
        var panelHost = panelHostGo.AddComponent<RuntimePanelHost>();
        // "Theater"는 RuntimePanelAssetRegistry에 등록돼 있지 않아 자동 설정이 안 된다.
        // 공유 PanelSettings + 공통 테마를 명시 연결해야 game view에 렌더된다.
        // visualTree는 null — theater UI는 TheaterModeController가 코드로 Root에 add한다.
        panelHost.Configure(
            RuntimePanelAssetRegistry.LoadSharedPanelSettings(),
            null,
            new[]
            {
                RuntimePanelAssetRegistry.LoadStyleSheet(RuntimePanelAssetRegistry.ThemeTokensStylePath),
                RuntimePanelAssetRegistry.LoadStyleSheet(RuntimePanelAssetRegistry.RuntimeThemeStylePath),
            },
            0,
            "TheaterRuntimePanelHost");

        var runnerGo = new GameObject("StoryPresentationRunner");
        var runner = runnerGo.AddComponent<StoryPresentationRunner>();

        var controllerGo = new GameObject("TheaterModeController");
        var controller = controllerGo.AddComponent<TheaterModeController>();

        var theaterTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TheaterTreePath);
        var theaterStyle = AssetDatabase.LoadAssetAtPath<StyleSheet>(TheaterStylePath);

        var controllerSo = new SerializedObject(controller);
        controllerSo.FindProperty("_panelHost").objectReferenceValue = panelHost;
        controllerSo.FindProperty("_storyRunner").objectReferenceValue = runner;
        controllerSo.FindProperty("_theaterTree").objectReferenceValue = theaterTree;
        controllerSo.FindProperty("_theaterStyle").objectReferenceValue = theaterStyle;
        controllerSo.ApplyModifiedPropertiesWithoutUndo();

        var runnerSo = new SerializedObject(runner);
        runnerSo.FindProperty("_panelHost").objectReferenceValue = panelHost;
        runnerSo.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        var saved = EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();

        var verified = saved
                       && panelHost != null
                       && runner != null
                       && controller != null
                       && theaterTree != null
                       && theaterStyle != null;

        if (verified)
        {
            Debug.Log($"[TheaterModeSceneBuilder] Theater scene 생성·검증 완료: {ScenePath}");
        }
        else
        {
            Debug.LogError($"[TheaterModeSceneBuilder] Theater scene 생성 불완전: saved={saved} " +
                           $"panelHost={panelHost != null} runner={runner != null} controller={controller != null} " +
                           $"uxml={theaterTree != null} uss={theaterStyle != null}");
        }
    }
}
