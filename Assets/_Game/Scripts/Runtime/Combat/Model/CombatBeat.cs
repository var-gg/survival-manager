using SM.Core.Ids;

namespace SM.Combat.Model;

/// <summary>
/// Phase 3 시너지/콤보 발현 채널의 beat 종류. beat 은 "화면 사건으로 승격될 자격이 있는 전투 사실"의
/// 단위 레코드다 — 시너지 브레이크포인트 발동, 증강 트리거 발동, 콤보 연쇄(primer→consume)가 여기 실린다.
/// 저수준 사실 로그(<see cref="BattleEvent"/>)·액션 생명주기(<see cref="BattleCombatEventIntent"/>)와 별개로,
/// presentation director(Phase 4)와 post-fight 로그가 그대로 읽을 중요도·연쇄 의미가 붙는다.
/// </summary>
public enum CombatBeatType
{
    /// <summary>전투 시작 시 활성화된 시너지 브레이크포인트(팀 단위, step 0).</summary>
    SynergyActivated = 0,

    /// <summary>증강/패시브 BattleStart 트리거 발동(시작 배리어 등, step 0).</summary>
    BattleStartEffect = 1,

    /// <summary>증강/패시브 OnKill 트리거 발동(처치 회복 등).</summary>
    OnKillEffect = 2,

    /// <summary>증강/패시브 OnHpBelow 임계 트리거 발동(빈사 배리어 등 — clutch 계열).</summary>
    HpThresholdEffect = 3,

    /// <summary>증강/패시브 OnAllyDeath 트리거 발동.</summary>
    AllyDeathEffect = 4,

    /// <summary>콤보 프라이머(세팅 디버프) 적용 — 같은 연쇄의 소비와 ChainId 를 공유한다.</summary>
    ComboPrimerApplied = 5,

    /// <summary>콤보 소비 — 프라이머 윈도우 안의 후속 타격이 연쇄를 완성했다.</summary>
    ComboConsumed = 6,
}

/// <summary>
/// beat 기본 중요도(0~100) — Phase 4 카메라/UI 강조 우선순위의 입력. 마스터 플랜 기준값으로 시작하며
/// 값 자체는 V1 authority 튜닝 노브다.
/// </summary>
public static class CombatBeatImportance
{
    public const int SynergyActivated = 60;
    public const int BattleStartEffect = 60;
    public const int OnKillEffect = 55;
    public const int HpThresholdEffect = 75;
    public const int AllyDeathEffect = 60;
    public const int ComboPrimerApplied = 55;
    public const int ComboConsumed = 80;
}

/// <summary>
/// 단일 beat 레코드. <see cref="StepIndex"/>/<see cref="SequenceInStep"/> 으로 결정론적 전순서를 가지며,
/// 콤보 연쇄는 <see cref="ChainId"/> 를 공유한다(연쇄 없음 = 0). <see cref="SourceId"/> 가 null 이면
/// 팀 단위 beat(시너지 발동)다. <see cref="Tag"/> 는 발생 출처 식별자(시너지/증강/status id)로
/// post-fight 로그와 presentation 라벨이 그대로 쓴다.
/// </summary>
public sealed record CombatBeat(
    int StepIndex,
    int SequenceInStep,
    CombatBeatType Type,
    TeamSide Side,
    EntityId? SourceId,
    EntityId? TargetId,
    int ChainId,
    int Importance,
    float Value,
    CombatVector2 Position,
    string Tag);
