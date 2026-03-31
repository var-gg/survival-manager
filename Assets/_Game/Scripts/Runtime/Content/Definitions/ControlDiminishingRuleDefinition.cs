using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace SM.Content.Definitions;

[CreateAssetMenu(menuName = "SM/Definitions/Control Diminishing Rule Definition", fileName = "control_diminishing_")]
public sealed class ControlDiminishingRuleDefinition : ScriptableObject
{
    public string Id = string.Empty;
    public string NameKey = string.Empty;
    public string DescriptionKey = string.Empty;
    public float ControlResistMultiplier = 0.5f;
    public float WindowSeconds = 1.5f;
    public List<string> FullTenacityStatusIds = new();
    public List<string> PartialTenacityStatusIds = new();

    [FormerlySerializedAs("DisplayName")]
    [SerializeField, HideInInspector] private string legacyDisplayName = string.Empty;

    [FormerlySerializedAs("Description")]
    [SerializeField, HideInInspector, TextArea] private string legacyDescription = string.Empty;

    public string LegacyDisplayName => legacyDisplayName;
    public string LegacyDescription => legacyDescription;
}
