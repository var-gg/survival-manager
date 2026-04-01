using System;
using SM.Core.Ids;

namespace SM.Core.Contracts;

public enum CombatEntityKind
{
    RosterUnit = 0,
    OwnedSummon = 1,
    Deployable = 2,
    Projectile = 3,
}

public enum SummonBehaviorKind
{
    Companion = 0,
    Minion = 1,
    Turret = 2,
    Trap = 3,
    Shade = 4,
}

[Flags]
public enum CombatantEligibilityFlags
{
    None = 0,
    CountsForRosterLimit = 1 << 0,
    CountsForSynergyThresholds = 1 << 1,
    EligibleForDirectAllyTargeting = 1 << 2,
    EligibleForTeamAuras = 1 << 3,
    OccupiesFootprintAndSlots = 1 << 4,
    BlocksPathing = 1 << 5,
}

[Flags]
public enum CombatCreditFlags
{
    None = 0,
    EligibleForScore = 1 << 0,
    EligibleForLoot = 1 << 1,
    EligibleForMirroredOwnerKill = 1 << 2,
    EligibleForMirroredOwnerAssist = 1 << 3,
    EligibleForOwnerOnKillTriggers = 1 << 4,
    EligibleForOwnerEnergyGain = 1 << 5,
}

[Serializable]
public struct OwnershipLink
{
    public EntityId OwnerEntity;
    public EntityId SourceEntity;
    public string SourceDefinitionId;
    public int SummonGeneration;
}

[Serializable]
public struct StatInheritanceProfile
{
    public float OffenseBonusScalar;
    public float DefenseBonusScalar;
    public float UtilityBonusScalar;
    public bool InheritCritChance;
    public bool InheritDodge;
    public bool InheritBlock;

    public static StatInheritanceProfile DefaultOwnedSummon => new()
    {
        OffenseBonusScalar = 0.50f,
        DefenseBonusScalar = 0.35f,
        UtilityBonusScalar = 0.25f,
        InheritCritChance = false,
        InheritDodge = false,
        InheritBlock = false,
    };
}

[Serializable]
public sealed class SummonProfile
{
    public CombatEntityKind EntityKind = CombatEntityKind.OwnedSummon;
    public SummonBehaviorKind BehaviorKind = SummonBehaviorKind.Minion;
    public CombatantEligibilityFlags Eligibility =
        CombatantEligibilityFlags.EligibleForTeamAuras |
        CombatantEligibilityFlags.OccupiesFootprintAndSlots;
    public CombatCreditFlags CreditPolicy =
        CombatCreditFlags.EligibleForScore |
        CombatCreditFlags.EligibleForLoot |
        CombatCreditFlags.EligibleForMirroredOwnerKill |
        CombatCreditFlags.EligibleForMirroredOwnerAssist;
    public int MaxConcurrentPerSource = 2;
    public int MaxConcurrentPerOwner = 4;
    public bool DespawnOnOwnerDeath = true;
    public float OwnerDeathDespawnDelaySeconds = 1.0f;
    public bool InheritOwnerTarget = true;
    public bool IsPersistent = true;
    public StatInheritanceProfile Inheritance = StatInheritanceProfile.DefaultOwnedSummon;
}

[Serializable]
public sealed class KillEventPayload
{
    public EntityId ActualKiller;
    public EntityId ActualVictim;
    public EntityId MirroredOwner;
    public bool IsMirroredFromOwnedSummon;
    public bool GrantsOwnerEnergy;
    public bool GrantsOwnerOnKillTriggers;
}
