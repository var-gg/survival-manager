using System.Collections.Generic;
using SM.Combat.Model;

namespace SM.Meta.Model;

public sealed record CampaignProgressState(
    string SelectedChapterId,
    string SelectedSiteId,
    IReadOnlyList<string> ClearedChapterIds,
    IReadOnlyList<string> ClearedSiteIds,
    bool StoryCleared,
    bool EndlessUnlocked);

public sealed record SiteTrackNodeState(
    int Index,
    string EncounterId,
    string RewardSourceId,
    bool RequiresBattle,
    bool IsResolved);

public sealed record BattleContextState(
    string ChapterId,
    string SiteId,
    int SiteNodeIndex,
    string EncounterId,
    int BattleSeed,
    string BattleContextHash,
    string RewardSourceId,
    int ThreatSkulls,
    bool IsBoss,
    string FactionId,
    string BossOverlayId);

public sealed record ResolvedEncounterContext(
    BattleContextState Context,
    TeamPostureType EnemyPosture,
    IReadOnlyList<BattleUnitLoadout> Enemies);
