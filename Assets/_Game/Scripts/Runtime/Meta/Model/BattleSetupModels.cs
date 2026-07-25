using System;
using System.Collections.Generic;
using SM.Combat.Model;
using SM.Core.Content;
using SM.Core.Contracts;
using SM.Core.Stats;

namespace SM.Meta.Model;

public sealed record BattleEquippedItemSpec(
    string ItemBaseId,
    IReadOnlyList<string> AffixIds,
    IReadOnlyDictionary<string, float>? AffixMagnitudes = null);

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
    string OpeningIntent = "opening:standard",
    string CharacterId = "",
    string RoleInstructionId = "",
    DominantHand DominantHand = DominantHand.Right,
    IReadOnlyList<string>? RuleModifierTags = null);

public sealed record BattleEncounterPlan(
    IReadOnlyList<BattleParticipantSpec> EnemyParticipants,
    TeamPostureType EnemyPosture);

public sealed record RecruitBannedPairingTemplate(
    string FlexActiveId,
    string FlexPassiveId);

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
    FootprintProfile? Footprint = null,
    BehaviorProfile? Behavior = null,
    MobilityActionProfile? Mobility = null,
    float PreferredDistance = 0f,
    float ProtectRadius = 0f,
    ManaEnvelope? Mana = null,
    IReadOnlyList<CombatRuleModifierPackage>? RulePackages = null,
    RecruitTier RecruitTier = RecruitTier.Common,
    bool IsRecruitable = true,
    IReadOnlyList<string>? RecruitPlanTags = null,
    IReadOnlyList<string>? ScoutBiasTags = null,
    IReadOnlyList<BattleSkillSpec>? RecruitFlexActivePool = null,
    IReadOnlyList<BattleSkillSpec>? RecruitFlexPassivePool = null,
    IReadOnlyList<RecruitBannedPairingTemplate>? RecruitBannedPairings = null,
    BattleBasicAttackSpec? BasicAttack = null,
    BattleSkillSpec? SignatureActive = null,
    BattleSkillSpec? FlexActive = null,
    BattlePassiveSpec? SignaturePassive = null,
    BattlePassiveSpec? FlexPassive = null,
    BattleMobilitySpec? MobilityReaction = null,
    EnergyProfile? Energy = null,
    CombatEntityKind EntityKind = CombatEntityKind.RosterUnit,
    OwnershipLink? Ownership = null,
    SummonProfile? SummonProfile = null,
    ContentGovernanceSummary? Governance = null,
    DominantHand DefaultDominantHand = DominantHand.Right,
    CombatModifierPackage? ClassStatPackage = null);

public sealed record TeamTacticTemplate(
    string Id,
    TeamTacticProfile Profile);

public sealed record RoleInstructionTemplate(
    string Id,
    SlotRoleInstruction Instruction);

public sealed record CharacterTemplate(
    string Id,
    string RaceId,
    string ClassId,
    string DefaultArchetypeId,
    string DefaultRoleInstructionId,
    DominantHand DominantHand = DominantHand.Right);

public sealed record PassiveNodeTemplate(
    string Id,
    CombatModifierPackage Package,
    IReadOnlyList<string> CompileTags,
    CombatRuleModifierPackage? RulePackage = null,
    string BoardId = "",
    int BoardDepth = 0,
    PassiveNodeKindValue NodeKind = PassiveNodeKindValue.Small,
    IReadOnlyList<string>? PrerequisiteNodeIds = null,
    IReadOnlyList<string>? MutualExclusionTagIds = null,
    string GrantedSkillId = "");

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
    CombatRuleModifierPackage? RulePackage = null,
    ContentGovernanceSummary? Governance = null,
    IReadOnlyList<CombatTriggeredEffect>? TriggeredEffects = null,
    string OfferBucket = "",
    string RiskRewardClass = "",
    IReadOnlyList<string>? BuildBiasTags = null);

public sealed record SynergyTierTemplate(
    string Id,
    TeamSynergyTierRule Rule,
    ContentGovernanceSummary? Governance = null);

public sealed record CampaignChapterTemplate(
    string Id,
    string Name,
    int StoryOrder,
    IReadOnlyList<string> SiteIds,
    bool UnlocksEndlessOnClear,
    CampaignChapterBalanceTemplate? Balance = null);

public sealed record CampaignChapterBalanceTemplate(
    float HpEnvelope,
    float AtkEnvelope,
    float SiteHpStep,
    float SiteAtkStep)
{
    public static CampaignChapterBalanceTemplate Inert { get; } = new(1f, 1f, 0f, 0f);
}

public sealed record ExpeditionSiteTemplate(
    string Id,
    string ChapterId,
    string Name,
    int SiteOrder,
    string FactionId,
    IReadOnlyList<string> EncounterIds,
    string ExtractRewardSourceId,
    int ThreatTier,
    SiteGraphTemplate? Graph = null);

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
    string CharacterId,
    DeploymentAnchorId Anchor,
    string PositiveTraitId,
    string NegativeTraitId,
    EnemySquadMemberRoleValue Role,
    IReadOnlyList<string> RuleModifierTags,
    float EquipmentBudget = 0f,
    string EquipmentItemBaseId = "",
    IReadOnlyList<string>? EquipmentAffixIds = null);

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
    IReadOnlyList<StatusApplicationSpec> AppliedStatuses,
    BossPressureClockSpec? PressureClock = null);

public sealed record StatusFamilyTemplate(
    string Id,
    StatusGroupValue Group,
    bool IsHardControl,
    bool UsesControlDiminishing,
    bool AffectedByTenacity,
    float TenacityScale,
    bool AppliesPeriodicDamage,
    string VfxCueId,
    bool IsRuleModifierOnly,
    IReadOnlyList<string> CompileTags,
    ContentGovernanceSummary? Governance = null,
    // 받는 피해 배수 delta (guarded=-0.1) — sim 리터럴의 콘텐츠 승격.
    float IncomingDamageDelta = 0f,
    // magnitude→숫자 채널 배율 (sunder/marked/exposed/wound/slow). 1=magnitude 직소비(현행 공식) — 2보.
    float MagnitudeScale = 1f,
    // 적용 시 즉시 보호막 전환(barrier) — 효과 종류 데이터화 3보 1슬라이스. false=일반 상태 잔존(현행 기본).
    bool GrantsBarrierOnApply = false,
    // 보유 시 저지불가(하드 컨트롤 적용 면역 + 저작 강제이동 면역, unstoppable) — 3보 3b. false=면역 없음(현행 기본).
    bool GrantsUnstoppable = false,
    // 보유 시 액티브 시전 차단(silence) — 3보 3c. false=차단 없음(현행 기본).
    bool BlocksActiveSkills = false,
    // 보유 시 자발 이동 차단(root) — 3보 3d. false=차단 없음(현행 기본).
    bool BlocksMovement = false,
    // 보유 시 행동 차단(턴 스킵/취소/전 행동 게이트, stun) — 3보 3e. false=차단 없음(현행 기본).
    bool BlocksAction = false,
    // 채널 membership 5종(3f) — 받는피해 가산(marked/exposed)/받는피해 delta(guarded)/방어·저항 차감(sunder)/
    // 치유 감소(wound)/공속·이속 감쇠(slow). false=채널 비소속(현행 기본).
    bool AmplifiesIncomingDamage = false,
    bool GrantsGuardedDefense = false,
    bool ShredsDefense = false,
    bool ReducesHealing = false,
    bool DampensTempo = false,
    // 타게팅 표식 membership(marked) — 3g. false=표식 아님(현행 기본).
    bool MarksTarget = false,
    // application magnitude의 단위 계약. authored family -> parser/converter -> pure template로 보존한다.
    MagnitudeUnit MagnitudeUnit = SM.Core.Content.MagnitudeUnit.Flat);

public sealed record CleanseProfileTemplate(
    string Id,
    IReadOnlyList<string> RemovesStatusIds,
    bool RemovesOneHardControl,
    bool GrantsUnstoppable,
    float GrantedUnstoppableDurationSeconds,
    // 부여 상태 id — 과거 sim "unstoppable" 리터럴의 콘텐츠 승격(위생 꼬리). 기본값=현행 동작.
    string GrantedStatusId = "unstoppable");

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
    bool IsGuaranteed,
    IReadOnlyList<string> RequiredContextTags);

public sealed record DropTableTemplate(
    string Id,
    string RewardSourceId,
    IReadOnlyList<LootBundleEntryTemplate> Entries,
    float GradePowerKappa = 0f,
    float GradeStepBudgetScore = 0f,
    IReadOnlyList<DropGradeProfileTemplate>? GradeProfiles = null,
    double GradeJackpotWeight = 0d,
    double GradeJackpotLatentMean = 4.25d,
    double GradeJackpotStandardDeviation = 0.25d);

public sealed record DropGradeProfileTemplate(
    string ChapterId,
    double InitialLatentMean,
    double InitialStandardDeviation,
    double MeanPreservingLatentMean,
    double StandardDeviation);

/// <summary>
/// Refit의 authored knobs와 closed-form에서 생성된 Q0.64 floor schedule.
/// Schedule은 content conversion 시 한 번 생성되어 snapshot/save-independent balance data로 직렬화된다.
/// </summary>
public sealed record RefitBalanceTemplate(
    int RulesVersion,
    string AffixCatalogVersion,
    int MaximumFloorNumerator,
    int MaximumFloorDenominator,
    int FloorDecayNumerator,
    int FloorDecayDenominator,
    IReadOnlyList<ulong> FloorScheduleQ64,
    double CostBaseFirstFarmEchoMultiplier,
    double CostGrowthPerLevel,
    double GradeCostRatio);

public sealed record LootBundleTemplate(
    string Id,
    string RewardSourceId,
    IReadOnlyList<LootBundleEntryTemplate> Entries);

public sealed record TraitTokenTemplate(
    string Id,
    RewardType RewardType);

/// <summary>
/// 아이템의 non-numeric 계약(CompileTags/무기 family 태그/장착 허용 메타). 수치는 기존 ItemPackages가 소유하고,
/// 이 템플릿은 유닛 태그 전파(→ affix 조건·서포트 젬 무기 게이트의 판정 재료)와
/// Unity 밖 sweep이 사용할 slot/class 제약을 담당한다.
/// 과거에는 수치만 변환돼 무기 게이트가 영구 무력이었다.
/// </summary>
public sealed record ItemTemplate(
    string Id,
    IReadOnlyList<string> CompileTags,
    string WeaponFamilyTag,
    string SlotType = "",
    IReadOnlyList<string>? AllowedClassIds = null,
    ItemRarityTierValue RarityTier = ItemRarityTierValue.Common,
    ItemIdentityValue IdentityKind = ItemIdentityValue.Baseline);

/// <summary>
/// affix의 non-numeric 계약(태그/조건/rule/저작 spawn 메타). 수치는 기존 AffixPackages가 계속 소유하고,
/// 이 템플릿은 CompileTags 전파·RequiredTags/ExcludedTags 조건 게이트·RuleModifierTags를
/// 컴파일까지 실어나르며 Unity 밖 sweep에는 slot/budget/weight를 제공한다.
/// 과거에는 ModifierPackageConverter가 수치만 변환해 전부 드롭됐다.
/// </summary>
public sealed record AffixTemplate(
    string Id,
    IReadOnlyList<string> CompileTags,
    IReadOnlyList<string> RequiredTags,
    IReadOnlyList<string> ExcludedTags,
    CombatRuleModifierPackage? RulePackage,
    IReadOnlyList<string>? AllowedSlotTypes = null,
    float BudgetScore = 0f,
    float SpawnWeight = 0f,
    string Tier = "",
    int ItemLevelMin = 0,
    string ExclusiveGroupId = "",
    float ValueMin = 0f,
    float ValueMax = 0f,
    IReadOnlyList<CombatTriggeredEffect>? TriggeredEffects = null)
{
    public bool IsConditional => RequiredTags.Count > 0 || ExcludedTags.Count > 0;
}

/// <summary>
/// 세션이 영웅의 trait id를 결정적으로 정규화할 때 필요한 archetype별 저작 순서.
/// EncounterTraits는 적 전용이므로 이 player roster pool에는 포함하지 않는다.
/// </summary>
public sealed record ArchetypeTraitPoolTemplate(
    IReadOnlyList<string> PositiveTraitIds,
    IReadOnlyList<string> NegativeTraitIds);

/// <summary>
/// RuntimeCombatContentLookup의 canonical fallback 순서를 Unity 밖에서도 그대로 재현하는 순수 snapshot.
/// 정렬 가능한 catalog dictionary만으로는 legacy 우선순위를 복원할 수 없으므로 순서를 명시적으로 보존한다.
/// </summary>
public sealed record SessionContentOrder(
    IReadOnlyList<string> ArchetypeIds,
    IReadOnlyList<string> ItemIds,
    IReadOnlyList<string> AffixIds,
    IReadOnlyList<string> TemporaryAugmentIds,
    IReadOnlyList<string> PermanentAugmentIds,
    IReadOnlyList<string> PassiveBoardIds,
    IReadOnlyList<string> SynergyFamilyIds);

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
    IReadOnlyDictionary<string, TraitTokenTemplate>? TraitTokens = null,
    FirstPlayableSliceDefinition? FirstPlayableSlice = null,
    IReadOnlyDictionary<string, CharacterTemplate>? Characters = null,
    IReadOnlyDictionary<string, AffixTemplate>? AffixCatalog = null,
    IReadOnlyDictionary<string, ItemTemplate>? ItemCatalog = null,
    IReadOnlyDictionary<string, ArchetypeTraitPoolTemplate>? ArchetypeTraitPools = null,
    SessionContentOrder? SessionContentOrder = null,
    WarWoundSpec? WarWound = null,
    IReadOnlyDictionary<string, SiteEventTemplate>? SiteEvents = null,
    RefitBalanceTemplate? RefitBalance = null);

public sealed record BattleSetupBuildResult(
    bool IsSuccess,
    string? Error,
    IReadOnlyList<BattleUnitLoadout> Allies,
    IReadOnlyList<BattleUnitLoadout> Enemies,
    CombatStatusRules? StatusRules = null)
{
    public static BattleSetupBuildResult Success(IReadOnlyList<BattleUnitLoadout> allies, IReadOnlyList<BattleUnitLoadout> enemies, CombatStatusRules? statusRules = null)
        => new(true, null, allies, enemies, statusRules);

    public static BattleSetupBuildResult Fail(string error)
        => new(false, error, Array.Empty<BattleUnitLoadout>(), Array.Empty<BattleUnitLoadout>(), CombatStatusRules.Default);
}
