using SM.Core;
using UnityEngine;

namespace SM.Content;

[CreateAssetMenu(menuName = "SM/Definitions/Narrative/Story Effect", fileName = "story_effect_")]
public sealed class StoryEffectDefinition : ScriptableObject
{
    [SerializeField] private string _id = string.Empty;
    [SerializeField] private StoryEffectKind _kind;
    [SerializeField] private string _payload = string.Empty;

    public string Id => _id;
    public StoryEffectKind Kind => _kind;
    public string Payload => _payload;
}
