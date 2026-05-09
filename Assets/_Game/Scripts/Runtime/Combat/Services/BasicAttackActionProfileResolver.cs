using System;
using SM.Combat.Model;
using SM.Core.Contracts;

namespace SM.Combat.Services;

public readonly record struct ResolvedBasicAttackActionProfile(
    BasicAttackActionProfile Profile,
    float LogicalRange,
    float ContactRange,
    float PreImpactStepDistance,
    int StepInArcSign,
    string LungeEntrySide,
    string WeaponTrailSide)
{
    public bool AllowsPreImpactStep => PreImpactStepDistance > 0.01f && ContactRange < LogicalRange - 0.05f;
}

public static class BasicAttackActionProfileResolver
{
    private const float RangedRangeThreshold = 2.05f;
    private const float StepInContactRange = 0.6f;
    private const float LungeContactRange = 0.52f;
    private const float DashContactRange = 0.68f;
    private const float StepInPreImpactDistance = 0.68f;
    private const float LungePreImpactDistance = 0.82f;
    private const float DashPreImpactDistance = 1.18f;

    public static ResolvedBasicAttackActionProfile Resolve(UnitSnapshot actor)
    {
        var logicalRange = Math.Max(0.5f, actor.AttackRange);
        var authored = actor.EffectiveBasicAttack;
        var profile = authored.ActionProfile == BasicAttackActionProfile.Auto
            ? InferProfile(actor, logicalRange)
            : authored.ActionProfile;
        var contactRange = ResolveContactRange(actor, authored, profile, logicalRange);
        var stepDistance = ResolvePreImpactStepDistance(authored, profile, logicalRange, contactRange);
        var arcSign = ResolveArcSign(actor);
        var entrySide = arcSign >= 0 ? "right" : "left";
        var trailSide = ResolveTrailSide(actor, authored.WeaponHandedness);
        return new ResolvedBasicAttackActionProfile(profile, logicalRange, contactRange, stepDistance, arcSign, entrySide, trailSide);
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

    public static string ToNoteToken(ResolvedBasicAttackActionProfile profile)
    {
        var baseToken = ToNoteToken(profile.Profile);
        var arcToken = profile.StepInArcSign >= 0 ? "StepInArcSign:right" : "StepInArcSign:left";
        return string.IsNullOrWhiteSpace(baseToken)
            ? $"{arcToken}+LungeEntrySide:{profile.LungeEntrySide}+WeaponTrailSide:{profile.WeaponTrailSide}"
            : $"{baseToken}+{arcToken}+LungeEntrySide:{profile.LungeEntrySide}+WeaponTrailSide:{profile.WeaponTrailSide}";
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
            BasicAttackActionProfile.StepInStrike => StepInContactRange,
            BasicAttackActionProfile.LungeStrike => LungeContactRange,
            BasicAttackActionProfile.DashStrike => DashContactRange,
            BasicAttackActionProfile.StationaryStrike => logicalRange,
            _ => IsRangedBasic(actor, logicalRange) ? logicalRange : StepInContactRange,
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
            BasicAttackActionProfile.StepInStrike => StepInPreImpactDistance,
            BasicAttackActionProfile.LungeStrike => LungePreImpactDistance,
            BasicAttackActionProfile.DashStrike => DashPreImpactDistance,
            _ => 0f,
        };

        return Math.Clamp(defaultStep, 0f, Math.Max(0f, logicalRange - contactRange));
    }

    private static int ResolveArcSign(UnitSnapshot actor)
    {
        var preferred = HandednessDecisionService.ResolvePreferredSideSign(actor.Definition.DominantHand, actor.Side);
        return preferred == 0
            ? (StableHash(actor.Id.Value) % 2 == 0 ? 1 : -1)
            : preferred;
    }

    private static string ResolveTrailSide(UnitSnapshot actor, WeaponHandednessProfile profile)
    {
        if (profile == WeaponHandednessProfile.DualWield)
        {
            return "dual";
        }

        return actor.Definition.DominantHand switch
        {
            DominantHand.Left => "left",
            DominantHand.Ambidextrous => "ambidextrous",
            _ => "right",
        };
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            var hash = 23;
            foreach (var ch in value)
            {
                hash = (hash * 31) + ch;
            }

            return hash;
        }
    }
}
