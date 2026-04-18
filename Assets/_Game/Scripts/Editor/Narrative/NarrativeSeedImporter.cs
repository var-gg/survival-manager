using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SM.Content;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Core;
using SM.Editor.Bootstrap;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace SM.Editor.Narrative;

public sealed record NarrativeSeedImportOptions(
    bool CleanRemovedAssets,
    bool InsertMissingEnglishEntries,
    bool DryRun);

public sealed record NarrativeSeedImportResult(
    int CreatedAssets,
    int UpdatedAssets,
    int DeletedAssets,
    int CreatedLocalizationEntries,
    int UpdatedLocalizationEntries,
    IReadOnlyList<NarrativeDiagnostic> Diagnostics);

public static class NarrativeSeedImporter
{
    private const string ManifestPath = "Temp/Narrative/narrative-seed.json";
    private const string StoryEventsDir = "Assets/Resources/_Game/Content/Definitions/StoryEvents";
    private const string DialogueSequencesDir = "Assets/Resources/_Game/Content/Definitions/DialogueSequences";
    private const string ArchiveCatalogDir = "Assets/Resources/_Game/Content/Definitions/StoryArchive";
    private const string StoryTableName = ContentLocalizationTables.Story;

    [MenuItem("SM/Narrative/Import Narrative Seeds")]
    public static void ImportFromMenu()
    {
        if (!File.Exists(ManifestPath))
        {
            Debug.LogError($"[NarrativeSeedImporter] Manifest not found at {ManifestPath}. Run 'python tools/narrative_build.py' first.");
            return;
        }

        var manifest = LoadManifest(ManifestPath);
        if (manifest == null)
        {
            Debug.LogError("[NarrativeSeedImporter] Failed to parse manifest.");
            return;
        }

        var result = Import(manifest, new NarrativeSeedImportOptions(
            CleanRemovedAssets: true,
            InsertMissingEnglishEntries: true,
            DryRun: false));

        Debug.Log($"[NarrativeSeedImporter] Done. Created={result.CreatedAssets}, Updated={result.UpdatedAssets}, " +
                  $"Deleted={result.DeletedAssets}, LocCreated={result.CreatedLocalizationEntries}, LocUpdated={result.UpdatedLocalizationEntries}");

        foreach (var diag in result.Diagnostics)
        {
            var msg = $"[NarrativeSeedImporter] [{diag.Severity}] {diag.Code}: {diag.Message} ({diag.SourcePath}:{diag.LineNumber})";
            if (diag.Severity == NarrativeDiagnosticSeverity.Error) Debug.LogError(msg);
            else if (diag.Severity == NarrativeDiagnosticSeverity.Warning) Debug.LogWarning(msg);
            else Debug.Log(msg);
        }
    }

    public static NarrativeSeedImportResult Import(
        NarrativeSeedManifest manifest,
        NarrativeSeedImportOptions options)
    {
        var diagnostics = new List<NarrativeDiagnostic>(manifest.Diagnostics);
        int created = 0, updated = 0, deleted = 0, locCreated = 0, locUpdated = 0;

        EnsureFolder(StoryEventsDir);
        EnsureFolder(DialogueSequencesDir);

        // --- Story Events ---
        var manifestEventIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var ev in manifest.StoryEvents)
        {
            manifestEventIds.Add(ev.EventId);
            var path = $"{StoryEventsDir}/{ev.EventId}.asset";
            bool isNew = !File.Exists(path);
            var asset = CreateOrLoadAsset<StoryEventDefinition>(path);
            ApplyStoryEvent(asset, path, ev);
            EditorUtility.SetDirty(asset);
            if (isNew) created++; else updated++;
        }

        // --- Dialogue Sequences ---
        var manifestSeqIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var seq in manifest.DialogueSequences)
        {
            manifestSeqIds.Add(seq.SequenceId);
            var path = $"{DialogueSequencesDir}/{seq.SequenceId}.asset";
            bool isNew = !File.Exists(path);
            var asset = CreateOrLoadAsset<DialogueSequenceDefinition>(path);
            ApplyDialogueSequence(asset, path, seq);
            EditorUtility.SetDirty(asset);
            if (isNew) created++; else updated++;
        }

        // --- Localization ---
        LocalizationFoundationBootstrap.EnsureFoundationAssets();
        var collection = EnsureStoryTableCollection();
        var koTable = GetOrCreateStringTable(collection, "ko");
        var enTable = GetOrCreateStringTable(collection, "en");

        foreach (var seq in manifest.DialogueSequences)
        {
            foreach (var line in seq.Lines)
            {
                var key = NarrativeLocalizationKeys.DialogueLine(seq.SequenceId, line.LineIndex);
                if (UpsertEntry(koTable, key, line.Text)) locCreated++; else locUpdated++;
                if (options.InsertMissingEnglishEntries) UpsertEntry(enTable, key, "", preserveExisting: true);
            }
        }

        foreach (var pres in manifest.Presentations)
        {
            var titleKey = NarrativeLocalizationKeys.PresentationTitle(pres.PresentationKey);
            var bodyKey = NarrativeLocalizationKeys.PresentationBody(pres.PresentationKey);
            if (UpsertEntry(koTable, titleKey, pres.Title ?? "")) locCreated++; else locUpdated++;
            if (UpsertEntry(koTable, bodyKey, pres.Body ?? "")) locCreated++; else locUpdated++;
            if (options.InsertMissingEnglishEntries)
            {
                UpsertEntry(enTable, titleKey, "", preserveExisting: true);
                UpsertEntry(enTable, bodyKey, "", preserveExisting: true);
            }
        }

        LocalizationEditorSettings.SetPreloadTableFlag(koTable, true);
        LocalizationEditorSettings.SetPreloadTableFlag(enTable, true);
        EditorUtility.SetDirty(collection);
        EditorUtility.SetDirty(collection.SharedData);
        EditorUtility.SetDirty(koTable);
        EditorUtility.SetDirty(enTable);

        // --- Orphan pruning ---
        if (options.CleanRemovedAssets)
        {
            deleted += PruneOrphans<StoryEventDefinition>(StoryEventsDir, manifestEventIds);
            deleted += PruneOrphans<DialogueSequenceDefinition>(DialogueSequencesDir, manifestSeqIds);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

        return new NarrativeSeedImportResult(created, updated, deleted, locCreated, locUpdated, diagnostics);
    }

    public static NarrativeSeedManifest? LoadManifest(string path)
    {
        try
        {
            var json = File.ReadAllText(path, System.Text.Encoding.UTF8);
            var jObj = JObject.Parse(json);
            return new NarrativeSeedManifest(
                jObj["version"]?.Value<int>() ?? 1,
                jObj["sourceHash"]?.Value<string>() ?? "",
                ParseStoryEvents(jObj["storyEvents"] as JArray),
                ParseDialogueSequences(jObj["dialogueSequences"] as JArray),
                ParsePresentations(jObj["presentations"] as JArray),
                ParseArchiveEntries(jObj["archiveEntries"] as JArray),
                ParseDiagnostics(jObj["diagnostics"] as JArray));
        }
        catch (Exception ex)
        {
            Debug.LogError($"[NarrativeSeedImporter] Failed to load manifest: {ex.Message}");
            return null;
        }
    }

    // --- SO apply methods ---

    private static void ApplyStoryEvent(StoryEventDefinition asset, string assetPath, NarrativeStoryEventDto ev)
    {
        var so = new SerializedObject(asset);
        so.FindProperty("_id").stringValue = ev.EventId;
        so.FindProperty("_moment").enumValueIndex = (int)ev.Moment;
        so.FindProperty("_priority").intValue = ev.Priority;
        so.FindProperty("_oncePolicy").enumValueIndex = (int)ev.OncePolicy;
        so.FindProperty("_presentationKey").stringValue = ev.PresentationKey;

        var conditionAssets = SyncChildAssets<StoryConditionDefinition>(
            assetPath, "condition", ev.Conditions.Count,
            (child, index) =>
            {
                var cso = new SerializedObject(child);
                var cond = ev.Conditions[index];
                cso.FindProperty("_id").stringValue = $"{ev.EventId}__cond__{index:D2}";
                cso.FindProperty("_kind").enumValueIndex = (int)NarrativePresentationKeyNormalizer.ParseConditionKindAlias(cond.KindToken);
                cso.FindProperty("_operandA").stringValue = cond.OperandA ?? "";
                cso.FindProperty("_operandB").stringValue = "";
                cso.ApplyModifiedPropertiesWithoutUndo();
            });
        SetObjectReferenceArray(so.FindProperty("_conditions"), conditionAssets);

        var effectAssets = SyncChildAssets<StoryEffectDefinition>(
            assetPath, "effect", ev.Effects.Count,
            (child, index) =>
            {
                var eso = new SerializedObject(child);
                var eff = ev.Effects[index];
                eso.FindProperty("_id").stringValue = $"{ev.EventId}__effect__{index:D2}";
                eso.FindProperty("_kind").enumValueIndex = (int)NarrativePresentationKeyNormalizer.ParseEffectKindAlias(eff.KindToken);
                eso.FindProperty("_payload").stringValue = eff.Payload ?? "";
                eso.ApplyModifiedPropertiesWithoutUndo();
            });
        SetObjectReferenceArray(so.FindProperty("_effects"), effectAssets);

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ApplyDialogueSequence(DialogueSequenceDefinition asset, string assetPath, NarrativeDialogueSequenceDto seq)
    {
        var so = new SerializedObject(asset);
        so.FindProperty("_id").stringValue = seq.SequenceId;

        var lineAssets = SyncChildAssets<DialogueLineDefinition>(
            assetPath, "line", seq.Lines.Count,
            (child, index) =>
            {
                var lso = new SerializedObject(child);
                var line = seq.Lines[index];
                lso.FindProperty("_id").stringValue = $"{seq.SequenceId}__line__{index:D3}";
                lso.FindProperty("_speakerId").stringValue = line.SpeakerId;
                lso.FindProperty("_textKey").stringValue = NarrativeLocalizationKeys.DialogueLine(seq.SequenceId, line.LineIndex);
                lso.FindProperty("_emote").stringValue = line.EmoteId ?? "Default";
                lso.FindProperty("_emotionTextKey").stringValue = line.EmotionId != "none"
                    ? NarrativeLocalizationKeys.Emotion(line.EmotionId)
                    : "";
                lso.FindProperty("_autoAdvanceHint").floatValue = 0f;
                lso.ApplyModifiedPropertiesWithoutUndo();
            });
        SetObjectReferenceArray(so.FindProperty("_lines"), lineAssets);

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // --- Asset helpers ---

    private static T CreateOrLoadAsset<T>(string path) where T : ScriptableObject
    {
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null) return existing;

        if (AssetDatabase.LoadMainAssetAtPath(path) != null || File.Exists(path))
        {
            AssetDatabase.DeleteAsset(path);
        }

        var instance = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(instance, path);
        return instance;
    }

    private static T[] SyncChildAssets<T>(string assetPath, string prefix, int count, Action<T, int> apply) where T : ScriptableObject
    {
        var existing = AssetDatabase.LoadAllAssetsAtPath(assetPath)
            .OfType<T>()
            .OrderBy(a => a.name, StringComparer.Ordinal)
            .ToList();

        while (existing.Count > count)
        {
            var last = existing.Count - 1;
            UnityEngine.Object.DestroyImmediate(existing[last], true);
            existing.RemoveAt(last);
        }

        var result = new T[count];
        for (int i = 0; i < count; i++)
        {
            T child;
            if (i < existing.Count)
            {
                child = existing[i];
            }
            else
            {
                child = ScriptableObject.CreateInstance<T>();
                AssetDatabase.AddObjectToAsset(child, assetPath);
                existing.Add(child);
            }
            child.name = $"{prefix}_{i:D2}";
            apply(child, i);
            EditorUtility.SetDirty(child);
            result[i] = child;
        }

        return result;
    }

    private static void SetObjectReferenceArray<T>(SerializedProperty property, IReadOnlyList<T> values) where T : UnityEngine.Object
    {
        property.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }

    private static int PruneOrphans<T>(string dir, HashSet<string> validIds) where T : ScriptableObject
    {
        int deleted = 0;
        var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { dir });
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) continue;

            var idField = asset.GetType().GetProperty("Id");
            var id = idField?.GetValue(asset) as string;
            if (!string.IsNullOrWhiteSpace(id) && !validIds.Contains(id))
            {
                AssetDatabase.DeleteAsset(path);
                deleted++;
            }
        }
        return deleted;
    }

    // --- Localization helpers ---

    private static StringTableCollection EnsureStoryTableCollection()
    {
        var collection = LocalizationEditorSettings.GetStringTableCollection(StoryTableName);
        if (collection != null) return collection;

        var locales = LocalizationEditorSettings.GetLocales()
            .Where(l => l != null && (l.Identifier.Code == "ko" || l.Identifier.Code == "en"))
            .ToList();
        collection = LocalizationEditorSettings.CreateStringTableCollection(
            StoryTableName,
            LocalizationFoundationBootstrap.StringTableRoot,
            locales);
        EditorUtility.SetDirty(collection);
        EditorUtility.SetDirty(collection.SharedData);
        return collection;
    }

    private static StringTable GetOrCreateStringTable(StringTableCollection collection, string localeCode)
    {
        return collection.GetTable(new UnityEngine.Localization.LocaleIdentifier(localeCode)) as StringTable
               ?? collection.AddNewTable(new UnityEngine.Localization.LocaleIdentifier(localeCode)) as StringTable
               ?? throw new InvalidOperationException($"Failed to get table '{localeCode}' for '{collection.name}'.");
    }

    private static bool UpsertEntry(StringTable table, string key, string value, bool preserveExisting = false)
    {
        var entry = table.GetEntry(key);
        bool isNew = entry == null;
        if (isNew)
        {
            table.AddEntry(key, value ?? "");
        }
        else if (!preserveExisting || !string.IsNullOrWhiteSpace(value))
        {
            entry!.Value = value ?? "";
        }
        EditorUtility.SetDirty(table);
        EditorUtility.SetDirty(table.SharedData);
        return isNew;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parts = path.Split('/');
        var current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            var next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }
            current = next;
        }
    }

    // --- JSON parsing ---

    private static IReadOnlyList<NarrativeStoryEventDto> ParseStoryEvents(JArray? arr)
    {
        if (arr == null) return Array.Empty<NarrativeStoryEventDto>();
        var list = new List<NarrativeStoryEventDto>();
        foreach (var item in arr)
        {
            list.Add(new NarrativeStoryEventDto(
                item["eventId"]?.Value<string>() ?? "",
                item["chapterId"]?.Value<string>() ?? "",
                item["siteId"]?.Value<string>() ?? "",
                Enum.TryParse<NarrativeMoment>(item["moment"]?.Value<string>(), out var moment) ? moment : NarrativeMoment.BootLoaded,
                item["priority"]?.Value<int>() ?? 0,
                Enum.TryParse<StoryOncePolicy>(item["oncePolicy"]?.Value<string>(), out var once) ? once : StoryOncePolicy.OncePerProfile,
                item["presentationKey"]?.Value<string>() ?? "",
                Enum.TryParse<StoryPresentationKind>(item["presentationKind"]?.Value<string>(), out var pk) ? pk : StoryPresentationKind.ToastBanner,
                ParseConditions(item["conditions"] as JArray),
                ParseEffects(item["effects"] as JArray),
                item["sourceOrder"]?.Value<int>() ?? 0));
        }
        return list;
    }

    private static IReadOnlyList<NarrativeConditionDto> ParseConditions(JArray? arr)
    {
        if (arr == null) return Array.Empty<NarrativeConditionDto>();
        return arr.Select(c => new NarrativeConditionDto(
            c["kindToken"]?.Value<string>() ?? "",
            c["operandA"]?.Value<string>() ?? "")).ToList();
    }

    private static IReadOnlyList<NarrativeEffectDto> ParseEffects(JArray? arr)
    {
        if (arr == null) return Array.Empty<NarrativeEffectDto>();
        return arr.Select(e => new NarrativeEffectDto(
            e["kindToken"]?.Value<string>() ?? "",
            e["payload"]?.Value<string>() ?? "")).ToList();
    }

    private static IReadOnlyList<NarrativeDialogueSequenceDto> ParseDialogueSequences(JArray? arr)
    {
        if (arr == null) return Array.Empty<NarrativeDialogueSequenceDto>();
        var list = new List<NarrativeDialogueSequenceDto>();
        foreach (var item in arr)
        {
            list.Add(new NarrativeDialogueSequenceDto(
                item["sequenceId"]?.Value<string>() ?? "",
                item["presentationKey"]?.Value<string>() ?? "",
                Enum.TryParse<StoryPresentationKind>(item["presentationKind"]?.Value<string>(), out var pk) ? pk : StoryPresentationKind.ToastBanner,
                Enum.TryParse<NarrativeRuntimeContextKind>(item["runtimeContext"]?.Value<string>(), out var rc) ? rc : NarrativeRuntimeContextKind.None,
                ParseLines(item["lines"] as JArray)));
        }
        return list;
    }

    private static IReadOnlyList<NarrativeDialogueLineDto> ParseLines(JArray? arr)
    {
        if (arr == null) return Array.Empty<NarrativeDialogueLineDto>();
        return arr.Select(l => new NarrativeDialogueLineDto(
            l["lineIndex"]?.Value<int>() ?? 0,
            l["speakerAlias"]?.Value<string>() ?? "",
            l["speakerId"]?.Value<string>() ?? "",
            l["emotionId"]?.Value<string>() ?? "none",
            l["emoteId"]?.Value<string>() ?? "Default",
            l["text"]?.Value<string>() ?? "")).ToList();
    }

    private static IReadOnlyList<NarrativePresentationDto> ParsePresentations(JArray? arr)
    {
        if (arr == null) return Array.Empty<NarrativePresentationDto>();
        return arr.Select(p => new NarrativePresentationDto(
            p["presentationKey"]?.Value<string>() ?? "",
            Enum.TryParse<StoryPresentationKind>(p["kind"]?.Value<string>(), out var pk) ? pk : StoryPresentationKind.ToastBanner,
            Enum.TryParse<NarrativeRuntimeContextKind>(p["runtimeContext"]?.Value<string>(), out var rc) ? rc : NarrativeRuntimeContextKind.None,
            p["title"]?.Value<string>() ?? "",
            p["body"]?.Value<string>(),
            p["iconId"]?.Value<string>())).ToList();
    }

    private static IReadOnlyList<NarrativeArchiveEntryDto> ParseArchiveEntries(JArray? arr)
    {
        if (arr == null) return Array.Empty<NarrativeArchiveEntryDto>();
        return arr.Select(a => new NarrativeArchiveEntryDto(
            a["eventId"]?.Value<string>() ?? "",
            a["chapterId"]?.Value<string>() ?? "",
            a["siteId"]?.Value<string>() ?? "",
            a["presentationKey"]?.Value<string>() ?? "",
            Enum.TryParse<StoryPresentationKind>(a["kind"]?.Value<string>(), out var pk) ? pk : StoryPresentationKind.ToastBanner,
            Enum.TryParse<NarrativeRuntimeContextKind>(a["runtimeContext"]?.Value<string>(), out var rc) ? rc : NarrativeRuntimeContextKind.None,
            a["displayTitle"]?.Value<string>() ?? "",
            a["sourceOrder"]?.Value<int>() ?? 0)).ToList();
    }

    private static IReadOnlyList<NarrativeDiagnostic> ParseDiagnostics(JArray? arr)
    {
        if (arr == null) return Array.Empty<NarrativeDiagnostic>();
        return arr.Select(d => new NarrativeDiagnostic(
            d["code"]?.Value<string>() ?? "",
            Enum.TryParse<NarrativeDiagnosticSeverity>(d["severity"]?.Value<string>(), out var sev) ? sev : NarrativeDiagnosticSeverity.Info,
            d["message"]?.Value<string>() ?? "",
            d["sourcePath"]?.Value<string>() ?? "",
            d["lineNumber"]?.Value<int>() ?? 0)).ToList();
    }
}
