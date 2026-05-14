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
    private string _selectedFilterKey = "all";

    public RosterGridPresenter(GameSessionRoot root, RosterGridView view, ContentTextResolver contentText)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _contentText = contentText ?? throw new ArgumentNullException(nameof(contentText));
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
        // TODO Sprint 3: Town hub controller에 hero selection 통보 → CharacterSheet navigation.
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
        // TODO Sprint 3: ContentTextResolver로 한국어 표시명 + archetype.RecommendedPosture 정확화.
        var session = _root.SessionState;
        var heroes = session.Profile.Heroes
            .Select(h =>
            {
                _root.CombatContentLookup.TryGetArchetype(h.HeroId, out var archetype);
                var raceId = archetype?.Race.Id ?? h.RaceId ?? "human";
                var classId = archetype?.Class.Id ?? h.ClassId ?? "vanguard";
                var className = _contentText.GetClassName(classId);
                var raceName = _contentText.GetRaceName(raceId);
                var archetypeName = _contentText.GetArchetypeName(h.HeroId);

                // 진행도 — HeroProgressionRecord (HeroId join). XP 커브 미정이라 pct는 근사.
                var progression = session.Profile.HeroProgressions
                    .FirstOrDefault(p => string.Equals(p.HeroId, h.HeroId, StringComparison.Ordinal));
                var level = progression?.Level ?? 1;
                var xpPct = progression != null
                    ? Mathf.Clamp(progression.Experience % 100, 0, 100)
                    : 0;

                return new RosterGridHeroCardViewState(
                    HeroId: h.HeroId,
                    DisplayName: !string.IsNullOrEmpty(h.Name) ? h.Name : archetypeName,
                    ArchetypeLabel: $"{className} / {raceName}",
                    FamilyKey: classId,
                    RarityKey: h.RecruitTier.ToString().ToLowerInvariant(),
                    EquipSlots: h.EquippedItemIds?.Count(s => !string.IsNullOrEmpty(s)) ?? 0,
                    Level: level,
                    XpPct: xpPct);
            })
            .ToList();

        return new RosterGridViewState(
            Heroes: heroes,
            RosterCap: MetaBalanceDefaults.TownRosterCap,
            SelectedFilterKey: _selectedFilterKey);
    }
}
