using System;
using SM.Core;
using UnityEngine;

namespace SM.Content;

[CreateAssetMenu(menuName = "SM/Definitions/Narrative/Story Event", fileName = "story_event_")]
public sealed class StoryEventDefinition : ScriptableObject
{
    [SerializeField] private string _id = string.Empty;
    [SerializeField] private NarrativeMoment _moment;
    [SerializeField] private int _priority;
    [SerializeField] private StoryOncePolicy _oncePolicy = StoryOncePolicy.OncePerProfile;
    [SerializeField] private StoryConditionDefinition[] _conditions = Array.Empty<StoryConditionDefinition>();
    [SerializeField] private StoryEffectDefinition[] _effects = Array.Empty<StoryEffectDefinition>();
    [SerializeField] private string _presentationKey = string.Empty;

    public string Id => _id;
    public NarrativeMoment Moment => _moment;
    public int Priority => _priority;
    public StoryOncePolicy OncePolicy => _oncePolicy;
    public StoryConditionDefinition[] Conditions => _conditions;
    public StoryEffectDefinition[] Effects => _effects;
    public string PresentationKey => _presentationKey;
}
