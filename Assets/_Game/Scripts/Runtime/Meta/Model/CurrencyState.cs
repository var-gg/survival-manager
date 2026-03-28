namespace SM.Meta.Model;

public sealed class CurrencyState
{
    public int Gold { get; private set; }
    public int TraitRerollCurrency { get; private set; }

    public CurrencyState(int gold = 0, int traitRerollCurrency = 0)
    {
        Gold = gold;
        TraitRerollCurrency = traitRerollCurrency;
    }

    public void AddGold(int amount) => Gold += amount;
    public void AddTraitRerollCurrency(int amount) => TraitRerollCurrency += amount;
}
