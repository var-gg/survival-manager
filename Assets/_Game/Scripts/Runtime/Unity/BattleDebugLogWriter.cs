using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Stats;
using UnityEngine;

namespace SM.Unity;

public static class BattleDebugLogWriter
{
    private const string LogDirectory = "Logs";
    private const string LogFileName = "last_battle_replay.json";

    public static void Write(BattleReplayBundle replay, IReadOnlyList<BattleUnitReadModel>? finalUnitStates = null)
    {
        try
        {
            var path = ResolvePath();
            var json = BuildJson(replay, finalUnitStates);
            File.WriteAllText(path, json, Encoding.UTF8);
            Debug.Log($"[BattleDebugLog] Written to {path} ({json.Length:N0} chars)");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[BattleDebugLog] Failed to write: {ex.Message}");
        }
    }

    private static string ResolvePath()
    {
        var dir = Path.Combine(Application.dataPath, "..", LogDirectory);
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        return Path.GetFullPath(Path.Combine(dir, LogFileName));
    }

    private static string BuildJson(BattleReplayBundle replay, IReadOnlyList<BattleUnitReadModel>? finalUnitStates)
    {
        var sb = new StringBuilder(8192);
        sb.AppendLine("{");

        WriteMeta(sb, replay);
        sb.AppendLine(",");

        WriteInitialUnits(sb, replay.Input);
        sb.AppendLine(",");

        WriteEvents(sb, replay.EventStream);
        sb.AppendLine(",");

        WriteFinalUnits(sb, replay, finalUnitStates);
        sb.AppendLine(",");

        WriteDiagnostics(sb, replay, finalUnitStates);
        sb.AppendLine(",");

        WriteStats(sb, replay);
        sb.AppendLine();

        sb.Append('}');
        return sb.ToString();
    }

    private static void WriteMeta(StringBuilder sb, BattleReplayBundle replay)
    {
        var h = replay.Header;
        var summary = replay.BattleSummary;
        sb.AppendLine("  \"meta\": {");
        sb.AppendLine($"    \"matchId\": \"{Escape(h.MatchId)}\",");
        sb.AppendLine($"    \"seed\": {h.Seed},");
        sb.AppendLine($"    \"simVersion\": \"{Escape(h.SimVersion)}\",");
        sb.AppendLine($"    \"winner\": \"{h.Winner}\",");
        sb.AppendLine($"    \"startedAt\": \"{Escape(h.StartedAtUtc)}\",");
        sb.AppendLine($"    \"completedAt\": \"{Escape(h.CompletedAtUtc)}\",");
        sb.AppendLine($"    \"totalEvents\": {replay.EventStream.Count},");
        if (summary != null)
        {
            sb.AppendLine($"    \"durationSeconds\": {summary.BattleDurationSeconds:0.###},");
            sb.AppendLine($"    \"timeToFirstDamage\": {summary.TimeToFirstDamageSeconds:0.###},");
            sb.AppendLine($"    \"timeout\": {(summary.TimeoutOccurred ? "true" : "false")},");
            sb.AppendLine($"    \"overkillRatio\": {summary.OverkillRatio:0.###},");
            sb.AppendLine($"    \"topDamageShare\": {summary.TopDamageShare:0.###},");
        }

        sb.AppendLine($"    \"allyPosture\": \"{replay.Input.TeamPosture}\",");
        sb.AppendLine($"    \"allyCount\": {replay.Input.Allies.Count},");
        sb.Append($"    \"enemyCount\": {replay.Input.Enemies.Count}");
        sb.AppendLine();
        sb.Append("  }");
    }

    private static void WriteInitialUnits(StringBuilder sb, BattleInputSnapshot input)
    {
        sb.AppendLine("  \"initialUnits\": [");
        var all = input.Allies.Select(u => (u, "Ally"))
            .Concat(input.Enemies.Select(u => (u, "Enemy")))
            .ToList();
        for (var i = 0; i < all.Count; i++)
        {
            var (unit, side) = all[i];
            sb.AppendLine("    {");
            sb.AppendLine($"      \"id\": \"{Escape(unit.Id)}\",");
            sb.AppendLine($"      \"name\": \"{Escape(unit.Name)}\",");
            sb.AppendLine($"      \"side\": \"{side}\",");
            sb.AppendLine($"      \"classId\": \"{Escape(unit.ClassId)}\",");
            sb.AppendLine($"      \"anchor\": \"{unit.PreferredAnchor}\",");
            WriteStatLine(sb, "hp", unit.BaseStats, StatKey.MaxHealth);
            WriteStatLine(sb, "physPower", unit.BaseStats, StatKey.PhysPower);
            WriteStatLine(sb, "armor", unit.BaseStats, StatKey.Armor);
            WriteStatLine(sb, "attackSpeed", unit.BaseStats, StatKey.AttackSpeed);
            WriteStatLine(sb, "attackCooldown", unit.BaseStats, StatKey.AttackCooldown);
            WriteStatLine(sb, "attackRange", unit.BaseStats, StatKey.AttackRange);
            WriteStatLine(sb, "moveSpeed", unit.BaseStats, StatKey.MoveSpeed);
            WriteStatLine(sb, "healPower", unit.BaseStats, StatKey.HealPower);
            WriteStatLine(sb, "leashDistance", unit.BaseStats, StatKey.LeashDistance);
            sb.AppendLine($"      \"attackWindup\": {GetStat(unit.BaseStats, StatKey.AttackWindup):0.###}");
            sb.Append("    }");
            if (i < all.Count - 1)
            {
                sb.Append(',');
            }

            sb.AppendLine();
        }

        sb.Append("  ]");
    }

    private static void WriteEvents(StringBuilder sb, IReadOnlyList<BattleEvent> events)
    {
        sb.AppendLine("  \"events\": [");
        for (var i = 0; i < events.Count; i++)
        {
            var evt = events[i];
            sb.Append("    { ");
            sb.Append($"\"step\": {evt.StepIndex}, ");
            sb.Append($"\"time\": {evt.TimeSeconds:0.##}, ");
            sb.Append($"\"actor\": \"{Escape(evt.ActorId.Value)}\", ");
            sb.Append($"\"action\": \"{evt.ActionType}\", ");
            sb.Append($"\"code\": \"{evt.LogCode}\", ");
            if (evt.TargetId != null)
            {
                sb.Append($"\"target\": \"{Escape(evt.TargetId.Value.Value)}\", ");
            }

            sb.Append($"\"value\": {evt.Value:0.###}");
            if (evt.EventKind != BattleEventKind.Action)
            {
                sb.Append($", \"kind\": \"{evt.EventKind}\"");
            }

            if (!string.IsNullOrEmpty(evt.Note))
            {
                sb.Append($", \"note\": \"{Escape(evt.Note)}\"");
            }

            sb.Append(" }");
            if (i < events.Count - 1)
            {
                sb.Append(',');
            }

            sb.AppendLine();
        }

        sb.Append("  ]");
    }

    private static void WriteFinalUnits(StringBuilder sb, BattleReplayBundle replay, IReadOnlyList<BattleUnitReadModel>? finalUnitStates)
    {
        sb.AppendLine("  \"finalUnits\": [");
        if (finalUnitStates is { Count: > 0 })
        {
            for (var i = 0; i < finalUnitStates.Count; i++)
            {
                var u = finalUnitStates[i];
                sb.Append($"    {{ \"id\": \"{Escape(u.Id)}\", \"name\": \"{Escape(u.Name)}\", \"side\": \"{u.Side}\", \"alive\": {(u.IsAlive ? "true" : "false")}, \"hp\": {u.CurrentHealth:0.#}, \"maxHp\": {u.MaxHealth:0.#}, \"barrier\": {u.Barrier:0.#}, \"state\": \"{u.ActionState}\", \"pendingAction\": \"{u.PendingActionType?.ToString() ?? string.Empty}\", \"target\": \"{Escape(u.TargetId ?? string.Empty)}\", \"x\": {u.Position.X:0.###}, \"y\": {u.Position.Y:0.###} }}");
                if (i < finalUnitStates.Count - 1)
                {
                    sb.Append(',');
                }

                sb.AppendLine();
            }
        }
        else
        {
            var fallback = replay.EventStream.Count > 0
                ? ExtractFinalUnitsFromEvents(replay)
                : Array.Empty<FinalUnitEntry>();
            for (var i = 0; i < fallback.Length; i++)
            {
                var u = fallback[i];
                sb.Append($"    {{ \"id\": \"{Escape(u.Id)}\", \"side\": \"{u.Side}\", \"alive\": {(u.Alive ? "true" : "false")} }}");
                if (i < fallback.Length - 1)
                {
                    sb.Append(',');
                }

                sb.AppendLine();
            }
        }

        sb.Append("  ]");
    }

    private static void WriteDiagnostics(StringBuilder sb, BattleReplayBundle replay, IReadOnlyList<BattleUnitReadModel>? finalUnitStates)
    {
        var events = replay.EventStream;
        var lastEvent = events.Count > 0
            ? events.OrderBy(e => e.StepIndex).ThenBy(e => e.TimeSeconds).Last()
            : null;
        var estimatedFinalStep = replay.BattleSummary != null
            ? (int)MathF.Round(replay.BattleSummary.BattleDurationSeconds / BattleSimulator.DefaultFixedStepSeconds)
            : -1;
        var lastEventStep = lastEvent?.StepIndex ?? -1;
        var idleStepsAfterLastEvent = estimatedFinalStep >= 0 && lastEventStep >= 0
            ? Math.Max(0, estimatedFinalStep - lastEventStep)
            : -1;
        var idleSecondsAfterLastEvent = replay.BattleSummary != null && lastEvent != null
            ? Math.Max(0f, replay.BattleSummary.BattleDurationSeconds - lastEvent.TimeSeconds)
            : -1f;
        var aliveAllies = finalUnitStates?.Count(u => u.Side == TeamSide.Ally && u.IsAlive) ?? 0;
        var aliveEnemies = finalUnitStates?.Count(u => u.Side == TeamSide.Enemy && u.IsAlive) ?? 0;
        var timeoutWithLivingBothSides = replay.BattleSummary?.TimeoutOccurred == true && aliveAllies > 0 && aliveEnemies > 0;
        var stallSuspected = timeoutWithLivingBothSides && idleSecondsAfterLastEvent >= 3f;

        sb.AppendLine("  \"diagnostics\": {");
        sb.AppendLine($"    \"estimatedFinalStep\": {estimatedFinalStep},");
        sb.AppendLine($"    \"lastEventStep\": {lastEventStep},");
        sb.AppendLine($"    \"lastEventTime\": {FormatNumber(lastEvent?.TimeSeconds ?? -1f)},");
        sb.AppendLine($"    \"idleStepsAfterLastEvent\": {idleStepsAfterLastEvent},");
        sb.AppendLine($"    \"idleSecondsAfterLastEvent\": {FormatNumber(idleSecondsAfterLastEvent)},");
        sb.AppendLine($"    \"timeoutWithLivingBothSides\": {(timeoutWithLivingBothSides ? "true" : "false")},");
        sb.AppendLine($"    \"stallSuspected\": {(stallSuspected ? "true" : "false")},");
        sb.AppendLine($"    \"reason\": \"{(stallSuspected ? "timeout_after_long_no_event_window" : string.Empty)}\",");
        sb.AppendLine($"    \"aliveAllies\": {aliveAllies},");
        sb.AppendLine($"    \"aliveEnemies\": {aliveEnemies},");
        sb.AppendLine("    \"aliveUnits\": [");
        var aliveUnits = finalUnitStates?
            .Where(u => u.IsAlive)
            .OrderBy(u => u.Side)
            .ThenBy(u => u.Id)
            .ToList() ?? new List<BattleUnitReadModel>();
        for (var i = 0; i < aliveUnits.Count; i++)
        {
            var u = aliveUnits[i];
            sb.Append("      { ");
            sb.Append($"\"id\": \"{Escape(u.Id)}\", ");
            sb.Append($"\"name\": \"{Escape(u.Name)}\", ");
            sb.Append($"\"side\": \"{u.Side}\", ");
            sb.Append($"\"hp\": {FormatNumber(u.CurrentHealth)}, ");
            sb.Append($"\"state\": \"{u.ActionState}\", ");
            sb.Append($"\"pendingAction\": \"{u.PendingActionType?.ToString() ?? string.Empty}\", ");
            sb.Append($"\"target\": \"{Escape(u.TargetId ?? string.Empty)}\", ");
            sb.Append($"\"selector\": \"{Escape(u.CurrentSelector)}\", ");
            sb.Append($"\"fallback\": \"{Escape(u.CurrentFallback)}\", ");
            sb.Append($"\"retargetLock\": {FormatNumber(u.RetargetLockRemaining)}, ");
            sb.Append($"\"cooldown\": {FormatNumber(u.CooldownRemaining)}, ");
            sb.Append($"\"x\": {FormatNumber(u.Position.X)}, ");
            sb.Append($"\"y\": {FormatNumber(u.Position.Y)}, ");
            sb.Append($"\"preferredRangeMin\": {FormatNumber(u.PreferredRangeMin)}, ");
            sb.Append($"\"preferredRangeMax\": {FormatNumber(u.PreferredRangeMax)}");
            sb.Append(" }");
            if (i < aliveUnits.Count - 1)
            {
                sb.Append(',');
            }

            sb.AppendLine();
        }

        sb.AppendLine("    ]");
        sb.Append("  }");
    }

    private static void WriteStats(StringBuilder sb, BattleReplayBundle replay)
    {
        var events = replay.EventStream;
        var allyDmg = 0f;
        var enemyDmg = 0f;
        var allyHeal = 0f;
        var enemyHeal = 0f;
        var allyKills = 0;
        var enemyKills = 0;
        var firstDamageStep = -1;
        var firstKillStep = -1;

        foreach (var evt in events)
        {
            var isAllyActor = evt.ActorId.Value.StartsWith("ally_");
            if (evt.LogCode is BattleLogCode.BasicAttackDamage or BattleLogCode.ActiveSkillDamage)
            {
                if (isAllyActor)
                {
                    allyDmg += evt.Value;
                }
                else
                {
                    enemyDmg += evt.Value;
                }

                if (firstDamageStep < 0)
                {
                    firstDamageStep = evt.StepIndex;
                }
            }
            else if (evt.LogCode == BattleLogCode.ActiveSkillHeal)
            {
                if (isAllyActor)
                {
                    allyHeal += evt.Value;
                }
                else
                {
                    enemyHeal += evt.Value;
                }
            }

            if (evt.EventKind == BattleEventKind.Kill)
            {
                if (isAllyActor)
                {
                    allyKills++;
                }
                else
                {
                    enemyKills++;
                }

                if (firstKillStep < 0)
                {
                    firstKillStep = evt.StepIndex;
                }
            }
        }

        sb.AppendLine("  \"stats\": {");
        sb.AppendLine($"    \"allyDamageDealt\": {allyDmg:0.#},");
        sb.AppendLine($"    \"enemyDamageDealt\": {enemyDmg:0.#},");
        sb.AppendLine($"    \"allyHealingDone\": {allyHeal:0.#},");
        sb.AppendLine($"    \"enemyHealingDone\": {enemyHeal:0.#},");
        sb.AppendLine($"    \"allyKills\": {allyKills},");
        sb.AppendLine($"    \"enemyKills\": {enemyKills},");
        sb.AppendLine($"    \"firstDamageStep\": {firstDamageStep},");
        sb.AppendLine($"    \"firstKillStep\": {firstKillStep},");

        var actionCounts = events
            .GroupBy(e => e.ActionType)
            .OrderByDescending(g => g.Count())
            .Select(g => $"\"{g.Key}\": {g.Count()}");
        sb.AppendLine($"    \"actionBreakdown\": {{ {string.Join(", ", actionCounts)} }},");

        var perUnit = events
            .Where(e => e.LogCode is BattleLogCode.BasicAttackDamage or BattleLogCode.ActiveSkillDamage)
            .GroupBy(e => e.ActorId.Value)
            .OrderByDescending(g => g.Sum(e => e.Value))
            .Select(g => $"\"{Escape(g.Key)}\": {g.Sum(e => e.Value):0.#}");
        sb.AppendLine($"    \"damageByUnit\": {{ {string.Join(", ", perUnit)} }}");
        sb.Append("  }");
    }

    private static void WriteStatLine(StringBuilder sb, string key, Dictionary<StatKey, float> stats, StatKey statKey)
    {
        sb.AppendLine($"      \"{key}\": {GetStat(stats, statKey):0.###},");
    }

    private static float GetStat(Dictionary<StatKey, float> stats, StatKey key)
    {
        return stats.TryGetValue(key, out var value) ? value : 0f;
    }

    private static string Escape(string input)
    {
        return input.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
    }

    private static string FormatNumber(float value)
    {
        return value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    }

    private readonly record struct FinalUnitEntry(string Id, string Side, bool Alive);

    private static FinalUnitEntry[] ExtractFinalUnitsFromEvents(BattleReplayBundle replay)
    {
        var killed = new HashSet<string>();
        foreach (var evt in replay.EventStream)
        {
            if (evt.EventKind == BattleEventKind.Kill && evt.TargetId != null)
            {
                killed.Add(evt.TargetId.Value.Value);
            }
        }

        var allyIds = new HashSet<string>(replay.Input.Allies.Select(u => u.Id));
        return replay.Input.Allies.Concat(replay.Input.Enemies)
            .Select(u => new FinalUnitEntry(u.Id, allyIds.Contains(u.Id) ? "Ally" : "Enemy", !killed.Contains(u.Id)))
            .ToArray();
    }
}
