namespace SM.Content.Definitions;

public enum SkillKindValue { Strike = 0, Heal = 1 }
public enum BattleActionTypeValue { BasicAttack = 0, ActiveSkill = 1, WaitDefend = 2 }
public enum TacticConditionTypeValue { SelfHpBelow = 0, AllyHpBelow = 1, EnemyInRange = 2, LowestHpEnemy = 3, EnemyExposed = 4, Fallback = 5 }
public enum TargetSelectorTypeValue { Self = 0, LowestHpAlly = 1, FirstEnemyInRange = 2, LowestHpEnemy = 3, NearestEnemy = 4, MostExposedEnemy = 5 }
public enum TeamPostureTypeValue { HoldLine = 0, StandardAdvance = 1, ProtectCarry = 2, CollapseWeakSide = 3, AllInBackline = 4 }
public enum DeploymentAnchorValue { FrontTop = 0, FrontCenter = 1, FrontBottom = 2, BackTop = 3, BackCenter = 4, BackBottom = 5 }
