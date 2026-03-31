using System;
using System.Collections.Generic;
using SM.Combat.Model;
using SM.Content.Definitions;
using SM.Core.Stats;

namespace SM.Meta.Model;

public sealed record BattleEquippedItemSpec(
    string ItemBaseId,
    IReadOnlyList<string> AffixIds);

public sealed record BattleParticipantSpec(
    string ParticipantId,
    string DisplayName,
    string ArchetypeId,
    DeploymentAnchorId Anchor,
    string PositiveTraitId,
    string NegativeTraitId,
    IReadOnlyList<BattleEquippedItemSpec> EquippedItems,
    IReadOnlyList<string> TemporaryAugmentIds,
    TeamPostureType TeamPosture = TeamPostureType.StandardAdvance,
    string RoleTag = "auto",
    string OpeningIntent = "opening:standard");

public sealed record BattleEncounterPlan(
    IReadOnlyList<BattleParticipantSpec> EnemyParticipants,
    TeamPostureType EnemyPosture);

public sealed record CombatArchetypeTemplate(
    string Id,
    string DisplayName,
    string RaceId,
    string ClassId,
    DeploymentAnchorId DefaultAnchor,
    IReadOnlyDictionary<StatKey, float> BaseStats,
    IReadOnlyList<TacticRule> Tactics,
    IReadOnlyList<BattleSkillSpec> Skills,
    string RoleTag = "auto",
    float PreferredDistance = 0f,
    float ProtectRadius = 0f,
    ManaEnvelope? Mana = null,
    IReadOnlyList<CombatRuleModifierPackage>? RulePackages = null);

public sealed record TeamTacticTemplate(
    string Id,
    TeamTacticProfile Profile);

public sealed record RoleInstructionTemplate(
    string Id,
    SlotRoleInstruction Instruction);

public sealed record PassiveNodeTemplate(
    string Id,
    CombatModifierPackage Package,
    IReadOnlyList<string> CompileTags,
    CombatRuleModifierPackage? RulePackage = null);

public sealed record AugmentCatalogEntry(
    string Id,
    string Category,
    string FamilyId,
    int Tier,
    bool IsPermanent,
    bool SuppressIfPermanentEquipped,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> MutualExclusionTags,
    CombatModifierPackage Package,
    CombatRuleModifierPackage? RulePackage = null);

public sealed record SynergyTierTemplate(
    string Id,
    TeamSynergyTierRule Rule);

public sealed record CampaignChapterTemplate(
    string Id,
    string Name,
    int StoryOrder,
    IReadOnlyList<string> SiteIds,
    bool UnlocksEndlessOnClear);

public sealed record ExpeditionSiteTemplate(
    string Id,
    string ChapterId,
    string Name,
    int SiteOrder,
    string FactionId,
    IReadOnlyList<string> EncounterIds,
    string ExtractRewardSourceId,
    int ThreatTier);

public sealed record EncounterTemplate(
    string Id,
    string Name,
    string SiteId,
    string EnemySquadTemplateId,
    string BossOverlayId,
    string RewardSourceId,
    string FactionId,
    int ThreatTier,
    int ThreatCost,
    int ThreatSkulls,
    string DifficultyBand,
    EncounterKindValue Kind,
    IReadOnlyList<string> RewardDropTags);

public sealed record EnemySquadMemberTemplate(
    string Id,
    string Name,
    string ArchetypeId,
    DeploymentAnchorId Anchor,
    string PositiveTraitId,
    string NegativeTraitId,
    EnemySquadMemberRoleValue Role,
    IReadOnlyList<string> RuleModifierTags);

public sealed record EnemySquadTemplate(
    string Id,
    string Name,
    string FactionId,
    TeamPostureType EnemyPosture,
    int ThreatTier,
    int ThreatCost,
    IReadOnlyList<string> RewardDropTags,
    IReadOnlyList<EnemySquadMemberTemplate> Members);

public sealed record BossOverlayTemplate(
    string Id,
    string Name,
    BossPhaseTriggerValue PhaseTrigger,
    int ThreatCost,
    string SignatureAuraTag,
    string SignatureUtilityTag,
    IReadOnlyList<string> RewardDropTags,
    IReadOnlyList<StatusApplicationSpec> AppliedStatuses);

public sealed record StatusFamilyTemplate(
    string Id,
    StatusGroupValue Group,
    bool IsHardControl,
    bool UsesControlDiminishing,
    bool AffectedByTenacity,
    float TenacityScale,
    bool IsRuleModifierOnly,
    IReadOnlyList<string> CompileTags);

public sealed record CleanseProfileTemplate(
    string Id,
    IReadOnlyList<string> RemovesStatusIds,
    bool RemovesOneHardControl,
    bool GrantsUnstoppable,
    float GrantedUnstoppableDurationSeconds);

public sealed record ControlDiminishingTemplate(
    string Id,
    float ControlResistMultiplier,
    float WindowSeconds,
    IReadOnlyList<string> FullTenacityStatusIds,
    IReadOnlyList<string> PartialTenacityStatusIds);

public sealed record RewardSourceTemplate(
    string Id,
    string Name,
    RewardSourceKindValue Kind,
    string DropTableId,
    bool UsesRewardCards,
    IReadOnlyList<RarityBracketValue> AllowedRarityBrackets);

public sealed record LootBundleEntryTemplate(
    string Id,
    RewardType RewardType,
    int Amount,
    RarityBracketValue RarityBracket,
    int Weight,
    bool IsGuaranteed);

public sealed record DropTableTemplate(
    string Id,
    string RewardSourceId,
    IReadOnlyList<LootBundleEntryTemplate> Entries);

public sealed record LootBundleTemplate(
    string Id,
    string RewardSourceId,
    IReadOnlyList<LootBundleEntryTemplate> Entries);

public sealed record TraitTokenTemplate(
    string Id,
    RewardType RewardType);

public sealed record CombatContentSnapshot(
    IReadOnlyDictionary<string, CombatArchetypeTemplate> Archetypes,
    IReadOnlyDictionary<string, CombatModifierPackage> TraitPackages,
    IReadOnlyDictionary<string, CombatModifierPackage> ItemPackages,
    IReadOnlyDictionary<string, CombatModifierPackage> AffixPackages,
    IReadOnlyDictionary<string, CombatModifierPackage> AugmentPackages,
    IReadOnlyDictionary<string, BattleSkillSpec> SkillCatalog,
    IReadOnlyDictionary<string, TeamTacticTemplate> TeamTactics,
    IReadOnlyDictionary<string, RoleInstructionTemplate> RoleInstructions,
    IReadOnlyDictionary<string, PassiveNodeTemplate> PassiveNodes,
    IReadOnlyDictionary<string, AugmentCatalogEntry> AugmentCatalog,
    IReadOnlyDictionary<string, SynergyTierTemplate> SynergyCatalog,
    IReadOnlyDictionary<string, IReadOnlyList<BattleSkillSpec>>? ItemGrantedSkills = null,
    IReadOnlyDictionary<string, CampaignChapterTemplate>? CampaignChapters = null,
    IReadOnlyDictionary<string, ExpeditionSiteTemplate>? ExpeditionSites = null,
    IReadOnlyDictionary<string, EncounterTemplate>? Encounters = null,
    IReadOnlyDictionary<string, EnemySquadTemplate>? EnemySquads = null,
    IReadOnlyDictionary<string, BossOverlayTemplate>? BossOverlays = null,
    IReadOnlyDictionary<string, StatusFamilyTemplate>? StatusFamilies = null,
    IReadOnlyDictionary<string, CleanseProfileTemplate>? CleanseProfiles = null,
    IReadOnlyDictionary<string, ControlDiminishingTemplate>? ControlDiminishingRules = null,
    IReadOnlyDictionary<string, RewardSourceTemplate>? RewardSources = null,
    IReadOnlyDictionary<string, DropTableTemplate>? DropTables = null,
    IReadOnlyDictionary<string, LootBundleTemplate>? LootBundles = null,
    IReadOnlyDictionary<string, TraitTokenTemplate>? TraitTokens = null);

public sealed record BattleSetupBuildResult(
    bool IsSuccess,
    string? Error,
    IReadOnlyList<BattleUnitLoadout> Allies,
    IReadOnlyList<BattleUnitLoadout> Enemies)
{
    public static BattleSetupBuildResult Success(IReadOnlyList<BattleUnitLoadout> allies, IReadOnlyList<BattleUnitLoadout> enemies)
        => new(true, null, allies, enemies);

    public static BattleSetupBuildResult Fail(string error)
        => new(false, error, Array.Empty<BattleUnitLoadout>(), Array.Empty<BattleUnitLoadout>());
}
