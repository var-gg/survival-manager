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
        Assert.That(error, Does.Contain("후속 패스").Or.Contain("범위"));
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

    private static SessionRealmCoordinator CreateCoordinator()
    {
        var sessionState = new GameSessionState(new FakeCombatContentLookup());
        return new SessionRealmCoordinator(sessionState, CreatePersistenceEntryPoint());
    }

    private static OfflineLocalSessionAdapter CreateOfflineLocalAdapter()
    {
        var sessionState = new GameSessionState(new FakeCombatContentLookup());
        return new OfflineLocalSessionAdapter(sessionState, CreatePersistenceEntryPoint());
    }

    private static PersistenceEntryPoint CreatePersistenceEntryPoint()
    {
        return new PersistenceEntryPoint(
            new PersistenceConfig
            {
                ProfileId = "test-profile",
                LocalSaveDirectory = "Temp/SessionRealmCoordinatorTests",
            },
            new InMemorySaveRepository());
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
}
