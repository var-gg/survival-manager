using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Core.Contracts;

namespace SM.Combat.Services;

public static class HandednessDecisionService
{
    public static int ResolvePreferredSideSign(DominantHand hand, TeamSide side)
    {
        return hand switch
        {
            DominantHand.Right => side == TeamSide.Ally ? 1 : -1,
            DominantHand.Left => side == TeamSide.Ally ? -1 : 1,
            _ => 0,
        };
    }

    public static float ResolveHandednessSlotWeight(UnitSnapshot actor, TacticContext context)
    {
        if (actor.Definition.DominantHand == DominantHand.Ambidextrous)
        {
            return 0f;
        }

        var baseWeight = actor.Definition.ClassId switch
        {
            "duelist" => 0.12f,
            "vanguard" => 0.07f,
            "ranger" => 0.05f,
            "mystic" => 0.04f,
            _ => 0.06f,
        };
        var weaponFactor = ResolveWeaponFactor(actor.EffectiveBasicAttack.WeaponHandedness);
        return baseWeight * weaponFactor * ResolveShapeDampening(context);
    }

    public static int ResolveAmbidextrousLeastCrowdedSign(
        BattleState state,
        UnitSnapshot actor,
        CombatVector2 lateral,
        float lateralDistance)
    {
        var positive = ResolveCrowdingScore(state, actor, actor.Position + (lateral * lateralDistance));
        var negative = ResolveCrowdingScore(state, actor, actor.Position - (lateral * lateralDistance));
        if (Math.Abs(positive - negative) <= 0.001f)
        {
            return ResolveStableSign(state, actor, "ambidextrous_lateral");
        }

        return positive < negative ? 1 : -1;
    }

    public static string ResolveHandLabel(DominantHand hand)
    {
        return hand switch
        {
            DominantHand.Left => "left",
            DominantHand.Ambidextrous => "ambidextrous",
            _ => "right",
        };
    }

    public static WeaponHandednessProfile ResolveWeaponProfile(string weaponFamilyTag)
    {
        var normalized = weaponFamilyTag?.Trim().ToLowerInvariant() ?? string.Empty;
        return normalized switch
        {
            "sword_shield" or "sword+shield" or "shield" => WeaponHandednessProfile.SwordAndShield,
            "dual_wield" or "dual-wield" or "dual" or "dagger_pair" => WeaponHandednessProfile.DualWield,
            "spear" or "polearm" => WeaponHandednessProfile.Spear,
            "two_hand" or "two-hand" or "greatsword" or "greataxe" => WeaponHandednessProfile.TwoHand,
            "bow" or "crossbow" => WeaponHandednessProfile.Bow,
            "gun" or "rifle" or "pistol" => WeaponHandednessProfile.Gun,
            "spell" or "staff" or "wand" or "focus" => WeaponHandednessProfile.Spell,
            _ => WeaponHandednessProfile.OneHand,
        };
    }

    public static float ResolveShapeDampening(TacticContext context)
    {
        return Math.Clamp((0.45f + (0.55f * context.Width)) * (1f - (0.60f * context.Compactness)), 0f, 1.35f);
    }

    public static int ResolveStableSign(BattleState state, UnitSnapshot actor, string intent)
    {
        unchecked
        {
            var hash = state.Seed;
            hash = (hash * 397) ^ state.StepIndex;
            hash = (hash * 397) ^ StableHash(actor.Id.Value);
            hash = (hash * 397) ^ StableHash(intent);
            return PositiveModulo(hash, 10000) >= 5000 ? 1 : -1;
        }
    }

    private static float ResolveWeaponFactor(WeaponHandednessProfile profile)
    {
        return profile switch
        {
            WeaponHandednessProfile.SwordAndShield => 1.15f,
            WeaponHandednessProfile.DualWield => 0.35f,
            WeaponHandednessProfile.Spear => 0.55f,
            WeaponHandednessProfile.TwoHand => 0.60f,
            WeaponHandednessProfile.Bow => 0.80f,
            WeaponHandednessProfile.Gun => 0.75f,
            WeaponHandednessProfile.Spell => 0.25f,
            _ => 1f,
        };
    }

    private static float ResolveCrowdingScore(BattleState state, UnitSnapshot actor, CombatVector2 candidate)
    {
        return state.AllUnits
            .Where(unit => unit.IsAlive && unit.Id != actor.Id)
            .Sum(unit =>
            {
                var distance = Math.Max(0.05f, candidate.DistanceTo(unit.Position));
                return 1f / distance;
            });
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            var hash = 17;
            foreach (var ch in value)
            {
                hash = (hash * 31) + ch;
            }

            return hash;
        }
    }

    private static int PositiveModulo(int value, int divisor)
    {
        var remainder = value % divisor;
        return remainder < 0 ? remainder + divisor : remainder;
    }
}
