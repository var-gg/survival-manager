using System;
using System.Linq;
using SM.Atlas.Model;
using SM.Atlas.Services;

namespace SM.Unity;

public sealed partial class GameSessionState
{
    internal sealed class SessionAtlasFlow
    {
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

            _session._atlasSession = AtlasSessionService.CreateInitial(
                region,
                identity,
                resolvedMode,
                AtlasGrayboxDataFactory.CreateDefaultPlacements(region));
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

        internal void Reset()
        {
            _session._atlasSession = null;
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
            return string.Join(
                ":",
                "campaign",
                string.IsNullOrWhiteSpace(siteId) ? "site_unknown" : siteId,
                cleared ? "revisit" : "story",
                _session.Profile.CampaignProgress.EndlessUnlocked ? "endless_unlocked" : "endless_locked");
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

    internal void ResetAtlasSession() => _atlasFlow.Reset();
}
