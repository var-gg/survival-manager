namespace SM.Meta;

/// <summary>
/// 서약 이행 판정 결과 종류. GPT Pro 검수(§5.2) 반영: 패배를 단순 Broken으로 뭉치지 않는다 —
/// 패배(임무 자체 미완)와 "이겼지만 약속 미달"은 정치적으로 다른 반응이어야 한다.
/// </summary>
public enum WarrantOutcome
{
    /// <summary>서약 없음(미선택) — 판정 대상 아님.</summary>
    NotApplicable,

    /// <summary>약속을 지킴.</summary>
    Kept,

    /// <summary>승리했으나 약속 조건 미달(늦음/손실 등).</summary>
    Broken,

    /// <summary>패배 — 임무 자체를 가져오지 못함(약속 이전의 실패).</summary>
    FailedMission,
}

/// <summary>
/// 서약이 깨진 원인. "깼다"가 아니라 "왜 깼나"를 남겨 세력 반응을 차등 적용한다(GPT Pro §5.2).
/// P3 확장: ProtecteeKilled, EvidenceDestroyed, TargetKilled 등 전투 엔티티 objective 원인.
/// </summary>
public enum WarrantFailureReason
{
    None,

    /// <summary>전투 패배.</summary>
    Defeated,

    /// <summary>속전 — turn 임계 초과.</summary>
    TurnLimitExceeded,

    /// <summary>온전 — squad 손실 발생.</summary>
    AllyKilled,
}

/// <summary>
/// 위반의 무게 — 세력 반응 강도 산정용(GPT Pro §5.2). 슬라이스 1 기본값은 보수적 placeholder,
/// 실제 가중은 FactionEffectProfile(P2b)에서 튜닝한다.
/// </summary>
public enum WarrantSeverity
{
    None,
    Minor,
    Major,
    Irreparable,
}

/// <summary>
/// Warrant 판정의 reason-bearing 결과. outcome만이 아니라 원인·관측 사실·무게를 함께 운반해
/// Dossier(영구 원장)가 "왜 지켰거나 깼는가"를 재구성 가능하게 한다(GPT Pro §5.2/§5.3).
/// </summary>
public sealed record WarrantJudgment(
    WarrantOutcome Outcome,
    WarrantFailureReason FailureReason,
    WarrantSeverity Severity,
    int ObservedTurnCount,
    int ResolvedTurnLimit,
    int SurvivorAllyCount,
    int TotalAllyCount)
{
    public static WarrantJudgment NotApplicable(BattleFactSet facts) => new(
        WarrantOutcome.NotApplicable,
        WarrantFailureReason.None,
        WarrantSeverity.None,
        facts.TurnCount,
        ResolvedTurnLimit: 0,
        facts.SurvivorAllyCount,
        facts.TotalAllyCount);

    public static string OutcomeToken(WarrantOutcome outcome) => outcome switch
    {
        WarrantOutcome.NotApplicable => "not_applicable",
        WarrantOutcome.Kept => "kept",
        WarrantOutcome.Broken => "broken",
        WarrantOutcome.FailedMission => "failed_mission",
        _ => "unknown",
    };

    public static string FailureReasonToken(WarrantFailureReason reason) => reason switch
    {
        WarrantFailureReason.None => "none",
        WarrantFailureReason.Defeated => "defeated",
        WarrantFailureReason.TurnLimitExceeded => "turn_limit_exceeded",
        WarrantFailureReason.AllyKilled => "ally_killed",
        _ => "unknown",
    };

    public static string SeverityToken(WarrantSeverity severity) => severity switch
    {
        WarrantSeverity.None => "none",
        WarrantSeverity.Minor => "minor",
        WarrantSeverity.Major => "major",
        WarrantSeverity.Irreparable => "irreparable",
        _ => "unknown",
    };
}
