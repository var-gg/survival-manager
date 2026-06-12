using NUnit.Framework;
using SM.Unity;

namespace SM.Tests.EditMode;

/// <summary>
/// 스핀 수정 C3 계약 — travel facing은 의도적 이동(연속 step + 속도)에만 열린다.
/// separation 미세 푸시(저속 지속)와 progress-gate 탈출 펄스(고속 단발)는 둘 다 차단,
/// 실제 접근 이동(고속 지속)은 짧은 지연 후 활성.
/// </summary>
[Category("FastUnit")]
public class BattleTravelFacingGateTests
{
    private const float TravelSpeed = 2.1f;   // 통상 MoveSpeed 풀스텝
    private const float JitterSpeed = 0.3f;   // separation 미세 푸시 영역(문턱 0.15 위, 의도 0.6 아래)

    [Test]
    public void SustainedTravel_ActivatesAfterActivationSteps()
    {
        var gate = new BattleTravelFacingGate();

        Assert.That(gate.Sample(0, TravelSpeed), Is.False, "첫 의도적 step만으로는 비활성");
        Assert.That(gate.Sample(1, TravelSpeed), Is.True, "연속 2 step 의도적 이동이면 활성");
        Assert.That(gate.Sample(2, TravelSpeed), Is.True, "지속 이동 동안 유지");
    }

    [Test]
    public void SingleStepPulse_NeverActivates()
    {
        var gate = new BattleTravelFacingGate();

        // progress-gate 탈출 펄스: 8틱마다 1 step짜리 고속 측방 이동
        for (var step = 0; step < 32; step++)
        {
            var speed = step % 8 == 0 ? TravelSpeed : 0f;
            Assert.That(gate.Sample(step, speed), Is.False, $"단발 펄스(step {step})로는 절대 활성화되지 않아야 한다");
        }
    }

    [Test]
    public void SustainedMicroJitter_NeverActivates()
    {
        var gate = new BattleTravelFacingGate();

        // separation 미세 푸시: locomotion 문턱(0.15m/s)은 넘지만 의도적 속도엔 못 미치는 지속 이동
        for (var step = 0; step < 20; step++)
        {
            Assert.That(gate.Sample(step, JitterSpeed), Is.False, "저속 지터는 지속돼도 활성화되지 않아야 한다");
        }
    }

    [Test]
    public void SlowStep_DeactivatesImmediately()
    {
        var gate = new BattleTravelFacingGate();
        gate.Sample(0, TravelSpeed);
        gate.Sample(1, TravelSpeed);
        Assert.That(gate.IsActive, Is.True);

        Assert.That(gate.Sample(2, 0.05f), Is.False, "비의도 step 1회로 즉시 해제(도착 즉시 전투 응시 복귀)");
        Assert.That(gate.Sample(3, TravelSpeed), Is.False, "해제 후엔 다시 연속 조건을 채워야 한다");
        Assert.That(gate.Sample(4, TravelSpeed), Is.True);
    }

    [Test]
    public void SameStepResample_DoesNotDoubleCount()
    {
        var gate = new BattleTravelFacingGate();

        // 같은 sim step의 렌더 프레임 재호출(보간)은 연속 step으로 세지 않는다
        gate.Sample(0, TravelSpeed);
        gate.Sample(0, TravelSpeed);
        gate.Sample(0, TravelSpeed);
        Assert.That(gate.IsActive, Is.False, "같은 step 재표본은 누적되지 않아야 한다");

        gate.Sample(1, TravelSpeed);
        Assert.That(gate.IsActive, Is.True);
    }

    [Test]
    public void ExactThresholdSpeed_CountsAsDeliberate()
    {
        var gate = new BattleTravelFacingGate();

        // 경계 계약: 정확히 DeliberateSpeedThreshold면 의도적 이동(>= 비교)
        Assert.That(gate.Sample(0, BattleTravelFacingGate.DeliberateSpeedThreshold), Is.False);
        Assert.That(gate.Sample(1, BattleTravelFacingGate.DeliberateSpeedThreshold), Is.True);

        gate.Reset();
        var justBelow = BattleTravelFacingGate.DeliberateSpeedThreshold - 0.001f;
        gate.Sample(0, justBelow);
        Assert.That(gate.Sample(1, justBelow), Is.False, "문턱 바로 아래는 지속돼도 비활성");
    }

    [Test]
    public void Reset_ClearsActivation()
    {
        var gate = new BattleTravelFacingGate();
        gate.Sample(0, TravelSpeed);
        gate.Sample(1, TravelSpeed);
        Assert.That(gate.IsActive, Is.True);

        gate.Reset();
        Assert.That(gate.IsActive, Is.False);
        Assert.That(gate.Sample(0, TravelSpeed), Is.False, "리셋 후 같은 step 인덱스도 새로 표본화된다");
        Assert.That(gate.Sample(1, TravelSpeed), Is.True);
    }
}
