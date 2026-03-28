using System.Collections.Generic;
using UnityEngine;

namespace SM.Content.Definitions;

[CreateAssetMenu(menuName = "SM/Definitions/Reward Table Definition", fileName = "rewardtable_")]
public sealed class RewardTableDefinition : ScriptableObject
{
    public string Id = string.Empty;
    public string DisplayName = string.Empty;
    public List<RewardEntry> Rewards = new();
}
