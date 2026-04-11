using System.IO;
using System.Linq;
using SM.Content;
using SM.Content.Definitions;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace SM.Editor.Narrative;

public static class NarrativeAssetValidator
{
    private const string StoryEventsDir = "Assets/Resources/_Game/Content/Definitions/StoryEvents";
    private const string DialogueSequencesDir = "Assets/Resources/_Game/Content/Definitions/DialogueSequences";
    private const string StoryTableName = ContentLocalizationTables.Story;

    [MenuItem("SM/Narrative/Validate Narrative Assets")]
    public static void Validate()
    {
        int errors = 0;
        int warnings = 0;

        var eventGuids = AssetDatabase.FindAssets($"t:{nameof(StoryEventDefinition)}", new[] { StoryEventsDir });
        if (eventGuids.Length == 0)
        {
            Debug.LogError("[NarrativeAssetValidator] No StoryEventDefinition assets found. Run SM/Narrative/Import Narrative Seeds.");
            errors++;
        }
        else
        {
            Debug.Log($"[NarrativeAssetValidator] StoryEvents: {eventGuids.Length} assets");
        }

        var seqGuids = AssetDatabase.FindAssets($"t:{nameof(DialogueSequenceDefinition)}", new[] { DialogueSequencesDir });
        if (seqGuids.Length == 0)
        {
            Debug.LogError("[NarrativeAssetValidator] No DialogueSequenceDefinition assets found. Run SM/Narrative/Import Narrative Seeds.");
            errors++;
        }
        else
        {
            Debug.Log($"[NarrativeAssetValidator] DialogueSequences: {seqGuids.Length} assets");
        }

        var collection = LocalizationEditorSettings.GetStringTableCollection(StoryTableName);
        if (collection == null)
        {
            Debug.LogError($"[NarrativeAssetValidator] String table collection '{StoryTableName}' not found.");
            errors++;
        }
        else
        {
            var koTable = collection.GetTable(new UnityEngine.Localization.LocaleIdentifier("ko")) as StringTable;
            if (koTable != null)
            {
                int emptyCount = 0;
                foreach (var entry in koTable.Values)
                {
                    if (entry != null && entry.Key.StartsWith("loc.story.") && string.IsNullOrWhiteSpace(entry.Value))
                    {
                        emptyCount++;
                    }
                }

                if (emptyCount > 0)
                {
                    Debug.LogWarning($"[NarrativeAssetValidator] {emptyCount} empty ko values in {StoryTableName}");
                    warnings += emptyCount;
                }
            }
        }

        NarrativePortraitValidator.ValidatePortraits();

        if (errors == 0 && warnings == 0)
        {
            Debug.Log("[NarrativeAssetValidator] All narrative assets are valid.");
        }
        else
        {
            Debug.Log($"[NarrativeAssetValidator] Validation complete: {errors} errors, {warnings} warnings");
        }
    }
}
