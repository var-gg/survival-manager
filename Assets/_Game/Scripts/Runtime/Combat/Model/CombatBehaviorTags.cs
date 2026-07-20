using System;
using System.Collections.Generic;

namespace SM.Combat.Model;

/// <summary>
/// Stable authored behavior-tag ids consumed by the deterministic combat and loadout layers.
/// Membership is read from the immutable loadout snapshot; callers only perform ordinal equality checks and never
/// depend on package or modifier iteration order.
/// </summary>
public static class CombatBehaviorTags
{
    public const string PackPursuit = "pack_pursuit";
    public const string DuelistDiveCommit = "duelist_dive_commit";
    public const string DuelistHoldBruiser = "duelist_hold_bruiser";
    public const string DuelistPeel = "duelist_peel";
    public const string ExecuteLowHp = "execute_low_hp";
    public const string DiveAssassinKeystone = "dive_assassin_keystone";

    public static bool IsInterpreted(string? behaviorTag)
    {
        return string.Equals(behaviorTag, PackPursuit, StringComparison.Ordinal)
               || string.Equals(behaviorTag, DuelistDiveCommit, StringComparison.Ordinal)
               || string.Equals(behaviorTag, DuelistHoldBruiser, StringComparison.Ordinal)
               || string.Equals(behaviorTag, DuelistPeel, StringComparison.Ordinal)
               || string.Equals(behaviorTag, ExecuteLowHp, StringComparison.Ordinal)
               || string.Equals(behaviorTag, DiveAssassinKeystone, StringComparison.Ordinal);
    }

    public static bool Contains(
        IReadOnlyList<CombatRuleModifierPackage>? packages,
        string behaviorTag)
    {
        if (packages == null || string.IsNullOrEmpty(behaviorTag))
        {
            return false;
        }

        foreach (var package in packages)
        {
            foreach (var modifier in package.Modifiers ?? Array.Empty<RuleModifier>())
            {
                if (modifier.Kind == RuleModifierKind.BehaviorTag
                    && string.Equals(modifier.Value, behaviorTag, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

}
