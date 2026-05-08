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
                CreateEnemy("enemy_grey_fang", "회조 (灰爪) / Grey Fang", "reaver", "npc_grey_fang", DeploymentAnchorId.FrontTop),
                CreateEnemy("enemy_silent_moon", "침월 (沉月) / Silent Moon", "hexer", "npc_silent_moon", DeploymentAnchorId.FrontBottom),
                CreateEnemy("enemy_lyra_sternfeld", "선영 (宣英) / Lyra Sternfeld", "priest", "npc_lyra_sternfeld", DeploymentAnchorId.BackTop),
                CreateEnemy("enemy_black_vellum", "흑지 (黑紙) / Black Vellum", "shaman", "npc_black_vellum", DeploymentAnchorId.BackBottom),
            },
            TeamPostureType.StandardAdvance);
    }

    private static BattleParticipantSpec CreateEnemy(
        string id,
        string displayName,
        string archetypeId,
        string characterId,
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
            System.Array.Empty<string>(),
            CharacterId: characterId);
    }
}
