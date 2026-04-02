using System.Collections.Generic;
using System.Linq;
using SM.Core.Contracts;
using SM.Core.Stats;

namespace SM.Combat.Model;

public record TeamTacticProfile(
    string Id,
    string DisplayName,
    TeamPostureType Posture,
    float CombatPace = 1f,
    float FocusModeBias = 0f,
    float FrontSpacingBias = 0f,
    float BackSpacingBias = 0f,
    float ProtectCarryBias = 0f,
    float TargetSwitchPenalty = 0f);

public record SlotRoleInstruction(
    DeploymentAnchorId Anchor,
    string RoleTag,
    float ProtectCarryBias = 0f,
    float BacklinePressureBias = 0f,
    float RetreatBias = 0f);

public record UnitRuleChain(
    string Id,
    IReadOnlyList<TacticRule> Rules);

public record ManaEnvelope(
    float Max,
    float GainOnAttack,
    float GainOnHit,
    float Current = 0f);

public record EnergyProfile(
    float Max,
    float Starting = 10f,
    ResourceKind Kind = ResourceKind.Energy);

public record TeamSynergyTierRule(
    string SynergyId,
    string CountedTagId,
    int Threshold,
    IReadOnlyList<StatModifier> Modifiers);

public record BattleUnitLoadout(
    string Id,
    string Name,
    string RaceId,
    string ClassId,
    DeploymentAnchorId PreferredAnchor,
    Dictionary<StatKey, float> BaseStats,
    IReadOnlyList<UnitRuleChain> RuleChains,
    IReadOnlyList<BattleSkillSpec> Skills,
    TeamTacticProfile? TeamTactic = null,
    SlotRoleInstruction? RoleInstruction = null,
    string OpeningIntent = "default",
    IReadOnlyList<CombatModifierPackage>? Packages = null,
    IReadOnlyList<CombatModifierPackage>? TeamPackages = null,
    IReadOnlyList<string>? CompileTags = null,
    string RoleTag = "auto",
    RoleVariantTag RoleVariant = RoleVariantTag.Unassigned,
    FootprintProfile? Footprint = null,
    BehaviorProfile? Behavior = null,
    MobilityActionProfile? Mobility = null,
    float PreferredDistance = 0f,
    float ProtectRadius = 0f,
    ManaEnvelope? Mana = null,
    IReadOnlyList<CombatRuleModifierPackage>? RulePackages = null,
    IReadOnlyList<CombatRuleModifierPackage>? TeamRulePackages = null,
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
    ContentGovernanceSummary? Governance = null)
{
    public IReadOnlyList<TacticRule> Tactics => RuleChains.SelectMany(chain => chain.Rules).ToList();

    public IReadOnlyList<CombatModifierPackage> NumericPackages => Packages ?? System.Array.Empty<CombatModifierPackage>();

    public ManaEnvelope EffectiveMana => Mana ?? new ManaEnvelope(0f, 0f, 0f, 0f);

    public EnergyProfile EffectiveEnergy => Energy ?? new EnergyProfile(100f, 10f);

    public BattleBasicAttackSpec EffectiveBasicAttack => BasicAttack ?? new BattleBasicAttackSpec(
        "basic_attack",
        "Basic Attack",
        TargetRuleData: new TargetRule());

    public BattleSkillSpec? EffectiveSignatureActive => SignatureActive
        ?? Skills.FirstOrDefault(skill => skill.EffectiveSlotKind == ActionSlotKind.SignatureActive);

    public BattleSkillSpec? EffectiveFlexActive => FlexActive
        ?? Skills.FirstOrDefault(skill => skill.EffectiveSlotKind == ActionSlotKind.FlexActive);

    public BattlePassiveSpec EffectiveSignaturePassive => SignaturePassive
        ?? new BattlePassiveSpec(
            Skills.FirstOrDefault(skill => skill.EffectiveSlotKind == ActionSlotKind.SignaturePassive)?.Id ?? $"{Id}:signature_passive",
            Skills.FirstOrDefault(skill => skill.EffectiveSlotKind == ActionSlotKind.SignaturePassive)?.Name ?? "Signature Passive",
            ActionSlotKind.SignaturePassive);

    public BattlePassiveSpec EffectiveFlexPassive => FlexPassive
        ?? new BattlePassiveSpec(
            Skills.FirstOrDefault(skill => skill.EffectiveSlotKind == ActionSlotKind.FlexPassive)?.Id ?? $"{Id}:flex_passive",
            Skills.FirstOrDefault(skill => skill.EffectiveSlotKind == ActionSlotKind.FlexPassive)?.Name ?? "Flex Passive",
            ActionSlotKind.FlexPassive);

    public BattleMobilitySpec? EffectiveMobilityReaction => MobilityReaction
        ?? (Mobility is { IsEnabled: true } profile
            ? new BattleMobilitySpec($"{Id}:mobility", "Mobility Reaction", profile, new TargetRule())
            : null);

    public bool HasLoopALoadout => BasicAttack != null
        || SignatureActive != null
        || FlexActive != null
        || SignaturePassive != null
        || FlexPassive != null
        || MobilityReaction != null;
}

[System.Obsolete("Use BattleUnitLoadout for compiled battle inputs.")]
public sealed record UnitDefinition : BattleUnitLoadout
{
    public UnitDefinition(
        string id,
        string name,
        string raceId,
        string classId,
        DeploymentAnchorId preferredAnchor,
        Dictionary<StatKey, float> baseStats,
        IReadOnlyList<TacticRule> tactics,
        IReadOnlyList<SkillDefinition> skills,
        IReadOnlyList<CombatModifierPackage>? packages = null)
        : base(
            id,
            name,
            raceId,
            classId,
            preferredAnchor,
            baseStats,
            new[] { new UnitRuleChain("legacy", tactics) },
            skills.Cast<BattleSkillSpec>().ToList(),
            null,
            null,
            "legacy",
            packages)
    {
    }
}
