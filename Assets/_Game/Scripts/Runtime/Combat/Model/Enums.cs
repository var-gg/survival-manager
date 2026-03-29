namespace SM.Combat.Model;

public enum TeamSide { Ally = 0, Enemy = 1 }
public enum DeploymentAnchorId { FrontTop = 0, FrontCenter = 1, FrontBottom = 2, BackTop = 3, BackCenter = 4, BackBottom = 5 }
public enum BattleActionType { BasicAttack = 0, ActiveSkill = 1, WaitDefend = 2 }
public enum TacticConditionType { SelfHpBelow = 0, AllyHpBelow = 1, EnemyInRange = 2, LowestHpEnemy = 3, EnemyExposed = 4, Fallback = 5 }
public enum TargetSelectorType { Self = 0, LowestHpAlly = 1, FirstEnemyInRange = 2, LowestHpEnemy = 3, NearestEnemy = 4, MostExposedEnemy = 5 }
public enum SkillKind { Strike = 0, Heal = 1 }
public enum CombatActionState { Spawn = 0, AdvanceToAnchor = 1, SeekTarget = 2, MoveToEngage = 3, Windup = 4, Recovery = 5, Reposition = 6, Retreat = 7, Dead = 8 }
public enum TeamPostureType { HoldLine = 0, StandardAdvance = 1, ProtectCarry = 2, CollapseWeakSide = 3, AllInBackline = 4 }

public static class DeploymentAnchorIdExtensions
{
    public static bool IsFrontRow(this DeploymentAnchorId anchor)
    {
        return anchor is DeploymentAnchorId.FrontTop or DeploymentAnchorId.FrontCenter or DeploymentAnchorId.FrontBottom;
    }

    public static bool IsBackRow(this DeploymentAnchorId anchor) => !anchor.IsFrontRow();

    public static int LaneIndex(this DeploymentAnchorId anchor)
    {
        return anchor switch
        {
            DeploymentAnchorId.FrontTop or DeploymentAnchorId.BackTop => 1,
            DeploymentAnchorId.FrontCenter or DeploymentAnchorId.BackCenter => 0,
            _ => -1,
        };
    }

    public static string ToDisplayName(this DeploymentAnchorId anchor)
    {
        return anchor switch
        {
            DeploymentAnchorId.FrontTop => "Front Top",
            DeploymentAnchorId.FrontCenter => "Front Center",
            DeploymentAnchorId.FrontBottom => "Front Bottom",
            DeploymentAnchorId.BackTop => "Back Top",
            DeploymentAnchorId.BackCenter => "Back Center",
            DeploymentAnchorId.BackBottom => "Back Bottom",
            _ => anchor.ToString(),
        };
    }
}
