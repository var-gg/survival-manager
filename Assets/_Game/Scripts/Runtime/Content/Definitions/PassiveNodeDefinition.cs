using System.Collections.Generic;
using SM.Core.Content;
using UnityEngine;
using UnityEngine.Serialization;

namespace SM.Content.Definitions;

[CreateAssetMenu(menuName = "SM/Definitions/Passive Node Definition", fileName = "passivenode_")]
public sealed class PassiveNodeDefinition : ScriptableObject, ISerializationCallbackReceiver
{
    public string Id = string.Empty;
    public string BoardId = string.Empty;
    public string NameKey = string.Empty;
    public string DescriptionKey = string.Empty;
    public PassiveNodeKindValue NodeKind = PassiveNodeKindValue.Small;
    public List<string> PrerequisiteNodeIds = new();
    public List<StableTagDefinition> MutualExclusionTags = new();
    public int BoardDepth;
    public List<StableTagDefinition> CompileTags = new();
    public List<StableTagDefinition> RuleModifierTags = new();
    public List<SerializableStatModifier> Modifiers = new();

    [FormerlySerializedAs("IsKeystone")]
    [SerializeField, HideInInspector] private bool legacyIsKeystone;

    [FormerlySerializedAs("DisplayName")]
    [SerializeField, HideInInspector] private string legacyDisplayName = string.Empty;

    [FormerlySerializedAs("Description")]
    [SerializeField, HideInInspector, TextArea] private string legacyDescription = string.Empty;

    public string LegacyDisplayName => legacyDisplayName;
    public string LegacyDescription => legacyDescription;

    public void OnBeforeSerialize()
    {
    }

    public void OnAfterDeserialize()
    {
        if (legacyIsKeystone && NodeKind == PassiveNodeKindValue.Small)
        {
            NodeKind = PassiveNodeKindValue.Keystone;
        }
    }
}
