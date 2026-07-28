using System;
using SM.Combat.Model;
using SM.Core.Content;

namespace SM.Unity.UI.Town.Preview;

internal sealed class CompendiumSkillReadoutFormatter
{
    private readonly Func<string, string, string> _localize;

    public CompendiumSkillReadoutFormatter(Func<string, string, string> localize)
    {
        _localize = localize ?? throw new ArgumentNullException(nameof(localize));
    }

    public string FormatIntent(BattleSkillSpec skill)
    {
        var presentation = skill.EffectivePresentation;
        if (skill.DamageType == DamageType.Healing || skill.Kind == SkillKind.Heal || presentation.Family == SkillPresentationFamily.Heal)
        {
            return _localize("ui.town.compendium.intent.recovery", "회복");
        }

        if (skill.Kind == SkillKind.Shield || presentation.Family == SkillPresentationFamily.Shield)
        {
            return _localize("ui.town.compendium.intent.protection", "보호");
        }

        if (presentation.Family == SkillPresentationFamily.Reposition)
        {
            return _localize("ui.town.compendium.intent.mobility", "기동");
        }

        if (skill.Kind == SkillKind.Debuff
            || presentation.Family == SkillPresentationFamily.Debuff
            || (skill.AppliedStatuses?.Count ?? 0) > 0)
        {
            return _localize("ui.town.compendium.intent.control", "제어");
        }

        if (skill.Kind == SkillKind.Buff
            || presentation.Family is SkillPresentationFamily.Aura or SkillPresentationFamily.PassiveProc)
        {
            return _localize("ui.town.compendium.intent.support", "지원");
        }

        return _localize("ui.town.compendium.intent.damage", "화력");
    }

    public string FormatQuickStats(BattleSkillSpec skill)
    {
        var power = Math.Abs(skill.Power) < 0.001f
            ? "0"
            : skill.Power.ToString("0.##");
        var cooldown = skill.BaseCooldownSeconds <= 0f
            ? _localize("ui.town.compendium.card.no_cooldown", "상시")
            : $"{skill.BaseCooldownSeconds:0.##}s";
        return string.Format(
            _localize("ui.town.compendium.card.quick_stats", "위력 {0} / {1}"),
            power,
            cooldown);
    }

    public string FormatCombatLine(BattleSkillSpec skill)
    {
        return $"{FormatDamage(skill.DamageType)} / {FormatDelivery(skill.Delivery)} / {FormatTarget(skill.TargetRule)}";
    }

    public string FormatKind(SkillKind kind)
    {
        return kind switch
        {
            SkillKind.Strike => _localize("ui.town.compendium.kind.strike", "공격"),
            SkillKind.Heal => _localize("ui.town.compendium.kind.heal", "회복"),
            SkillKind.Shield => _localize("ui.town.compendium.kind.shield", "보호막"),
            SkillKind.Buff => _localize("ui.town.compendium.kind.buff", "강화"),
            SkillKind.Debuff => _localize("ui.town.compendium.kind.debuff", "약화"),
            SkillKind.Utility => _localize("ui.town.compendium.kind.utility", "지원"),
            _ => _localize("ui.town.compendium.kind.unknown", "알 수 없는 유형"),
        };
    }

    public string FormatKind(SkillKindValue kind)
    {
        return kind switch
        {
            SkillKindValue.Strike => _localize("ui.town.compendium.kind.strike", "공격"),
            SkillKindValue.Heal => _localize("ui.town.compendium.kind.heal", "회복"),
            SkillKindValue.Shield => _localize("ui.town.compendium.kind.shield", "보호막"),
            SkillKindValue.Buff => _localize("ui.town.compendium.kind.buff", "강화"),
            SkillKindValue.Debuff => _localize("ui.town.compendium.kind.debuff", "약화"),
            SkillKindValue.Utility => _localize("ui.town.compendium.kind.utility", "지원"),
            _ => _localize("ui.town.compendium.kind.unknown", "알 수 없는 유형"),
        };
    }

    public string FormatDamage(DamageType damage)
    {
        return damage switch
        {
            DamageType.Physical => _localize("ui.town.compendium.damage.physical", "물리"),
            DamageType.Magical => _localize("ui.town.compendium.damage.magical", "마법"),
            DamageType.Healing => _localize("ui.town.compendium.damage.healing", "회복"),
            DamageType.True => _localize("ui.town.compendium.damage.true", "고정"),
            _ => _localize("ui.town.compendium.damage.unknown", "알 수 없는 피해"),
        };
    }

    public string FormatDelivery(SkillDelivery delivery)
    {
        return delivery switch
        {
            SkillDelivery.Melee => _localize("ui.town.compendium.delivery.melee", "근접"),
            SkillDelivery.Ranged => _localize("ui.town.compendium.delivery.ranged", "원거리"),
            SkillDelivery.Projectile => _localize("ui.town.compendium.delivery.projectile", "투사체"),
            SkillDelivery.Nova => _localize("ui.town.compendium.delivery.nova", "폭발"),
            SkillDelivery.Aura => _localize("ui.town.compendium.delivery.aura", "오라"),
            SkillDelivery.Trap => _localize("ui.town.compendium.delivery.trap", "함정"),
            SkillDelivery.Zone => _localize("ui.town.compendium.delivery.zone", "장판"),
            _ => _localize("ui.town.compendium.delivery.unknown", "알 수 없는 전달 방식"),
        };
    }

    public string FormatTarget(SkillTargetRule target)
    {
        return target switch
        {
            SkillTargetRule.NearestEnemy => _localize("ui.town.compendium.target.nearest_enemy", "가까운 적"),
            SkillTargetRule.LowestHpEnemy => _localize("ui.town.compendium.target.lowest_hp_enemy", "약한 적"),
            SkillTargetRule.MostExposedEnemy => _localize("ui.town.compendium.target.exposed_enemy", "노출된 적"),
            SkillTargetRule.LowestHpAlly => _localize("ui.town.compendium.target.lowest_hp_ally", "약한 아군"),
            SkillTargetRule.ProtectedAlly => _localize("ui.town.compendium.target.protected_ally", "보호 대상"),
            SkillTargetRule.Self => _localize("ui.town.compendium.target.self", "자신"),
            SkillTargetRule.MarkedTarget => _localize("ui.town.compendium.target.marked", "표식 대상"),
            _ => _localize("ui.town.compendium.target.unknown", "알 수 없는 대상"),
        };
    }

    public string FormatTarget(SkillTargetRuleValue target)
    {
        return target switch
        {
            SkillTargetRuleValue.NearestEnemy => _localize("ui.town.compendium.target.nearest_enemy", "가까운 적"),
            SkillTargetRuleValue.LowestHpEnemy => _localize("ui.town.compendium.target.lowest_hp_enemy", "약한 적"),
            SkillTargetRuleValue.MostExposedEnemy => _localize("ui.town.compendium.target.exposed_enemy", "노출된 적"),
            SkillTargetRuleValue.LowestHpAlly => _localize("ui.town.compendium.target.lowest_hp_ally", "약한 아군"),
            SkillTargetRuleValue.ProtectedAlly => _localize("ui.town.compendium.target.protected_ally", "보호 대상"),
            SkillTargetRuleValue.Self => _localize("ui.town.compendium.target.self", "자신"),
            SkillTargetRuleValue.MarkedTarget => _localize("ui.town.compendium.target.marked", "표식 대상"),
            _ => _localize("ui.town.compendium.target.unknown", "알 수 없는 대상"),
        };
    }
}
