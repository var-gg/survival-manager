using System;
using System.Collections.Generic;
using SM.Combat.Model;
using SM.Core.Stats;
using SM.Meta.Model;

namespace SM.Meta.Services;

/// <summary>
/// 저작된 campaign chapter envelope를 endless Heat와 동일한 적 modifier package 채널로 변환한다.
/// BaseStats는 건드리지 않으며 chapter 1은 site order와 무관하게 완전히 inert하다.
/// </summary>
public static class CampaignEnvelopeService
{
    // 힐-삶 난이도 오버홀(gpt-pro 방향): 기존 1.14/1.10 실링이 후반 엔벌롭을 강제로 밋밋하게
    // 만들어 ch2+ 보스를 트리비얼화했다. Mid 프로파일(ch5 HP 1.55/ATK 1.35)이 물도록 절대상한까지 개방.
    public const float HpHardCap = 1.85f;
    public const float AtkHardCap = 1.52f;

    public static IReadOnlyList<CombatModifierPackage> BuildEnemyChapterPackages(
        int chapterOrder,
        CampaignChapterBalanceTemplate? balance,
        int siteOrder = 1)
    {
        var safe = balance ?? CampaignChapterBalanceTemplate.Inert;
        if (chapterOrder <= 1 || safe.HpEnvelope <= 1f && safe.AtkEnvelope <= 1f)
        {
            return Array.Empty<CombatModifierPackage>();
        }

        var siteStepCount = Math.Max(0, siteOrder - 1);
        var hpMultiplier = Math.Min(
            HpHardCap,
            Math.Max(1f, safe.HpEnvelope) * (float)Math.Pow(1f + Math.Max(0f, safe.SiteHpStep), siteStepCount));
        var atkMultiplier = Math.Min(
            AtkHardCap,
            Math.Max(1f, safe.AtkEnvelope) * (float)Math.Pow(1f + Math.Max(0f, safe.SiteAtkStep), siteStepCount));
        var sourceId = $"campaign_envelope:c{chapterOrder}";
        var modifiers = new List<StatModifier>(3);
        if (hpMultiplier > 1f)
        {
            modifiers.Add(new StatModifier(
                StatKey.MaxHealth,
                ModifierOp.Increased,
                hpMultiplier - 1f,
                ModifierSource.Other,
                sourceId));
        }

        if (atkMultiplier > 1f)
        {
            var increased = atkMultiplier - 1f;
            modifiers.Add(new StatModifier(StatKey.PhysPower, ModifierOp.Increased, increased, ModifierSource.Other, sourceId));
            modifiers.Add(new StatModifier(StatKey.MagPower, ModifierOp.Increased, increased, ModifierSource.Other, sourceId));
        }

        return modifiers.Count == 0
            ? Array.Empty<CombatModifierPackage>()
            : new[] { new CombatModifierPackage(sourceId, ModifierSource.Other, modifiers) };
    }
}
