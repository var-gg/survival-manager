using System;
using System.Collections.Generic;

namespace SM.Meta;

/// <summary>
/// 출격 전 서약 선택(P2b)이 화면에 띄울 한 서약 옵션의 데이터 — id + 미리보기. ID/label 분리:
/// 여기엔 표시명이 없다(stable id만). 표시명(솔라룸/온전 등)은 presentation의 label layer가 붙인다.
/// <see cref="PreviewConditions"/>는 "이 서약을 걸면 *현재 standing 기준* 다음 전투에 무엇이 붙나"의 미리보기
/// (GPT Pro #1·#2 — 정치 조건을 선택 시점에 보여준다).
/// </summary>
/// <param name="Kind">세력 기준의 fact-predicate 종류(Swift=속전/Intact=온전).</param>
public sealed record WarrantOption(
    string WarrantId,
    string IssuerFactionId,
    string OpposedFactionId,
    WarrantKind Kind,
    IReadOnlyList<PoliticalCombatCondition> PreviewConditions);

/// <summary>
/// 제안된 서약 id 집합 + 현재 standing → 선택 화면 옵션 목록. P2b 선택 surface의 *로직 코어*(순수, headless).
/// View/Presenter(SM.Unity)는 이 옵션에 label(표시명)과 UXML 렌더만 얹는다 — UI/에디터 의존 없이 검증 가능.
/// 비정치 서약(issuer 없음)은 제외(정치 선택만 제안). 미리보기는 <see cref="PoliticalCombatConditionService.Resolve"/> 재사용.
/// </summary>
public static class WarrantOptionBuilder
{
    public static IReadOnlyList<WarrantOption> Build(
        IEnumerable<string> offeredWarrantIds,
        Func<string, int> standingLookup)
    {
        var options = new List<WarrantOption>();
        if (offeredWarrantIds is null)
        {
            return options;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var warrantId in offeredWarrantIds)
        {
            if (!seen.Add(warrantId)
                || !WarrantCatalog.TryResolve(warrantId, out var spec)
                || string.IsNullOrWhiteSpace(spec.IssuerFactionId))
            {
                continue;
            }

            var preview = standingLookup is null
                ? Array.Empty<PoliticalCombatCondition>()
                : PoliticalCombatConditionService.Resolve(warrantId, standingLookup);

            options.Add(new WarrantOption(
                spec.Id,
                spec.IssuerFactionId,
                spec.OpposedFactionId,
                spec.Kind,
                preview));
        }

        return options;
    }
}
