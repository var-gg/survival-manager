using SM.Core.Results;
using SM.Meta.Model;

namespace SM.Meta.Services;

public interface ISessionRealmProvider
{
    bool HasActiveSession { get; }
    SessionRealm? CurrentRealm { get; }
}

public interface ISessionCapabilityProvider
{
    SessionCapabilities CurrentCapabilities { get; }
}

public interface IProfileQueryService
{
    ProfileView GetProfileView(string playerId);
    InventoryView GetInventoryView(string playerId);
    LoadoutView GetLoadoutView(string playerId);
}

public interface IProfileCommandService
{
    Result EquipItem(string heroId, string itemInstanceId);
    Result UnequipItem(string heroId, string itemInstanceId);
    Result EquipPermanentAugment(string augmentId);
    Result SelectPassiveBoard(string heroId, string boardId);
}

public interface IArenaQueryService
{
    ArenaDashboardView GetArenaDashboard(string playerId);
}

public interface IArenaCommandService
{
    AuthorityActionResult PublishDefenseSnapshot(string blueprintId);
    AuthorityActionResult StartArenaMatch(string defenseSnapshotId);
    AuthorityActionResult FinalizeArenaMatch(string matchId, string clientEvidence);
    AuthorityActionResult ClaimArenaReward(string rewardClaimToken);
}

public interface IBattleAuthority
{
    AuthorityActionResult CreateOfficialBattleSeed();
    AuthorityActionResult ResolveOfficialMatch(string matchId);
    AuthorityActionResult ValidateReplayEvidence(string matchId);
}
