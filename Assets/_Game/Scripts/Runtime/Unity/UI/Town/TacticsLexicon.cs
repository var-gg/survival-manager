using SM.Combat.Model;
using SM.Content.Definitions;
using SM.Core.Contracts;

namespace SM.Unity.UI.Town;

/// <summary>
/// 전술 어휘 한국어 표시명 단일 소스 — posture / anchor / formation / range / target directive /
/// counter-coverage 차원의 UI 라벨. SquadBuilderPresenter(전술 설정)와 TacticalWorkshopPresenter(전술 공방)가
/// 같은 세션 truth를 다른 화면에서 보여주므로, 라벨 사본이 화면마다 드리프트하지 않도록 여기서만 정의한다.
/// 라벨만 소유한다 — gameplay 규칙/계산은 두지 않는다.
/// </summary>
public static class TacticsLexicon
{
    public static string Posture(TeamPostureType posture) => posture switch
    {
        TeamPostureType.HoldLine => "전열 사수",
        TeamPostureType.StandardAdvance => "표준 전진",
        TeamPostureType.ProtectCarry => "캐리 보호",
        TeamPostureType.CollapseWeakSide => "약측 무너뜨리기",
        TeamPostureType.AllInBackline => "후열 깊이 침투",
        _ => posture.ToString(),
    };

    public static string Anchor(DeploymentAnchorId anchor) => anchor switch
    {
        DeploymentAnchorId.FrontTop => "전열 상",
        DeploymentAnchorId.FrontCenter => "전열 중",
        DeploymentAnchorId.FrontBottom => "전열 하",
        DeploymentAnchorId.BackTop => "후열 상",
        DeploymentAnchorId.BackCenter => "후열 중",
        DeploymentAnchorId.BackBottom => "후열 하",
        _ => anchor.ToString(),
    };

    public static string Formation(FormationLine? formation) => formation switch
    {
        FormationLine.Frontline => "전열",
        FormationLine.Midline => "중열",
        FormationLine.Backline => "후열",
        _ => "배치 기준",
    };

    public static string Range(RangeDiscipline? range) => range switch
    {
        RangeDiscipline.Collapse => "압박 접근",
        RangeDiscipline.HoldBand => "거리 유지",
        RangeDiscipline.KiteBackward => "후퇴 카이팅",
        RangeDiscipline.SideStepHold => "측면 유지",
        RangeDiscipline.AnchorNearFrontline => "전열 근접",
        _ => "기본 교전 거리",
    };

    public static string Directive(PlayerTargetDirective directive) => directive switch
    {
        PlayerTargetDirective.NearestEnemy => "최근접 교전",
        PlayerTargetDirective.FinishLowestHp => "마무리 우선",
        PlayerTargetDirective.HuntExposedBackline => "후열 사냥",
        PlayerTargetDirective.BreakLargestCluster => "밀집 격파",
        _ => "기본(자동)",
    };

    /// <summary>SquadCounterCoveragePreview.Dimensions 8차원의 표시명. 미등록 차원은 raw id 노출로 결손을 드러낸다.</summary>
    public static string CounterTool(string tool) => tool switch
    {
        "ArmorShred" => "방어 관통",
        "Exposure" => "약점 노출",
        "GuardBreakMultiHit" => "가드 브레이크",
        "TrackingArea" => "광역 추적",
        "TenacityStability" => "강인·안정",
        "AntiHealShatter" => "치유 차단",
        "InterceptPeel" => "차단·견제",
        "CleaveWaveclear" => "다수 정리",
        _ => tool,
    };
}
