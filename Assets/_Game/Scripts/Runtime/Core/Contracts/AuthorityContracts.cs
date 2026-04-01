using System;

namespace SM.Core.Contracts;

public enum AuthorityLayer
{
    UnitKit = 0,
    Skill = 1,
    Affix = 2,
    Synergy = 3,
    Augment = 4,
    Status = 5,
}

public enum EffectScope
{
    Self = 0,
    CurrentTarget = 1,
    GroundArea = 2,
    AlliedRosterUnits = 3,
    AlliedCombatants = 4,
    EnemyCombatants = 5,
    OwnerOwnedSummons = 6,
    GlobalCombat = 7,
    RewardPhase = 8,
    ShopPhase = 9,
}

[Flags]
public enum EffectCapability
{
    None = 0,
    ModifyStats = 1 << 0,
    ApplyStatus = 1 << 1,
    DealDamage = 1 << 2,
    HealOrBarrier = 1 << 3,
    Reposition = 1 << 4,
    ModifyResource = 1 << 5,
    ModifyCooldown = 1 << 6,
    SpawnSummon = 1 << 7,
    SpawnDeployable = 1 << 8,
    GrantPassive = 1 << 9,
    GrantAction = 1 << 10,
    ModifyTargeting = 1 << 11,
    ModifyCompositionPayoff = 1 << 12,
    ModifyGlobalCombatRule = 1 << 13,
    ModifyEconomyRule = 1 << 14,
    ModifyOfferWeights = 1 << 15,
}

[Serializable]
public sealed class EffectDescriptor
{
    public AuthorityLayer Layer = AuthorityLayer.Skill;
    public EffectScope Scope = EffectScope.Self;
    public EffectCapability Capabilities = EffectCapability.None;
    public bool AllowMirroredOwnedSummonKill = false;
    public bool AllowsPersistentSummonChain = false;
    public int LoadoutTopologyDelta = 0;
}
