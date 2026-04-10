using UnityEngine;

namespace SM.Unity.Narrative;

public interface IStoryPortraitResolver
{
    bool TryResolve(string characterId, string emoteId, out Sprite portrait);
}
