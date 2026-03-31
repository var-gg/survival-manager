using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace SM.Content.Definitions;

public enum StatusGroupValue
{
    Control = 0,
    Attrition = 1,
    TacticalMark = 2,
    DefensiveBoon = 3,
}

[System.Serializable]
public sealed class StatusApplicationRule
{
    public string Id = string.Empty;
    public string StatusId = string.Empty;
    public float DurationSeconds = 1f;
    public float Magnitude = 0f;
    public int MaxStacks = 1;
    public bool RefreshDurationOnReapply = true;
    public int StackCap = 0;
    public StatusStackPolicyValue StackPolicy = StatusStackPolicyValue.LegacyDerived;
    public StatusRefreshPolicyValue RefreshPolicy = StatusRefreshPolicyValue.LegacyDerived;
    public StatusProcAttributionPolicyValue ProcAttributionPolicy = StatusProcAttributionPolicyValue.LegacyDerived;
    public StatusOwnershipPolicyValue OwnershipPolicy = StatusOwnershipPolicyValue.LegacyDerived;
}

[CreateAssetMenu(menuName = "SM/Definitions/Status Family Definition", fileName = "status_family_")]
public sealed class StatusFamilyDefinition : ScriptableObject
{
    public string Id = string.Empty;
    public string NameKey = string.Empty;
    public string DescriptionKey = string.Empty;
    public StatusGroupValue Group = StatusGroupValue.Control;
    public bool IsHardControl;
    public bool UsesControlDiminishing;
    public bool AffectedByTenacity = true;
    public float TenacityScale = 1f;
    public bool IsRuleModifierOnly;
    public int DefaultStackCap = 1;
    public StatusStackPolicyValue DefaultStackPolicy = StatusStackPolicyValue.LegacyDerived;
    public StatusRefreshPolicyValue DefaultRefreshPolicy = StatusRefreshPolicyValue.LegacyDerived;
    public StatusProcAttributionPolicyValue DefaultProcAttributionPolicy = StatusProcAttributionPolicyValue.LegacyDerived;
    public StatusOwnershipPolicyValue DefaultOwnershipPolicy = StatusOwnershipPolicyValue.LegacyDerived;
    public bool IsAiRelevant = true;
    public int VisualPriority = 0;
    public List<string> CompileTags = new();

    [FormerlySerializedAs("DisplayName")]
    [SerializeField, HideInInspector] private string legacyDisplayName = string.Empty;

    [FormerlySerializedAs("Description")]
    [SerializeField, HideInInspector, TextArea] private string legacyDescription = string.Empty;

    public string LegacyDisplayName => legacyDisplayName;
    public string LegacyDescription => legacyDescription;
}
