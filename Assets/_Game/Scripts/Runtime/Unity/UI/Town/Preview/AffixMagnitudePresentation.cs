using System;
using System.Collections.Generic;
using System.Linq;
using SM.Content.Definitions;
using SM.Core.Stats;
using SM.Meta.Services;
using SM.Persistence.Abstractions.Models;

namespace SM.Unity.UI.Town.Preview;

internal sealed record AffixMagnitudeEffectReadout(
    string StatId,
    ModifierOp Op,
    float Value);

internal sealed record AffixMagnitudeReadout(
    float Magnitude,
    bool HasPersistedRoll,
    double RollPosition,
    IReadOnlyList<AffixMagnitudeEffectReadout> Effects);

public static class AffixMagnitudePresentation
{
    internal delegate string Localizer(
        string key,
        string fallback,
        params object[] arguments);

    public static float Resolve(InventoryItemRecord item, AffixDefinition definition)
        => Build(item, definition).Magnitude;

    internal static AffixMagnitudeReadout Build(
        InventoryItemRecord item,
        AffixDefinition definition)
    {
        if (item == null) throw new ArgumentNullException(nameof(item));
        if (definition == null) throw new ArgumentNullException(nameof(definition));

        var persisted = item.AffixMagnitudeRolls?
            .FirstOrDefault(roll =>
                roll != null
                && string.Equals(roll.AffixId, definition.Id, StringComparison.Ordinal)
                && !float.IsNaN(roll.Magnitude)
                && !float.IsInfinity(roll.Magnitude));
        var authoredModifiers = definition.Modifiers?
            .Where(modifier => modifier != null)
            .ToArray()
            ?? Array.Empty<SerializableStatModifier>();
        var magnitude = persisted?.Magnitude
                        ?? authoredModifiers.FirstOrDefault()?.Value
                        ?? (float)AffixMagnitudeRoller.ExpectedMagnitude(
                            definition.ValueMin,
                            definition.ValueMax);
        var effects = ResolveEffects(authoredModifiers, magnitude);

        return new AffixMagnitudeReadout(
            Magnitude: magnitude,
            HasPersistedRoll: persisted != null,
            RollPosition: ResolveRollPosition(
                magnitude,
                definition.ValueMin,
                definition.ValueMax),
            Effects: effects);
    }

    internal static double ResolveRollPosition(
        float magnitude,
        float valueMin,
        float valueMax)
    {
        var minimum = Math.Min(valueMin, valueMax);
        var maximum = Math.Max(valueMin, valueMax);
        return maximum > minimum
            ? Math.Max(0d, Math.Min(1d, (magnitude - minimum) / (maximum - minimum)))
            : 1d;
    }

    internal static string FormatEffect(
        AffixMagnitudeEffectReadout effect,
        Func<string, string> statName,
        Localizer localize)
    {
        if (effect == null) throw new ArgumentNullException(nameof(effect));
        if (statName == null) throw new ArgumentNullException(nameof(statName));
        if (localize == null) throw new ArgumentNullException(nameof(localize));

        var localizedStatName = statName(effect.StatId);
        return effect.Op switch
        {
            ModifierOp.Increased or ModifierOp.More => localize(
                "ui.town.refit.affix.effect.percent",
                "{0} {1:+0.###;-0.###;0}%",
                localizedStatName,
                effect.Value * 100d),
            ModifierOp.ClampMin => localize(
                "ui.town.refit.affix.effect.clamp_min",
                "{0} min {1:+0.###;-0.###;0}",
                localizedStatName,
                effect.Value),
            ModifierOp.ClampMax => localize(
                "ui.town.refit.affix.effect.clamp_max",
                "{0} max {1:+0.###;-0.###;0}",
                localizedStatName,
                effect.Value),
            _ => localize(
                "ui.town.refit.affix.effect.flat",
                "{0} {1:+0.###;-0.###;0}",
                localizedStatName,
                effect.Value),
        };
    }

    internal static string FormatRollContext(
        AffixMagnitudeReadout readout,
        Localizer localize)
    {
        if (readout == null) throw new ArgumentNullException(nameof(readout));
        if (localize == null) throw new ArgumentNullException(nameof(localize));

        return readout.HasPersistedRoll
            ? localize(
                "ui.town.refit.affix.roll_quality",
                "Roll quality {0:0}%",
                readout.RollPosition * 100d)
            : localize(
                "ui.town.refit.affix.legacy_baseline",
                "Legacy baseline");
    }

    private static IReadOnlyList<AffixMagnitudeEffectReadout> ResolveEffects(
        IReadOnlyList<SerializableStatModifier> authoredModifiers,
        float magnitude)
    {
        if (authoredModifiers.Count == 0)
        {
            return Array.Empty<AffixMagnitudeEffectReadout>();
        }

        var baseline = authoredModifiers[0].Value;
        var effects = new AffixMagnitudeEffectReadout[authoredModifiers.Count];
        for (var index = 0; index < authoredModifiers.Count; index++)
        {
            var modifier = authoredModifiers[index];
            var effectiveValue = baseline != 0f
                ? modifier.Value * (magnitude / baseline)
                : index == 0
                    ? magnitude
                    : modifier.Value;
            effects[index] = new AffixMagnitudeEffectReadout(
                modifier.StatId,
                modifier.Op,
                effectiveValue);
        }

        return effects;
    }
}
