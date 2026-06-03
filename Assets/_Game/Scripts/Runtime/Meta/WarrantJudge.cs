namespace SM.Meta;

/// <summary>
/// 서약(<see cref="WarrantSpec"/>)을 전투 사실로 판정하는 순수 함수. ludonarrative 루프 P2a의
/// 판정 코어 — SM.Combat / SM.Unity 의존 없이 EditMode로 단위 검증 가능하게 분리한다.
/// combat은 사실(<c>BattleResult</c>: 승패·생존·turn 수)만 산출하고, "약속을 지켰나"의 판정은
/// 여기(Meta)서 한다 — combat 순수성 보존(ADR-0006/0027).
/// </summary>
public static class WarrantJudge
{
    /// <param name="spec">서약 정의. null이면 미서약(NotApplicable).</param>
    /// <param name="victory">전투 승리 여부.</param>
    /// <param name="survivorAllyCount">생존 ally roster unit 수.</param>
    /// <param name="totalAllyCount">출격 ally roster unit 총원.</param>
    /// <param name="stepCount">전투 종료 시 sim step 수(<c>BattleResult.StepCount</c>).</param>
    public static WarrantOutcome Judge(
        WarrantSpec? spec,
        bool victory,
        int survivorAllyCount,
        int totalAllyCount,
        int stepCount)
    {
        if (spec is null)
        {
            return WarrantOutcome.NotApplicable;
        }

        // 패배는 어떤 서약이든 임무를 못 가져온 것 — 깬 것으로 본다.
        if (!victory)
        {
            return WarrantOutcome.Broken;
        }

        var kept = spec.Kind switch
        {
            // 속전: turn 임계 이하로 끝냄.
            WarrantKind.Swift => stepCount <= spec.SwiftStepThreshold,
            // 온전: squad 전원 귀환(손실 0). totalAllyCount==0(로스터 미상)은 vacuously kept.
            WarrantKind.Intact => survivorAllyCount >= totalAllyCount,
            _ => false,
        };

        return kept ? WarrantOutcome.Kept : WarrantOutcome.Broken;
    }

    public static string ToToken(WarrantOutcome outcome)
    {
        return outcome switch
        {
            WarrantOutcome.NotApplicable => "not_applicable",
            WarrantOutcome.Kept => "kept",
            WarrantOutcome.Broken => "broken",
            _ => "unknown",
        };
    }
}
