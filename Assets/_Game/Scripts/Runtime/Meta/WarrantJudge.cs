namespace SM.Meta;

/// <summary>
/// 서약(<see cref="WarrantSpec"/>)을 전투 사실(<see cref="BattleFactSet"/>)로 판정하는 순수 함수.
/// ludonarrative 루프 P2a의 판정 코어 — SM.Combat / SM.Unity 의존 없이 EditMode로 단위 검증.
/// combat은 사실(BattleResult)만 산출하고 "약속을 지켰나"의 판정은 여기(Meta)서 한다 — combat 순수성(ADR-0006/0027).
///
/// GPT Pro 검수(§5.1/§5.2) 반영: fact bag + reason-bearing judgment. Swift/Intact는 fact-query
/// adapter이고, P3(Protect/Evidence/NonLethal)는 WarrantKind 케이스 추가만으로 확장된다 —
/// signature(spec, facts, context)와 BattleFactSet/WarrantJudgment는 그대로다.
/// </summary>
public static class WarrantJudge
{
    /// <param name="spec">서약 정의. null이면 미서약(NotApplicable).</param>
    /// <param name="facts">전투가 산출한 objective-agnostic 사실(SM.Unity가 BattleResult에서 조립).</param>
    /// <param name="context">encounter-relative 판정 문맥(Swift turn limit 등).</param>
    public static WarrantJudgment Judge(WarrantSpec? spec, BattleFactSet facts, EncounterContext context)
    {
        if (spec is null)
        {
            return WarrantJudgment.NotApplicable(facts);
        }

        var resolvedTurnLimit = ResolveTurnLimit(spec, context);

        // 패배는 약속 이전의 실패 — 임무 자체를 못 가져왔다(FailedMission). 단순 Broken과 구분.
        if (!facts.Victory)
        {
            return Build(WarrantOutcome.FailedMission, WarrantFailureReason.Defeated, WarrantSeverity.Major, facts, resolvedTurnLimit);
        }

        return spec.Kind switch
        {
            // 속전: encounter-resolved turn 임계 이하로 끝냄.
            WarrantKind.Swift => facts.TurnCount <= resolvedTurnLimit
                ? Build(WarrantOutcome.Kept, WarrantFailureReason.None, WarrantSeverity.None, facts, resolvedTurnLimit)
                : Build(WarrantOutcome.Broken, WarrantFailureReason.TurnLimitExceeded, WarrantSeverity.Minor, facts, resolvedTurnLimit),

            // 온전: squad 손실 0. 자기 사람을 잃는 것은 무겁게(Major) 본다.
            WarrantKind.Intact => facts.AllyDeaths == 0
                ? Build(WarrantOutcome.Kept, WarrantFailureReason.None, WarrantSeverity.None, facts, resolvedTurnLimit)
                : Build(WarrantOutcome.Broken, WarrantFailureReason.AllyKilled, WarrantSeverity.Major, facts, resolvedTurnLimit),

            _ => Build(WarrantOutcome.Broken, WarrantFailureReason.None, WarrantSeverity.Minor, facts, resolvedTurnLimit),
        };
    }

    /// <summary>
    /// 적용할 Swift turn 임계. context가 양수면 encounter-relative 값(P2b/content)을, 아니면 spec placeholder.
    /// </summary>
    private static int ResolveTurnLimit(WarrantSpec spec, EncounterContext context)
    {
        return context.ResolvedSwiftTurnLimit > 0 ? context.ResolvedSwiftTurnLimit : spec.SwiftStepThreshold;
    }

    private static WarrantJudgment Build(
        WarrantOutcome outcome,
        WarrantFailureReason reason,
        WarrantSeverity severity,
        BattleFactSet facts,
        int resolvedTurnLimit)
    {
        return new WarrantJudgment(
            outcome,
            reason,
            severity,
            facts.TurnCount,
            resolvedTurnLimit,
            facts.SurvivorAllyCount,
            facts.TotalAllyCount);
    }
}
