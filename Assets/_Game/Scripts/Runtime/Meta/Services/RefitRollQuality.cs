using System;
using System.Collections.Generic;
using System.Linq;
using SM.Meta.Model;

namespace SM.Meta.Services;

/// <summary>
/// Measures and rerolls the numeric roll positions of an item's existing affixes.
/// The BudgetScore-weighted mean is used directly as a percentile approximation;
/// it is not mapped through a compiled distribution of weighted means.
/// </summary>
public static class RefitRollQuality
{
    private const double FloorRoundingMargin = 1e-7d;

    public static double Measure(
        CombatContentSnapshot snapshot,
        IReadOnlyList<string> affixIds,
        IReadOnlyDictionary<string, float>? rolledMagnitudes)
    {
        if (snapshot == null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        var affixCatalog = snapshot.AffixCatalog
                           ?? throw new InvalidOperationException(
                               "Roll quality requires an affix catalog.");
        if (affixIds == null || affixIds.Count == 0)
        {
            throw new ArgumentException(
                "Roll quality requires at least one affix.",
                nameof(affixIds));
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var weightedPosition = 0d;
        var totalWeight = 0d;
        foreach (var affixId in affixIds)
        {
            var affix = ResolveAffix(affixCatalog, affixId, seen);
            var magnitude = ResolveMagnitude(snapshot, affix, rolledMagnitudes);
            var position = ResolvePosition(affix, magnitude);
            weightedPosition += affix.BudgetScore * position;
            totalWeight += affix.BudgetScore;
        }

        if (!double.IsFinite(totalWeight) || totalWeight <= 0d)
        {
            throw new InvalidOperationException(
                "Roll quality requires a positive finite total BudgetScore weight.");
        }

        var quality = weightedPosition / totalWeight;
        if (!double.IsFinite(quality) || quality < 0d || quality > 1d)
        {
            throw new InvalidOperationException(
                $"Roll quality resolved outside [0,1]: {quality:R}.");
        }

        return quality;
    }

    public static IReadOnlyDictionary<string, float> RerollToFloor(
        CombatContentSnapshot snapshot,
        IReadOnlyList<string> affixIds,
        int stableSeed,
        ulong floorQ64)
    {
        if (snapshot == null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        var affixCatalog = snapshot.AffixCatalog
                           ?? throw new InvalidOperationException(
                               "Magnitude reroll requires an affix catalog.");
        if (affixIds == null || affixIds.Count == 0)
        {
            throw new ArgumentException(
                "Magnitude reroll requires at least one affix.",
                nameof(affixIds));
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var components = new List<RollComponent>(affixIds.Count);
        var weightedPosition = 0d;
        var totalWeight = 0d;
        for (var index = 0; index < affixIds.Count; index++)
        {
            var affixId = affixIds[index];
            var affix = ResolveAffix(affixCatalog, affixId, seen);
            var position = affix.ValueMin == affix.ValueMax
                ? 1d
                : AffixMagnitudeRoller.Roll(stableSeed, affixId, index, 0f, 1f);
            components.Add(new RollComponent(affixId, affix, position));
            weightedPosition += affix.BudgetScore * position;
            totalWeight += affix.BudgetScore;
        }

        if (!double.IsFinite(totalWeight) || totalWeight <= 0d)
        {
            throw new InvalidOperationException(
                "Magnitude reroll requires a positive finite total BudgetScore weight.");
        }

        var naturalQuality = weightedPosition / totalWeight;
        var purchasedFloor = FromQ64(floorQ64);
        var liftTarget = Math.Min(1d, purchasedFloor + FloorRoundingMargin);
        var lift = naturalQuality >= liftTarget
            ? 0d
            : (liftTarget - naturalQuality) / (1d - naturalQuality);
        if (!double.IsFinite(lift) || lift < 0d || lift > 1d)
        {
            throw new InvalidOperationException(
                $"Magnitude floor lift resolved outside [0,1]: {lift:R}.");
        }

        var result = new Dictionary<string, float>(affixIds.Count, StringComparer.Ordinal);
        foreach (var component in components)
        {
            var affix = component.Affix;
            var liftedPosition = component.Position + (lift * (1d - component.Position));
            var magnitude = affix.ValueMin == affix.ValueMax
                ? affix.ValueMin
                : (float)(affix.ValueMin
                          + ((affix.ValueMax - affix.ValueMin) * liftedPosition));
            magnitude = Math.Max(affix.ValueMin, Math.Min(affix.ValueMax, magnitude));
            result.Add(component.AffixId, magnitude);
        }

        var measured = Measure(snapshot, affixIds, result);
        if (ToQ64(measured) < floorQ64)
        {
            // Magnitudes persist as float. A nearest-float conversion can land just
            // below the double-space target, so advance each non-degenerate result by
            // at most one representable float step until the exact Q64 floor holds.
            foreach (var component in components)
            {
                var affix = component.Affix;
                if (affix.ValueMin == affix.ValueMax)
                {
                    continue;
                }

                var current = result[component.AffixId];
                var advanced = NextRepresentableUp(current);
                result[component.AffixId] = Math.Min(affix.ValueMax, advanced);
                measured = Measure(snapshot, affixIds, result);
                if (ToQ64(measured) >= floorQ64)
                {
                    break;
                }
            }
        }

        if (ToQ64(measured) < floorQ64)
        {
            throw new InvalidOperationException(
                $"Magnitude reroll landed below its purchased floor: "
                + $"{measured:R} < {purchasedFloor:R}.");
        }

        return result;
    }

    public static IReadOnlyDictionary<string, float> RerollUnlockedToFloor(
        CombatContentSnapshot snapshot,
        IReadOnlyList<string> affixIds,
        IReadOnlyDictionary<string, float>? rolledMagnitudes,
        IReadOnlyCollection<string> sealedAffixIds,
        int stableSeed,
        ulong floorQ64)
    {
        if (sealedAffixIds == null)
        {
            throw new ArgumentNullException(nameof(sealedAffixIds));
        }

        if (sealedAffixIds.Count == 0)
        {
            return RerollToFloor(snapshot, affixIds, stableSeed, floorQ64);
        }

        if (snapshot == null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        var affixCatalog = snapshot.AffixCatalog
                           ?? throw new InvalidOperationException(
                               "Magnitude Seal requires an affix catalog.");
        if (affixIds == null || affixIds.Count == 0)
        {
            throw new ArgumentException(
                "Magnitude Seal requires at least one affix.",
                nameof(affixIds));
        }

        var sealedSet = new HashSet<string>(sealedAffixIds, StringComparer.Ordinal);
        if (sealedSet.Count != sealedAffixIds.Count
            || sealedSet.Count >= affixIds.Count
            || sealedSet.Any(id => !affixIds.Contains(id, StringComparer.Ordinal)))
        {
            throw new ArgumentException(
                "Sealed affixes must be a unique proper subset of the item affixes.",
                nameof(sealedAffixIds));
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var components = new List<SealedRollComponent>(affixIds.Count);
        var weightedPosition = 0d;
        var totalWeight = 0d;
        var availableLiftWeight = 0d;
        for (var index = 0; index < affixIds.Count; index++)
        {
            var affixId = affixIds[index];
            var affix = ResolveAffix(affixCatalog, affixId, seen);
            var isSealed = sealedSet.Contains(affixId);
            var originalMagnitude = isSealed
                ? ResolveMagnitude(snapshot, affix, rolledMagnitudes)
                : 0f;
            var position = isSealed
                ? ResolvePosition(affix, originalMagnitude)
                : affix.ValueMin == affix.ValueMax
                    ? 1d
                    : AffixMagnitudeRoller.Roll(stableSeed, affixId, index, 0f, 1f);
            components.Add(new SealedRollComponent(
                affixId,
                affix,
                position,
                isSealed,
                originalMagnitude));
            weightedPosition += affix.BudgetScore * position;
            totalWeight += affix.BudgetScore;
            if (!isSealed)
            {
                availableLiftWeight += affix.BudgetScore * (1d - position);
            }
        }

        if (!double.IsFinite(totalWeight) || totalWeight <= 0d)
        {
            throw new InvalidOperationException(
                "Magnitude Seal requires a positive finite total BudgetScore weight.");
        }

        var purchasedFloor = FromQ64(floorQ64);
        var liftTarget = Math.Min(1d, purchasedFloor + FloorRoundingMargin);
        var requiredLiftWeight = Math.Max(
            0d,
            (liftTarget * totalWeight) - weightedPosition);
        if (requiredLiftWeight > availableLiftWeight + 1e-12d)
        {
            throw new InvalidOperationException(
                "The selected sealed affixes make the purchased floor unreachable.");
        }

        var lift = requiredLiftWeight <= 0d
            ? 0d
            : requiredLiftWeight / availableLiftWeight;
        if (!double.IsFinite(lift) || lift < 0d || lift > 1d + 1e-12d)
        {
            throw new InvalidOperationException(
                $"Magnitude Seal lift resolved outside [0,1]: {lift:R}.");
        }

        lift = Math.Clamp(lift, 0d, 1d);
        var result = new Dictionary<string, float>(affixIds.Count, StringComparer.Ordinal);
        foreach (var component in components)
        {
            if (component.IsSealed)
            {
                result.Add(component.AffixId, component.OriginalMagnitude);
                continue;
            }

            var affix = component.Affix;
            var liftedPosition = component.Position + (lift * (1d - component.Position));
            var magnitude = affix.ValueMin == affix.ValueMax
                ? affix.ValueMin
                : (float)(affix.ValueMin
                          + ((affix.ValueMax - affix.ValueMin) * liftedPosition));
            result.Add(
                component.AffixId,
                Math.Max(affix.ValueMin, Math.Min(affix.ValueMax, magnitude)));
        }

        var measured = Measure(snapshot, affixIds, result);
        if (ToQ64(measured) < floorQ64)
        {
            foreach (var component in components)
            {
                if (component.IsSealed
                    || component.Affix.ValueMin == component.Affix.ValueMax)
                {
                    continue;
                }

                var current = result[component.AffixId];
                result[component.AffixId] = Math.Min(
                    component.Affix.ValueMax,
                    NextRepresentableUp(current));
                measured = Measure(snapshot, affixIds, result);
                if (ToQ64(measured) >= floorQ64)
                {
                    break;
                }
            }
        }

        if (ToQ64(measured) < floorQ64)
        {
            throw new InvalidOperationException(
                $"Magnitude Seal landed below its purchased floor: "
                + $"{measured:R} < {purchasedFloor:R}.");
        }

        return result;
    }

    public static ulong ToQ64(double quality)
    {
        if (!double.IsFinite(quality) || quality < 0d || quality > 1d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quality),
                quality,
                "Roll quality must be finite and inside [0,1].");
        }

        if (quality <= 0d)
        {
            return 0UL;
        }

        if (quality >= 1d)
        {
            return ulong.MaxValue;
        }

        return (ulong)decimal.Floor((decimal)quality * ulong.MaxValue);
    }

    public static double FromQ64(ulong qualityQ64)
        => qualityQ64 / (double)ulong.MaxValue;

    private static AffixTemplate ResolveAffix(
        IReadOnlyDictionary<string, AffixTemplate> affixCatalog,
        string affixId,
        ISet<string> seen)
    {
        if (string.IsNullOrWhiteSpace(affixId)
            || !seen.Add(affixId)
            || !affixCatalog.TryGetValue(affixId, out var affix))
        {
            throw new InvalidOperationException(
                $"Roll quality found an unknown or duplicate affix '{affixId}'.");
        }

        if (!float.IsFinite(affix.BudgetScore) || affix.BudgetScore <= 0f)
        {
            throw new InvalidOperationException(
                $"Affix '{affixId}' has non-positive or non-finite BudgetScore.");
        }

        if (!float.IsFinite(affix.ValueMin)
            || !float.IsFinite(affix.ValueMax)
            || affix.ValueMin > affix.ValueMax)
        {
            throw new InvalidOperationException(
                $"Affix '{affixId}' has an invalid authored magnitude range "
                + $"[{affix.ValueMin:R},{affix.ValueMax:R}].");
        }

        return affix;
    }

    private static float ResolveMagnitude(
        CombatContentSnapshot snapshot,
        AffixTemplate affix,
        IReadOnlyDictionary<string, float>? rolledMagnitudes)
    {
        float magnitude;
        if (rolledMagnitudes != null
            && rolledMagnitudes.TryGetValue(affix.Id, out var persisted))
        {
            magnitude = persisted;
        }
        else if (snapshot.AffixPackages.TryGetValue(
                     affix.Id,
                     out var sharedPackage)
                 && sharedPackage.Modifiers is { Count: > 0 })
        {
            // Legacy saves have no instance roll. Their effective value is the shared
            // definition package's first modifier, so measure that exact fallback.
            magnitude = sharedPackage.Modifiers[0].Value;
        }
        else
        {
            throw new InvalidOperationException(
                $"Affix '{affix.Id}' has neither a persisted magnitude nor a legacy package baseline.");
        }

        if (!float.IsFinite(magnitude)
            || magnitude < affix.ValueMin
            || magnitude > affix.ValueMax)
        {
            throw new InvalidOperationException(
                $"Affix '{affix.Id}' magnitude {magnitude:R} is outside its authored range "
                + $"[{affix.ValueMin:R},{affix.ValueMax:R}].");
        }

        return magnitude;
    }

    private static double ResolvePosition(AffixTemplate affix, float magnitude)
    {
        if (affix.ValueMin == affix.ValueMax)
        {
            // A fixed affix has no unlucky attainable roll. Treat it as fully satisfied
            // so an impossible range cannot hold the whole item below a purchased floor.
            return 1d;
        }

        return Math.Clamp(
            (magnitude - (double)affix.ValueMin)
            / (affix.ValueMax - (double)affix.ValueMin),
            0d,
            1d);
    }

    private static float NextRepresentableUp(float value)
    {
        if (!float.IsFinite(value))
        {
            return value;
        }

        if (value == 0f)
        {
            return float.Epsilon;
        }

        var bits = BitConverter.SingleToInt32Bits(value);
        return BitConverter.Int32BitsToSingle(value > 0f ? bits + 1 : bits - 1);
    }

    private sealed record RollComponent(
        string AffixId,
        AffixTemplate Affix,
        double Position);

    private sealed record SealedRollComponent(
        string AffixId,
        AffixTemplate Affix,
        double Position,
        bool IsSealed,
        float OriginalMagnitude);
}
