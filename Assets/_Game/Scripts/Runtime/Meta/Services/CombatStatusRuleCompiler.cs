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
                family.GrantsBarrierOnApply))
            .ToDictionary(rule => rule.Id, StringComparer.Ordinal);
        var cleanses = (content.CleanseProfiles ?? new Dictionary<string, CleanseProfileTemplate>())
            .Values
            .Where(profile => profile != null && !string.IsNullOrWhiteSpace(profile.Id))
            .Select(profile => new CombatCleanseProfileRule(
                profile.Id,
                profile.RemovesStatusIds ?? Array.Empty<string>(),
                profile.RemovesOneHardControl,
                profile.GrantsUnstoppable,
                profile.GrantedUnstoppableDurationSeconds))
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
}
