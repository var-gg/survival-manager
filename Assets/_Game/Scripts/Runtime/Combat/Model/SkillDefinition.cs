using System.Collections.Generic;
using SM.Core.Contracts;

namespace SM.Combat.Model;

public enum DamageType
{
    Physical = 0,
    Magical = 1,
    Healing = 2,
    True = 3,
}

public static class CompiledSkillSlots
{
    public const string CoreActive = "core_active";
    public const string UtilityActive = "utility_active";
    public const string Passive = "passive";
    public const string Support = "support";

    private static readonly IReadOnlyDictionary<string, string> LegacyAliases = new Dictionary<string, string>(System.StringComparer.Ordinal)
    {
        ["active_core"] = CoreActive,
        ["active_utility"] = UtilityActive,
    };

    public static readonly IReadOnlyList<string> Ordered = new[]
    {
        CoreActive,
        UtilityActive,
        Passive,
        Support,
    };

    public static bool IsSupported(string? slotKind)
    {
        if (string.IsNullOrWhiteSpace(slotKind))
        {
            return false;
        }

        if (LegacyAliases.ContainsKey(slotKind))
        {
            return true;
        }

        for (var i = 0; i < Ordered.Count; i++)
        {
            if (string.Equals(Ordered[i], slotKind, System.StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public static string Normalize(string? slotKind, string fallback = CoreActive)
    {
        if (!string.IsNullOrWhiteSpace(slotKind) && LegacyAliases.TryGetValue(slotKind, out var canonical))
        {
            return canonical;
        }

        if (IsSupported(slotKind))
        {
            return slotKind!;
        }

        if (!string.IsNullOrWhiteSpace(fallback) && LegacyAliases.TryGetValue(fallback, out canonical))
        {
            return canonical;
        }

        return IsSupported(fallback) ? fallback : CoreActive;
    }

    public static ActionSlotKind ToActionSlotKind(string? slotKind, ActionSlotKind fallback = ActionSlotKind.FlexActive)
    {
        return Normalize(slotKind) switch
        {
            CoreActive => ActionSlotKind.SignatureActive,
            UtilityActive => ActionSlotKind.FlexActive,
            Passive => ActionSlotKind.SignaturePassive,
            Support => ActionSlotKind.FlexPassive,
            _ => fallback,
        };
    }

    public static string FromActionSlotKind(ActionSlotKind slotKind)
    {
        return slotKind switch
        {
            ActionSlotKind.SignatureActive => CoreActive,
            ActionSlotKind.FlexActive => UtilityActive,
            ActionSlotKind.SignaturePassive => Passive,
            ActionSlotKind.FlexPassive => Support,
            _ => CoreActive,
        };
    }
}

public sealed record BattleBasicAttackSpec(
    string Id,
    string Name,
    DamageType DamageType = DamageType.Physical,
    TargetRule? TargetRuleData = null,
    ActionLane Lane = ActionLane.Primary,
    ActionLockRule LockRule = ActionLockRule.SoftCommit,
    IReadOnlyList<EffectDescriptor>? EffectDescriptors = null);

public sealed record BattlePassiveSpec(
    string Id,
    string Name,
    ActionSlotKind SlotKind = ActionSlotKind.SignaturePassive,
    ActivationModel ActivationModel = ActivationModel.Passive,
    IReadOnlyList<EffectDescriptor>? EffectDescriptors = null,
    bool AllowMirroredOwnedSummonKill = false,
    string EffectFamilyId = "");

public sealed record BattleMobilitySpec(
    string Id,
    string Name,
    MobilityActionProfile Profile,
    TargetRule? TargetRuleData = null,
    ActivationModel ActivationModel = ActivationModel.Trigger,
    ActionLane Lane = ActionLane.Reaction,
    ActionLockRule LockRule = ActionLockRule.HardCommit,
    IReadOnlyList<EffectDescriptor>? EffectDescriptors = null);

public record BattleSkillSpec(
    string Id,
    string Name,
    SkillKind Kind,
    float Power,
    float Range,
    string SlotKind = CompiledSkillSlots.CoreActive,
    IReadOnlyList<string>? CompileTags = null,
    DamageType DamageType = DamageType.Physical,
    float PowerFlat = 0f,
    float PhysCoeff = 1f,
    float MagCoeff = 0f,
    float HealCoeff = 0f,
    float ManaCost = 0f,
    float BaseCooldownSeconds = 0f,
    float CastWindupSeconds = 0f,
    IReadOnlyList<string>? RuleModifierTags = null,
    float HealthCoeff = 0f,
    bool CanCrit = false,
    SkillDelivery Delivery = SkillDelivery.Melee,
    SkillTargetRule TargetRule = SkillTargetRule.NearestEnemy,
    IReadOnlyList<string>? SupportAllowedTags = null,
    IReadOnlyList<string>? SupportBlockedTags = null,
    IReadOnlyList<string>? RequiredWeaponTags = null,
    IReadOnlyList<string>? RequiredClassTags = null,
    IReadOnlyList<StatusApplicationSpec>? AppliedStatuses = null,
    string CleanseProfileId = "",
    ActionSlotKind ResolvedSlotKind = ActionSlotKind.SignatureActive,
    ActivationModel ActivationModel = ActivationModel.Cooldown,
    ActionLane Lane = ActionLane.Primary,
    ActionLockRule LockRule = ActionLockRule.HardCommit,
    AuthorityLayer AuthorityLayer = AuthorityLayer.Skill,
    TargetRule? TargetRuleData = null,
    IReadOnlyList<EffectDescriptor>? EffectDescriptors = null,
    SummonProfile? SummonProfile = null,
    float InterruptRefundScalar = 0.5f,
    string EffectFamilyId = "",
    string MutuallyExclusiveGroupId = "",
    IReadOnlyList<string>? RecruitNativeTags = null,
    IReadOnlyList<string>? RecruitPlanTags = null,
    IReadOnlyList<string>? RecruitScoutTags = null)
{
    public float ResolvedPowerFlat => PowerFlat == 0f ? Power : PowerFlat;

    public ActionSlotKind EffectiveSlotKind => ResolvedSlotKind;

    public bool UsesEnergy => ActivationModel == ActivationModel.Energy;
}

public sealed record StatusApplicationSpec(
    string Id,
    string StatusId,
    float DurationSeconds,
    float Magnitude,
    int MaxStacks = 1,
    bool RefreshDurationOnReapply = true);

[System.Obsolete("Use BattleSkillSpec for compiled battle inputs.")]
public sealed record SkillDefinition(
    string Id,
    string Name,
    SkillKind Kind,
    float Power,
    float Range)
    : BattleSkillSpec(Id, Name, Kind, Power, Range);
