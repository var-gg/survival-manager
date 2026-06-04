using System.Collections.Generic;

namespace SM.Unity.UI.Atlas;

/// <summary>
/// 출격 서약 선택 화면의 한 서약 카드 표시 상태. ID/label 분리 — WarrantId만 stable id이고
/// 표시 문구(TitleText/IssuerText/...)는 Presenter가 label resolver로 채운 immutable snapshot이다.
/// PreviewText는 "현재 standing 기준 이 서약을 걸면 다음 전투에 무엇이 붙나"의 짧은 요약(ADR-0028).
/// </summary>
public sealed record WarrantSelectionCardViewState(
    string WarrantId,
    string TitleText,
    string IssuerText,
    string OpposedText,
    string KindText,
    string PreviewText,
    string ActionLabel,
    bool IsSelected);

/// <summary>
/// 출격 서약 선택 화면 전체 표시 상태. RewardScreenViewState와 같은 패턴 — 모든 display string +
/// 카드 리스트를 담은 immutable record. View는 이 snapshot만 보고 렌더하고 battle/save truth를 만들지 않는다.
/// </summary>
public sealed record WarrantSelectionViewState(
    string Title,
    string HeaderText,
    IReadOnlyList<WarrantSelectionCardViewState> Cards,
    string ProceedLabel,
    bool ProceedIsSelected,
    string StatusText);
