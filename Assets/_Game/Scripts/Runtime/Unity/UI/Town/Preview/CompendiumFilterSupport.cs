using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Meta.Model;

namespace SM.Unity.UI.Town.Preview;

internal sealed class CompendiumFilterSupport
{
    private const string AnyFilterValue = "";

    private readonly ContentTextResolver _contentText;
    private readonly Func<string, string, object[], string> _localize;

    public CompendiumFilterSupport(ContentTextResolver contentText, Func<string, string, object[], string> localize)
    {
        _contentText = contentText ?? throw new ArgumentNullException(nameof(contentText));
        _localize = localize ?? throw new ArgumentNullException(nameof(localize));
    }

    public CompendiumFilterBarViewState BuildFilterBar(
        CombatContentSnapshot snapshot,
        CompendiumTab activeTab,
        string searchText,
        string classFilter,
        string slotFilter,
        string vfxFamilyFilter,
        int shownCount,
        int totalCount)
    {
        return new CompendiumFilterBarViewState(
            ShowSkillFilters: activeTab == CompendiumTab.Skills,
            SearchText: searchText,
            SearchPlaceholder: Localize("ui.town.compendium.filter.search", "검색"),
            ClassLabel: Localize("ui.town.compendium.filter.class", "클래스"),
            ClassValue: classFilter,
            ClassOptions: BuildClassFilterOptions(snapshot),
            SlotLabel: Localize("ui.town.compendium.filter.slot", "슬롯"),
            SlotValue: slotFilter,
            SlotOptions: BuildSlotFilterOptions(snapshot),
            VfxFamilyLabel: Localize("ui.town.compendium.filter.vfx_family", "연출"),
            VfxFamilyValue: vfxFamilyFilter,
            VfxFamilyOptions: BuildVfxFamilyFilterOptions(snapshot),
            ResultSummary: Localize("ui.town.compendium.filter.count", "{0}/{1}", shownCount, totalCount));
    }

    public bool MatchesSkillFilters(BattleSkillSpec skill, string classFilter, string slotFilter, string vfxFamilyFilter)
    {
        if (!string.IsNullOrWhiteSpace(classFilter))
        {
            var tags = skill.RequiredClassTags ?? Array.Empty<string>();
            if (!tags.Contains(classFilter, StringComparer.Ordinal))
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(slotFilter)
            && !string.Equals(CompiledSkillSlots.Normalize(skill.SlotKind), slotFilter, StringComparison.Ordinal))
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(vfxFamilyFilter)
               || string.Equals(skill.EffectivePresentation.Family.ToString(), vfxFamilyFilter, StringComparison.Ordinal);
    }

    public bool MatchesSkillSearch(BattleSkillSpec skill, string searchText)
    {
        var classTags = skill.RequiredClassTags ?? Array.Empty<string>();
        var classLabels = classTags.Select(_contentText.GetClassName).ToArray();
        var statusLabels = (skill.AppliedStatuses ?? Array.Empty<StatusApplicationSpec>())
            .Where(status => !string.IsNullOrWhiteSpace(status.StatusId))
            .Select(status => _contentText.GetStatusName(status.StatusId))
            .ToArray();
        var presentation = skill.EffectivePresentation;
        return ContainsSearch(
            searchText,
            new[]
            {
                skill.Id,
                _contentText.GetSkillName(skill.Id),
                _contentText.GetSkillDescription(skill.Id),
                FormatSlot(skill.SlotKind),
                skill.DamageType.ToString(),
                skill.Delivery.ToString(),
                skill.TargetRule.ToString(),
                skill.VfxHookId,
                presentation.Family.ToString(),
                presentation.Skin.ToString(),
                presentation.Gesture.ToString(),
                presentation.CueSequence.ToString(),
            }
            .Concat(classTags)
            .Concat(classLabels)
            .Concat(statusLabels));
    }

    public bool MatchesStatusSearch(StatusFamilyTemplate status, string searchText)
    {
        return ContainsSearch(
            searchText,
            status.Id,
            _contentText.GetStatusName(status.Id),
            _contentText.GetStatusDescription(status.Id),
            status.Group.ToString(),
            FormatStatusRule(status),
            status.VfxCueId);
    }

    public bool MatchesSynergySearch(CompendiumSynergyViewState synergy, string searchText)
    {
        return ContainsSearch(
            searchText,
            synergy.Id,
            synergy.Name,
            synergy.Description,
            synergy.CountedTagLabel,
            synergy.TierSummary);
    }

    public bool MatchesCharacterSearch(CompendiumCharacterViewState character, string searchText)
    {
        return ContainsSearch(
            searchText,
            character.Id,
            character.DisplayName,
            character.Description,
            character.RaceLabel,
            character.ClassLabel,
            character.RoleLabel,
            character.UnlockLabel);
    }

    public string FormatStatusRule(StatusFamilyTemplate status)
    {
        var flags = new List<string>();
        if (status.IsHardControl) flags.Add(Localize("ui.town.compendium.status_rule.hard_control", "하드 제어"));
        if (status.UsesControlDiminishing) flags.Add(Localize("ui.town.compendium.status_rule.diminishing", "점감"));
        if (status.AffectedByTenacity)
        {
            flags.Add(Localize(
                "ui.town.compendium.status_rule.tenacity",
                "강인함 {0}",
                status.TenacityScale.ToString("0.##")));
        }
        if (status.AppliesPeriodicDamage) flags.Add(Localize("ui.town.compendium.status_rule.periodic_damage", "주기 피해"));
        if (status.IsRuleModifierOnly) flags.Add(Localize("ui.town.compendium.status_rule.rule_modifier", "규칙 보정"));
        return flags.Count == 0 ? "-" : string.Join(", ", flags);
    }

    public string MetricLabel(string key, string fallback)
    {
        return Localize($"ui.town.compendium.metric.{key}", fallback);
    }

    public string FormatSlot(string slot)
    {
        return CompiledSkillSlots.Normalize(slot) switch
        {
            CompiledSkillSlots.CoreActive => Localize("ui.town.compendium.slot.core", "핵심"),
            CompiledSkillSlots.UtilityActive => Localize("ui.town.compendium.slot.utility", "유틸"),
            CompiledSkillSlots.Passive => Localize("ui.town.compendium.slot.passive", "패시브"),
            CompiledSkillSlots.Support => Localize("ui.town.compendium.slot.support", "지원"),
            _ => slot,
        };
    }

    public static int SlotOrder(string slot)
    {
        return CompiledSkillSlots.Normalize(slot) switch
        {
            CompiledSkillSlots.CoreActive => 0,
            CompiledSkillSlots.UtilityActive => 1,
            CompiledSkillSlots.Passive => 2,
            CompiledSkillSlots.Support => 3,
            _ => 9,
        };
    }

    private IReadOnlyList<CompendiumFilterOptionViewState> BuildClassFilterOptions(CombatContentSnapshot snapshot)
    {
        var options = new List<CompendiumFilterOptionViewState>
        {
            new(AnyFilterValue, Localize("ui.town.compendium.filter.all_classes", "전체 클래스")),
        };
        options.AddRange(snapshot.SkillCatalog.Values
            .SelectMany(skill => skill.RequiredClassTags ?? Array.Empty<string>())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(tag => _contentText.GetClassName(tag), StringComparer.Ordinal)
            .Select(tag => new CompendiumFilterOptionViewState(tag, _contentText.GetClassName(tag))));
        return options;
    }

    private IReadOnlyList<CompendiumFilterOptionViewState> BuildSlotFilterOptions(CombatContentSnapshot snapshot)
    {
        var slots = CompiledSkillSlots.Ordered
            .Concat(snapshot.SkillCatalog.Values.Select(skill => CompiledSkillSlots.Normalize(skill.SlotKind)))
            .Where(slot => !string.IsNullOrWhiteSpace(slot))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(SlotOrder)
            .ToArray();
        return new[] { new CompendiumFilterOptionViewState(AnyFilterValue, Localize("ui.town.compendium.filter.all_slots", "전체 슬롯")) }
            .Concat(slots.Select(slot => new CompendiumFilterOptionViewState(slot, FormatSlot(slot))))
            .ToList();
    }

    private IReadOnlyList<CompendiumFilterOptionViewState> BuildVfxFamilyFilterOptions(CombatContentSnapshot snapshot)
    {
        var families = snapshot.SkillCatalog.Values
            .Select(skill => skill.EffectivePresentation.Family)
            .Where(family => family != SkillPresentationFamily.Any)
            .Distinct()
            .OrderBy(family => family.ToString(), StringComparer.Ordinal)
            .ToArray();
        return new[] { new CompendiumFilterOptionViewState(AnyFilterValue, Localize("ui.town.compendium.filter.all_vfx", "전체 연출")) }
            .Concat(families.Select(family => new CompendiumFilterOptionViewState(family.ToString(), family.ToString())))
            .ToList();
    }

    private string Localize(string key, string fallback, params object[] arguments)
    {
        return _localize(key, fallback, arguments);
    }

    private static bool ContainsSearch(string searchText, params string[] values)
    {
        return ContainsSearch(searchText, (IEnumerable<string>)values);
    }

    private static bool ContainsSearch(string searchText, IEnumerable<string> values)
    {
        var query = (searchText ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(query)
               || values.Any(value => !string.IsNullOrWhiteSpace(value)
                                      && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
    }
}
