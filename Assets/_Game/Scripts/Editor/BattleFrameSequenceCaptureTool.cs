using System;
using System.IO;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SM.Editor.Tools;

/// <summary>
/// Anim-seam review harness. Captures a dense, evenly-spaced PNG frame sequence of a live battle in
/// PlayMode so the two animation-seam artifacts — treadmill (foot-slide) and popping (position jump),
/// both inter-frame phenomena — can be reviewed frame by frame. Uses Unity Recorder's Image Sequence
/// recorder in FrameInterval mode (deterministic frame timing via captured framerate). Output:
/// Captures/anim-seam/&lt;ts&gt;/frame_####.png. Reuses the RecorderController wiring proven by
/// DemoVideoCaptureTool, swapping the movie recorder for an image-sequence recorder.
/// </summary>
public static class BattleFrameSequenceCaptureTool
{
    private const string BattleScenePath = "Assets/_Game/Scenes/Battle.unity";
    private const string CaptureRoot = "Captures/anim-seam";
    private const int Width = 1920;
    private const int Height = 1080;
    private const int FrameRate = 30;
    private const int CaptureFrames = 150; // ~5s
    private const float CloseUpFieldOfView = 38f; // close 3/4 framing for foot-slide review

    private static RecorderController? s_Controller;
    private static string? s_OutDir;
    private static bool s_CloseUp;
    private static bool s_ControllerDisabled;
    private static bool s_CameraFrozen;

    [MenuItem("SM/Internal/Capture/Battle Frame Sequence")]
    public static void RunWide() => Run(false);

    [MenuItem("SM/Internal/Capture/Battle Frame Sequence (Close)")]
    public static void RunClose() => Run(true);

    private static void Run(bool closeUp)
    {
        if (s_Controller != null)
        {
            Debug.LogWarning("[AnimSeamFrames] already running.");
            return;
        }

        s_CloseUp = closeUp;
        s_ControllerDisabled = false;
        s_CameraFrozen = false;

        if (!EnsureBattleSceneOpen())
        {
            Debug.LogError("[AnimSeamFrames] cannot open Battle scene.");
            return;
        }

        s_OutDir = Path.Combine(CaptureRoot, DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        Directory.CreateDirectory(s_OutDir);

        if (!Application.isPlaying)
        {
            EditorApplication.playModeStateChanged += OnEntered;
            EditorApplication.EnterPlaymode();
            Debug.Log("[AnimSeamFrames] entering PlayMode...");
            return;
        }

        Begin();
    }

    private static void OnEntered(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredPlayMode)
        {
            return;
        }

        EditorApplication.playModeStateChanged -= OnEntered;
        Begin();
    }

    private static void Begin()
    {
        try
        {
            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();

            var image = ScriptableObject.CreateInstance<ImageRecorderSettings>();
            image.name = "AnimSeamFrames";
            image.Enabled = true;
            image.OutputFormat = ImageRecorderSettings.ImageRecorderOutputFormat.PNG;
            image.CaptureAlpha = false;
            image.imageInputSettings = new GameViewInputSettings
            {
                OutputWidth = Width,
                OutputHeight = Height,
            };
            // NOTE: do NOT Path.Combine the wildcard — DefaultWildcard.Frame ("<Frame>") contains angle
            // brackets that Path.Combine rejects as illegal path chars. Recorder wants the raw token.
            image.OutputFile = s_OutDir.Replace('\\', '/') + "/frame_" + DefaultWildcard.Frame;
            image.FrameRate = FrameRate;
            image.CapFrameRate = true;
            image.FrameRatePlayback = FrameRatePlayback.Constant;

            settings.AddRecorderSettings(image);
            settings.SetRecordModeToFrameInterval(0, CaptureFrames);
            settings.FrameRate = FrameRate;

            s_Controller = new RecorderController(settings);
            s_Controller.PrepareRecording();
            s_Controller.StartRecording();
            EditorApplication.update += OnUpdate;
            Debug.Log($"[AnimSeamFrames] recording {CaptureFrames} frames @ {FrameRate}fps {Width}x{Height} → {s_OutDir}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[AnimSeamFrames] begin failed: {e}");
            s_Controller = null;
        }
    }

    private static void OnUpdate()
    {
        if (s_Controller == null)
        {
            EditorApplication.update -= OnUpdate;
            return;
        }

        AutoSkipStoryPresentation();

        if (s_CloseUp)
        {
            AimCloseUpCameraAtCombat();
        }

        if (s_Controller.IsRecording())
        {
            return;
        }

        EditorApplication.update -= OnUpdate;
        s_Controller = null;
        Debug.Log($"[AnimSeamFrames] complete → {s_OutDir}");
        if (EditorApplication.isPlaying)
        {
            EditorApplication.ExitPlaymode();
        }
    }

    // 전투 전 인트로 컷씬/스토리 카드가 캡쳐 프레임 전체를 잡아먹지 않도록 자동 스킵한다(이 하네스는
    // 모션 검증용이라 내러티브 연출은 캡쳐 대상이 아니다). 러너의 presenter 필드는 비공개라 에디터
    // 진단 도구 한정으로 reflection을 쓴다 — 런타임 코드에는 이 패턴을 들이지 않는다.
    private static void AutoSkipStoryPresentation()
    {
        var runner = UnityEngine.Object.FindFirstObjectByType<SM.Unity.Narrative.StoryPresentationRunner>();
        if (runner == null || !runner.IsBusy)
        {
            return;
        }

        InvokePresenterSkip(runner, "_dialogueScenePresenter", "ConfirmSkipAll");
        InvokePresenterSkip(runner, "_dialogueOverlayPresenter", "SkipAll");
        InvokePresenterSkip(runner, "_storyCardPresenter", "Skip");
    }

    private static void InvokePresenterSkip(object runner, string fieldName, string skipMethodName)
    {
        var field = runner.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var presenter = field?.GetValue(runner);
        if (presenter == null)
        {
            return;
        }

        var isPlaying = presenter.GetType().GetProperty("IsPlaying")?.GetValue(presenter) as bool? ?? false;
        if (!isPlaying)
        {
            return;
        }

        presenter.GetType().GetMethod(skipMethodName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)?.Invoke(presenter, null);
    }

    // Frames the live combat for foot-slide review: disables the battle camera controller (which would
    // otherwise keep repositioning to the arena centre, leaving the front-line clash off the optical
    // centre) and aims a close 3/4 camera at the centroid of the actor views.
    private static void AimCloseUpCameraAtCombat()
    {
        // Aim once, then freeze: a STATIC camera keeps the ground fixed in screen space, which is what
        // makes foot-slide (treadmill) judgeable — a centroid-following camera masks unit translation.
        if (s_CameraFrozen)
        {
            return;
        }

        var cam = Camera.main;
        if (cam == null)
        {
            return;
        }

        if (!s_ControllerDisabled)
        {
            var controller = UnityEngine.Object.FindFirstObjectByType<SM.Unity.BattleCameraController>();
            if (controller != null)
            {
                controller.enabled = false;
                s_ControllerDisabled = true;
            }
        }

        var views = UnityEngine.Object.FindObjectsByType<SM.Unity.BattleActorView>(FindObjectsSortMode.None);
        if (views.Length == 0)
        {
            return;
        }

        var centroid = Vector3.zero;
        foreach (var view in views)
        {
            centroid += view.transform.position;
        }

        centroid /= views.Length;

        cam.transform.position = centroid + new Vector3(0f, 7f, -7f);
        cam.transform.LookAt(centroid + new Vector3(0f, 0.8f, 0f));
        cam.fieldOfView = CloseUpFieldOfView;
        s_CameraFrozen = true;
    }

    private static bool EnsureBattleSceneOpen()
    {
        var active = SceneManager.GetActiveScene();
        if (active.IsValid() && active.path == BattleScenePath)
        {
            return true;
        }

        return EditorSceneManager.OpenScene(BattleScenePath, OpenSceneMode.Single).IsValid();
    }
}
