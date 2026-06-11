using NUnit.Framework;
using SM.Combat.Model;
using SM.Unity;

namespace SM.Tests.EditMode;

/// <summary>
/// 전투 종료 positional detector 카운트 한 줄 요약 — ActivityMetricRecorded telemetry 소비 계약.
/// 0이어도 표기해야(침묵 금지) 전투테스트에서 "발동 안 했음"을 판정할 수 있다.
/// </summary>
[Category("FastUnit")]
public sealed class BattleActivityMetricsLogTests
{
    [Test]
    public void FormatsAllPositionalCounters_IncludingZeros()
    {
        var events = new[]
        {
            Metric("ScreenAbsorbCount", 2f),
            Metric("FlankStrikeCount", 5f),
            Metric("RearStrikeCount", 1f),
            Metric("BacklineDiveKillCount", 0f),
            Metric("SaveMomentCount", 3f),
            Metric("TargetEntropy", 0.42f),
        };

        var formatted = BattleActivityMetricsLog.TryFormatPositionalSummary(events, out var line);

        Assert.That(formatted, Is.True);
        Assert.That(line, Is.EqualTo("positional | 차단 2 · 측면 5 · 후방 1 · 후열 다이브킬 0 · 구출 3"));
    }

    [Test]
    public void MissingMetrics_FallBackToZero_WhenAnyActivityMetricExists()
    {
        var events = new[] { Metric("TargetEntropy", 0.9f) };

        var formatted = BattleActivityMetricsLog.TryFormatPositionalSummary(events, out var line);

        Assert.That(formatted, Is.True, "ActivityMetric telemetry가 있으면 카운터는 0으로라도 보고한다");
        Assert.That(line, Does.Contain("차단 0").And.Contain("구출 0"));
    }

    [Test]
    public void NoActivityTelemetry_ProducesNoLine()
    {
        Assert.That(BattleActivityMetricsLog.TryFormatPositionalSummary(null, out _), Is.False);
        Assert.That(BattleActivityMetricsLog.TryFormatPositionalSummary(new TelemetryEventRecord[0], out _), Is.False);

        var unrelated = new[] { new TelemetryEventRecord { EventKind = TelemetryEventKind.BattleStarted } };
        Assert.That(BattleActivityMetricsLog.TryFormatPositionalSummary(unrelated, out _), Is.False);
    }

    private static TelemetryEventRecord Metric(string metricId, float value)
    {
        return new TelemetryEventRecord
        {
            EventKind = TelemetryEventKind.ActivityMetricRecorded,
            StringValueA = metricId,
            ValueA = value,
        };
    }
}
