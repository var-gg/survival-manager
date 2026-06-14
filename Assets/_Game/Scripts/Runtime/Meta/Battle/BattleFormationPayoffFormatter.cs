using System.Collections.Generic;

namespace SM.Meta;

/// <summary>
/// <see cref="BattleFormationPayoffSummary"/> → 보상 화면 행 값 문자열. 어휘는 전투 피드
/// (<see cref="SM.Unity.BattleHighlightLedger"/>)와 1:1로 맞춘다 — 플레이어가 전투 중 본 표기를
/// 빌드 결정 화면에서 다시 읽게. 0 카운트는 억제한다(양수만 노출 — 침묵 토큰이 0을 거꾸로 읽히게
/// 두지 않는다). 순수 함수 — 세션/로컬라이저/Unity 무참조라 FastUnit 으로 단독 검증한다.
/// </summary>
public static class BattleFormationPayoffFormatter
{
    /// <summary>MVP 행 값 — "이름 · 피해 N · 회복 N · 처치 N". MVP 가 없으면 빈 문자열.</summary>
    public static string BuildMvpValue(BattleFormationPayoffSummary payoff)
    {
        if (payoff == null || !payoff.HasData || string.IsNullOrEmpty(payoff.MvpUnitName))
        {
            return "";
        }

        return $"{payoff.MvpUnitName} · 피해 {payoff.MvpDamage:0} · 회복 {payoff.MvpHealing:0} · 처치 {payoff.MvpKills}";
    }

    /// <summary>진형 전과 행 값 — 측면/후방/차단/구출/후열격파 중 양수만 조립. 전무하면 빈 문자열.</summary>
    public static string BuildHighlightValue(BattleFormationPayoffSummary payoff)
    {
        if (payoff == null || !payoff.HasData)
        {
            return "";
        }

        var parts = new List<string>(5);
        if (payoff.RearStrikes > 0) parts.Add($"후방 강타 {payoff.RearStrikes}");
        if (payoff.FlankStrikes > 0) parts.Add($"측면 협격 {payoff.FlankStrikes}");
        if (payoff.ScreenAbsorbs > 0) parts.Add($"전열 차단 {payoff.ScreenAbsorbs}");
        if (payoff.SaveMoments > 0) parts.Add($"구출 {payoff.SaveMoments}");
        if (payoff.BacklineDiveKills > 0) parts.Add($"후열 격파 {payoff.BacklineDiveKills}");
        return parts.Count == 0 ? "" : string.Join(" · ", parts);
    }

    /// <summary>발현 행 값 — 시너지/콤보/증강 발동 중 양수만 조립. 전무하면 빈 문자열.</summary>
    public static string BuildManifestValue(BattleFormationPayoffSummary payoff)
    {
        if (payoff == null || !payoff.HasData)
        {
            return "";
        }

        var parts = new List<string>(3);
        if (payoff.SynergyActivations > 0) parts.Add($"시너지 발동 {payoff.SynergyActivations}");
        if (payoff.ComboConsumes > 0) parts.Add($"콤보 작렬 {payoff.ComboConsumes}");
        if (payoff.TriggeredEffects > 0) parts.Add($"증강 발동 {payoff.TriggeredEffects}");
        return parts.Count == 0 ? "" : string.Join(" · ", parts);
    }
}
