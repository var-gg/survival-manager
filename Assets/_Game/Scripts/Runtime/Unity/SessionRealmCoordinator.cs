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

    public bool CanStartRealm(SessionRealm realm, out string reason)
    {
        if (realm == SessionRealm.OnlineAuthoritative)
        {
            reason = "공식 온라인 세션은 후속 패스에서 개방됩니다.";
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

        if (realm == SessionRealm.OfflineLocal)
        {
            _offlineLocal.LoadOrCreateProfile();
            _currentRealm = realm;
            error = string.Empty;
            return true;
        }

        error = "OnlineAuthoritative adapter는 이번 패스 범위에 포함되지 않습니다.";
        return false;
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

    public void ReloadActiveSession()
    {
        if (!_currentRealm.HasValue)
        {
            EnsureOfflineLocalSession();
            return;
        }

        if (_currentRealm == SessionRealm.OfflineLocal)
        {
            _offlineLocal.LoadOrCreateProfile();
            return;
        }

        throw new NotSupportedException("OnlineAuthoritative reload path is not implemented in this slice.");
    }

    public void SaveActiveSession()
    {
        if (_currentRealm == SessionRealm.OfflineLocal)
        {
            _offlineLocal.SaveProfile();
        }
    }

    public void EndSession()
    {
        _currentRealm = null;
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
            throw new InvalidOperationException("No active session realm. Select a realm at Boot first.");
        }

        throw new NotSupportedException("OnlineAuthoritative adapter is not implemented in this slice.");
    }
}
