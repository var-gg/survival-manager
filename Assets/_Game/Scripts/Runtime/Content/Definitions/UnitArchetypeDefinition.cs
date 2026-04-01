using System.Collections.Generic;
using SM.Core.Contracts;
using UnityEngine;
using UnityEngine.Serialization;

namespace SM.Content.Definitions;

[CreateAssetMenu(menuName = "SM/Definitions/Unit Archetype Definition", fileName = "archetype_")]
public sealed class UnitArchetypeDefinition : ScriptableObject
{
    public string Id = string.Empty;
    public string NameKey = string.Empty;
    public ArchetypeScopeValue ScopeKind = ArchetypeScopeValue.Core;
    public RaceDefinition Race;
    public ClassDefinition Class;
    public TraitPoolDefinition TraitPool;
    public List<SkillDefinitionAsset> Skills = new();
    public List<TacticPresetEntry> TacticPreset = new();
    public DeploymentAnchorValue DefaultAnchor = DeploymentAnchorValue.FrontCenter;
    public TeamPostureTypeValue PreferredTeamPosture = TeamPostureTypeValue.StandardAdvance;
    public string RoleTag = "auto";
    public string RoleFamilyTag = string.Empty;
    public string PrimaryWeaponFamilyTag = string.Empty;
    public List<StableTagDefinition> SupportModifierBiasTags = new();
    public string LockedAttackProfileId = string.Empty;
    public string LockedAttackProfileTag = string.Empty;
    public SkillDefinitionAsset LockedSignatureActiveSkill;
    public SkillDefinitionAsset LockedSignaturePassiveSkill;
    public List<SkillDefinitionAsset> FlexUtilitySkillPool = new();
    public List<SkillDefinitionAsset> FlexSupportSkillPool = new();
    public UnitLoadoutDefinition Loadout = new();
    public FootprintProfileDefinition FootprintProfile;
    public BehaviorProfileDefinition BehaviorProfile;
    public MobilityProfileDefinition MobilityProfile;
    public float BaseMaxHealth = 20f;
    public float BaseArmor = 2f;
    public float BaseResist = 0f;
    public float BaseBarrierPower = 0f;
    public float BaseTenacity = 0f;
    public float BasePhysPower = 5f;
    public float BaseMagPower = 0f;
    public float BaseAttackSpeed = 3f;
    public float BaseAttack = 5f;
    public float BaseDefense = 2f;
    public float BaseSpeed = 3f;
    public float BaseHealPower = 0f;
    public float BaseMoveSpeed = 1.7f;
    public float BaseAttackRange = 1.5f;
    public float BaseMaxEnergy = 100f;
    public float BaseStartingEnergy = 10f;
    public float BaseSkillHaste = 0f;
    public float BaseManaMax = 0f;
    public float BaseManaGainOnAttack = 0f;
    public float BaseManaGainOnHit = 0f;
    public float BaseCooldownRecovery = 0f;
    public float BaseCritChance = 0f;
    public float BaseCritMultiplier = 0f;
    public float BasePhysPen = 0f;
    public float BaseMagPen = 0f;
    public float BaseAggroRadius = 7f;
    public float BasePreferredDistance = 0f;
    public float BaseProtectRadius = 0f;
    public float BaseAttackWindup = 0.22f;
    public float BaseCastWindup = 0.22f;
    public float BaseProjectileSpeed = 0f;
    public float BaseCollisionRadius = 0.5f;
    public float BaseRepositionCooldown = 0f;
    public float BaseAttackCooldown = 0.95f;
    public float BaseLeashDistance = 5f;
    public float BaseTargetSwitchDelay = 0.35f;

    [FormerlySerializedAs("DisplayName")]
    [SerializeField, HideInInspector] private string legacyDisplayName = string.Empty;

    public string LegacyDisplayName => legacyDisplayName;
}
