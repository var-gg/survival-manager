using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Meta.Model;

namespace SM.Meta.Services;

public static class RunStateService
{
    public static ActiveRunState StartRun(
        string expeditionId,
        SquadBlueprintState blueprint,
        bool isQuickBattle)
    {
        var overlay = new RunOverlayState(
            0,
            Array.Empty<string>(),
            Array.Empty<string>(),
            LoadoutCompiler.CurrentCompileVersion,
            string.Empty);

        return new ActiveRunState(
            Guid.NewGuid().ToString("N"),
            expeditionId,
            blueprint,
            overlay,
            blueprint.DeploymentAssignments.OrderBy(pair => pair.Key).Select(pair => pair.Value).ToList(),
            isQuickBattle);
    }

    public static ActiveRunState AdvanceNode(ActiveRunState run, int nodeIndex)
    {
        return run with
        {
            Overlay = run.Overlay with { CurrentNodeIndex = nodeIndex }
        };
    }

    public static ActiveRunState ApplyTemporaryAugment(ActiveRunState run, string augmentId)
    {
        var augments = run.Overlay.TemporaryAugmentIds
            .Concat(new[] { augmentId })
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return run with
        {
            Overlay = run.Overlay with { TemporaryAugmentIds = augments }
        };
    }

    public static ActiveRunState SyncBlueprint(
        ActiveRunState run,
        SquadBlueprintState blueprint,
        string compileHash,
        IReadOnlyList<string> pendingRewardIds)
    {
        return run with
        {
            Blueprint = blueprint,
            BattleDeployHeroIds = blueprint.DeploymentAssignments.OrderBy(pair => pair.Key).Select(pair => pair.Value).ToList(),
            Overlay = run.Overlay with
            {
                PendingRewardIds = pendingRewardIds.ToList(),
                LastCompileHash = compileHash,
            }
        };
    }

    public static ActiveRunState CompleteBattle(ActiveRunState run, string matchId)
    {
        return run with { LastBattleMatchId = matchId };
    }
}
