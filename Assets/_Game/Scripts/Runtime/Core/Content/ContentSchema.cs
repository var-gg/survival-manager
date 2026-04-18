using System;
using System.Collections.Generic;
using SM.Core.Contracts;

namespace SM.Core.Content;

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
public enum BodySizeCategoryValue { Small = 0, Medium = 1, Large = 2 }
public enum MobilityStyleValue { None = 0, Dash = 1, Roll = 2, Blink = 3 }
public enum MobilityPurposeValue { None = 0, Engage = 1, Disengage = 2, Evade = 3, Chase = 4, MaintainRange = 5 }
public enum AugmentCategoryValue { Combat = 0, Synergy = 1, EconomyLoot = 2, RunUtility = 3 }
public enum AffixCategoryValue { OffenseFlat = 0, OffenseScaling = 1, DefenseFlat = 2, DefenseScaling = 3, Utility = 4, SynergyTagged = 5 }
public enum ItemRarityTierValue { Common = 0, Magic = 1, Rare = 2, Epic = 3, Legendary = 4 }
public enum ItemIdentityValue { Baseline = 0, Named = 1, Unique = 2 }
public enum ArchetypeScopeValue { Core = 0, Specialist = 1 }

public enum RewardType
{
    Gold = 0,
    TemporaryAugment = 1,
    Echo = 2,
    TraitRerollCurrency = Echo,
    Item = 3,
    // Deprecated in the normal playable lane. Kept for legacy content compatibility only.
    PermanentAugmentSlot = 4,
    TraitLockToken = 5,
    TraitPurgeToken = 6,
    EmberDust = 7,
    EchoCrystal = 8,
    BossSigil = 9,
    SkillManual = 10,
    SkillShard = 11,
}

public enum RarityBracketValue
{
    Common = 0,
    Advanced = 1,
    Elite = 2,
    Boss = 3,
}

public enum RewardSourceKindValue
{
    Skirmish = 0,
    Elite = 1,
    Boss = 2,
    ShrineEvent = 3,
    ExtractEndRun = 4,
    SalvageDismantle = 5,
}

public enum EncounterKindValue
{
    Skirmish = 0,
    Elite = 1,
    Boss = 2,
}

public enum ThreatTierValue
{
    Tier1 = 1,
    Tier2 = 2,
    Tier3 = 3,
}

public enum EnemySquadMemberRoleValue
{
    Unit = 0,
    Captain = 1,
    Escort = 2,
}

public enum BossPhaseTriggerValue
{
    None = 0,
    HealthBelowHalf = 1,
    HealthBelowQuarter = 2,
    EscortDefeated = 3,
}

public enum StatusGroupValue
{
    Control = 0,
    Attrition = 1,
    TacticalMark = 2,
    DefensiveBoon = 3,
}

public enum BudgetDomain
{
    UnitBlueprint = 0,
    Skill = 1,
    Passive = 2,
    Mobility = 3,
    Affix = 4,
    SynergyBreakpoint = 5,
    Augment = 6,
    Status = 7,
}

public enum ContentRarity
{
    Common = 0,
    Rare = 1,
    Epic = 2,
}

public enum PowerBand
{
    Micro = 0,
    Minor = 1,
    Standard = 2,
    Major = 3,
    Signature = 4,
    Keystone = 5,
}

public enum BudgetAxis
{
    SustainedDamage = 0,
    BurstDamage = 1,
    Durability = 2,
    Control = 3,
    Mobility = 4,
    Support = 5,
    CounterCoverage = 6,
    Reliability = 7,
    Economy = 8,
    DrawbackCredit = 9,
}

public enum CombatRoleBudgetProfile
{
    None = -1,
    Vanguard = 0,
    Bruiser = 1,
    Duelist = 2,
    Ranger = 3,
    Arcanist = 4,
    Support = 5,
    Summoner = 6,
}

public enum ThreatPattern
{
    ArmorFrontline = 0,
    ResistanceShell = 1,
    GuardBulwark = 2,
    EvasiveSkirmish = 3,
    ControlChain = 4,
    SustainBall = 5,
    DiveBackline = 6,
    SwarmFlood = 7,
}

public enum CounterTool
{
    ArmorShred = 0,
    Exposure = 1,
    GuardBreakMultiHit = 2,
    TrackingArea = 3,
    TenacityStability = 4,
    AntiHealShatter = 5,
    InterceptPeel = 6,
    CleaveWaveclear = 7,
}

public enum CounterCoverageStrength
{
    Light = 1,
    Standard = 2,
    Strong = 3,
}

public enum CounterCoverageLevel
{
    None = 0,
    Light = 1,
    Standard = 2,
    Strong = 3,
}

[Flags]
public enum ContentFeatureFlag
{
    None = 0,
    TrueDamage = 1 << 0,
    AccuracyRoll = 1 << 1,
    ReviveOrDeathDeny = 1 << 2,
    MindControl = 1 << 3,
    LongUntargetableOrStealth = 1 << 4,
    ResourceBurnOrSteal = 1 << 5,
    ExtraActionOrCooldownReset = 1 << 6,
    PermanentUncappedStacking = 1 << 7,
    StatOrSkillTheft = 1 << 8,
    ReflectLoop = 1 << 9,
    FriendlyFire = 1 << 10,
    TerrainOrProjectileCollision = 1 << 11,
    SpawnChain = 1 << 12,
    GlobalAuraSameKeyStack = 1 << 13,
    NonAugmentEconomyOrOfferChange = 1 << 14,
    LoadoutTopologyMutation = 1 << 15,
    CrossRunPowerGrant = 1 << 16,
    LegendaryOrUniqueException = 1 << 17,
}

[Serializable]
public sealed class BudgetVector
{
    public int SustainedDamage;
    public int BurstDamage;
    public int Durability;
    public int Control;
    public int Mobility;
    public int Support;
    public int CounterCoverage;
    public int Reliability;
    public int Economy;
    public int DrawbackCredit;

    public int PositiveTotal =>
        SustainedDamage +
        BurstDamage +
        Durability +
        Control +
        Mobility +
        Support +
        CounterCoverage +
        Reliability +
        Economy;

    public int FinalScore => PositiveTotal - DrawbackCredit;

    public int PositiveAxisCount
    {
        get
        {
            var count = 0;
            count += SustainedDamage > 0 ? 1 : 0;
            count += BurstDamage > 0 ? 1 : 0;
            count += Durability > 0 ? 1 : 0;
            count += Control > 0 ? 1 : 0;
            count += Mobility > 0 ? 1 : 0;
            count += Support > 0 ? 1 : 0;
            count += CounterCoverage > 0 ? 1 : 0;
            count += Reliability > 0 ? 1 : 0;
            count += Economy > 0 ? 1 : 0;
            return count;
        }
    }

    public int GetAxisValue(BudgetAxis axis)
    {
        return axis switch
        {
            BudgetAxis.SustainedDamage => SustainedDamage,
            BudgetAxis.BurstDamage => BurstDamage,
            BudgetAxis.Durability => Durability,
            BudgetAxis.Control => Control,
            BudgetAxis.Mobility => Mobility,
            BudgetAxis.Support => Support,
            BudgetAxis.CounterCoverage => CounterCoverage,
            BudgetAxis.Reliability => Reliability,
            BudgetAxis.Economy => Economy,
            BudgetAxis.DrawbackCredit => DrawbackCredit,
            _ => 0,
        };
    }

    public IReadOnlyList<KeyValuePair<BudgetAxis, int>> EnumeratePositiveAxes()
    {
        var values = new List<KeyValuePair<BudgetAxis, int>>(9);
        AddPositive(values, BudgetAxis.SustainedDamage, SustainedDamage);
        AddPositive(values, BudgetAxis.BurstDamage, BurstDamage);
        AddPositive(values, BudgetAxis.Durability, Durability);
        AddPositive(values, BudgetAxis.Control, Control);
        AddPositive(values, BudgetAxis.Mobility, Mobility);
        AddPositive(values, BudgetAxis.Support, Support);
        AddPositive(values, BudgetAxis.CounterCoverage, CounterCoverage);
        AddPositive(values, BudgetAxis.Reliability, Reliability);
        AddPositive(values, BudgetAxis.Economy, Economy);
        return values;
    }

    private static void AddPositive(ICollection<KeyValuePair<BudgetAxis, int>> values, BudgetAxis axis, int amount)
    {
        if (amount > 0)
        {
            values.Add(new KeyValuePair<BudgetAxis, int>(axis, amount));
        }
    }
}

[Serializable]
public sealed class CounterToolContribution
{
    public CounterTool Tool;
    public CounterCoverageStrength Strength = CounterCoverageStrength.Light;
}

[Serializable]
public sealed class BudgetCard
{
    public BudgetDomain Domain = BudgetDomain.UnitBlueprint;
    public ContentRarity Rarity = ContentRarity.Common;
    public PowerBand PowerBand = PowerBand.Standard;
    public CombatRoleBudgetProfile RoleProfile = CombatRoleBudgetProfile.None;
    public BudgetVector Vector = new();
    public int KeywordCount;
    public int ConditionClauseCount;
    public int RuleExceptionCount;
    public ThreatPattern[] DeclaredThreatPatterns = Array.Empty<ThreatPattern>();
    public CounterToolContribution[] DeclaredCounterTools = Array.Empty<CounterToolContribution>();
    public ContentFeatureFlag DeclaredFeatureFlags = ContentFeatureFlag.None;
}

[Serializable]
public sealed class TeamCounterCoverageReport
{
    public CounterCoverageLevel ArmorShred = CounterCoverageLevel.None;
    public CounterCoverageLevel Exposure = CounterCoverageLevel.None;
    public CounterCoverageLevel GuardBreakMultiHit = CounterCoverageLevel.None;
    public CounterCoverageLevel TrackingArea = CounterCoverageLevel.None;
    public CounterCoverageLevel TenacityStability = CounterCoverageLevel.None;
    public CounterCoverageLevel AntiHealShatter = CounterCoverageLevel.None;
    public CounterCoverageLevel InterceptPeel = CounterCoverageLevel.None;
    public CounterCoverageLevel CleaveWaveclear = CounterCoverageLevel.None;

    public CounterCoverageLevel GetLevel(CounterTool tool)
    {
        return tool switch
        {
            CounterTool.ArmorShred => ArmorShred,
            CounterTool.Exposure => Exposure,
            CounterTool.GuardBreakMultiHit => GuardBreakMultiHit,
            CounterTool.TrackingArea => TrackingArea,
            CounterTool.TenacityStability => TenacityStability,
            CounterTool.AntiHealShatter => AntiHealShatter,
            CounterTool.InterceptPeel => InterceptPeel,
            CounterTool.CleaveWaveclear => CleaveWaveclear,
            _ => CounterCoverageLevel.None,
        };
    }

    public void SetLevel(CounterTool tool, CounterCoverageLevel level)
    {
        switch (tool)
        {
            case CounterTool.ArmorShred:
                ArmorShred = level;
                break;
            case CounterTool.Exposure:
                Exposure = level;
                break;
            case CounterTool.GuardBreakMultiHit:
                GuardBreakMultiHit = level;
                break;
            case CounterTool.TrackingArea:
                TrackingArea = level;
                break;
            case CounterTool.TenacityStability:
                TenacityStability = level;
                break;
            case CounterTool.AntiHealShatter:
                AntiHealShatter = level;
                break;
            case CounterTool.InterceptPeel:
                InterceptPeel = level;
                break;
            case CounterTool.CleaveWaveclear:
                CleaveWaveclear = level;
                break;
        }
    }
}

[Serializable]
public sealed class CounterTopologyRule
{
    public ThreatPattern Threat;
    public CounterTool PrimaryAnswer;
}

[Serializable]
public sealed class V1FeaturePolicy
{
    public ContentFeatureFlag ForbiddenFlags;
}

public sealed record BudgetTarget(int Target, int Tolerance);

public sealed record RarityComplexityBudget(int KeywordCount, int ConditionClauseCount, int RuleExceptionCount, int MaxThreatDeclarations, int MaxCounterDeclarations);

public sealed record RoleAxisProfile(IReadOnlyList<BudgetAxis> PrimaryAxes, BudgetAxis SecondaryAxis);

public static class LoopCContentGovernance
{
    public const int UnitDerivedDeltaThreshold = 8;
    public const int StandardDerivedDeltaThreshold = 3;
    public const int AugmentDerivedDeltaThreshold = 4;
    public static readonly int[] AllowedSynergyThresholds = { 2, 3, 4 };

    public static readonly IReadOnlyDictionary<PowerBand, BudgetTarget> PowerBandTargets = new Dictionary<PowerBand, BudgetTarget>
    {
        [PowerBand.Micro] = new(4, 1),
        [PowerBand.Minor] = new(8, 1),
        [PowerBand.Standard] = new(12, 2),
        [PowerBand.Major] = new(18, 2),
        [PowerBand.Signature] = new(26, 3),
        [PowerBand.Keystone] = new(34, 3),
    };

    public static readonly IReadOnlyDictionary<ContentRarity, BudgetTarget> UnitBudgetTargets = new Dictionary<ContentRarity, BudgetTarget>
    {
        [ContentRarity.Common] = new(100, 4),
        [ContentRarity.Rare] = new(120, 4),
        [ContentRarity.Epic] = new(140, 4),
    };

    public static readonly IReadOnlyDictionary<ContentRarity, BudgetTarget> AffixBudgetTargets = new Dictionary<ContentRarity, BudgetTarget>
    {
        [ContentRarity.Common] = new(6, 1),
        [ContentRarity.Rare] = new(10, 1),
        [ContentRarity.Epic] = new(14, 2),
    };

    public static readonly IReadOnlyDictionary<ContentRarity, BudgetTarget> AugmentBudgetTargets = new Dictionary<ContentRarity, BudgetTarget>
    {
        [ContentRarity.Common] = new(18, 2),
        [ContentRarity.Rare] = new(26, 3),
        [ContentRarity.Epic] = new(34, 3),
    };

    public static readonly IReadOnlyDictionary<ContentRarity, RarityComplexityBudget> RarityComplexityCaps = new Dictionary<ContentRarity, RarityComplexityBudget>
    {
        [ContentRarity.Common] = new(2, 1, 0, 1, 1),
        [ContentRarity.Rare] = new(3, 2, 1, 1, 2),
        [ContentRarity.Epic] = new(4, 2, 1, 2, 2),
    };

    public static readonly IReadOnlyDictionary<CombatRoleBudgetProfile, RoleAxisProfile> RoleProfiles = new Dictionary<CombatRoleBudgetProfile, RoleAxisProfile>
    {
        [CombatRoleBudgetProfile.Vanguard] = new(new[] { BudgetAxis.Durability, BudgetAxis.Control }, BudgetAxis.CounterCoverage),
        [CombatRoleBudgetProfile.Bruiser] = new(new[] { BudgetAxis.SustainedDamage, BudgetAxis.Durability }, BudgetAxis.Mobility),
        [CombatRoleBudgetProfile.Duelist] = new(new[] { BudgetAxis.BurstDamage, BudgetAxis.Mobility }, BudgetAxis.Reliability),
        [CombatRoleBudgetProfile.Ranger] = new(new[] { BudgetAxis.SustainedDamage, BudgetAxis.Reliability }, BudgetAxis.Mobility),
        [CombatRoleBudgetProfile.Arcanist] = new(new[] { BudgetAxis.BurstDamage, BudgetAxis.Control }, BudgetAxis.Reliability),
        [CombatRoleBudgetProfile.Support] = new(new[] { BudgetAxis.Support, BudgetAxis.Reliability }, BudgetAxis.Durability),
        [CombatRoleBudgetProfile.Summoner] = new(new[] { BudgetAxis.SustainedDamage, BudgetAxis.Support }, BudgetAxis.CounterCoverage),
    };

    public static readonly IReadOnlyDictionary<ThreatPattern, CounterTool> ThreatAnswers = new Dictionary<ThreatPattern, CounterTool>
    {
        [ThreatPattern.ArmorFrontline] = CounterTool.ArmorShred,
        [ThreatPattern.ResistanceShell] = CounterTool.Exposure,
        [ThreatPattern.GuardBulwark] = CounterTool.GuardBreakMultiHit,
        [ThreatPattern.EvasiveSkirmish] = CounterTool.TrackingArea,
        [ThreatPattern.ControlChain] = CounterTool.TenacityStability,
        [ThreatPattern.SustainBall] = CounterTool.AntiHealShatter,
        [ThreatPattern.DiveBackline] = CounterTool.InterceptPeel,
        [ThreatPattern.SwarmFlood] = CounterTool.CleaveWaveclear,
    };

    public static readonly V1FeaturePolicy DefaultFeaturePolicy = new()
    {
        ForbiddenFlags =
            ContentFeatureFlag.TrueDamage |
            ContentFeatureFlag.AccuracyRoll |
            ContentFeatureFlag.ReviveOrDeathDeny |
            ContentFeatureFlag.MindControl |
            ContentFeatureFlag.LongUntargetableOrStealth |
            ContentFeatureFlag.ResourceBurnOrSteal |
            ContentFeatureFlag.ExtraActionOrCooldownReset |
            ContentFeatureFlag.PermanentUncappedStacking |
            ContentFeatureFlag.StatOrSkillTheft |
            ContentFeatureFlag.ReflectLoop |
            ContentFeatureFlag.FriendlyFire |
            ContentFeatureFlag.TerrainOrProjectileCollision |
            ContentFeatureFlag.SpawnChain |
            ContentFeatureFlag.GlobalAuraSameKeyStack |
            ContentFeatureFlag.NonAugmentEconomyOrOfferChange |
            ContentFeatureFlag.LoadoutTopologyMutation |
            ContentFeatureFlag.CrossRunPowerGrant |
            ContentFeatureFlag.LegendaryOrUniqueException,
    };

    public static ContentRarity FromRecruitTier(RecruitTier tier)
    {
        return tier switch
        {
            RecruitTier.Rare => ContentRarity.Rare,
            RecruitTier.Epic => ContentRarity.Epic,
            _ => ContentRarity.Common,
        };
    }

    public static RecruitTier ToRecruitTier(ContentRarity rarity)
    {
        return rarity switch
        {
            ContentRarity.Rare => RecruitTier.Rare,
            ContentRarity.Epic => RecruitTier.Epic,
            _ => RecruitTier.Common,
        };
    }

    public static int GetDerivedDeltaThreshold(BudgetDomain domain)
    {
        return domain switch
        {
            BudgetDomain.UnitBlueprint => UnitDerivedDeltaThreshold,
            BudgetDomain.Augment => AugmentDerivedDeltaThreshold,
            _ => StandardDerivedDeltaThreshold,
        };
    }

    public static CounterCoverageLevel ToCoverageLevel(int strengthScore)
    {
        if (strengthScore >= 3)
        {
            return CounterCoverageLevel.Strong;
        }

        if (strengthScore >= 2)
        {
            return CounterCoverageLevel.Standard;
        }

        return strengthScore >= 1 ? CounterCoverageLevel.Light : CounterCoverageLevel.None;
    }
}
