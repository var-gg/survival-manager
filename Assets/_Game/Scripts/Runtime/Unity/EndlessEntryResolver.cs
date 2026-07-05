using SM.Persistence.Abstractions.Models;

namespace SM.Unity;

/// <summary>
/// 무한 순환 진입 여부 판정 — 엔딩(EndlessUnlocked) 이후 Town 원정 CTA가 스토리 원정 대신
/// 무한 순환을 가리키는지 결정한다. <see cref="FirstRunStatusResolver"/>와 동형의 순수 static
/// resolver(영속 record만 입력, FastUnit 대상) — presenter 라벨/툴팁과 세션 라우팅이 같은 판정을 읽는다.
///
/// run 재개/보상 정산 대기 같은 세션 상태 게이트는 caller(BuildExpeditionLabel 사다리,
/// GameSessionState.CanBeginEndlessCycle)가 앞단에서 처리한다 — 여기는 영속 진행도만 본다.
/// </summary>
public static class EndlessEntryResolver
{
    public static bool IsEndlessEntryActive(CampaignProgressRecord progress)
    {
        return progress is { EndlessUnlocked: true };
    }
}
