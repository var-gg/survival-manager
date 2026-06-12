namespace SM.Unity;

/// <summary>
/// Travel-facing 활성 게이트(스핀 수정 C3). "이동 방향 바라보기"는 의도적 이동에만 허용한다.
/// 기존에는 locomotion 문턱(0.15m/s = 틱당 1.5cm)만 넘으면 facing이 이동 방향을 쫓아서,
/// separation 미세 푸시(쌍별 radial, 방향 가변)와 progress-gate 탈출 펄스(8틱 단발 측방)가
/// facing 표적을 나침반처럼 돌리고 회전 평활이 그걸 쫓아 순변위 0의 제자리 회전이 됐다.
/// 활성 조건: 연속 ActivationSteps sim step 동안 의도적 속도(DeliberateSpeedThreshold 이상) 유지.
/// 비의도 step 1회로 즉시 해제(전투 응시 복귀). presentation 전용 — sim truth 불변.
/// </summary>
public sealed class BattleTravelFacingGate
{
    /// <summary>의도적 이동으로 인정하는 최소 속도(m/s) — separation 미세 푸시(통상 ≤0.5) 위,
    /// 실제 travel(MoveSpeed ~2.1) 아래.</summary>
    public const float DeliberateSpeedThreshold = 0.6f;

    /// <summary>활성화에 필요한 연속 의도적 step 수 — 단발 탈출 펄스(1 step)를 걸러낸다.</summary>
    public const int ActivationSteps = 2;

    private int _lastStepIndex = int.MinValue;
    private int _sustainedSteps;

    public bool IsActive { get; private set; }

    /// <summary>step당 1회만 표본화한다(같은 step의 렌더 프레임 재호출은 무시). worldSpeed는
    /// 해당 step의 틱 변위 기반 속도라 step 안에서 상수다.</summary>
    public bool Sample(int stepIndex, float worldSpeed)
    {
        if (stepIndex != _lastStepIndex)
        {
            _lastStepIndex = stepIndex;
            var deliberate = worldSpeed >= DeliberateSpeedThreshold;
            _sustainedSteps = deliberate ? System.Math.Min(_sustainedSteps + 1, ActivationSteps) : 0;
            IsActive = _sustainedSteps >= ActivationSteps;
        }

        return IsActive;
    }

    public void Reset()
    {
        _lastStepIndex = int.MinValue;
        _sustainedSteps = 0;
        IsActive = false;
    }
}
