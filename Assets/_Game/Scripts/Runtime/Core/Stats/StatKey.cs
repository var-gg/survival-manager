namespace SM.Core.Stats;

public readonly record struct StatKey(string Value)
{
    public override string ToString() => Value;

    public static readonly StatKey MaxHealth = new("max_health");
    public static readonly StatKey Attack = new("attack");
    public static readonly StatKey Defense = new("defense");
    public static readonly StatKey Speed = new("speed");
    public static readonly StatKey HealPower = new("heal_power");
}
