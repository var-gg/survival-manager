using UnityEngine;

namespace SM.Unity.Narrative;

public sealed class ResourcesStoryPortraitResolver : IStoryPortraitResolver
{
    private const string BasePath = "Narrative/Portraits";
    private const string DefaultEmoteId = "Default";

    public bool TryResolve(string characterId, string emoteId, out Sprite portrait)
    {
        portrait = null!;

        if (string.IsNullOrWhiteSpace(characterId))
        {
            return false;
        }

        if (TryLoad(characterId, emoteId, out portrait))
        {
            return true;
        }

        return TryLoad(characterId, DefaultEmoteId, out portrait);
    }

    private static bool TryLoad(string characterId, string emoteId, out Sprite portrait)
    {
        portrait = null!;
        if (string.IsNullOrWhiteSpace(characterId) || string.IsNullOrWhiteSpace(emoteId))
        {
            return false;
        }

        portrait = Resources.Load<Sprite>($"{BasePath}/{characterId}/{emoteId}");
        return portrait != null;
    }
}
