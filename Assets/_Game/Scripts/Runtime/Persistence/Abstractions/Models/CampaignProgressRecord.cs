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
    public bool StoryCleared;
    public bool EndlessUnlocked;
}
