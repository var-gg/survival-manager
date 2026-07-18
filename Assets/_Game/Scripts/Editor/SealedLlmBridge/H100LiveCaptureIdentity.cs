using System;
using System.Globalization;
using System.IO;
using SM.HeadlessMetrics;

namespace SM.SealedLlmBridge;

/// <summary>Cohort-stable live capture identity. Per-run RunId and SeedBase are deliberately absent.</summary>
public sealed record H100LiveCaptureIdentity(
    string BuildManifestHash,
    string ScorerConfigHash)
{
    public const string LiveScorerConfigToken = "live-cold-start:bt5bt10-v1";

    public static H100LiveCaptureIdentity Create(
        int campaignSiteSafety,
        int maxBattleSteps,
        float targetBattleSeconds,
        string policyId)
    {
        if (campaignSiteSafety <= 0) throw new ArgumentOutOfRangeException(nameof(campaignSiteSafety));
        if (maxBattleSteps <= 0) throw new ArgumentOutOfRangeException(nameof(maxBattleSteps));
        if (float.IsNaN(targetBattleSeconds)
            || float.IsInfinity(targetBattleSeconds)
            || targetBattleSeconds <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(targetBattleSeconds));
        }

        if (string.IsNullOrWhiteSpace(policyId))
        {
            throw new ArgumentException("Policy id is required.", nameof(policyId));
        }

        return new H100LiveCaptureIdentity(
            HashParts(
                "H100SealedBuildManifestV1",
                campaignSiteSafety.ToString(CultureInfo.InvariantCulture),
                maxBattleSteps.ToString(CultureInfo.InvariantCulture),
                targetBattleSeconds.ToString("R", CultureInfo.InvariantCulture),
                policyId),
            HashParts(
                "H100SealedScorerConfigV1",
                LiveScorerConfigToken));
    }

    private static string HashParts(params string[] values)
    {
        using var payload = new MemoryStream();
        foreach (var value in values)
        {
            LengthPrefixedStableHash.AppendPart(payload, value ?? string.Empty);
        }

        return LengthPrefixedStableHash.Compute(payload.ToArray());
    }
}
