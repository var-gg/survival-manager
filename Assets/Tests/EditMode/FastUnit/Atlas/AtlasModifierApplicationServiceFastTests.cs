using NUnit.Framework;
using SM.Meta.Services;

namespace SM.Tests.EditMode;

/// <summary>
/// task-atlas-modifier-application-v1 acceptance #1·#2·#3·#4·#6·#7:
/// Atlas RewardBias/ThreatPressure/AffinityBoost 3 channel을 Reward weight + Battle risk band +
/// Affinity reason 3 surface로 deterministic하게 매핑한다.
/// </summary>
[Category("FastUnit")]
public sealed class AtlasModifierApplicationServiceFastTests
{
    [Test]
    public void ComputeRewardPreviewWeight_PositiveBias_ScalesUp()
    {
        var weight = AtlasModifierApplicationService.ComputeRewardPreviewWeight(basePreviewWeight: 100, rewardBiasPercent: 20);
        Assert.That(weight, Is.EqualTo(120), "20% positive bias → 100 × 1.20 = 120.");
    }

    [Test]
    public void ComputeRewardPreviewWeight_ZeroBias_ReturnsBaseWeight()
    {
        var weight = AtlasModifierApplicationService.ComputeRewardPreviewWeight(basePreviewWeight: 50, rewardBiasPercent: 0);
        Assert.That(weight, Is.EqualTo(50));
    }

    [Test]
    public void ComputeRewardPreviewWeight_NegativeBiasClampedAtMinusHundred()
    {
        var weight = AtlasModifierApplicationService.ComputeRewardPreviewWeight(basePreviewWeight: 100, rewardBiasPercent: -150);
        Assert.That(weight, Is.EqualTo(0), "-100% 이하 clamp → 0.");
    }

    [Test]
    public void ComputeRewardPreviewWeight_ZeroBaseWeight_ReturnsZero()
    {
        Assert.That(AtlasModifierApplicationService.ComputeRewardPreviewWeight(0, 50), Is.EqualTo(0));
    }

    [Test]
    public void ComputeThreatBand_MapsBoundariesCorrectly()
    {
        Assert.That(AtlasModifierApplicationService.ComputeThreatBand(-10), Is.EqualTo(AtlasThreatBand.Normal));
        Assert.That(AtlasModifierApplicationService.ComputeThreatBand(0), Is.EqualTo(AtlasThreatBand.Normal));
        Assert.That(AtlasModifierApplicationService.ComputeThreatBand(15), Is.EqualTo(AtlasThreatBand.Normal));
        Assert.That(AtlasModifierApplicationService.ComputeThreatBand(16), Is.EqualTo(AtlasThreatBand.Elevated));
        Assert.That(AtlasModifierApplicationService.ComputeThreatBand(40), Is.EqualTo(AtlasThreatBand.Elevated));
        Assert.That(AtlasModifierApplicationService.ComputeThreatBand(41), Is.EqualTo(AtlasThreatBand.High));
        Assert.That(AtlasModifierApplicationService.ComputeThreatBand(75), Is.EqualTo(AtlasThreatBand.High));
        Assert.That(AtlasModifierApplicationService.ComputeThreatBand(76), Is.EqualTo(AtlasThreatBand.Severe));
        Assert.That(AtlasModifierApplicationService.ComputeThreatBand(999), Is.EqualTo(AtlasThreatBand.Severe));
    }

    [Test]
    public void ComputeAffinityRecommendationReason_PositiveBoost_RendersText()
    {
        var reason = AtlasModifierApplicationService.ComputeAffinityRecommendationReason(25);
        Assert.That(reason, Is.EqualTo("인연 보정 +25%"));
    }

    [Test]
    public void ComputeAffinityRecommendationReason_ZeroOrNegative_ReturnsEmpty()
    {
        Assert.That(AtlasModifierApplicationService.ComputeAffinityRecommendationReason(0), Is.Empty);
        Assert.That(AtlasModifierApplicationService.ComputeAffinityRecommendationReason(-5), Is.Empty);
    }

    [Test]
    public void BuildSummary_ZeroPercents_ReturnsEmpty()
    {
        var summary = AtlasModifierApplicationService.BuildSummary(0, 0, 0);
        Assert.That(summary, Is.EqualTo(AtlasModifierApplicationSummary.Empty));
        Assert.That(summary.IsPopulated, Is.False);
    }

    [Test]
    public void BuildSummary_NonZeroPercents_IsDeterministicForSameInput()
    {
        // task-atlas-modifier-application-v1 acceptance #4: same input → same summary (record equality).
        var a = AtlasModifierApplicationService.BuildSummary(15, 30, 10);
        var b = AtlasModifierApplicationService.BuildSummary(15, 30, 10);

        Assert.That(a, Is.EqualTo(b));
        Assert.That(a.RewardWeightMultiplierPercent, Is.EqualTo(15));
        Assert.That(a.ThreatBand, Is.EqualTo(AtlasThreatBand.Elevated));
        Assert.That(a.AffinityReason, Is.EqualTo("인연 보정 +10%"));
        Assert.That(a.IsPopulated, Is.True);
    }
}
