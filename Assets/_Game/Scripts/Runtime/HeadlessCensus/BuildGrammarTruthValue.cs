using System.Globalization;
using SM.Combat.Model;
using SM.Core.Stats;

namespace SM.HeadlessCensus;

/// <summary>truth와 visible projection이 공유하는 invariant 구조값 포맷.</summary>
public static class BuildGrammarTruthValue
{
    public static string Status(StatusApplicationSpec status)
        => Status(status.DurationSeconds, status.Magnitude, status.MaxStacks);

    public static string Status(float durationSeconds, float magnitude, int maxStacks)
        => $"duration_seconds={Number(durationSeconds)};magnitude={Number(magnitude)};max_stacks={maxStacks.ToString(CultureInfo.InvariantCulture)}";

    public static string Modifier(StatModifier modifier)
        => Modifier(modifier.Stat.ToString(), modifier.Op.ToString(), modifier.Value, modifier.Tag?.Value ?? string.Empty);

    public static string Modifier(string statId, string operation, float value, string tagId)
        => $"stat={statId};operation={operation};value={Number(value)};tag={tagId ?? string.Empty}";

    public static string RuleModifier(RuleModifier modifier)
        => RuleModifier(modifier.Kind.ToString(), modifier.Value, modifier.Magnitude);

    public static string RuleModifier(string kind, string value, float magnitude)
        => $"kind={kind};value={value ?? string.Empty};magnitude={Number(magnitude)}";

    public static string Trigger(CombatTriggeredEffect effect)
        => Trigger(
            effect.Trigger.ToString(),
            effect.Op.ToString(),
            effect.Scope.ToString(),
            effect.Magnitude,
            effect.ThresholdRatio,
            effect.DurationSeconds,
            effect.MaxStacks);

    public static string Trigger(
        string trigger,
        string operation,
        string scope,
        float magnitude,
        float thresholdRatio,
        float durationSeconds,
        int maxStacks)
        => $"trigger={trigger};operation={operation};scope={scope};magnitude={Number(magnitude)};"
           + $"threshold_ratio={Number(thresholdRatio)};duration_seconds={Number(durationSeconds)};"
           + $"max_stacks={maxStacks.ToString(CultureInfo.InvariantCulture)}";

    public static string SkillPayoff(BattleSkillSpec skill)
        => SkillPayoff(
            skill.ResolvedPowerFlat,
            skill.PhysCoeff,
            skill.MagCoeff,
            skill.HealCoeff,
            skill.HealthCoeff,
            skill.CanCrit);

    public static string SkillPayoff(
        float powerFlat,
        float physicalCoefficient,
        float magicalCoefficient,
        float healingCoefficient,
        float healthCoefficient,
        bool canCrit)
        => $"power_flat={Number(powerFlat)};physical_coefficient={Number(physicalCoefficient)};"
           + $"magical_coefficient={Number(magicalCoefficient)};healing_coefficient={Number(healingCoefficient)};"
           + $"health_coefficient={Number(healthCoefficient)};can_crit={(canCrit ? "true" : "false")}";

    public static string Threshold(int threshold)
        => $"threshold={threshold.ToString(CultureInfo.InvariantCulture)}";

    private static string Number(float value) => value.ToString("R", CultureInfo.InvariantCulture);
}
