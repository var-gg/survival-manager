using SM.Combat.Model;
using SM.Meta.Model;

namespace SM.Meta.Services;

public static class BattleEncounterPlans
{
    public static BattleEncounterPlan CreateObserverSmokePlan()
    {
        return new BattleEncounterPlan(
            new[]
            {
                CreateEnemy("enemy_guardian", "Enemy Guardian", "guardian", DeploymentAnchorId.FrontTop),
                CreateEnemy("enemy_raider", "Enemy Raider", "raider", DeploymentAnchorId.FrontBottom),
                CreateEnemy("enemy_hunter", "Enemy Hunter", "hunter", DeploymentAnchorId.BackTop),
                CreateEnemy("enemy_hexer", "Enemy Hexer", "hexer", DeploymentAnchorId.BackBottom),
            },
            TeamPostureType.StandardAdvance);
    }

    private static BattleParticipantSpec CreateEnemy(
        string id,
        string displayName,
        string archetypeId,
        DeploymentAnchorId anchor)
    {
        return new BattleParticipantSpec(
            id,
            displayName,
            archetypeId,
            anchor,
            string.Empty,
            string.Empty,
            System.Array.Empty<BattleEquippedItemSpec>(),
            System.Array.Empty<string>());
    }
}
