using System;
using System.Collections.Generic;
using System.Linq;
using SM.Meta.Model;
using UnityEngine;

namespace SM.Unity.UI.Town.Preview;

/// <summary>
/// RosterGrid V1 Presenter — Profile.Heroes (HeroInstanceRecord[]) → ViewState.
/// pindoc V1 SoT (ux-surface-catalog#Town.RosterGrid): Town default 진입 panel.
///
/// Sprint 1 scaffold. hero list + count는 wire 가능. 추천 자세 / level·xp 등은 archetype 매트릭스 lookup으로
/// 정확화 (Sprint 2). hero 선택 → CharacterSheet navigation은 별도 hub presenter 통합 시 wire.
///
/// 워크플로우: 사용자가 hero card 클릭 → 다른 surface (CharacterSheet / Loadout)로 navigate → 편집 → 다시 RosterGrid로 복귀.
/// </summary>
public sealed class RosterGridPresenter : IRosterGridActions
{
    private readonly GameSessionRoot _root;
    private readonly RosterGridView _view;
    private readonly ContentTextResolver _contentText;
    private readonly Action<string>? _heroSelected;
    private string _selectedFilterKey = "all";

    public RosterGridPresenter(
        GameSessionRoot root,
        RosterGridView view,
        ContentTextResolver contentText,
        Action<string>? heroSelected = null)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _contentText = contentText ?? throw new ArgumentNullException(nameof(contentText));
        _heroSelected = heroSelected;
    }

    public void Initialize()
    {
        _view.Bind(this);
        Refresh();
    }

    public void Refresh()
    {
        _view.Render(BuildState());
    }

    void IRosterGridActions.OnHeroSelected(string heroId)
    {
        if (!string.IsNullOrWhiteSpace(heroId))
        {
            _heroSelected?.Invoke(heroId);
        }
    }

    void IRosterGridActions.OnFilterSelected(string filterKey)
    {
        _selectedFilterKey = filterKey;
        Refresh();
    }

    void IRosterGridActions.OnQuickBattleClicked()
    {
        if (_root.SessionState.CanStartQuickBattleSmoke)
        {
            _root.BeginTransientTownSmoke();
            _root.SessionState.PrepareTownQuickBattleSmoke();
            _root.SceneFlow.GoToBattle();
        }
    }

    private RosterGridViewState BuildState()
    {
        // Sprint 2: Profile.Heroes + archetype matrix lookup으로 race/class 정확화.
        var session = _root.SessionState;

        // 1) 전 동료를 (카드 ViewState, raceId, classId)로 전개 — 필터·칩 빌드 공용.
        var rows = session.Profile.Heroes
            .Select(h =>
            {
                // HeroId는 "hero-{Guid}" instance 식별자 — archetype lookup은 ArchetypeId로.
                _root.CombatContentLookup.TryGetArchetype(h.ArchetypeId, out var archetype);
                var raceId = archetype?.Race.Id ?? h.RaceId ?? "human";
                var classId = archetype?.Class.Id ?? h.ClassId ?? "vanguard";
                var className = _contentText.GetClassName(classId);
                var raceName = _contentText.GetRaceName(raceId);
                var displayName = _contentText.GetCharacterName(h.CharacterId, h.ArchetypeId);

                var progression = session.Profile.HeroProgressions
                    .FirstOrDefault(p => string.Equals(p.HeroId, h.HeroId, StringComparison.Ordinal));
                var level = progression?.Level ?? 1;
                var xpPct = progression != null
                    ? Mathf.Clamp(progression.Experience % 100, 0, 100)
                    : 0;

                var card = new RosterGridHeroCardViewState(
                    HeroId: h.HeroId,
                    DisplayName: displayName,
                    ArchetypeLabel: $"{className} / {raceName}",
                    FamilyKey: classId,
                    RarityKey: h.RecruitTier.ToString().ToLowerInvariant(),
                    EquipSlots: h.EquippedItemIds?.Count(s => !string.IsNullOrEmpty(s)) ?? 0,
                    Level: level,
                    XpPct: xpPct,
                    // 초상화 resolve 키 — instance CharacterId 우선, 비면 archetype id로 fallback.
                    CharacterId: string.IsNullOrWhiteSpace(h.CharacterId) ? h.ArchetypeId : h.CharacterId);
                return (card, raceId, raceName, classId, className);
            })
            .ToList();

        // 2) 선택된 필터 적용 — "all"이거나, race/class 키 일치.
        var filteredCards = (_selectedFilterKey == "all"
                ? rows
                : rows.Where(r => r.raceId == _selectedFilterKey || r.classId == _selectedFilterKey))
            .Select(r => r.card)
            .ToList();

        // 3) 필터 칩 — 로스터에 실재하는 race/class만 (dead 필터 없음). 라벨은 ContentTextResolver 산출.
        var filters = new List<RosterGridFilterChipViewState>
        {
            new("all", "전체", "all", _selectedFilterKey == "all"),
        };
        foreach (var (key, label) in DistinctOrdered(rows.Select(r => (r.raceId, r.raceName))))
        {
            filters.Add(new(key, label, "race", _selectedFilterKey == key));
        }
        foreach (var (key, label) in DistinctOrdered(rows.Select(r => (r.classId, r.className))))
        {
            filters.Add(new(key, label, "class", _selectedFilterKey == key));
        }

        return new RosterGridViewState(
            Heroes: filteredCards,
            RosterCap: MetaBalanceDefaults.TownRosterCap,
            SelectedFilterKey: _selectedFilterKey,
            Filters: filters);
    }

    // 등장 순서 보존 + 키 중복 제거 (Linq Distinct는 tuple 순서 비보장 우려 → 명시적).
    private static IEnumerable<(string Key, string Label)> DistinctOrdered(IEnumerable<(string Key, string Label)> source)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (key, label) in source)
        {
            if (!string.IsNullOrWhiteSpace(key) && seen.Add(key))
            {
                yield return (key, label);
            }
        }
    }
}
