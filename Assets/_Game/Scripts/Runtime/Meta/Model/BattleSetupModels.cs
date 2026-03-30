using System;
using System.Collections.Generic;
using SM.Combat.Model;
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
    IReadOnlyDictionary<string, IReadOnlyList<BattleSkillSpec>>? ItemGrantedSkills = null);

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
