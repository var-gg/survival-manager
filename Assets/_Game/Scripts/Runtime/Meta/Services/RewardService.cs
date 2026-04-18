using System.Collections.Generic;
using System.Linq;
using SM.Core.Content;
using SM.Meta.Model;

namespace SM.Meta.Services;

public static class RewardService
{
    public static void ApplyRewardPick(CurrencyState currencyState, ExpeditionState expeditionState, IReadOnlyList<RewardOption> options, RewardPick pick)
    {
        var selected = options.Single(x => x.Id == pick.RewardId);
        switch (selected.Type)
        {
            case RewardType.Gold:
                currencyState.AddGold(selected.Amount);
                break;
            case RewardType.TemporaryAugment:
                expeditionState.AddTemporaryAugment(selected.Id);
                break;
            case RewardType.Echo:
                currencyState.AddEcho(selected.Amount);
                break;
        }
    }
}
