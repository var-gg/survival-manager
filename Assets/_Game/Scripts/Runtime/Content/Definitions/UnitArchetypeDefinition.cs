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
    public float BaseMaxHealth = 20f;
    public float BaseAttack = 5f;
    public float BaseDefense = 2f;
    public float BaseSpeed = 3f;
    public float BaseHealPower = 0f;
}
