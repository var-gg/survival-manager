using System;
using System.Collections.Generic;

namespace SM.Persistence.Abstractions.Models;

[Serializable]
public sealed class MatchRecordBlob
{
    public string MatchId = string.Empty;
    public string CompileVersion = string.Empty;
    public string CompileHash = string.Empty;
    public string InputDigest = string.Empty;
    public string BattleSummaryDigest = string.Empty;
    public string ReadabilityDigest = string.Empty;
    public List<string> EventStream = new();
    public List<string> KeyframeDigests = new();
    public List<string> TelemetryEvents = new();
    public List<string> ArtifactPaths = new();
    // ADR-0028 #provenance: 이 전투에 적용된 정치 조건 provenance("factionId|channel|reasonCode" stable token).
    // hash는 효과만 포착 — 출처 mandate는 여기 남는다. 기본값 new()로 backward-compatible.
    public List<string> PoliticalConditions = new();
}
