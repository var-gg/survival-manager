using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using SM.Combat.Model;

namespace SM.Combat.Services;

public static class ReplayAssembler
{
    public const string CurrentSimVersion = "live-sim.v1";

    public static BattleReplayBundle Assemble(
        BattleLoadoutSnapshot playerSnapshot,
        IReadOnlyList<BattleUnitLoadout> enemySnapshot,
        BattleResult result,
        int seed,
        string startedAtUtc,
        string completedAtUtc)
    {
        var input = new BattleInputSnapshot(
            playerSnapshot.CompileVersion,
            playerSnapshot.CompileHash,
            playerSnapshot.TeamTactic.Posture,
            playerSnapshot.Allies,
            enemySnapshot,
            playerSnapshot.TeamTags);

        var playerHash = ComputeLoadoutHash(playerSnapshot.Allies);
        var enemyHash = ComputeLoadoutHash(enemySnapshot);
        var finalStateHash = ComputeFinalStateHash(result);
        var header = new BattleReplayHeader(
            Guid.NewGuid().ToString("N"),
            playerSnapshot.CompileVersion,
            CurrentSimVersion,
            seed,
            playerHash,
            enemyHash,
            startedAtUtc,
            completedAtUtc,
            result.Winner,
            finalStateHash);

        var keyframes = BuildKeyframes(result);
        return new BattleReplayBundle(header, input, result.Events, keyframes);
    }

    private static IReadOnlyList<BattleKeyframeDigest> BuildKeyframes(BattleResult result)
    {
        var keyframes = new List<BattleKeyframeDigest>();
        foreach (var group in result.Events.GroupBy(@event => @event.StepIndex))
        {
            if (group.Key % 10 != 0 && group.Key != result.StepCount)
            {
                continue;
            }

            var digest = ComputeHash(string.Join("|", group.OrderBy(@event => @event.ActorId.Value).Select(@event =>
                $"{@event.StepIndex}:{@event.ActorId.Value}:{@event.ActionType}:{@event.TargetId?.Value}:{@event.Value:0.###}:{@event.Note}")));
            keyframes.Add(new BattleKeyframeDigest(group.Key, group.First().TimeSeconds, digest));
        }

        if (keyframes.Count == 0)
        {
            keyframes.Add(new BattleKeyframeDigest(result.StepCount, result.DurationSeconds, ComputeFinalStateHash(result)));
        }

        return keyframes;
    }

    private static string ComputeLoadoutHash(IEnumerable<BattleUnitLoadout> units)
    {
        var builder = string.Join("|", units
            .OrderBy(unit => unit.Id, StringComparer.Ordinal)
            .Select(unit => $"{unit.Id}:{unit.RaceId}:{unit.ClassId}:{unit.PreferredAnchor}:{string.Join(",", unit.CompileTags ?? Array.Empty<string>())}"));
        return ComputeHash(builder);
    }

    private static string ComputeFinalStateHash(BattleResult result)
    {
        var builder = new StringBuilder();
        builder.Append(result.Winner).Append('|').Append(result.StepCount).Append('|').Append(result.DurationSeconds).Append('|');
        foreach (var unit in result.FinalUnits.OrderBy(unit => unit.Id, StringComparer.Ordinal))
        {
            builder.Append(unit.Id).Append(':')
                .Append(unit.CurrentHealth.ToString("0.###")).Append(':')
                .Append(unit.IsAlive).Append(':')
                .Append(unit.Position.X.ToString("0.###")).Append(':')
                .Append(unit.Position.Y.ToString("0.###")).Append('|');
        }

        return ComputeHash(builder.ToString());
    }

    private static string ComputeHash(string input)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
        {
            sb.Append(b.ToString("x2"));
        }

        return sb.ToString();
    }
}
