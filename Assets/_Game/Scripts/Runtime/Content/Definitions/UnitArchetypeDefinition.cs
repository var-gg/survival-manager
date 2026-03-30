using System.Collections.Generic;
using UnityEngine;

namespace SM.Content.Definitions;

[CreateAssetMenu(menuName = "SM/Definitions/Unit Archetype Definition", fileName = "archetype_")]
public sealed class UnitArchetypeDefinition : ScriptableObject
{
    public string Id = string.Empty;
    public string DisplayName = string.Empty;
    public RaceDefinition Race;
    public ClassDefinition Class;
    public TraitPoolDefinition TraitPool;
    public List<SkillDefinitionAsset> Skills = new();
    public List<TacticPresetEntry> TacticPreset = new();
    public DeploymentAnchorValue DefaultAnchor = DeploymentAnchorValue.FrontCenter;
    public TeamPostureTypeValue PreferredTeamPosture = TeamPostureTypeValue.StandardAdvance;
    public string RoleTag = "auto";
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
}
