using System.Collections.Generic;
using SM.Combat.Model;
using SM.Core.Contracts;

namespace SM.Meta.Model;

public sealed record HeroLoadoutState(
    string HeroId,
    IReadOnlyList<string> EquippedItemInstanceIds,
    IReadOnlyList<string> EquippedSkillInstanceIds,
    string PassiveBoardId,
    IReadOnlyList<string> SelectedPassiveNodeIds,
    IReadOnlyList<string> EquippedPermanentAugmentIds);

public sealed record HeroProgressionState(
    string HeroId,
    int Level,
    int Experience,
    IReadOnlyList<string> UnlockedPassiveNodeIds,
    IReadOnlyList<string> UnlockedSkillIds);

public sealed record ItemInstanceState(
    string ItemInstanceId,
    string ItemBaseId,
    IReadOnlyList<string> AffixIds,
    string EquippedHeroId);

public sealed record SkillInstanceState(
    string SkillInstanceId,
    string SkillId,
    string SlotKind,
    IReadOnlyList<string> CompileTags,
    ActionSlotKind? ResolvedSlotKind = null);

public sealed record PassiveBoardSelectionState(
    string HeroId,
    string BoardId,
    IReadOnlyList<string> SelectedNodeIds);

public sealed record PermanentAugmentLoadoutState(
    string BlueprintId,
    IReadOnlyList<string> EquippedAugmentIds);

public sealed record SquadBlueprintState(
    string BlueprintId,
    string DisplayName,
    TeamPostureType TeamPosture,
    string TeamTacticId,
    IReadOnlyDictionary<DeploymentAnchorId, string> DeploymentAssignments,
    IReadOnlyList<string> ExpeditionSquadHeroIds,
    IReadOnlyDictionary<string, string> HeroRoleIds);

public sealed record RunOverlayState(
    int CurrentNodeIndex,
    IReadOnlyList<string> TemporaryAugmentIds,
    IReadOnlyList<string> PendingRewardIds,
    string CompileVersion,
    string LastCompileHash,
    string ChapterId = "",
    string SiteId = "",
    int SiteNodeIndex = 0,
    string EncounterId = "",
    int BattleSeed = 0,
    string BattleContextHash = "",
    string RewardSourceId = "");

public sealed record ActiveRunState(
    string RunId,
    string ExpeditionId,
    SquadBlueprintState Blueprint,
    RunOverlayState Overlay,
    IReadOnlyList<string> BattleDeployHeroIds,
    bool IsQuickBattle,
    string? LastBattleMatchId = null,
    bool StoryCleared = false,
    bool EndlessUnlocked = false);

public sealed record InventoryLedgerEntry(
    string EntryId,
    string RunId,
    string ItemInstanceId,
    string ItemBaseId,
    string ChangeKind,
    int Amount,
    string CreatedAtUtc,
    string Summary,
    string SourceId = "",
    string SourceKind = "");

public sealed record RewardLedgerEntry(
    string EntryId,
    string RunId,
    string RewardId,
    string RewardType,
    int Amount,
    string CreatedAtUtc,
    string Summary,
    string SourceId = "",
    string SourceKind = "");
