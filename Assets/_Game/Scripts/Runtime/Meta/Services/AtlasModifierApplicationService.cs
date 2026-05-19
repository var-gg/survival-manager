using System;
using System.Globalization;

namespace SM.Meta.Services;

/// <summary>
/// Atlas RewardBias/ThreatPressure/AffinityBoost percent를 cycle 흐름의 3 surface(Reward preview /
/// Battle threat band / Affinity recommendation)에 매핑하는 pure service.
/// task-atlas-modifier-application-v1 acceptance #1·#2·#3·#6.
///
/// Sigil은 named augment를 직접 부여하지 않고 16 augment live count를 변경하지 않는다 —
/// caller는 이 service 결과를 reward preview weight, battle summary risk band,
/// affinity reason text에만 적용한다.
/// </summary>
public static class AtlasModifierApplicationService
{
    /// <summary>
    /// RewardBiasPercent를 reward preview weight scalar로 변환한다.
    /// base weight × (1 + percent/100) — 보상 가중 적용. clamped to non-negative.
    /// </summary>
    public static int ComputeRewardPreviewWeight(int basePreviewWeight, int rewardBiasPercent)
    {
        if (basePreviewWeight <= 0)
        {
            return 0;
        }

        var multiplier = 1.0 + Math.Max(-100, rewardBiasPercent) / 100.0;
        var scaled = (int)Math.Round(basePreviewWeight * multiplier, MidpointRounding.AwayFromZero);
        return Math.Max(0, scaled);
    }

    /// <summary>
    /// ThreatPressurePercent를 Battle setup summary의 risk band로 변환한다.
    /// 0~15: normal · 16~40: elevated · 41~75: high · 76+: severe.
    /// negative는 normal로 clamp.
    /// </summary>
    public static AtlasThreatBand ComputeThreatBand(int threatPressurePercent)
    {
        if (threatPressurePercent <= 15)
        {
            return AtlasThreatBand.Normal;
        }

        if (threatPressurePercent <= 40)
        {
            return AtlasThreatBand.Elevated;
        }

        if (threatPressurePercent <= 75)
        {
            return AtlasThreatBand.High;
        }

        return AtlasThreatBand.Severe;
    }

    /// <summary>
    /// AffinityBoostPercent를 recommendation reason text로 변환한다 — combat stat 암묵 변환 없음.
    /// </summary>
    public static string ComputeAffinityRecommendationReason(int affinityBoostPercent)
    {
        if (affinityBoostPercent <= 0)
        {
            return string.Empty;
        }

        return string.Format(
            CultureInfo.InvariantCulture,
            "인연 보정 +{0}%",
            affinityBoostPercent);
    }

    /// <summary>
    /// 무효/zero modifier 입력에 대한 default summary. caller는 stale payload를 남기지 않는다.
    /// </summary>
    public static AtlasModifierApplicationSummary BuildSummary(int rewardBiasPercent, int threatPressurePercent, int affinityBoostPercent)
    {
        return new AtlasModifierApplicationSummary(
            RewardWeightMultiplierPercent: Math.Max(-100, rewardBiasPercent),
            ThreatBand: ComputeThreatBand(threatPressurePercent),
            AffinityReason: ComputeAffinityRecommendationReason(affinityBoostPercent));
    }
}

public enum AtlasThreatBand
{
    Normal = 0,
    Elevated = 1,
    High = 2,
    Severe = 3,
}

/// <summary>
/// 한 node의 modifier 3채널을 Reward/Battle/Affinity surface 표시용으로 정리한 read-only summary.
/// caller(RewardScreenPresenter, BattleScreenPresenter, AtlasScreenPresenter)는 이 summary를 직접 표시.
/// </summary>
public sealed record AtlasModifierApplicationSummary(
    int RewardWeightMultiplierPercent,
    AtlasThreatBand ThreatBand,
    string AffinityReason)
{
    public static AtlasModifierApplicationSummary Empty { get; } = new(
        RewardWeightMultiplierPercent: 0,
        ThreatBand: AtlasThreatBand.Normal,
        AffinityReason: string.Empty);

    public bool IsPopulated =>
        RewardWeightMultiplierPercent != 0
        || ThreatBand != AtlasThreatBand.Normal
        || !string.IsNullOrWhiteSpace(AffinityReason);
}
