using System.Collections.Generic;
using UnityEngine;

namespace SM.Unity.UI.Town.Preview;

/// <summary>
/// RosterGrid V1 surface ViewState — Town hub default.
/// runtime 모델 정합 (HeroInstanceRecord + HeroProgressionRecord, HeroId join):
/// - hero card = 정체성(Name/archetype/class/RecruitTier) + 진행도(Level/XP) + 장비 슬롯 수
/// - posture / KO·injured / last-node는 roster 영속 필드 아님 (squad·run 컨텍스트) → 컬럼에서 제거
/// </summary>
public sealed record RosterGridHeroCardViewState(
    string HeroId,                  // HeroInstanceRecord.HeroId
    string DisplayName,             // HeroInstanceRecord.Name (단일 필드)
    string ArchetypeLabel,          // "전위 / 솔라룸" — class / race resolved
    string FamilyKey,               // class — HeroPortraitCard atom modifier
    string RarityKey,               // common / rare / epic ← HeroInstanceRecord.RecruitTier
    int EquipSlots,                 // 0..3 ← EquippedItemIds count
    int Level,                      // ← HeroProgressionRecord.Level
    int XpPct,                      // 0..100 ← HeroProgressionRecord.Experience 파생 (커브 미정 근사)
    string CharacterId = ""         // 초상화 resolve 키 (CharacterId → ArchetypeId fallback). 비면 이니셜 placeholder.
);

public sealed record RosterGridFilterChipViewState(
    string Key,                     // all / human / beastkin / undead / vanguard / duelist / ranger / mystic
    string Label,
    string GroupKey,                // all / race / class (divider 구분용)
    bool IsSelected
);

public sealed record RosterGridViewState(
    IReadOnlyList<RosterGridHeroCardViewState> Heroes,
    int RosterCap,                  // 12 (MetaBalanceDefaults.TownRosterCap V1)
    string SelectedFilterKey,
    // 필터 칩은 로스터에 실제 존재하는 race/class만 data-driven 생성 → dead 필터 없음 +
    // 라벨이 ContentTextResolver 산출이라 UXML 하드코딩(세력명) ↔ 런타임(race) drift 제거.
    IReadOnlyList<RosterGridFilterChipViewState>? Filters = null
);
