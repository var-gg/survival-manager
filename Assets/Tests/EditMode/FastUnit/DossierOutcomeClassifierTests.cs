using NUnit.Framework;
using SM.Meta;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class DossierOutcomeClassifierTests
{
    [Test]
    public void Classify_Defeat_WhenNotVictory()
    {
        Assert.That(
            DossierOutcomeClassifier.Classify(victory: false, survivorAllyCount: 4, totalAllyCount: 4),
            Is.EqualTo(DossierOutcome.Defeat));
    }

    [Test]
    public void Classify_CleanVictory_WhenAllAlliesSurvive()
    {
        Assert.That(
            DossierOutcomeClassifier.Classify(victory: true, survivorAllyCount: 4, totalAllyCount: 4),
            Is.EqualTo(DossierOutcome.CleanVictory));
    }

    [Test]
    public void Classify_CostlyVictory_WhenSomeAlliesFall()
    {
        Assert.That(
            DossierOutcomeClassifier.Classify(victory: true, survivorAllyCount: 2, totalAllyCount: 4),
            Is.EqualTo(DossierOutcome.CostlyVictory));
    }

    [Test]
    public void Classify_CleanVictory_WhenRosterCountUnknown()
    {
        // totalAllyCount == 0 (로스터 정보 없음) + 승리 → vacuously clean.
        Assert.That(
            DossierOutcomeClassifier.Classify(victory: true, survivorAllyCount: 0, totalAllyCount: 0),
            Is.EqualTo(DossierOutcome.CleanVictory));
    }

    [Test]
    public void ToToken_MapsEachOutcome()
    {
        Assert.That(DossierOutcomeClassifier.ToToken(DossierOutcome.Defeat), Is.EqualTo("defeat"));
        Assert.That(DossierOutcomeClassifier.ToToken(DossierOutcome.CostlyVictory), Is.EqualTo("costly_victory"));
        Assert.That(DossierOutcomeClassifier.ToToken(DossierOutcome.CleanVictory), Is.EqualTo("clean_victory"));
    }
}
