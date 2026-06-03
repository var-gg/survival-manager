namespace SM.Meta;

/// <summary>
/// 한 sortie의 squad 생존 결과 분류. ludonarrative 루프 P1a의 순수 판정 코어 —
/// SM.Combat / SM.Unity 의존 없이 EditMode로 단위 검증 가능하게 분리한다.
/// (설계: analysis-ludonarrative-loop-implementation)
/// </summary>
public enum DossierOutcome
{
    /// <summary>패배.</summary>
    Defeat,

    /// <summary>승리했지만 ally roster unit 일부가 쓰러짐 — 손실 있는 승리.</summary>
    CostlyVictory,

    /// <summary>승리 + 전원 귀환(손실 0).</summary>
    CleanVictory,
}

/// <summary>
/// 전투 사실(승패 + ally 생존/총원)을 DossierOutcome으로 분류하는 순수 함수.
/// P1a는 squad 생존만 본다. 민간인 구조·증거 확보 같은 objective는 combat 모델링이
/// 붙는 P2에서 별도 분류로 확장한다.
/// </summary>
public static class DossierOutcomeClassifier
{
    public static DossierOutcome Classify(bool victory, int survivorAllyCount, int totalAllyCount)
    {
        if (!victory)
        {
            return DossierOutcome.Defeat;
        }

        // 손실 0(전원 귀환) = clean. 승리했지만 누군가 쓰러짐 = costly.
        // totalAllyCount == 0(로스터 정보 없음)이면 vacuously clean으로 둔다.
        return survivorAllyCount >= totalAllyCount
            ? DossierOutcome.CleanVictory
            : DossierOutcome.CostlyVictory;
    }

    public static string ToToken(DossierOutcome outcome)
    {
        return outcome switch
        {
            DossierOutcome.Defeat => "defeat",
            DossierOutcome.CostlyVictory => "costly_victory",
            DossierOutcome.CleanVictory => "clean_victory",
            _ => "unknown",
        };
    }
}
