using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using SM.Atlas.Model;

namespace SM.Atlas.Services;

public static class AtlasContextHasher
{
    public static string BuildNodeOverlayHash(
        string regionId,
        AtlasRegionNode node,
        string routeId,
        string cycleSalt,
        IReadOnlyList<AtlasSigilInfluence> affectingSigils)
    {
        var builder = new StringBuilder();
        builder.Append("atlas-node-overlay|")
            .Append(regionId).Append('|')
            .Append(node.NodeId).Append('|')
            .Append(node.Hex).Append('|')
            .Append(routeId).Append('|')
            .Append(cycleSalt);

        foreach (var sigil in SortedSigils(affectingSigils))
        {
            builder.Append('|')
                .Append(sigil.SigilId).Append('@')
                .Append(sigil.AnchorHex).Append(':')
                .Append(sigil.Distance).Append(':')
                .Append(sigil.Category).Append(':')
                .Append(sigil.EffectivePercent);
        }

        return ComputeHash(builder.ToString());
    }

    public static string BuildBattleContextHash(
        string runId,
        string chapterId,
        string siteId,
        int nodeIndex,
        string encounterId,
        string nodeOverlayHash,
        string squadSnapshotId)
    {
        var input = string.Join(
            "|",
            "atlas-battle-context",
            runId,
            chapterId,
            siteId,
            nodeIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
            encounterId,
            nodeOverlayHash,
            squadSnapshotId);

        return ComputeHash(input);
    }

    public static IReadOnlyList<string> SortedSigilIds(IEnumerable<AtlasSigilInfluence> affectingSigils)
    {
        return SortedSigils(affectingSigils).Select(sigil => sigil.SigilId).Distinct(StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<AtlasSigilInfluence> SortedSigils(IEnumerable<AtlasSigilInfluence> affectingSigils)
    {
        return (affectingSigils ?? Array.Empty<AtlasSigilInfluence>())
            .OrderBy(sigil => sigil.SigilId, StringComparer.Ordinal)
            .ThenBy(sigil => sigil.Category)
            .ThenBy(sigil => sigil.Label, StringComparer.Ordinal)
            .ToArray();
    }

    private static string ComputeHash(string input)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
    }
}
