using UnityEngine;

namespace SM.Content.Definitions;

[CreateAssetMenu(menuName = "SM/Definitions/Race Definition", fileName = "race_")]
public sealed class RaceDefinition : ScriptableObject
{
    public string Id = string.Empty;
    public string DisplayName = string.Empty;
    [TextArea] public string Description = string.Empty;
}
