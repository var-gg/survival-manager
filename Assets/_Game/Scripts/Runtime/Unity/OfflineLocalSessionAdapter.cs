using System;
using System.Linq;
using SM.Core.Results;
using SM.Meta.Model;
using SM.Meta.Services;
using Unity.Profiling;

namespace SM.Unity;

public sealed class OfflineLocalSessionAdapter :
    IProfileQueryService,
    IProfileCommandService,
    IArenaQueryService,
    IArenaCommandService,
    IBattleAuthority
{
    private static readonly ProfilerMarker LoadOrCreateProfileMarker = new("SM.OfflineLocalSessionAdapter.LoadOrCreateProfile");

    private readonly GameSessionState _sessionState;
    private readonly PersistenceEntryPoint _persistence;

    public OfflineLocalSessionAdapter(
        GameSessionState sessionState,
        PersistenceEntryPoint persistence)
    {
        _sessionState = sessionState;
        _persistence = persistence;
    }

    public string ActiveProfileId => string.IsNullOrWhiteSpace(_sessionState.Profile.ProfileId)
        ? _persistence.Config.ProfileId
        : _sessionState.Profile.ProfileId;

    public void LoadOrCreateProfile()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        using (LoadOrCreateProfileMarker.Auto())
        {
            var profile = _persistence.Repository.LoadOrCreate(_persistence.Config.ProfileId);
            _sessionState.BindProfile(profile);
        }

        stopwatch.Stop();
        RuntimeInstrumentation.LogDuration(
            nameof(OfflineLocalSessionAdapter) + ".LoadOrCreateProfile",
            stopwatch.Elapsed,
            $"profileId={_persistence.Config.ProfileId}");
    }

    public void SaveProfile()
    {
        _persistence.Repository.Save(_sessionState.Profile);
    }

    public ProfileView GetProfileView(string playerId)
    {
        EnsureProfileMatches(playerId);
        var heroes = _sessionState.Profile.Heroes
            .Select(hero => new ProfileHeroView(
                hero.HeroId,
                hero.Name,
                hero.RaceId,
                hero.ClassId,
                _sessionState.ExpeditionSquadHeroIds.Contains(hero.HeroId)))
            .ToArray();
        return new ProfileView(
            ActiveProfileId,
            _sessionState.Profile.DisplayName,
            SessionRealm.OfflineLocal,
            false,
            _sessionState.Profile.Currencies.Gold,
            _sessionState.Profile.Currencies.Echo,
            _sessionState.PermanentAugmentSlotCount,
            heroes.Length,
            _sessionState.Profile.Inventory.Count,
            heroes);
    }

    public InventoryView GetInventoryView(string playerId)
    {
        EnsureProfileMatches(playerId);
        var items = _sessionState.Profile.Inventory
            .Select(item => new InventoryItemView(
                item.ItemInstanceId,
                item.ItemBaseId,
                item.EquippedHeroId ?? string.Empty))
            .ToArray();
        return new InventoryView(items.Length, items);
    }

    public LoadoutView GetLoadoutView(string playerId)
    {
        EnsureProfileMatches(playerId);
        var deployments = _sessionState.EnumerateDeploymentAssignments()
            .Where(entry => !string.IsNullOrWhiteSpace(entry.HeroId))
            .Select(entry => new LoadoutDeploymentView(entry.Anchor, entry.HeroId!))
            .ToArray();
        return new LoadoutView(
            _sessionState.Profile.ActiveBlueprintId,
            _sessionState.SelectedTeamPosture,
            _sessionState.ExpeditionSquadHeroIds.Count,
            _sessionState.BattleDeployHeroIds.Count,
            _sessionState.ExpeditionSquadHeroIds.ToArray(),
            deployments);
    }

    public ArenaDashboardView GetArenaDashboard(string playerId)
    {
        EnsureProfileMatches(playerId);
        // Arena/authority DTOs remain as hidden future seams and are not rendered on the active UI.
        return new ArenaDashboardView(
            SessionRealm.OfflineLocal,
            false,
            false,
            "공식 PvP와 공식 defense snapshot은 OnlineAuthoritative 세션에서만 열립니다.",
            _sessionState.Profile.ArenaDefenseSnapshots.Count,
            _sessionState.Profile.ArenaMatchRecords.Count);
    }

    public Result EquipItem(string heroId, string itemInstanceId) => _sessionState.EquipItem(heroId, itemInstanceId);
    public Result UnequipItem(string heroId, string itemInstanceId) => _sessionState.UnequipItem(heroId, itemInstanceId);
    public Result EquipPermanentAugment(string augmentId) => _sessionState.EquipPermanentAugment(augmentId);
    public Result SelectPassiveBoard(string heroId, string boardId) => _sessionState.SelectPassiveBoard(heroId, boardId);

    public AuthorityActionResult PublishDefenseSnapshot(string blueprintId)
        => AuthorityActionResult.Unsupported("OfflineLocal은 공식 defense snapshot publish를 허용하지 않습니다.");

    public AuthorityActionResult StartArenaMatch(string defenseSnapshotId)
        => AuthorityActionResult.Unsupported("OfflineLocal은 공식 arena match start를 허용하지 않습니다.");

    public AuthorityActionResult FinalizeArenaMatch(string matchId, string clientEvidence)
        => AuthorityActionResult.Unsupported("OfflineLocal은 공식 arena settlement를 허용하지 않습니다.");

    public AuthorityActionResult ClaimArenaReward(string rewardClaimToken)
        => AuthorityActionResult.Unsupported("OfflineLocal은 공식 arena reward claim을 허용하지 않습니다.");

    public AuthorityActionResult CreateOfficialBattleSeed()
        => AuthorityActionResult.PreviewOnly("OfflineLocal 전투는 로컬 preview/편의 경로이며 official battle seed를 발급하지 않습니다.");

    public AuthorityActionResult ResolveOfficialMatch(string matchId)
        => AuthorityActionResult.PreviewOnly("OfflineLocal 전투 결과는 official settlement가 아니라 로컬 반영만 수행합니다.");

    public AuthorityActionResult ValidateReplayEvidence(string matchId)
        => AuthorityActionResult.PreviewOnly("OfflineLocal replay는 debug/audit 참고용이며 authoritative evidence가 아닙니다.");

    private void EnsureProfileMatches(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return;
        }

        if (!string.Equals(playerId, ActiveProfileId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Profile '{playerId}' is not active in OfflineLocal session.");
        }
    }
}
