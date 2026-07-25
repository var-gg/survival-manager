using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Core.Stats;

namespace SM.Meta.Services;

/// <summary>
/// Materializes an item-instance package without modifying the shared definition package.
/// A missing magnitude deliberately returns the shared package to preserve legacy save behavior.
/// </summary>
public static class AffixMagnitudePackageResolver
{
    public static CombatModifierPackage Resolve(
        CombatModifierPackage sharedPackage,
        float? rolledMagnitude)
    {
        if (sharedPackage == null)
        {
            throw new ArgumentNullException(nameof(sharedPackage));
        }

        if (!rolledMagnitude.HasValue
            || float.IsNaN(rolledMagnitude.Value)
            || float.IsInfinity(rolledMagnitude.Value)
            || sharedPackage.Modifiers.Count == 0)
        {
            return sharedPackage;
        }

        var magnitude = rolledMagnitude.Value;
        var baseline = sharedPackage.Modifiers[0].Value;
        IReadOnlyList<StatModifier> modifiers;
        if (baseline != 0f)
        {
            var scale = magnitude / baseline;
            modifiers = sharedPackage.Modifiers
                .Select(modifier => modifier with { Value = modifier.Value * scale })
                .ToArray();
        }
        else
        {
            modifiers = sharedPackage.Modifiers
                .Select((modifier, index) => index == 0
                    ? modifier with { Value = magnitude }
                    : modifier)
                .ToArray();
        }

        return new CombatModifierPackage(
            sharedPackage.SourceId,
            sharedPackage.Source,
            modifiers,
            sharedPackage.GrantedTeamRuleId);
    }

    public static IReadOnlyList<CombatTriggeredEffect> ResolveTriggeredEffects(
        IReadOnlyList<CombatTriggeredEffect>? sharedEffects,
        float? rolledMagnitude)
    {
        if (sharedEffects == null || sharedEffects.Count == 0)
        {
            return Array.Empty<CombatTriggeredEffect>();
        }

        if (!rolledMagnitude.HasValue
            || float.IsNaN(rolledMagnitude.Value)
            || float.IsInfinity(rolledMagnitude.Value))
        {
            return sharedEffects;
        }

        var magnitude = rolledMagnitude.Value;
        var baseline = sharedEffects[0].Magnitude;
        if (baseline != 0f)
        {
            var scale = magnitude / baseline;
            return sharedEffects
                .Select(effect => effect with { Magnitude = effect.Magnitude * scale })
                .ToArray();
        }

        return sharedEffects
            .Select((effect, index) => index == 0
                ? effect with { Magnitude = magnitude }
                : effect)
            .ToArray();
    }

    public static float? Find(
        IReadOnlyDictionary<string, float>? magnitudes,
        string affixId)
    {
        if (magnitudes == null
            || string.IsNullOrWhiteSpace(affixId)
            || !magnitudes.TryGetValue(affixId, out var magnitude)
            || float.IsNaN(magnitude)
            || float.IsInfinity(magnitude))
        {
            return null;
        }

        return magnitude;
    }
}
