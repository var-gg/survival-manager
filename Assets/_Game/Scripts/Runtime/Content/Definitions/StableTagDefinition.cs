using UnityEngine;

namespace SM.Content.Definitions;

[CreateAssetMenu(menuName = "SM/Definitions/Stable Tag Definition", fileName = "tag_")]
public sealed class StableTagDefinition : ScriptableObject
{
    public string Id = string.Empty;
    public string DisplayName = string.Empty;
}
