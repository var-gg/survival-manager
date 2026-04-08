using System;
using System.Linq;
using SM.Core.Results;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Persistence.Abstractions;
using SM.Persistence.Abstractions.Models;
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
    private SessionPersistenceLane _lane = SessionPersistenceLane.Canonical;

    public OfflineLocalSessionAdapter(
        GameSessionState sessionState,
        PersistenceEntryPoint persistence)
    {
        _sessionState = sessionState;
        _persistence = persistence;
        ApplyInstrumentationPolicy();
    }

    public string ActiveProfileId => ResolveRepositoryProfileId();
    public bool IsTransientTownSmokeActive => _lane == SessionPersistenceLane.TransientTownSmoke;
    public bool IsDedicatedSmokeNamespace => _lane == SessionPersistenceLane.DedicatedSmokeNamespace;

    public void UseDedicatedSmokeNamespace()
    {
        _lane = SessionPersistenceLane.DedicatedSmokeNamespace;
        ApplyInstrumentationPolicy();
    }

    public void BeginTransientTownSmoke()
    {
        _lane = SessionPersistenceLane.TransientTownSmoke;
        ApplyInstrumentationPolicy();
    }

    public void ReturnToCanonicalLane()
    {
        _lane = SessionPersistenceLane.Canonical;
        ApplyInstrumentationPolicy();
    }

    public SessionCheckpointResult LoadOrCreateProfile(SessionCheckpointKind kind = SessionCheckpointKind.StartupLoad)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        RecordOperationalTelemetry(RuntimeOperationalTelemetry.CreateCheckpointStarted(
            ResolveRunId(),
            kind,
            ActiveProfileId));

        using (LoadOrCreateProfileMarker.Auto())
        {
            if (_persistence.Repository is ISaveRepositoryDiagnostics diagnostics)
            {
                var loadResult = diagnostics.LoadOrCreateDetailed(
                    ActiveProfileId,
                    CreateRequest(kind));
                var mapped = MapLoadResult(kind, loadResult);
                if (!mapped.IsSuccessful)
                {
                    RecordOperationalTelemetry(RuntimeOperationalTelemetry.CreateCheckpointFailed(
                        ResolveRunId(),
                        kind,
                        ActiveProfileId,
                        mapped.Message,
                        mapped.QuarantinePath));
                    return mapped;
                }

                if (loadResult.Profile == null)
                {
                    var failed = SessionCheckpointResult.Failed(kind, ActiveProfileId, "Load succeeded without profile payload.");
                    RecordOperationalTelemetry(RuntimeOperationalTelemetry.CreateCheckpointFailed(
                        ResolveRunId(),
                        kind,
                        ActiveProfileId,
                        failed.Message,
                        failed.QuarantinePath));
                    return failed;
                }

                _sessionState.BindProfile(loadResult.Profile);
                if (mapped.Status == SessionCheckpointStatus.RecoveredFromBackup)
                {
                    _sessionState.Profile.SuspicionFlags.Add(new SuspicionFlagRecord
                    {
                        FlagId = Guid.NewGuid().ToString("N"),
                        RunId = ResolveRunId(),
                        MatchId = string.Empty,
                        Reason = "save_recovered_from_backup",
                        ExpectedHash = loadResult.Manifest?.PayloadHash ?? string.Empty,
                        ObservedHash = loadResult.RecoveryPath,
                        CreatedAtUtc = DateTime.UtcNow.ToString("O"),
                    });
                }

                RecordOperationalTelemetry(RuntimeOperationalTelemetry.CreateCheckpointSucceeded(
                    ResolveRunId(),
                    kind,
                    ActiveProfileId,
                    mapped.Status,
                    mapped.PayloadBytes,
                    mapped.Message));
                stopwatch.Stop();
                RuntimeInstrumentation.LogDuration(
                    nameof(OfflineLocalSessionAdapter) + ".LoadOrCreateProfile",
                    stopwatch.Elapsed,
                    $"profileId={ActiveProfileId}; status={mapped.Status}");
                return mapped;
            }

            var profile = _persistence.Repository.LoadOrCreate(ActiveProfileId);
            _sessionState.BindProfile(profile);
            var fallback = SessionCheckpointResult.Success(
                kind,
                ActiveProfileId,
                "Profile loaded.",
                payloadBytes: 0);
            RecordOperationalTelemetry(RuntimeOperationalTelemetry.CreateCheckpointSucceeded(
                ResolveRunId(),
                kind,
                ActiveProfileId,
                fallback.Status,
                fallback.PayloadBytes,
                fallback.Message));
            stopwatch.Stop();
            RuntimeInstrumentation.LogDuration(
                nameof(OfflineLocalSessionAdapter) + ".LoadOrCreateProfile",
                stopwatch.Elapsed,
                $"profileId={ActiveProfileId}; status={fallback.Status}");
            return fallback;
        }
    }

    public SessionCheckpointResult SaveProfile(SessionCheckpointKind kind = SessionCheckpointKind.ManualSave)
    {
        if (IsTransientTownSmokeActive)
        {
            var blocked = SessionCheckpointResult.Blocked(
                kind,
                ActiveProfileId,
                "Transient Town smoke overlay does not write canonical save.");
            RecordOperationalTelemetry(RuntimeOperationalTelemetry.CreateCheckpointFailed(
                ResolveRunId(),
                kind,
                ActiveProfileId,
                blocked.Message,
                blocked.QuarantinePath));
            return blocked;
        }

        RecordOperationalTelemetry(RuntimeOperationalTelemetry.CreateCheckpointStarted(
            ResolveRunId(),
            kind,
            ActiveProfileId));

        if (_persistence.Repository is ISaveRepositoryDiagnostics diagnostics)
        {
            var saveResult = diagnostics.SaveDetailed(_sessionState.Profile, CreateRequest(kind));
            var mapped = MapSaveResult(kind, saveResult);
            if (mapped.IsSuccessful)
            {
                RecordOperationalTelemetry(RuntimeOperationalTelemetry.CreateCheckpointSucceeded(
                    ResolveRunId(),
                    kind,
                    ActiveProfileId,
                    mapped.Status,
                    mapped.PayloadBytes,
                    mapped.Message));
            }
            else
            {
                RecordOperationalTelemetry(RuntimeOperationalTelemetry.CreateCheckpointFailed(
                    ResolveRunId(),
                    kind,
                    ActiveProfileId,
                    mapped.Message,
                    mapped.QuarantinePath));
            }

            return mapped;
        }

        try
        {
            _persistence.Repository.Save(_sessionState.Profile);
            var fallback = SessionCheckpointResult.Success(kind, ActiveProfileId, "Profile saved.");
            RecordOperationalTelemetry(RuntimeOperationalTelemetry.CreateCheckpointSucceeded(
                ResolveRunId(),
                kind,
                ActiveProfileId,
                fallback.Status,
                fallback.PayloadBytes,
                fallback.Message));
            return fallback;
        }
        catch (Exception ex)
        {
            var failed = SessionCheckpointResult.Failed(kind, ActiveProfileId, ex.Message);
            RecordOperationalTelemetry(RuntimeOperationalTelemetry.CreateCheckpointFailed(
                ResolveRunId(),
                kind,
                ActiveProfileId,
                failed.Message,
                failed.QuarantinePath));
            return failed;
        }
    }

    ProfileView IProfileQueryService.GetProfileView(string playerId)
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

    InventoryView IProfileQueryService.GetInventoryView(string playerId)
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

    LoadoutView IProfileQueryService.GetLoadoutView(string playerId)
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

    ArenaDashboardView IArenaQueryService.GetArenaDashboard(string playerId)
    {
        EnsureProfileMatches(playerId);
        return new ArenaDashboardView(
            SessionRealm.OfflineLocal,
            false,
            false,
            "공식 PvP와 공식 defense snapshot은 OnlineAuthoritative 세션에서만 열립니다.",
            _sessionState.Profile.ArenaDefenseSnapshots.Count,
            _sessionState.Profile.ArenaMatchRecords.Count);
    }

    Result IProfileCommandService.EquipItem(string heroId, string itemInstanceId) => _sessionState.EquipItem(heroId, itemInstanceId);
    Result IProfileCommandService.UnequipItem(string heroId, string itemInstanceId) => _sessionState.UnequipItem(heroId, itemInstanceId);
    Result IProfileCommandService.EquipPermanentAugment(string augmentId) => _sessionState.EquipPermanentAugment(augmentId);
    Result IProfileCommandService.SelectPassiveBoard(string heroId, string boardId) => _sessionState.SelectPassiveBoard(heroId, boardId);

    AuthorityActionResult IArenaCommandService.PublishDefenseSnapshot(string blueprintId)
        => AuthorityActionResult.Unsupported("OfflineLocal은 공식 defense snapshot publish를 허용하지 않습니다.");

    AuthorityActionResult IArenaCommandService.StartArenaMatch(string defenseSnapshotId)
        => AuthorityActionResult.Unsupported("OfflineLocal은 공식 arena match start를 허용하지 않습니다.");

    AuthorityActionResult IArenaCommandService.FinalizeArenaMatch(string matchId, string clientEvidence)
        => AuthorityActionResult.Unsupported("OfflineLocal은 공식 arena settlement를 허용하지 않습니다.");

    AuthorityActionResult IArenaCommandService.ClaimArenaReward(string rewardClaimToken)
        => AuthorityActionResult.Unsupported("OfflineLocal은 공식 arena reward claim을 허용하지 않습니다.");

    AuthorityActionResult IBattleAuthority.CreateOfficialBattleSeed()
        => AuthorityActionResult.PreviewOnly("OfflineLocal 전투는 로컬 preview/편의 경로이며 official battle seed를 발급하지 않습니다.");

    AuthorityActionResult IBattleAuthority.ResolveOfficialMatch(string matchId)
        => AuthorityActionResult.PreviewOnly("OfflineLocal 전투 결과는 official settlement가 아니라 로컬 반영만 수행합니다.");

    AuthorityActionResult IBattleAuthority.ValidateReplayEvidence(string matchId)
        => AuthorityActionResult.PreviewOnly("OfflineLocal replay는 debug/audit 참고용이며 authoritative evidence가 아닙니다.");

    private SaveRepositoryRequest CreateRequest(SessionCheckpointKind kind)
    {
        return new SaveRepositoryRequest
        {
            CheckpointKind = kind.ToString(),
            CompileHash = _sessionState.Profile.ActiveRun?.CompileHash ?? string.Empty,
        };
    }

    private SessionCheckpointResult MapLoadResult(SessionCheckpointKind kind, SaveRepositoryLoadResult loadResult)
    {
        return loadResult.Status switch
        {
            SaveRepositoryLoadStatus.MissingCreated => SessionCheckpointResult.Success(
                kind,
                ActiveProfileId,
                loadResult.Message,
                loadResult.RecoveryPath,
                createdNewProfile: true,
                manifestVerified: loadResult.ManifestVerified,
                payloadBytes: loadResult.PayloadBytes),
            SaveRepositoryLoadStatus.LoadedPrimary => SessionCheckpointResult.Success(
                kind,
                ActiveProfileId,
                loadResult.Message,
                loadResult.RecoveryPath,
                createdNewProfile: false,
                manifestVerified: loadResult.ManifestVerified,
                payloadBytes: loadResult.PayloadBytes),
            SaveRepositoryLoadStatus.LoadedBackupRecovered => SessionCheckpointResult.Recovered(
                kind,
                ActiveProfileId,
                loadResult.Message,
                loadResult.RecoveryPath,
                loadResult.QuarantinePath,
                loadResult.PayloadBytes),
            _ => SessionCheckpointResult.Failed(
                kind,
                ActiveProfileId,
                loadResult.Message,
                loadResult.RecoveryPath,
                loadResult.QuarantinePath),
        };
    }

    private SessionCheckpointResult MapSaveResult(SessionCheckpointKind kind, SaveRepositorySaveResult saveResult)
    {
        return saveResult.IsSuccessful
            ? SessionCheckpointResult.Success(
                kind,
                ActiveProfileId,
                saveResult.Message,
                saveResult.RecoveryPath,
                payloadBytes: saveResult.PayloadBytes,
                manifestVerified: true)
            : SessionCheckpointResult.Failed(
                kind,
                ActiveProfileId,
                saveResult.Message,
                saveResult.RecoveryPath,
                saveResult.QuarantinePath);
    }

    private string ResolveRepositoryProfileId()
    {
        var profileId = string.IsNullOrWhiteSpace(_persistence.Config.ProfileId)
            ? "default"
            : _persistence.Config.ProfileId;
        return _lane == SessionPersistenceLane.DedicatedSmokeNamespace
            ? $"{profileId}.smoke"
            : profileId;
    }

    private string ResolveRunId()
    {
        return _sessionState.ActiveRun?.RunId
               ?? _sessionState.Profile.ActiveRun?.RunId
               ?? ActiveProfileId;
    }

    private void RecordOperationalTelemetry(SM.Combat.Model.TelemetryEventRecord record)
    {
        _sessionState.RecordOperationalTelemetry(record);
    }

    private void ApplyInstrumentationPolicy()
    {
        RuntimeInstrumentation.SetPolicy(_lane == SessionPersistenceLane.Canonical
            ? RuntimeInstrumentationPolicy.SummaryNormal
            : RuntimeInstrumentationPolicy.VerboseSmoke);
    }

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
