using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Unity;
using UnityEngine;

namespace SM.Tests.PlayMode;

internal static class PlayModeSmokeEvidence
{
    public const string SummaryDirectory = "Logs/vertical-slice";
    public const string ScreenshotDirectory = "Screenshots/smoke";
    public const string MarkdownFileName = "smoke-run-summary.md";
    public const string JsonFileName = "smoke-run-summary.json";

    public static readonly string[] ScreenshotFileNames =
    {
        "01_boot.png",
        "02_town.png",
        "03_battle.png",
        "04_reward.png",
    };

    public static void Reset()
    {
        Directory.CreateDirectory(SummaryDirectory);
        Directory.CreateDirectory(ScreenshotDirectory);

        DeleteIfExists(Path.Combine(SummaryDirectory, MarkdownFileName));
        DeleteIfExists(Path.Combine(SummaryDirectory, JsonFileName));
        foreach (var fileName in ScreenshotFileNames)
        {
            DeleteIfExists(Path.Combine(ScreenshotDirectory, fileName));
        }
    }

    public static IEnumerator CaptureScreenshot(string fileName)
    {
        yield return new WaitForEndOfFrame();

        Directory.CreateDirectory(ScreenshotDirectory);
        var texture = CaptureFrameTexture();
        Assert.That(texture, Is.Not.Null, $"PlayMode smoke screenshot capture returned null: {fileName}");

        var path = Path.Combine(ScreenshotDirectory, fileName);
        try
        {
            var png = texture!.EncodeToPNG();
            File.WriteAllBytes(path, png);
            Assert.That(new FileInfo(path).Length, Is.GreaterThan(0), $"PlayMode smoke screenshot should be non-empty: {path}");
        }
        finally
        {
            UnityEngine.Object.Destroy(texture);
        }
    }

    public static VerticalSliceRunSummary BuildSummary(GameSessionState session)
    {
        var run = session.ActiveRun;
        var overlay = run?.Overlay;
        var payload = session.RunBattlePayload;

        var input = new VerticalSliceRunSummaryInput(
            RunId: FirstNonEmpty(payload?.RunId, run?.RunId),
            ChapterId: FirstNonEmpty(payload?.ChapterId, overlay?.ChapterId, session.SelectedCampaignChapterId),
            SiteId: FirstNonEmpty(payload?.SiteId, overlay?.SiteId, session.SelectedCampaignSiteId),
            SiteNodeIndex: payload?.SiteNodeIndex ?? overlay?.SiteNodeIndex ?? -1,
            EncounterId: FirstNonEmpty(overlay?.EncounterId, payload?.EncounterId),
            FactionId: string.Empty,
            BattleSeed: overlay?.BattleSeed ?? 0,
            IsBoss: false,
            LastSettlementWasVictory: run?.LastSettlementWasVictory ?? session.LastBattleVictory,
            BattleContextHash: FirstNonEmpty(overlay?.BattleContextHash, payload?.BattleContextHash),
            NodeOverlayHash: payload?.NodeOverlayHash ?? string.Empty,
            SelectedRouteHash: payload?.StageCandidatePathHash ?? string.Empty,
            RewardBiasPercent: payload?.RewardBiasPercent ?? 0,
            ThreatPressurePercent: payload?.ThreatPressurePercent ?? 0,
            AffinityBoostPercent: payload?.AffinityBoostPercent ?? 0,
            ResolvedModifierIds: payload?.ResolvedModifierIds ?? Array.Empty<string>(),
            RewardSourceId: overlay?.RewardSourceId ?? string.Empty,
            RewardCommitId: overlay?.RewardCommitId ?? string.Empty,
            RewardChoiceLedgerCount: session.Profile.RewardLedger?.Count ?? 0,
            HasPendingRewardSettlement: session.HasPendingRewardSettlement);

        return VerticalSliceRunSummaryBuilder.Build(input);
    }

    public static string BuildDeterministicWitnessHash(VerticalSliceRunSummary summary)
    {
        var payload = string.Join(
            "|",
            summary.Atlas.RunId,
            summary.Atlas.ChapterId,
            summary.Atlas.SiteId,
            summary.Atlas.SiteNodeIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
            summary.Atlas.SelectedRouteHash,
            summary.Atlas.NodeOverlayHash,
            summary.Battle.EncounterId,
            summary.Battle.BattleContextHash,
            summary.Battle.BattleSeed.ToString(System.Globalization.CultureInfo.InvariantCulture),
            summary.Reward.RewardCommitId,
            string.Join(",", summary.Atlas.ResolvedModifierIds.OrderBy(static id => id, StringComparer.Ordinal)));

        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var builder = new StringBuilder(bytes.Length * 2);
        foreach (var value in bytes)
        {
            builder.Append(value.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    public static void AssertRequiredTraceFields(VerticalSliceRunSummary summary)
    {
        Assert.That(summary.Atlas.RunId, Is.Not.Empty, "smoke summary should include RunId.");
        Assert.That(summary.Atlas.SiteNodeIndex, Is.GreaterThanOrEqualTo(0), "smoke summary should include SiteNodeIndex.");
        Assert.That(summary.Atlas.NodeOverlayHash, Is.Not.Empty, "smoke summary should include NodeOverlayHash.");
        Assert.That(summary.Battle.EncounterId, Is.Not.Empty, "smoke summary should include EncounterId.");
        Assert.That(summary.Battle.BattleContextHash, Is.Not.Empty, "smoke summary should include BattleContextHash.");
        Assert.That(summary.Reward.RewardCommitId, Is.Not.Empty, "smoke summary should include RewardCommitId.");
    }

    public static void WriteSummary(
        VerticalSliceRunSummary summary,
        string deterministicWitnessHash,
        string rootCause,
        IReadOnlyList<string> screenshotFileNames)
    {
        Directory.CreateDirectory(SummaryDirectory);
        var markdownPath = Path.Combine(SummaryDirectory, MarkdownFileName);
        var jsonPath = Path.Combine(SummaryDirectory, JsonFileName);

        var markdown = new StringBuilder();
        markdown.AppendLine(VerticalSliceRunSummaryBuilder.ToMarkdown(summary));
        markdown.AppendLine();
        markdown.AppendLine("## PlayMode Smoke Evidence");
        markdown.AppendLine($"- DeterministicWitnessHash: `{deterministicWitnessHash}`");
        markdown.AppendLine($"- RootCause: {rootCause}");
        markdown.AppendLine("- Screenshots:");
        foreach (var fileName in screenshotFileNames)
        {
            markdown.AppendLine($"  - `{ScreenshotDirectory}/{fileName}`");
        }

        File.WriteAllText(markdownPath, markdown.ToString(), Encoding.UTF8);
        File.WriteAllText(jsonPath, BuildJson(summary, deterministicWitnessHash, rootCause, screenshotFileNames), Encoding.UTF8);
    }

    private static string BuildJson(
        VerticalSliceRunSummary summary,
        string deterministicWitnessHash,
        string rootCause,
        IReadOnlyList<string> screenshotFileNames)
    {
        var screenshots = string.Join(
            ",\n",
            screenshotFileNames.Select(fileName => $"    \"{EscapeJson($"{ScreenshotDirectory}/{fileName}")}\""));

        var builder = new StringBuilder();
        builder.AppendLine("{");
        builder.AppendLine($"  \"runId\": \"{EscapeJson(summary.Atlas.RunId)}\",");
        builder.AppendLine($"  \"siteNodeIndex\": {summary.Atlas.SiteNodeIndex},");
        builder.AppendLine($"  \"encounterId\": \"{EscapeJson(summary.Battle.EncounterId)}\",");
        builder.AppendLine($"  \"nodeOverlayHash\": \"{EscapeJson(summary.Atlas.NodeOverlayHash)}\",");
        builder.AppendLine($"  \"battleContextHash\": \"{EscapeJson(summary.Battle.BattleContextHash)}\",");
        builder.AppendLine($"  \"rewardCommitId\": \"{EscapeJson(summary.Reward.RewardCommitId)}\",");
        builder.AppendLine($"  \"deterministicWitnessHash\": \"{EscapeJson(deterministicWitnessHash)}\",");
        builder.AppendLine($"  \"rootCause\": \"{EscapeJson(rootCause)}\",");
        builder.AppendLine("  \"screenshots\": [");
        builder.AppendLine(screenshots);
        builder.AppendLine("  ]");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static Texture2D? CaptureFrameTexture()
    {
        var screenCaptureType = Type.GetType("UnityEngine.ScreenCapture, UnityEngine.ScreenCaptureModule")
                                ?? Type.GetType("UnityEngine.ScreenCapture, UnityEngine.CoreModule");
        var method = screenCaptureType?.GetMethod(
            "CaptureScreenshotAsTexture",
            BindingFlags.Public | BindingFlags.Static,
            null,
            Type.EmptyTypes,
            null);
        return method?.Invoke(null, Array.Empty<object>()) as Texture2D;
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static string EscapeJson(string value) =>
        value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");
}
