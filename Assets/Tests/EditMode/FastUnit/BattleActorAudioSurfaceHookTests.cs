using NUnit.Framework;
using SM.Unity;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class BattleActorAudioSurfaceHookTests
{
    [TestCase(BattlePresentationCueType.ActionCommitBasic, "sfx.combat.action_commit_basic")]
    [TestCase(BattlePresentationCueType.ActionCommitSkill, "sfx.combat.action_commit_skill")]
    [TestCase(BattlePresentationCueType.ActionCommitHeal, "sfx.combat.action_commit_heal")]
    [TestCase(BattlePresentationCueType.ImpactDamage, "sfx.combat.impact_damage")]
    [TestCase(BattlePresentationCueType.ImpactHeal, "sfx.combat.impact_heal")]
    [TestCase(BattlePresentationCueType.GuardEnter, "sfx.combat.guard_enter")]
    [TestCase(BattlePresentationCueType.GuardExit, "sfx.combat.guard_exit")]
    [TestCase(BattlePresentationCueType.RepositionStart, "sfx.combat.reposition_start")]
    [TestCase(BattlePresentationCueType.RepositionStop, "sfx.combat.reposition_stop")]
    [TestCase(BattlePresentationCueType.DeathStart, "sfx.combat.death_start")]
    public void TryResolveHookId_MapsAudioSurfaceCues(BattlePresentationCueType cueType, string expectedHookId)
    {
        var resolved = BattleActorAudioSurface.TryResolveHookId(cueType, out var hookId);

        Assert.That(resolved, Is.True);
        Assert.That(hookId, Is.EqualTo(expectedHookId));
    }

    [Test]
    public void TryResolveHookId_IgnoresNonAudioSurfaceCue()
    {
        var resolved = BattleActorAudioSurface.TryResolveHookId(BattlePresentationCueType.WindupEnter, out var hookId);

        Assert.That(resolved, Is.False);
        Assert.That(hookId, Is.Empty);
    }
}
