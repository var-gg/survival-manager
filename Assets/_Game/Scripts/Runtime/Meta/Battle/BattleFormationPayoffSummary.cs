namespace SM.Meta;

/// <summary>
/// 전투 종료 후 "내 진형이 만든 그림"의 구조화 요약 — MVP + 진형 전과(측면/후방/차단/구출/후열격파) +
/// 발현(시너지/콤보/증강). presentation carrier 전용(원시값만) — sim/save truth 무접촉.
/// <see cref="SM.Unity.BattleHighlightLedger"/> 가 산출하고, GameSessionState 가 런타임으로 보관하며,
/// RewardScreenPresenter 가 빌드를 고르는 보상 화면에 표시한다(전투 피드의 transient 요약과 같은 데이터,
/// 다른 surface). 표시 포맷은 <see cref="BattleFormationPayoffFormatter"/> 가 소유한다.
/// </summary>
public sealed record BattleFormationPayoffSummary(
    bool HasData,
    string MvpUnitName,
    float MvpDamage,
    float MvpHealing,
    int MvpKills,
    int FlankStrikes,
    int RearStrikes,
    int ScreenAbsorbs,
    int SaveMoments,
    int BacklineDiveKills,
    int SynergyActivations,
    int ComboConsumes,
    int TriggeredEffects)
{
    /// <summary>집계가 전무한 기본값 — reset/미전투 상태에서 보상 화면이 행을 만들지 않게 한다.</summary>
    public static readonly BattleFormationPayoffSummary Empty = new(
        HasData: false,
        MvpUnitName: "",
        MvpDamage: 0f,
        MvpHealing: 0f,
        MvpKills: 0,
        FlankStrikes: 0,
        RearStrikes: 0,
        ScreenAbsorbs: 0,
        SaveMoments: 0,
        BacklineDiveKills: 0,
        SynergyActivations: 0,
        ComboConsumes: 0,
        TriggeredEffects: 0);
}
