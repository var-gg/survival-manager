using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SM.Editor.SeedData;
using SM.Editor.Validation;

namespace SM.Tests.EditMode;

/// <summary>
/// 게이트① sweep 하네스 witness — <see cref="CampaignBalanceSweepRunner"/>의 축소 scope(smoke)를
/// 표준 BatchOnly 게이트에서 end-to-end로 돌려 "측정이 실제로 실 sim을 돌리고 리포트를 쓴다"를 잠근다.
/// full sweep(32시드×3분대×전 벤치마크)은 게이트 밖 수동 레인(배치 -executeMethod RunFullFromCli /
/// 메뉴) — Loop D와 동일한 분리. 측정 허구 3원칙: 여기서 (a) sim이 tick을 돌렸는지(StepP50>0),
/// (b) 장착 arm이 실제 분대원에게 붙었는지, (c) 델타가 baseline과 paired인지의 계약을 검증한다.
/// </summary>
[Category("BatchOnly")]
public sealed class HeadlessBalanceSweepSmokeTests
{
    [SetUp]
    public void SetUp()
    {
        SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(HeadlessBalanceSweepSmokeTests));
    }

    [Test]
    public void SmokeScope_MeasuresCurveAndDeltas_AndWritesReports()
    {
        var result = CampaignBalanceSweepRunner.Run(CampaignBalanceSweepRunner.SmokeScope);

        // ── 곡선 레인: 1분대 × 1사이트가 실 sim으로 측정됐다 ──
        Assert.That(result.Curve, Has.Count.EqualTo(1), "smoke는 혼성 분대 1종만 돈다.");
        var preset = result.Curve[0];
        Assert.That(preset.Sites, Is.Not.Empty, "최소 1사이트가 측정됐다.");
        var nodes = preset.Sites.SelectMany(site => site.Nodes).ToList();
        Assert.That(nodes, Is.Not.Empty, "사이트에 전투 노드 측정이 존재한다.");
        Assert.That(nodes.All(node => node.StepP50 > 0f), Is.True,
            "모든 노드 측정이 실 BattleSimulator tick을 돌렸다(자동승리 단축 아님).");
        Assert.That(nodes.All(node => node.WinRate >= 0f && node.WinRate <= 1f), Is.True,
            "승률이 [0,1] 범위다.");
        Assert.That(nodes.All(node => node.PerSeedWin.Count == CampaignBalanceSweepRunner.SmokeScope.SeedCount), Is.True,
            "시드별 관측이 scope의 시드 수와 일치한다.");

        // ── 델타 레인: front/back 분대가 각자 적응 벤치마크에서 paired 측정됐다 ──
        Assert.That(result.Deltas, Has.Count.EqualTo(2), "델타 분대(front/back) 2종이 측정됐다.");
        Assert.That(result.Deltas.All(delta => delta.BaselineNodes.Count > 0), Is.True,
            "분대별 baseline 노드 스캔이 존재한다.");
        Assert.That(result.Deltas.All(delta => delta.BaselineWinRate >= 0f && delta.BaselineWinRate <= 1f), Is.True,
            "분대별 baseline 승률이 유효하다.");
        var measuredItems = result.Deltas.SelectMany(delta => delta.ItemArms)
            .Where(arm => arm.Status == "measured")
            .ToList();
        Assert.That(measuredItems, Is.Not.Empty, "최소 1개 아이템 arm이 실측됐다(전량 equip-blocked 회귀 차단).");
        Assert.That(measuredItems.All(arm => !string.IsNullOrEmpty(arm.EquippedHeroId)), Is.True,
            "실측 아이템 arm은 실제 분대원에게 장착됐다.");
        Assert.That(result.Deltas.All(delta => delta.ItemArms
            .Where(arm => arm.Status == "measured")
            .All(arm => Math.Abs(arm.Delta - (arm.WinRate - delta.BaselineWinRate)) < 0.0001f)), Is.True,
            "델타가 같은 분대의 baseline과 paired로 계산됐다.");
        var ghostArms = result.Deltas.SelectMany(delta => delta.GhostArms).ToList();
        Assert.That(ghostArms, Is.Not.Empty, "유령 패시브 arm이 최소 1개 측정 시도됐다.");
        Assert.That(ghostArms.Any(arm => arm.Status == "measured"), Is.True,
            "유령 패시브 arm이 실측됐다(보드 미선택 무음 차단 회귀 방지).");

        // ── 리포트가 실제로 쓰였다 ──
        Assert.That(File.Exists(Path.Combine(result.ReportDirectory, "sweep_curve_report.json")), Is.True);
        Assert.That(File.Exists(Path.Combine(result.ReportDirectory, "sweep_delta_report.json")), Is.True);
        Assert.That(File.Exists(Path.Combine(result.ReportDirectory, "sweep_report.md")), Is.True);
    }
}
