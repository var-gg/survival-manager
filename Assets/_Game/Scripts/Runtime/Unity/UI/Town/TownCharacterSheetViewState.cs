using System.Collections.Generic;
using UnityEngine;

namespace SM.Unity.UI.Town;

public sealed record TownCharacterSheetPanelRowViewState(
    string Label,
    string Value,
    string Tone = "");

public sealed record TownCharacterSheetPanelViewState(
    string Title,
    IReadOnlyList<TownCharacterSheetPanelRowViewState> Rows);

public sealed record TownCharacterSheetHeroRailEntryViewState(
    string HeroId,
    string DisplayName,
    string MetaLabel,
    string FamilyKey,
    Texture2D? PortraitSprite,
    bool IsSelected);

public sealed record TownCharacterSheetStatViewState(
    string Key,
    string Label,
    string Value,
    string DeltaLabel,
    string Tone);

public sealed record TownCharacterSheetSkillCardViewState(
    string SkillId,
    string Name,
    string MetaLabel,
    string Description,
    string IconKey,
    Texture2D? IconSprite,
    bool IsSignature);

public sealed record TownCharacterSheetEquipmentSlotViewState(
    string SlotKey,
    string SlotLabel,
    string ItemLabel,
    string MetaLabel,
    string IconKey,
    Texture2D? IconSprite,
    bool IsFilled);

public sealed record TownCharacterSheetProgressionNodeViewState(
    string Label,
    string State);

public sealed record TownCharacterSheetViewState(
    string HeroId,
    string DisplayName,
    string ArchetypeLabel,
    string RoleLabel,
    string FamilyKey,
    Texture2D? PortraitSprite,
    IReadOnlyList<TownCharacterSheetHeroRailEntryViewState> HeroRail,
    IReadOnlyList<TownCharacterSheetStatViewState> Stats,
    IReadOnlyList<TownCharacterSheetSkillCardViewState> Skills,
    IReadOnlyList<TownCharacterSheetEquipmentSlotViewState> Equipment,
    IReadOnlyList<TownCharacterSheetProgressionNodeViewState> ProgressionNodes,
    TownCharacterSheetPanelViewState Overview,
    TownCharacterSheetPanelViewState Loadout,
    TownCharacterSheetPanelViewState Passives,
    TownCharacterSheetPanelViewState Synergy,
    TownCharacterSheetPanelViewState Progression);
