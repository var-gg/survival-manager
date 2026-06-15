using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;

namespace SM.Tests.EditMode.Playthrough;

/// <summary>
/// 결정론적 스크립트 정책 — CampaignCompletionGoldenFastTests의 인라인 선택을 재현한다.
/// "policy를 갈아끼우면 같은 엔딩에 도달하는가"의 byte-identical 기준선이자, 실제 LLM 정책의
/// 자리표시자(같은 IPlaythroughDecisionPolicy 계약).
///
/// - 진형: 근접(vanguard/duelist)은 전열 앵커, 원거리(ranger/mystic)는 후열 앵커로 — 역할 인지 배치.
///   앵커가 부족하면 남은 아무 앵커로 흘린다(전 영웅 배치 보장). 진형은 자동 승리 fixture에서 승패를
///   가르지 않지만, 정책이 "전술 의도"를 표현한다는 seam을 증명한다.
/// - 보상: 생성 시 지정한 index(기본 0)를 고른다. 범위 밖이면 clamp.
/// </summary>
public sealed class ScriptedPlaythroughPolicy : IPlaythroughDecisionPolicy
{
    private static readonly HashSet<string> FrontRowClasses = new(StringComparer.Ordinal) { "vanguard", "duelist" };

    private readonly int _rewardIndex;

    public ScriptedPlaythroughPolicy(int rewardIndex = 0)
    {
        _rewardIndex = rewardIndex;
    }

    public IReadOnlyList<PlaythroughPlacement> DecideDeployment(PlaythroughDeploymentView view)
    {
        var frontAnchors = new Queue<DeploymentAnchorId>(view.Anchors.Where(anchor => anchor.IsFrontRow()));
        var backAnchors = new Queue<DeploymentAnchorId>(view.Anchors.Where(anchor => !anchor.IsFrontRow()));
        var placements = new List<PlaythroughPlacement>();

        foreach (var hero in view.AvailableHeroes)
        {
            var prefersFront = FrontRowClasses.Contains(hero.ClassId);
            var primary = prefersFront ? frontAnchors : backAnchors;
            var fallback = prefersFront ? backAnchors : frontAnchors;

            if (primary.Count > 0)
            {
                placements.Add(new PlaythroughPlacement(primary.Dequeue(), hero.HeroId));
            }
            else if (fallback.Count > 0)
            {
                placements.Add(new PlaythroughPlacement(fallback.Dequeue(), hero.HeroId));
            }
            // 앵커가 모두 소진되면 더는 배치하지 않는다(정원 초과 영웅은 미배치).
        }

        return placements;
    }

    public int DecideReward(PlaythroughRewardView view)
    {
        if (view.Options.Count == 0)
        {
            return 0;
        }

        return Math.Clamp(_rewardIndex, 0, view.Options.Count - 1);
    }
}
