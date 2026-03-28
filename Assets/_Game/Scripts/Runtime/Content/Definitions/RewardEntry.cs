using SM.Meta.Model;
using UnityEngine;

namespace SM.Content.Definitions;

[System.Serializable]
public sealed class RewardEntry
{
    public string Id = string.Empty;
    public RewardType RewardType = RewardType.Gold;
    public int Amount = 0;
    public string Label = string.Empty;
}
