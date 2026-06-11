using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using SM.Combat.Model;

namespace SM.Unity;

/// <summary>
/// 전투 종료 시 positional consequence detector(차단/측면/후방/다이브킬/구출) 발동 횟수를
/// 사람이 읽을 수 있는 한 줄로 만든다. sim이 남기는 ActivityMetricRecorded telemetry의
/// 런타임 소비자 — QuickBattle smoke 레인에서 "detector가 실제로 몇 번 발동했는가"를 확인하는 surface.
/// </summary>
public static class BattleActivityMetricsLog
{
    // 라벨은 BattleHighlightLedger의 한국어 어휘와 맞춘다. 0이어도 표기 — 침묵하면 검증 surface가 아니다.
    private static readonly (string MetricId, string Label)[] PositionalCounters =
    {
        ("ScreenAbsorbCount", "차단"),
        ("FlankStrikeCount", "측면"),
        ("RearStrikeCount", "후방"),
        ("BacklineDiveKillCount", "후열 다이브킬"),
        ("SaveMomentCount", "구출"),
    };

    public static bool TryFormatPositionalSummary(IReadOnlyList<TelemetryEventRecord>? telemetryEvents, out string line)
    {
        line = string.Empty;
        if (telemetryEvents == null || telemetryEvents.Count == 0)
        {
            return false;
        }

        var values = new Dictionary<string, float>(StringComparer.Ordinal);
        foreach (var record in telemetryEvents)
        {
            if (record is { EventKind: TelemetryEventKind.ActivityMetricRecorded } && !string.IsNullOrEmpty(record.StringValueA))
            {
                values[record.StringValueA] = record.ValueA;
            }
        }

        if (values.Count == 0)
        {
            return false;
        }

        var builder = new StringBuilder("positional |");
        for (var i = 0; i < PositionalCounters.Length; i++)
        {
            var (metricId, label) = PositionalCounters[i];
            var count = values.TryGetValue(metricId, out var value) ? value : 0f;
            builder.Append(i == 0 ? " " : " · ")
                .Append(label)
                .Append(' ')
                .Append(count.ToString("0", CultureInfo.InvariantCulture));
        }

        line = builder.ToString();
        return true;
    }
}
