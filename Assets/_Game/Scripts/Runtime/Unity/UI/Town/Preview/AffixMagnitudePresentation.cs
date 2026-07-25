using System;
using System.Globalization;
using System.Linq;
using SM.Content.Definitions;
using SM.Meta.Services;
using SM.Persistence.Abstractions.Models;

namespace SM.Unity.UI.Town.Preview;

public static class AffixMagnitudePresentation
{
    public static float Resolve(InventoryItemRecord item, AffixDefinition definition)
    {
        var persisted = item.AffixMagnitudeRolls?
            .FirstOrDefault(roll =>
                roll != null
                && string.Equals(roll.AffixId, definition.Id, StringComparison.Ordinal)
                && !float.IsNaN(roll.Magnitude)
                && !float.IsInfinity(roll.Magnitude));
        if (persisted != null)
        {
            return persisted.Magnitude;
        }

        // Legacy saves intentionally retain the fixed definition package magnitude.
        return definition.Modifiers?.FirstOrDefault()?.Value
               ?? (float)AffixMagnitudeRoller.ExpectedMagnitude(definition.ValueMin, definition.ValueMax);
    }

    public static string Format(float magnitude, float valueMin, float valueMax)
    {
        var minimum = Math.Min(valueMin, valueMax);
        var maximum = Math.Max(valueMin, valueMax);
        var percentile = maximum > minimum
            ? Math.Max(0d, Math.Min(1d, (magnitude - minimum) / (maximum - minimum)))
            : 1d;

        return string.Format(
            CultureInfo.InvariantCulture,
            "{0:0.###} · {1:0}% [{2:0.###} ~ {3:0.###}]",
            magnitude,
            percentile * 100d,
            minimum,
            maximum);
    }
}
