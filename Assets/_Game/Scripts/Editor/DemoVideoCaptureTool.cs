using System;
using System.IO;
using SM.Unity.Tools;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;

namespace SM.Editor.Tools;

/// <summary>
/// wave-62 — Demo Video Capture Tool
/// PlayMode 진입 + RecorderController(MovieRecorderSettings + GameViewInputSettings) 활용해
/// 1080p 60fps mp4 영상을 Captures/demo/ 에 출력. menu trigger 1쌍 (Start / Stop).
///
/// Start 동작:
///   1) Captures/demo/wave_62_demo_yyyyMMdd_HHmmss.mp4 경로 결정
///   2) PlayMode 가 아니면 PlayMode 진입 trigger 후 entered 콜백에서 record start
///   3) PlayMode 이미 안에 있으면 즉시 record start
///
/// Stop 동작:
///   1) record stop 후 controller disposed
///   2) PlayMode 종료는 사용자 선택 (자동 종료 안 함 — 사용자가 추가 walk 가능)
///
/// 5분 full walkthrough automation 은 별도 wave. 본 도구는 manual trigger pipeline 검증 목적.
/// </summary>
public static class DemoVideoCaptureTool
{
    private const string CaptureDirectory = "Captures/demo";
    private const int CaptureWidth = 1920;
    private const int CaptureHeight = 1080;
    private const int FrameRate = 60;

    private static RecorderController? s_Controller;
    private static string? s_PendingOutputBaseName;

    [MenuItem("SM/Internal/Capture/Demo Video Start")]
    public static void StartFromMenu()
    {
        if (s_Controller != null)
        {
            Debug.LogWarning("[DemoVideoCaptureTool] Already recording — Stop 먼저 호출 필요.");
            return;
        }

        Directory.CreateDirectory(CaptureDirectory);
        s_PendingOutputBaseName = $"wave_62_demo_{DateTime.Now:yyyyMMdd_HHmmss}";

        if (!Application.isPlaying)
        {
            // PlayMode 진입 trigger → entered 콜백에서 record start
            EditorApplication.playModeStateChanged += OnPlayModeStateChangedForStart;
            EditorApplication.isPlaying = true;
            Debug.Log("[DemoVideoCaptureTool] PlayMode 진입 trigger — entered 후 record start.");
            return;
        }

        BeginRecordingNow();
    }

    [MenuItem("SM/Internal/Capture/Demo Video Stop")]
    public static void StopFromMenu()
    {
        if (s_Controller == null)
        {
            Debug.LogWarning("[DemoVideoCaptureTool] 진행 중 record 없음.");
            return;
        }

        s_Controller.StopRecording();
        s_Controller = null;
        Debug.Log($"[DemoVideoCaptureTool] Stop — output: {Path.Combine(CaptureDirectory, s_PendingOutputBaseName ?? "wave_62_demo")}.mp4");
        s_PendingOutputBaseName = null;
    }

    /// <summary>
    /// wave-62-2 — Demo walkthrough + record 통합 trigger.
    /// PlayMode 진입 → entered 콜백에서 record start + DemoWalkthroughRunner GameObject 생성 + StartCoroutine.
    /// Runner OnComplete (success/fail) → record stop + PlayMode exit.
    /// </summary>
    [MenuItem("SM/Internal/Capture/Run Demo Walkthrough")]
    public static void RunDemoWalkthroughFromMenu()
    {
        if (s_Controller != null)
        {
            Debug.LogWarning("[DemoVideoCaptureTool] Already recording — Stop 먼저.");
            return;
        }
        Directory.CreateDirectory(CaptureDirectory);
        s_PendingOutputBaseName = $"wave_62_walkthrough_{DateTime.Now:yyyyMMdd_HHmmss}";

        if (!Application.isPlaying)
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChangedForWalkthrough;
            EditorApplication.isPlaying = true;
            Debug.Log("[DemoVideoCaptureTool] Walkthrough — PlayMode 진입 trigger.");
            return;
        }

        BeginRecordingNow();
        SpawnWalkthroughRunner();
    }

    private static void OnPlayModeStateChangedForWalkthrough(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredPlayMode)
        {
            return;
        }
        EditorApplication.playModeStateChanged -= OnPlayModeStateChangedForWalkthrough;
        BeginRecordingNow();
        SpawnWalkthroughRunner();
    }

    private static void SpawnWalkthroughRunner()
    {
        var go = new GameObject("DemoWalkthroughRunner");
        UnityEngine.Object.DontDestroyOnLoad(go);
        var runner = go.AddComponent<DemoWalkthroughRunner>();
        runner.OnComplete = success =>
        {
            Debug.Log($"[DemoVideoCaptureTool] Walkthrough complete (success={success}). record stop + PlayMode exit 예약.");
            EditorApplication.delayCall += () =>
            {
                if (s_Controller != null)
                {
                    s_Controller.StopRecording();
                    s_Controller = null;
                    Debug.Log($"[DemoVideoCaptureTool] Walkthrough record stopped — output: {Path.Combine(CaptureDirectory, s_PendingOutputBaseName ?? "wave_62_walkthrough")}.mp4");
                }
                s_PendingOutputBaseName = null;
                EditorApplication.isPlaying = false;
            };
        };
    }

    private static void OnPlayModeStateChangedForStart(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredPlayMode)
        {
            return;
        }

        EditorApplication.playModeStateChanged -= OnPlayModeStateChangedForStart;
        BeginRecordingNow();
    }

    private static void BeginRecordingNow()
    {
        try
        {
            var controllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();

            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name = "wave_62_demo_movie";
            movie.Enabled = true;
            movie.EncoderSettings = new CoreEncoderSettings
            {
                EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High,
                Codec = CoreEncoderSettings.OutputCodec.MP4,
            };
            movie.CaptureAudio = true;
            movie.CaptureAlpha = false;
            movie.ImageInputSettings = new GameViewInputSettings
            {
                OutputWidth = CaptureWidth,
                OutputHeight = CaptureHeight,
            };
            movie.OutputFile = Path.Combine(CaptureDirectory, s_PendingOutputBaseName ?? "wave_62_demo");
            movie.FrameRate = FrameRate;
            movie.CapFrameRate = true;
            movie.FrameRatePlayback = FrameRatePlayback.Constant;
            movie.RecordMode = RecordMode.Manual;

            controllerSettings.AddRecorderSettings(movie);
            controllerSettings.SetRecordModeToManual();
            controllerSettings.FrameRate = FrameRate;

            s_Controller = new RecorderController(controllerSettings);
            s_Controller.PrepareRecording();
            s_Controller.StartRecording();

            Debug.Log($"[DemoVideoCaptureTool] Recording started — {CaptureWidth}x{CaptureHeight} @ {FrameRate}fps, output: {movie.OutputFile}.mp4");
        }
        catch (Exception e)
        {
            Debug.LogError($"[DemoVideoCaptureTool] BeginRecordingNow 실패: {e}");
            s_Controller = null;
            s_PendingOutputBaseName = null;
        }
    }
}
