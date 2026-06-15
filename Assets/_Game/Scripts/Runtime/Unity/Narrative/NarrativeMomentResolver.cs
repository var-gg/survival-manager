using System;
using SM.Meta;

namespace SM.Unity.Narrative;

/// <summary>
/// 원정 종료 시점에 어떤 narrative moment를 발화할지를 세션 상태만으로 결정(순수, UnityEngine 0).
/// nav seam의 ExpeditionFlowResolver와 동형 — controller 인라인 분기를 순수 결정으로 codify한다.
///
/// 현 흐름의 빈 구멍 = ExtractCommitted: 정식 extract 노드 정산 후 이 moment를 발화하는 호출처가 없어
/// 캠페인 엔딩(story_event_campaign_complete, moment=ExtractCommitted)이 영원히 안 켜진다.
/// 이 resolver는 "방금 정산한 노드가 extract인가"와 "엔딩 moment에 실을 chapter/site 컨텍스트"를 한곳에 모은다.
/// (프로덕션 발화 배선은 PlayMode 검증이 필요한 후속 단계; 본 resolver는 그 결정을 순수하게 잠근다.)
/// </summary>
public static class NarrativeMomentResolver
{
    /// <summary>방금 정산한 노드 id가 현재 선택 사이트의 extract 노드면 ExtractCommitted를 발화해야 한다.</summary>
    public static bool ShouldCommitExtract(GameSessionState session, string resolvedNodeId)
    {
        if (session == null || string.IsNullOrEmpty(resolvedNodeId))
        {
            return false;
        }

        return string.Equals(
            resolvedNodeId,
            $"{session.SelectedCampaignSiteId}:extract",
            StringComparison.Ordinal);
    }

    /// <summary>엔딩 moment에 넘길 컨텍스트(chapter/site)를 세션 진실에서 조립 — 조건(ChapterIs/SiteIs) 평가용.</summary>
    public static StoryMomentContext BuildExtractContext(GameSessionState session)
    {
        return new StoryMomentContext
        {
            ChapterId = session.SelectedCampaignChapterId,
            SiteId = session.SelectedCampaignSiteId,
        };
    }

    /// <summary>
    /// 전투/보상 등 노드-스코프 moment에 넘길 컨텍스트 — 사이트 좌표 + 현재 노드 인덱스 + (옵션) 보상 요약.
    /// 씬 controller의 BuildStoryMomentContext와 동일 필드를 세션 진실에서 조립해, 발화원을 세션으로 통합해도
    /// 동일 컨텍스트가 director에 전달되게 한다.
    /// </summary>
    public static StoryMomentContext BuildNodeContext(GameSessionState session, bool withRewardSummary = false)
    {
        return new StoryMomentContext
        {
            ChapterId = session.SelectedCampaignChapterId,
            SiteId = session.SelectedCampaignSiteId,
            NodeIndex = session.CurrentExpeditionNodeIndex,
            RewardSummary = withRewardSummary ? session.LastCommittedRewardSummary : null,
        };
    }
}
