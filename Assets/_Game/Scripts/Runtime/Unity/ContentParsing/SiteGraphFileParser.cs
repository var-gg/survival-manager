using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SM.Content.Definitions;
using SM.Core.Content;
using UnityEngine;
using static SM.Unity.ContentParsing.YamlFieldExtractor;

namespace SM.Unity.ContentParsing;

internal static class SiteGraphFileParser
{
    internal static IReadOnlyList<SiteGraphDefinition> LoadSiteGraphs()
    {
        return RuntimeCombatContentFileParser.LoadAssets("SiteGraphs", path =>
        {
            var definition = Parse(File.ReadAllLines(path));
            ApplyFallbackIdentity(definition, path);
            return definition;
        }).Values.ToList();
    }

    internal static SiteGraphDefinition Parse(string[] lines)
    {
        var definition = ScriptableObject.CreateInstance<SiteGraphDefinition>();
        definition.Id = ExtractValue(lines, "Id:");
        definition.SiteId = ExtractValue(lines, "SiteId:");
        definition.Nodes = ParseNodes(lines);
        return definition;
    }

    private static List<SiteGraphNodeDefinition> ParseNodes(string[] lines)
    {
        var result = new List<SiteGraphNodeDefinition>();
        var index = FindLineIndex(lines, "Nodes:");
        if (index < 0)
        {
            return result;
        }

        for (index++; index < lines.Length; index++)
        {
            var trimmed = lines[index].Trim();
            if (!trimmed.StartsWith("- NodeId:", StringComparison.Ordinal))
            {
                if (GetIndent(lines[index]) <= 2 && trimmed.EndsWith(":", StringComparison.Ordinal))
                {
                    break;
                }

                continue;
            }

            var node = new SiteGraphNodeDefinition
            {
                NodeId = trimmed["- NodeId:".Length..].Trim()
            };

            for (index++; index < lines.Length; index++)
            {
                trimmed = lines[index].Trim();
                if (trimmed.StartsWith("- NodeId:", StringComparison.Ordinal)
                    || (GetIndent(lines[index]) <= 2 && trimmed.EndsWith(":", StringComparison.Ordinal)))
                {
                    index--;
                    break;
                }

                if (trimmed.StartsWith("Rank:", StringComparison.Ordinal))
                {
                    node.Rank = ParseInt(trimmed["Rank:".Length..].Trim());
                }
                else if (trimmed.StartsWith("Kind:", StringComparison.Ordinal))
                {
                    node.Kind = (SiteNodeKindValue)ParseInt(trimmed["Kind:".Length..].Trim());
                }
                else if (trimmed.StartsWith("LaneTag:", StringComparison.Ordinal))
                {
                    node.LaneTag = trimmed["LaneTag:".Length..].Trim();
                }
                else if (trimmed.StartsWith("EncounterId:", StringComparison.Ordinal))
                {
                    node.EncounterId = trimmed["EncounterId:".Length..].Trim();
                }
                else if (trimmed.StartsWith("EventId:", StringComparison.Ordinal))
                {
                    node.EventId = trimmed["EventId:".Length..].Trim();
                }
                else if (string.Equals(trimmed, "NextNodeIds: []", StringComparison.Ordinal))
                {
                    node.NextNodeIds = new List<string>();
                }
                else if (string.Equals(trimmed, "NextNodeIds:", StringComparison.Ordinal))
                {
                    node.NextNodeIds = ParseNextNodeIds(lines, ref index);
                }
                else if (trimmed.StartsWith("EchoIncome:", StringComparison.Ordinal))
                {
                    node.EchoIncome = ParseInt(trimmed["EchoIncome:".Length..].Trim());
                }
                else if (trimmed.StartsWith("RewardSourceId:", StringComparison.Ordinal))
                {
                    node.RewardSourceId = trimmed["RewardSourceId:".Length..].Trim();
                }
            }

            result.Add(node);
        }

        return result;
    }

    private static List<string> ParseNextNodeIds(string[] lines, ref int index)
    {
        var result = new List<string>();
        for (index++; index < lines.Length; index++)
        {
            var trimmed = lines[index].Trim();
            if (GetIndent(lines[index]) < 4 || !trimmed.StartsWith("-", StringComparison.Ordinal))
            {
                index--;
                break;
            }

            var value = trimmed[1..].Trim();
            if (!string.IsNullOrWhiteSpace(value))
            {
                result.Add(value);
            }
        }

        return result;
    }
}
