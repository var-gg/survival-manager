using System;
using System.Collections.Generic;
using System.Linq;
using SM.Core.Content;
using SM.Meta.Model;
using SM.Unity.UI;
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
    // headless conformance(Phase 2 Stage 2): GameSessionRoot(MonoBehaviour) 대신 순수 GameSessionState +
    // ICombatContentLookup을, ContentTextResolver(→GameLocalizationController MonoBehaviour) 대신 이름 resolver
    // delegate를 받아 씬/엔진 없이 BuildState를 구동한다. 화면 전환(빠른 전투)도 주입 Action으로 분리.
    private readonly GameSessionState _session;
    private readonly ICombatContentLookup _lookup;
    private readonly IRosterGridView _view;
    private readonly Func<string, string> _className;
    private readonly Func<string, string> _raceName;
    private readonly Func<string, string, string> _characterName;
    private readonly Func<string, string>? _archetypeName;
    private readonly Action? _quickBattle;
    private readonly Action<string>? _heroSelected;
    private string _selectedFilterKey = "all";

    public RosterGridPresenter(
        GameSessionState session,
        ICombatContentLookup lookup,
        IRosterGridView view,
        Func<string, string> className,
        Func<string, string> raceName,
        Func<string, string, string> characterName,
        Action? quickBattle = null,
        Action<string>? heroSelected = null,
        Func<string, string>? archetypeName = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _lookup = lookup ?? throw new ArgumentNullException(nameof(lookup));
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _className = className ?? throw new ArgumentNullException(nameof(className));
        _raceName = raceName ?? throw new ArgumentNullException(nameof(raceName));
        _characterName = characterName ?? throw new ArgumentNullException(nameof(characterName));
        _archetypeName = archetypeName;
        _quickBattle = quickBattle;
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
        // 빠른 전투 진입(transient smoke + 씬 전환)은 GameSessionRoot/SceneFlow 작업이라 주입 Action으로 분리.
        // 가드(가능 여부)는 순수 세션 상태로 유지해 headless에서도 결정이 검증된다.
        if (_session.CanStartQuickBattleSmoke)
        {
            _quickBattle?.Invoke();
        }
    }

    public RosterGridViewState BuildState()
    {
        // Sprint 2: Profile.Heroes + archetype matrix lookup으로 race/class 정확화.
        var session = _session;

        // 1) 전 동료를 (카드 ViewState, raceId, classId)로 전개 — 필터·칩 빌드 공용.
        var rows = session.Profile.Heroes
            .Select(h =>
            {
                // HeroId는 "hero-{Guid}" instance 식별자 — archetype lookup은 ArchetypeId로.
                _lookup.TryGetArchetype(h.ArchetypeId, out var archetype);
                var raceId = archetype?.Race.Id ?? h.RaceId ?? "human";
                var classId = archetype?.Class.Id ?? h.ClassId ?? "vanguard";
                var className = _className(classId);
                var raceName = _raceName(raceId);
                var displayName = HeroDisplayLabelFormatter.ResolvePersonAndJob(h, _characterName, _archetypeName);

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
        //    각 칩에 해당 인원수도 실어 12+ 명부에서 탐색을 돕는다.
        var filters = new List<RosterGridFilterChipViewState>
        {
            new("all", "전체", "all", _selectedFilterKey == "all", rows.Count),
        };
        foreach (var (key, label) in DistinctOrdered(rows.Select(r => (r.raceId, r.raceName))))
        {
            var count = rows.Count(r => r.raceId == key);
            filters.Add(new(key, label, "race", _selectedFilterKey == key, count));
        }
        foreach (var (key, label) in DistinctOrdered(rows.Select(r => (r.classId, r.className))))
        {
            var count = rows.Count(r => r.classId == key);
            filters.Add(new(key, label, "class", _selectedFilterKey == key, count));
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
