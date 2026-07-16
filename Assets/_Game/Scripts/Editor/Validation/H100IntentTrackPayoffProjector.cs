using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;

namespace SM.Editor.Validation;

/// <summary>battle telemetry/beat를 E03의 observable payoff witness vocabulary로 낮춘다.</summary>
internal static class H100IntentTrackPayoffProjector
{
    public static IReadOnlyList<string> Project(BattleResult result)
    {
        if (result == null) throw new ArgumentNullException(nameof(result));
        var witnesses = new HashSet<string>(StringComparer.Ordinal);
        foreach (var record in result.TelemetryEvents ?? Array.Empty<TelemetryEventRecord>())
        {
            if (record.Actor?.SideIndex != (int)TeamSide.Ally)
            {
                continue;
            }

            var witness = record.EventKind switch
            {
                TelemetryEventKind.DamageApplied => "telemetry.damage_applied",
                TelemetryEventKind.HealingApplied => "telemetry.healing_applied",
                TelemetryEventKind.StatusApplied => "telemetry.status_applied",
                TelemetryEventKind.StatusRemoved => "telemetry.status_removed",
                _ => string.Empty,
            };
            if (!string.IsNullOrWhiteSpace(witness)) witnesses.Add(witness);
        }

        foreach (var beat in result.Beats ?? Array.Empty<CombatBeat>())
        {
            if (beat.Side != TeamSide.Ally)
            {
                continue;
            }

            var witness = beat.Type switch
            {
                CombatBeatType.SynergyActivated => "beat.synergy_activated",
                CombatBeatType.BattleStartEffect => "beat.battle_start_effect",
                CombatBeatType.OnKillEffect => "beat.on_kill_effect",
                CombatBeatType.HpThresholdEffect => "beat.hp_threshold_effect",
                CombatBeatType.AllyDeathEffect => "beat.ally_death_effect",
                CombatBeatType.ComboPrimerApplied => "beat.combo_primer_applied",
                CombatBeatType.ComboConsumed => "beat.combo_consumed",
                _ => string.Empty,
            };
            if (!string.IsNullOrWhiteSpace(witness)) witnesses.Add(witness);
        }

        return witnesses.OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }
}
