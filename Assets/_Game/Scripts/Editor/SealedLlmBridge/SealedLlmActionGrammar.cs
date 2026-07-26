using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;

namespace SM.SealedLlmBridge;

internal static class SealedLlmActionGrammar
{
    public const string Skip = "skip";

    public static string DeploymentPair(DeploymentAnchorId anchor, string heroId)
    {
        var anchorId = SealedLlmCanonicalValue.EnumName(anchor, nameof(anchor));
        RequireToken(anchorId, nameof(anchor), '=', ';');
        RequireToken(heroId, nameof(heroId), '=', ';');
        return string.Concat(anchorId, "=", heroId);
    }

    public static string PrepEquipmentPair(string itemInstanceId, string heroId)
    {
        RequireToken(itemInstanceId, nameof(itemInstanceId), '=', ',', '#', '|');
        RequireToken(heroId, nameof(heroId), '=', ',', '#', '|');
        return string.Concat(itemInstanceId, "=", heroId);
    }

    public static string PassiveKey(string heroId, string boardId, string nodeId)
    {
        RequireToken(heroId, nameof(heroId), ':');
        RequireToken(boardId, nameof(boardId), ':');
        RequireToken(nodeId, nameof(nodeId), ':');
        return string.Concat(heroId, ":", boardId, ":", nodeId);
    }

    public static string RefitKey(string itemInstanceId, int slotIndex)
    {
        RequireToken(itemInstanceId, nameof(itemInstanceId), ':');
        if (slotIndex < 0)
        {
            throw new InvalidOperationException("A refit action slot index must be non-negative.");
        }

        return string.Concat(itemInstanceId, ":", SealedLlmCanonicalValue.Invariant(slotIndex));
    }

    public static string RefitSealKey(
        string itemInstanceId,
        int slotIndex,
        IReadOnlyList<string> sealedAffixIds)
    {
        RequireToken(itemInstanceId, nameof(itemInstanceId), ':', ',', '=');
        if (sealedAffixIds == null
            || sealedAffixIds.Count == 0
            || sealedAffixIds.Any(string.IsNullOrWhiteSpace)
            || sealedAffixIds.Distinct(StringComparer.Ordinal).Count() != sealedAffixIds.Count)
        {
            throw new InvalidOperationException(
                "A Seal action requires distinct non-empty affix ids.");
        }

        var canonical = sealedAffixIds.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        foreach (var affixId in canonical)
        {
            RequireToken(affixId, nameof(sealedAffixIds), ':', ',', '=');
        }

        return string.Concat(
            RefitKey(itemInstanceId, slotIndex),
            ":seal=",
            string.Join(",", canonical));
    }

    public static void RequireToken(string value, string path, params char[] reserved)
    {
        if (string.IsNullOrEmpty(value)
            || value.Any(char.IsWhiteSpace)
            || reserved.Any(value.Contains))
        {
            throw new InvalidOperationException(
                $"{path} must be a non-empty action token without whitespace or reserved delimiters.");
        }
    }
}
