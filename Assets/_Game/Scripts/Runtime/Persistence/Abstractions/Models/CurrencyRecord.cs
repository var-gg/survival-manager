using System;

namespace SM.Persistence.Abstractions.Models;

[Serializable]
public sealed class CurrencyRecord
{
    public int Gold;
    public int TraitRerollCurrency;
    public int TraitLockToken;
    public int TraitPurgeToken;
    public int EmberDust;
    public int EchoCrystal;
    public int BossSigil;
}
