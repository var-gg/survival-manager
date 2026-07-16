using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
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
        var payload = new StringBuilder();
        AppendPart(payload, SchemaVersion);
        AppendPart(payload, canonicalStateHash);
        AppendPart(payload, activityReplayHash);
        return StableHash(payload.ToString());
    }

    public static string ComputeManifest(IEnumerable<string> replayHashes)
    {
        var payload = new StringBuilder();
        AppendPart(payload, "H100ReplayManifestV1");
        foreach (var replayHash in replayHashes.OrderBy(value => value, System.StringComparer.Ordinal))
        {
            AppendPart(payload, replayHash ?? string.Empty);
        }

        return StableHash(payload.ToString());
    }

    private static void AppendPart(StringBuilder builder, string value)
    {
        var normalized = value ?? string.Empty;
        builder.Append(Encoding.UTF8.GetByteCount(normalized).ToString(CultureInfo.InvariantCulture))
            .Append(':')
            .Append(normalized)
            .Append('|');
    }

    private static string StableHash(string input)
    {
        unchecked
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            var hash = offset;
            foreach (var value in Encoding.UTF8.GetBytes(input))
            {
                hash ^= value;
                hash *= prime;
            }

            return hash.ToString("x16", CultureInfo.InvariantCulture);
        }
    }
}
