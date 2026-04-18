using System.Collections.Generic;
using System.IO;
using SM.Content.Definitions;
using SM.Core.Content;
using UnityEngine;
using static SM.Unity.ContentParsing.YamlFieldExtractor;

namespace SM.Unity.ContentParsing;

internal static class CharacterFileParser
{
    internal static Dictionary<string, CharacterDefinition> LoadCharacters(
        IReadOnlyDictionary<string, string> guidToPath,
        IReadOnlyDictionary<string, RaceDefinition> races,
        IReadOnlyDictionary<string, ClassDefinition> classes,
        IReadOnlyDictionary<string, UnitArchetypeDefinition> archetypes,
        IReadOnlyDictionary<string, RoleInstructionDefinition> roleInstructions)
    {
        return RuntimeCombatContentFileParser.LoadAssets("Characters", path =>
        {
            var lines = File.ReadAllLines(path);
            var definition = ScriptableObject.CreateInstance<CharacterDefinition>();
            definition.Id = ExtractValue(lines, "Id:");
            definition.NameKey = ExtractValue(lines, "NameKey:");
            definition.DescriptionKey = ExtractValue(lines, "DescriptionKey:");
            definition.Race = ResolveReference(lines, "Race:", guidToPath, races);
            definition.Class = ResolveReference(lines, "Class:", guidToPath, classes);
            definition.DefaultArchetype = ResolveReference(lines, "DefaultArchetype:", guidToPath, archetypes);
            definition.DefaultRoleInstruction = ResolveReference(lines, "DefaultRoleInstruction:", guidToPath, roleInstructions);
            if (definition.DefaultRoleInstruction == null
                && definition.DefaultArchetype != null
                && !string.IsNullOrWhiteSpace(definition.DefaultArchetype.RoleTag))
            {
                if (roleInstructions.TryGetValue(definition.DefaultArchetype.RoleTag, out var roleInstruction))
                {
                    definition.DefaultRoleInstruction = roleInstruction;
                }
            }

            SetLegacyField(definition, "legacyDisplayName", ExtractValue(lines, "DisplayName:"));
            SetLegacyField(definition, "legacyDescription", ExtractValue(lines, "Description:"));
            ApplyFallbackIdentity(definition, path);
            if (string.IsNullOrWhiteSpace(definition.NameKey))
            {
                definition.NameKey = ContentLocalizationTables.BuildCharacterNameKey(definition.Id);
            }

            if (string.IsNullOrWhiteSpace(definition.DescriptionKey))
            {
                definition.DescriptionKey = ContentLocalizationTables.BuildCharacterDescriptionKey(definition.Id);
            }

            return definition;
        }, guidToPath);
    }
}
