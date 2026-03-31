using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace SM.Content.Definitions;

[CreateAssetMenu(menuName = "SM/Definitions/Cleanse Profile Definition", fileName = "cleanse_profile_")]
public sealed class CleanseProfileDefinition : ScriptableObject
{
    public string Id = string.Empty;
    public string NameKey = string.Empty;
    public string DescriptionKey = string.Empty;
    public List<string> RemovesStatusIds = new();
    public bool RemovesOneHardControl;
    public bool GrantsUnstoppable;
    public float GrantedUnstoppableDurationSeconds = 0f;

    [FormerlySerializedAs("DisplayName")]
    [SerializeField, HideInInspector] private string legacyDisplayName = string.Empty;

    [FormerlySerializedAs("Description")]
    [SerializeField, HideInInspector, TextArea] private string legacyDescription = string.Empty;

    public string LegacyDisplayName => legacyDisplayName;
    public string LegacyDescription => legacyDescription;
}
