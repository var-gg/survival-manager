using System;
using System.Collections.Generic;

namespace SM.Persistence.Abstractions.Models;

[Serializable]
public sealed class SaveProfile
{
    public string ProfileId = "default";
    public string DisplayName = "Player";
    public List<HeroInstanceRecord> Heroes = new();
    public List<InventoryItemRecord> Inventory = new();
    public CurrencyRecord Currencies = new();
    public List<string> UnlockedPermanentAugmentIds = new();
    public List<RunSummaryRecord> RunSummaries = new();
}
