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
    // 차단은 흡수(스크린 뚫고 들어온 피해 경감) + 우회 유도(타게팅이 후열을 포기) 합산 — 차단이
    // 건강하게 작동하면 흡수가 0에 수렴하므로 흡수만 세면 거꾸로 읽힌다.
    private static readonly (string[] MetricIds, string Label)[] PositionalCounters =
    {
        (new[] { "ScreenAbsorbCount", "ScreenDeterrenceCount" }, "차단"),
        (new[] { "FlankStrikeCount" }, "측면"),
        (new[] { "RearStrikeCount" }, "후방"),
        (new[] { "BacklineDiveKillCount" }, "후열 다이브킬"),
        (new[] { "SaveMomentCount" }, "구출"),
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
            var (metricIds, label) = PositionalCounters[i];
            var count = 0f;
            foreach (var metricId in metricIds)
            {
                count += values.TryGetValue(metricId, out var value) ? value : 0f;
            }

            builder.Append(i == 0 ? " " : " · ")
                .Append(label)
                .Append(' ')
                .Append(count.ToString("0", CultureInfo.InvariantCulture));
        }

        line = builder.ToString();
        return true;
    }
}
