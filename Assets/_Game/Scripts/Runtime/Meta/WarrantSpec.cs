using System;
using System.Collections.Generic;

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
public sealed record WarrantSpec(string Id, WarrantKind Kind, int SwiftStepThreshold = 0);

/// <summary>
/// 서약 id(overlay에 실려 settlement까지 운반되는 string) → <see cref="WarrantSpec"/> 해석.
/// 슬라이스 1 정의 카탈로그. "어떤 site가 어떤 서약을 거나"(site→warrant 매핑)는 여기 두지 않는다 —
/// 그건 lore이고, P2b의 선택 surface / authoring이 소유한다(코드 id에 lore 박지 않음, ADR-0027).
/// </summary>
public static class WarrantCatalog
{
    public const string SwiftId = "warrant_swift";
    public const string IntactId = "warrant_intact";

    // SwiftStepThreshold는 placeholder다. 서약이 live로 가는 P2b에서 실제 관측 stepCount에 맞춰
    // 튜닝한다(슬라이스 1은 ships dark — live에서 PledgedWarrantId="" 이라 미사용, test는 자체 임계 주입).
    private const int SwiftStepThresholdDefault = 600;

    private static readonly IReadOnlyDictionary<string, WarrantSpec> Specs =
        new Dictionary<string, WarrantSpec>(StringComparer.Ordinal)
        {
            [SwiftId] = new WarrantSpec(SwiftId, WarrantKind.Swift, SwiftStepThresholdDefault),
            [IntactId] = new WarrantSpec(IntactId, WarrantKind.Intact),
        };

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
