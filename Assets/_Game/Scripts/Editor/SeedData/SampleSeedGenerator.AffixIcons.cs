using System;
using System.Collections.Generic;
using System.Linq;
using SM.Content.Definitions;
using UnityEditor;

namespace SM.Editor.SeedData;

public static partial class SampleSeedGenerator
{
    private static readonly IReadOnlyDictionary<string, string> AuthoredAffixIconIds =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["affix_blessed"] = "affix_heal",
            ["affix_blood_price"] = "affix_blood_price",
            ["affix_bracing"] = "affix_hp",
            ["affix_brittle_focus"] = "affix_brittle_focus",
            ["affix_burdened_reach"] = "affix_burdened_reach",
            ["affix_channeling"] = "affix_cast_speed",
            ["affix_cleansing"] = "affix_cleanse",
            ["affix_desperate_focus"] = "affix_desperate_focus",
            ["affix_executioners_edge"] = "affix_executioners_edge",
            ["affix_fallen_chorus"] = "affix_fallen_chorus",
            ["affix_farshot"] = "affix_pierce",
            ["affix_fierce"] = "affix_atk",
            ["affix_first_light"] = "affix_first_light",
            ["affix_focusing"] = "affix_magic_atk",
            ["affix_guarded"] = "affix_block",
            ["affix_hallowed"] = "affix_amplify",
            ["affix_hasty"] = "affix_speed",
            ["affix_heavy"] = "affix_armor",
            ["affix_ironclad"] = "affix_armor",
            ["affix_last_ward"] = "affix_last_ward",
            ["affix_lightfooted_plate"] = "affix_lightfooted_plate",
            ["affix_lithe"] = "affix_speed",
            ["affix_lucid"] = "affix_cooldown",
            ["affix_mender"] = "affix_heal",
            ["affix_mourning_aegis"] = "affix_mourning_aegis",
            ["affix_overclocked"] = "affix_overclocked",
            ["affix_packborn"] = "affix_link",
            ["affix_piercing"] = "affix_pierce",
            ["affix_precise"] = "affix_crit",
            ["affix_quick"] = "affix_speed",
            ["affix_ravenous"] = "affix_lifesteal",
            ["affix_reaching"] = "affix_pierce",
            ["affix_reaper_spark"] = "affix_reaper_spark",
            ["affix_reckless_edge"] = "affix_reckless_edge",
            ["affix_relentless"] = "affix_charge",
            ["affix_resolute"] = "affix_resist_phys",
            ["affix_sharp"] = "affix_atk",
            ["affix_spined"] = "affix_pierce",
            ["affix_sturdy"] = "affix_armor",
            ["affix_vital"] = "affix_hp",
            ["affix_war_chorus"] = "affix_war_chorus",
            ["affix_warded"] = "affix_resist_magic",
            ["affix_watchful"] = "affix_crit",
            ["affix_wraithbound"] = "affix_amplify",
        };

    private static void ApplyAffixIconIdentity()
    {
        var affixes = LoadDefinitionsById<AffixDefinition>($"{ResourcesRoot}/Affixes");
        var missingMappings = affixes.Keys
            .Where(id => !AuthoredAffixIconIds.ContainsKey(id))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        var unknownMappings = AuthoredAffixIconIds.Keys
            .Where(id => !affixes.ContainsKey(id))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        if (missingMappings.Length > 0 || unknownMappings.Length > 0)
        {
            throw new InvalidOperationException(
                $"Affix IconId map drift. Missing=[{string.Join(", ", missingMappings)}] Unknown=[{string.Join(", ", unknownMappings)}]");
        }

        foreach (var (id, iconId) in AuthoredAffixIconIds)
        {
            var affix = affixes[id];
            affix.IconId = iconId;
            EditorUtility.SetDirty(affix);
        }
    }
}
