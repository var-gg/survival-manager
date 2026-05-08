using System;
using System.Collections.Generic;

namespace SM.Unity
{

internal static class BattleP09AppearanceRoster
{
    private static readonly string[] CharacterIds =
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
    };

    private static readonly Dictionary<string, int> CharacterOrder = BuildCharacterOrder();
    private static readonly Dictionary<string, string> ConfirmedDisplayNames = new(StringComparer.Ordinal)
    {
        ["hero_dawn_priest"] = "단린 (丹麟) / Dawn Priest",
        ["hero_pack_raider"] = "이빨바람 / Pack Raider",
        ["hero_grave_hexer"] = "묵향 (墨香) / Grave Hexer",
        ["hero_echo_savant"] = "공한 (空閑) / Echo Savant",
        ["npc_lyra_sternfeld"] = "선영 (宣英) / Lyra Sternfeld",
        ["npc_grey_fang"] = "회조 (灰爪) / Grey Fang",
        ["npc_black_vellum"] = "흑지 (黑紙) / Black Vellum",
        ["npc_silent_moon"] = "침월 (沉月) / Silent Moon",
        ["npc_baekgyu_sternheim"] = "백규 (白圭) / Baekgyu Sternheim",
    };

    public static IReadOnlyList<string> CanonicalCharacterIds => CharacterIds;

    public static int GetSortOrder(string characterId)
    {
        return CharacterOrder.TryGetValue(characterId ?? string.Empty, out var order)
            ? order
            : int.MaxValue;
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

    public static string BuildAuthoringLabel(string characterId, string displayName)
    {
        var resolvedDisplayName = TryGetDefinedDisplayName(characterId, out var definedDisplayName)
            ? definedDisplayName
            : string.IsNullOrWhiteSpace(displayName) ? characterId : displayName;

        return TryGetSlotId(characterId, out var slotId)
            ? $"{slotId} · {resolvedDisplayName}"
            : $"{resolvedDisplayName} · {characterId}";
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

    private static Dictionary<string, int> BuildCharacterOrder()
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < CharacterIds.Length; i++)
        {
            result[CharacterIds[i]] = i;
        }

        return result;
    }
}

}
