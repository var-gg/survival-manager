using System;
using SM.Core;
using UnityEngine;

namespace SM.Content
{

    [Serializable]
    public sealed class StoryArchiveEntryDefinition : ScriptableObject
    {
        [SerializeField] private string _eventId = string.Empty;
        [SerializeField] private string _chapterId = string.Empty;
        [SerializeField] private string _siteId = string.Empty;
        [SerializeField] private string _presentationKey = string.Empty;
        [SerializeField] private StoryPresentationKind _presentationKind;
        [SerializeField] private NarrativeRuntimeContextKind _runtimeContext;
        [SerializeField] private StoryArchiveReplayPolicy _replayPolicy;
        [SerializeField] private string _labelTextKey = string.Empty;
        [SerializeField] private int _sourceOrder;

        public string EventId => _eventId;
        public string ChapterId => _chapterId;
        public string SiteId => _siteId;
        public string PresentationKey => _presentationKey;
        public StoryPresentationKind PresentationKind => _presentationKind;
        public NarrativeRuntimeContextKind RuntimeContext => _runtimeContext;
        public StoryArchiveReplayPolicy ReplayPolicy => _replayPolicy;
        public string LabelTextKey => _labelTextKey;
        public int SourceOrder => _sourceOrder;
    }
}
