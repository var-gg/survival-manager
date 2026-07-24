using System;
using System.Collections.Generic;
using SM.Combat.Model;
using SM.Core.Stats;

namespace SM.Meta.Services;

/// <summary>
/// 무한 순환(endless cycle)의 단일 truth 계산기 — 사이클 전이, Heat→적 강화 package, Heat→보상 스케일.
/// 전부 순수(입력 불변). 세션 계층은 전이 시점과 영속만 소유하고 규칙은 여기서 읽는다.
///
/// V1 규칙: 사이클 1회 = 원정 1회. Heat = CycleIndex (사이클마다 1씩 상승, 선택형 Pact는 V2).
/// 적 강화는 정치 조건과 같은 채널(CombatModifierPackage → PoliticalCombatConditionService.ApplyEnemyPackages)로
/// 접힌다 — 콘텐츠 tier 복제(저작 폭발)나 BaseStats 직접 변조(provenance 파괴) 대신 modifier 주입.
/// </summary>
public static class EndlessCycleService
{
    /// <summary>Heat 1당 적 최대체력 증가율(Increased 합산). 현재 shipped 규칙을 그대로 유지한다.</summary>
    public const float HeatMaxHealthIncreasedPerHeat = 0.10f;

    /// <summary>
    /// 향후 최대체력 증가 cap을 위한 knob. shipped 규칙은 uncapped이므로 모든 유효 int Heat에서
    /// <c>Math.Min</c>이 입력 Heat를 그대로 반환하도록 사실상 비활성화한다.
    /// </summary>
    public const int HeatMaxHealthCapHeat = int.MaxValue;

    /// <summary>Heat 1당 primary-target 공격력(물리/마법) 증가율(Increased 합산). 현재 shipped 규칙.</summary>
    public const float HeatPrimaryPowerIncreasedPerHeat = 0.06f;

    /// <summary>
    /// 계산된 secondary-pressure remainder에 적용할 오너 ratifiable 배율. 실패한 difficulty arm은
    /// 인프라와 계측만 보존하고, shipped 동작을 유지하기 위해 현재 0으로 비활성화한다.
    /// </summary>
    public const float HeatSecondaryPressureScale = 0.0f;

    private const double ShippedHeatMaxHealthIncreasedPerHeat = 0.10d;
    private const double ShippedHeatPowerIncreasedPerHeat = 0.06d;

    /// <summary>Heat 1당 잔향(Echo) 보상 증가율.</summary>
    public const float HeatEchoBonusPerHeat = 0.15f;

    /// <summary>
    /// Heat 드랍 latent mean 이동식의 owner-ratifiable 분자 계수.
    /// 2026-07-25의 32 seed x 3 canonical squad reward grid에서 jackpot step 0.002와 함께 0.15를 선택했다.
    /// </summary>
    public const double HeatDropLatentMeanNumerator = 0.15d;

    /// <summary>Heat 드랍 latent mean 이동식의 포화 기울기.</summary>
    public const double HeatDropLatentMeanDenominatorSlope = 0.15d;

    /// <summary>Heat 1당 jackpot 성분의 절대 weight 증가량.</summary>
    public const double HeatDropJackpotWeightStep = 0.002d;

    /// <summary>Heat가 추가할 수 있는 jackpot weight의 최대 절대 증가량.</summary>
    public const double HeatDropJackpotWeightDeltaCap = 0.10d;

    /// <summary>Heat 적용 후 jackpot weight의 절대 상한.</summary>
    public const double HeatDropJackpotWeightAbsoluteCap = 0.20d;

    /// <summary>
    /// 다음 사이클 상태를 계산한다. Modifiers는 새 dict로 복사 — 공유 static Empty의
    /// dict를 in-place 변이하면 프로세스 전역이 오염된다(세이브 로드 Empty 오염과 동계열 함정).
    /// </summary>
    public static EndlessCycleStateRecord BeginNextCycle(EndlessCycleStateRecord current)
    {
        var safe = current ?? EndlessCycleStateRecord.Empty;
        var nextIndex = Math.Max(0, safe.CycleIndex) + 1;
        return new EndlessCycleStateRecord
        {
            CycleIndex = nextIndex,
            Heat = nextIndex,
            Modifiers = new Dictionary<string, int>(safe.Modifiers, StringComparer.Ordinal),
        };
    }

    /// <summary>
    /// Heat에 비례하는 적 강화 package. heat &lt;= 0이면 빈 목록(스토리 경로 byte-identical 보존).
    /// sourceId에 heat 값을 박아 전투 로그/리플레이에서 강화 출처가 읽히게 한다.
    /// </summary>
    public static IReadOnlyList<CombatModifierPackage> BuildEnemyHeatPackages(int heat)
    {
        if (heat <= 0)
        {
            return Array.Empty<CombatModifierPackage>();
        }

        var sourceId = $"endless_heat:h{heat}";
        var cappedHealthHeat = Math.Min(heat, HeatMaxHealthCapHeat);
        return new[]
        {
            new CombatModifierPackage(sourceId, ModifierSource.Other, new[]
            {
                new StatModifier(StatKey.MaxHealth, ModifierOp.Increased, HeatMaxHealthIncreasedPerHeat * cappedHealthHeat, ModifierSource.Other, sourceId),
                new StatModifier(StatKey.PhysPower, ModifierOp.Increased, HeatPrimaryPowerIncreasedPerHeat * heat, ModifierSource.Other, sourceId),
                new StatModifier(StatKey.MagPower, ModifierOp.Increased, HeatPrimaryPowerIncreasedPerHeat * heat, ModifierSource.Other, sourceId),
            }),
        };
    }

    /// <summary>
    /// StatModifier로 표현할 수 없는 secondary-pressure 규칙 package. numeric Heat package와 같은
    /// sourceId를 사용해 combat snapshot이 pre-Heat action budget을 재구성할 수 있게 한다.
    /// </summary>
    public static IReadOnlyList<CombatRuleModifierPackage> BuildEnemyHeatSecondaryPressurePackages(int heat)
    {
        var fraction = SecondaryPressureFraction(heat);
        // Scale 0은 source/package를 만들기 전에 여기서 끝나므로 모든 Heat가 shipped action path를 유지한다.
        if (fraction <= 0f)
        {
            return Array.Empty<CombatRuleModifierPackage>();
        }

        var sourceId = $"endless_heat:h{heat}";
        return new[]
        {
            new CombatRuleModifierPackage(sourceId, ModifierSource.Other, new[]
            {
                new RuleModifier(
                    RuleModifierKind.SecondaryPressure,
                    "equal-non-primary",
                    fraction),
            }),
        };
    }

    /// <summary>
    /// Shipped aggregate exposure proxy에서 capped HP와 primary power를 뺀 pre-Heat raw damage fraction.
    /// heat &lt;= 0은 정확히 0이라 story/H0 action path에 event나 RNG 소비를 추가하지 않는다.
    /// </summary>
    public static float SecondaryPressureFraction(int heat)
    {
        if (heat <= 0)
        {
            return 0f;
        }

        var shippedBudget = (1d + (ShippedHeatMaxHealthIncreasedPerHeat * heat))
                            * (1d + (ShippedHeatPowerIncreasedPerHeat * heat));
        var healthMultiplier = 1d
                               + (HeatMaxHealthIncreasedPerHeat
                                  * Math.Min(heat, HeatMaxHealthCapHeat));
        var primaryMultiplier = 1d + (HeatPrimaryPowerIncreasedPerHeat * heat);
        var remainder = Math.Max(0d, (shippedBudget / healthMultiplier) - primaryMultiplier);
        return (float)(remainder * HeatSecondaryPressureScale);
    }

    /// <summary>
    /// Heat에 따른 드랍 등급 latent mean 이동량. heat &lt;= 0이면 정확히 0을 반환해
    /// 캠페인 드랍 확률 경로를 byte-identical하게 보존한다.
    /// </summary>
    public static double DropLatentMeanShift(int heat)
        => DropLatentMeanShift(heat, HeatDropLatentMeanNumerator);

    internal static double DropLatentMeanShift(
        int heat,
        double meanNumerator)
    {
        if (heat <= 0)
        {
            return 0d;
        }

        if (!double.IsFinite(meanNumerator) || meanNumerator < 0d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(meanNumerator),
                meanNumerator,
                "Heat drop latent mean numerator must be finite and non-negative.");
        }

        return (meanNumerator * heat)
               / (1d + (HeatDropLatentMeanDenominatorSlope * heat));
    }

    /// <summary>
    /// 저장된 campaign jackpot weight를 변이하지 않고 Heat용 로컬 weight를 계산한다.
    /// heat &lt;= 0이면 입력값을 그대로 반환해 campaign 경로를 보존한다.
    /// </summary>
    public static double DropJackpotWeight(double campaignWeight, int heat)
        => DropJackpotWeight(campaignWeight, heat, HeatDropJackpotWeightStep);

    internal static double DropJackpotWeight(
        double campaignWeight,
        int heat,
        double jackpotWeightStep)
    {
        if (!double.IsFinite(campaignWeight)
            || campaignWeight < 0d
            || campaignWeight >= 1d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(campaignWeight),
                campaignWeight,
                "Campaign jackpot weight must be finite and in [0, 1).");
        }

        if (heat <= 0)
        {
            return campaignWeight;
        }

        if (!double.IsFinite(jackpotWeightStep) || jackpotWeightStep < 0d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(jackpotWeightStep),
                jackpotWeightStep,
                "Heat drop jackpot weight step must be finite and non-negative.");
        }

        return Math.Min(
            Math.Min(
                campaignWeight + (jackpotWeightStep * heat),
                campaignWeight + HeatDropJackpotWeightDeltaCap),
            HeatDropJackpotWeightAbsoluteCap);
    }

    /// <summary>잔향(Echo) 보상을 Heat에 비례해 스케일한다. heat &lt;= 0 또는 0 이하 금액은 원값 유지.</summary>
    public static int ScaleEchoAmount(int baseAmount, int heat)
    {
        if (heat <= 0 || baseAmount <= 0)
        {
            return baseAmount;
        }

        return baseAmount + (int)Math.Round(baseAmount * HeatEchoBonusPerHeat * heat, MidpointRounding.AwayFromZero);
    }
}
