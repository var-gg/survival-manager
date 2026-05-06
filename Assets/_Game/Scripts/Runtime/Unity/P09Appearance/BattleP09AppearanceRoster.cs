using System;
using System.Collections.Generic;

namespace SM.Unity
{

internal static class BattleP09AppearanceRoster
{
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
    };

    private static readonly Dictionary<string, int> CharacterOrder = BuildCharacterOrder();

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

    public static string BuildAuthoringLabel(string characterId, string displayName)
    {
        var resolvedDisplayName = string.IsNullOrWhiteSpace(displayName) ? characterId : displayName;
        return TryGetSlotId(characterId, out var slotId)
            ? $"{slotId} · {resolvedDisplayName}"
            : resolvedDisplayName;
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
