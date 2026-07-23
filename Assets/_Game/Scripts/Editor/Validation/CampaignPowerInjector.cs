using System;
using System.Globalization;
using System.Linq;
using SM.Combat.Model;
using SM.Core.Stats;

namespace SM.Editor.Validation;

/// <summary>
/// Signed clear-deficit 계측 전용 단조 power injector.
/// log-power x를 HP와 물리/마법 공격에 각각 exp(x/2) 배율로 적용한다.
/// </summary>
internal static class CampaignPowerInjector
{
    internal const string SourceId = "campaign_signed_deficit_injector";

    internal static BattleLoadoutSnapshot Apply(BattleLoadoutSnapshot snapshot, double logPower)
    {
        if (snapshot == null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        var factor = Math.Exp(logPower / 2d);
        if (!double.IsFinite(factor) || factor <= 0d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(logPower),
                logPower,
                "Power injector requires a finite positive multiplier.");
        }

        if (logPower == 0d)
        {
            return snapshot;
        }

        var more = checked((float)(factor - 1d));
        var package = new CombatModifierPackage(
            SourceId,
            ModifierSource.Other,
            new[]
            {
                new StatModifier(StatKey.MaxHealth, ModifierOp.More, more, ModifierSource.Other, SourceId),
                new StatModifier(StatKey.PhysPower, ModifierOp.More, more, ModifierSource.Other, SourceId),
                new StatModifier(StatKey.MagPower, ModifierOp.More, more, ModifierSource.Other, SourceId),
            });
        var allies = snapshot.Allies
            .Select(unit => unit with
            {
                Packages = (unit.Packages ?? Array.Empty<CombatModifierPackage>())
                    .Concat(new[] { package })
                    .ToArray(),
            })
            .ToArray();
        return snapshot with
        {
            CompileHash = $"{snapshot.CompileHash}|power:{logPower.ToString("R", CultureInfo.InvariantCulture)}",
            Allies = allies,
        };
    }
}
