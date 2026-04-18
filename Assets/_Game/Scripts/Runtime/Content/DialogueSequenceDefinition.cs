using System;
using UnityEngine;

namespace SM.Content
{

    [CreateAssetMenu(menuName = "SM/Definitions/Narrative/Dialogue Sequence", fileName = "dialogue_sequence_")]
    public sealed class DialogueSequenceDefinition : ScriptableObject
    {
        [SerializeField] private string _id = string.Empty;
        [SerializeField] private DialogueLineDefinition[] _lines = Array.Empty<DialogueLineDefinition>();

        public string Id => _id;
        public DialogueLineDefinition[] Lines => _lines;
    }
}
