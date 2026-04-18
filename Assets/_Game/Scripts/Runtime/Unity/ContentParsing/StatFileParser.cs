using System.Collections.Generic;
using System.IO;
using SM.Content.Definitions;
using SM.Core.Content;
using UnityEngine;
using static SM.Unity.ContentParsing.YamlFieldExtractor;

namespace SM.Unity.ContentParsing;

internal static class StatFileParser
{
    internal static Dictionary<string, StatDefinition> LoadStats(IReadOnlyDictionary<string, string> guidToPath)
    {
        return RuntimeCombatContentFileParser.LoadAssets("Stats", path =>
        {
            var lines = File.ReadAllLines(path);
            var definition = ScriptableObject.CreateInstance<StatDefinition>();
            definition.Id = ExtractValue(lines, "Id:");
            definition.NameKey = ContentLocalizationTables.BuildStatNameKey(definition.Id);
            definition.DescriptionKey = ContentLocalizationTables.BuildStatDescriptionKey(definition.Id);
            SetLegacyField(definition, "legacyDisplayName", ExtractValue(lines, "DisplayName:"));
            SetLegacyField(definition, "legacyDescription", ExtractValue(lines, "Description:"));
            return definition;
        }, guidToPath);
    }
}
