using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Meta.Model;

namespace SM.Meta.Services;

public static class CombatStatusRuleCompiler
{
    public static CombatStatusRules Compile(CombatContentSnapshot content)
    {
        if (content == null)
        {
            return CombatStatusRules.Default;
        }

        var families = (content.StatusFamilies ?? new Dictionary<string, StatusFamilyTemplate>())
            .Values
            .Where(family => family != null && !string.IsNullOrWhiteSpace(family.Id))
            .Select(family => new CombatStatusFamilyRule(
                family.Id,
                family.Group,
                family.IsHardControl,
                family.UsesControlDiminishing,
                family.AffectedByTenacity,
                family.TenacityScale,
                family.AppliesPeriodicDamage,
                family.IsRuleModifierOnly,
                family.CompileTags ?? Array.Empty<string>(),
                family.VfxCueId ?? string.Empty,
                family.IncomingDamageDelta,
                family.MagnitudeScale,
                family.GrantsBarrierOnApply,
                family.GrantsUnstoppable,
                family.BlocksActiveSkills,
                family.BlocksMovement,
                family.BlocksAction,
                family.AmplifiesIncomingDamage,
                family.GrantsGuardedDefense,
                family.ShredsDefense,
                family.ReducesHealing,
                family.DampensTempo,
                family.MarksTarget,
                // 콤보 payoff는 전투 튜닝 노브(코드 SoT=Default, 콤보 window/ICD·타게팅 bias와 동종) —
                // StatusFamilyTemplate/asset 저작면 밖이다. content 경로가 family를 저작하면 payoff는
                // 여기서 Default를 id로 overlay해 실게임에 실린다. content-lane 승격은 seed-sync 감사와 함께(후속).
                ComboPayoffDamageBonus: DefaultComboPayoff(family.Id),
                MagnitudeUnit: family.MagnitudeUnit))
            .ToDictionary(rule => rule.Id, StringComparer.Ordinal);
        var cleanses = (content.CleanseProfiles ?? new Dictionary<string, CleanseProfileTemplate>())
            .Values
            .Where(profile => profile != null && !string.IsNullOrWhiteSpace(profile.Id))
            .Select(profile => new CombatCleanseProfileRule(
                profile.Id,
                profile.RemovesStatusIds ?? Array.Empty<string>(),
                profile.RemovesOneHardControl,
                profile.GrantsUnstoppable,
                profile.GrantedUnstoppableDurationSeconds,
                string.IsNullOrWhiteSpace(profile.GrantedStatusId) ? "unstoppable" : profile.GrantedStatusId))
            .ToDictionary(rule => rule.Id, StringComparer.Ordinal);
        var control = (content.ControlDiminishingRules ?? new Dictionary<string, ControlDiminishingTemplate>())
            .Values
            .Where(rule => rule != null && !string.IsNullOrWhiteSpace(rule.Id))
            .Select(rule => new CombatControlDiminishingRule(
                rule.Id,
                rule.ControlResistMultiplier,
                rule.WindowSeconds,
                rule.FullTenacityStatusIds ?? Array.Empty<string>(),
                rule.PartialTenacityStatusIds ?? Array.Empty<string>()))
            .FirstOrDefault();

        return new CombatStatusRules(
            families.Count > 0 ? families : CombatStatusRules.Default.StatusFamilies,
            cleanses.Count > 0 ? cleanses : CombatStatusRules.Default.CleanseProfiles,
            control ?? CombatStatusRules.Default.ControlDiminishing);
    }

    // 콤보 payoff 배율은 Default(코드 SoT)에만 저작된다. compiler가 content template로 family를
    // 재구성할 때 id가 일치하는 Default family의 payoff를 실어, 실게임 compiled 경로에서도 추가타가 산다.
    // 미등록 family는 0(현행 보존).
    private static float DefaultComboPayoff(string statusId)
        => CombatStatusRules.Default.TryGetStatusFamily(statusId, out var rule) ? rule.ComboPayoffDamageBonus : 0f;
}
