using System;
using System.Collections.Generic;

namespace SM.Unity
{

internal static class BattleP09AppearanceRoster
{
    // Runtime fallback keeps archetype aliases because authored content can still
    // resolve visuals by ArchetypeId. Studio authoring uses AuthoringCharacterIds.
    private static readonly string[] CharacterIds =
    {
        "warden",
        "guardian",
        "bulwark",
        "slayer",
        "raider",
        "reaver",
        "hunter",
        "scout",
        "marksman",
        "priest",
        "hexer",
        "shaman",
        "rift_stalker",
        "bastion_penitent",
        "pale_executor",
        "mirror_cantor",
        "hero_dawn_priest",
        "hero_pack_raider",
        "hero_grave_hexer",
        "hero_echo_savant",
        "npc_lyra_sternfeld",
        "npc_grey_fang",
        "npc_black_vellum",
        "npc_silent_moon",
        "npc_baekgyu_sternheim",
        "hero_aegis_sentinel",
        "hero_shardblade",
        "hero_prism_seeker",
        "hero_ember_runner",
        "hero_iron_pelt",
        "npc_aldric",
    };

    private static readonly string[] AuthoringCharacterIds =
    {
        "hero_dawn_priest",
        "hero_pack_raider",
        "hero_grave_hexer",
        "hero_echo_savant",
        "npc_lyra_sternfeld",
        "npc_grey_fang",
        "npc_black_vellum",
        "npc_silent_moon",
        "npc_baekgyu_sternheim",
        "warden",
        "guardian",
        "bulwark",
        "slayer",
        "reaver",
        "hunter",
        "scout",
        "marksman",
        "shaman",
        "rift_stalker",
        "bastion_penitent",
        "pale_executor",
        "mirror_cantor",
        "hero_aegis_sentinel",
        "hero_shardblade",
        "hero_prism_seeker",
        "hero_ember_runner",
        "hero_iron_pelt",
        "npc_aldric",
    };

    private static readonly HashSet<string> AuthoringHiddenAliases = new(StringComparer.Ordinal)
    {
        "priest",
        "raider",
        "hexer",
    };

    private static readonly Dictionary<string, int> CharacterOrder = BuildCharacterOrder(CharacterIds);
    private static readonly Dictionary<string, int> AuthoringCharacterOrder = BuildCharacterOrder(AuthoringCharacterIds);
    private static readonly Dictionary<string, string> ConfirmedDisplayNames = new(StringComparer.Ordinal)
    {
        ["warden"] = "철위 (鐵衛) / Iron Warden",
        ["guardian"] = "묘직 (墓直) / Crypt Guardian",
        ["bulwark"] = "송곳벽 / Fang Bulwark",
        ["slayer"] = "서검 (誓劍) / Oath Slayer",
        ["raider"] = "이빨바람 / Pack Raider",
        ["reaver"] = "묵괴 (墨壞) / Grave Reaver",
        ["hunter"] = "원시 (遠矢) / Longshot Hunter",
        ["scout"] = "숲살이 / Trail Scout",
        ["marksman"] = "냉시 (冷矢) / Dread Marksman",
        ["priest"] = "단린 (丹麟) / Dawn Priest",
        ["hexer"] = "묵향 (墨香) / Grave Hexer",
        ["shaman"] = "풍의 (風儀) / Storm Shaman",
        ["rift_stalker"] = "틈사냥꾼 / Rift Stalker",
        ["bastion_penitent"] = "참회벽 / Bastion Penitent",
        ["pale_executor"] = "백집행 (白執行) / Pale Executor",
        ["mirror_cantor"] = "명음 (明音) / Mirror Cantor",
        ["hero_dawn_priest"] = "단린 (丹麟) / Dawn Priest",
        ["hero_pack_raider"] = "이빨바람 / Pack Raider",
        ["hero_grave_hexer"] = "묵향 (墨香) / Grave Hexer",
        ["hero_echo_savant"] = "공한 (空閑) / Echo Savant",
        ["npc_lyra_sternfeld"] = "선영 (宣英) / Lyra Sternfeld",
        ["npc_grey_fang"] = "회조 (灰爪) / Grey Fang",
        ["npc_black_vellum"] = "흑지 (黑紙) / Black Vellum",
        ["npc_silent_moon"] = "침월 (沉月) / Silent Moon",
        ["npc_baekgyu_sternheim"] = "백규 (白圭) / Baekgyu Sternheim",
        ["hero_aegis_sentinel"] = "방진 (方陣) / Aegis Sentinel",
        ["hero_shardblade"] = "편검 (片劍) / Shardblade",
        ["hero_prism_seeker"] = "광로 (光路) / Prism Seeker",
        ["hero_ember_runner"] = "연주 (燕走) / Ember Runner",
        ["hero_iron_pelt"] = "철피 (鐵皮) / Iron Pelt",
        ["npc_aldric"] = "단현 스턴홀트 (丹玄) / Aldric Sternfeld",
    };

    public static IReadOnlyList<string> CanonicalCharacterIds => CharacterIds;

    public static int GetSortOrder(string characterId)
    {
        return CharacterOrder.TryGetValue(characterId ?? string.Empty, out var order)
            ? order
            : int.MaxValue;
    }

    public static int GetAuthoringSortOrder(string characterId)
    {
        return AuthoringCharacterOrder.TryGetValue(characterId ?? string.Empty, out var order)
            ? order
            : GetSortOrder(characterId);
    }

    public static bool TryGetSlotId(string characterId, out string slotId)
    {
        if (CharacterOrder.TryGetValue(characterId ?? string.Empty, out var order))
        {
            slotId = FormatSlotId(order);
            return true;
        }

        slotId = string.Empty;
        return false;
    }

    public static bool HasDefinedLabel(string characterId)
    {
        return TryGetDefinedDisplayName(characterId, out _);
    }

    public static bool ShouldShowInAuthoring(string characterId)
    {
        return !AuthoringHiddenAliases.Contains(characterId ?? string.Empty)
            && TryGetDefinedDisplayName(characterId, out _);
    }

    public static string BuildAuthoringLabel(string characterId, string displayName)
    {
        var resolvedDisplayName = TryGetDefinedDisplayName(characterId, out var definedDisplayName)
            ? definedDisplayName
            : string.IsNullOrWhiteSpace(displayName) ? characterId : displayName;

        if (TryGetAuthoringSlotId(characterId, out var slotId)
            || TryGetSlotId(characterId, out slotId))
        {
            return $"{slotId} · {resolvedDisplayName}";
        }

        return $"{resolvedDisplayName} · {characterId}";
    }

    public static string BuildPresetDisplayName(string characterId, string displayName)
    {
        if (TryGetDefinedDisplayName(characterId, out var confirmedDisplayName))
        {
            return confirmedDisplayName;
        }

        return string.IsNullOrWhiteSpace(displayName) ? characterId : displayName;
    }

    public static bool TryGetDefinedDisplayName(string characterId, out string displayName)
    {
        return ConfirmedDisplayNames.TryGetValue(characterId ?? string.Empty, out displayName);
    }

    private static string FormatSlotId(int zeroBasedOrder)
    {
        return $"chr_{zeroBasedOrder + 1:0000}";
    }

    private static bool TryGetAuthoringSlotId(string characterId, out string slotId)
    {
        if (AuthoringCharacterOrder.TryGetValue(characterId ?? string.Empty, out var order))
        {
            slotId = FormatSlotId(order);
            return true;
        }

        slotId = string.Empty;
        return false;
    }

    private static Dictionary<string, int> BuildCharacterOrder(IReadOnlyList<string> characterIds)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < characterIds.Count; i++)
        {
            result[characterIds[i]] = i;
        }

        return result;
    }
}

}
