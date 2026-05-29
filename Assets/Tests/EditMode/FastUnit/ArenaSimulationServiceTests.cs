using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Core.Contracts;
using SM.Core.Stats;
using SM.Meta.Model;
using SM.Meta.Services;

namespace SM.Tests.EditMode;

/// <summary>
/// ③a — offline arena 엔진(ArenaSimulationService) de-orphan + 검증. 완성돼 있으나 호출처·테스트가 0이라
/// offline-playable arena 레인의 선행 단계로 엔진 거동만 격리 검증한다(콘텐츠 파이프라인 없이 in-memory snapshot).
/// 공식 랭크 PvP는 OnlineAuthoritative 권위 대상이고, 이 엔진은 그 시뮬레이션 + offline preview 경로의 공용 코어.
/// </summary>
[Category("FastUnit")]
public sealed class ArenaSimulationServiceTests
{
    private static BattleLoadoutSnapshot BuildSnapshot(
        string snapshotId,
        BattleUnitLoadout[] allies,
        string compileVersion = "compile:test",
        string compileHash = "hash:test",
        System.Collections.Generic.IReadOnlyList<CompileProvenanceEntry>? provenance = null)
    {
        return new BattleLoadoutSnapshot(
            snapshotId,
            compileVersion,
            compileHash,
            new TeamTacticProfile("posture:standard", "Standard", TeamPostureType.StandardAdvance),
            allies,
            allies.Select(a => a.Id).ToArray(),
            new[] { "human" },
            Provenance: provenance);
    }

    private static ArenaDefenseSnapshot Defense(string id, int rating, string createdAtUtc = "2026-01-01T00:00:00Z")
        => new(id, "blueprint_x", "snaphash", "compile:test", "compilehash", "content:v1", rating, createdAtUtc);

    [Test]
    public void TryCreateDefenseSnapshot_Succeeds_WithCleanLoadout()
    {
        var service = new ArenaSimulationService();
        var snapshot = BuildSnapshot("snap:off", new[] { CombatTestFactory.CreateUnit("u1") });

        var ok = service.TryCreateDefenseSnapshot(snapshot, "blueprint_alpha", "content:v1", 1000, out var defense, out var error);

        Assert.That(ok, Is.True, error);
        Assert.That(defense.BlueprintId, Is.EqualTo("blueprint_alpha"));
        Assert.That(defense.Rating, Is.EqualTo(1000));
        Assert.That(defense.ContentVersion, Is.EqualTo("content:v1"));
        Assert.That(defense.CompileVersion, Is.EqualTo(snapshot.CompileVersion));
        Assert.That(defense.SnapshotId, Does.StartWith("arena_defense:blueprint_alpha:"));
    }

    [Test]
    public void TryCreateDefenseSnapshot_Rejects_TemporaryAugmentProvenance()
    {
        var service = new ArenaSimulationService();
        var provenance = new[]
        {
            new CompileProvenanceEntry("u1", ModifierSource.Augment, "aug_temp", "augment_temporary", new[] { "temp" }),
        };
        var snapshot = BuildSnapshot("snap:off", new[] { CombatTestFactory.CreateUnit("u1") }, provenance: provenance);

        var ok = service.TryCreateDefenseSnapshot(snapshot, "blueprint_alpha", "content:v1", 1000, out _, out var error);

        Assert.That(ok, Is.False, "Temporary-augment loadouts must not become arena defense snapshots");
        Assert.That(error, Does.Contain("temporary"));
    }

    [Test]
    public void TryCreateDefenseSnapshot_Rejects_BlankBlueprintId()
    {
        var service = new ArenaSimulationService();
        var snapshot = BuildSnapshot("snap:off", new[] { CombatTestFactory.CreateUnit("u1") });

        var ok = service.TryCreateDefenseSnapshot(snapshot, "   ", "content:v1", 1000, out _, out var error);

        Assert.That(ok, Is.False);
        Assert.That(error, Does.Contain("blueprint"));
    }

    [Test]
    public void IsFresh_Flags_StaleCompileAndContentVersions()
    {
        var service = new ArenaSimulationService();
        var defense = Defense("snap:1", 1000); // compile:test / content:v1

        Assert.That(service.IsFresh(defense, "compile:test", "content:v1", out _), Is.True);

        Assert.That(service.IsFresh(defense, "compile:OTHER", "content:v1", out var compileError), Is.False);
        Assert.That(compileError, Does.Contain("stale"));

        Assert.That(service.IsFresh(defense, "compile:test", "content:OTHER", out var contentError), Is.False);
        Assert.That(contentError, Does.Contain("stale"));
    }

    [Test]
    public void BuildOpponentCandidates_FiltersByRatingBand_OrdersByProximity_CapsCount()
    {
        var service = new ArenaSimulationService();
        var pool = new[]
        {
            Defense("snap:self", 1000), // excluded by id
            Defense("snap:near", 1010), // Δ10
            Defense("snap:mid", 1100),  // Δ100
            Defense("snap:edge", 1150), // Δ150 (band edge, in-band)
            Defense("snap:far", 1300),  // Δ300 (out of band)
            Defense("snap:low", 880),   // Δ120 (in-band)
        };

        var candidates = service.BuildOpponentCandidates(pool, currentRating: 1000, excludeSnapshotId: "snap:self", ratingBand: 150);

        Assert.That(candidates.Count, Is.EqualTo(ArenaSimulationService.OpponentCandidateCount));
        Assert.That(candidates.Select(c => c.SnapshotId), Does.Not.Contain("snap:self"), "Self snapshot must be excluded");
        Assert.That(candidates.Select(c => c.SnapshotId), Does.Not.Contain("snap:far"), "Out-of-band snapshot must be excluded");
        Assert.That(candidates[0].SnapshotId, Is.EqualTo("snap:near"), "Closest rating should rank first");
    }

    [Test]
    public void CreateSeasonState_Initializes_ActiveSeason()
    {
        var service = new ArenaSimulationService();

        var season = service.CreateSeasonState("season:2026q1", "2026-01-01T00:00:00Z", "2026-04-01T00:00:00Z", initialRating: 1200);

        Assert.That(season.SeasonId, Is.EqualTo("season:2026q1"));
        Assert.That(season.CurrentRating, Is.EqualTo(1200));
        Assert.That(season.IsActive, Is.True);
    }

    [Test]
    public void SimulateMatch_ProducesMatchRecord_WithReplayAndWinnerDerivedRatingDelta()
    {
        var service = new ArenaSimulationService();
        var offense = BuildSnapshot("snap:offense", new[] { CombatTestFactory.CreateUnit("off1", attack: 80f, hp: 150f) });
        var defenseLoadout = BuildSnapshot(
            "snap:defense_load",
            new[] { CombatTestFactory.CreateUnit("def1", race: "undead", classId: "duelist", anchor: DeploymentAnchorId.BackCenter, hp: 6f, attack: 1f, defense: 0f) });
        var defenseInfo = Defense("snap:defense_info", 1000);

        var result = service.SimulateMatch(offense, defenseInfo, defenseLoadout, "season:test", seed: 42);

        Assert.That(result.Match.OffenseSnapshotId, Is.EqualTo("snap:offense"));
        Assert.That(result.Match.DefenseSnapshotId, Is.EqualTo("snap:defense_info"));
        Assert.That(result.Match.SeasonId, Is.EqualTo("season:test"));
        Assert.That(result.Match.Seed, Is.EqualTo(42));
        Assert.That(result.Replay, Is.Not.Null);
        Assert.That(result.Result.Winner, Is.EqualTo(TeamSide.Ally), "Overwhelming offense should defeat the token defender");
        Assert.That(result.Match.RatingDelta, Is.EqualTo(15), "Ally win should yield +15 rating delta");
    }
}
