using System;
using System.IO;
using SM.Editor.Bootstrap;
using SM.Unity;
using SM.Unity.UI.Battle;
using UnityEditor;
using UnityEngine;

namespace SM.Editor.Tools;

/// <summary>
/// Battle PlayAuto 캡처의 editor-session lifecycle을 소유한다.
/// 메뉴 디스패치 호출이 반환된 다음 Play 진입을 요청하고, 실제 전환과 런타임 준비 상태를 검증한다.
/// </summary>
internal static class BattlePlayAutoCaptureSession
{
    private const string PlayAutoPendingKey = "SM.BattleCapture.PlayAutoPending";
    private const string PlayAutoFrameKey = "SM.BattleCapture.PlayAutoFrame";
    private const string ScreenshotRequestedKey = "SM.BattleCapture.ScreenshotRequested";
    private const string AbModeKey = "SM.BattleCapture.AbCharacterLighting";
    private const string AbStepKey = "SM.BattleCapture.AbStep";
    private const string AbStepFrameKey = "SM.BattleCapture.AbStepFrame";
    private const string PlayEntryCallbackPendingKey = "SM.BattleCapture.PlayEntryCallbackPending";
    private const string PlayEntryRequestedKey = "SM.BattleCapture.PlayEntryRequested";
    private const string PlayEntryWaitUpdatesKey = "SM.BattleCapture.PlayEntryWaitUpdates";
    private const string PlaybackPausedKey = "SM.BattleCapture.PlaybackPaused";

    /// <summary>
    /// 켜면 정지 직후 유닛 상세창을 열고 찍는다. 상세창은 클릭해야 나오는 화면이라
    /// 평소 캡쳐 경로로는 <b>한 번도 볼 수 없다</b> — 눈으로 못 본 화면은 고칠 수도 없다.
    /// </summary>
    private const string OpenUnitDetailKey = "SM.BattleCapture.OpenUnitDetail";

    /// <summary>
    /// 켜면 중반 게이트를 통과한 뒤 재생을 <b>전투 종료까지</b> 밀고 찍는다.
    /// 중반 게이트를 먼저 통과시키는 게 중요하다 — "전투가 실제로 돌고 액터가 전부 렌더링된다"를
    /// 확인한 다음에 끝으로 보내야, 끝 화면이 비어 있을 때 그게 결함인지 셋업 실패인지 구분된다.
    /// </summary>
    private const string SeekBattleEndKey = "SM.BattleCapture.SeekBattleEnd";
    private const string CaptureStepIndexKey = "SM.BattleCapture.StepIndex";
    private const string CaptureAliveUnitsKey = "SM.BattleCapture.AliveUnits";
    private const string CaptureTotalUnitsKey = "SM.BattleCapture.TotalUnits";

    /// <summary>
    /// 전투가 실제로 시작될 때까지 기다릴 상한. 게이트가 상태 기반이므로 조건이 서면 즉시 다음 단계로 간다.
    /// </summary>
    private const int PlayAutoReadyTimeoutFrames = 3600;

    private const int PlayAutoMaxExtraFrames = 1800;
    private const int PlayEntryCallbackTimeoutUpdates = 120;
    private const int PlayEntryTransitionTimeoutUpdates = 300;
    private const int AbSettleFrames = 6;
    private const int AbFileWaitFrames = 900;
    private const float MinUsefulCaptureLuminance = 0.012f;

    private const string PlayModeScreenshotPath = "Captures/battle_playmode.png";
    private const string AbOffScreenshotPath = "Captures/battle_ab_charlight_off.png";
    private const string AbOnScreenshotPath = "Captures/battle_ab_charlight_on.png";
    [InitializeOnLoadMethod]
    private static void RegisterHooks()
    {
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;

        if (SessionState.GetBool(PlayAutoPendingKey, false)
            && SessionState.GetBool(PlayEntryCallbackPendingKey, false))
        {
            SchedulePlayEntry();
        }
    }

    internal static void Start(bool characterLightingAb) => Start(characterLightingAb, openUnitDetail: false);

    internal static void Start(bool characterLightingAb, bool openUnitDetail)
        => Start(characterLightingAb, openUnitDetail, seekBattleEnd: false);

    internal static void Start(bool characterLightingAb, bool openUnitDetail, bool seekBattleEnd)
    {
        SessionState.SetBool(OpenUnitDetailKey, openUnitDetail);
        SessionState.SetBool(SeekBattleEndKey, seekBattleEnd);
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            if (characterLightingAb)
            {
                BattleCaptureResultFile.WriteFailure(
                    "PlayAuto cannot start while the Editor is already playing or changing Play Mode.");
            }

            Debug.LogError(
                "[BattleSceneCaptureTool] PlayAuto failed: the Editor is already playing or changing Play Mode.");
            return;
        }

        if (!BattleSceneCaptureTool.EnsureBattleSceneOpen())
        {
            if (characterLightingAb)
            {
                BattleCaptureResultFile.WriteFailure("Failed to open the Battle scene.");
            }

            Debug.LogError("[BattleSceneCaptureTool] PlayAuto: failed to open Battle scene.");
            return;
        }

        SessionState.SetBool(PlayAutoPendingKey, true);
        SessionState.SetInt(PlayAutoFrameKey, 0);
        SessionState.EraseBool(ScreenshotRequestedKey);
        SessionState.SetBool(AbModeKey, characterLightingAb);
        SessionState.SetInt(AbStepKey, 0);
        SessionState.SetInt(AbStepFrameKey, 0);
        SessionState.SetBool(PlayEntryCallbackPendingKey, true);
        SessionState.EraseBool(PlayEntryRequestedKey);
        SessionState.SetInt(PlayEntryWaitUpdatesKey, 0);
        SessionState.EraseBool(PlaybackPausedKey);
        SessionState.EraseInt(CaptureStepIndexKey);
        SessionState.EraseInt(CaptureAliveUnitsKey);
        SessionState.EraseInt(CaptureTotalUnitsKey);
        EditorPrefs.DeleteKey(FirstPlayableBootstrap.CombatSandboxRequestedKey);

        try
        {
            DeleteIfExists(PlayModeScreenshotPath);
            if (characterLightingAb)
            {
                DeleteIfExists(AbOffScreenshotPath);
                DeleteIfExists(AbOnScreenshotPath);
                BattleCaptureResultFile.Clear();
            }
        }
        catch (Exception ex)
        {
            FailPlayAutoCapture($"could not reset capture outputs before capture. {ex.Message}");
            return;
        }

        Debug.Log(
            "[BattleSceneCaptureTool] PlayAuto armed. Play entry is deferred until after the menu dispatch returns; " +
            $"sandboxPref={EditorPrefs.GetBool(FirstPlayableBootstrap.CombatSandboxRequestedKey, false)}.");
        SchedulePlayEntry();
    }

    private static void SchedulePlayEntry()
    {
        EditorApplication.delayCall -= EnterPlayModeAfterMenuDispatch;
        EditorApplication.delayCall += EnterPlayModeAfterMenuDispatch;
    }

    private static void EnterPlayModeAfterMenuDispatch()
    {
        if (!SessionState.GetBool(PlayAutoPendingKey, false)
            || EditorApplication.isPlaying
            || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        SessionState.EraseBool(PlayEntryCallbackPendingKey);
        SessionState.SetBool(PlayEntryRequestedKey, true);
        SessionState.SetInt(PlayEntryWaitUpdatesKey, 0);
        EditorPrefs.SetBool(FirstPlayableBootstrap.CombatSandboxRequestedKey, true);

        Debug.Log(
            "[BattleSceneCaptureTool] PlayAuto requesting Play Mode from deferred editor callback. " +
            $"sandboxPref={EditorPrefs.GetBool(FirstPlayableBootstrap.CombatSandboxRequestedKey, false)}, " +
            $"isPlaying={EditorApplication.isPlaying}, " +
            $"isPlayingOrWillChange={EditorApplication.isPlayingOrWillChangePlaymode}.");

        try
        {
            EditorApplication.EnterPlaymode();
        }
        catch (Exception ex)
        {
            FailPlayAutoCapture($"EnterPlaymode threw before the transition started: {ex.Message}");
        }
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(PlayAutoPendingKey, false))
        {
            return;
        }

        var sandboxRequested = EditorPrefs.GetBool(FirstPlayableBootstrap.CombatSandboxRequestedKey, false);
        Debug.Log(
            $"[BattleSceneCaptureTool] PlayAuto transition state={state}, sandboxPref={sandboxRequested}, " +
            $"pending={SessionState.GetBool(PlayAutoPendingKey, false)}.");

        if (state == PlayModeStateChange.ExitingEditMode && !sandboxRequested)
        {
            FailPlayAutoCapture(
                "Play Mode transition began without the combat sandbox request signal. " +
                "The capture session will not continue on a non-sandbox lane.");
            return;
        }

        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            if (!sandboxRequested)
            {
                FailPlayAutoCapture(
                    "Play Mode was entered, but the combat sandbox request signal was false at runtime bootstrap.");
                return;
            }

            SessionState.EraseBool(PlayEntryCallbackPendingKey);
            SessionState.EraseBool(PlayEntryRequestedKey);
            SessionState.EraseInt(PlayEntryWaitUpdatesKey);
        }
    }

    private static void OnEditorUpdate()
    {
        if (!SessionState.GetBool(PlayAutoPendingKey, false))
        {
            return;
        }

        if (!EditorApplication.isPlaying)
        {
            MonitorPlayEntry();
            return;
        }

        var frame = SessionState.GetInt(PlayAutoFrameKey, 0) + 1;
        SessionState.SetInt(PlayAutoFrameKey, frame);

        var readiness = BattleCaptureReadinessProbe.Observe();

        // 종료 화면 모드는 <b>중반 창을 요구하지 않는다.</b> 중반 창(생존 75% 등)은 레이스라
        // 관측이 늦으면 전투가 창을 지나쳐 캡쳐가 통째로 실패한다 — 실제로 첫 시도가
        // stepIndex=185 · aliveUnits=2/8 로 그렇게 죽었다. 어차피 끝까지 밀 것이므로
        // "씬이 제대로 섰는가"만 확인하면 되고, 그러면 전투 길이·밸런스와 무관하게 항상 성립한다.
        var seekBattleEnd = SessionState.GetBool(SeekBattleEndKey, false);
        var gatePassed = seekBattleEnd ? readiness.IsSceneBuilt : readiness.IsReady;

        if (!gatePassed)
        {
            if (!seekBattleEnd && readiness.CaptureWindowMissed)
            {
                FailPlayAutoCapture(
                    "the mid-battle capture window was missed because fewer than 75% of the units remain alive. " +
                    $"Current state: {readiness.State}");
                return;
            }

            if (frame < PlayAutoReadyTimeoutFrames)
            {
                return;
            }

            FailPlayAutoCapture(
                $"timed out after {frame} frames waiting for an engaged battle with at least 75% living units " +
                $"and active character renderers for every expected actor. Current state: {readiness.State}");
            return;
        }

        if (SessionState.GetBool(AbModeKey, false))
        {
            if (!SessionState.GetBool(PlaybackPausedKey, false))
            {
                PauseAtCaptureWindow(frame, readiness);
                return;
            }

            if (!CaptureStateRemainedStable(readiness, out var stabilityError))
            {
                FailPlayAutoCapture(stabilityError);
                return;
            }

            AdvanceCharacterLightingAb(frame, readiness);
            return;
        }

        AdvanceSingleCapture(frame, readiness.State);
    }

    private static void MonitorPlayEntry()
    {
        var waitUpdates = SessionState.GetInt(PlayEntryWaitUpdatesKey, 0) + 1;
        SessionState.SetInt(PlayEntryWaitUpdatesKey, waitUpdates);

        var callbackPending = SessionState.GetBool(PlayEntryCallbackPendingKey, false);
        var entryRequested = SessionState.GetBool(PlayEntryRequestedKey, false);
        if (callbackPending && waitUpdates < PlayEntryCallbackTimeoutUpdates)
        {
            return;
        }

        if (!entryRequested)
        {
            FailPlayAutoCapture(
                $"the deferred Play entry callback did not run after {waitUpdates} editor updates. " +
                $"sandboxPref={EditorPrefs.GetBool(FirstPlayableBootstrap.CombatSandboxRequestedKey, false)}.");
            return;
        }

        if (waitUpdates < PlayEntryTransitionTimeoutUpdates)
        {
            return;
        }

        FailPlayAutoCapture(
            $"EnterPlaymode was requested but ignored after {waitUpdates} editor updates. " +
            $"sandboxPref={EditorPrefs.GetBool(FirstPlayableBootstrap.CombatSandboxRequestedKey, false)}, " +
            $"isPlaying={EditorApplication.isPlaying}, " +
            $"isPlayingOrWillChange={EditorApplication.isPlayingOrWillChangePlaymode}.");
    }

    private static void PauseAtCaptureWindow(int frame, BattleCaptureReadiness readiness)
    {
        var screen = UnityEngine.Object.FindFirstObjectByType<BattleScreenController>();
        if (screen == null)
        {
            FailPlayAutoCapture($"the BattleScreenController disappeared at the capture window. {readiness.State}");
            return;
        }

        screen.TogglePause();

        // 카메라를 수렴된 보드 프레임으로 확정한다.
        //
        // 이게 없으면 같은 시드·같은 스텝인데 <b>회차마다 프레이밍이 달라진다</b>. 패시브 프레이밍은
        // 시정수 약 0.54 초짜리 지수 블렌드라 개전 후 2~3 초는 부트스트랩 와이드 프레임에서
        // 수렴하는 중이고, 여기서 붙잡는 시점은 전투 1.6 초(step 16)다. 정지 전까지 흐른 실시간이
        // 에디터 부하에 따라 달라지므로 수렴 정도가 회차마다 달랐고, 실제로 전투가 좌하단 구석에서
        // UI 뒤로 밀린 캡쳐가 나왔다. 시각 A/B 를 하려면 카메라가 통제 변수여야 한다.
        // 끝으로 보내는 건 카메라 스냅보다 <b>먼저</b> 해야 한다. 유닛이 이동·사망하므로
        // 종료 시점의 보드 프레임은 중반 프레임과 다르다. 순서가 뒤집히면 카메라가 옛 위치를 잡는다.
        if (SessionState.GetBool(SeekBattleEndKey, false) && !screen.SeekToBattleEnd())
        {
            FailPlayAutoCapture(
                "전투 종료까지 밀었는데 타임라인이 종료 상태가 아니다. "
                + $"최대 스텝 안에 승부가 안 났을 수 있다. {readiness.State}");
            return;
        }

        screen.SnapCameraToSettledBoardFrame();

        if (SessionState.GetBool(OpenUnitDetailKey, false))
        {
            screen.SelectUnitDetailTab(BattleUnitDetailTab.Overview);
        }

        // readiness 는 seek 이후에 다시 관측한다 — CaptureStateRemainedStable 이 이 값과
        // 대조하므로, seek 전 값을 기록하면 A/B 준비 중에 "재생이 안 멈췄다"고 오진한다.
        var pausedReadiness = BattleCaptureReadinessProbe.Observe();
        SessionState.SetBool(PlaybackPausedKey, true);
        SessionState.SetInt(CaptureStepIndexKey, pausedReadiness.StepIndex);
        SessionState.SetInt(CaptureAliveUnitsKey, pausedReadiness.AliveUnits);
        SessionState.SetInt(CaptureTotalUnitsKey, pausedReadiness.TotalUnits);
        SessionState.SetInt(AbStepFrameKey, frame);

        Debug.Log(
            "[BattleSceneCaptureTool] PlayAuto mid-battle gate passed and playback pause was requested. " +
            pausedReadiness.State);
    }

    private static bool CaptureStateRemainedStable(
        BattleCaptureReadiness readiness,
        out string error)
    {
        var expectedStep = SessionState.GetInt(CaptureStepIndexKey, -1);
        var expectedAlive = SessionState.GetInt(CaptureAliveUnitsKey, -1);
        var expectedTotal = SessionState.GetInt(CaptureTotalUnitsKey, -1);
        if (readiness.StepIndex == expectedStep
            && readiness.AliveUnits == expectedAlive
            && readiness.TotalUnits == expectedTotal)
        {
            error = string.Empty;
            return true;
        }

        error =
            "playback did not remain paused while preparing the A/B pair. " +
            $"expectedStep={expectedStep}, actualStep={readiness.StepIndex}, " +
            $"expectedAlive={expectedAlive}/{expectedTotal}, actualAlive={readiness.AliveUnits}/{readiness.TotalUnits}. " +
            $"Current state: {readiness.State}";
        return false;
    }

    private static void AdvanceSingleCapture(int frame, string readiness)
    {
        if (!SessionState.GetBool(ScreenshotRequestedKey, false))
        {
            ScreenCapture.CaptureScreenshot(PlayModeScreenshotPath);
            SessionState.SetBool(ScreenshotRequestedKey, true);
            Debug.Log($"[BattleSceneCaptureTool] PlayAuto runtime gate passed at frame {frame}. {readiness}");
            return;
        }

        if (!File.Exists(PlayModeScreenshotPath))
        {
            if (frame < PlayAutoReadyTimeoutFrames + PlayAutoMaxExtraFrames)
            {
                return;
            }

            FailPlayAutoCapture(
                $"timed out waiting for backbuffer screenshot '{PlayModeScreenshotPath}' after the runtime gate passed. " +
                $"Current state: {readiness}");
            return;
        }

        BattleSceneCaptureTool.CaptureBattleLive();
        if (BattleSceneCaptureTool.LastCaptureLuminance < MinUsefulCaptureLuminance
            && frame < PlayAutoReadyTimeoutFrames + PlayAutoMaxExtraFrames)
        {
            return;
        }

        if (BattleSceneCaptureTool.LastCaptureLuminance < MinUsefulCaptureLuminance)
        {
            FailPlayAutoCapture(
                $"the capture is nearly black (mean luminance {BattleSceneCaptureTool.LastCaptureLuminance:0.000}). " +
                $"Current state: {readiness}");
            return;
        }

        FinishPlayAutoCapture();
    }

    private static void AdvanceCharacterLightingAb(int frame, BattleCaptureReadiness readiness)
    {
        var step = SessionState.GetInt(AbStepKey, 0);
        var stepFrame = SessionState.GetInt(AbStepFrameKey, 0);

        switch (step)
        {
            case 0:
                if (!TrySetCharacterLighting(false))
                {
                    FailPlayAutoCapture(
                        "A/B: no BattleRenderEnvironmentAuthoring in the running scene, so character lighting " +
                        $"could not be toggled. Current state: {readiness.State}");
                    return;
                }

                AdvanceAbStep(1, frame);
                return;

            case 1:
                if (frame - stepFrame < AbSettleFrames)
                {
                    return;
                }

                ScreenCapture.CaptureScreenshot(AbOffScreenshotPath);
                AdvanceAbStep(2, frame);
                return;

            case 2:
                if (!File.Exists(AbOffScreenshotPath))
                {
                    if (frame - stepFrame < AbFileWaitFrames)
                    {
                        return;
                    }

                    FailPlayAutoCapture(
                        $"A/B: lights-off frame '{AbOffScreenshotPath}' never landed. Current state: {readiness.State}");
                    return;
                }

                if (!TrySetCharacterLighting(true))
                {
                    FailPlayAutoCapture(
                        "A/B: character lighting could not be restored before the lights-on frame. " +
                        $"Current state: {readiness.State}");
                    return;
                }

                AdvanceAbStep(3, frame);
                return;

            case 3:
                if (frame - stepFrame < AbSettleFrames)
                {
                    return;
                }

                ScreenCapture.CaptureScreenshot(AbOnScreenshotPath);
                AdvanceAbStep(4, frame);
                return;

            default:
                if (!File.Exists(AbOnScreenshotPath))
                {
                    if (frame - stepFrame < AbFileWaitFrames)
                    {
                        return;
                    }

                    FailPlayAutoCapture(
                        $"A/B: lights-on frame '{AbOnScreenshotPath}' never landed. Current state: {readiness.State}");
                    return;
                }

                CompleteCharacterLightingAb(frame, readiness);
                return;
        }
    }

    private static void CompleteCharacterLightingAb(int frame, BattleCaptureReadiness readiness)
    {
        var stepIndex = SessionState.GetInt(CaptureStepIndexKey, readiness.StepIndex);
        var aliveUnits = SessionState.GetInt(CaptureAliveUnitsKey, readiness.AliveUnits);
        var totalUnits = SessionState.GetInt(CaptureTotalUnitsKey, readiness.TotalUnits);
        if (!BattleCaptureResultFile.TryWriteSuccess(
                AbOffScreenshotPath,
                AbOnScreenshotPath,
                MinUsefulCaptureLuminance,
                stepIndex,
                aliveUnits,
                totalUnits,
                out var metrics,
                out var error))
        {
            FailPlayAutoCapture($"A/B: {error}");
            return;
        }

        Debug.Log(
            $"[BattleSceneCaptureTool] A/B captured at frame {frame}: " +
            $"'{AbOffScreenshotPath}' ({metrics.OffBytes} bytes, luminance={metrics.OffLuminance:0.000000}) and " +
            $"'{AbOnScreenshotPath}' ({metrics.OnBytes} bytes, luminance={metrics.OnLuminance:0.000000}); " +
            $"bytesDiffer={metrics.BytesDiffer}, captureStep={stepIndex}, " +
            $"aliveUnits={aliveUnits}/{totalUnits}. {readiness.State}");
        FinishPlayAutoCapture();
    }

    private static bool TrySetCharacterLighting(bool enabled)
    {
        var environment = UnityEngine.Object.FindFirstObjectByType<BattleRenderEnvironmentAuthoring>();
        if (environment == null)
        {
            return false;
        }

        environment.SetCharacterLightingEnabled(enabled);
        return true;
    }

    private static void AdvanceAbStep(int step, int frame)
    {
        SessionState.SetInt(AbStepKey, step);
        SessionState.SetInt(AbStepFrameKey, frame);
    }

    private static void FinishPlayAutoCapture()
    {
        ClearPlayAutoState();
        EditorPrefs.DeleteKey(FirstPlayableBootstrap.CombatSandboxRequestedKey);
        if (EditorApplication.isPlaying)
        {
            EditorApplication.ExitPlaymode();
        }
    }

    private static void FailPlayAutoCapture(string reason)
    {
        Debug.LogError($"[BattleSceneCaptureTool] PlayAuto failed: {reason}");
        if (SessionState.GetBool(AbModeKey, false))
        {
            WriteFailureResult(reason);
        }

        ClearPlayAutoState();
        EditorPrefs.DeleteKey(FirstPlayableBootstrap.CombatSandboxRequestedKey);
        if (EditorApplication.isPlaying)
        {
            EditorApplication.ExitPlaymode();
        }
        else if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall -= ExitPlayModeIfNeeded;
            EditorApplication.delayCall += ExitPlayModeIfNeeded;
        }
    }

    private static void ExitPlayModeIfNeeded()
    {
        if (EditorApplication.isPlaying)
        {
            EditorApplication.ExitPlaymode();
        }
    }

    private static void WriteFailureResult(string reason)
    {
        BattleCaptureResultFile.WriteFailure(
            reason,
            AbOffScreenshotPath,
            AbOnScreenshotPath,
            SessionState.GetInt(CaptureStepIndexKey, -1),
            SessionState.GetInt(CaptureAliveUnitsKey, 0),
            SessionState.GetInt(CaptureTotalUnitsKey, 0));
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void ClearPlayAutoState()
    {
        EditorApplication.delayCall -= EnterPlayModeAfterMenuDispatch;
        SessionState.EraseBool(PlayAutoPendingKey);
        SessionState.EraseInt(PlayAutoFrameKey);
        SessionState.EraseBool(ScreenshotRequestedKey);
        SessionState.EraseBool(AbModeKey);
        SessionState.EraseInt(AbStepKey);
        SessionState.EraseInt(AbStepFrameKey);
        SessionState.EraseBool(PlayEntryCallbackPendingKey);
        SessionState.EraseBool(PlayEntryRequestedKey);
        SessionState.EraseInt(PlayEntryWaitUpdatesKey);
        SessionState.EraseBool(PlaybackPausedKey);
        SessionState.EraseInt(CaptureStepIndexKey);
        SessionState.EraseInt(CaptureAliveUnitsKey);
        SessionState.EraseInt(CaptureTotalUnitsKey);
    }

}
