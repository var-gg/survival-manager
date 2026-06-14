using System;
using System.Collections.Generic;
using UnityEngine;

namespace SM.Unity.UI.HeroDetail;

/// <summary>
/// 재사용 HeroDetail 공통 상세 surface의 4-slot 스킬 위계 상태.
/// per-hero stance/tier 곡선은 V1 런타임에 없어 본 분류에 포함하지 않는다 ([[hero-detail-uitk-adaptation]]).
/// </summary>
public enum HeroDetailSlotKind
{
    SignatureLock = 0,
    FlexActive = 1,
    FlexRetrain = 2,
    LateUnlock = 3,
}

public enum HeroDetailIconState
{
    Present = 0,
    Fallback = 1,
    Missing = 2,
}

public sealed record HeroDetailSkillSlotViewState(
    HeroDetailSlotKind SlotKind,
    string SkillId,
    string Name,
    string MetaLabel,
    string Description,
    string IconKey,
    HeroDetailIconState IconState,
    string CooldownText,
    bool IsPassive);

public sealed record HeroDetailStatViewState(
    string Key,
    string Label,
    string Value,
    string Tone);

public sealed record HeroDetailEquipSlotViewState(
    string SlotKey,
    string SlotLabel,
    string ItemLabel,
    string MetaLabel,
    string IconKey,
    bool IsFilled);

public sealed record HeroDetailTraitViewState(
    string Kind,
    string Name,
    string Description);

/// <summary>
/// Town/Battle 공유 HeroDetail UI 전용 read model. gameplay truth를 UI에서 재계산하지 않고
/// presenter/formatter가 공급한다. stance/xpRatio/최종 누적 스탯은 런타임 데이터 부재로 제외(scoped V1).
/// </summary>
public sealed record HeroDetailViewState(
    string HeroId,
    string DisplayName,
    string ArchetypeLabel,
    string RoleLabel,
    string FamilyKey,
    string TierLabel,
    string LevelLabel,
    Texture2D? PortraitSprite,
    IReadOnlyList<HeroDetailStatViewState> Stats,
    IReadOnlyList<HeroDetailSkillSlotViewState> SkillSlots,
    IReadOnlyList<HeroDetailEquipSlotViewState> Equipment,
    IReadOnlyList<HeroDetailTraitViewState> Traits)
{
    public static HeroDetailViewState Empty { get; } = new(
        HeroId: string.Empty,
        DisplayName: "—",
        ArchetypeLabel: string.Empty,
        RoleLabel: string.Empty,
        FamilyKey: string.Empty,
        TierLabel: string.Empty,
        LevelLabel: string.Empty,
        PortraitSprite: null,
        Stats: Array.Empty<HeroDetailStatViewState>(),
        SkillSlots: Array.Empty<HeroDetailSkillSlotViewState>(),
        Equipment: Array.Empty<HeroDetailEquipSlotViewState>(),
        Traits: Array.Empty<HeroDetailTraitViewState>());
}
