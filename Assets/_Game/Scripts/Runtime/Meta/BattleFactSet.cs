using System;

namespace SM.Meta;

/// <summary>
/// 전투가 산출한 objective-agnostic 사실 묶음 — Warrant 판정의 입력 계약.
/// GPT Pro 검수(§2/§5.1) 반영: scalar 인자 나열 대신 fact bag으로 받아 WarrantKind 확장 시
/// signature가 깨지지 않게 한다. SM.Combat은 이 타입을 모른다 — SM.Unity settlement이
/// BattleResult(전투 사실)에서 조립해 SM.Meta judge에 넘긴다(combat 순수성 보존).
///
/// P3 확장 지점: 민간인 보호·증거 확보·비살상은 전투가 새 사실을 산출해야 한다
/// (EntityFinalStates, InteractableEvents, Killed/Downed/CapturedEvents).
/// 그 사실이 BattleResult에 생기면 여기에 필드로 추가하고 WarrantJudge에 WarrantKind 케이스를
/// 더한다 — 이 record와 Judge signature는 그대로다. (ADR-0027)
/// </summary>
public sealed record BattleFactSet(
    bool Victory,
    int TurnCount,
    int SurvivorAllyCount,
    int TotalAllyCount)
{
    /// <summary>출격 squad 중 쓰러진 수.</summary>
    public int AllyDeaths => Math.Max(0, TotalAllyCount - SurvivorAllyCount);
}

/// <summary>
/// 한 sortie의 Warrant 판정 문맥 — encounter-relative 임계 등. GPT Pro 검수(§1.C/§5.1) 반영:
/// Swift turn limit는 전역 고정값이 아니라 encounter par-turn에서 해석돼야 의미가 산다.
/// 슬라이스 1은 spec의 placeholder 임계를 그대로 운반(ResolvedSwiftTurnLimit)하고,
/// 실제 encounter-relative 해석(encounterParTurn - pressureDelta)은 P2b/content가 채운다.
/// </summary>
public sealed record EncounterContext(int ResolvedSwiftTurnLimit);
