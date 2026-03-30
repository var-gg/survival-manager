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
    IReadOnlyList<string> TemporaryAugmentIds);

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
    IReadOnlyList<SkillDefinition> Skills);

public sealed record CombatContentSnapshot(
    IReadOnlyDictionary<string, CombatArchetypeTemplate> Archetypes,
    IReadOnlyDictionary<string, CombatModifierPackage> TraitPackages,
    IReadOnlyDictionary<string, CombatModifierPackage> ItemPackages,
    IReadOnlyDictionary<string, CombatModifierPackage> AffixPackages,
    IReadOnlyDictionary<string, CombatModifierPackage> AugmentPackages);

public sealed record BattleSetupBuildResult(
    bool IsSuccess,
    string? Error,
    IReadOnlyList<UnitDefinition> Allies,
    IReadOnlyList<UnitDefinition> Enemies)
{
    public static BattleSetupBuildResult Success(IReadOnlyList<UnitDefinition> allies, IReadOnlyList<UnitDefinition> enemies)
        => new(true, null, allies, enemies);

    public static BattleSetupBuildResult Fail(string error)
        => new(false, error, Array.Empty<UnitDefinition>(), Array.Empty<UnitDefinition>());
}
