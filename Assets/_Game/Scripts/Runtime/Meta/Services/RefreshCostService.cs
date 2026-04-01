using SM.Meta.Model;

namespace SM.Meta.Services;

public static class RefreshCostService
{
    public static int GetRefreshCost(RecruitPhaseState phaseState)
    {
        if (phaseState.FreeRefreshesRemaining > 0)
        {
            return 0;
        }

        return phaseState.PaidRefreshCountThisPhase switch
        {
            0 => 2,
            1 => 4,
            _ => 6,
        };
    }

    public static RecruitPhaseState ConsumeRefresh(RecruitPhaseState phaseState)
    {
        var updated = phaseState.Clone();
        if (updated.FreeRefreshesRemaining > 0)
        {
            updated.FreeRefreshesRemaining--;
        }
        else
        {
            updated.PaidRefreshCountThisPhase++;
        }

        return updated;
    }
}
