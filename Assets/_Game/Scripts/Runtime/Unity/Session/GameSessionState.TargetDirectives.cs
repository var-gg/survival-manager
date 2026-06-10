using System;
using System.Collections.Generic;
using SM.Core.Contracts;

namespace SM.Unity;

// P1 유닛별 타겟 지시 — 세션이 소유하는 플레이어 전술 레버 상태. GameSessionState 파사드 파일의
// public 표면 예산(BuildBoundaryGuardFastTests)을 지키기 위해 focused partial로 분리한다.
// 저장/복원은 SessionProfileSync(Capture/RestoreHeroTargetDirectivesFromActiveBlueprint)가 소유.
public sealed partial class GameSessionState
{
    // heroId → directive. Default는 저장하지 않는다(사전 부재 = Default).
    private readonly Dictionary<string, PlayerTargetDirective> _heroTargetDirectives = new(StringComparer.Ordinal);

    /// <summary>P1 유닛별 타겟 지시 조회 — 미설정은 Default.</summary>
    public PlayerTargetDirective GetHeroTargetDirective(string heroId)
    {
        return !string.IsNullOrWhiteSpace(heroId) && _heroTargetDirectives.TryGetValue(heroId, out var directive)
            ? directive
            : PlayerTargetDirective.Default;
    }

    public void SetHeroTargetDirective(string heroId, PlayerTargetDirective directive) =>
        _deploymentFlow.SetHeroTargetDirective(heroId, directive);

    private void SetHeroTargetDirectiveCore(string heroId, PlayerTargetDirective directive)
    {
        if (string.IsNullOrWhiteSpace(heroId))
        {
            return;
        }

        if (directive == PlayerTargetDirective.Default)
        {
            _heroTargetDirectives.Remove(heroId);
        }
        else
        {
            _heroTargetDirectives[heroId] = directive;
        }

        CaptureBlueprintState();
        SyncActiveRunIfPresent();
    }

    /// <summary>P1 지시 cycle — Default → 최근접 → 마무리 → 후열 사냥 → 밀집 격파 → Default.</summary>
    public PlayerTargetDirective CycleHeroTargetDirective(string heroId)
    {
        var values = (PlayerTargetDirective[])Enum.GetValues(typeof(PlayerTargetDirective));
        var next = values[(Array.IndexOf(values, GetHeroTargetDirective(heroId)) + 1) % values.Length];
        SetHeroTargetDirective(heroId, next);
        return next;
    }
}
