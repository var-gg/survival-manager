using System;
using System.Collections.Generic;
using System.Linq;

namespace SM.Core.Stats;

public readonly record struct StatKey(string Value)
{
    public static readonly StatKey MaxHealth = new("max_health");
    public static readonly StatKey Armor = new("armor");
    public static readonly StatKey Resist = new("resist");
    public static readonly StatKey BarrierPower = new("barrier_power");
    public static readonly StatKey Tenacity = new("tenacity");
    public static readonly StatKey HealPower = new("heal_power");
    public static readonly StatKey PhysPower = new("phys_power");
    public static readonly StatKey MagPower = new("mag_power");
    public static readonly StatKey AttackSpeed = new("attack_speed");
    public static readonly StatKey MoveSpeed = new("move_speed");
    public static readonly StatKey AttackRange = new("attack_range");
    public static readonly StatKey ManaMax = new("mana_max");
    public static readonly StatKey ManaGainOnAttack = new("mana_gain_on_attack");
    public static readonly StatKey ManaGainOnHit = new("mana_gain_on_hit");
    public static readonly StatKey CooldownRecovery = new("cooldown_recovery");
    public static readonly StatKey CritChance = new("crit_chance");
    public static readonly StatKey CritMultiplier = new("crit_multiplier");
    public static readonly StatKey PhysPen = new("phys_pen");
    public static readonly StatKey MagPen = new("mag_pen");
    public static readonly StatKey AggroRadius = new("aggro_radius");
    public static readonly StatKey LeashDistance = new("leash_distance");
    public static readonly StatKey TargetSwitchDelay = new("target_switch_delay");
    public static readonly StatKey PreferredDistance = new("preferred_distance");
    public static readonly StatKey ProtectRadius = new("protect_radius");
    public static readonly StatKey AttackWindup = new("attack_windup");
    public static readonly StatKey CastWindup = new("cast_windup");
    public static readonly StatKey ProjectileSpeed = new("projectile_speed");
    public static readonly StatKey CollisionRadius = new("collision_radius");
    public static readonly StatKey RepositionCooldown = new("reposition_cooldown");
    public static readonly StatKey AttackCooldown = new("attack_cooldown");

    [Obsolete("Use PhysPower instead.")]
    public static readonly StatKey Attack = new("attack");

    [Obsolete("Use Armor instead.")]
    public static readonly StatKey Defense = new("defense");

    [Obsolete("Use AttackSpeed instead.")]
    public static readonly StatKey Speed = new("speed");

    private static readonly IReadOnlyDictionary<string, StatKey> CanonicalById = new Dictionary<string, StatKey>(StringComparer.Ordinal)
    {
        [MaxHealth.Value] = MaxHealth,
        [Armor.Value] = Armor,
        [Resist.Value] = Resist,
        [BarrierPower.Value] = BarrierPower,
        [Tenacity.Value] = Tenacity,
        [HealPower.Value] = HealPower,
        [PhysPower.Value] = PhysPower,
        [MagPower.Value] = MagPower,
        [AttackSpeed.Value] = AttackSpeed,
        [MoveSpeed.Value] = MoveSpeed,
        [AttackRange.Value] = AttackRange,
        [ManaMax.Value] = ManaMax,
        [ManaGainOnAttack.Value] = ManaGainOnAttack,
        [ManaGainOnHit.Value] = ManaGainOnHit,
        [CooldownRecovery.Value] = CooldownRecovery,
        [CritChance.Value] = CritChance,
        [CritMultiplier.Value] = CritMultiplier,
        [PhysPen.Value] = PhysPen,
        [MagPen.Value] = MagPen,
        [AggroRadius.Value] = AggroRadius,
        [LeashDistance.Value] = LeashDistance,
        [TargetSwitchDelay.Value] = TargetSwitchDelay,
        [PreferredDistance.Value] = PreferredDistance,
        [ProtectRadius.Value] = ProtectRadius,
        [AttackWindup.Value] = AttackWindup,
        [CastWindup.Value] = CastWindup,
        [ProjectileSpeed.Value] = ProjectileSpeed,
        [CollisionRadius.Value] = CollisionRadius,
        [RepositionCooldown.Value] = RepositionCooldown,
        [AttackCooldown.Value] = AttackCooldown,
    };

    private static readonly IReadOnlyDictionary<string, StatKey> LegacyAliasToCanonical = new Dictionary<string, StatKey>(StringComparer.Ordinal)
    {
        [Attack.Value] = PhysPower,
        [Defense.Value] = Armor,
        [Speed.Value] = AttackSpeed,
    };

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<StatKey>> CanonicalToLegacyAliases = new Dictionary<string, IReadOnlyList<StatKey>>(StringComparer.Ordinal)
    {
        [PhysPower.Value] = new[] { Attack },
        [Armor.Value] = new[] { Defense },
        [AttackSpeed.Value] = new[] { Speed },
    };

    public override string ToString() => Value;

    public bool IsLegacyAlias => LegacyAliasToCanonical.ContainsKey(Value);

    public StatKey Canonicalized => Canonicalize(this);

    public static IReadOnlyCollection<string> SupportedIds => CanonicalById.Keys.Concat(LegacyAliasToCanonical.Keys).ToArray();

    public static bool TryResolve(string id, out StatKey key)
    {
        var result = TryResolve(id, out key, out _);
        return result;
    }

    public static bool TryResolve(string id, out StatKey key, out bool isLegacyAlias)
    {
        if (CanonicalById.TryGetValue(id, out key))
        {
            isLegacyAlias = false;
            return true;
        }

        if (LegacyAliasToCanonical.TryGetValue(id, out key))
        {
            isLegacyAlias = true;
            return true;
        }

        key = default;
        isLegacyAlias = false;
        return false;
    }

    public static StatKey Canonicalize(StatKey key)
    {
        return LegacyAliasToCanonical.TryGetValue(key.Value, out var canonical)
            ? canonical
            : key;
    }

    public static IReadOnlyList<StatKey> GetEquivalentKeys(StatKey key)
    {
        var canonical = Canonicalize(key);
        var equivalent = new List<StatKey> { canonical };
        if (CanonicalToLegacyAliases.TryGetValue(canonical.Value, out var aliases))
        {
            equivalent.AddRange(aliases);
        }

        return equivalent;
    }
}
