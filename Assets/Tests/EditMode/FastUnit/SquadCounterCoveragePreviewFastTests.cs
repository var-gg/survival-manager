using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Meta.Services;

namespace SM.Tests.EditMode;

/// <summary>
/// SquadCounterCoveragePreview.Classify 분류 로직 검증 — 강함(Standard 이상)/취약(None) 분리 + 순서.
/// 집계(Evaluate)는 CounterCoverageAggregationService(별도 테스트 보유)에 위임하므로 여기선 분류 계약에 집중.
/// </summary>
[Category("FastUnit")]
public sealed class SquadCounterCoveragePreviewFastTests
{
    [Test]
    public void Classify_SeparatesStrongAndGaps_LightIsNeither()
    {
        var report = new TeamCounterCoverageReport
        {
            ArmorShred = CounterCoverageLevelValue.Strong,        // 강함
            Exposure = CounterCoverageLevelValue.Standard,        // 강함
            GuardBreakMultiHit = CounterCoverageLevelValue.Light, // 강함도 취약도 아님
            TrackingArea = CounterCoverageLevelValue.None,        // 취약
            // 나머지 4개(TenacityStability/AntiHealShatter/InterceptPeel/CleaveWaveclear) 기본 None → 취약
        };

        var (strong, gaps) = SquadCounterCoveragePreview.Classify(report);

        CollectionAssert.AreEqual(new[] { "ArmorShred", "Exposure" }, strong.ToArray());
        Assert.AreEqual(5, gaps.Count, "TrackingArea + 기본 None 4개");
        Assert.Contains("TrackingArea", gaps.ToArray());
        Assert.IsFalse(gaps.Contains("GuardBreakMultiHit"), "Light 은 취약 아님");
        Assert.IsFalse(strong.Contains("GuardBreakMultiHit"), "Light 은 강함 아님");
    }

    [Test]
    public void Classify_AllNone_AllGaps_NoStrong()
    {
        var (strong, gaps) = SquadCounterCoveragePreview.Classify(new TeamCounterCoverageReport());

        Assert.IsEmpty(strong);
        Assert.AreEqual(8, gaps.Count);
    }

    [Test]
    public void Classify_NullReport_ReturnsEmpty()
    {
        var (strong, gaps) = SquadCounterCoveragePreview.Classify(null);

        Assert.IsEmpty(strong);
        Assert.IsEmpty(gaps);
    }

    [Test]
    public void Evaluate_NullTemplates_ReturnsEmptyReport()
    {
        var report = SquadCounterCoveragePreview.Evaluate(null);

        Assert.IsNotNull(report);
        var (strong, gaps) = SquadCounterCoveragePreview.Classify(report);
        Assert.IsEmpty(strong);
        Assert.AreEqual(8, gaps.Count, "빈 분대 → 전 차원 None");
    }
}
