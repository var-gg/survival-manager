using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Core.Contracts;
using SM.Core.Stats;
using SM.Meta.Model;

namespace SM.Meta.Services;

public static class RecruitmentTemplateResolver
{
    public static IReadOnlyList<CombatArchetypeTemplate> GetRecruitPool(
        IReadOnlyDictionary<string, CombatArchetypeTemplate> archetypes,
        IReadOnlyList<HeroRecord> roster)
    {
        var ownedBlueprints = new HashSet<string>(roster.Select(hero => hero.ArchetypeId), StringComparer.Ordinal);
        return archetypes.Values
            .Where(template => template.IsRecruitable && !ownedBlueprints.Contains(template.Id))
            .OrderBy(template => template.Id, StringComparer.Ordinal)
            .ToList();
    }

    public static IReadOnlyCollection<string> GetNativeTags(CombatArchetypeTemplate template)
    {
        var tags = new HashSet<string>(StringComparer.Ordinal)
        {
            template.RaceId,
            template.ClassId,
            template.RoleTag,
            template.Behavior?.FormationLine.ToString() ?? string.Empty,
        };

        foreach (var tag in template.RecruitPlanTags ?? Array.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(tag))
            {
                tags.Add(tag);
            }
        }

        if ((template.BaseStats.TryGetValue(StatKey.HealPower, out var healPower) && healPower > 0f)
            || string.Equals(template.RoleTag, "support", StringComparison.Ordinal))
        {
            tags.Add("support");
        }

        var physPower = template.BaseStats.TryGetValue(StatKey.PhysPower, out var physical) ? physical : 0f;
        var magPower = template.BaseStats.TryGetValue(StatKey.MagPower, out var magical) ? magical : 0f;
        if (physPower >= magPower)
        {
            tags.Add("physical");
        }

        if (magPower >= physPower && magPower > 0f)
        {
            tags.Add("magical");
        }

        return tags.Where(tag => !string.IsNullOrWhiteSpace(tag)).ToList();
    }

    public static bool MatchesDirective(CombatArchetypeTemplate template, ScoutDirective directive)
    {
        if (directive == null || directive.IsNone)
        {
            return false;
        }

        var tags = new HashSet<string>(GetNativeTags(template), StringComparer.Ordinal);
        foreach (var scoutTag in template.ScoutBiasTags ?? Array.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(scoutTag))
            {
                tags.Add(scoutTag);
            }
        }

        return directive.Kind switch
        {
            ScoutDirectiveKind.Frontline => template.Behavior?.FormationLine == FormationLine.Frontline || tags.Contains("Frontline") || tags.Contains("frontline"),
            ScoutDirectiveKind.Backline => template.Behavior?.FormationLine == FormationLine.Backline || tags.Contains("Backline") || tags.Contains("backline"),
            ScoutDirectiveKind.Physical => tags.Contains("physical"),
            ScoutDirectiveKind.Magical => tags.Contains("magical"),
            ScoutDirectiveKind.Support => tags.Contains("support"),
            ScoutDirectiveKind.SynergyTag => !string.IsNullOrWhiteSpace(directive.SynergyTagId) && tags.Contains(directive.SynergyTagId),
            _ => false,
        };
    }

    public static bool IsNativeCoherent(CombatArchetypeTemplate template, BattleSkillSpec option)
    {
        return ResolveOptionNativeTags(template, option).Overlaps(GetNativeTags(template));
    }

    public static bool IsPlanCoherent(
        CombatArchetypeTemplate template,
        BattleSkillSpec option,
        TeamPlanProfile plan,
        ScoutDirective? directive = null)
    {
        var optionTags = ResolveOptionPlanTags(template, option);
        if (plan.TopSynergyTagIds.Any(optionTags.Contains))
        {
            return true;
        }

        if (plan.NeedsFrontline && optionTags.Contains("frontline"))
        {
            return true;
        }

        if (plan.NeedsBackline && optionTags.Contains("backline"))
        {
            return true;
        }

        if (plan.NeedsSupport && optionTags.Contains("support"))
        {
            return true;
        }

        if (plan.PrefersPhysical && optionTags.Contains("physical"))
        {
            return true;
        }

        if (plan.PrefersMagical && optionTags.Contains("magical"))
        {
            return true;
        }

        if (plan.AugmentHookTags.Any(optionTags.Contains))
        {
            return true;
        }

        return directive != null && !directive.IsNone && MatchesOptionDirective(optionTags, directive);
    }

    public static bool HasSameFamilyConflict(BattleSkillSpec option, BattleSkillSpec? signatureActive, BattlePassiveSpec? signaturePassive)
    {
        if (!string.IsNullOrWhiteSpace(option.EffectFamilyId)
            && signatureActive != null
            && string.Equals(option.EffectFamilyId, signatureActive.EffectFamilyId, StringComparison.Ordinal))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(option.EffectFamilyId)
               && signaturePassive != null
               && string.Equals(option.EffectFamilyId, signaturePassive.EffectFamilyId, StringComparison.Ordinal);
    }

    public static bool IsBannedPairing(CombatArchetypeTemplate template, string flexActiveId, string flexPassiveId)
    {
        return (template.RecruitBannedPairings ?? Array.Empty<RecruitBannedPairingTemplate>())
            .Any(pairing =>
                string.Equals(pairing.FlexActiveId, flexActiveId, StringComparison.Ordinal)
                && string.Equals(pairing.FlexPassiveId, flexPassiveId, StringComparison.Ordinal));
    }

    public static IReadOnlyList<BattleSkillSpec> GetFlexActivePool(CombatArchetypeTemplate template)
    {
        return (template.RecruitFlexActivePool ?? Array.Empty<BattleSkillSpec>())
            .Where(skill => skill != null && skill.EffectiveSlotKind == ActionSlotKind.FlexActive)
            .ToList();
    }

    public static IReadOnlyList<BattleSkillSpec> GetFlexPassivePool(CombatArchetypeTemplate template)
    {
        return (template.RecruitFlexPassivePool ?? Array.Empty<BattleSkillSpec>())
            .Where(skill => skill != null && skill.EffectiveSlotKind == ActionSlotKind.FlexPassive)
            .ToList();
    }

    private static HashSet<string> ResolveOptionNativeTags(CombatArchetypeTemplate template, BattleSkillSpec option)
    {
        var tags = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tag in option.RecruitNativeTags ?? Array.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(tag))
            {
                tags.Add(tag);
            }
        }

        if (tags.Count == 0)
        {
            foreach (var tag in GetNativeTags(template))
            {
                tags.Add(tag);
            }
        }

        return tags;
    }

    private static HashSet<string> ResolveOptionPlanTags(CombatArchetypeTemplate template, BattleSkillSpec option)
    {
        var tags = new HashSet<string>(ResolveOptionNativeTags(template, option), StringComparer.Ordinal);
        foreach (var tag in option.RecruitPlanTags ?? Array.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(tag))
            {
                tags.Add(tag);
            }
        }

        foreach (var tag in option.RecruitScoutTags ?? Array.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(tag))
            {
                tags.Add(tag);
            }
        }

        return tags;
    }

    private static bool MatchesOptionDirective(IReadOnlyCollection<string> optionTags, ScoutDirective directive)
    {
        return directive.Kind switch
        {
            ScoutDirectiveKind.Frontline => optionTags.Contains("frontline"),
            ScoutDirectiveKind.Backline => optionTags.Contains("backline"),
            ScoutDirectiveKind.Physical => optionTags.Contains("physical"),
            ScoutDirectiveKind.Magical => optionTags.Contains("magical"),
            ScoutDirectiveKind.Support => optionTags.Contains("support"),
            ScoutDirectiveKind.SynergyTag => !string.IsNullOrWhiteSpace(directive.SynergyTagId) && optionTags.Contains(directive.SynergyTagId),
            _ => false,
        };
    }
}
