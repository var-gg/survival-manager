using System;

namespace SM.Unity;

/// <summary>
/// 원정 루프 화면 전환의 단일 진실(순수) — "현재 세션 상태에서 이 전환 이벤트가 어느 화면으로 가는가"를
/// 세션 플래그만으로 결정한다. UnityEngine/VisualElement/SceneManager 의존 0 → EditMode에서 씬 없이 검증 가능.
///
/// 결정 출처(코드 그대로 codify): TownScreenPresenter.OpenExpedition(pending→Reward / 아니면 Atlas),
/// AtlasScreenController.ContinueToExpedition(선택노드 RequiresBattle→Battle / extract 정산→Reward),
/// BattleScreenController.ContinueToReward(→Reward), RewardScreenPresenter.ReturnToTown(→Town).
/// controller들이 이 resolver를 채택하면(후속 stage) 인라인 분기가 사라지고 전환 로직이 한 곳에 모인다.
/// </summary>
public static class ExpeditionFlowResolver
{
    /// <summary>Town "원정" 진입 — 보상 정산이 밀려 있으면 정산 화면, 아니면 Atlas(resume·new 공통).</summary>
    public static NavTarget ResolveExpeditionEntry(GameSessionState session)
    {
        if (session == null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        return session.HasPendingRewardSettlement ? NavTarget.Reward : NavTarget.Atlas;
    }

    /// <summary>Atlas "계속" — 선택 노드가 전투면 Battle, 아니면(추출/정산 노드) Reward.</summary>
    public static NavTarget ResolveAtlasContinue(GameSessionState session)
    {
        if (session == null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        var node = session.GetSelectedExpeditionNode();
        return node != null && node.RequiresBattle ? NavTarget.Battle : NavTarget.Reward;
    }

    /// <summary>전투 종료 → 보상 정산(고정).</summary>
    public static NavTarget AfterBattleResolved => NavTarget.Reward;

    /// <summary>보상 정산 종료 → Town 복귀(고정).</summary>
    public static NavTarget AfterRewardSettled => NavTarget.Town;
}
