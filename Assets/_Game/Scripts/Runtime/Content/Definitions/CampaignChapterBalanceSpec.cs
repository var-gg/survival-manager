using System;
using UnityEngine;

namespace SM.Content.Definitions
{
    /// <summary>챕터별 적 stat envelope 저작값. runtime 상태나 전투 BaseStats를 소유하지 않는다.</summary>
    [Serializable]
    public sealed class CampaignChapterBalanceSpec
    {
        [Min(1f)] public float HpEnvelope = 1f;
        [Min(1f)] public float AtkEnvelope = 1f;
        [Min(0f)] public float SiteHpStep = 0.01f;
        [Min(0f)] public float SiteAtkStep = 0.005f;
    }
}
