using System.Collections.Generic;
using SM.Combat.Model;
using SM.Unity;

namespace SM.Tests.EditMode.Playthrough;

/// <summary>
/// 헤드리스 플레이스루의 **결정 표면** — "Unity는 껍데기, 게임플레이 truth는 순수 C#" 비전의 마지막 조각.
/// CampaignCompletionGoldenFastTests가 선택을 인라인으로 하드코딩한다면, 이 seam은 그 선택을
/// "게임이 player-facing View를 제시 → policy가 행동을 고른다"로 뽑아낸다. CampaignPlaythroughRunner가
/// 계약 너머의 모든 세션 구동을 담당하므로, **실제 LLM은 이 인터페이스만 구현하면 골든 스크립트 대신
/// 게임을 시작부터 엔딩까지 운전**할 수 있다(관찰→결정→행동 에이전트 루프).
///
/// 모델링한 결정:
/// - (1) 출격 진형 배치(DecideDeployment) — 드림게임 핵 pillar. 어느 영웅을 어느 앵커에 둘지.
/// - (2) 보상 선택(DecideReward) — 추출 후 제시된 N개 중 하나.
///
/// 의도적으로 제외:
/// - 전투 승패는 결정이 아니라 sim 결과 → policy가 고르지 않는다(헤드리스 fixture는 자동 승리).
///   진짜 전투 sim이 헤드리스로 배선되면 진형이 승패를 가르므로 같은 seam이 그대로 이어진다.
/// - 사이트 라우팅은 캠페인이 선형(전 사이트 클리어 필수)이라 runner 기계로 둔다.
///   무한모드(Atlas 파밍) 라우팅이 생기면 DecideNextSite 같은 policy 메서드로 승격한다.
/// </summary>
public interface IPlaythroughDecisionPolicy
{
    /// <summary>출격 전: 가용 영웅을 진형 앵커에 배치한다(배치 목록 반환, 빈 목록=무배치).</summary>
    IReadOnlyList<PlaythroughPlacement> DecideDeployment(PlaythroughDeploymentView view);

    /// <summary>추출 후: 제시된 보상 중 하나의 index를 고른다.</summary>
    int DecideReward(PlaythroughRewardView view);
}

/// <summary>진형 결정 입력 — 가용 영웅과 배치 가능한 앵커.</summary>
public sealed record PlaythroughDeploymentView(
    IReadOnlyList<PlaythroughHero> AvailableHeroes,
    IReadOnlyList<DeploymentAnchorId> Anchors);

/// <summary>진형 결정에 노출되는 영웅 한 명의 player-facing 정보.</summary>
public sealed record PlaythroughHero(
    string HeroId,
    string ClassId,
    string ArchetypeId,
    int Level);

/// <summary>policy의 진형 결정 한 칸 — 이 영웅을 이 앵커에.</summary>
public sealed record PlaythroughPlacement(
    DeploymentAnchorId Anchor,
    string HeroId);

/// <summary>보상 결정 입력 — 이 사이트 좌표와 제시된 보상 후보.</summary>
public sealed record PlaythroughRewardView(
    string ChapterId,
    string SiteId,
    IReadOnlyList<PlaythroughRewardOption> Options);

/// <summary>보상 후보 한 개의 player-facing 정보(PendingRewardChoices projection).</summary>
public sealed record PlaythroughRewardOption(
    int Index,
    RewardChoiceKind Kind,
    string PayloadId,
    int GoldAmount,
    int EchoAmount);
