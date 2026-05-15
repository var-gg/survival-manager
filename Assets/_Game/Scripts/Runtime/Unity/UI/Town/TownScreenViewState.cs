using System.Collections.Generic;
using SM.Unity.UI;

namespace SM.Unity.UI.Town;

/// <summary>
/// V1 hub viewstate — RosterGrid 12 hero card + bottom toolbar (audit §2.1).
/// 옛 dashboard (recruit/deploy/character sheet/retrain/dismiss/board/augment 30+ 액션 통합)는 폐기.
/// 후속 phase에서 Recruit/EquipmentRefit/PassiveBoard/CharacterSheet는 modal로 분리.
/// </summary>
public sealed record TownScreenViewState(
    string TitleEyebrow,
    string Title,
    string LocaleStatus,
    string LocaleKoLabel,
    string LocaleEnLabel,
    string HelpButtonLabel,
    HelpStripViewState Help,
    int RosterCap,
    IReadOnlyList<TownHeroCardViewState> Heroes,
    string RecruitLabel,
    string EquipmentRefitLabel,
    string PassiveBoardLabel,
    string PermanentAugmentLabel,
    string ExpeditionLabel,
    string ExpeditionTooltip,
    string SaveLabel,
    string LoadLabel,
    string ReturnToStartLabel,
    string ReturnToStartTooltip,
    bool CanReturnToStart,
    string QuickBattleLabel,
    string QuickBattleTooltip,
    bool CanQuickBattle,
    bool ShowQuickBattle,
    string StatusText);

/// <summary>per-hero card column — runtime model (HeroInstanceRecord + HeroProgressionRecord) 정합.</summary>
public sealed record TownHeroCardViewState(
    string HeroId,
    string DisplayName,
    string ArchetypeLabel,
    string FamilyKey,
    string RarityKey,
    int EquipSlots,
    int Level,
    int XpPct);
