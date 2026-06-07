using System;

namespace SM.Combat.Services;

/// <summary>
/// 결정적 정수 틱 변환 권위 (ADR-0029 §Time / Phase 2). sim의 시간 SoT는 정수 틱이며 1틱 = 1/TickRate 초다
/// (<c>BattleState.FixedStepSeconds=0.1</c>은 display-derived). 콘텐츠가 저작한 초 단위 duration을
/// <see cref="DurationToTicks"/>로 정수 틱에 단 한 번 양자화한다 — 기본 사칙연산만 쓰므로(초월함수·누산 없음)
/// IEEE754 correctly-rounded 보장 아래 x86/ARM cross-platform 동일하다. 이는
/// <see cref="BattleWindupTickMath.StepsUntilResolve"/>의 float 감산 루프(누적 드리프트 위험)를 결정적으로 대체한다.
///
/// <para>주의: seconds 입력은 콘텐츠 저작값(ingress)이다. Phase 5 경계 폐쇄에서 이 변환을 BattleFactory/loadout
/// compile의 유일 ingress로 모으고, sim 내부는 정수 틱만 들고 다니게 한다.</para>
/// </summary>
public static class BattleTickMath
{
    /// <summary>정수 틱레이트 SoT(10Hz). 1틱 = 0.1초.</summary>
    public const int DefaultTickRate = 10;

    // sec*rate가 정수에 정확히 떨어질 때(예: 0.3*10=3.0) float 오차로 ceil이 한 틱 부풀지 않게 하는 여유.
    private const double CeilEpsilon = 1e-4;

    /// <summary>
    /// 초 → 정수 틱. positive duration은 최소 1틱(0틱 금지 — 같은 틱 다단공격 방지), 0 이하는 0틱(명시 Instant).
    /// <c>ticks = max(1, ceil(seconds*tickRate - eps))</c>.
    /// </summary>
    public static int DurationToTicks(double seconds, int tickRate = DefaultTickRate)
    {
        if (seconds <= 0d || tickRate <= 0)
        {
            return 0;
        }

        var ticks = (int)Math.Ceiling((seconds * tickRate) - CeilEpsilon);
        return ticks < 1 ? 1 : ticks;
    }
}
