using System.Collections.Generic;
using SM.Combat.Model;
using SM.Content;
using SM.Core.Content;
using SM.Core.Results;
using SM.Meta.Model;
using SM.Unity.Sandbox;

namespace SM.Unity;

public sealed partial class GameSessionState
{
    internal sealed class SessionDeploymentFlow
    {
        private readonly GameSessionState _session;

        internal SessionDeploymentFlow(GameSessionState session)
        {
            _session = session;
        }

        internal Result EquipItem(string heroId, string itemInstanceId) =>
            _session.EquipItemCore(heroId, itemInstanceId);

        internal Result UnequipItem(string heroId, string itemInstanceId) =>
            _session.UnequipItemCore(heroId, itemInstanceId);

        internal Result RefitItem(string itemInstanceId, int affixSlotIndex) =>
            _session.RefitItemCore(itemInstanceId, affixSlotIndex);

        internal Result UnlockPermanentAugmentCandidate(string augmentId) =>
            _session.UnlockPermanentAugmentCandidateCore(augmentId);

        internal Result EquipPermanentAugment(string augmentId) =>
            _session.EquipPermanentAugmentCore(augmentId);

        internal Result UnequipPermanentAugment(string augmentId) =>
            _session.UnequipPermanentAugmentCore(augmentId);

        internal Result SelectPassiveBoard(string heroId, string boardId) =>
            _session.SelectPassiveBoardCore(heroId, boardId);

        internal Result TogglePassiveNode(string heroId, string nodeId) =>
            _session.TogglePassiveNodeCore(heroId, nodeId);

        internal bool ToggleExpeditionHero(string heroId) => _session.ToggleExpeditionHeroCore(heroId);

        internal void EnsureBattleDeployReady() => _session.EnsureBattleDeployReadyCore();

        internal void ApplyQuickBattleAllySlotOverrides(CombatSandboxConfig config) =>
            _session.ApplyQuickBattleAllySlotOverridesCore(config);

        internal void PromoteToBattleDeploy(string heroId) => _session.PromoteToBattleDeployCore(heroId);

        internal string? GetAssignedHeroId(DeploymentAnchorId anchor) => _session.GetAssignedHeroIdCore(anchor);

        internal bool AssignHeroToAnchor(DeploymentAnchorId anchor, string? heroId) =>
            _session.AssignHeroToAnchorCore(anchor, heroId);

        internal bool CycleDeploymentAssignment(DeploymentAnchorId anchor) =>
            _session.CycleDeploymentAssignmentCore(anchor);

        internal void CycleTeamPosture() => _session.CycleTeamPostureCore();

        internal void SetTeamPosture(TeamPostureType posture) => _session.SetTeamPostureCore(posture);

        internal void SetTeamTactic(string teamTacticId) => _session.SetTeamTacticCore(teamTacticId);

        internal IEnumerable<(DeploymentAnchorId Anchor, string? HeroId)> EnumerateDeploymentAssignments() =>
            _session.EnumerateDeploymentAssignmentsCore();

        internal IReadOnlyList<BattleParticipantSpec> BuildBattleParticipants() =>
            _session.BuildBattleParticipantsCore();

        internal BattleLoadoutSnapshot BuildBattleLoadoutSnapshot() =>
            _session.BuildBattleLoadoutSnapshotCore();
    }
}
