using System;
using System.Collections.Generic;
using UnityEngine;

namespace SM.Unity.Narrative;

/// <summary>
/// 캐릭터 포트레잇 sprite를 Resources에서 해석한다.
/// 자산 구조 SoT: pindoc://analysis-character-asset-matrix-dawn-priest
/// 경로: Resources/_Game/Art/Characters/{characterId}/
/// - VN 좌우 배치 화자: portrait_bust_{emote}_{R|L} (asset-matrix resolve_dialogue_bust)
/// - bust 미존재 / narrator: portrait_face_{emote} close-up fallback
/// - 둘 다 없으면 portrait_face_default
/// bust emote는 8종(default/smile/serious/shock/anger/sad/cry/quiet)이다. authoring이 그
/// 밖의 자유 감정어를 쓰면 <see cref="MapToBustEmote"/>로 가장 가까운 8종 표정에 매핑한다.
/// </summary>
public sealed class ResourcesStoryPortraitResolver : IStoryPortraitResolver
{
    private const string BasePath = "_Game/Art/Characters";
    private const string DefaultEmote = "default";

    // bust 표정은 8종뿐이라, dialogue authoring이 쓴 자유 감정어를 가장 가까운 표정으로 모은다.
    private static readonly Dictionary<string, string> BustEmoteAliases = new(StringComparer.Ordinal)
    {
        ["grim"] = "serious",
        ["solemn"] = "serious",
        ["resolute"] = "serious",
        ["dry"] = "serious",
        ["skeptical"] = "serious",
        ["wary"] = "serious",
        ["concerned"] = "serious",
        ["blunt"] = "serious",
        ["tense"] = "serious",
        ["uneasy"] = "serious",
        ["warning"] = "serious",
        ["measured"] = "serious",
        ["cold"] = "serious",
        ["dark"] = "serious",
        ["strained"] = "serious",
        ["bitter"] = "sad",
        ["weary"] = "sad",
        ["pained"] = "sad",
        ["hollow"] = "sad",
        ["disturbed"] = "sad",
        ["resigned"] = "sad",
        ["moved"] = "sad",
        ["flat"] = "quiet",
        ["calm"] = "quiet",
        ["reasonable"] = "quiet",
        ["distant"] = "quiet",
        ["patient"] = "quiet",
        ["empty"] = "quiet",
        ["careful"] = "quiet",
        ["gentle"] = "smile",
        ["sardonic"] = "smile",
        ["casual"] = "smile",
        ["forced-casual"] = "smile",
        ["curious"] = "smile",
        ["amused"] = "smile",
        ["defiant"] = "anger",
        ["furious"] = "anger",
        ["fierce"] = "anger",
        ["frustrated"] = "anger",
        ["breaking"] = "cry",
        ["broken"] = "cry",
        ["tearful"] = "cry",
        ["disoriented"] = "shock",
        ["dread"] = "shock",
        ["confused"] = "shock",
        ["realization"] = "shock",
        ["incredulous"] = "shock",
        ["surprise"] = "shock",
        ["surprised"] = "shock",
        ["horror"] = "shock",
        ["awe"] = "shock",
    };

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

            // 8종 밖 감정어는 가장 가까운 표정으로 매핑해 재시도한다.
            var bucketed = MapToBustEmote(emote);
            if (bucketed != emote
                && TryLoad(characterId, $"portrait_bust_{bucketed}_{direction}", out portrait))
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

    private static string MapToBustEmote(string emote)
    {
        return BustEmoteAliases.TryGetValue(emote, out var mapped) ? mapped : emote;
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
