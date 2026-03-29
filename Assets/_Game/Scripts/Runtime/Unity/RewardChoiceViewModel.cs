namespace SM.Unity;

public enum RewardChoiceKind
{
    Gold = 0,
    Item = 1,
    TemporaryAugment = 2,
    TraitRerollCurrency = 3,
    PermanentAugmentSlot = 4
}

public sealed record RewardChoiceViewModel(
    RewardChoiceKind Kind,
    string Title,
    string Description,
    int GoldAmount,
    int TraitRerollAmount,
    int PermanentSlotAmount,
    string PayloadId);
