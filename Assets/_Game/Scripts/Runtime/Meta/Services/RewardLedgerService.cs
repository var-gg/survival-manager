using System;
using SM.Core.Content;
using SM.Meta.Model;

namespace SM.Meta.Services;

public sealed record RewardLedgerResult(
    string Summary,
    RewardLedgerEntry RewardEntry,
    InventoryLedgerEntry? InventoryEntry,
    ActiveRunState UpdatedRun);

public static class RewardLedgerService
{
    public static RewardLedgerResult ApplyReward(
        CurrencyState currencyState,
        ActiveRunState run,
        RewardOption option)
    {
        var timestamp = DateTime.UtcNow.ToString("O");
        InventoryLedgerEntry? inventoryEntry = null;
        var amount = option.Amount;
        var summary = option.SummaryKey;

        switch (option.Type)
        {
            case RewardType.Gold:
                currencyState.AddGold(option.Amount);
                break;
            case RewardType.TemporaryAugment:
                run = RunStateService.ApplyTemporaryAugment(run, option.Id);
                break;
            case RewardType.Echo:
                currencyState.AddEcho(option.Amount);
                break;
        }

        var rewardEntry = new RewardLedgerEntry(
            Guid.NewGuid().ToString("N"),
            run.RunId,
            option.Id,
            option.Type.ToString(),
            amount,
            timestamp,
            summary);

        if (option.Type == RewardType.TemporaryAugment)
        {
            inventoryEntry = new InventoryLedgerEntry(
                Guid.NewGuid().ToString("N"),
                run.RunId,
                option.Id,
                option.Id,
                "run_overlay",
                1,
                timestamp,
                $"temporary augment:{option.Id}");
        }

        return new RewardLedgerResult(summary, rewardEntry, inventoryEntry, run);
    }
}
