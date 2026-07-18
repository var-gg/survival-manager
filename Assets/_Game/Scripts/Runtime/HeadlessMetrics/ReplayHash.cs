using System.Collections.Generic;
using System.IO;
using System.Linq;
using SM.Combat.Model;
using SM.Combat.Services;

namespace SM.HeadlessMetrics;

/// <summary>
/// H100ReplayHashV1. 기존 CanonicalStateHashV1과 활동 telemetry hash를 길이-prefix로 합성한다.
/// 같은 seed·입력의 final state와 관측값이 같으면 byte-identical digest가 된다.
/// </summary>
public static class ReplayHash
{
    public const string SchemaVersion = "H100ReplayHashV1";

    public static string Compute(BattleState finalState, BattleActivityTelemetrySnapshot? activity)
    {
        return Compute(
            BattleStateCanonicalHash.Compute(finalState),
            activity?.ReplayHash ?? string.Empty);
    }

    public static string Compute(string canonicalStateHash, string activityReplayHash)
    {
        using var payload = new MemoryStream();
        LengthPrefixedStableHash.AppendPart(payload, SchemaVersion);
        LengthPrefixedStableHash.AppendPart(payload, canonicalStateHash ?? string.Empty);
        LengthPrefixedStableHash.AppendPart(payload, activityReplayHash ?? string.Empty);
        return LengthPrefixedStableHash.Compute(payload.ToArray());
    }

    public static string ComputeManifest(IEnumerable<string> replayHashes)
    {
        using var payload = new MemoryStream();
        LengthPrefixedStableHash.AppendPart(payload, "H100ReplayManifestV1");
        foreach (var replayHash in replayHashes.OrderBy(value => value, System.StringComparer.Ordinal))
        {
            LengthPrefixedStableHash.AppendPart(payload, replayHash ?? string.Empty);
        }

        return LengthPrefixedStableHash.Compute(payload.ToArray());
    }
}
