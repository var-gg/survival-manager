using System;
using SM.Combat.Model;

namespace SM.Combat.Services;

public readonly record struct ResolvedBasicAttackActionProfile(
    BasicAttackActionProfile Profile,
    float LogicalRange,
    float ContactRange,
    float PreImpactStepDistance)
{
    public bool AllowsPreImpactStep => PreImpactStepDistance > 0.01f && ContactRange < LogicalRange - 0.05f;
}

public static class BasicAttackActionProfileResolver
{
    private const float RangedRangeThreshold = 2.05f;

    public static ResolvedBasicAttackActionProfile Resolve(UnitSnapshot actor)
    {
        var logicalRange = Math.Max(0.5f, actor.AttackRange);
        var authored = actor.EffectiveBasicAttack;
        var profile = authored.ActionProfile == BasicAttackActionProfile.Auto
            ? InferProfile(actor, logicalRange)
            : authored.ActionProfile;
        var contactRange = ResolveContactRange(actor, authored, profile, logicalRange);
        var stepDistance = ResolvePreImpactStepDistance(authored, profile, logicalRange, contactRange);
        return new ResolvedBasicAttackActionProfile(profile, logicalRange, contactRange, stepDistance);
    }

    public static string ToNoteToken(BasicAttackActionProfile profile)
    {
        return profile switch
        {
            BasicAttackActionProfile.StepInStrike => "profile_stepin",
            BasicAttackActionProfile.LungeStrike => "profile_lunge",
            BasicAttackActionProfile.DashStrike => "profile_dash",
            BasicAttackActionProfile.StationaryStrike => "profile_stationary",
            _ => string.Empty,
        };
    }

    private static BasicAttackActionProfile InferProfile(UnitSnapshot actor, float logicalRange)
    {
        if (IsRangedBasic(actor, logicalRange))
        {
            return BasicAttackActionProfile.StationaryStrike;
        }

        return actor.Definition.ClassId switch
        {
            "duelist" => BasicAttackActionProfile.LungeStrike,
            "vanguard" => BasicAttackActionProfile.StepInStrike,
            _ => BasicAttackActionProfile.StepInStrike,
        };
    }

    private static bool IsRangedBasic(UnitSnapshot actor, float logicalRange)
    {
        return logicalRange >= RangedRangeThreshold
               || actor.PreferredRangeBand.ClampedMin >= 1.8f
               || actor.Definition.ClassId is "ranger" or "mystic";
    }

    private static float ResolveContactRange(
        UnitSnapshot actor,
        BattleBasicAttackSpec authored,
        BasicAttackActionProfile profile,
        float logicalRange)
    {
        if (authored.ContactRange > 0f)
        {
            return Math.Clamp(authored.ContactRange, 0.15f, logicalRange);
        }

        var defaultContact = profile switch
        {
            BasicAttackActionProfile.StepInStrike => 0.78f,
            BasicAttackActionProfile.LungeStrike => 0.68f,
            BasicAttackActionProfile.DashStrike => 0.88f,
            BasicAttackActionProfile.StationaryStrike => logicalRange,
            _ => IsRangedBasic(actor, logicalRange) ? logicalRange : 0.78f,
        };

        return Math.Clamp(defaultContact, 0.15f, logicalRange);
    }

    private static float ResolvePreImpactStepDistance(
        BattleBasicAttackSpec authored,
        BasicAttackActionProfile profile,
        float logicalRange,
        float contactRange)
    {
        if (authored.PreImpactStepDistance > 0f)
        {
            return Math.Clamp(authored.PreImpactStepDistance, 0f, Math.Max(0f, logicalRange - contactRange));
        }

        var defaultStep = profile switch
        {
            BasicAttackActionProfile.StepInStrike => 0.46f,
            BasicAttackActionProfile.LungeStrike => 0.72f,
            BasicAttackActionProfile.DashStrike => 1.12f,
            _ => 0f,
        };

        return Math.Clamp(defaultStep, 0f, Math.Max(0f, logicalRange - contactRange));
    }
}
