using UnityEngine;

namespace SM.Unity.Narrative;

public interface IStoryPortraitResolver
{
    /// <summary>
    /// 캐릭터 포트레잇 sprite를 해석한다. side는 VN 좌우 배치 화자의 bust 방향 결정에 쓰인다
    /// (Left/FarLeft → bust R, Right/FarRight → bust L, None → bust 미사용·face fallback).
    /// </summary>
    bool TryResolve(string characterId, string emoteId, StorySpeakerSide side, out Sprite portrait);
}
