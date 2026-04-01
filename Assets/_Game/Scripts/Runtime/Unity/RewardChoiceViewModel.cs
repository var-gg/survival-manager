namespace SM.Unity;

public enum RewardChoiceKind
{
    Gold = 0,
    Item = 1,
    TemporaryAugment = 2,
    Echo = 3,
    TraitRerollCurrency = Echo,
    PermanentAugmentSlot = 4
}

public sealed record RewardChoiceViewModel(
    RewardChoiceKind Kind,
    string TitleKey,
    string DescriptionKey,
    int GoldAmount,
    int EchoAmount,
    int PermanentSlotAmount,
    string PayloadId)
{
    public int TraitRerollAmount => EchoAmount;
}
