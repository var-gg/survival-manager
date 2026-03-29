namespace SM.Core.Stats;

public readonly record struct StatKey(string Value)
{
    public override string ToString() => Value;

    public static readonly StatKey MaxHealth = new("max_health");
    public static readonly StatKey Attack = new("attack");
    public static readonly StatKey Defense = new("defense");
    public static readonly StatKey Speed = new("speed");
    public static readonly StatKey HealPower = new("heal_power");
    public static readonly StatKey MoveSpeed = new("move_speed");
    public static readonly StatKey AttackRange = new("attack_range");
    public static readonly StatKey AggroRadius = new("aggro_radius");
    public static readonly StatKey AttackWindup = new("attack_windup");
    public static readonly StatKey AttackCooldown = new("attack_cooldown");
    public static readonly StatKey LeashDistance = new("leash_distance");
    public static readonly StatKey TargetSwitchDelay = new("target_switch_delay");
}
