using System.Linq;
using NUnit.Framework;
using SM.Meta.Services;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class AugmentOfferScheduleServiceTests
{
    [Test]
    public void DefaultSchedule_UsesFiveSettledEncounterPicks()
    {
        var schedule = AugmentOfferScheduleService.DefaultPicks;

        Assert.That(schedule.Select(pick => pick.EncounterIndex), Is.EqualTo(new[] { 0, 4, 8, 16, 20 }));
        Assert.That(schedule.Select(pick => pick.PreferredBucket), Is.EqualTo(new[]
        {
            "TacticalRewrite",
            "HeroRewrite",
            "SynergyPact",
            "ScalingEngine",
            "EconomyAndLoot",
        }));
    }

    [Test]
    public void TryResolvePick_ReturnsOnlyCurrentUnclaimedSchedulePoint()
    {
        Assert.That(AugmentOfferScheduleService.TryResolvePick(4, acquiredTemporaryAugmentCount: 1, out var secondPick), Is.True);
        Assert.That(secondPick.PickIndex, Is.EqualTo(1));
        Assert.That(secondPick.PreferredBucket, Is.EqualTo("HeroRewrite"));

        Assert.That(AugmentOfferScheduleService.TryResolvePick(4, acquiredTemporaryAugmentCount: 2, out _), Is.False,
            "이미 두 번째 pick까지 획득한 run은 E4 offer를 다시 열지 않는다.");
        Assert.That(AugmentOfferScheduleService.TryResolvePick(5, acquiredTemporaryAugmentCount: 1, out _), Is.False);
    }
}
