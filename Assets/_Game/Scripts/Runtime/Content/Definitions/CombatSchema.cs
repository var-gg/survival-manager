namespace SM.Content.Definitions;

public enum SkillKindValue { Strike = 0, Heal = 1, Shield = 2, Buff = 3, Debuff = 4, Utility = 5 }
public enum DamageTypeValue { Physical = 0, Magical = 1, Healing = 2, True = 3 }
public enum SkillDeliveryValue { Melee = 0, Ranged = 1, Projectile = 2, Nova = 3, Aura = 4, Trap = 5, Zone = 6 }
public enum SkillTargetRuleValue { NearestEnemy = 0, LowestHpEnemy = 1, MostExposedEnemy = 2, LowestHpAlly = 3, ProtectedAlly = 4, Self = 5, MarkedTarget = 6 }
public enum SkillSlotKindValue { CoreActive = 0, UtilityActive = 1, Passive = 2, Support = 3 }
public enum PassiveNodeKindValue { Small = 0, Notable = 1, Keystone = 2 }
public enum BattleActionTypeValue { BasicAttack = 0, ActiveSkill = 1, WaitDefend = 2 }
public enum TacticConditionTypeValue
{
    SelfHpBelow = 0,
    AllyHpBelow = 1,
    EnemyInRange = 2,
    LowestHpEnemy = 3,
    EnemyExposed = 4,
    Fallback = 5,
    CooldownReady = 6,
    HasBuff = 7,
    EnemyTagMatch = 8,
    AllyThreatened = 9,
    RangeBand = 10,
    SelfResource = 11,
    TargetHealthBand = 12,
}
public enum TargetSelectorTypeValue { Self = 0, LowestHpAlly = 1, FirstEnemyInRange = 2, LowestHpEnemy = 3, NearestEnemy = 4, MostExposedEnemy = 5 }
public enum TeamPostureTypeValue { HoldLine = 0, StandardAdvance = 1, ProtectCarry = 2, CollapseWeakSide = 3, AllInBackline = 4 }
public enum DeploymentAnchorValue { FrontTop = 0, FrontCenter = 1, FrontBottom = 2, BackTop = 3, BackCenter = 4, BackBottom = 5 }
public enum AugmentCategoryValue { Combat = 0, Synergy = 1, EconomyLoot = 2, RunUtility = 3 }
public enum AffixCategoryValue { OffenseFlat = 0, OffenseScaling = 1, DefenseFlat = 2, DefenseScaling = 3, Utility = 4, SynergyTagged = 5 }
public enum ItemRarityTierValue { Common = 0, Magic = 1, Rare = 2, Epic = 3, Legendary = 4 }
public enum ItemIdentityValue { Baseline = 0, Named = 1, Unique = 2 }
public enum ArchetypeScopeValue { Core = 0, Specialist = 1 }
