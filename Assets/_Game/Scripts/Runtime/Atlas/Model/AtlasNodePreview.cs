using System.Collections.Generic;

namespace SM.Atlas.Model;

public sealed record AtlasRecommendedCharacter(
    string CharacterId,
    string DisplayName,
    string Role,
    string Reason,
    int Score);

public sealed record AtlasNodePreview(
    string NodeId,
    string NodeLabel,
    string JudgementLine,
    string EnemyPreview,
    string RewardPreview,
    string SigilAugmentBoundaryNote,
    IReadOnlyList<AtlasResolvedModifier> ModifierStack,
    IReadOnlyList<AtlasRecommendedCharacter> RecommendedCharacters,
    string RouteId,
    string NodeOverlayHash,
    string BattleContextHash);
