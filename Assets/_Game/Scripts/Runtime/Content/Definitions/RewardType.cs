namespace SM.Content.Definitions;

public enum RewardType
{
    Gold = 0,
    TemporaryAugment = 1,
    Echo = 2,
    TraitRerollCurrency = Echo,
    Item = 3,
    // Deprecated in the normal playable lane. Kept for legacy content compatibility only.
    PermanentAugmentSlot = 4,
    TraitLockToken = 5,
    TraitPurgeToken = 6,
    EmberDust = 7,
    EchoCrystal = 8,
    BossSigil = 9,
    SkillManual = 10,
    SkillShard = 11,
}
