using System;
using System.Collections.Generic;
using System.Linq;
using SM.Atlas.Model;
using SM.Atlas.Services;
using SM.Core.Content;
using SM.Meta.Model;
using SM.Meta.Services;

namespace SM.Unity;

public sealed partial class GameSessionState
{
    internal enum AtlasExpeditionHandoffFailure
    {
        None = 0,
        CandidateUnavailable = 1,
        RegionNodeMissing = 2,
        RegionNodeUnmapped = 3,
        ExpeditionSelectionRefused = 4,
    }

    internal sealed record AtlasExpeditionHandoffResult(
        bool Succeeded,
        AtlasExpeditionHandoffFailure Failure,
        string Diagnostic);

    internal sealed class SessionAtlasFlow
    {
        private sealed record CandidateResolution(
            AtlasStageCandidate? Candidate,
            AtlasRegionNode? Node,
            AtlasExpeditionHandoffFailure Failure);

        private readonly GameSessionState _session;

        internal SessionAtlasFlow(GameSessionState session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        internal AtlasSessionState EnsureAtlasSession(
            AtlasRegionDefinition region,
            AtlasTraversalMode? traversalMode = null)
        {
            if (region == null)
            {
                throw new ArgumentNullException(nameof(region));
            }

            var resolvedMode = ResolveTraversalMode(traversalMode);
            var identity = BuildIdentity();
            if (CanReuseSession(region, identity, resolvedMode))
            {
                return _session._atlasSession!;
            }

            _session._atlasSession = AlignToExpeditionProgress(region, AtlasSessionService.CreateInitial(
                region,
                identity,
                resolvedMode,
                AtlasGrayboxDataFactory.CreateDefaultPlacements(region)));
            return _session._atlasSession;
        }

        internal AtlasSessionResolution ResolveAtlasSession(AtlasRegionDefinition region)
        {
            return AtlasSessionService.Resolve(region, EnsureAtlasSession(region));
        }

        internal void SelectSigil(AtlasRegionDefinition region, string sigilId)
        {
            _session._atlasSession = AtlasSessionService.SelectSigil(
                region,
                EnsureAtlasSession(region),
                sigilId);
        }

        internal void SelectNode(AtlasRegionDefinition region, string nodeId)
        {
            _session._atlasSession = AtlasSessionService.SelectNode(
                region,
                EnsureAtlasSession(region),
                nodeId);
        }

        internal void PlaceSelectedSigil(AtlasRegionDefinition region, string nodeId)
        {
            _session._atlasSession = AtlasSessionService.PlaceSelectedSigil(
                region,
                EnsureAtlasSession(region),
                nodeId);
        }

        internal bool TryApplySelectedNodeToExpedition(AtlasRegionDefinition region) =>
            ApplySelectedNodeToExpedition(region).Succeeded;

        internal AtlasExpeditionHandoffResult ApplySelectedNodeToExpedition(AtlasRegionDefinition region)
        {
            if (region == null)
            {
                throw new ArgumentNullException(nameof(region));
            }

            _session._atlasExpeditionModifierPayload = null;
            _session._runBattlePayload = null;
            var state = EnsureAtlasSession(region);
            var resolution = ResolveCandidateForCurrentExpeditionNode(region, state);
            if (resolution.Failure != AtlasExpeditionHandoffFailure.None)
            {
                return Failure(
                    resolution.Failure,
                    region,
                    state,
                    resolution.Candidate,
                    resolution.Node,
                    selectNodeFromAtlas: "not_attempted");
            }

            var candidate = resolution.Candidate!;
            var node = resolution.Node!;

            var selectedFromAtlas = _session._expeditionFlow.SelectNodeFromAtlas(node.SiteNodeIndex);
            if (!selectedFromAtlas)
            {
                return Failure(
                    AtlasExpeditionHandoffFailure.ExpeditionSelectionRefused,
                    region,
                    state,
                    candidate,
                    node,
                    selectNodeFromAtlas: "false");
            }

            var modifier = BuildModifierPayload(region, state, candidate, node);
            _session._atlasExpeditionModifierPayload = modifier;
            _session._runBattlePayload = TryBuildRunBattlePayload(state, modifier);
            return new AtlasExpeditionHandoffResult(
                true,
                AtlasExpeditionHandoffFailure.None,
                string.Empty);
        }

        internal void Reset()
        {
            _session._atlasSession = null;
            _session._atlasExpeditionModifierPayload = null;
            _session._runBattlePayload = null;
        }

        private CandidateResolution ResolveCandidateForCurrentExpeditionNode(
            AtlasRegionDefinition region,
            AtlasSessionState state)
        {
            var candidates = EnumerateCandidatePreferences(region, state).ToArray();
            if (candidates.Length == 0)
            {
                return new CandidateResolution(
                    null,
                    null,
                    AtlasExpeditionHandoffFailure.CandidateUnavailable);
            }

            CandidateResolution? mappingFailure = null;
            CandidateResolution? selectionFailure = null;
            foreach (var candidate in candidates)
            {
                var node = region.Nodes.FirstOrDefault(item =>
                    string.Equals(item.NodeId, candidate.HexId, StringComparison.Ordinal));
                if (node == null)
                {
                    mappingFailure ??= new CandidateResolution(
                        candidate,
                        null,
                        AtlasExpeditionHandoffFailure.RegionNodeMissing);
                    continue;
                }

                if (node.SiteNodeIndex < 0)
                {
                    mappingFailure ??= new CandidateResolution(
                        candidate,
                        node,
                        AtlasExpeditionHandoffFailure.RegionNodeUnmapped);
                    continue;
                }

                if (!CanSelectExpeditionNode(node.SiteNodeIndex))
                {
                    selectionFailure ??= new CandidateResolution(
                        candidate,
                        node,
                        AtlasExpeditionHandoffFailure.ExpeditionSelectionRefused);
                    continue;
                }

                return new CandidateResolution(
                    candidate,
                    node,
                    AtlasExpeditionHandoffFailure.None);
            }

            return mappingFailure
                   ?? selectionFailure
                   ?? new CandidateResolution(
                       null,
                       null,
                       AtlasExpeditionHandoffFailure.CandidateUnavailable);
        }

        private bool CanSelectExpeditionNode(int nodeIndex)
        {
            var current = _session._expeditionFlow.GetCurrentExpeditionNode();
            return current != null
                   && nodeIndex >= 0
                   && nodeIndex < _session.ExpeditionNodes.Count
                   && ((current.Index == nodeIndex
                        && !_session._expeditionFlow.IsExpeditionNodeResolved(current.GraphNodeId))
                       || current.NextNodeIndices.Contains(nodeIndex));
        }

        private IEnumerable<AtlasStageCandidate> EnumerateCandidatePreferences(
            AtlasRegionDefinition region,
            AtlasSessionState state)
        {
            var emitted = new HashSet<string>(StringComparer.Ordinal);

            // StageCandidatePath에 들어간 hex는 이미 SelectNode 시점에 lock check를 통과했다 —
            // siteSpineIndex가 그 candidate를 따라 advance했기 때문에 지금 시점 lock 재검은 불필요.
            foreach (var hexId in state.StageCandidatePath ?? Array.Empty<string>())
            {
                var candidate = AtlasSpineProgressionService.FindCandidate(region, hexId);
                if (candidate != null && emitted.Add(candidate.HexId))
                {
                    yield return candidate;
                }
            }

            // task-atlas-sitetrack-absorption-v1 acceptance #4·#5:
            // 사용자가 future-locked node(Boss/Extract)를 click한 경우 SelectedNodeId의 candidate가
            // lock 상태로 SelectedNodeId에 박혀 있다. direct handoff를 차단하고 current stage candidate로 fallback.
            var selectedCandidate = AtlasSpineProgressionService.FindCandidate(region, state.SelectedNodeId);
            if (selectedCandidate != null
                && emitted.Add(selectedCandidate.HexId)
                && AtlasSpineProgressionService.CanEnterStoryCandidate(
                    region,
                    selectedCandidate.HexId,
                    state.SiteSpineIndex,
                    state.BossResolved))
            {
                yield return selectedCandidate;
            }

            foreach (var candidate in AtlasSpineProgressionService.CurrentCandidates(
                         region,
                         state.SiteSpineIndex,
                         state.BossResolved))
            {
                if (emitted.Add(candidate.HexId))
                {
                    yield return candidate;
                }
            }
        }

        private AtlasSessionState AlignToExpeditionProgress(
            AtlasRegionDefinition region,
            AtlasSessionState state)
        {
            return state with
            {
                SiteSpineIndex = ResolveSemanticSiteSpineIndex(region),
                BossResolved = _session.ExpeditionNodes.Any(node =>
                    node.NodeKind == SiteNodeKindValue.Boss
                    && _session._expeditionFlow.IsExpeditionNodeResolved(node.GraphNodeId)),
            };
        }

        private int ResolveSemanticSiteSpineIndex(AtlasRegionDefinition region)
        {
            var mappedNodeIndices = region.Nodes
                .Where(node => node.SiteNodeIndex >= 0)
                .Select(node => node.SiteNodeIndex)
                .ToHashSet();
            var current = _session._expeditionFlow.GetCurrentExpeditionNode();
            if (current != null && mappedNodeIndices.Contains(current.Index))
            {
                return Math.Min(current.Index, AtlasSpineProgressionService.BossStageIndex);
            }

            // Briefing/event nodes are graph routing nodes rather than Atlas spine stages.
            // Walk only through unresolved non-battle routing nodes until the first mapped stage;
            // never skip an unmapped battle node.
            if (current is { RequiresBattle: false })
            {
                var queue = new Queue<int>(current.NextNodeIndices ?? Array.Empty<int>());
                var visited = new HashSet<int> { current.Index };
                while (queue.Count > 0)
                {
                    var index = queue.Dequeue();
                    if (!visited.Add(index))
                    {
                        continue;
                    }

                    var node = _session.ExpeditionNodes.FirstOrDefault(item => item.Index == index);
                    if (node == null)
                    {
                        continue;
                    }

                    if (mappedNodeIndices.Contains(node.Index))
                    {
                        return Math.Min(node.Index, AtlasSpineProgressionService.BossStageIndex);
                    }

                    if (!node.RequiresBattle)
                    {
                        foreach (var next in node.NextNodeIndices ?? Array.Empty<int>())
                        {
                            queue.Enqueue(next);
                        }
                    }
                }
            }

            var lastResolvedMappedIndex = _session.ExpeditionNodes
                .Where(node =>
                    mappedNodeIndices.Contains(node.Index)
                    && _session._expeditionFlow.IsExpeditionNodeResolved(node.GraphNodeId))
                .Select(node => node.Index)
                .DefaultIfEmpty(-1)
                .Max();
            return Math.Min(
                Math.Max(0, lastResolvedMappedIndex + 1),
                AtlasSpineProgressionService.BossStageIndex);
        }

        private AtlasExpeditionHandoffResult Failure(
            AtlasExpeditionHandoffFailure failure,
            AtlasRegionDefinition region,
            AtlasSessionState state,
            AtlasStageCandidate? candidate,
            AtlasRegionNode? node,
            string selectNodeFromAtlas)
        {
            var current = _session._expeditionFlow.GetCurrentExpeditionNode();
            var action = failure switch
            {
                AtlasExpeditionHandoffFailure.CandidateUnavailable =>
                    "Verify that the current Atlas spine stage declares at least one candidate.",
                AtlasExpeditionHandoffFailure.RegionNodeMissing =>
                    "Add the candidate hex to the Atlas region node catalog.",
                AtlasExpeditionHandoffFailure.RegionNodeUnmapped =>
                    "Assign a non-negative SiteNodeIndex to the candidate Atlas region node.",
                AtlasExpeditionHandoffFailure.ExpeditionSelectionRefused =>
                    "Verify that the candidate SiteNodeIndex is the current expedition node or a direct authored edge from it.",
                _ => "Inspect the Atlas-to-expedition handoff state.",
            };
            var diagnostic =
                $"failure={failure};"
                + $" siteId={state.Identity.SiteId};"
                + $" currentExpeditionNodeIndex={_session.CurrentExpeditionNodeIndex};"
                + $" currentGraphNodeId={current?.GraphNodeId ?? "<missing>"};"
                + $" currentNodeKind={current?.NodeKind.ToString() ?? "<missing>"};"
                + $" currentResolved={(current != null && _session._expeditionFlow.IsExpeditionNodeResolved(current.GraphNodeId))};"
                + $" currentNext=[{string.Join(",", current?.NextNodeIndices ?? Array.Empty<int>())}];"
                + $" siteSpineIndex={state.SiteSpineIndex};"
                + $" bossResolved={state.BossResolved};"
                + $" stageCandidatePath=[{string.Join(",", state.StageCandidatePath ?? Array.Empty<string>())}];"
                + $" selectedAtlasNodeId={state.SelectedNodeId};"
                + $" candidateHexId={candidate?.HexId ?? "<none>"};"
                + $" candidateSiteNodeIndex={(node == null ? "<none>" : node.SiteNodeIndex.ToString())};"
                + $" selectNodeFromAtlas={selectNodeFromAtlas};"
                + $" atlasSiteNodes=[{string.Join(",", region.Nodes.Where(item => item.SiteNodeIndex >= 0).Select(item => $"{item.NodeId}:{item.SiteNodeIndex}"))}];"
                + $" action={action}";
            return new AtlasExpeditionHandoffResult(false, failure, diagnostic);
        }

        private AtlasExpeditionModifierPayload BuildModifierPayload(
            AtlasRegionDefinition region,
            AtlasSessionState state,
            AtlasStageCandidate candidate,
            AtlasRegionNode node)
        {
            var resolution = AtlasSessionService.Resolve(region, state);
            var stack = resolution.ModifierResolution.FindNode(node.NodeId)
                        ?? new AtlasNodeModifierStack(node.NodeId, node.Hex, Array.Empty<AtlasSigilInfluence>(), Array.Empty<AtlasResolvedModifier>());
            var expeditionNode = _session.ExpeditionNodes.FirstOrDefault(item => item.Index == node.SiteNodeIndex);
            var stageCandidatePath = AtlasSpineProgressionService.StageCandidatePath(state.StageCandidatePath, candidate);
            var stageCandidatePathHash = AtlasContextHasher.BuildStageCandidatePathHash(stageCandidatePath);
            var nodeOverlayHash = AtlasContextHasher.BuildNodeOverlayHash(region.RegionId, node, state.Identity.CycleSalt, stack);
            var battleContextHash = AtlasContextHasher.BuildBattleContextHash(
                state.Identity.ChapterId,
                state.Identity.SiteId,
                node.SiteNodeIndex,
                expeditionNode?.Id ?? state.Identity.EncounterId,
                stageCandidatePathHash,
                nodeOverlayHash,
                state.Identity.SquadSnapshotId);

            return new AtlasExpeditionModifierPayload(
                region.RegionId,
                node.NodeId,
                node.SiteNodeIndex,
                expeditionNode?.Id ?? string.Empty,
                stageCandidatePathHash,
                nodeOverlayHash,
                battleContextHash,
                stack.RewardBiasPercent,
                stack.ThreatPressurePercent,
                stack.AffinityBoostPercent,
                stack.ResolvedModifiers);
        }

        private static RunBattlePayload? TryBuildRunBattlePayload(
            AtlasSessionState state,
            AtlasExpeditionModifierPayload modifier)
        {
            var resolvedIds = modifier.ResolvedModifiers
                .Select(static m => $"{m.Category}:{m.Label}")
                .Where(static id => !string.IsNullOrWhiteSpace(id))
                .ToArray();

            var input = new RunBattlePayloadInput(
                RunId: state.Identity.RunId,
                ChapterId: state.Identity.ChapterId,
                SiteId: state.Identity.SiteId,
                SiteNodeIndex: modifier.SiteNodeIndex,
                EncounterId: state.Identity.EncounterId,
                ExpeditionNodeId: modifier.ExpeditionNodeId,
                SquadSnapshotId: state.Identity.SquadSnapshotId,
                StageCandidatePathHash: modifier.StageCandidatePathHash,
                NodeOverlayHash: modifier.NodeOverlayHash,
                BattleContextHash: modifier.BattleContextHash,
                RewardBiasPercent: modifier.RewardBiasPercent,
                ThreatPressurePercent: modifier.ThreatPressurePercent,
                AffinityBoostPercent: modifier.AffinityBoostPercent,
                ResolvedModifierIds: resolvedIds);

            return RunBattlePayloadBuilder.TryBuild(input, out var payload) ? payload : null;
        }

        private bool CanReuseSession(
            AtlasRegionDefinition region,
            AtlasSessionIdentity identity,
            AtlasTraversalMode traversalMode)
        {
            var current = _session._atlasSession;
            if (current == null || current.TraversalMode != traversalMode || current.Identity != identity)
            {
                return false;
            }

            return region.Nodes.Any(node => string.Equals(node.NodeId, current.SelectedNodeId, StringComparison.Ordinal));
        }

        private AtlasSessionIdentity BuildIdentity()
        {
            _session.EnsureCampaignSelection();
            var runId = _session.ActiveRun?.RunId;
            if (string.IsNullOrWhiteSpace(runId))
            {
                runId = string.IsNullOrWhiteSpace(_session.Profile.ActiveRun?.RunId)
                    ? _session.GetExpeditionRunId()
                    : _session.Profile.ActiveRun.RunId;
            }

            var chapterId = _session.Profile.CampaignProgress.SelectedChapterId;
            var siteId = _session.Profile.CampaignProgress.SelectedSiteId;
            var encounterId = _session.GetSelectedExpeditionNode()?.Id
                              ?? _session.GetCurrentExpeditionNode()?.Id
                              ?? string.Empty;
            if (string.IsNullOrWhiteSpace(encounterId))
            {
                encounterId = string.IsNullOrWhiteSpace(siteId) ? "atlas:unknown" : $"{siteId}:atlas";
            }

            return new AtlasSessionIdentity(
                runId ?? string.Empty,
                chapterId ?? string.Empty,
                siteId ?? string.Empty,
                encounterId,
                BuildCycleSalt(siteId),
                BuildSquadSnapshotId());
        }

        private AtlasTraversalMode ResolveTraversalMode(AtlasTraversalMode? requested)
        {
            if (requested.HasValue)
            {
                return requested.Value;
            }

            // 무한 순환 run이 활성이면 EndlessRegion — sigil cap 3·endless 질량 상수가 이 분기로 살아난다.
            // fresh 세션/스토리 run은 종전 판정 그대로(StoryFirstClear/StoryRevisit) 보존.
            if ((_session.ActiveRun?.EndlessCycleIndex ?? 0) > 0)
            {
                return AtlasTraversalMode.EndlessRegion;
            }

            var selectedSiteId = _session.Profile.CampaignProgress.SelectedSiteId;
            return !string.IsNullOrWhiteSpace(selectedSiteId)
                   && _session.Profile.CampaignProgress.ClearedSiteIds.Contains(selectedSiteId, StringComparer.Ordinal)
                ? AtlasTraversalMode.StoryRevisit
                : AtlasTraversalMode.StoryFirstClear;
        }

        private string BuildCycleSalt(string siteId)
        {
            var cleared = !string.IsNullOrWhiteSpace(siteId)
                          && _session.Profile.CampaignProgress.ClearedSiteIds.Contains(siteId, StringComparer.Ordinal);
            var salt = string.Join(
                ":",
                "campaign",
                string.IsNullOrWhiteSpace(siteId) ? "site_unknown" : siteId,
                cleared ? "revisit" : "story",
                _session.Profile.CampaignProgress.EndlessUnlocked ? "endless_unlocked" : "endless_locked");

            // 무한 순환 회차 토큰 — NodeOverlayHash/프리뷰 시드가 회차별로 분화된다. cycle 0은 종전 salt 그대로.
            var cycleIndex = _session.ActiveRun?.EndlessCycleIndex ?? 0;
            return cycleIndex > 0 ? $"{salt}:cycle_{cycleIndex}" : salt;
        }

        private string BuildSquadSnapshotId()
        {
            var deployed = _session.BattleDeployHeroIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            if (deployed.Length > 0)
            {
                return "deploy:" + string.Join("|", deployed);
            }

            var squad = _session.ExpeditionSquadHeroIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            return squad.Length == 0 ? "squad:empty" : "squad:" + string.Join("|", squad);
        }
    }

    internal AtlasExpeditionHandoffResult ApplyAtlasSelectionToExpedition(AtlasRegionDefinition region) =>
        _atlasFlow.ApplySelectedNodeToExpedition(region);

    internal void ResetAtlasSession() => _atlasFlow.Reset();
}
