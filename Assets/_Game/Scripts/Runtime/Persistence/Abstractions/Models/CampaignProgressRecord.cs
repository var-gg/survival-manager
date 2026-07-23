using System;
using System.Collections.Generic;

namespace SM.Persistence.Abstractions.Models;

[Serializable]
public sealed class CampaignProgressRecord
{
    public string SelectedChapterId = string.Empty;
    public string SelectedSiteId = string.Empty;
    public List<string> ClearedChapterIds = new();
    public List<string> ClearedSiteIds = new();
    // Additive save fields: legacy saves omit them and normalize to empty dictionaries (count = 0).
    public Dictionary<string, int> RewardedRevisitCountsByChapter = new();
    public Dictionary<string, int> DefeatConsolationCountsByChapter = new();
    public bool StoryCleared;
    public bool EndlessUnlocked;
}
