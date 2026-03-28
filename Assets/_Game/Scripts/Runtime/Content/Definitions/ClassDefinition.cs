using UnityEngine;

namespace SM.Content.Definitions;

[CreateAssetMenu(menuName = "SM/Definitions/Class Definition", fileName = "class_")]
public sealed class ClassDefinition : ScriptableObject
{
    public string Id = string.Empty;
    public string DisplayName = string.Empty;
    [TextArea] public string Description = string.Empty;
}
