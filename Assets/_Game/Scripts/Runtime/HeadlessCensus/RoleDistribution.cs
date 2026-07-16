namespace SM.HeadlessCensus;

public sealed record RoleDistribution(
    int TankCount,
    int DamageCount,
    int RangedCount,
    int HealerCount)
{
    public int Total => TankCount + DamageCount + RangedCount + HealerCount;

    public bool IsRoleComplete => TankCount == 1
                                  && DamageCount == 1
                                  && RangedCount == 1
                                  && HealerCount == 1;
}
