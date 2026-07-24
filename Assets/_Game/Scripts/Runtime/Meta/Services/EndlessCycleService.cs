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
    /// <summary>Heat 1당 적 최대체력 증가율(Increased 합산).</summary>
    public const float HeatMaxHealthIncreasedPerHeat = 0.10f;

    /// <summary>Heat 1당 적 공격력(물리/마법) 증가율(Increased 합산).</summary>
    public const float HeatPowerIncreasedPerHeat = 0.06f;

    /// <summary>Heat 1당 잔향(Echo) 보상 증가율.</summary>
    public const float HeatEchoBonusPerHeat = 0.15f;

    /// <summary>Heat 드랍 latent mean 이동식의 분자 계수.</summary>
    public const double HeatDropLatentMeanNumerator = 0.12d;

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
        return new[]
        {
            new CombatModifierPackage(sourceId, ModifierSource.Other, new[]
            {
                new StatModifier(StatKey.MaxHealth, ModifierOp.Increased, HeatMaxHealthIncreasedPerHeat * heat, ModifierSource.Other, sourceId),
                new StatModifier(StatKey.PhysPower, ModifierOp.Increased, HeatPowerIncreasedPerHeat * heat, ModifierSource.Other, sourceId),
                new StatModifier(StatKey.MagPower, ModifierOp.Increased, HeatPowerIncreasedPerHeat * heat, ModifierSource.Other, sourceId),
            }),
        };
    }

    /// <summary>
    /// Heat에 따른 드랍 등급 latent mean 이동량. heat &lt;= 0이면 정확히 0을 반환해
    /// 캠페인 드랍 확률 경로를 byte-identical하게 보존한다.
    /// </summary>
    public static double DropLatentMeanShift(int heat)
    {
        if (heat <= 0)
        {
            return 0d;
        }

        return (HeatDropLatentMeanNumerator * heat)
               / (1d + (HeatDropLatentMeanDenominatorSlope * heat));
    }

    /// <summary>
    /// 저장된 campaign jackpot weight를 변이하지 않고 Heat용 로컬 weight를 계산한다.
    /// heat &lt;= 0이면 입력값을 그대로 반환해 campaign 경로를 보존한다.
    /// </summary>
    public static double DropJackpotWeight(double campaignWeight, int heat)
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

        return Math.Min(
            Math.Min(
                campaignWeight + (HeatDropJackpotWeightStep * heat),
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
