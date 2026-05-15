using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Editor.Bootstrap.UI;

/// <summary>
/// SM/Town Preview surface 캡쳐 유틸 — 9 surface를 UIDocument + PanelSettings.targetTexture로
/// 오프스크린 RenderTexture에 렌더한 뒤 PNG로 저장. 디자인 검토 루프용.
///
/// 핵심: OS 화면 픽셀(InternalEditorUtility.ReadScreenPixel)이 아니라 panel 자체를 RT에 렌더한다.
/// → Unity가 포그라운드가 아니어도, 사용자가 다른 창을 쓰는 중이어도 정확히 캡쳐된다.
/// (BattleSceneCaptureTool의 Camera.Render → RT → ReadPixels 패턴을 UI Toolkit panel에 적용.)
///
/// 사용: 메뉴 `SM/Town/▶ Preview 9종 캡쳐` 또는
///       `unity-bridge.ps1 exec -Dangerous -Code 'SM.Editor.Bootstrap.UI.TownPreviewCaptureUtility.CaptureAll()'`.
///
/// EditorApplication.update 기반 state machine — surface 빌드 → N프레임 layout/render 안정화
/// (QueuePlayerLoopUpdate로 runtime panel tick 강제) → ReadPixels. exec는 즉시 return하므로
/// 호출 후 ~10초 대기 후 PNG를 읽는다. 출력: Screenshots/mockups/{surface}.png
/// </summary>
public static class TownPreviewCaptureUtility
{
    // 각 surface Bootstrap의 public BuildInto(VisualElement)를 호출 — EditorWindow를 띄우지 않고
    // ScriptableObject 인스턴스만 만들어 지정 root에 빌드한다 (Make<T> 헬퍼).
    private static readonly (Action<VisualElement> Build, string FileName)[] Targets =
    {
        (r => Make<TownRosterGridPreviewBootstrap>(b => b.BuildInto(r)),   "roster_grid"),
        (r => Make<TacticalWorkshopPreviewBootstrap>(b => b.BuildInto(r)), "tactical_workshop"),
        (r => Make<RecruitPreviewBootstrap>(b => b.BuildInto(r)),          "recruit"),
        (r => Make<EquipmentRefitPreviewBootstrap>(b => b.BuildInto(r)),   "equipment_refit"),
        (r => Make<PermanentAugmentPreviewBootstrap>(b => b.BuildInto(r)), "permanent_augment"),
        (r => Make<PassiveBoardPreviewBootstrap>(b => b.BuildInto(r)),     "passive_board"),
        (r => Make<InventoryPreviewBootstrap>(b => b.BuildInto(r)),        "inventory"),
        (r => Make<TheaterPreviewBootstrap>(b => b.BuildInto(r)),          "theater"),
        (r => Make<SettingsPreviewBootstrap>(b => b.BuildInto(r)),         "settings"),
        // production Town hub + SquadBuilder modal — Phase 1/4 retroactive 시각 검증 (atom 적용).
        (r => BuildProductionTownHub(r, openSquadBuilder: false),          "production_town_hub"),
        (r => BuildProductionTownHub(r, openSquadBuilder: true),           "squad_builder_modal"),
    };

    private const string TownScreenUxmlPath = "Assets/_Game/UI/Screens/Town/TownScreen.uxml";
    private const string TownScreenUssPath = "Assets/_Game/UI/Screens/Town/TownScreen.uss";
    private const string ThemeTokensPath = "Assets/_Game/UI/Foundation/Styles/ThemeTokens.uss";
    private const string RuntimePanelThemePath = "Assets/_Game/UI/Foundation/Styles/RuntimePanelTheme.uss";

    /// <summary>
    /// production TownScreen.uxml + atom style 직접 inject + real-session presenter wire.
    /// Bootstrap 우회 (production controller가 scene 전용이라 EditorWindow에 없음). audit §2.1/§2.2 정합 캡처.
    /// </summary>
    private static void BuildProductionTownHub(VisualElement root, bool openSquadBuilder)
    {
        var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TownScreenUxmlPath);
        if (visualTree == null)
        {
            root.Add(new UnityEngine.UIElements.Label($"TownScreen.uxml 못 찾음: {TownScreenUxmlPath}"));
            return;
        }

        var tokens = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.StyleSheet>(ThemeTokensPath);
        var theme = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.StyleSheet>(RuntimePanelThemePath);
        var townUss = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.StyleSheet>(TownScreenUssPath);
        if (tokens != null) root.styleSheets.Add(tokens);
        if (theme != null) root.styleSheets.Add(theme);
        if (townUss != null) root.styleSheets.Add(townUss);
        visualTree.CloneTree(root);

        try
        {
            var sessionRoot = PreviewSessionContext.EnsureSession();
            var contentText = PreviewSessionContext.CreateContentText(sessionRoot);
            var view = new SM.Unity.UI.Town.TownScreenView(root);
            var presenter = new SM.Unity.UI.Town.TownScreenPresenter(
                sessionRoot, sessionRoot.Localization, contentText, view);
            presenter.Initialize();

            // SquadBuilder modal — Town hub 안에 인스턴스 (UXML Instance). presenter ctor가 element 조회.
            // openSquadBuilder=true면 즉시 Open (modal 열린 상태 캡처).
            var squadBuilder = new SM.Unity.UI.Town.SquadBuilderPresenter(root, sessionRoot, contentText);
            if (openSquadBuilder)
            {
                squadBuilder.Open();
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PreviewCapture] production hub wire 실패: {e.Message}");
            root.Add(new UnityEngine.UIElements.Label($"production hub wire fail: {e.Message}"));
        }
    }

    private const string OutputDir = "Screenshots/mockups";
    private const int CaptureWidth = 1600;
    private const int CaptureHeight = 900;
    // build 후 layout + panel render 안정화 프레임. UIDocument runtime panel은 player loop tick에
    // 맞춰 layout/render하므로 여유있게 대기.
    private const int LayoutWaitFrames = 16;

    private static int _index;
    private static int _phase;       // 0 = setup 단계, 1 = wait+capture 단계
    private static int _frameWait;
    private static GameObject? _host;
    private static UIDocument? _uiDocument;
    private static PanelSettings? _panelSettings;
    private static RenderTexture? _renderTexture;

    // edit mode에서 runtime panel은 player loop tick만으론 targetTexture에 안 그려진다 —
    // UIElementsRuntimeUtility의 내부 update/repaint를 명시 호출해야 RT에 렌더된다 (reflection).
    private static MethodInfo? _updateRuntimePanels;
    private static MethodInfo? _repaintOffscreenPanels;
    private static bool _reflectionResolved;

    [MenuItem("SM/Town/▶ Preview 9종 캡쳐", false, 4)]
    public static void CaptureAll()
    {
        Directory.CreateDirectory(OutputDir);
        _index = 0;
        _phase = 0;
        _frameWait = 0;
        Cleanup();
        EditorApplication.update -= Step;
        EditorApplication.update += Step;
        Debug.Log($"[PreviewCapture] 시작 — {Targets.Length} surface → {OutputDir}/ (offscreen RT 렌더, 포그라운드 무관)");
    }

    /// <summary>EditorWindow를 띄우지 않고 인스턴스만 만들어 build 콜백 실행 후 폐기.</summary>
    private static void Make<T>(Action<T> build) where T : EditorWindow
    {
        var bootstrap = ScriptableObject.CreateInstance<T>();
        try { build(bootstrap); }
        finally { UnityEngine.Object.DestroyImmediate(bootstrap); }
    }

    private static void Step()
    {
        if (_index >= Targets.Length)
        {
            EditorApplication.update -= Step;
            Cleanup();
            AssetDatabase.Refresh();
            Debug.Log($"[PreviewCapture] 완료 — {Targets.Length} surface 저장");
            return;
        }

        var target = Targets[_index];

        if (_phase == 0)
        {
            try
            {
                SetupSurface(target.Build);
            }
            catch (Exception e)
            {
                Debug.LogError($"[PreviewCapture] {target.FileName} setup 실패: {e.Message}\n{e.StackTrace}");
                Cleanup();
                _index++;
                return;
            }
            _frameWait = 0;
            _phase = 1;
            return;
        }

        // phase 1: layout + render 안정화 대기 — runtime panel을 명시적으로 update + offscreen repaint.
        EditorApplication.QueuePlayerLoopUpdate();
        _uiDocument?.rootVisualElement?.MarkDirtyRepaint();
        PumpRuntimePanels();
        _frameWait++;
        if (_frameWait < LayoutWaitFrames) return;

        try
        {
            PumpRuntimePanels();   // capture 직전 마지막 render pass
            CaptureCurrent(target.FileName);
        }
        catch (Exception e)
        {
            Debug.LogError($"[PreviewCapture] {target.FileName} 캡쳐 실패: {e.Message}\n{e.StackTrace}");
        }

        Cleanup();
        _index++;
        _phase = 0;
    }

    private static void SetupSurface(Action<VisualElement> build)
    {
        _renderTexture = new RenderTexture(CaptureWidth, CaptureHeight, 24, RenderTextureFormat.ARGB32)
        {
            name = "TownPreviewCaptureRT",
        };
        _renderTexture.Create();

        _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
        _panelSettings.name = "TownPreviewCapturePanelSettings";
        _panelSettings.hideFlags = HideFlags.HideAndDontSave;
        _panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
        _panelSettings.scale = 1f;
        _panelSettings.targetTexture = _renderTexture;
        _panelSettings.clearColor = true;
        _panelSettings.colorClearValue = new Color(0.047f, 0.067f, 0.149f, 1f); // sm-bg-0 deep midnight

        // GameObject를 inactive로 만들어 UIDocument + panelSettings를 먼저 세팅한 뒤 활성화 →
        // OnEnable이 panelSettings가 있는 상태로 돌아 rootVisualElement가 정상 생성된다.
        _host = new GameObject("__TownPreviewCaptureHost") { hideFlags = HideFlags.HideAndDontSave };
        _host.SetActive(false);
        _uiDocument = _host.AddComponent<UIDocument>();
        _uiDocument.panelSettings = _panelSettings;
        _host.SetActive(true);

        var root = _uiDocument.rootVisualElement;
        if (root == null)
        {
            throw new InvalidOperationException("UIDocument.rootVisualElement is null — panel 생성 실패");
        }

        root.style.width = CaptureWidth;
        root.style.height = CaptureHeight;
        build(root);
    }

    private static void CaptureCurrent(string fileName)
    {
        if (_renderTexture == null) return;

        var prevActive = RenderTexture.active;
        Texture2D? tex = null;
        try
        {
            RenderTexture.active = _renderTexture;
            tex = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0f, 0f, CaptureWidth, CaptureHeight), 0, 0);
            tex.Apply();

            var png = tex.EncodeToPNG();
            var path = Path.Combine(OutputDir, fileName + ".png");
            File.WriteAllBytes(path, png);
            Debug.Log($"[PreviewCapture] {fileName}.png 저장 ({CaptureWidth}x{CaptureHeight})");
        }
        finally
        {
            RenderTexture.active = prevActive;
            if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
        }
    }

    private static void Cleanup()
    {
        if (_host != null) { UnityEngine.Object.DestroyImmediate(_host); _host = null; }
        _uiDocument = null;
        if (_panelSettings != null) { UnityEngine.Object.DestroyImmediate(_panelSettings); _panelSettings = null; }
        if (_renderTexture != null)
        {
            _renderTexture.Release();
            UnityEngine.Object.DestroyImmediate(_renderTexture);
            _renderTexture = null;
        }
    }

    /// <summary>
    /// runtime panel을 명시적으로 layout update + offscreen(targetTexture) repaint.
    /// edit mode에선 player loop tick만으론 targetTexture 렌더가 안 돌아서, UIElementsRuntimeUtility의
    /// 내부 메서드를 reflection으로 직접 호출한다 (editor 전용 capture tool — runtime 의존성 아님).
    /// </summary>
    private static void PumpRuntimePanels()
    {
        if (!_reflectionResolved)
        {
            _reflectionResolved = true;
            var runtimeUtility = typeof(PanelSettings).Assembly
                .GetType("UnityEngine.UIElements.UIElementsRuntimeUtility");
            if (runtimeUtility != null)
            {
                const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                try { _updateRuntimePanels = runtimeUtility.GetMethod("UpdateRuntimePanels", flags, null, Type.EmptyTypes, null); }
                catch (AmbiguousMatchException) { _updateRuntimePanels = null; }
                try { _repaintOffscreenPanels = runtimeUtility.GetMethod("RepaintOffscreenPanels", flags, null, Type.EmptyTypes, null); }
                catch (AmbiguousMatchException) { _repaintOffscreenPanels = null; }
            }
            if (_repaintOffscreenPanels == null)
            {
                Debug.LogWarning("[PreviewCapture] UIElementsRuntimeUtility.RepaintOffscreenPanels 못 찾음 — " +
                                 "panel이 RT에 안 그려질 수 있음 (Unity 버전 내부 API 변경 확인 필요)");
            }
        }

        try
        {
            _updateRuntimePanels?.Invoke(null, null);
            _repaintOffscreenPanels?.Invoke(null, null);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PreviewCapture] PumpRuntimePanels 호출 실패: {e.Message}");
        }
    }
}
