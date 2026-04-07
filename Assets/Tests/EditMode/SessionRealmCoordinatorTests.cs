using NUnit.Framework;
using SM.Meta.Model;
using SM.Persistence.Abstractions;
using SM.Persistence.Abstractions.Models;
using SM.Tests.EditMode.Fakes;
using SM.Unity;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class SessionRealmCoordinatorTests
{
    [Test]
    public void SessionCapabilities_MapOfflineAndOnlineRealms()
    {
        var offline = SessionCapabilities.ForRealm(SessionRealm.OfflineLocal);
        var online = SessionCapabilities.ForRealm(SessionRealm.OnlineAuthoritative);

        Assert.That(offline.CanUsePvp, Is.False);
        Assert.That(offline.CanClaimOfficialRewards, Is.False);
        Assert.That(offline.CanUploadAuthoritativeProgress, Is.False);
        Assert.That(online.CanUsePvp, Is.True);
        Assert.That(online.CanClaimOfficialRewards, Is.True);
        Assert.That(online.CanUploadAuthoritativeProgress, Is.True);
    }

    [Test]
    public void SessionRealmCoordinator_RejectsOnlineRealmUntilAdapterExists()
    {
        var coordinator = CreateCoordinator();

        var started = coordinator.StartRealm(SessionRealm.OnlineAuthoritative, out var error);

        Assert.That(started, Is.False);
        Assert.That(coordinator.HasActiveSession, Is.False);
        Assert.That(coordinator.CurrentCapabilities, Is.EqualTo(SessionCapabilities.None));
        Assert.That(error, Does.Contain("hidden future seam").Or.Contain("포함되지 않습니다"));
    }

    [Test]
    public void OfflineLocalSessionAdapter_OfficialCommandsStayUnsupportedOrPreviewOnly()
    {
        var adapter = CreateOfflineLocalAdapter();

        var publish = adapter.PublishDefenseSnapshot("blueprint.default");
        var start = adapter.StartArenaMatch("defense.snapshot");
        var settle = adapter.FinalizeArenaMatch("match-001", "client-evidence");
        var claim = adapter.ClaimArenaReward("reward-claim");
        var seed = adapter.CreateOfficialBattleSeed();
        var resolve = adapter.ResolveOfficialMatch("match-001");

        Assert.That(publish.Status, Is.EqualTo(AuthorityActionStatus.UnsupportedForRealm));
        Assert.That(start.Status, Is.EqualTo(AuthorityActionStatus.UnsupportedForRealm));
        Assert.That(settle.Status, Is.EqualTo(AuthorityActionStatus.UnsupportedForRealm));
        Assert.That(claim.Status, Is.EqualTo(AuthorityActionStatus.UnsupportedForRealm));
        Assert.That(seed.Status, Is.EqualTo(AuthorityActionStatus.PreviewOnly));
        Assert.That(resolve.Status, Is.EqualTo(AuthorityActionStatus.PreviewOnly));
    }

    [Test]
    public void SessionRealmAutoStartPolicy_UsesOfflineLocalForQuickBattleAndDirectScenePlay()
    {
        Assert.That(SessionRealmAutoStartPolicy.ShouldForceOfflineLocalForQuickBattle(true), Is.True);
        Assert.That(SessionRealmAutoStartPolicy.ShouldForceOfflineLocalForQuickBattle(false), Is.False);
        Assert.That(SessionRealmAutoStartPolicy.ShouldForceOfflineLocalForScene(SceneNames.Boot), Is.False);
        Assert.That(SessionRealmAutoStartPolicy.ShouldForceOfflineLocalForScene(SceneNames.Town), Is.True);
        Assert.That(SessionRealmAutoStartPolicy.ShouldForceOfflineLocalForScene(SceneNames.Battle), Is.True);
    }

    [Test]
    public void OfflineLocalSessionAdapter_UsesSmokeProfileNamespace_WhenDedicatedLaneEnabled()
    {
        var repo = new TrackingSaveRepository();
        var adapter = CreateOfflineLocalAdapter(repo);
        adapter.UseDedicatedSmokeNamespace();

        var result = adapter.LoadOrCreateProfile(SessionCheckpointKind.StartupLoad);

        Assert.That(result.IsSuccessful, Is.True);
        Assert.That(adapter.ActiveProfileId, Is.EqualTo("test-profile.smoke"));
        Assert.That(repo.LastLoadProfileId, Is.EqualTo("test-profile.smoke"));
    }

    [Test]
    public void OfflineLocalSessionAdapter_BlocksSave_DuringTransientTownSmoke()
    {
        var repo = new TrackingSaveRepository();
        var adapter = CreateOfflineLocalAdapter(repo);
        adapter.LoadOrCreateProfile(SessionCheckpointKind.StartupLoad);
        adapter.BeginTransientTownSmoke();

        var result = adapter.SaveProfile(SessionCheckpointKind.ManualSave);

        Assert.That(result.Status, Is.EqualTo(SessionCheckpointStatus.Blocked));
        Assert.That(repo.SaveCalls, Is.EqualTo(0));
    }

    [Test]
    public void SessionRealmCoordinator_StartRealm_FailsOnCorruptSaveResult()
    {
        var sessionState = new GameSessionState(new FakeCombatContentLookup());
        var repo = new TrackingSaveRepository
        {
            LoadResult = new SaveRepositoryLoadResult
            {
                Status = SaveRepositoryLoadStatus.FailedCorrupt,
                Message = "save recovery failed",
                QuarantinePath = "Temp/quarantine",
            }
        };
        var coordinator = new SessionRealmCoordinator(
            sessionState,
            new PersistenceEntryPoint(
                new PersistenceConfig
                {
                    ProfileId = "test-profile",
                    LocalSaveDirectory = "Temp/SessionRealmCoordinatorTests",
                },
                repo));

        var started = coordinator.StartRealm(SessionRealm.OfflineLocal, out var error);

        Assert.That(started, Is.False);
        Assert.That(error, Does.Contain("save recovery failed"));
        Assert.That(coordinator.HasActiveSession, Is.False);
        Assert.That(coordinator.LastCheckpointResult.Status, Is.EqualTo(SessionCheckpointStatus.Failed));
    }

    private static SessionRealmCoordinator CreateCoordinator()
    {
        var sessionState = new GameSessionState(new FakeCombatContentLookup());
        return new SessionRealmCoordinator(sessionState, CreatePersistenceEntryPoint());
    }

    private static OfflineLocalSessionAdapter CreateOfflineLocalAdapter(ISaveRepository? repository = null)
    {
        var sessionState = new GameSessionState(new FakeCombatContentLookup());
        return new OfflineLocalSessionAdapter(sessionState, CreatePersistenceEntryPoint(repository));
    }

    private static PersistenceEntryPoint CreatePersistenceEntryPoint(ISaveRepository? repository = null)
    {
        return new PersistenceEntryPoint(
            new PersistenceConfig
            {
                ProfileId = "test-profile",
                LocalSaveDirectory = "Temp/SessionRealmCoordinatorTests",
            },
            repository ?? new InMemorySaveRepository());
    }

    private sealed class InMemorySaveRepository : ISaveRepository
    {
        private readonly SaveProfile _profile = new() { ProfileId = "test-profile" };

        public SaveProfile LoadOrCreate(string profileId)
        {
            _profile.ProfileId = profileId;
            return _profile;
        }

        public void Save(SaveProfile profile)
        {
            _profile.ProfileId = profile.ProfileId;
        }
    }

    private sealed class TrackingSaveRepository : ISaveRepository, ISaveRepositoryDiagnostics
    {
        private readonly SaveProfile _profile = new() { ProfileId = "test-profile" };

        public string LastLoadProfileId { get; private set; } = string.Empty;
        public int SaveCalls { get; private set; }
        public SaveRepositoryLoadResult? LoadResult { get; set; }

        public SaveProfile LoadOrCreate(string profileId)
        {
            LastLoadProfileId = profileId;
            _profile.ProfileId = profileId;
            return _profile;
        }

        public void Save(SaveProfile profile)
        {
            SaveCalls += 1;
            _profile.ProfileId = profile.ProfileId;
        }

        public SaveRepositoryLoadResult LoadOrCreateDetailed(string profileId, SaveRepositoryRequest? request = null)
        {
            LastLoadProfileId = profileId;
            if (LoadResult != null)
            {
                return LoadResult;
            }

            _profile.ProfileId = profileId;
            return new SaveRepositoryLoadResult
            {
                Status = SaveRepositoryLoadStatus.LoadedPrimary,
                Profile = _profile,
                Message = "loaded",
                ManifestVerified = true,
            };
        }

        public SaveRepositorySaveResult SaveDetailed(SaveProfile profile, SaveRepositoryRequest? request = null)
        {
            SaveCalls += 1;
            _profile.ProfileId = profile.ProfileId;
            return new SaveRepositorySaveResult
            {
                Status = SaveRepositorySaveStatus.Success,
                Message = "saved",
            };
        }
    }
}
