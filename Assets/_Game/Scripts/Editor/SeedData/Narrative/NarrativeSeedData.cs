using System;
using System.Collections.Generic;
using System.Linq;
using SM.Content.Definitions;
using SM.Core;

namespace SM.Editor.SeedData;

internal static partial class NarrativeSeedData
{
    internal const int ExpectedStoryEventCount = 56;
    internal const int ExpectedDialogueSequenceCount = 66;
    internal const int ExpectedChapterBeatCount = 50;
    internal const int ExpectedHeroLoreCount = 20;

    internal static readonly DialogueSequenceSeed[] DialogueSequences = CreateDialogueSequences();
    internal static readonly PresentationTextSeed[] PresentationTexts = CreatePresentationTexts();
    internal static readonly StoryEventSeed[] StoryEvents = CreateStoryEvents();
    internal static readonly ChapterBeatSeed[] ChapterBeats = CreateChapterBeats();
    internal static readonly HeroLoreSeed[] HeroLore = CreateHeroLore();
    internal static readonly StoryStringSeed[] Strings = BuildStrings();
    internal static readonly string[] StoryFlags = BuildStoryFlags();
    internal static readonly string[] ChapterIds = BuildChapterIds();
    internal static readonly string[] SiteIds = BuildSiteIds();

    internal static void ValidateSeedCountsOrThrow()
    {
        if (StoryEvents.Length != ExpectedStoryEventCount)
        {
            throw new InvalidOperationException($"Expected {ExpectedStoryEventCount} story events, found {StoryEvents.Length}.");
        }

        if (DialogueSequences.Length != ExpectedDialogueSequenceCount)
        {
            throw new InvalidOperationException($"Expected {ExpectedDialogueSequenceCount} dialogue sequences, found {DialogueSequences.Length}.");
        }

        if (ChapterBeats.Length != ExpectedChapterBeatCount)
        {
            throw new InvalidOperationException($"Expected {ExpectedChapterBeatCount} chapter beats, found {ChapterBeats.Length}.");
        }

        if (HeroLore.Length != ExpectedHeroLoreCount)
        {
            throw new InvalidOperationException($"Expected {ExpectedHeroLoreCount} hero lore entries, found {HeroLore.Length}.");
        }
    }

    internal static string BuildDialogueTextKey(string sequenceId, int lineIndex)
    {
        return $"loc.story.{NormalizeNarrativeId(TrimDialoguePrefix(sequenceId))}.{lineIndex}";
    }

    internal static string BuildHeroLoreTitleKey(string heroId)
    {
        return $"loc.story.hero_lore.{NormalizeNarrativeId(heroId)}.0";
    }

    internal static string BuildHeroLoreSummaryKey(string heroId)
    {
        return $"loc.story.hero_lore.{NormalizeNarrativeId(heroId)}.1";
    }

    internal static string BuildHeroLoreBodyKey(string heroId, int bodyIndex)
    {
        return $"loc.story.hero_lore.{NormalizeNarrativeId(heroId)}.{bodyIndex + 2}";
    }

    internal static string BuildHeroLoreCanonKey(string heroId)
    {
        return $"loc.story.hero_lore.{NormalizeNarrativeId(heroId)}.canon";
    }

    internal static string BuildPresentationTitleKey(string presentationKey)
    {
        return $"loc.story.presentation.{NormalizeNarrativeId(presentationKey)}.title";
    }

    internal static string BuildPresentationBodyKey(string presentationKey)
    {
        return $"loc.story.presentation.{NormalizeNarrativeId(presentationKey)}.body";
    }

    internal static StoryPresentationKind InferPresentationKind(string presentationKey)
    {
        if (string.IsNullOrWhiteSpace(presentationKey))
        {
            throw new InvalidOperationException("Presentation key is missing.");
        }

        if (presentationKey.StartsWith("dialogue_scene_", StringComparison.Ordinal))
        {
            return StoryPresentationKind.DialogueScene;
        }

        if (presentationKey.StartsWith("dialogue_overlay_", StringComparison.Ordinal))
        {
            return StoryPresentationKind.DialogueOverlay;
        }

        if (presentationKey.StartsWith("story_card_", StringComparison.Ordinal))
        {
            return StoryPresentationKind.StoryCard;
        }

        if (presentationKey.StartsWith("toast_", StringComparison.Ordinal))
        {
            return StoryPresentationKind.ToastBanner;
        }

        throw new InvalidOperationException($"Unsupported narrative presentation key '{presentationKey}'.");
    }

    private static StoryStringSeed[] BuildStrings()
    {
        var entries = new Dictionary<string, StoryStringSeed>(StringComparer.Ordinal);

        void Upsert(StoryStringSeed seed)
        {
            entries[seed.Key] = seed;
        }

        foreach (var sequence in DialogueSequences)
        {
            for (var lineIndex = 0; lineIndex < sequence.Lines.Length; lineIndex++)
            {
                var line = sequence.Lines[lineIndex];
                Upsert(new StoryStringSeed(
                    BuildDialogueTextKey(sequence.Id, lineIndex),
                    line.Ko,
                    line.En));
            }
        }

        foreach (var presentation in PresentationTexts)
        {
            Upsert(new StoryStringSeed(
                BuildPresentationTitleKey(presentation.PresentationKey),
                presentation.KoTitle,
                presentation.EnTitle));
            Upsert(new StoryStringSeed(
                BuildPresentationBodyKey(presentation.PresentationKey),
                presentation.KoBody,
                presentation.EnBody));
        }

        foreach (var lore in HeroLore)
        {
            Upsert(new StoryStringSeed(
                BuildHeroLoreTitleKey(lore.HeroId),
                lore.TitleKo,
                lore.TitleEn));
            Upsert(new StoryStringSeed(
                BuildHeroLoreSummaryKey(lore.HeroId),
                lore.SummaryKo,
                lore.SummaryEn));

            var bodyEn = lore.BodyEn ?? Array.Empty<string>();
            for (var bodyIndex = 0; bodyIndex < lore.BodyKo.Length; bodyIndex++)
            {
                Upsert(new StoryStringSeed(
                    BuildHeroLoreBodyKey(lore.HeroId, bodyIndex),
                    lore.BodyKo[bodyIndex],
                    bodyIndex < bodyEn.Length ? bodyEn[bodyIndex] : string.Empty));
            }

            Upsert(new StoryStringSeed(
                BuildHeroLoreCanonKey(lore.HeroId),
                BuildHeroLoreCanonText(lore.BodyKo, lore.SummaryKo),
                BuildHeroLoreCanonText(bodyEn, lore.SummaryEn)));
        }

        return entries.Values
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] BuildStoryFlags()
    {
        return StoryEvents
            .SelectMany(seed => seed.Conditions
                .Where(condition =>
                    condition.Kind == StoryConditionKind.FlagSet ||
                    condition.Kind == StoryConditionKind.FlagNotSet)
                .Select(condition => condition.OperandA))
            .Concat(StoryEvents
                .SelectMany(seed => seed.Effects)
                .Select(effect => effect.Kind switch
                {
                    StoryEffectKind.SetFlag => effect.Payload,
                    StoryEffectKind.ClearFlag => effect.Payload,
                    _ => string.Empty,
                }))
            .Where(flagId => !string.IsNullOrWhiteSpace(flagId))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(flagId => flagId, StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] BuildChapterIds()
    {
        return ChapterBeats
            .Select(seed => seed.ChapterId)
            .Concat(StoryEvents.SelectMany(seed => seed.Conditions.Where(condition => condition.Kind == StoryConditionKind.ChapterIs).Select(condition => condition.OperandA)))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] BuildSiteIds()
    {
        return ChapterBeats
            .Select(seed => seed.SiteId)
            .Concat(StoryEvents.SelectMany(seed => seed.Conditions.Where(condition => condition.Kind == StoryConditionKind.SiteIs).Select(condition => condition.OperandA)))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
    }

    private static string BuildHeroLoreCanonText(IReadOnlyList<string> body, string summary)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(summary))
        {
            parts.Add(summary);
        }

        foreach (var paragraph in body)
        {
            if (!string.IsNullOrWhiteSpace(paragraph))
            {
                parts.Add(paragraph);
            }
        }

        return string.Join("\n\n", parts);
    }

    private static string TrimDialoguePrefix(string sequenceId)
    {
        return sequenceId.StartsWith("dialogue_", StringComparison.Ordinal)
            ? sequenceId.Substring("dialogue_".Length)
            : sequenceId;
    }

    private static string NormalizeNarrativeId(string raw)
    {
        return ContentLocalizationTables.NormalizeId(raw);
    }

    private static DialogueSequenceSeed Seq(string id, string block)
    {
        var lines = new List<DialogueLineSeed>();
        var normalizedBlock = block.Replace("\r\n", "\n", StringComparison.Ordinal);
        foreach (var rawLine in normalizedBlock.Split('\n'))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var cells = line.Split('|', 3, StringSplitOptions.None);
            if (cells.Length != 3)
            {
                throw new InvalidOperationException($"Invalid dialogue seed row '{line}' in '{id}'.");
            }

            lines.Add(Line(
                NormalizeSpeakerId(cells[0].Trim()),
                cells[2].Trim(),
                emotion: cells[1].Trim()));
        }

        return new DialogueSequenceSeed(id, lines.ToArray());
    }

    private static DialogueLineSeed Line(
        string speakerId,
        string ko,
        string en = "",
        string emotion = "",
        float autoAdvanceHint = 0f)
    {
        return new DialogueLineSeed(speakerId, ko, en, emotion, autoAdvanceHint);
    }

    private static StoryEventSeed Evt(
        string id,
        NarrativeMoment moment,
        int priority,
        string presentationKey,
        StoryConditionSeed[] conditions,
        StoryEffectSeed[] effects)
    {
        return new StoryEventSeed(
            id,
            moment,
            priority,
            NormalizePresentationKey(presentationKey),
            conditions,
            effects);
    }

    private static StoryConditionSeed Cond(StoryConditionKind kind, string operandA, string operandB = "")
    {
        return new StoryConditionSeed(kind, operandA, operandB);
    }

    private static StoryEffectSeed Eff(StoryEffectKind kind, string payload)
    {
        return new StoryEffectSeed(kind, payload);
    }

    private static ChapterBeatSeed Beat(
        string id,
        string chapterId,
        string siteId,
        int nodeIndex,
        string beatLabel,
        float tension,
        float relief,
        float humor,
        float catharsis)
    {
        return new ChapterBeatSeed(id, chapterId, siteId, nodeIndex, beatLabel, tension, relief, humor, catharsis);
    }

    private static HeroLoreSeed Lore(
        string id,
        string heroId,
        NarrativeTier tier,
        int beatBudget,
        string unresolvedHook,
        string titleKo,
        string summaryKo,
        string[] bodyKo)
    {
        return new HeroLoreSeed(id, heroId, tier, beatBudget, unresolvedHook, titleKo, summaryKo, bodyKo);
    }

    private static PresentationTextSeed Presentation(
        string presentationKey,
        string koTitle,
        string koBody)
    {
        return new PresentationTextSeed(NormalizePresentationKey(presentationKey), koTitle, koBody);
    }

    private static string NormalizePresentationKey(string presentationKey)
    {
        return presentationKey switch
        {
            "dialogue_overlay_boss_defeat_wolfpine_trail" => "dialogue_scene_boss_defeat_wolfpine_trail",
            "dialogue_overlay_boss_defeat_tithe_road" => "dialogue_scene_boss_defeat_tithe_road",
            _ => presentationKey,
        };
    }

    private static string NormalizeSpeakerId(string rawSpeakerId)
    {
        return rawSpeakerId switch
        {
            "Narrator" => "Narrator",
            "Dawn Priest" => "hero_dawn_priest",
            "Pack Raider" => "hero_pack_raider",
            "Grave Hexer" => "hero_grave_hexer",
            "Echo Savant" => "hero_echo_savant",
            "Aldric" => "npc_aldric",
            "npc_aldric" => "npc_aldric",
            "npc_aldric (voice fragment)" => "npc_aldric",
            "npc_ember_runner" => "npc_ember_runner",
            _ => rawSpeakerId,
        };
    }

    private static partial DialogueSequenceSeed[] CreateDialogueSequences();
    private static partial PresentationTextSeed[] CreatePresentationTexts();
    private static partial StoryEventSeed[] CreateStoryEvents();
    private static partial ChapterBeatSeed[] CreateChapterBeats();
    private static partial HeroLoreSeed[] CreateHeroLore();
}
