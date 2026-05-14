using UnityEngine;

namespace SM.Unity.Narrative;

/// <summary>
/// 캐릭터 포트레잇 sprite를 Resources에서 해석한다.
/// 자산 구조 SoT: pindoc://analysis-character-asset-matrix-dawn-priest
/// 경로: Resources/_Game/Art/Characters/{characterId}/
/// - VN 좌우 배치 화자: portrait_bust_{emote}_{R|L} (asset-matrix resolve_dialogue_bust)
/// - bust 미존재 / narrator: portrait_face_{emote} close-up fallback
/// - 둘 다 없으면 portrait_face_default
/// emote는 8종 enum(default/smile/serious/shock/anger/sad/cry/quiet) — authoring-map 정합.
/// </summary>
public sealed class ResourcesStoryPortraitResolver : IStoryPortraitResolver
{
    private const string BasePath = "_Game/Art/Characters";
    private const string DefaultEmote = "default";

    public bool TryResolve(string characterId, string emoteId, StorySpeakerSide side, out Sprite portrait)
    {
        portrait = null!;

        if (string.IsNullOrWhiteSpace(characterId))
        {
            return false;
        }

        var emote = string.IsNullOrWhiteSpace(emoteId)
            ? DefaultEmote
            : emoteId.Trim().ToLowerInvariant();
        var direction = ResolveDirection(side);

        // 1) VN bust — 좌우 배치 화자 (asset-matrix: bust R = 화면 왼쪽 위치, bust L = 오른쪽)
        if (direction != null)
        {
            if (TryLoad(characterId, $"portrait_bust_{emote}_{direction}", out portrait))
            {
                return true;
            }

            if (TryLoad(characterId, $"portrait_bust_{DefaultEmote}_{direction}", out portrait))
            {
                return true;
            }
        }

        // 2) face close-up fallback (narrator / bust 미존재 캐릭터)
        if (TryLoad(characterId, $"portrait_face_{emote}", out portrait))
        {
            return true;
        }

        return TryLoad(characterId, $"portrait_face_{DefaultEmote}", out portrait);
    }

    private static string? ResolveDirection(StorySpeakerSide side)
    {
        return side switch
        {
            StorySpeakerSide.Left or StorySpeakerSide.FarLeft => "R",
            StorySpeakerSide.Right or StorySpeakerSide.FarRight => "L",
            _ => null,
        };
    }

    private static bool TryLoad(string characterId, string fileStem, out Sprite portrait)
    {
        portrait = Resources.Load<Sprite>($"{BasePath}/{characterId}/{fileStem}");
        return portrait != null;
    }
}
