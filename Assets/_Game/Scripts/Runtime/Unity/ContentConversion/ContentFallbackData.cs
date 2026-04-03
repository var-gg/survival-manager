using System;
using System.Collections.Generic;
using SM.Core.Contracts;
using SM.Meta.Model;

namespace SM.Unity.ContentConversion;

internal static class ContentFallbackData
{
    internal static readonly string[] CanonicalArchetypeOrder =
    {
        "warden",
        "guardian",
        "slayer",
        "raider",
        "hunter",
        "scout",
        "priest",
        "hexer",
        "bulwark",
        "reaver",
        "marksman",
        "shaman"
    };

    internal static readonly string[] LegacyItemFallbackOrder =
    {
        "item_iron_sword",
        "item_bone_blade",
        "item_leather_armor",
        "item_bone_plate",
        "item_lucky_charm",
        "item_prayer_bead"
    };

    internal static readonly string[] LegacyAugmentFallbackOrder =
    {
        "augment_silver_guard",
        "augment_silver_focus",
        "augment_silver_stride",
        "augment_gold_bastion",
        "augment_gold_fury",
        "augment_gold_mending"
    };

    internal static readonly IReadOnlyDictionary<string, RecruitTier> LoopBRecruitTierFallbacks = new Dictionary<string, RecruitTier>(StringComparer.Ordinal)
    {
        ["guardian"] = RecruitTier.Rare,
        ["raider"] = RecruitTier.Rare,
        ["bulwark"] = RecruitTier.Rare,
        ["reaver"] = RecruitTier.Rare,
        ["marksman"] = RecruitTier.Rare,
        ["hexer"] = RecruitTier.Epic,
        ["shaman"] = RecruitTier.Epic,
    };

    internal static readonly IReadOnlyDictionary<string, string[]> LoopBRecruitPlanTagFallbacks = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["warden"] = new[] { "vanguard", "frontline", "guard", "physical" },
        ["guardian"] = new[] { "vanguard", "frontline", "guard", "support" },
        ["bulwark"] = new[] { "vanguard", "frontline", "shield_skill", "support" },
        ["slayer"] = new[] { "duelist", "frontline", "strike", "execute" },
        ["raider"] = new[] { "duelist", "frontline", "mark", "physical" },
        ["reaver"] = new[] { "duelist", "frontline", "burst", "execute" },
        ["hunter"] = new[] { "ranger", "backline", "projectile", "physical" },
        ["scout"] = new[] { "ranger", "backline", "mark", "exposed" },
        ["marksman"] = new[] { "ranger", "backline", "pierce", "physical" },
        ["priest"] = new[] { "mystic", "backline", "support", "heal" },
        ["hexer"] = new[] { "mystic", "backline", "magical", "silence" },
        ["shaman"] = new[] { "mystic", "backline", "magical", "burn" },
    };

    internal static readonly IReadOnlyDictionary<string, string[]> LoopBScoutBiasTagFallbacks = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["warden"] = new[] { "frontline", "guard", "vanguard" },
        ["guardian"] = new[] { "frontline", "guard", "vanguard" },
        ["bulwark"] = new[] { "frontline", "guard", "vanguard" },
        ["slayer"] = new[] { "frontline", "physical", "duelist" },
        ["raider"] = new[] { "frontline", "physical", "duelist" },
        ["reaver"] = new[] { "frontline", "physical", "duelist" },
        ["hunter"] = new[] { "backline", "physical", "ranger" },
        ["scout"] = new[] { "backline", "physical", "ranger" },
        ["marksman"] = new[] { "backline", "physical", "ranger" },
        ["priest"] = new[] { "backline", "support", "heal", "mystic" },
        ["hexer"] = new[] { "backline", "magical", "mystic" },
        ["shaman"] = new[] { "backline", "magical", "mystic" },
    };

    internal static readonly IReadOnlyDictionary<string, string[]> LoopBFlexActivePoolFallbacks = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["vanguard"] = new[] { "skill_guardian_utility", "skill_warden_utility" },
        ["duelist"] = new[] { "skill_slayer_utility", "skill_raider_utility", "skill_reaver_utility" },
        ["ranger"] = new[] { "skill_hunter_utility", "skill_marksman_utility", "skill_scout_utility" },
        ["mystic"] = new[] { "skill_minor_heal", "skill_hexer_utility", "skill_shaman_utility" },
    };

    internal static readonly IReadOnlyDictionary<string, string[]> LoopBFlexPassivePoolFallbacks = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["vanguard"] = new[] { "skill_vanguard_support_1", "skill_vanguard_support_2", "support_guarded", "support_anchored" },
        ["duelist"] = new[] { "skill_duelist_support_1", "skill_duelist_support_2", "support_executioner", "support_brutal" },
        ["ranger"] = new[] { "skill_ranger_support_1", "skill_ranger_support_2", "support_longshot", "support_hunter_mark", "support_piercing", "support_swift" },
        ["mystic"] = new[] { "skill_mystic_support_1", "skill_mystic_support_2", "support_purifying", "support_siphon", "support_echo", "support_lingering" },
    };

    internal static readonly IReadOnlyDictionary<string, RecruitBannedPairingTemplate[]> LoopBRecruitBannedPairingFallbacks = new Dictionary<string, RecruitBannedPairingTemplate[]>(StringComparer.Ordinal)
    {
        ["vanguard"] = new[] { new RecruitBannedPairingTemplate("skill_warden_utility", "support_anchored") },
        ["duelist"] = new[] { new RecruitBannedPairingTemplate("skill_reaver_utility", "support_brutal") },
        ["ranger"] = new[] { new RecruitBannedPairingTemplate("skill_scout_utility", "support_longshot") },
        ["mystic"] = new[] { new RecruitBannedPairingTemplate("skill_minor_heal", "support_siphon") },
    };

    internal static readonly IReadOnlyDictionary<string, RecruitSkillFallback> LoopBRecruitSkillFallbacks = new Dictionary<string, RecruitSkillFallback>(StringComparer.Ordinal)
    {
        ["skill_guardian_core"] = new("guard_signature", string.Empty, new[] { "vanguard", "frontline", "guard" }, new[] { "vanguard", "guard", "support" }, new[] { "frontline", "vanguard" }),
        ["skill_bulwark_core"] = new("bulwark_signature", string.Empty, new[] { "vanguard", "frontline", "shield_skill" }, new[] { "vanguard", "support", "shield_skill" }, new[] { "frontline", "vanguard" }),
        ["skill_slayer_core"] = new("slayer_signature", string.Empty, new[] { "duelist", "frontline", "strike" }, new[] { "duelist", "execute", "physical" }, new[] { "frontline", "physical" }),
        ["skill_raider_core"] = new("raider_signature", string.Empty, new[] { "duelist", "frontline", "mark" }, new[] { "duelist", "mark", "physical" }, new[] { "frontline", "physical" }),
        ["skill_hexer_core"] = new("hexer_signature", string.Empty, new[] { "mystic", "backline", "burn" }, new[] { "mystic", "magical", "silence" }, new[] { "backline", "magical" }),
        ["skill_priest_core"] = new("priest_signature", string.Empty, new[] { "mystic", "backline", "heal" }, new[] { "mystic", "support", "heal" }, new[] { "backline", "support" }),
        ["skill_shaman_core"] = new("shaman_signature", string.Empty, new[] { "mystic", "backline", "burn" }, new[] { "mystic", "magical", "zone" }, new[] { "backline", "magical" }),
        ["skill_warden_utility"] = new("guard_cleanse", string.Empty, new[] { "vanguard", "frontline", "guard" }, new[] { "vanguard", "support", "cleanse" }, new[] { "frontline", "support" }),
        ["skill_guardian_utility"] = new("guard_rally", string.Empty, new[] { "vanguard", "frontline", "guard" }, new[] { "vanguard", "support", "physical" }, new[] { "frontline", "support" }),
        ["skill_slayer_utility"] = new("bleed_followup", string.Empty, new[] { "duelist", "frontline", "execute" }, new[] { "duelist", "physical", "execute" }, new[] { "frontline", "physical" }),
        ["skill_raider_utility"] = new("mark_followup", string.Empty, new[] { "duelist", "frontline", "mark" }, new[] { "duelist", "physical", "mark" }, new[] { "frontline", "physical" }),
        ["skill_reaver_utility"] = new("burst_followup", string.Empty, new[] { "duelist", "frontline", "burst" }, new[] { "duelist", "physical", "burst" }, new[] { "frontline", "physical" }),
        ["skill_hunter_utility"] = new("hunter_mark", string.Empty, new[] { "ranger", "backline", "projectile" }, new[] { "ranger", "mark", "physical" }, new[] { "backline", "physical" }),
        ["skill_marksman_utility"] = new("marksman_pierce", string.Empty, new[] { "ranger", "backline", "projectile" }, new[] { "ranger", "pierce", "physical" }, new[] { "backline", "physical" }),
        ["skill_scout_utility"] = new("scout_exposed", string.Empty, new[] { "ranger", "backline", "mark" }, new[] { "ranger", "exposed", "physical" }, new[] { "backline", "physical" }),
        ["skill_minor_heal"] = new("minor_heal", string.Empty, new[] { "mystic", "backline", "heal" }, new[] { "mystic", "support", "heal" }, new[] { "backline", "support" }),
        ["skill_hexer_utility"] = new("hexer_silence", string.Empty, new[] { "mystic", "backline", "silence" }, new[] { "mystic", "magical", "silence" }, new[] { "backline", "magical" }),
        ["skill_shaman_utility"] = new("shaman_zone", string.Empty, new[] { "mystic", "backline", "burn" }, new[] { "mystic", "magical", "zone" }, new[] { "backline", "magical" }),
        ["skill_vanguard_support_1"] = new("guard_support", string.Empty, new[] { "vanguard", "frontline", "guard" }, new[] { "vanguard", "support", "guard" }, new[] { "frontline", "support" }),
        ["skill_vanguard_support_2"] = new("bulwark_support", string.Empty, new[] { "vanguard", "frontline", "shield_skill" }, new[] { "vanguard", "support", "shield_skill" }, new[] { "frontline", "support" }),
        ["skill_duelist_support_1"] = new("slayer_support", string.Empty, new[] { "duelist", "frontline", "execute" }, new[] { "duelist", "physical", "execute" }, new[] { "frontline", "physical" }),
        ["skill_duelist_support_2"] = new("raider_support", string.Empty, new[] { "duelist", "frontline", "mark" }, new[] { "duelist", "physical", "mark" }, new[] { "frontline", "physical" }),
        ["skill_ranger_support_1"] = new("hunter_support", string.Empty, new[] { "ranger", "backline", "projectile" }, new[] { "ranger", "physical", "mark" }, new[] { "backline", "physical" }),
        ["skill_ranger_support_2"] = new("scout_support", string.Empty, new[] { "ranger", "backline", "projectile" }, new[] { "ranger", "physical", "exposed" }, new[] { "backline", "physical" }),
        ["skill_mystic_support_1"] = new("priest_support", string.Empty, new[] { "mystic", "backline", "heal" }, new[] { "mystic", "support", "heal" }, new[] { "backline", "support" }),
        ["skill_mystic_support_2"] = new("hexer_support", string.Empty, new[] { "mystic", "backline", "burn" }, new[] { "mystic", "magical", "silence" }, new[] { "backline", "magical" }),
        ["support_guarded"] = new("guard_signature", "vanguard_guard", new[] { "vanguard", "frontline", "guard" }, new[] { "vanguard", "support", "guard" }, new[] { "frontline", "support" }),
        ["support_anchored"] = new("anchored_support", "vanguard_guard", new[] { "vanguard", "frontline", "shield_skill" }, new[] { "vanguard", "support", "shield_skill" }, new[] { "frontline", "support" }),
        ["support_executioner"] = new("executioner_support", "duelist_stance", new[] { "duelist", "frontline", "execute" }, new[] { "duelist", "physical", "execute" }, new[] { "frontline", "physical" }),
        ["support_brutal"] = new("brutal_support", "duelist_stance", new[] { "duelist", "frontline", "strike" }, new[] { "duelist", "physical", "burst" }, new[] { "frontline", "physical" }),
        ["support_longshot"] = new("longshot_support", "ranger_tempo", new[] { "ranger", "backline", "projectile" }, new[] { "ranger", "physical", "pierce" }, new[] { "backline", "physical" }),
        ["support_hunter_mark"] = new("hunter_mark_support", "ranger_tempo", new[] { "ranger", "backline", "mark" }, new[] { "ranger", "physical", "mark" }, new[] { "backline", "physical" }),
        ["support_piercing"] = new("piercing_support", string.Empty, new[] { "ranger", "backline", "pierce" }, new[] { "ranger", "physical", "pierce" }, new[] { "backline", "physical" }),
        ["support_swift"] = new("swift_support", string.Empty, new[] { "ranger", "backline", "projectile" }, new[] { "ranger", "physical", "mark" }, new[] { "backline", "physical" }),
        ["support_purifying"] = new("priest_signature", "mystic_alignment", new[] { "mystic", "backline", "heal" }, new[] { "mystic", "support", "cleanse" }, new[] { "backline", "support" }),
        ["support_siphon"] = new("siphon_support", "mystic_alignment", new[] { "mystic", "backline", "burn" }, new[] { "mystic", "magical", "burn" }, new[] { "backline", "magical" }),
        ["support_echo"] = new("echo_support", string.Empty, new[] { "mystic", "backline", "zone" }, new[] { "mystic", "support", "zone" }, new[] { "backline", "support" }),
        ["support_lingering"] = new("lingering_support", string.Empty, new[] { "mystic", "backline", "zone" }, new[] { "mystic", "magical", "zone" }, new[] { "backline", "magical" }),
    };

    internal sealed record RecruitSkillFallback(
        string EffectFamilyId,
        string MutuallyExclusiveGroupId,
        string[] NativeTags,
        string[] PlanTags,
        string[] ScoutTags);
}
