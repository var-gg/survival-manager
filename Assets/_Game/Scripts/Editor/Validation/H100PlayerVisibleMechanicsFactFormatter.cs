using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SM.HeadlessPolicies;

namespace SM.Editor.Validation;

/// <summary>player-visible mechanics DTO를 hash 입력용 invariant 문자열로만 정규화한다.</summary>
internal static class H100PlayerVisibleMechanicsFactFormatter
{
    public static string Hero(HeadlessHeroObservation hero)
        => Join(
            Pair("hero", hero.HeroId),
            Pair("archetype", hero.ArchetypeId),
            Pair("race", hero.RaceId),
            Pair("class", hero.ClassId),
            Pair("role", hero.RoleTag),
            Pair("level", hero.Level),
            Pair("hp", $"{hero.CurrentHp.ToString(CultureInfo.InvariantCulture)}/{hero.MaxHp.ToString(CultureInfo.InvariantCulture)}"),
            Pair("equipped_items", hero.EquippedItemCount),
            Pair("deployed", hero.IsDeployed),
            Pair("preferred_anchor", hero.PreferredAnchor.ToString()));

    public static string Skill(HeadlessSkillObservation skill)
        => Join(
            Pair("skill", skill.SkillId),
            Pair("kind", skill.Kind.ToString()),
            Pair("slot", skill.SlotKind),
            Pair("power", skill.Power),
            Pair("range", skill.Range),
            Pair("damage_type", skill.DamageType.ToString()),
            Pair("power_flat", skill.PowerFlat),
            Pair("physical_coefficient", skill.PhysicalCoefficient),
            Pair("magical_coefficient", skill.MagicalCoefficient),
            Pair("healing_coefficient", skill.HealingCoefficient),
            Pair("health_coefficient", skill.HealthCoefficient),
            Pair("mana_cost", skill.ManaCost),
            Pair("cooldown_seconds", skill.CooldownSeconds),
            Pair("windup_seconds", skill.WindupSeconds),
            Pair("can_crit", skill.CanCrit),
            Pair("delivery", skill.Delivery.ToString()),
            Pair("target_rule", skill.TargetRule.ToString()),
            Pair("statuses", Sequence(skill.AppliedStatuses
                .OrderBy(value => value.StatusId, StringComparer.Ordinal)
                .ThenBy(value => value.ApplicationId, StringComparer.Ordinal)
                .Select(Status))));

    public static string Item(HeadlessItemMechanicsObservation item)
        => Join(
            Pair("item", item.ItemId),
            Pair("instance", item.ItemInstanceId),
            Pair("tags", Sequence(item.Tags.OrderBy(value => value, StringComparer.Ordinal))),
            Pair("weapon_family", item.WeaponFamilyTag),
            Pair("stat_modifiers", StatModifiers(item.StatModifiers)),
            Pair("affixes", Sequence(item.Affixes
                .OrderBy(value => value.AffixId, StringComparer.Ordinal)
                .Select(Affix))),
            Pair("granted_skills", Sequence(item.GrantedSkills
                .OrderBy(value => value.SkillId, StringComparer.Ordinal)
                .Select(Skill))));

    public static string Augment(HeadlessAugmentMechanicsObservation augment)
        => Join(
            Pair("augment", augment.AugmentId),
            Pair("category", augment.Category),
            Pair("family", augment.FamilyId),
            Pair("tier", augment.Tier),
            Pair("tags", Sequence(augment.Tags.OrderBy(value => value, StringComparer.Ordinal))),
            Pair("build_bias_tags", Sequence(augment.BuildBiasTags.OrderBy(value => value, StringComparer.Ordinal))),
            Pair("stat_modifiers", StatModifiers(augment.StatModifiers)),
            Pair("rule_modifiers", RuleModifiers(augment.RuleModifiers)),
            Pair("triggered_effects", Sequence(augment.TriggeredEffects
                .OrderBy(value => value.Trigger, StringComparer.Ordinal)
                .ThenBy(value => value.Operation, StringComparer.Ordinal)
                .ThenBy(value => value.Scope, StringComparer.Ordinal)
                .ThenBy(value => value.StatusId, StringComparer.Ordinal)
                .Select(TriggeredEffect))));

    public static string Synergy(HeadlessSynergyObservation synergy)
        => Join(
            Pair("synergy", synergy.SynergyId),
            Pair("counted_tag", synergy.CountedTagId),
            Pair("tiers", Sequence(synergy.Tiers
                .OrderBy(value => value.Threshold)
                .ThenBy(value => value.GrantedTeamRuleId, StringComparer.Ordinal)
                .Select(value => Join(
                    Pair("threshold", value.Threshold),
                    Pair("stat_modifiers", StatModifiers(value.StatModifiers)),
                    Pair("team_rule", value.GrantedTeamRuleId))))));

    public static string EnemyPreview(HeadlessEnemyPreview preview)
        => Join(
            Pair("available", preview.IsAvailable),
            Pair("encounter", preview.EncounterId),
            Pair("faction", preview.FactionId),
            Pair("difficulty", preview.DifficultyBand),
            Pair("threat_skulls", preview.ThreatSkulls),
            Pair("units", Sequence(preview.Units.Select(EnemyUnit))),
            Pair("boss_aura", preview.BossAuraTag),
            Pair("boss_utility", preview.BossUtilityTag),
            Pair("reward_drop_tags", Sequence(preview.RewardDropTags.OrderBy(value => value, StringComparer.Ordinal))));

    public static string EnemyUnit(HeadlessEnemyUnitPreview unit)
    {
        var identity = Join(
            Pair("archetype", unit.ArchetypeId),
            Pair("race", unit.RaceId),
            Pair("class", unit.ClassId),
            Pair("role", unit.RoleTag),
            Pair("preferred_anchor", unit.PreferredAnchor.ToString()));
        return unit.EquippedItems.Count == 0
            ? identity
            : Join(
                identity,
                Pair("equipped_items", Sequence(unit.EquippedItems
                    .OrderBy(value => value.ItemId, StringComparer.Ordinal)
                    .Select(Item))));
    }

    public static string Reward(HeadlessRewardOption option)
        => Join(
            Pair("index", option.Index),
            Pair("kind", option.Kind.ToString()),
            Pair("payload", option.PayloadId),
            Pair("gold", option.GoldAmount),
            Pair("echo", option.EchoAmount),
            Pair("permanent_slot", option.PermanentSlotAmount),
            Pair("item_mechanics", option.Mechanics.Item == null ? string.Empty : Item(option.Mechanics.Item)),
            Pair("augment_mechanics", option.Mechanics.TemporaryAugment == null
                ? string.Empty
                : Augment(option.Mechanics.TemporaryAugment)));

    public static string StatModifiers(IEnumerable<HeadlessStatModifierObservation> modifiers)
        => Sequence((modifiers ?? Array.Empty<HeadlessStatModifierObservation>())
            .OrderBy(value => value.StatId, StringComparer.Ordinal)
            .ThenBy(value => value.Operation, StringComparer.Ordinal)
            .ThenBy(value => value.Value)
            .ThenBy(value => value.TagId, StringComparer.Ordinal)
            .Select(value => Join(
                Pair("stat", value.StatId),
                Pair("operation", value.Operation),
                Pair("value", value.Value),
                Pair("tag", value.TagId))));

    private static string RuleModifiers(IEnumerable<HeadlessRuleModifierObservation> modifiers)
        => Sequence((modifiers ?? Array.Empty<HeadlessRuleModifierObservation>())
            .OrderBy(value => value.Kind, StringComparer.Ordinal)
            .ThenBy(value => value.Value, StringComparer.Ordinal)
            .ThenBy(value => value.Magnitude)
            .Select(value => Join(
                Pair("kind", value.Kind),
                Pair("value", value.Value),
                Pair("magnitude", value.Magnitude))));

    private static string Affix(HeadlessAffixMechanicsObservation affix)
        => Join(
            Pair("affix", affix.AffixId),
            Pair("compile_tags", Sequence(affix.CompileTags.OrderBy(value => value, StringComparer.Ordinal))),
            Pair("required_tags", Sequence(affix.RequiredTags.OrderBy(value => value, StringComparer.Ordinal))),
            Pair("excluded_tags", Sequence(affix.ExcludedTags.OrderBy(value => value, StringComparer.Ordinal))),
            Pair("stat_modifiers", StatModifiers(affix.StatModifiers)),
            Pair("rule_modifiers", RuleModifiers(affix.RuleModifiers)));

    private static string TriggeredEffect(HeadlessTriggeredEffectObservation effect)
        => Join(
            Pair("trigger", effect.Trigger),
            Pair("operation", effect.Operation),
            Pair("scope", effect.Scope),
            Pair("magnitude", effect.Magnitude),
            Pair("threshold_ratio", effect.ThresholdRatio),
            Pair("status", effect.StatusId),
            Pair("duration_seconds", effect.DurationSeconds),
            Pair("max_stacks", effect.MaxStacks));

    private static string Status(HeadlessStatusApplicationObservation status)
        => Join(
            Pair("application", status.ApplicationId),
            Pair("status", status.StatusId),
            Pair("duration_seconds", status.DurationSeconds),
            Pair("magnitude", status.Magnitude),
            Pair("max_stacks", status.MaxStacks));

    private static string Pair(string key, string value) => $"{key}={Escape(value)}";

    private static string Pair(string key, int value) => Pair(key, value.ToString(CultureInfo.InvariantCulture));

    private static string Pair(string key, float value) => Pair(key, value.ToString("R", CultureInfo.InvariantCulture));

    private static string Pair(string key, bool value) => Pair(key, value ? "true" : "false");

    private static string Join(params string[] values) => string.Join(";", values);

    private static string Sequence(IEnumerable<string> values) => string.Join("|", values.Select(Escape));

    private static string Escape(string value)
        => (value ?? string.Empty).Replace("\\", "\\\\").Replace(";", "\\;").Replace("|", "\\|");
}
