using UnityEngine;

namespace SM.Content.Definitions;

[CreateAssetMenu(menuName = "SM/Definitions/Stat Definition", fileName = "stat_")]
public sealed class StatDefinition : ScriptableObject
{
    public string Id = string.Empty;
    public string DisplayName = string.Empty;
    [TextArea] public string Description = string.Empty;
}
