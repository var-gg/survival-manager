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

    /// <summary>WarrantJudge 토큰: "kept" | "broken" | "not_applicable"(서약 없음).</summary>
    public string WarrantOutcome = string.Empty;

    public string CompletedAtUtc = string.Empty;
}
