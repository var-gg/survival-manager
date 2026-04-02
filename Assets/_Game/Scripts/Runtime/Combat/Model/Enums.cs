namespace SM.Combat.Model;

public enum TeamSide { Ally = 0, Enemy = 1 }
public enum DeploymentAnchorId { FrontTop = 0, FrontCenter = 1, FrontBottom = 2, BackTop = 3, BackCenter = 4, BackBottom = 5 }
public enum BattleActionType { BasicAttack = 0, ActiveSkill = 1, WaitDefend = 2 }
public enum BattleLogCode { Generic = 0, BasicAttackDamage = 1, ActiveSkillDamage = 2, ActiveSkillHeal = 3, WaitDefend = 4 }
public enum TacticConditionType { SelfHpBelow = 0, AllyHpBelow = 1, EnemyInRange = 2, LowestHpEnemy = 3, EnemyExposed = 4, Fallback = 5 }
public enum TargetSelectorType { Self = 0, LowestHpAlly = 1, FirstEnemyInRange = 2, LowestHpEnemy = 3, NearestEnemy = 4, MostExposedEnemy = 5 }
public enum SkillKind { Strike = 0, Heal = 1, Shield = 2, Buff = 3, Debuff = 4, Utility = 5 }
public enum SkillDelivery { Melee = 0, Ranged = 1, Projectile = 2, Nova = 3, Aura = 4, Trap = 5, Zone = 6 }
public enum SkillTargetRule { NearestEnemy = 0, LowestHpEnemy = 1, MostExposedEnemy = 2, LowestHpAlly = 3, ProtectedAlly = 4, Self = 5, MarkedTarget = 6 }
public enum CombatActionState
{
    Spawn = 0,
    AdvanceToAnchor = 1,
    AcquireTarget = 2,
    Approach = 3,
    ExecuteAction = 4,
    Recover = 5,
    Reposition = 6,
    BreakContact = 7,
    Dead = 8,
    SecurePosition = 9,
    SeekTarget = AcquireTarget,
    MoveToEngage = Approach,
    Windup = ExecuteAction,
    Recovery = Recover,
    Retreat = BreakContact,
}
public enum TeamPostureType { HoldLine = 0, StandardAdvance = 1, ProtectCarry = 2, CollapseWeakSide = 3, AllInBackline = 4 }

public enum RoleVariantTag
{
    Unassigned = 0,
    Anchor = 1,
    Peeler = 2,
    Diver = 3,
    Executioner = 4,
    Harrier = 5,
    Sniper = 6,
    Battery = 7,
    Controller = 8,
}

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

    public static string ToLocalizationKey(this DeploymentAnchorId anchor)
    {
        return anchor switch
        {
            DeploymentAnchorId.FrontTop => "ui.common.anchor.front_top",
            DeploymentAnchorId.FrontCenter => "ui.common.anchor.front_center",
            DeploymentAnchorId.FrontBottom => "ui.common.anchor.front_bottom",
            DeploymentAnchorId.BackTop => "ui.common.anchor.back_top",
            DeploymentAnchorId.BackCenter => "ui.common.anchor.back_center",
            DeploymentAnchorId.BackBottom => "ui.common.anchor.back_bottom",
            _ => "ui.common.anchor.unknown",
        };
    }
}
