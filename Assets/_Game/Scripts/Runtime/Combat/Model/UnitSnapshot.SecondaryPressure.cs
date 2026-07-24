using System;
using System.Collections.Generic;
using System.Linq;
using SM.Core.Numerics;
using SM.Core.Stats;

namespace SM.Combat.Model;

public sealed partial class UnitSnapshot
{
    private StatBlock? _secondaryPressureBaselineStats;

    public Fixed32 SecondaryPressureFraction { get; private set; } = Fixed32.Zero;

    private void InitializeSecondaryPressure(BattleUnitLoadout definition)
    {
        var pressurePackages = (definition.RulePackages ?? Array.Empty<CombatRuleModifierPackage>())
            .Where(package => package.Modifiers.Any(modifier =>
                modifier.Kind == RuleModifierKind.SecondaryPressure))
            .OrderBy(package => package.SourceId, StringComparer.Ordinal)
            .ToArray();
        if (pressurePackages.Length == 0)
        {
            return;
        }

        var fractionRaw = 0;
        foreach (var package in pressurePackages)
        {
            foreach (var modifier in package.Modifiers)
            {
                if (modifier.Kind != RuleModifierKind.SecondaryPressure
                    || !float.IsFinite(modifier.Magnitude)
                    || modifier.Magnitude <= 0f)
                {
                    continue;
                }

                fractionRaw += Fixed32.FromFloatQuantized(modifier.Magnitude).Raw;
            }
        }

        if (fractionRaw <= 0)
        {
            return;
        }

        SecondaryPressureFraction = Fixed32.FromRaw(fractionRaw);
        var pressureSourceIds = new HashSet<string>(
            pressurePackages.Select(package => package.SourceId),
            StringComparer.Ordinal);
        var baselineModifiers = definition.NumericPackages
            .Where(package => !pressureSourceIds.Contains(package.SourceId))
            .SelectMany(package => package.Modifiers)
            .ToList();
        _secondaryPressureBaselineStats = new StatBlock(
            new Dictionary<StatKey, float>(definition.BaseStats),
            baselineModifiers);
    }

    internal Hp64 GetSecondaryPressureBaselineStat(StatKey key)
        => (_secondaryPressureBaselineStats ?? Stats).GetWide(key);
}
