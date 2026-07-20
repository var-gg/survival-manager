using System;
using System.Linq;
using SM.Combat.Model;

namespace SM.Meta.Services;

/// <summary>
/// 컴파일 타임 skill-effect 변환의 단일 구현. sim은 변환된 BattleSkillSpec만 소비한다.
/// </summary>
internal static class BattleSkillEffectModifier
{
    internal static BattleSkillSpec Apply(
        BattleSkillSpec skill,
        BattleSupportModifierSpec modifier,
        Func<StatusApplicationSpec, bool>? durationFilter = null,
        float coefficientMultiplier = 1f)
    {
        var statuses = (skill.AppliedStatuses ?? Array.Empty<StatusApplicationSpec>()).ToList();
        if (Math.Abs(modifier.StatusDurationMultiplier - 1f) > 0.0001f)
        {
            statuses = statuses
                .Select(status => durationFilter == null || durationFilter(status)
                    ? status with { DurationSeconds = status.DurationSeconds * modifier.StatusDurationMultiplier }
                    : status)
                .ToList();
        }

        if (modifier.AddedStatuses is { Count: > 0 })
        {
            statuses.AddRange(modifier.AddedStatuses);
        }

        var coefficients = ResolveScaledCoefficients(skill, coefficientMultiplier);
        return skill with
        {
            Power = skill.Power * modifier.PowerMultiplier,
            PowerFlat = skill.PowerFlat * modifier.PowerMultiplier,
            PhysCoeff = coefficients.Phys,
            MagCoeff = coefficients.Mag,
            HealCoeff = coefficients.Heal,
            HealthCoeff = coefficients.Health,
            BaseCooldownSeconds = skill.BaseCooldownSeconds * modifier.CooldownMultiplier,
            CastWindupSeconds = skill.CastWindupSeconds * modifier.CastWindupMultiplier,
            Range = skill.Range + modifier.RangeBonus,
            CanCrit = skill.CanCrit || modifier.ForceCanCrit,
            AppliedStatuses = statuses.Count > 0 ? statuses : skill.AppliedStatuses,
            CleanseProfileId = string.IsNullOrWhiteSpace(skill.CleanseProfileId)
                               && !string.IsNullOrWhiteSpace(modifier.GrantCleanseProfileId)
                ? modifier.GrantCleanseProfileId
                : skill.CleanseProfileId,
        };
    }

    private static (float Phys, float Mag, float Heal, float Health) ResolveScaledCoefficients(
        BattleSkillSpec skill,
        float multiplier)
    {
        if (Math.Abs(multiplier - 1f) <= 0.0001f)
        {
            return (skill.PhysCoeff, skill.MagCoeff, skill.HealCoeff, skill.HealthCoeff);
        }

        var usesAuthoredCoefficients = !Approximately(skill.PhysCoeff, 1f)
                                       || !Approximately(skill.MagCoeff, 0f)
                                       || !Approximately(skill.HealCoeff, 0f)
                                       || !Approximately(skill.HealthCoeff, 0f);
        if (usesAuthoredCoefficients)
        {
            return (
                skill.PhysCoeff * multiplier,
                skill.MagCoeff * multiplier,
                skill.HealCoeff * multiplier,
                skill.HealthCoeff * multiplier);
        }

        // HitResolutionService의 legacy coefficient fallback을 명시 coefficient로 정규화한 뒤 배수를 적용한다.
        if (skill.Kind is SkillKind.Heal or SkillKind.Shield || skill.DamageType == DamageType.Healing)
        {
            return (0f, 0f, multiplier, 0f);
        }

        return skill.DamageType == DamageType.Magical
            ? (0f, multiplier, 0f, 0f)
            : (multiplier, 0f, 0f, 0f);
    }

    private static bool Approximately(float left, float right) => Math.Abs(left - right) <= 0.0001f;
}
