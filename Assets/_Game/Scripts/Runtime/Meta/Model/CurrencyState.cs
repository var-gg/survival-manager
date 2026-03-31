namespace SM.Meta.Model;

public sealed class CurrencyState
{
    public int Gold { get; private set; }
    public int TraitRerollCurrency { get; private set; }
    public int TraitLockToken { get; private set; }
    public int TraitPurgeToken { get; private set; }
    public int EmberDust { get; private set; }
    public int EchoCrystal { get; private set; }
    public int BossSigil { get; private set; }

    public CurrencyState(
        int gold = 0,
        int traitRerollCurrency = 0,
        int traitLockToken = 0,
        int traitPurgeToken = 0,
        int emberDust = 0,
        int echoCrystal = 0,
        int bossSigil = 0)
    {
        Gold = gold;
        TraitRerollCurrency = traitRerollCurrency;
        TraitLockToken = traitLockToken;
        TraitPurgeToken = traitPurgeToken;
        EmberDust = emberDust;
        EchoCrystal = echoCrystal;
        BossSigil = bossSigil;
    }

    public void AddGold(int amount) => Gold += amount;
    public void AddTraitRerollCurrency(int amount) => TraitRerollCurrency += amount;
    public void AddTraitLockToken(int amount) => TraitLockToken += amount;
    public void AddTraitPurgeToken(int amount) => TraitPurgeToken += amount;
    public void AddEmberDust(int amount) => EmberDust += amount;
    public void AddEchoCrystal(int amount) => EchoCrystal += amount;
    public void AddBossSigil(int amount) => BossSigil += amount;
}
