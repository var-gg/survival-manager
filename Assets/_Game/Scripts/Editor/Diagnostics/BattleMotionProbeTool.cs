using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SM.Editor.Diagnostics;

/// <summary>
/// Tier-1 모션 결함(피격 리액션 부재/사망 시 본체 증발) 라이브 진단. PlayMode 전투 중 활성화하면
/// 약 12초간 매 에디터 업데이트마다 모든 BattleActorView를 샘플링해 (a) 드라이버 one-shot 클립
/// 전환 이력(히트 리액션/death 클립이 실제로 재생되는지), (b) 렌더러 활성/스케일/위치(시체가
/// 숨겨지는지)를 기록하고 콘솔에 단일 로그로 출력한다. 읽기 전용 — 상태를 바꾸지 않는다.
/// </summary>
public static class BattleMotionProbeTool
{
    private const double ProbeSeconds = 12.0;
    private const double SnapshotInterval = 1.0;

    private static double s_startTime;
    private static double s_lastSnapshot;
    private static StringBuilder? s_log;
    private static readonly Dictionary<int, string> s_lastClip = new();

    [MenuItem("SM/Internal/Diagnostics/Battle Motion Probe (12s)")]
    public static void Start()
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("[MotionProbe] PlayMode 전투 중에 실행해야 한다.");
            return;
        }

        s_startTime = EditorApplication.timeSinceStartup;
        s_lastSnapshot = 0d;
        s_log = new StringBuilder(16384);
        s_lastClip.Clear();
        s_log.AppendLine("[MotionProbe] start");
        EditorApplication.update -= OnUpdate;
        EditorApplication.update += OnUpdate;
    }

    private static void OnUpdate()
    {
        if (s_log == null)
        {
            EditorApplication.update -= OnUpdate;
            return;
        }

        var elapsed = EditorApplication.timeSinceStartup - s_startTime;
        if (elapsed >= ProbeSeconds || !Application.isPlaying)
        {
            // play 중에는 unity-cli console 조회가 빈 값을 주므로 파일에도 기록한다 (진단 전용 산출물).
            var path = System.IO.Path.Combine(Application.dataPath, "..", "Logs", "motion_probe.txt");
            System.IO.File.WriteAllText(path, s_log.ToString());
            Debug.Log(s_log.ToString());
            s_log = null;
            EditorApplication.update -= OnUpdate;
            return;
        }

        var views = Object.FindObjectsByType<SM.Unity.BattleActorView>(FindObjectsSortMode.None);
        var snapshot = elapsed - s_lastSnapshot >= SnapshotInterval;
        if (snapshot)
        {
            s_lastSnapshot = elapsed;
        }

        foreach (var view in views)
        {
            var driver = view.GetComponent<SM.Unity.BattleHumanoidAnimationDriver>()
                         ?? view.GetComponentInChildren<SM.Unity.BattleHumanoidAnimationDriver>(true);
            var clip = driver != null && driver.CurrentOneShotClip != null ? driver.CurrentOneShotClip.name : "-";
            var id = view.GetInstanceID();
            if (!s_lastClip.TryGetValue(id, out var previous) || previous != clip)
            {
                s_lastClip[id] = clip;
                s_log.AppendLine($"t={elapsed:0.00} CLIP {view.name}: {previous ?? "(none)"} -> {clip}");
            }

            if (snapshot)
            {
                var renderers = view.GetComponentsInChildren<Renderer>(true);
                var enabledCount = 0;
                foreach (var renderer in renderers)
                {
                    if (renderer.enabled && renderer.gameObject.activeInHierarchy)
                    {
                        enabledCount++;
                    }
                }

                var scale = view.transform.lossyScale;
                s_log.AppendLine($"t={elapsed:0.00} SNAP {view.name}: pos={view.transform.position.x:0.0},{view.transform.position.z:0.0} scaleY={scale.y:0.00} renderersOn={enabledCount}/{renderers.Length} active={view.gameObject.activeInHierarchy}");
            }
        }

        if (snapshot && views.Length == 0)
        {
            s_log.AppendLine($"t={elapsed:0.00} SNAP (no BattleActorView found)");
        }
    }
}
