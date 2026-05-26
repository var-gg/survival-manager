using System.Collections.Generic;
using SM.Combat.Model;

namespace SM.Meta.Model;

public enum SessionRealm
{
    OfflineLocal,
    OnlineAuthoritative,
}

public sealed record SessionCapabilities(
    bool CanUsePvp,
    bool CanClaimOfficialRewards,
    bool CanUploadAuthoritativeProgress)
{
    public static SessionCapabilities None { get; } = new(false, false, false);

    public static SessionCapabilities ForRealm(SessionRealm realm)
    {
        return realm switch
        {
            SessionRealm.OfflineLocal => None,
            SessionRealm.OnlineAuthoritative => new SessionCapabilities(true, true, true),
            _ => None,
        };
    }
}

public sealed record ProfileHeroView(
    string HeroId,
    string DisplayName,
    string RaceId,
    string ClassId,
    bool IsInExpeditionSquad,
    // wave-33-progression: 진짜 progression 노출. Level/Experience는 HeroProgressionRecord에서,
    // CurrentHp/MaxHp는 HeroInstanceRecord에서. 0/0이면 "데이터 없음" → 표시 fallback.
    int Level = 1,
    int Experience = 0,
    int ExperienceToNextLevel = 100,
    int CurrentHp = 0,
    int MaxHp = 0);

public sealed record ProfileView(
    string ProfileId,
    string DisplayName,
    SessionRealm Realm,
    bool IsOfficialProgression,
    int Gold,
    int Echo,
    int PermanentAugmentSlotCount,
    int HeroCount,
    int InventoryCount,
    IReadOnlyList<ProfileHeroView> Heroes);

public sealed record InventoryItemView(
    string ItemInstanceId,
    string ItemBaseId,
    string EquippedHeroId);

public sealed record InventoryView(
    int Count,
    IReadOnlyList<InventoryItemView> Items);

public sealed record LoadoutDeploymentView(
    DeploymentAnchorId Anchor,
    string HeroId);

public sealed record LoadoutView(
    string BlueprintId,
    TeamPostureType TeamPosture,
    int ExpeditionSquadCount,
    int BattleDeployCount,
    IReadOnlyList<string> ExpeditionSquadHeroIds,
    IReadOnlyList<LoadoutDeploymentView> Deployments);

public sealed record ArenaDashboardView(
    SessionRealm Realm,
    bool CanUsePvp,
    bool CanClaimOfficialRewards,
    string AvailabilityMessage,
    int DefenseSnapshotCount,
    int MatchRecordCount);

public enum AuthorityActionStatus
{
    Success,
    UnsupportedForRealm,
    PreviewOnly,
}

public sealed record AuthorityActionResult(
    AuthorityActionStatus Status,
    string Message)
{
    public static AuthorityActionResult Success(string message)
        => new(AuthorityActionStatus.Success, message);

    public static AuthorityActionResult Unsupported(string message)
        => new(AuthorityActionStatus.UnsupportedForRealm, message);

    public static AuthorityActionResult PreviewOnly(string message)
        => new(AuthorityActionStatus.PreviewOnly, message);
}
