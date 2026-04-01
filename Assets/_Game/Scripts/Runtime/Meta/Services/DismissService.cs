using SM.Meta.Model;

namespace SM.Meta.Services;

public sealed record DismissRefundResult(
    int GoldRefund,
    int EchoRefund);

public static class DismissService
{
    public static DismissRefundResult CalculateRefund(UnitEconomyFootprint footprint)
    {
        return new DismissRefundResult(
            footprint.RecruitGoldPaid / 2,
            footprint.RetrainEchoPaid / 2);
    }
}
