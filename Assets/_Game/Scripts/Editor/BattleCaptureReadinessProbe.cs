using System.Linq;
using SM.Combat.Model;
using SM.Unity;
using UnityEngine;

namespace SM.Editor.Tools;

/// <summary>
/// 실행 중인 Battle 화면이 중반 캡처 조건을 만족하는지 읽기 전용으로 관측한다.
/// </summary>
internal static class BattleCaptureReadinessProbe
{
    private const float MinimumAliveUnitRatio = 0.75f;

    internal static BattleCaptureReadiness Observe()
    {
        var screen = Object.FindFirstObjectByType<BattleScreenController>();
        var step = screen != null ? screen.LatestStep : null;
        var stepIndex = step?.StepIndex ?? -1;
        var expectedActorCount = step?.Units.Count ?? 0;
        var aliveUnits = step?.Units.Count(unit => unit.IsAlive) ?? 0;
        var damagedLivingUnits = step?.Units.Count(
            unit => unit.IsAlive
                    && unit.MaxHealth > 0f
                    && unit.CurrentHealth < unit.MaxHealth - 0.01f) ?? 0;
        var allyAlive = step?.Units.Count(unit => unit.IsAlive && unit.Side == TeamSide.Ally) ?? 0;
        var enemyAlive = step?.Units.Count(unit => unit.IsAlive && unit.Side == TeamSide.Enemy) ?? 0;
        var minimumAliveUnits = expectedActorCount > 0
            ? Mathf.CeilToInt(expectedActorCount * MinimumAliveUnitRatio)
            : 0;

        var wrappers = Object.FindObjectsByType<BattleActorWrapper>(FindObjectsSortMode.None);
        var activeWrapperCount = 0;
        var wrappersWithCharacterRenderer = 0;
        var firstMissingCharacterRenderer = "<none>";

        foreach (var wrapper in wrappers)
        {
            if (!wrapper.gameObject.activeInHierarchy)
            {
                continue;
            }

            activeWrapperCount++;
            var hasCharacterRenderer = wrapper
                .GetComponentsInChildren<Renderer>(true)
                .Any(IsCaptureReadyCharacterRenderer);
            if (hasCharacterRenderer)
            {
                wrappersWithCharacterRenderer++;
            }
            else if (firstMissingCharacterRenderer == "<none>")
            {
                firstMissingCharacterRenderer = wrapper.name;
            }
        }

        var state =
            $"controller={(screen != null ? "present" : "missing")}, stepIndex={stepIndex}, " +
            $"aliveUnits={aliveUnits}/{expectedActorCount}, minimumAliveUnits={minimumAliveUnits}, " +
            $"damagedLivingUnits={damagedLivingUnits}, allyAlive={allyAlive}, enemyAlive={enemyAlive}, " +
            $"activeWrappers={activeWrapperCount}, " +
            $"wrappersWithCharacterRenderer={wrappersWithCharacterRenderer}, " +
            $"firstMissingCharacterRenderer={firstMissingCharacterRenderer}";
        var captureWindowMissed = stepIndex > 0
                                  && expectedActorCount > 0
                                  && aliveUnits < minimumAliveUnits;
        var isReady = stepIndex > 0
                      && expectedActorCount > 0
                      && aliveUnits >= minimumAliveUnits
                      && damagedLivingUnits > 0
                      && allyAlive > 0
                      && enemyAlive > 0
                      && activeWrapperCount >= expectedActorCount
                      && wrappersWithCharacterRenderer >= expectedActorCount;

        return new BattleCaptureReadiness(
            isReady,
            captureWindowMissed,
            stepIndex,
            aliveUnits,
            expectedActorCount,
            state);
    }

    private static bool IsCaptureReadyCharacterRenderer(Renderer renderer)
    {
        if (!renderer.enabled
            || renderer.forceRenderingOff
            || !renderer.gameObject.activeInHierarchy)
        {
            return false;
        }

        return renderer.name != "ContactShadow"
               && renderer.name != "GroundShadow"
               && renderer.name != "PulseProxy";
    }
}

internal readonly struct BattleCaptureReadiness
{
    internal BattleCaptureReadiness(
        bool isReady,
        bool captureWindowMissed,
        int stepIndex,
        int aliveUnits,
        int totalUnits,
        string state)
    {
        IsReady = isReady;
        CaptureWindowMissed = captureWindowMissed;
        StepIndex = stepIndex;
        AliveUnits = aliveUnits;
        TotalUnits = totalUnits;
        State = state;
    }

    internal bool IsReady { get; }
    internal bool CaptureWindowMissed { get; }
    internal int StepIndex { get; }
    internal int AliveUnits { get; }
    internal int TotalUnits { get; }
    internal string State { get; }
}
