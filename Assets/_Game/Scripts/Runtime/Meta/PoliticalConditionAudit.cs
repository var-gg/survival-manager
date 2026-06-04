using System;
using System.Collections.Generic;

namespace SM.Meta;

/// <summary>
/// 정치 조건을 match audit/record에 남기는 stable 인코딩(ID/label 분리 — 표시 문구 아님, stable id/code만).
/// compile/snapshot hash는 정치 *효과*를 포착하나 "어느 세력 mandate·통로·사유에서 왔나"는 안 남는다 —
/// 이 audit token이 그 provenance를 영속한다(GPT Pro: replay/audit에 정치 condition provenance가 남아야 한다).
/// 향후 replay 재검증(변동된 standing에서 재도출 시 drift 비교)의 입력이 된다. 순수 함수, SM.Combat 무관.
/// </summary>
public static class PoliticalConditionAudit
{
    /// <summary>한 조건 → "factionId|channel|reasonCode" stable token(persistence·audit용).</summary>
    public static string Encode(PoliticalCombatCondition condition) =>
        $"{condition.SourceFactionId}|{condition.Channel}|{condition.ReasonCode}";

    /// <summary>조건 목록 → audit token 목록(빈 입력은 빈 결과).</summary>
    public static IReadOnlyList<string> EncodeAll(IReadOnlyList<PoliticalCombatCondition> conditions)
    {
        if (conditions == null || conditions.Count == 0)
        {
            return Array.Empty<string>();
        }

        var encoded = new List<string>(conditions.Count);
        foreach (var condition in conditions)
        {
            encoded.Add(Encode(condition));
        }

        return encoded;
    }
}
