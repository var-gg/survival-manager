using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Meta.Model;

namespace SM.Meta.Services;

public sealed record ArenaMatchSimulationResult(
    ArenaMatchRecord Match,
    BattleReplayBundle Replay,
    BattleResult Result);

public sealed class ArenaSimulationService
{
    public const int OpponentCandidateCount = 3;
    public const int DefaultRatingBand = 150;

    public bool TryCreateDefenseSnapshot(
        BattleLoadoutSnapshot compiledSnapshot,
        string blueprintId,
        string contentVersion,
        int rating,
        out ArenaDefenseSnapshot snapshot,
        out string error)
    {
        snapshot = null!;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(blueprintId))
        {
            error = "Arena defense snapshot requires a blueprint id.";
            return false;
        }

        if (ContainsTemporaryAugmentProvenance(compiledSnapshot))
        {
            error = "Arena defense snapshots must exclude temporary augments.";
            return false;
        }

        var snapshotHash = ComputeSnapshotHash(compiledSnapshot.CompileVersion, compiledSnapshot.CompileHash, blueprintId, contentVersion);
        snapshot = new ArenaDefenseSnapshot(
            $"arena_defense:{blueprintId}:{snapshotHash[..12]}",
            blueprintId,
            snapshotHash,
            compiledSnapshot.CompileVersion,
            compiledSnapshot.CompileHash,
            contentVersion,
            rating,
            DateTime.UtcNow.ToString("O"));
        return true;
    }

    public bool IsFresh(ArenaDefenseSnapshot snapshot, string expectedCompileVersion, string expectedContentVersion, out string error)
    {
        if (!string.Equals(snapshot.CompileVersion, expectedCompileVersion, StringComparison.Ordinal))
        {
            error = $"Arena snapshot '{snapshot.SnapshotId}' is stale for compile version '{expectedCompileVersion}'.";
            return false;
        }

        if (!string.Equals(snapshot.ContentVersion, expectedContentVersion, StringComparison.Ordinal))
        {
            error = $"Arena snapshot '{snapshot.SnapshotId}' is stale for content version '{expectedContentVersion}'.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public IReadOnlyList<ArenaDefenseSnapshot> BuildOpponentCandidates(
        IEnumerable<ArenaDefenseSnapshot> snapshots,
        int currentRating,
        string excludeSnapshotId = "",
        int ratingBand = DefaultRatingBand)
    {
        return snapshots
            .Where(snapshot => !string.IsNullOrWhiteSpace(snapshot.SnapshotId))
            .Where(snapshot => !string.Equals(snapshot.SnapshotId, excludeSnapshotId, StringComparison.Ordinal))
            .Where(snapshot => Math.Abs(snapshot.Rating - currentRating) <= Math.Max(1, ratingBand))
            .OrderBy(snapshot => Math.Abs(snapshot.Rating - currentRating))
            .ThenByDescending(snapshot => snapshot.CreatedAtUtc, StringComparer.Ordinal)
            .ThenBy(snapshot => snapshot.SnapshotId, StringComparer.Ordinal)
            .Take(OpponentCandidateCount)
            .ToList();
    }

    public ArenaSeasonState CreateSeasonState(
        string seasonId,
        string startedAtUtc,
        string endsAtUtc,
        int initialRating = 1000)
    {
        return new ArenaSeasonState(seasonId, startedAtUtc, endsAtUtc, initialRating, 0, true);
    }

    public ArenaMatchSimulationResult SimulateMatch(
        BattleLoadoutSnapshot offenseSnapshot,
        ArenaDefenseSnapshot defenseInfo,
        BattleLoadoutSnapshot defenseSnapshot,
        string seasonId,
        int seed)
    {
        var startedAtUtc = DateTime.UtcNow.ToString("O");
        var state = BattleFactory.Create(
            offenseSnapshot.Allies,
            defenseSnapshot.Allies,
            offenseSnapshot.TeamTactic.Posture,
            defenseSnapshot.TeamTactic.Posture,
            BattleSimulator.DefaultFixedStepSeconds,
            seed);
        var result = BattleResolver.Run(state);
        var completedAtUtc = DateTime.UtcNow.ToString("O");
        var replay = ReplayAssembler.Assemble(offenseSnapshot, defenseSnapshot.Allies, result, seed, startedAtUtc, completedAtUtc);
        var ratingDelta = result.Winner == TeamSide.Ally ? 15 : -15;
        var match = new ArenaMatchRecord(
            replay.Header.MatchId,
            seasonId,
            offenseSnapshot.SnapshotId,
            defenseInfo.SnapshotId,
            seed,
            replay.Header.MatchId,
            ratingDelta,
            result.Winner.ToString(),
            completedAtUtc);
        return new ArenaMatchSimulationResult(match, replay, result);
    }

    private static bool ContainsTemporaryAugmentProvenance(BattleLoadoutSnapshot snapshot)
    {
        return snapshot.Provenance?.Any(entry =>
            string.Equals(entry.ArtifactKind, "augment_temporary", StringComparison.Ordinal)
            || string.Equals(entry.ArtifactKind, "augment_temporary_rule", StringComparison.Ordinal)) == true;
    }

    private static string ComputeSnapshotHash(string compileVersion, string compileHash, string blueprintId, string contentVersion)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes($"{compileVersion}|{compileHash}|{blueprintId}|{contentVersion}"));
        var builder = new StringBuilder(bytes.Length * 2);
        foreach (var value in bytes)
        {
            builder.Append(value.ToString("x2"));
        }

        return builder.ToString();
    }
}
