using System;
using System.Collections.Generic;
using System.Linq;

namespace SM.Meta;

/// <summary>
/// 출격 전 squad가 서약하는 기준의 종류. ludonarrative 루프 P2a — 전투가 정치극의
/// "원인"이 되게 하는 앞단. 슬라이스 1은 전투가 이미 산출하는 사실(승패·생존·turn 수)만으로
/// 판정 가능한 종류만 둔다. 민간인 보호·증거 확보·비살상 같은 종류는 전투 엔티티 모델 변경이
/// 필요하므로 P3에서 확장한다. (설계: ADR-0027, pindoc analysis-p2-warrant-system-design)
/// </summary>
public enum WarrantKind
{
    /// <summary>속전(Swift): 승리하되 turn 수가 임계 이하 — 결단/속도를 요구하는 세력의 기준.</summary>
    Swift,

    /// <summary>온전(Intact): 승리하되 squad 손실 0 — 자기 사람을 지키라는 세력의 기준.</summary>
    Intact,
}

/// <summary>
/// 하나의 서약 정의. stable id(코드 식별자) + 판정 종류 + (Swift용) turn 임계.
/// 표시명은 여기 넣지 않는다 — ID/label 분리(localization label은 별도).
/// </summary>
/// <param name="IssuerFactionId">warrant를 발행한 정치 세력(없으면 "") — ADR-0028 정치 warrant.</param>
/// <param name="OpposedFactionId">이 warrant를 수락하면 거스르는 세력(없으면 "").</param>
public sealed record WarrantSpec(
    string Id,
    WarrantKind Kind,
    int SwiftStepThreshold = 0,
    string IssuerFactionId = "",
    string OpposedFactionId = "");

/// <summary>
/// 서약 id(overlay에 실려 settlement까지 운반되는 string) → <see cref="WarrantSpec"/> 해석.
/// 슬라이스 1 정의 카탈로그. "어떤 site가 어떤 서약을 거나"(site→warrant 매핑)는 여기 두지 않는다 —
/// 그건 lore이고, P2b의 선택 surface / authoring이 소유한다(코드 id에 lore 박지 않음, ADR-0027).
/// </summary>
public static class WarrantCatalog
{
    public const string SwiftId = "warrant_swift";
    public const string IntactId = "warrant_intact";

    /// <summary>
    /// 정치 세력 stable id (settled — pindoc analysis-narrative-reskin-4-faction-root-draft).
    /// 표시명(솔라룸/이리솔 부족/회상 결사/그물 결사)은 label layer 소유 — 코드 id에 lore 미박입(ID/label 분리).
    /// political 세력은 per-site enemy FactionId와 별개 layer(ADR-0028) — 매핑은 후속.
    /// </summary>
    public const string SolarumId = "faction_solarum";
    public const string WolfpineId = "faction_wolfpine_tribes";
    public const string PaleConclaveId = "faction_pale_conclave";
    public const string LatticeId = "faction_lattice_order";

    /// <summary>
    /// 세력 위임 서약 — issuer=수락하는 세력 기준, opposed=거스르는 세력. 조건은 세력 가치의 fact-predicate
    /// (질서/생명 보존 → Intact 손실0, 사냥/효율 → Swift 속전). Solarum(광휘 왕좌의 잔당)은 정화로 회상 결사를
    /// 박해한 패권이라 나머지 세력 위임이 대체로 Solarum을 거스른다(다극 회색 충돌).
    /// </summary>
    public const string SolarumOrderId = "warrant_solarum_order";
    public const string WolfpineHuntId = "warrant_wolfpine_hunt";
    public const string PaleConclaveVigilId = "warrant_pale_conclave_vigil";
    public const string LatticePrecisionId = "warrant_lattice_precision";

    // SwiftStepThreshold는 placeholder다. 서약이 live로 가는 P2b에서 실제 관측 stepCount에 맞춰
    // 튜닝한다(슬라이스 1은 ships dark — live에서 PledgedWarrantId="" 이라 미사용, test는 자체 임계 주입).
    private const int SwiftStepThresholdDefault = 600;

    private static readonly IReadOnlyDictionary<string, WarrantSpec> Specs =
        new Dictionary<string, WarrantSpec>(StringComparer.Ordinal)
        {
            [SwiftId] = new WarrantSpec(SwiftId, WarrantKind.Swift, SwiftStepThresholdDefault),
            [IntactId] = new WarrantSpec(IntactId, WarrantKind.Intact),
            // 질서/규율 → 손실 0. 정화의 가해자라 박해한 회상 결사를 거스른다.
            [SolarumOrderId] = new WarrantSpec(SolarumOrderId, WarrantKind.Intact, IssuerFactionId: SolarumId, OpposedFactionId: PaleConclaveId),
            // 사냥/결단 → 속전. 변경을 통제하는 왕국 잔당을 거스른다.
            [WolfpineHuntId] = new WarrantSpec(WolfpineHuntId, WarrantKind.Swift, SwiftStepThresholdDefault, IssuerFactionId: WolfpineId, OpposedFactionId: SolarumId),
            // 기억=영혼 → 한 명도 잃지 마라(손실 0). 박해자 Solarum을 거스른다.
            [PaleConclaveVigilId] = new WarrantSpec(PaleConclaveVigilId, WarrantKind.Intact, IssuerFactionId: PaleConclaveId, OpposedFactionId: SolarumId),
            // 정밀/효율 → 속전. 격자 무기화(Solarum)는 최악의 모독이라 거스른다.
            [LatticePrecisionId] = new WarrantSpec(LatticePrecisionId, WarrantKind.Swift, SwiftStepThresholdDefault, IssuerFactionId: LatticeId, OpposedFactionId: SolarumId),
        };

    /// <summary>
    /// 정치 warrant id(issuer 있음) 목록 — #5 OfferSet의 placeholder offer 소스(ADR-0028).
    /// 실제 per-site offer(어느 site가 무엇을 제안하나)는 P2b content가 소유 — 그때 이 derive를 교체.
    /// </summary>
    public static IReadOnlyList<string> PoliticalWarrantIds { get; } = Specs
        .Where(pair => !string.IsNullOrWhiteSpace(pair.Value.IssuerFactionId))
        .Select(pair => pair.Key)
        .OrderBy(key => key, StringComparer.Ordinal)
        .ToList();

    /// <summary>
    /// 빈 문자열/미등록 id는 false(서약 없음 → 판정 NotApplicable).
    /// </summary>
    public static bool TryResolve(string? warrantId, out WarrantSpec spec)
    {
        if (!string.IsNullOrWhiteSpace(warrantId) && Specs.TryGetValue(warrantId, out var found))
        {
            spec = found;
            return true;
        }

        spec = null!;
        return false;
    }
}
