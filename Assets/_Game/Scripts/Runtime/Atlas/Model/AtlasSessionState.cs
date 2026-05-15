using System.Collections.Generic;

namespace SM.Atlas.Model;

public sealed record AtlasSessionIdentity(
    string RunId,
    string ChapterId,
    string SiteId,
    string EncounterId,
    string CycleSalt,
    string SquadSnapshotId)
{
    public static AtlasSessionIdentity GrayboxDefault { get; } = new(
        "atlas_graybox_run",
        "chapter_graybox",
        "site_wolfpine",
        "encounter_graybox",
        "cycle_2026_05_p2",
        "squad:dawn|pack|echo|grave");
}

public sealed record AtlasSessionState(
    AtlasSessionIdentity Identity,
    AtlasTraversalMode TraversalMode,
    string SelectedSigilId,
    string SelectedNodeId,
    int SiteSpineIndex,
    bool BossResolved,
    IReadOnlyList<string> StageCandidatePath,
    IReadOnlyList<AtlasPlacedSigil> Placements);

public sealed record AtlasSessionResolution(
    AtlasSessionState State,
    int CurrentStageIndex,
    int ActiveSigilCap,
    AtlasSigilMathResolution MathResolution,
    AtlasSigilResolution ModifierResolution,
    AtlasRegionNode SelectedNode,
    AtlasNodeModifierStack SelectedStack,
    AtlasStageCandidate? SelectedCandidate,
    IReadOnlyList<string> StageCandidatePath,
    string StageCandidatePathHash,
    string SigilSnapshotHash);
