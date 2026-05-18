using NUnit.Framework;
using SM.Combat.Model;

namespace SM.Tests.FastUnit;

[Category("FastUnit")]
public sealed class SkillPresentationProfileResolverTests
{
    [Test]
    public void Resolve_CoversPilotSkillPresentationProfiles()
    {
        AssertProfile(
            SkillPresentationProfileResolver.Resolve(
                "skill_aegis_linebreaker",
                SkillKind.Strike,
                DamageType.Physical,
                SkillDelivery.Melee,
                CompiledSkillSlots.CoreActive),
            SkillPresentationFamily.Melee,
            SkillPresentationSkin.GuardSteel,
            SkillPresentationGesture.HeavyMelee,
            SkillPresentationCueSequence.StrikeImpact);

        AssertProfile(
            SkillPresentationProfileResolver.Resolve(
                "skill_ember_arrow",
                SkillKind.Strike,
                DamageType.Physical,
                SkillDelivery.Projectile,
                CompiledSkillSlots.CoreActive),
            SkillPresentationFamily.Projectile,
            SkillPresentationSkin.Fire,
            SkillPresentationGesture.BowShot,
            SkillPresentationCueSequence.ProjectileImpact);

        AssertProfile(
            SkillPresentationProfileResolver.Resolve(
                "skill_echo_resonance",
                SkillKind.Debuff,
                DamageType.Magical,
                SkillDelivery.Projectile,
                CompiledSkillSlots.CoreActive,
                appliedStatusCount: 1),
            SkillPresentationFamily.Debuff,
            SkillPresentationSkin.EchoArcane,
            SkillPresentationGesture.ProjectileCast,
            SkillPresentationCueSequence.DebuffApply);

        AssertProfile(
            SkillPresentationProfileResolver.Resolve(
                "skill_memory_tuning",
                SkillKind.Heal,
                DamageType.Healing,
                SkillDelivery.Aura,
                CompiledSkillSlots.UtilityActive),
            SkillPresentationFamily.Heal,
            SkillPresentationSkin.HealGold,
            SkillPresentationGesture.SpellOmni,
            SkillPresentationCueSequence.HealPulse);

        AssertProfile(
            SkillPresentationProfileResolver.Resolve(
                "skill_aegis_sentinel_oath",
                SkillKind.Shield,
                DamageType.Magical,
                SkillDelivery.Nova,
                CompiledSkillSlots.CoreActive),
            SkillPresentationFamily.Shield,
            SkillPresentationSkin.GuardSteel,
            SkillPresentationGesture.GuardPose,
            SkillPresentationCueSequence.ShieldGuard);

        AssertProfile(
            SkillPresentationProfileResolver.Resolve(
                "skill_fracture_step",
                SkillKind.Utility,
                DamageType.Magical,
                SkillDelivery.Zone,
                CompiledSkillSlots.UtilityActive),
            SkillPresentationFamily.Reposition,
            SkillPresentationSkin.FrostGlass,
            SkillPresentationGesture.Reposition,
            SkillPresentationCueSequence.RepositionTrail);
    }

    private static void AssertProfile(
        BattleSkillPresentationProfile profile,
        SkillPresentationFamily family,
        SkillPresentationSkin skin,
        SkillPresentationGesture gesture,
        SkillPresentationCueSequence sequence)
    {
        Assert.That(profile.IsResolved, Is.True);
        Assert.That(profile.Family, Is.EqualTo(family));
        Assert.That(profile.Skin, Is.EqualTo(skin));
        Assert.That(profile.Gesture, Is.EqualTo(gesture));
        Assert.That(profile.CueSequence, Is.EqualTo(sequence));
    }
}
