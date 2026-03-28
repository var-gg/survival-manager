namespace SM.Combat.Model;

public enum TeamSide { Ally = 0, Enemy = 1 }
public enum RowPosition { Front = 0, Back = 1 }
public enum BattleActionType { BasicAttack = 0, ActiveSkill = 1, WaitDefend = 2 }
public enum TacticConditionType { SelfHpBelow = 0, AllyHpBelow = 1, EnemyInRange = 2, LowestHpEnemy = 3, Fallback = 4 }
public enum TargetSelectorType { Self = 0, LowestHpAlly = 1, FirstEnemyInRange = 2, LowestHpEnemy = 3 }
public enum SkillKind { Strike = 0, Heal = 1 }
