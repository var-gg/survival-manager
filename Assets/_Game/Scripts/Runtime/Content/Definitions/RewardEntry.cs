using UnityEngine;
using SM.Core.Content;
using UnityEngine.Serialization;

namespace SM.Content.Definitions;

[System.Serializable]
public sealed class RewardEntry
{
    public string Id = string.Empty;
    public RewardType RewardType = RewardType.Gold;
    public int Amount = 0;
    public string LabelKey = string.Empty;

    [FormerlySerializedAs("Label")]
    [SerializeField, HideInInspector] private string legacyLabel = string.Empty;

    public string LegacyLabel => legacyLabel;
}
