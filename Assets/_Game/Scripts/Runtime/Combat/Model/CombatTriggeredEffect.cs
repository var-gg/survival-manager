using SM.Core.Contracts;

namespace SM.Combat.Model;

/// <summary>
/// 런타임 트리거 효과 — 증강·패시브의 authored <c>TriggeredEffectSpec</c>(SM.Content)이 변환되어 들어오는 전투 입력.
/// <see cref="Services.CombatTriggerEngine"/> 가 시뮬레이터 hook(전투 시작 / 처치 / 체력 임계)에서 평가·실행한다.
/// 상시 적용되는 <see cref="CombatModifierPackage"/> 와는 별개 채널(시점·조건 발동).
/// </summary>
public sealed record CombatTriggeredEffect(
    string SourceId,
    CombatTriggerKind Trigger,
    TriggeredEffectOp Op,
    EffectScope Scope = EffectScope.Self,
    float Magnitude = 0f,
    float ThresholdRatio = 0f,
    string StatusId = "",
    float DurationSeconds = 0f,
    int MaxStacks = 1);
