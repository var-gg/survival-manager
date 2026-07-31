using System.Collections.Generic;
using SM.Unity.UI;

namespace SM.Unity.UI.Reward;

public sealed record RewardChoiceCardViewState(
    string Title,
    string Body,
    string KindText,
    string ContextText,
    string ActionLabel,
    string Tooltip,
    bool IsEnabled);

/// <summary>
/// 정산 조회 결과. <b>더 이상 패널이 아니다.</b>
///
/// 2026-07-31 이전에는 이 레코드가 화면 좌상단 "정산" 패널 네 행(거점/단계/조우/기록)과
/// 수정자 칩 세 개를 그렸다. 시안에는 그런 패널이 없고, 커밋 id 는 애초에 플레이어 정보가 아니다.
/// 지금은 <see cref="RewardScreenViewState.ResultHeadline"/> 이 쓸 <b>지점 이름</b>을 얻는
/// 조회 경로로만 남는다 — 계산은 그대로라 기존 FastUnit 계약도 그대로 선다.
/// </summary>
public sealed record RewardSettlementSummaryViewState(
    string TitleText,
    string SiteKeyText,
    string SiteValueText,
    string StageKeyText,
    string StageValueText,
    string EncounterKeyText,
    string EncounterValueText,
    string CommitIdKeyText,
    string CommitIdValueText,
    string RewardBiasChipText,
    string ThreatPressureChipText,
    string AffinityBoostChipText,
    bool HasAnyModifier,
    string ThreatBandLabelText = "")
{
    public static readonly RewardSettlementSummaryViewState Empty = new(
        TitleText: "Settlement",
        SiteKeyText: "Site",
        SiteValueText: "-",
        StageKeyText: "Stage",
        StageValueText: "-",
        EncounterKeyText: "Encounter",
        EncounterValueText: "-",
        CommitIdKeyText: "Commit",
        CommitIdValueText: "-",
        RewardBiasChipText: string.Empty,
        ThreatPressureChipText: string.Empty,
        AffinityBoostChipText: string.Empty,
        HasAnyModifier: false,
        ThreatBandLabelText: string.Empty);
}

/// <summary>
/// 결과 줄 옆의 화폐 칩. 시안(<c>ui_ux_bible_reward_v1</c>)의 <c>XP +84 / 골드 +25 / 잔향 +8</c> 자리다.
///
/// 시안의 XP 칩은 넣지 않았다 — 현행 시스템은 전투 단위 경험치 <b>증분</b>을 노출하지 않는다.
/// 없는 값을 만들어 띄우느니 실제 전리품(골드·잔향·아이템 수)만 낸다.
/// </summary>
public sealed record RewardCurrencyChipViewState(
    string Label,
    string ToneKey);

/// <summary>
/// 전과 원장 한 줄.
///
/// 이전 "진행" 패널은 여섯 줄이었고 그중 다섯이 텔레메트리였다(전투 스텝 수, 지갑 총액,
/// 인벤토리 개수, 보상 선택 상태, 복귀 상태). 그 다섯은 결과 줄·화폐 칩·하단 힌트가 가져갔다.
/// 남은 것은 <b>이번 전투가 실제로 만든 것</b>이다 — 진형 페이오프, 영구 해금 예고, 정치 정산.
/// 셋 다 평시엔 행이 없어 조용하다.
/// </summary>
public sealed record RewardProgressionRowViewState(
    string KeyText,
    string ValueText,
    string ToneKey);

// wave-28-survivor GPT Pro patch: squad 4명 survivor row (portrait glyph + HP/MaxHP + Level/Exp).
public sealed record RewardSurvivorRowViewState(
    string HeroId,
    string DisplayName,
    string PortraitGlyph,        // ◆ ⚔ ♟ etc — class별 또는 generic
    string HpText,                // "62 / 80"
    float HpPercent,              // 0.0~1.0
    string ExpText,               // "Lv 4 · 320 / 500"
    string StatusChipText,        // "생존" / "기절" / "EXP +120" (PVE 결과)
    string StatusChipKind);       // "victory" / "downed" / "exp-gain"

public sealed record RewardScreenViewState(
    string Title,
    string LocaleStatus,
    string LocaleKoLabel,
    string LocaleEnLabel,
    string HelpButtonLabel,
    HelpStripViewState Help,
    string ResultHeadline,
    IReadOnlyList<RewardCurrencyChipViewState> CurrencyChips,
    IReadOnlyList<RewardProgressionRowViewState> PayoffRows,
    IReadOnlyList<RewardChoiceCardViewState> Choices,
    string StatusText,
    string ReturnTownLabel,
    string ReturnTownTooltip,
    bool CanReturnToTown,
    bool ReturnTownIsPrimary,
    // wave-28-survivor GPT Pro patch: squad 4명 survivor list (portrait + HP/EXP).
    // default empty이면 View가 survivor section을 hide.
    IReadOnlyList<RewardSurvivorRowViewState>? SurvivorRows = null);
