using System;
using System.Collections.Generic;
using System.Linq;
using SM.Content;
using SM.Core;
using SM.Editor.SeedData;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace SM.Editor.Validation;

public static class NarrativeContentValidator
{
    private const string StoryEventsResourcesPath = "_Game/Content/Definitions/StoryEvents";
    private const string DialogueSequencesResourcesPath = "_Game/Content/Definitions/DialogueSequences";
    private const string ChapterBeatsResourcesPath = "_Game/Content/Definitions/ChapterBeats";
    private const string HeroLoreResourcesPath = "_Game/Content/Definitions/HeroLore";
    private const string StoryTableName = "Content_Story";

    [MenuItem("SM/Validation/Validate Narrative Content")]
    public static void ValidateOrThrow()
    {
        NarrativeSeedData.ValidateSeedCountsOrThrow();

        var errors = new List<string>();
        var storyEvents = Resources.LoadAll<StoryEventDefinition>(StoryEventsResourcesPath);
        var dialogueSequences = Resources.LoadAll<DialogueSequenceDefinition>(DialogueSequencesResourcesPath);
        var chapterBeats = Resources.LoadAll<ChapterBeatDefinition>(ChapterBeatsResourcesPath);
        var heroLore = Resources.LoadAll<HeroLoreDefinition>(HeroLoreResourcesPath);

        ValidateCount("StoryEventDefinition", storyEvents.Length, NarrativeSeedData.ExpectedStoryEventCount, errors);
        ValidateCount("DialogueSequenceDefinition", dialogueSequences.Length, NarrativeSeedData.ExpectedDialogueSequenceCount, errors);
        ValidateCount("ChapterBeatDefinition", chapterBeats.Length, NarrativeSeedData.ExpectedChapterBeatCount, errors);
        ValidateCount("HeroLoreDefinition", heroLore.Length, NarrativeSeedData.ExpectedHeroLoreCount, errors);

        ValidateUniqueIds(storyEvents, definition => definition.Id, "story_event_id", errors);
        ValidateUniqueIds(dialogueSequences, definition => definition.Id, "dialogue_sequence_id", errors);
        ValidateUniqueIds(chapterBeats, definition => definition.Id, "chapter_beat_id", errors);
        ValidateUniqueIds(heroLore, definition => definition.Id, "hero_lore_id", errors);

        ValidateAssetLocations(storyEvents, "Assets/Resources/_Game/Content/Definitions/StoryEvents", errors);
        ValidateAssetLocations(dialogueSequences, "Assets/Resources/_Game/Content/Definitions/DialogueSequences", errors);
        ValidateAssetLocations(chapterBeats, "Assets/Resources/_Game/Content/Definitions/ChapterBeats", errors);
        ValidateAssetLocations(heroLore, "Assets/Resources/_Game/Content/Definitions/HeroLore", errors);

        ValidateConditionScopes(storyEvents, chapterBeats, errors);
        ValidateStoryFlags(storyEvents, errors);
        ValidateBeatCoverage(chapterBeats, errors);
        ValidatePresentationReferences(storyEvents, dialogueSequences, errors);

        var collection = LocalizationEditorSettings.GetStringTableCollection(StoryTableName);
        if (collection == null)
        {
            errors.Add("[NarrativeValidator] Missing string table collection 'Content_Story'.");
        }
        else
        {
            var sharedData = collection.SharedData;
            var koTable = collection.GetTable(new LocaleIdentifier("ko")) as StringTable;
            if (sharedData == null)
            {
                errors.Add("[NarrativeValidator] Content_Story shared table data is missing.");
            }

            if (koTable == null)
            {
                errors.Add("[NarrativeValidator] Missing ko locale table for Content_Story.");
            }

            if (sharedData != null && koTable != null)
            {
                ValidateDialogueTextKeys(dialogueSequences, sharedData, koTable, errors);
                ValidateHeroLoreTextKeys(heroLore, sharedData, koTable, errors);
                ValidatePresentationTextKeys(sharedData, koTable, errors);
            }
        }

        if (errors.Count > 0)
        {
            throw new BuildFailedException("[NarrativeValidator]\n" + string.Join("\n", errors));
        }
    }

    private static void ValidateCount(string label, int actual, int expected, ICollection<string> errors)
    {
        if (actual != expected)
        {
            errors.Add($"[NarrativeValidator] Expected {expected} {label} assets, found {actual}.");
        }
    }

    private static void ValidateUniqueIds<T>(
        IEnumerable<T> assets,
        Func<T, string> idSelector,
        string label,
        ICollection<string> errors)
        where T : UnityEngine.Object
    {
        foreach (var group in assets.GroupBy(idSelector, StringComparer.Ordinal).Where(group => !string.IsNullOrWhiteSpace(group.Key) && group.Count() > 1))
        {
            var assetPaths = string.Join(", ", group.Select(AssetDatabase.GetAssetPath));
            errors.Add($"[NarrativeValidator] Duplicate {label} '{group.Key}' found in {assetPaths}.");
        }
    }

    private static void ValidateAssetLocations<T>(
        IEnumerable<T> assets,
        string expectedPrefix,
        ICollection<string> errors)
        where T : UnityEngine.Object
    {
        foreach (var asset in assets)
        {
            var path = AssetDatabase.GetAssetPath(asset);
            if (!path.StartsWith(expectedPrefix, StringComparison.Ordinal))
            {
                errors.Add($"[NarrativeValidator] Narrative asset '{path}' is outside '{expectedPrefix}'.");
            }
        }
    }

    private static void ValidateDialogueTextKeys(
        IEnumerable<DialogueSequenceDefinition> dialogueSequences,
        SharedTableData sharedData,
        StringTable koTable,
        ICollection<string> errors)
    {
        foreach (var sequence in dialogueSequences)
        {
            var lines = sequence.Lines ?? Array.Empty<DialogueLineDefinition>();
            for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                var line = lines[lineIndex];
                if (line == null)
                {
                    errors.Add($"[NarrativeValidator] Dialogue sequence '{sequence.Id}' line {lineIndex} is missing.");
                    continue;
                }

                var textKey = line.TextKey;
                if (string.IsNullOrWhiteSpace(textKey) || sharedData.GetEntry(textKey) == null)
                {
                    errors.Add($"[NarrativeValidator] Missing Content_Story key '{textKey}' referenced by dialogue sequence '{sequence.Id}' line {lineIndex}.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(koTable.GetEntry(textKey)?.Value))
                {
                    errors.Add($"[NarrativeValidator] Empty ko value for key '{textKey}' referenced by dialogue sequence '{sequence.Id}' line {lineIndex}.");
                }
            }
        }
    }

    private static void ValidateHeroLoreTextKeys(
        IEnumerable<HeroLoreDefinition> heroLoreDefinitions,
        SharedTableData sharedData,
        StringTable koTable,
        ICollection<string> errors)
    {
        var seedByHeroId = NarrativeSeedData.HeroLore
            .ToDictionary(seed => seed.HeroId, seed => seed, StringComparer.Ordinal);

        foreach (var heroLore in heroLoreDefinitions)
        {
            if (!seedByHeroId.TryGetValue(heroLore.HeroId, out var seed))
            {
                errors.Add($"[NarrativeValidator] Missing hero lore seed for hero '{heroLore.HeroId}'.");
                continue;
            }

            ValidateRequiredKoEntry(sharedData, koTable, NarrativeSeedData.BuildHeroLoreTitleKey(heroLore.HeroId), $"hero lore '{heroLore.Id}' title", errors);
            ValidateRequiredKoEntry(sharedData, koTable, NarrativeSeedData.BuildHeroLoreSummaryKey(heroLore.HeroId), $"hero lore '{heroLore.Id}' summary", errors);
            ValidateRequiredKoEntry(sharedData, koTable, NarrativeSeedData.BuildHeroLoreCanonKey(heroLore.HeroId), $"hero lore '{heroLore.Id}' canon", errors);

            for (var bodyIndex = 0; bodyIndex < seed.BodyKo.Length; bodyIndex++)
            {
                ValidateRequiredKoEntry(
                    sharedData,
                    koTable,
                    NarrativeSeedData.BuildHeroLoreBodyKey(heroLore.HeroId, bodyIndex),
                    $"hero lore '{heroLore.Id}' body {bodyIndex}",
                    errors);
            }
        }
    }

    private static void ValidatePresentationTextKeys(
        SharedTableData sharedData,
        StringTable koTable,
        ICollection<string> errors)
    {
        foreach (var presentation in NarrativeSeedData.PresentationTexts)
        {
            ValidateRequiredKoEntry(
                sharedData,
                koTable,
                NarrativeSeedData.BuildPresentationTitleKey(presentation.PresentationKey),
                $"presentation '{presentation.PresentationKey}' title",
                errors);
            ValidateRequiredKoEntry(
                sharedData,
                koTable,
                NarrativeSeedData.BuildPresentationBodyKey(presentation.PresentationKey),
                $"presentation '{presentation.PresentationKey}' body",
                errors);
        }
    }

    private static void ValidateRequiredKoEntry(
        SharedTableData sharedData,
        StringTable koTable,
        string key,
        string ownerLabel,
        ICollection<string> errors)
    {
        if (sharedData.GetEntry(key) == null)
        {
            errors.Add($"[NarrativeValidator] Missing Content_Story key '{key}' referenced by {ownerLabel}.");
            return;
        }

        if (string.IsNullOrWhiteSpace(koTable.GetEntry(key)?.Value))
        {
            errors.Add($"[NarrativeValidator] Empty ko value for key '{key}' referenced by {ownerLabel}.");
        }
    }

    private static void ValidateConditionScopes(
        IEnumerable<StoryEventDefinition> storyEvents,
        IEnumerable<ChapterBeatDefinition> chapterBeats,
        ICollection<string> errors)
    {
        var validChapterIds = new HashSet<string>(NarrativeSeedData.ChapterIds, StringComparer.Ordinal);
        var validSiteIds = new HashSet<string>(NarrativeSeedData.SiteIds, StringComparer.Ordinal);

        foreach (var storyEvent in storyEvents)
        {
            foreach (var condition in storyEvent.Conditions ?? Array.Empty<StoryConditionDefinition>())
            {
                if (condition == null)
                {
                    continue;
                }

                if (condition.Kind == StoryConditionKind.ChapterIs && !validChapterIds.Contains(condition.OperandA))
                {
                    errors.Add($"[NarrativeValidator] Invalid chapterId '{condition.OperandA}' in '{storyEvent.Id}'.");
                }

                if (condition.Kind == StoryConditionKind.SiteIs && !validSiteIds.Contains(condition.OperandA))
                {
                    errors.Add($"[NarrativeValidator] Invalid siteId '{condition.OperandA}' in '{storyEvent.Id}'.");
                }
            }
        }

        foreach (var beat in chapterBeats)
        {
            if (!validChapterIds.Contains(beat.ChapterId))
            {
                errors.Add($"[NarrativeValidator] Invalid chapterId '{beat.ChapterId}' in '{beat.Id}'.");
            }

            if (!validSiteIds.Contains(beat.SiteId))
            {
                errors.Add($"[NarrativeValidator] Invalid siteId '{beat.SiteId}' in '{beat.Id}'.");
            }
        }
    }

    private static void ValidateStoryFlags(IEnumerable<StoryEventDefinition> storyEvents, ICollection<string> errors)
    {
        var validFlags = new HashSet<string>(NarrativeSeedData.StoryFlags, StringComparer.Ordinal);

        foreach (var storyEvent in storyEvents)
        {
            foreach (var condition in storyEvent.Conditions ?? Array.Empty<StoryConditionDefinition>())
            {
                if (condition == null)
                {
                    continue;
                }

                if ((condition.Kind == StoryConditionKind.FlagSet || condition.Kind == StoryConditionKind.FlagNotSet)
                    && !validFlags.Contains(condition.OperandA))
                {
                    errors.Add($"[NarrativeValidator] Unknown story flag '{condition.OperandA}' referenced by '{storyEvent.Id}'.");
                }
            }

            foreach (var effect in storyEvent.Effects ?? Array.Empty<StoryEffectDefinition>())
            {
                if (effect == null)
                {
                    continue;
                }

                if ((effect.Kind == StoryEffectKind.SetFlag || effect.Kind == StoryEffectKind.ClearFlag)
                    && !validFlags.Contains(effect.Payload))
                {
                    errors.Add($"[NarrativeValidator] Unknown story flag '{effect.Payload}' referenced by '{storyEvent.Id}'.");
                }
            }
        }
    }

    private static void ValidateBeatCoverage(IEnumerable<ChapterBeatDefinition> chapterBeats, ICollection<string> errors)
    {
        var beats = chapterBeats.ToArray();
        if (beats.Length != NarrativeSeedData.ExpectedChapterBeatCount)
        {
            errors.Add($"[NarrativeValidator] Expected {NarrativeSeedData.ExpectedChapterBeatCount} ChapterBeatDefinition assets, found {beats.Length}.");
        }

        foreach (var duplicate in beats.GroupBy(beat => (beat.ChapterId, beat.SiteId, beat.NodeIndex)).Where(group => group.Count() > 1))
        {
            var assetPaths = string.Join(", ", duplicate.Select(AssetDatabase.GetAssetPath));
            errors.Add($"[NarrativeValidator] Duplicate beat node ({duplicate.Key.ChapterId}, {duplicate.Key.SiteId}, {duplicate.Key.NodeIndex}) in {assetPaths}.");
        }

        var chapterSiteGroups = beats
            .GroupBy(beat => (beat.ChapterId, beat.SiteId))
            .ToArray();

        if (chapterSiteGroups.Length != 10)
        {
            errors.Add($"[NarrativeValidator] Invalid beat coverage: found {chapterSiteGroups.Length} unique chapter/site pairs; expected 10.");
        }

        foreach (var group in chapterSiteGroups)
        {
            var nodes = group.Select(beat => beat.NodeIndex).OrderBy(index => index).ToArray();
            if (nodes.Length != 5 || !nodes.SequenceEqual(new[] { 1, 2, 3, 4, 5 }))
            {
                errors.Add($"[NarrativeValidator] Invalid beat coverage: pair ({group.Key.ChapterId}, {group.Key.SiteId}) has nodes [{string.Join(", ", nodes)}]; expected [1, 2, 3, 4, 5].");
            }
        }

        var chapterCoverage = chapterSiteGroups
            .GroupBy(group => group.Key.ChapterId)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        if (chapterCoverage.Count != 5)
        {
            errors.Add($"[NarrativeValidator] Invalid beat coverage: found {chapterCoverage.Count} chapters; expected 5.");
        }

        foreach (var pair in chapterCoverage.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (pair.Value != 2)
            {
                errors.Add($"[NarrativeValidator] Invalid beat coverage: chapter '{pair.Key}' has {pair.Value} sites; expected 2.");
            }
        }
    }

    private static void ValidatePresentationReferences(
        IEnumerable<StoryEventDefinition> storyEvents,
        IEnumerable<DialogueSequenceDefinition> dialogueSequences,
        ICollection<string> errors)
    {
        var dialogueIds = new HashSet<string>(dialogueSequences.Select(definition => definition.Id), StringComparer.Ordinal);
        var presentationKeys = new HashSet<string>(NarrativeSeedData.PresentationTexts.Select(seed => seed.PresentationKey), StringComparer.Ordinal);

        foreach (var storyEvent in storyEvents)
        {
            if (storyEvent.Effects == null || storyEvent.Effects.All(effect => effect == null || effect.Kind != StoryEffectKind.EnqueuePresentation))
            {
                errors.Add($"[NarrativeValidator] Story event '{storyEvent.Id}' is missing EnqueuePresentation effect.");
            }

            var presentationKey = storyEvent.PresentationKey;
            if (string.IsNullOrWhiteSpace(presentationKey))
            {
                errors.Add($"[NarrativeValidator] Story event '{storyEvent.Id}' is missing PresentationKey.");
                continue;
            }

            if (presentationKey.StartsWith("dialogue_", StringComparison.Ordinal))
            {
                if (!dialogueIds.Contains(presentationKey))
                {
                    errors.Add($"[NarrativeValidator] Missing dialogue sequence '{presentationKey}' referenced by story event '{storyEvent.Id}'.");
                }

                continue;
            }

            if ((presentationKey.StartsWith("toast_", StringComparison.Ordinal)
                || presentationKey.StartsWith("story_card_", StringComparison.Ordinal))
                && !presentationKeys.Contains(presentationKey))
            {
                errors.Add($"[NarrativeValidator] Missing presentation text seed '{presentationKey}' referenced by story event '{storyEvent.Id}'.");
            }
        }
    }
}
