using NUnit.Framework;
using SM.Editor.Validation;

namespace SM.Tests.EditMode;

[Category("BatchOnly")]
public sealed class CampaignWallDeficitSearchTests
{
    [Test]
    public void FindMinimumWinningCorrection_BracketsObservedThreshold()
    {
        const double threshold = 0.37d;

        var result = CampaignWallDeficitSearch.FindMinimumWinningCorrection(
            additional => additional >= threshold,
            searchMaximum: 4d,
            tolerance: 0.001d);

        Assert.That(result.RightCensored, Is.False);
        Assert.That(result.MonotonicityViolated, Is.False);
        Assert.That(result.AdditionalLogDeficit, Is.Not.Null);
        Assert.That(result.AdditionalLogDeficit!.Value, Is.GreaterThanOrEqualTo(threshold));
        Assert.That(result.AdditionalLogDeficit.Value, Is.LessThan(threshold + 0.001d));
        Assert.That(result.EvaluationCount, Is.GreaterThan(2));
    }

    [Test]
    public void FindMinimumWinningCorrection_ReportsRightCensoring()
    {
        var result = CampaignWallDeficitSearch.FindMinimumWinningCorrection(
            additional => additional > 5d,
            searchMaximum: 4d,
            tolerance: 0.001d);

        Assert.That(result.AdditionalLogDeficit, Is.Null);
        Assert.That(result.RightCensored, Is.True);
        Assert.That(result.MonotonicityViolated, Is.False);
    }

    [Test]
    public void FindMinimumWinningCorrection_RejectsAlreadyWinningInput()
    {
        Assert.That(
            () => CampaignWallDeficitSearch.FindMinimumWinningCorrection(
                _ => true,
                searchMaximum: 4d,
                tolerance: 0.001d),
            Throws.InvalidOperationException);
    }
}
