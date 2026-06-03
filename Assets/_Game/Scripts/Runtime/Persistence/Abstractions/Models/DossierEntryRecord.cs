using System;
using System.Collections.Generic;

namespace SM.Persistence.Abstractions.Models;

/// <summary>
/// 한 번의 전투(sortie node) 결과를 캠페인 영구 기록으로 남기는 ledger entry.
/// ludonarrative 루프의 "전투 → 기록" 절반: 전투 결과가 휘발성 컷신 소품이 아니라
/// save truth의 실제 상태값이 되게 한다. (설계: analysis-ludonarrative-loop-implementation)
/// 기존 RunSummaryRecord / RewardLedgerEntryRecord 와 같은 ledger 패턴.
/// </summary>
[Serializable]
public sealed class DossierEntryRecord
{
    public string EntryId = string.Empty;
    public string RunId = string.Empty;
    public string ChapterId = string.Empty;
    public string SiteId = string.Empty;
    public string NodeId = string.Empty;

    /// <summary>"victory" | "defeat".</summary>
    public string Result = string.Empty;

    /// <summary>DossierOutcomeClassifier 토큰: "clean_victory" | "costly_victory" | "defeat".</summary>
    public string Outcome = string.Empty;

    public int SurvivorAllyCount;
    public int TotalAllyCount;

    /// <summary>전투에서 쓰러진 ally roster unit id — "빈 줄로 만든 사람".</summary>
    public List<string> FallenAllyIds = new();

    /// <summary>
    /// 이 sortie에서 서약한 Warrant id(없으면 ""). ludonarrative 루프 P2a — 출격 전 서약.
    /// stable id(`warrant_swift` 등), 표시명은 label에서 별도(ID/label 분리). ADR-0027.
    /// </summary>
    public string WarrantId = string.Empty;

    /// <summary>WarrantJudge 토큰: "kept" | "broken" | "failed_mission" | "not_applicable".</summary>
    public string WarrantOutcome = string.Empty;

    /// <summary>위반 원인 토큰: "none" | "defeated" | "turn_limit_exceeded" | "ally_killed" (+P3 원인).</summary>
    public string WarrantFailureReason = string.Empty;

    /// <summary>위반 무게 토큰: "none" | "minor" | "major" | "irreparable". 세력 반응 강도 산정용.</summary>
    public string WarrantSeverity = string.Empty;

    /// <summary>판정 시점 관측 사실 — "왜 깼나"를 결과창·Dossier UI에서 재구성하게 한다(GPT Pro §5.3).</summary>
    public int WarrantObservedTurnCount;

    /// <summary>그때 적용된 resolved turn 임계(encounter-relative, patch 후 과거 기록 보존용).</summary>
    public int WarrantResolvedTurnLimit;

    /// <summary>warrant를 발행한 정치 세력(없으면 ""). "누구에게 한 약속인가". ADR-0028.</summary>
    public string IssuerFactionId = string.Empty;

    /// <summary>이 warrant로 거스른 세력(없으면 ""). "누구의 기준을 거절했나".</summary>
    public string OpposedFactionId = string.Empty;

    public string CompletedAtUtc = string.Empty;
}
