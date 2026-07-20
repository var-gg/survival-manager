using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SM.Content.Definitions;
using SM.Core.Content;
using UnityEngine;
using static SM.Unity.ContentParsing.YamlFieldExtractor;

namespace SM.Unity.ContentParsing;

internal static class SiteEventFileParser
{
    internal static IReadOnlyList<SiteEventDefinition> LoadSiteEvents()
    {
        return RuntimeCombatContentFileParser.LoadAssets("SiteEvents", path =>
        {
            var definition = Parse(File.ReadAllLines(path));
            ApplyFallbackIdentity(definition, path);
            return definition;
        }).Values.ToList();
    }

    internal static SiteEventDefinition Parse(string[] lines)
    {
        var definition = ScriptableObject.CreateInstance<SiteEventDefinition>();
        definition.Id = ExtractValue(lines, "Id:");
        definition.SiteId = UnquoteScalar(ExtractValue(lines, "SiteId:"));
        definition.SetupKey = ExtractValue(lines, "SetupKey:");
        definition.Choices = ParseChoices(lines);
        return definition;
    }

    private static List<SiteEventChoiceDefinition> ParseChoices(string[] lines)
    {
        var result = new List<SiteEventChoiceDefinition>();
        var index = FindLineIndex(lines, "Choices:");
        if (index < 0) return result;

        for (index++; index < lines.Length; index++)
        {
            var trimmed = lines[index].Trim();
            if (!trimmed.StartsWith("- Id:", StringComparison.Ordinal))
            {
                if (GetIndent(lines[index]) <= 2 && trimmed.EndsWith(":", StringComparison.Ordinal)) break;
                continue;
            }

            var choiceIndent = GetIndent(lines[index]);
            var choice = new SiteEventChoiceDefinition
            {
                Id = trimmed["- Id:".Length..].Trim(),
            };
            for (index++; index < lines.Length; index++)
            {
                trimmed = lines[index].Trim();
                var indent = GetIndent(lines[index]);
                if ((indent == choiceIndent && trimmed.StartsWith("- Id:", StringComparison.Ordinal))
                    || (indent < choiceIndent && trimmed.EndsWith(":", StringComparison.Ordinal)))
                {
                    index--;
                    break;
                }

                if (trimmed.StartsWith("LabelKey:", StringComparison.Ordinal))
                {
                    choice.LabelKey = trimmed["LabelKey:".Length..].Trim();
                }
                else if (string.Equals(trimmed, "Outcomes: []", StringComparison.Ordinal))
                {
                    choice.Outcomes = new List<SiteEventOutcomeDefinition>();
                }
                else if (string.Equals(trimmed, "Outcomes:", StringComparison.Ordinal))
                {
                    choice.Outcomes = ParseOutcomes(lines, ref index, indent);
                }
            }

            result.Add(choice);
        }

        return result;
    }

    private static List<SiteEventOutcomeDefinition> ParseOutcomes(string[] lines, ref int index, int sectionIndent)
    {
        var result = new List<SiteEventOutcomeDefinition>();
        for (index++; index < lines.Length; index++)
        {
            var trimmed = lines[index].Trim();
            var indent = GetIndent(lines[index]);
            if (indent < sectionIndent
                || (indent == sectionIndent && !trimmed.StartsWith("- Kind:", StringComparison.Ordinal)))
            {
                index--;
                break;
            }

            if (!trimmed.StartsWith("- Kind:", StringComparison.Ordinal)) continue;
            var outcomeIndent = indent;
            var outcome = new SiteEventOutcomeDefinition
            {
                Kind = (OutcomeKind)ParseInt(trimmed["- Kind:".Length..].Trim()),
            };
            for (index++; index < lines.Length; index++)
            {
                trimmed = lines[index].Trim();
                indent = GetIndent(lines[index]);
                if (indent <= sectionIndent || (indent == outcomeIndent && trimmed.StartsWith("- Kind:", StringComparison.Ordinal)))
                {
                    index--;
                    break;
                }

                if (trimmed.StartsWith("PayloadId:", StringComparison.Ordinal))
                    outcome.PayloadId = trimmed["PayloadId:".Length..].Trim();
                else if (trimmed.StartsWith("AuxiliaryId:", StringComparison.Ordinal))
                    outcome.AuxiliaryId = trimmed["AuxiliaryId:".Length..].Trim();
                else if (trimmed.StartsWith("Amount:", StringComparison.Ordinal))
                    outcome.Amount = ParseInt(trimmed["Amount:".Length..].Trim());
                else if (trimmed.StartsWith("TargetRule:", StringComparison.Ordinal))
                    outcome.TargetRule = (OutcomeTargetRule)ParseInt(trimmed["TargetRule:".Length..].Trim());
            }

            result.Add(outcome);
        }

        return result;
    }

    private static string UnquoteScalar(string value)
    {
        if (value.Length >= 2
            && ((value[0] == '\'' && value[^1] == '\'')
                || (value[0] == '"' && value[^1] == '"')))
        {
            return value[1..^1];
        }

        return value;
    }
}
