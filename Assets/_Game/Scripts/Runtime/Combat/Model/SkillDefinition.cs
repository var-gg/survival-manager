using System.Collections.Generic;

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
        return IsSupported(slotKind) ? slotKind! : fallback;
    }
}

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
    IReadOnlyList<string>? RequiredWeaponTags = null,
    IReadOnlyList<string>? RequiredClassTags = null)
{
    public float ResolvedPowerFlat => PowerFlat == 0f ? Power : PowerFlat;
}

[System.Obsolete("Use BattleSkillSpec for compiled battle inputs.")]
public sealed record SkillDefinition(
    string Id,
    string Name,
    SkillKind Kind,
    float Power,
    float Range)
    : BattleSkillSpec(Id, Name, Kind, Power, Range);
