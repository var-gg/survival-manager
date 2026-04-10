using UnityEngine;

namespace SM.Content;

[CreateAssetMenu(menuName = "SM/Definitions/Narrative/Dialogue Line", fileName = "dialogue_line_")]
public sealed class DialogueLineDefinition : ScriptableObject
{
    [SerializeField] private string _id = string.Empty;
    [SerializeField] private string _speakerId = string.Empty;
    [SerializeField] private string _textKey = string.Empty;
    [SerializeField] private string _emote = string.Empty;
    [SerializeField] private float _autoAdvanceHint;

    public string Id => _id;
    public string SpeakerId => _speakerId;
    public string TextKey => _textKey;
    public string Emote => _emote;
    public float AutoAdvanceHint => _autoAdvanceHint;
}
