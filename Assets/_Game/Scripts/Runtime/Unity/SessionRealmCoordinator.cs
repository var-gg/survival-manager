using System;
using SM.Core.Results;
using SM.Meta.Model;
using SM.Meta.Services;

namespace SM.Unity;

public sealed class SessionRealmCoordinator :
    ISessionRealmProvider,
    ISessionCapabilityProvider,
    IProfileQueryService,
    IProfileCommandService,
    IArenaQueryService,
    IArenaCommandService,
    IBattleAuthority
{
    private readonly OfflineLocalSessionAdapter _offlineLocal;
    private SessionRealm? _currentRealm;

    public SessionRealmCoordinator(
        GameSessionState sessionState,
        PersistenceEntryPoint persistence)
    {
        _offlineLocal = new OfflineLocalSessionAdapter(sessionState, persistence);
    }

    public bool HasActiveSession => _currentRealm.HasValue;
    public SessionRealm? CurrentRealm => _currentRealm;
    public SessionCapabilities CurrentCapabilities => _currentRealm.HasValue
        ? SessionCapabilities.ForRealm(_currentRealm.Value)
        : SessionCapabilities.None;
    public string ActiveProfileId => _offlineLocal.ActiveProfileId;
    public SessionCheckpointResult LastCheckpointResult { get; private set; } = SessionCheckpointResult.Success(
        SessionCheckpointKind.StartupLoad,
        string.Empty,
        "No checkpoint yet.");
    public bool IsTransientTownSmokeActive => _offlineLocal.IsTransientTownSmokeActive;
    public bool IsDedicatedSmokeNamespace => _offlineLocal.IsDedicatedSmokeNamespace;

    public bool CanStartRealm(SessionRealm realm, out string reason)
    {
        if (realm == SessionRealm.OnlineAuthoritative)
        {
            reason = "공식 온라인 seam은 hidden future seam이며 현재 local loop에 포함되지 않습니다.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public bool StartRealm(SessionRealm realm, out string error)
    {
        if (!CanStartRealm(realm, out error))
        {
            return false;
        }

        if (realm != SessionRealm.OfflineLocal)
        {
            error = "OnlineAuthoritative seam은 이번 패스 범위에 포함되지 않습니다.";
            return false;
        }

        var loadResult = _offlineLocal.LoadOrCreateProfile(SessionCheckpointKind.StartupLoad);
        LastCheckpointResult = loadResult;
        if (!loadResult.IsSuccessful)
        {
            error = loadResult.Message;
            return false;
        }

        _currentRealm = realm;
        error = string.Empty;
        return true;
    }

    public void EnsureOfflineLocalSession()
    {
        if (_currentRealm == SessionRealm.OfflineLocal)
        {
            return;
        }

        if (!StartRealm(SessionRealm.OfflineLocal, out var error))
        {
            throw new InvalidOperationException(error);
        }
    }

    public SessionCheckpointResult ReloadActiveSession(SessionCheckpointKind kind = SessionCheckpointKind.ManualLoad)
    {
        if (!_currentRealm.HasValue)
        {
            EnsureOfflineLocalSession();
            return LastCheckpointResult;
        }

        if (_currentRealm == SessionRealm.OfflineLocal)
        {
            LastCheckpointResult = _offlineLocal.LoadOrCreateProfile(kind);
            return LastCheckpointResult;
        }

        throw new NotSupportedException("OnlineAuthoritative reload seam is not implemented in this slice.");
    }

    public SessionCheckpointResult SaveActiveSession(SessionCheckpointKind kind = SessionCheckpointKind.ManualSave)
    {
        if (_currentRealm == SessionRealm.OfflineLocal)
        {
            LastCheckpointResult = _offlineLocal.SaveProfile(kind);
            return LastCheckpointResult;
        }

        LastCheckpointResult = SessionCheckpointResult.Blocked(kind, ActiveProfileId, "No active OfflineLocal session.");
        return LastCheckpointResult;
    }

    public void UseDedicatedSmokeNamespace()
    {
        _offlineLocal.UseDedicatedSmokeNamespace();
    }

    public void BeginTransientTownSmoke()
    {
        _offlineLocal.BeginTransientTownSmoke();
    }

    public void ReturnToCanonicalLane()
    {
        _offlineLocal.ReturnToCanonicalLane();
    }

    public void EndSession()
    {
        _currentRealm = null;
        _offlineLocal.ReturnToCanonicalLane();
    }

    public ProfileView GetProfileView(string playerId) => RequireOfflineLocal().GetProfileView(playerId);
    public InventoryView GetInventoryView(string playerId) => RequireOfflineLocal().GetInventoryView(playerId);
    public LoadoutView GetLoadoutView(string playerId) => RequireOfflineLocal().GetLoadoutView(playerId);
    public ArenaDashboardView GetArenaDashboard(string playerId) => RequireOfflineLocal().GetArenaDashboard(playerId);
    public Result EquipItem(string heroId, string itemInstanceId) => RequireOfflineLocal().EquipItem(heroId, itemInstanceId);
    public Result UnequipItem(string heroId, string itemInstanceId) => RequireOfflineLocal().UnequipItem(heroId, itemInstanceId);
    public Result EquipPermanentAugment(string augmentId) => RequireOfflineLocal().EquipPermanentAugment(augmentId);
    public Result SelectPassiveBoard(string heroId, string boardId) => RequireOfflineLocal().SelectPassiveBoard(heroId, boardId);
    public AuthorityActionResult PublishDefenseSnapshot(string blueprintId) => RequireOfflineLocal().PublishDefenseSnapshot(blueprintId);
    public AuthorityActionResult StartArenaMatch(string defenseSnapshotId) => RequireOfflineLocal().StartArenaMatch(defenseSnapshotId);
    public AuthorityActionResult FinalizeArenaMatch(string matchId, string clientEvidence) => RequireOfflineLocal().FinalizeArenaMatch(matchId, clientEvidence);
    public AuthorityActionResult ClaimArenaReward(string rewardClaimToken) => RequireOfflineLocal().ClaimArenaReward(rewardClaimToken);
    public AuthorityActionResult CreateOfficialBattleSeed() => RequireOfflineLocal().CreateOfficialBattleSeed();
    public AuthorityActionResult ResolveOfficialMatch(string matchId) => RequireOfflineLocal().ResolveOfficialMatch(matchId);
    public AuthorityActionResult ValidateReplayEvidence(string matchId) => RequireOfflineLocal().ValidateReplayEvidence(matchId);

    private OfflineLocalSessionAdapter RequireOfflineLocal()
    {
        if (_currentRealm == SessionRealm.OfflineLocal)
        {
            return _offlineLocal;
        }

        if (!_currentRealm.HasValue)
        {
            throw new InvalidOperationException("No active session. Use the Boot start screen first.");
        }

        throw new NotSupportedException("OnlineAuthoritative seam is not implemented in this slice.");
    }
}
