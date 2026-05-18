using System.Collections.Generic;
using UnityEngine;

namespace SM.Unity.UI.Town.Preview;

public enum CompendiumTab
{
    Skills = 0,
    Status = 1,
    Synergy = 2,
    Characters = 3,
}

public sealed record CompendiumTabViewState(
    CompendiumTab Tab,
    string Label,
    bool IsSelected);

public sealed record CompendiumMetricViewState(
    string Label,
    string Value);

public sealed record CompendiumFilterOptionViewState(
    string Value,
    string Label);

public sealed record CompendiumFilterBarViewState(
    bool ShowSkillFilters,
    string SearchText,
    string SearchPlaceholder,
    string ClassLabel,
    string ClassValue,
    IReadOnlyList<CompendiumFilterOptionViewState> ClassOptions,
    string SlotLabel,
    string SlotValue,
    IReadOnlyList<CompendiumFilterOptionViewState> SlotOptions,
    string VfxFamilyLabel,
    string VfxFamilyValue,
    IReadOnlyList<CompendiumFilterOptionViewState> VfxFamilyOptions,
    string ResultSummary);

public sealed record CompendiumSkillViewState(
    string Id,
    string Name,
    string Description,
    string SlotLabel,
    string ClassLabel,
    string IntentLabel,
    string QuickStatLabel,
    string CombatLineLabel,
    string DamageLabel,
    string DeliveryLabel,
    string TargetLabel,
    string PowerLabel,
    string CooldownLabel,
    string StatusLabel,
    bool HasStatusPayload,
    string IconId,
    string VfxHookId,
    string VfxFamilyLabel,
    string VfxSkinLabel,
    string AnimationLabel,
    string CueSequenceLabel,
    string VfxPrefabLabel,
    string VfxPreviewStyle,
    GameObject? VfxPreviewPrefab,
    Texture2D? IconSprite,
    bool IsSelected);

public sealed record CompendiumStatusViewState(
    string Id,
    string Name,
    string Description,
    string GroupLabel,
    string RuleSummary,
    string VfxCueId,
    bool IsSelected);

public sealed record CompendiumSynergyViewState(
    string Id,
    string Name,
    string Description,
    string CountedTagLabel,
    string TierSummary,
    bool IsSelected);

public sealed record CompendiumCharacterViewState(
    string Id,
    string DisplayName,
    string Description,
    string RaceLabel,
    string ClassLabel,
    string RoleLabel,
    string UnlockLabel,
    Texture2D? PortraitSprite,
    bool IsUnlocked,
    bool IsSelected);

public sealed record CompendiumDetailViewState(
    string Title,
    string Subtitle,
    string Description,
    string HookLabel,
    Texture2D? IconSprite,
    CompendiumVfxPreviewViewState VfxPreview,
    IReadOnlyList<CompendiumMetricViewState> Metrics);

public sealed record CompendiumVfxPreviewViewState(
    bool CanPreview,
    int PlayToken,
    string Title,
    string ReplayLabel,
    string HookId,
    string StyleKey,
    GameObject? Prefab,
    string PrefabLabel,
    string Caption);

public sealed record CompendiumViewState(
    string Title,
    string Subtitle,
    string CloseLabel,
    CompendiumFilterBarViewState Filters,
    IReadOnlyList<CompendiumTabViewState> Tabs,
    CompendiumTab ActiveTab,
    IReadOnlyList<CompendiumSkillViewState> Skills,
    IReadOnlyList<CompendiumStatusViewState> Statuses,
    IReadOnlyList<CompendiumSynergyViewState> Synergies,
    IReadOnlyList<CompendiumCharacterViewState> Characters,
    CompendiumDetailViewState Detail);
