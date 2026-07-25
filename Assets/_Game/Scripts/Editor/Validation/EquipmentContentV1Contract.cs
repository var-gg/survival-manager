using System;
using System.Collections.Generic;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Core.Contracts;
using SM.Core.Stats;

namespace SM.Editor.Validation;

internal readonly record struct EquipmentAffixV1Spec(
    string Id,
    AffixTierValue Tier,
    AffixFamilyValue Family,
    AffixEffectTypeValue EffectType,
    AffixCategoryValue Category,
    string StatId,
    ModifierOp Operation,
    float ValueMin,
    float ValueMax,
    IReadOnlyList<ItemSlotType> AllowedSlots,
    IReadOnlyList<string> CompileTagIds,
    IReadOnlyList<string> RequiredTagIds,
    IReadOnlyList<string> RuleModifierTagIds,
    string ExclusiveGroupId,
    string TextTemplateKey,
    float BudgetScore,
    float SpawnWeight,
    int ItemLevelMin,
    IReadOnlyList<EquipmentAffixModifierV1Spec>? AdditionalModifiers = null,
    IReadOnlyList<EquipmentAffixTriggerV1Spec>? TriggeredEffects = null,
    EffectCapability Capabilities = EffectCapability.ModifyStats,
    ContentRarity BudgetRarity = ContentRarity.Common);

internal readonly record struct EquipmentAffixModifierV1Spec(
    string StatId,
    ModifierOp Operation,
    float Value);

internal readonly record struct EquipmentAffixTriggerV1Spec(
    CombatTriggerKind Trigger,
    TriggeredEffectOp Op,
    EffectScope Scope,
    float Magnitude,
    float ThresholdRatio = 0f,
    string StatusId = "",
    float DurationSeconds = 0f,
    int MaxStacks = 1);

internal static class EquipmentContentV1Contract
{
    internal const int ItemCount = 42;
    internal const int AffixCount = 44;
    internal const int LiveAffixCount = 44;
    internal const int ReservedAffixCount = 0;
    internal const int ReservedAffixItemLevelMin = 999;
    internal const string RefitCurrencyTag = "echo";

    internal static readonly HashSet<string> RareItemIds = new(StringComparer.Ordinal)
    {
        "item_bone_blade",
        "item_guardian_shield",
        "item_hunter_bow",
        "item_priest_focus",
        "item_cantor_focus",
        "item_oath_bead",
        "item_penitent_shield",
        "item_reaver_blade",
        "item_wayfinder_trinket",
    };

    internal static readonly HashSet<string> EpicItemIds = new(StringComparer.Ordinal)
    {
        "item_bulwark_armor",
        "item_rift_bow",
        "item_prayer_bead",
    };

    internal static readonly HashSet<string> NamedItemIds = new(StringComparer.Ordinal)
    {
        "item_cantor_focus",
        "item_oath_bead",
        "item_penitent_shield",
        "item_reaver_blade",
        "item_wayfinder_trinket",
        "item_bulwark_armor",
    };

    internal static readonly HashSet<string> UniqueItemIds = new(StringComparer.Ordinal)
    {
        "item_rift_bow",
        "item_prayer_bead",
    };

    internal static readonly IReadOnlyDictionary<string, string> GrantedSkillByItemId = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["item_cantor_focus"] = "skill_priest_core",
        ["item_oath_bead"] = "skill_minor_heal",
        ["item_penitent_shield"] = "skill_warden_utility",
        ["item_reaver_blade"] = "skill_reaver_core",
        ["item_wayfinder_trinket"] = "skill_scout_utility",
        ["item_bulwark_armor"] = "skill_bulwark_utility",
        ["item_rift_bow"] = "skill_marksman_utility",
        ["item_prayer_bead"] = "skill_priest_core",
    };

    internal static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> RequiredItemDropsByTable =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["drop_table_skirmish"] = new[] { "item_iron_sword", "item_leather_armor", "item_lucky_charm" },
            ["drop_table_elite"] = new[] { "item_bone_blade", "item_guardian_shield", "item_priest_focus" },
            ["drop_table_boss"] = new[] { "item_prayer_bead", "item_bulwark_armor", "item_rift_bow" },
        };

    internal static readonly HashSet<string> LiveAffixIds = new(StringComparer.Ordinal)
    {
        "affix_sharp",
        "affix_focusing",
        "affix_sturdy",
        "affix_warded",
        "affix_blessed",
        "affix_hasty",
        "affix_fierce",
        "affix_precise",
        "affix_piercing",
        "affix_vital",
        "affix_ironclad",
        "affix_mender",
        "affix_lithe",
        "affix_lucid",
        "affix_farshot",
        "affix_guarded",
        "affix_channeling",
        "affix_cleansing",
        "affix_bracing",
        "affix_resolute",
        "affix_relentless",
        "affix_watchful",
        "affix_packborn",
        "affix_wraithbound",
        "affix_hallowed",
        "affix_heavy",
        "affix_quick",
        "affix_ravenous",
        "affix_reaching",
        "affix_spined",
        "affix_reckless_edge",
        "affix_brittle_focus",
        "affix_overclocked",
        "affix_blood_price",
        "affix_lightfooted_plate",
        "affix_burdened_reach",
        "affix_reaper_spark",
        "affix_last_ward",
        "affix_executioners_edge",
        "affix_desperate_focus",
        "affix_mourning_aegis",
        "affix_first_light",
        "affix_war_chorus",
        "affix_fallen_chorus",
    };

    internal static readonly HashSet<string> ReservedAffixIds = new(StringComparer.Ordinal);

    internal static readonly IReadOnlyList<string> LiveAffixOrder = new[]
    {
        "affix_blessed",
        "affix_blood_price",
        "affix_bracing",
        "affix_brittle_focus",
        "affix_burdened_reach",
        "affix_channeling",
        "affix_cleansing",
        "affix_desperate_focus",
        "affix_executioners_edge",
        "affix_fallen_chorus",
        "affix_farshot",
        "affix_fierce",
        "affix_first_light",
        "affix_focusing",
        "affix_guarded",
        "affix_hallowed",
        "affix_hasty",
        "affix_heavy",
        "affix_ironclad",
        "affix_last_ward",
        "affix_lightfooted_plate",
        "affix_lithe",
        "affix_lucid",
        "affix_mender",
        "affix_mourning_aegis",
        "affix_overclocked",
        "affix_packborn",
        "affix_piercing",
        "affix_precise",
        "affix_quick",
        "affix_ravenous",
        "affix_reaching",
        "affix_reaper_spark",
        "affix_reckless_edge",
        "affix_relentless",
        "affix_resolute",
        "affix_sharp",
        "affix_spined",
        "affix_sturdy",
        "affix_vital",
        "affix_warded",
        "affix_war_chorus",
        "affix_watchful",
        "affix_wraithbound",
    };

    internal static readonly IReadOnlyList<EquipmentAffixV1Spec> AffixSpecs = new[]
    {
        Live("affix_sharp", AffixTierValue.Implicit, AffixFamilyValue.CoreScalar, AffixEffectTypeValue.StatModifier, AffixCategoryValue.OffenseFlat, "phys_power", ModifierOp.Flat, 2f, 4f, Slots(ItemSlotType.Weapon), Tags("physical", "strike"), Tags(), Tags(), "implicit.phys_power", "content.affix.template.scalar", 6f),
        Live("affix_focusing", AffixTierValue.Implicit, AffixFamilyValue.CoreScalar, AffixEffectTypeValue.StatModifier, AffixCategoryValue.OffenseFlat, "mag_power", ModifierOp.Flat, 2f, 4f, Slots(ItemSlotType.Weapon), Tags("magical", "focus"), Tags(), Tags(), "implicit.mag_power", "content.affix.template.scalar", 6f),
        Live("affix_sturdy", AffixTierValue.Implicit, AffixFamilyValue.CoreScalar, AffixEffectTypeValue.StatModifier, AffixCategoryValue.DefenseFlat, "armor", ModifierOp.Flat, 1f, 3f, Slots(ItemSlotType.Armor), Tags("frontline", "guard"), Tags(), Tags(), "implicit.armor", "content.affix.template.scalar", 6f),
        Live("affix_warded", AffixTierValue.Implicit, AffixFamilyValue.CoreScalar, AffixEffectTypeValue.StatModifier, AffixCategoryValue.DefenseFlat, "resist", ModifierOp.Flat, 1f, 2f, Slots(ItemSlotType.Armor, ItemSlotType.Accessory), Tags("magical", "sustain"), Tags(), Tags(), "implicit.resist", "content.affix.template.scalar", 6f),
        Live("affix_blessed", AffixTierValue.Implicit, AffixFamilyValue.CoreScalar, AffixEffectTypeValue.StatModifier, AffixCategoryValue.Utility, "heal_power", ModifierOp.Flat, 1f, 2f, Slots(ItemSlotType.Weapon, ItemSlotType.Accessory), Tags("heal", "support"), Tags(), Tags(), "implicit.heal_power", "content.affix.template.scalar", 6f),
        Live("affix_hasty", AffixTierValue.Implicit, AffixFamilyValue.CoreScalar, AffixEffectTypeValue.StatModifier, AffixCategoryValue.Utility, "attack_speed", ModifierOp.Increased, 0.04f, 0.08f, Slots(ItemSlotType.Accessory), Tags("tempo"), Tags(), Tags(), "implicit.attack_speed", "content.affix.template.scalar", 6f),

        Live("affix_fierce", AffixTierValue.Prefix, AffixFamilyValue.CoreScalar, AffixEffectTypeValue.StatModifier, AffixCategoryValue.OffenseFlat, "phys_power", ModifierOp.Flat, 1f, 3f, Slots(ItemSlotType.Weapon), Tags("physical", "strike"), Tags(), Tags(), "prefix.phys_power", "content.affix.template.scalar", 8f),
        Live("affix_precise", AffixTierValue.Prefix, AffixFamilyValue.CoreScalar, AffixEffectTypeValue.StatModifier, AffixCategoryValue.OffenseScaling, "crit_chance", ModifierOp.Increased, 0.03f, 0.06f, Slots(ItemSlotType.Weapon, ItemSlotType.Accessory), Tags("physical", "mark"), Tags(), Tags(), "prefix.crit_chance", "content.affix.template.scalar", 8f),
        Live("affix_piercing", AffixTierValue.Prefix, AffixFamilyValue.CoreScalar, AffixEffectTypeValue.StatModifier, AffixCategoryValue.OffenseFlat, "phys_pen", ModifierOp.Flat, 0.4f, 0.9f, Slots(ItemSlotType.Weapon), Tags("pierce", "projectile"), Tags(), Tags(), "prefix.phys_pen", "content.affix.template.scalar", 8f),
        Live("affix_vital", AffixTierValue.Prefix, AffixFamilyValue.CoreScalar, AffixEffectTypeValue.StatModifier, AffixCategoryValue.DefenseScaling, "max_health", ModifierOp.Flat, 2f, 5f, Slots(ItemSlotType.Armor, ItemSlotType.Accessory), Tags("sustain"), Tags(), Tags(), "prefix.max_health", "content.affix.template.scalar", 8f),
        Live("affix_ironclad", AffixTierValue.Prefix, AffixFamilyValue.CoreScalar, AffixEffectTypeValue.StatModifier, AffixCategoryValue.DefenseFlat, "armor", ModifierOp.Flat, 1f, 3f, Slots(ItemSlotType.Armor), Tags("frontline", "guard"), Tags(), Tags(), "prefix.armor", "content.affix.template.scalar", 8f),
        Live("affix_mender", AffixTierValue.Prefix, AffixFamilyValue.CoreScalar, AffixEffectTypeValue.StatModifier, AffixCategoryValue.Utility, "heal_power", ModifierOp.Flat, 1f, 3f, Slots(ItemSlotType.Weapon, ItemSlotType.Accessory), Tags("heal", "support"), Tags(), Tags(), "prefix.heal_power", "content.affix.template.scalar", 8f),
        Live("affix_lithe", AffixTierValue.Prefix, AffixFamilyValue.CoreScalar, AffixEffectTypeValue.StatModifier, AffixCategoryValue.Utility, "move_speed", ModifierOp.Increased, 0.04f, 0.08f, Slots(ItemSlotType.Armor, ItemSlotType.Accessory), Tags("tempo"), Tags(), Tags(), "prefix.move_speed", "content.affix.template.scalar", 8f),
        Live("affix_lucid", AffixTierValue.Prefix, AffixFamilyValue.CoreScalar, AffixEffectTypeValue.StatModifier, AffixCategoryValue.Utility, "skill_haste", ModifierOp.Increased, 0.04f, 0.08f, Slots(ItemSlotType.Weapon, ItemSlotType.Accessory), Tags("tempo", "magical"), Tags(), Tags(), "prefix.skill_haste", "content.affix.template.scalar", 8f),
        Live("affix_heavy", AffixTierValue.Prefix, AffixFamilyValue.CoreScalar, AffixEffectTypeValue.StatModifier, AffixCategoryValue.DefenseFlat, "armor", ModifierOp.Flat, 1f, 3f, Slots(ItemSlotType.Armor), Tags("frontline", "guard"), Tags(), Tags(), "prefix.armor", "content.affix.template.scalar", 8f),
        Live("affix_quick", AffixTierValue.Prefix, AffixFamilyValue.CoreScalar, AffixEffectTypeValue.StatModifier, AffixCategoryValue.Utility, "attack_speed", ModifierOp.Increased, 0.04f, 0.08f, Slots(ItemSlotType.Accessory), Tags("tempo"), Tags(), Tags(), "prefix.attack_speed", "content.affix.template.scalar", 8f),
        Live("affix_reaching", AffixTierValue.Prefix, AffixFamilyValue.CoreScalar, AffixEffectTypeValue.StatModifier, AffixCategoryValue.Utility, "attack_range", ModifierOp.Flat, 0.10f, 0.20f, Slots(ItemSlotType.Weapon), Tags("backline"), Tags(), Tags(), "prefix.attack_range", "content.affix.template.scalar", 8f),
        // Ratio-valued stats with a zero baseline carry their percentage-point magnitude as Flat;
        // Increased would multiply the zero baseline and silently compile to zero.
        Live("affix_hallowed", AffixTierValue.Suffix, AffixFamilyValue.CoreScalar, AffixEffectTypeValue.StatModifier, AffixCategoryValue.Utility, "status_potency", ModifierOp.Flat, 0.10f, 0.20f, Slots(ItemSlotType.Weapon, ItemSlotType.Accessory), Tags(), Tags(), Tags(), "suffix.status_potency", "content.affix.template.scalar", 8f),
        Live("affix_ravenous", AffixTierValue.Suffix, AffixFamilyValue.CoreScalar, AffixEffectTypeValue.StatModifier, AffixCategoryValue.Utility, "lifesteal", ModifierOp.Flat, 0.04f, 0.08f, Slots(ItemSlotType.Weapon), Tags("sustain"), Tags(), Tags(), "suffix.lifesteal", "content.affix.template.scalar", 8f),
        Live("affix_spined", AffixTierValue.Suffix, AffixFamilyValue.CoreScalar, AffixEffectTypeValue.StatModifier, AffixCategoryValue.OffenseFlat, "phys_pen", ModifierOp.Flat, 0.4f, 0.9f, Slots(ItemSlotType.Weapon, ItemSlotType.Armor), Tags("pierce", "physical"), Tags(), Tags(), "suffix.phys_pen", "content.affix.template.scalar", 8f),

        Live("affix_farshot", AffixTierValue.Prefix, AffixFamilyValue.ConditionalTagged, AffixEffectTypeValue.ConditionalTagged, AffixCategoryValue.SynergyTagged, "attack_range", ModifierOp.Flat, 0.15f, 0.3f, Slots(ItemSlotType.Weapon), Tags("projectile", "backline"), Tags("projectile"), Tags(), "conditional.projectile_range", "content.affix.template.conditional", 10f),
        Live("affix_guarded", AffixTierValue.Prefix, AffixFamilyValue.ConditionalTagged, AffixEffectTypeValue.ConditionalTagged, AffixCategoryValue.SynergyTagged, "armor", ModifierOp.Flat, 1f, 2f, Slots(ItemSlotType.Armor, ItemSlotType.Accessory), Tags("guard", "frontline"), Tags("guard"), Tags(), "conditional.guard_armor", "content.affix.template.conditional", 10f),
        Live("affix_channeling", AffixTierValue.Prefix, AffixFamilyValue.ConditionalTagged, AffixEffectTypeValue.ConditionalTagged, AffixCategoryValue.SynergyTagged, "mag_power", ModifierOp.Flat, 1f, 3f, Slots(ItemSlotType.Weapon), Tags("magical", "focus"), Tags("magical"), Tags(), "conditional.magical_channel", "content.affix.template.conditional", 10f),
        Live("affix_cleansing", AffixTierValue.Prefix, AffixFamilyValue.ConditionalTagged, AffixEffectTypeValue.ConditionalTagged, AffixCategoryValue.SynergyTagged, "heal_power", ModifierOp.Flat, 1f, 2f, Slots(ItemSlotType.Weapon, ItemSlotType.Accessory), Tags("cleanse", "heal"), Tags("cleanse"), Tags(), "conditional.cleanse_heal", "content.affix.template.conditional", 10f),
        Live("affix_bracing", AffixTierValue.Suffix, AffixFamilyValue.ConditionalTagged, AffixEffectTypeValue.ConditionalTagged, AffixCategoryValue.DefenseScaling, "max_health", ModifierOp.Flat, 2f, 4f, Slots(ItemSlotType.Armor, ItemSlotType.Accessory), Tags("frontline", "sustain"), Tags("frontline"), Tags(), "conditional.frontline_health", "content.affix.template.conditional", 10f),
        Live("affix_resolute", AffixTierValue.Suffix, AffixFamilyValue.ConditionalTagged, AffixEffectTypeValue.ConditionalTagged, AffixCategoryValue.DefenseScaling, "tenacity", ModifierOp.Increased, 0.04f, 0.08f, Slots(ItemSlotType.Armor, ItemSlotType.Accessory), Tags("control", "guard"), Tags("control"), Tags(), "conditional.control_tenacity", "content.affix.template.conditional", 10f),

        // BuildShaping 4종의 rule tag(BehaviorTag)는 sim 해석기가 존재한 적 없는 장식이라 위생 정리로 제거 —
        // 실효 payload는 CompileTags(시너지/조건 게이트 소비) + 수치 modifier. EffectType도 실체(BuildShaping)로 정정.
        Live("affix_relentless", AffixTierValue.Suffix, AffixFamilyValue.BuildShaping, AffixEffectTypeValue.BuildShaping, AffixCategoryValue.SynergyTagged, "attack_speed", ModifierOp.Increased, 0.04f, 0.08f, Slots(ItemSlotType.Weapon, ItemSlotType.Accessory), Tags("tempo", "execute"), Tags(), Tags(), "build.relentless_tempo", "content.affix.template.build", 12f),
        Live("affix_watchful", AffixTierValue.Suffix, AffixFamilyValue.BuildShaping, AffixEffectTypeValue.BuildShaping, AffixCategoryValue.SynergyTagged, "crit_chance", ModifierOp.Increased, 0.03f, 0.06f, Slots(ItemSlotType.Weapon, ItemSlotType.Accessory), Tags("mark", "backline"), Tags(), Tags(), "build.watchful_mark", "content.affix.template.build", 12f),
        Live("affix_packborn", AffixTierValue.Suffix, AffixFamilyValue.BuildShaping, AffixEffectTypeValue.BuildShaping, AffixCategoryValue.SynergyTagged, "max_health", ModifierOp.Flat, 2f, 4f, Slots(ItemSlotType.Armor, ItemSlotType.Accessory), Tags("support", "sustain"), Tags(), Tags(), "build.packborn_sustain", "content.affix.template.build", 12f),
        Live("affix_wraithbound", AffixTierValue.Suffix, AffixFamilyValue.BuildShaping, AffixEffectTypeValue.BuildShaping, AffixCategoryValue.SynergyTagged, "mag_power", ModifierOp.Flat, 1f, 3f, Slots(ItemSlotType.Weapon, ItemSlotType.Accessory), Tags("magical", "wildcard_risk"), Tags(), Tags(), "build.wraithbound_magic", "content.affix.template.build", 12f),

        // Decision-bearing tradeoffs. The first modifier owns ValueMin/ValueMax; every additional modifier
        // scales proportionally from the same item-instance roll through AffixMagnitudePackageResolver.
        Live("affix_reckless_edge", AffixTierValue.Implicit, AffixFamilyValue.BuildShaping, AffixEffectTypeValue.StatModifier, AffixCategoryValue.OffenseFlat, "phys_power", ModifierOp.Flat, 1.5f, 2.5f, Slots(ItemSlotType.Weapon), Tags("physical", "strike"), Tags(), Tags(), "tradeoff.reckless_edge", "content.affix.template.build", 6f, additionalModifiers: ExtraModifiers(ExtraModifier("armor", ModifierOp.Flat, -1f))),
        Live("affix_brittle_focus", AffixTierValue.Implicit, AffixFamilyValue.BuildShaping, AffixEffectTypeValue.StatModifier, AffixCategoryValue.OffenseFlat, "mag_power", ModifierOp.Flat, 1.5f, 2.5f, Slots(ItemSlotType.Weapon), Tags("magical", "focus"), Tags(), Tags(), "tradeoff.brittle_focus", "content.affix.template.build", 6f, additionalModifiers: ExtraModifiers(ExtraModifier("resist", ModifierOp.Flat, -1f))),
        Live("affix_overclocked", AffixTierValue.Implicit, AffixFamilyValue.BuildShaping, AffixEffectTypeValue.StatModifier, AffixCategoryValue.Utility, "attack_speed", ModifierOp.Increased, 0.03f, 0.05f, Slots(ItemSlotType.Accessory), Tags("tempo", "wildcard_risk"), Tags(), Tags(), "tradeoff.overclocked", "content.affix.template.build", 6f, additionalModifiers: ExtraModifiers(ExtraModifier("max_health", ModifierOp.Increased, -0.04f))),
        Live("affix_blood_price", AffixTierValue.Prefix, AffixFamilyValue.BuildShaping, AffixEffectTypeValue.StatModifier, AffixCategoryValue.Utility, "lifesteal", ModifierOp.Flat, 0.03f, 0.05f, Slots(ItemSlotType.Weapon), Tags("sustain", "wildcard_risk"), Tags(), Tags(), "tradeoff.blood_price", "content.affix.template.build", 8f, additionalModifiers: ExtraModifiers(ExtraModifier("max_health", ModifierOp.Increased, -0.04f))),
        Live("affix_lightfooted_plate", AffixTierValue.Prefix, AffixFamilyValue.BuildShaping, AffixEffectTypeValue.StatModifier, AffixCategoryValue.Utility, "move_speed", ModifierOp.Increased, 0.03f, 0.05f, Slots(ItemSlotType.Armor, ItemSlotType.Accessory), Tags("tempo", "guard"), Tags(), Tags(), "tradeoff.lightfooted_plate", "content.affix.template.build", 8f, additionalModifiers: ExtraModifiers(ExtraModifier("armor", ModifierOp.Flat, -1f))),
        Live("affix_burdened_reach", AffixTierValue.Prefix, AffixFamilyValue.BuildShaping, AffixEffectTypeValue.StatModifier, AffixCategoryValue.Utility, "attack_range", ModifierOp.Flat, 0.15f, 0.25f, Slots(ItemSlotType.Weapon), Tags("backline", "tempo"), Tags(), Tags(), "tradeoff.burdened_reach", "content.affix.template.build", 8f, additionalModifiers: ExtraModifiers(ExtraModifier("move_speed", ModifierOp.Increased, -0.03f))),

        // Existing trigger and behavior-tag consumers make these live in actual battle without adding a new
        // status, slot, grade, trigger kind, or combat-state rule language.
        Live("affix_reaper_spark", AffixTierValue.Prefix, AffixFamilyValue.BuildShaping, AffixEffectTypeValue.Proc, AffixCategoryValue.Utility, string.Empty, ModifierOp.Flat, 8f, 12f, Slots(ItemSlotType.Weapon, ItemSlotType.Accessory), Tags("tempo"), Tags(), Tags(), "trigger.reaper_spark", "content.affix.template.build", 8f, triggeredEffects: TriggerSpecs(Trigger(CombatTriggerKind.OnKill, TriggeredEffectOp.GainEnergy, EffectScope.Self, 10f)), capabilities: EffectCapability.ModifyResource),
        Live("affix_last_ward", AffixTierValue.Prefix, AffixFamilyValue.BuildShaping, AffixEffectTypeValue.Proc, AffixCategoryValue.DefenseScaling, string.Empty, ModifierOp.Flat, 4f, 8f, Slots(ItemSlotType.Armor, ItemSlotType.Accessory), Tags("guard", "sustain"), Tags(), Tags(), "trigger.last_ward", "content.affix.template.build", 8f, triggeredEffects: TriggerSpecs(Trigger(CombatTriggerKind.OnHpBelow, TriggeredEffectOp.Barrier, EffectScope.Self, 6f, 0.5f)), capabilities: EffectCapability.HealOrBarrier),
        Live("affix_executioners_edge", AffixTierValue.Suffix, AffixFamilyValue.BuildShaping, AffixEffectTypeValue.BuildShaping, AffixCategoryValue.OffenseFlat, "phys_power", ModifierOp.Flat, 1f, 2f, Slots(ItemSlotType.Weapon), Tags("physical", "execute"), Tags(), Tags("execute_low_hp"), "conditional.executioners_edge", "content.affix.template.build", 8f, capabilities: EffectCapability.ModifyStats | EffectCapability.ModifyGlobalCombatRule),
        Live("affix_desperate_focus", AffixTierValue.Prefix, AffixFamilyValue.BuildShaping, AffixEffectTypeValue.Proc, AffixCategoryValue.Utility, string.Empty, ModifierOp.Flat, 15f, 25f, Slots(ItemSlotType.Weapon, ItemSlotType.Accessory), Tags("focus", "tempo"), Tags(), Tags(), "trigger.desperate_focus", "content.affix.template.build", 10f, triggeredEffects: TriggerSpecs(Trigger(CombatTriggerKind.OnHpBelow, TriggeredEffectOp.GainEnergy, EffectScope.Self, 20f, 0.4f)), capabilities: EffectCapability.ModifyResource, budgetRarity: ContentRarity.Epic),
        Live("affix_mourning_aegis", AffixTierValue.Prefix, AffixFamilyValue.BuildShaping, AffixEffectTypeValue.Proc, AffixCategoryValue.DefenseScaling, string.Empty, ModifierOp.Flat, 4f, 8f, Slots(ItemSlotType.Armor, ItemSlotType.Accessory), Tags("guard", "support"), Tags(), Tags(), "trigger.mourning_aegis", "content.affix.template.build", 10f, triggeredEffects: TriggerSpecs(Trigger(CombatTriggerKind.OnAllyDeath, TriggeredEffectOp.Barrier, EffectScope.Self, 6f)), capabilities: EffectCapability.HealOrBarrier),
        Live("affix_first_light", AffixTierValue.Suffix, AffixFamilyValue.BuildShaping, AffixEffectTypeValue.Proc, AffixCategoryValue.DefenseScaling, string.Empty, ModifierOp.Flat, 3f, 5f, Slots(ItemSlotType.Weapon, ItemSlotType.Armor, ItemSlotType.Accessory), Tags("guard", "support"), Tags(), Tags(), "trigger.first_light", "content.affix.template.build", 10f, triggeredEffects: TriggerSpecs(Trigger(CombatTriggerKind.BattleStart, TriggeredEffectOp.Barrier, EffectScope.Self, 4f)), capabilities: EffectCapability.HealOrBarrier),
        Live("affix_war_chorus", AffixTierValue.Suffix, AffixFamilyValue.BuildShaping, AffixEffectTypeValue.Proc, AffixCategoryValue.Utility, string.Empty, ModifierOp.Flat, 2f, 4f, Slots(ItemSlotType.Accessory), Tags("support", "guard"), Tags(), Tags(), "trigger.war_chorus", "content.affix.template.build", 12f, triggeredEffects: TriggerSpecs(Trigger(CombatTriggerKind.BattleStart, TriggeredEffectOp.Barrier, EffectScope.AlliedCombatants, 3f)), capabilities: EffectCapability.HealOrBarrier),
        Live("affix_fallen_chorus", AffixTierValue.Suffix, AffixFamilyValue.BuildShaping, AffixEffectTypeValue.Proc, AffixCategoryValue.Utility, string.Empty, ModifierOp.Flat, 3f, 7f, Slots(ItemSlotType.Accessory), Tags("support", "sustain"), Tags(), Tags(), "trigger.fallen_chorus", "content.affix.template.build", 12f, triggeredEffects: TriggerSpecs(Trigger(CombatTriggerKind.OnAllyDeath, TriggeredEffectOp.Heal, EffectScope.AlliedCombatants, 5f)), capabilities: EffectCapability.HealOrBarrier),

    };

    internal static readonly IReadOnlyDictionary<string, EquipmentAffixV1Spec> AffixSpecsById =
        BuildAffixSpecIndex();

    private static EquipmentAffixV1Spec Live(
        string id,
        AffixTierValue tier,
        AffixFamilyValue family,
        AffixEffectTypeValue effectType,
        AffixCategoryValue category,
        string statId,
        ModifierOp operation,
        float valueMin,
        float valueMax,
        IReadOnlyList<ItemSlotType> slots,
        IReadOnlyList<string> compileTags,
        IReadOnlyList<string> requiredTags,
        IReadOnlyList<string> ruleTags,
        string exclusiveGroup,
        string templateKey,
        float budgetScore,
        IReadOnlyList<EquipmentAffixModifierV1Spec>? additionalModifiers = null,
        IReadOnlyList<EquipmentAffixTriggerV1Spec>? triggeredEffects = null,
        EffectCapability capabilities = EffectCapability.ModifyStats,
        ContentRarity budgetRarity = ContentRarity.Common)
    {
        return new EquipmentAffixV1Spec(
            id,
            tier,
            family,
            effectType,
            category,
            statId,
            operation,
            valueMin,
            valueMax,
            slots,
            compileTags,
            requiredTags,
            ruleTags,
            exclusiveGroup,
            templateKey,
            budgetScore,
            1f,
            0,
            additionalModifiers,
            triggeredEffects,
            capabilities,
            budgetRarity);
    }

    private static IReadOnlyList<ItemSlotType> Slots(params ItemSlotType[] slots) => slots;

    private static IReadOnlyList<string> Tags(params string[] tags) => tags;

    private static EquipmentAffixModifierV1Spec ExtraModifier(
        string statId,
        ModifierOp operation,
        float value)
    {
        return new EquipmentAffixModifierV1Spec(statId, operation, value);
    }

    private static IReadOnlyList<EquipmentAffixModifierV1Spec> ExtraModifiers(
        params EquipmentAffixModifierV1Spec[] modifiers)
    {
        return modifiers;
    }

    private static EquipmentAffixTriggerV1Spec Trigger(
        CombatTriggerKind trigger,
        TriggeredEffectOp op,
        EffectScope scope,
        float magnitude,
        float thresholdRatio = 0f)
    {
        return new EquipmentAffixTriggerV1Spec(
            trigger,
            op,
            scope,
            magnitude,
            thresholdRatio);
    }

    private static IReadOnlyList<EquipmentAffixTriggerV1Spec> TriggerSpecs(
        params EquipmentAffixTriggerV1Spec[] effects)
    {
        return effects;
    }

    private static IReadOnlyDictionary<string, EquipmentAffixV1Spec> BuildAffixSpecIndex()
    {
        var result = new Dictionary<string, EquipmentAffixV1Spec>(StringComparer.Ordinal);
        foreach (var spec in AffixSpecs)
        {
            result[spec.Id] = spec;
        }

        return result;
    }
}
