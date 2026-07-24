using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Core.Stats;

namespace SM.Meta;

/// <summary>다음 전투에 정치 상태가 거는 조건의 통로. 한 channel = 한 종류의 정치적 결과.</summary>
public enum PoliticalChannel
{
    /// <summary>발행 세력이 squad에 보내는 지원(아군 버프).</summary>
    AllySupport,

    /// <summary>거스른 세력이 적을 경계시킴(적 버프).</summary>
    EnemyAlertness,
}

/// <summary>
/// 다음 전투 한 건에 적용될 정치 조건 하나. ADR-0028 slice 3 — GPT Pro "체크박스 충족" 검수의
/// #4(타입 경계) 대응: 정치 결과를 bare stat package로만 흘리지 않고, 출처·통로를 함께 운반한다.
/// <see cref="Package"/>는 leaf(전투가 보는 일반 modifier), 상위 메타(출처/통로/사유)는 여기 남는다.
/// </summary>
/// <param name="SourceFactionId">이 조건을 만든 정치 세력(지원=issuer, 경계=opposed).</param>
/// <param name="ReasonCode">사유 stable token(표시 문구 아님 — label은 별도 layer, ID/label 분리). 예: "support.trust_threshold".</param>
public sealed record PoliticalCombatCondition(
    string SourceFactionId,
    PoliticalChannel Channel,
    string ReasonCode,
    CombatModifierPackage Package);

/// <summary>
/// ADR-0028 정치 warrant 루프의 뒷단 — FactionState(trust/standing) → 다음 전투 조건.
/// slice 2(아군 지원)를 slice 3에서 양방향으로 확장: 발행 세력 신뢰가 높으면 지원(아군 버프),
/// 거스른 세력 적대가 누적되면 경계(적 버프). 둘 다 "전투→정치→전투"를 닫되, 경계 쪽이 정치 충돌을
/// 다음 전장에 실제로 들여보낸다(GPT Pro #3·#5 — opposed 축이 ledger에서 죽지 않게).
///
/// 순수 함수다. <see cref="SM.Persistence.Abstractions.Models.FactionStandingRecord"/>(persistence) 대신
/// primitive(faction id + standing int)만 받아 SM.Meta 경계를 지킨다(FactionTrustService와 동일 패턴).
/// 산출물 package는 일반 <see cref="CombatModifierPackage"/> — 전투 시뮬은 이게 정치에서 왔는지 모른다.
///
/// determinism: 조건은 출격 시점 standing에서 도출되고, 아군 package는 compile hash에, 적 package는
/// EnemySnapshotHash에 포착된다(live 결정적). replay 재검증이 *변동된* Profile에서 재도출하는 경로는
/// 현재 없다 — 생기면 resolved 조건을 per-sortie로 BattleContextState에 snapshot해야 한다(ADR-0028 후속).
/// </summary>
public static class PoliticalCombatConditionService
{
    /// <summary>지원 개시 신뢰 임계. slice 1: 서약 이행당 issuer +2 → 4 = 서약 2회 이행.</summary>
    public const int SupportTrustThreshold = 4;

    /// <summary>경계 발동 적대 임계(이하). slice 1: 이행당 opposed −1 → −2 = 서약 2회 이행으로 거스름 누적.</summary>
    public const int AlertStandingThreshold = -2;

    /// <summary>지원 시 squad 전원 가산(Flat). placeholder — 밸런스는 후속.</summary>
    public const float SupportMaxHealthBonus = 4f;
    public const float SupportPhysPowerBonus = 2f;

    /// <summary>경계 시 적 전원 가산(Flat). 지원보다 약하게(적이 "준비됨" 정도). placeholder.</summary>
    public const float AlertMaxHealthBonus = 2f;
    public const float AlertPhysPowerBonus = 1f;

    public const string SupportReasonCode = "support.trust_threshold";
    public const string AlertReasonCode = "alert.opposed_hostility";

    /// <summary>
    /// 서약 id → 발행/거스른 세력 → (standingLookup으로 읽은) standing → 적용될 정치 조건 0..2개.
    /// 분기 전부(서약 해석·임계 게이트·package 구성)를 여기 모아 단위 테스트로 잡는다.
    /// </summary>
    /// <param name="standingLookup">faction id → 현재 standing(trust/적대 누적). persistence 읽기는 호출자(SM.Unity)가 주입.</param>
    public static IReadOnlyList<PoliticalCombatCondition> Resolve(
        string pledgedWarrantId,
        Func<string, int> standingLookup)
    {
        var conditions = new List<PoliticalCombatCondition>();
        if (standingLookup is null || !WarrantCatalog.TryResolve(pledgedWarrantId, out var spec))
        {
            return conditions;
        }

        // 발행 세력 신뢰가 임계 이상이면 지원(아군 버프).
        if (!string.IsNullOrWhiteSpace(spec.IssuerFactionId)
            && standingLookup(spec.IssuerFactionId) >= SupportTrustThreshold)
        {
            conditions.Add(BuildSupport(spec.IssuerFactionId));
        }

        // 거스른 세력 적대가 임계 이하면 경계(적 버프) — 정치 충돌이 다음 전장에 닿는 지점.
        if (!string.IsNullOrWhiteSpace(spec.OpposedFactionId)
            && standingLookup(spec.OpposedFactionId) <= AlertStandingThreshold)
        {
            conditions.Add(BuildAlert(spec.OpposedFactionId));
        }

        return conditions;
    }

    /// <summary>AllySupport 통로의 package만 — loadout compile seam에서 ally numeric package로 접힌다.</summary>
    public static IReadOnlyList<CombatModifierPackage> AllyPackages(IReadOnlyList<PoliticalCombatCondition> conditions) =>
        Channel(conditions, PoliticalChannel.AllySupport);

    /// <summary>EnemyAlertness 통로의 package만 — encounter resolve seam에서 적 package로 접힌다.</summary>
    public static IReadOnlyList<CombatModifierPackage> EnemyPackages(IReadOnlyList<PoliticalCombatCondition> conditions) =>
        Channel(conditions, PoliticalChannel.EnemyAlertness);

    /// <summary>
    /// package들을 각 적 loadout의 numeric package에 접는다(squad-wide 적용). 적은 ally CompileHash 같은
    /// 해시가 없어 EnemySnapshotHash가 이를 포착한다. 순수 — 새 loadout 리스트를 반환(입력 불변).
    /// </summary>
    public static IReadOnlyList<BattleUnitLoadout> ApplyEnemyPackages(
        IReadOnlyList<BattleUnitLoadout> enemies,
        IReadOnlyList<CombatModifierPackage> packages)
    {
        if (packages is not { Count: > 0 })
        {
            return enemies;
        }

        return enemies
            .Select(enemy => enemy with { Packages = enemy.NumericPackages.Concat(packages).ToList() })
            .ToList();
    }

    /// <summary>
    /// 비수치 enemy rule package를 각 적 loadout에 접는다. secondary pressure처럼 StatModifier로
    /// 표현할 수 없는 전투 규칙이 쓰는 pure compile seam이다.
    /// </summary>
    public static IReadOnlyList<BattleUnitLoadout> ApplyEnemyRulePackages(
        IReadOnlyList<BattleUnitLoadout> enemies,
        IReadOnlyList<CombatRuleModifierPackage> packages)
    {
        if (packages is not { Count: > 0 })
        {
            return enemies;
        }

        return enemies
            .Select(enemy => enemy with
            {
                RulePackages = (enemy.RulePackages ?? Array.Empty<CombatRuleModifierPackage>())
                    .Concat(packages)
                    .ToList(),
            })
            .ToList();
    }

    private static IReadOnlyList<CombatModifierPackage> Channel(
        IReadOnlyList<PoliticalCombatCondition> conditions,
        PoliticalChannel channel) =>
        conditions.Where(condition => condition.Channel == channel).Select(condition => condition.Package).ToList();

    private static PoliticalCombatCondition BuildSupport(string issuerFactionId)
    {
        var sourceId = $"faction_support:{issuerFactionId}";
        return new PoliticalCombatCondition(
            issuerFactionId,
            PoliticalChannel.AllySupport,
            SupportReasonCode,
            new CombatModifierPackage(sourceId, ModifierSource.Other, new[]
            {
                new StatModifier(StatKey.MaxHealth, ModifierOp.Flat, SupportMaxHealthBonus, ModifierSource.Other, sourceId),
                new StatModifier(StatKey.PhysPower, ModifierOp.Flat, SupportPhysPowerBonus, ModifierSource.Other, sourceId),
            }));
    }

    private static PoliticalCombatCondition BuildAlert(string opposedFactionId)
    {
        var sourceId = $"faction_alert:{opposedFactionId}";
        return new PoliticalCombatCondition(
            opposedFactionId,
            PoliticalChannel.EnemyAlertness,
            AlertReasonCode,
            new CombatModifierPackage(sourceId, ModifierSource.Other, new[]
            {
                new StatModifier(StatKey.MaxHealth, ModifierOp.Flat, AlertMaxHealthBonus, ModifierSource.Other, sourceId),
                new StatModifier(StatKey.PhysPower, ModifierOp.Flat, AlertPhysPowerBonus, ModifierSource.Other, sourceId),
            }));
    }
}
