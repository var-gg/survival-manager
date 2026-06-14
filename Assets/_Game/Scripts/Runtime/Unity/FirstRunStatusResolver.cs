using SM.Persistence.Abstractions.Models;

namespace SM.Unity;

/// <summary>
/// first-run(첫 원정 루프 미완료) 여부 판정 — 신규 플레이어 온보딩 강조(원정 CTA/안내)에 쓴다.
///
/// 판정 신호는 <b>콘텐츠 무저작</b>인 캠페인 진행도: 클리어한 site 가 0이고 스토리 미클리어면 first-run.
/// gpt-pro 온보딩 설계는 narrative sentinel(story_onboarding_first_loop_completed_v1, SeenCount==0)을
/// 제안했으나, 그건 StoryEvent 에셋 저작 + narrative 빌드 파이프라인이 필요하다([[project_narrative_pindoc_runtime_drift]]).
/// 동일 의미(첫 루프 완료 전)를 이미 영속되는 <see cref="CampaignProgressRecord"/>로 판정해 콘텐츠 의존을 없앤다.
/// 후속에서 narrative sentinel 로 교체하려면 이 한 곳만 바꾸면 된다.
///
/// 순수 — 영속 record 만 받는다(FastUnit 대상).
/// </summary>
public static class FirstRunStatusResolver
{
    public static bool IsFirstRunActive(CampaignProgressRecord progress)
    {
        if (progress == null)
        {
            return true;
        }

        if (progress.StoryCleared)
        {
            return false;
        }

        return progress.ClearedSiteIds == null || progress.ClearedSiteIds.Count == 0;
    }
}
