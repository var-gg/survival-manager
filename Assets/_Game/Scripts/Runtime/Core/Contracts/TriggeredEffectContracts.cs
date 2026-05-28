namespace SM.Core.Contracts;

/// <summary>
/// 증강·패시브가 전투 중 특정 순간에 효과를 발동하는 트리거 종류.
/// 상시 적용되는 정적 스탯 modifier 와 달리, "전투 순간"에 조건부로 한 번 발동된다.
/// 타깃 범위는 기존 <see cref="EffectScope"/> 를 재사용한다 (Self / AlliedCombatants / EnemyCombatants / CurrentTarget).
/// </summary>
public enum CombatTriggerKind
{
    /// <summary>전투 시작 시 1회.</summary>
    BattleStart = 0,

    /// <summary>이 효과를 소유한 유닛이 적을 처치했을 때 (처치마다).</summary>
    OnKill = 1,

    /// <summary>소유 유닛 체력이 임계 비율(ThresholdRatio) 이하로 처음 떨어졌을 때 — 전투당 1회 latch.</summary>
    OnHpBelow = 2,
}

/// <summary>
/// 트리거 발동 시 실행할 효과 연산. 전부 기존 UnitSnapshot 연산(ApplyStatus/Heal/AddBarrier/GainEnergy)으로 매핑되어
/// 전투 시스템에 새로운 스탯 레이어를 추가하지 않는다 (저위험).
/// </summary>
public enum TriggeredEffectOp
{
    /// <summary>상태(buff/debuff) 부여. StatusId + Magnitude + DurationSeconds + MaxStacks 사용.</summary>
    ApplyStatus = 0,

    /// <summary>즉시 회복. Magnitude(flat) 사용.</summary>
    Heal = 1,

    /// <summary>보호막 부여. Magnitude(flat) 사용.</summary>
    Barrier = 2,

    /// <summary>에너지 획득. Magnitude(flat) 사용.</summary>
    GainEnergy = 3,
}
