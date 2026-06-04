using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SM.Meta;

namespace SM.Unity.UI.Atlas;

/// <summary>
/// 출격 서약 선택(P2b) Presenter — RewardScreenPresenter 패턴(Initialize/Bind/Render + BuildState)을 따른다.
/// 선택된 서약 id만 추적하고(빈 문자열 = 서약 없이 출격), 실제 PledgeWarrant 호출·flow 통합은 별도 계층이 한다.
///
/// 테스트성: 이름 해석은 concrete ContentTextResolver 대신 Func 델리게이트(factionName/warrantName)로 주입받는다.
/// → FastUnit 테스트가 무거운 GameLocalizationController/ICombatContentLookup 없이 단순 fake로 검증 가능.
/// 통합 계층은 resolver.GetFactionName / resolver.GetWarrantName 를 그대로 넘긴다.
/// standing 읽기도 Func&lt;string,int&gt;로 주입 — persistence 의존을 Presenter 밖에 둔다(SM.Meta 경계 패턴 재사용).
/// </summary>
public sealed class WarrantSelectionPresenter
{
    private readonly Func<string, int> _standingLookup;
    private readonly Func<string, string> _factionName;
    private readonly Func<string, string> _warrantName;
    private readonly IReadOnlyList<string> _offeredWarrantIds;
    private readonly string _siteCauseText;

    private string _selectedWarrantId = string.Empty;

    public WarrantSelectionPresenter(
        Func<string, int> standingLookup,
        Func<string, string> factionName,
        Func<string, string> warrantName,
        IReadOnlyList<string>? offeredWarrantIds = null,
        string? siteCauseText = null)
    {
        _standingLookup = standingLookup ?? throw new ArgumentNullException(nameof(standingLookup));
        _factionName = factionName ?? throw new ArgumentNullException(nameof(factionName));
        _warrantName = warrantName ?? throw new ArgumentNullException(nameof(warrantName));
        _offeredWarrantIds = offeredWarrantIds ?? WarrantCatalog.PoliticalWarrantIds;
        // ADR-0028 #b: site 정치 압력 산문(왜 이 장소에서 이 위임이 나왔나)을 헤더에 노출 — warrant가 "계약 카드"가
        // 아니라 "장소에서 발생한 정치 사건"으로 읽히게. 빈 값이면 기본 안내(미등록 site fallback).
        _siteCauseText = siteCauseText ?? string.Empty;
    }

    /// <summary>선택된 서약 id. 빈 문자열이면 "서약 없이 출격".</summary>
    public string SelectedWarrantId => _selectedWarrantId;

    /// <summary>View가 (재)렌더를 트리거할 수 있게 선택 변경을 알린다.</summary>
    public event Action? StateChanged;

    public void Select(string warrantId)
    {
        var next = warrantId ?? string.Empty;
        if (string.Equals(_selectedWarrantId, next, StringComparison.Ordinal))
        {
            return;
        }

        _selectedWarrantId = next;
        StateChanged?.Invoke();
    }

    public void SelectNone()
    {
        if (_selectedWarrantId.Length == 0)
        {
            return;
        }

        _selectedWarrantId = string.Empty;
        StateChanged?.Invoke();
    }

    public WarrantSelectionViewState BuildState()
    {
        var options = WarrantOptionBuilder.Build(_offeredWarrantIds, _standingLookup);
        var cards = options.Select(BuildCard).ToArray();
        return new WarrantSelectionViewState(
            Title: "출격 서약 선택",
            HeaderText: _siteCauseText.Length > 0
                ? $"{_siteCauseText}\n출격 전 서약을 하나 고르거나 서약 없이 출격합니다."
                : "출격 전 서약을 하나 고르거나 서약 없이 출격합니다.",
            Cards: cards,
            ProceedLabel: "출격(서약 없이)",
            ProceedIsSelected: _selectedWarrantId.Length == 0,
            StatusText: BuildStatusText());
    }

    private WarrantSelectionCardViewState BuildCard(WarrantOption option)
    {
        return new WarrantSelectionCardViewState(
            WarrantId: option.WarrantId,
            TitleText: ResolveWarrantName(option.WarrantId),
            IssuerText: $"발행: {ResolveFactionName(option.IssuerFactionId)}",
            OpposedText: $"거스름: {ResolveFactionName(option.OpposedFactionId)}",
            KindText: $"기준: {WarrantDisplayDefaults.KindName(option.Kind)}",
            PreviewText: BuildPreviewText(option),
            ActionLabel: "서약",
            IsSelected: string.Equals(_selectedWarrantId, option.WarrantId, StringComparison.Ordinal));
    }

    private string BuildPreviewText(WarrantOption option)
    {
        if (option.PreviewConditions == null || option.PreviewConditions.Count == 0)
        {
            return "현재 신뢰로는 추가 조건 없음.";
        }

        var lines = new List<string>(option.PreviewConditions.Count);
        foreach (var condition in option.PreviewConditions)
        {
            var sourceName = ResolveFactionName(condition.SourceFactionId);
            var line = condition.Channel switch
            {
                PoliticalChannel.AllySupport => $"후원: {sourceName} 지원",
                PoliticalChannel.EnemyAlertness => $"경계: {sourceName} 적 경계",
                _ => sourceName,
            };

            var delta = SummarizePackage(condition);
            lines.Add(string.IsNullOrEmpty(delta) ? line : $"{line} ({delta})");
        }

        return string.Join("\n", lines);
    }

    private static string SummarizePackage(PoliticalCombatCondition condition)
    {
        var modifiers = condition.Package?.Modifiers;
        if (modifiers == null || modifiers.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var modifier in modifiers)
        {
            if (builder.Length > 0)
            {
                builder.Append(", ");
            }

            var sign = modifier.Value >= 0f ? "+" : string.Empty;
            builder.Append($"{modifier.Stat.Value} {sign}{modifier.Value:0.#}");
        }

        return builder.ToString();
    }

    private string BuildStatusText()
    {
        if (_selectedWarrantId.Length == 0)
        {
            return "서약 없이 출격합니다.";
        }

        return $"서약 선택: {ResolveWarrantName(_selectedWarrantId)}";
    }

    private string ResolveFactionName(string factionId)
    {
        if (string.IsNullOrWhiteSpace(factionId))
        {
            return "-";
        }

        var resolved = _factionName(factionId);
        return string.IsNullOrWhiteSpace(resolved) ? factionId : resolved;
    }

    private string ResolveWarrantName(string warrantId)
    {
        if (string.IsNullOrWhiteSpace(warrantId))
        {
            return "-";
        }

        var resolved = _warrantName(warrantId);
        return string.IsNullOrWhiteSpace(resolved) ? warrantId : resolved;
    }
}
