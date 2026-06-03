using System;
using System.Collections.Generic;
using SM.Combat.Model;
using SM.Core.Stats;

namespace SM.Meta;

/// <summary>
/// ADR-0028 정치 warrant 루프의 뒷단(slice 2) — FactionState(trust) → 다음 전투 조건 변화.
/// 슬라이스 1이 "전투 결과 → 세력 신뢰"를 깔았다면, 이 서비스는 그 신뢰를 *다음 출격*으로 되먹인다:
/// 서약을 발행한 세력과의 신뢰가 임계 이상이면 그 세력이 squad에 물자 지원을 보낸다(소폭 stat 버프).
/// 이걸로 전투→정치→전투 1 cycle이 닫힌다(GPT Pro "다음 전투 조건 변화" 요건).
///
/// 순수 함수다. <see cref="SM.Persistence.Abstractions.Models.FactionStandingRecord"/>(persistence)를
/// 직접 받지 않고 primitive(issuer id + trust int)만 받아 SM.Meta 경계를 지킨다 —
/// <see cref="FactionTrustService"/>와 동일 패턴. trust 읽기/쓰기는 SM.Unity가 소유한다.
///
/// combat 순수성: 산출물은 일반 <see cref="CombatModifierPackage"/> 하나다. SM.Combat은 이게
/// 정치에서 왔는지 모른다 — loadout compile이 다른 numeric package와 똑같이 접어 넣는다.
/// </summary>
public static class NextCombatSupportService
{
    /// <summary>지원 개시 신뢰 임계. slice 1이 서약 이행당 issuer +2를 주므로 4 = 서약 2회 이행.</summary>
    public const int SupportTrustThreshold = 4;

    /// <summary>지원 시 squad 전원 max_health 가산(Flat). base HP ~20 기준 소폭(≈+20%). slice 2 placeholder — 밸런스는 후속.</summary>
    public const float SupportMaxHealthBonus = 4f;

    /// <summary>지원 시 squad 전원 phys_power 가산(Flat). 2-set synergy tier와 동급. slice 2 placeholder.</summary>
    public const float SupportPhysPowerBonus = 2f;

    /// <summary>
    /// issuer가 없거나(서약 미발행/build축) 신뢰가 임계 미만이면 null(지원 없음).
    /// </summary>
    public static StartingSupportGrant? Resolve(string issuerFactionId, int issuerTrust)
    {
        if (string.IsNullOrWhiteSpace(issuerFactionId))
        {
            return null;
        }

        if (issuerTrust < SupportTrustThreshold)
        {
            return null;
        }

        return new StartingSupportGrant(issuerFactionId, issuerTrust);
    }

    /// <summary>
    /// 지원 grant를 combat이 소비하는 numeric package로 변환. SourceId는 자기서술적(faction_support:{id})이라
    /// compile hash / provenance에서 출처가 드러난다.
    /// </summary>
    public static CombatModifierPackage BuildPackage(StartingSupportGrant grant)
    {
        var sourceId = $"faction_support:{grant.FactionId}";
        return new CombatModifierPackage(
            sourceId,
            ModifierSource.Other,
            new[]
            {
                new StatModifier(StatKey.MaxHealth, ModifierOp.Flat, SupportMaxHealthBonus, ModifierSource.Other, sourceId),
                new StatModifier(StatKey.PhysPower, ModifierOp.Flat, SupportPhysPowerBonus, ModifierSource.Other, sourceId),
            });
    }

    /// <summary>
    /// 서약 id → 발행 세력 → (trustLookup으로 읽은) 신뢰 → 지원 package 0..1개. seam이 한 줄로 부르도록 합성한 진입점.
    /// 분기 전부(서약 해석·임계 게이트·package 구성)를 여기 모아 단위 테스트로 잡는다.
    /// </summary>
    /// <param name="pledgedWarrantId">overlay에 실린 서약 id(없으면 "").</param>
    /// <param name="trustLookup">faction id → 현재 신뢰. persistence 읽기는 호출자(SM.Unity)가 주입.</param>
    public static IReadOnlyList<CombatModifierPackage> ResolveSupportPackages(
        string pledgedWarrantId,
        Func<string, int> trustLookup)
    {
        if (trustLookup is null)
        {
            return Array.Empty<CombatModifierPackage>();
        }

        if (!WarrantCatalog.TryResolve(pledgedWarrantId, out var spec)
            || string.IsNullOrWhiteSpace(spec.IssuerFactionId))
        {
            return Array.Empty<CombatModifierPackage>();
        }

        var grant = Resolve(spec.IssuerFactionId, trustLookup(spec.IssuerFactionId));
        return grant is null
            ? Array.Empty<CombatModifierPackage>()
            : new[] { BuildPackage(grant) };
    }
}

/// <summary>
/// 다음 전투에 적용될 세력 지원 한 건. FactionId/Trust는 provenance용 — 실제 stat 값은
/// <see cref="NextCombatSupportService"/> 상수가 소유한다(서약 종류와 무관한 단일 tier, slice 2).
/// </summary>
public sealed record StartingSupportGrant(string FactionId, int Trust);
