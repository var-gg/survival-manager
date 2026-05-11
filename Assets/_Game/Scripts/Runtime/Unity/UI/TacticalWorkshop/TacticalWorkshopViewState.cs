using System.Collections.Generic;

namespace SM.Unity.UI.TacticalWorkshop;

public sealed record TacticalWorkshopViewState(
    string Eyebrow,
    string Title,
    IReadOnlyList<TacticalWorkshopPostureViewState> Postures,
    IReadOnlyList<TacticalWorkshopScenarioViewState> Scenarios,
    IReadOnlyList<TacticalWorkshopTacticStripViewState> Tactics)
{
    public const string SelectedPostureClass = "sm-tw-posture-card--selected";
    public const string SelectedScenarioTabClass = "sm-tw-scenario-tab--selected";
    public const string SelectedPreviewClass = "sm-tw-live-preview__image--selected";

    public static TacticalWorkshopViewState CreateDefault(int selectedPostureIndex, int selectedScenarioIndex)
    {
        selectedPostureIndex = ClampIndex(selectedPostureIndex, 5);
        selectedScenarioIndex = ClampIndex(selectedScenarioIndex, 3);

        return new TacticalWorkshopViewState(
            "TACTICAL WORKSHOP",
            "전술 공방",
            new[]
            {
                new TacticalWorkshopPostureViewState(
                    "TwPosture_HoldLine",
                    "TwPosture_HoldLine_Title",
                    "TwPosture_HoldLine_Body",
                    "전열 사수",
                    "전열을 고정하고 후열 피해를 줄입니다.",
                    selectedPostureIndex == 0),
                new TacticalWorkshopPostureViewState(
                    "TwPosture_StandardAdvance",
                    "TwPosture_StandardAdvance_Title",
                    "TwPosture_StandardAdvance_Body",
                    "표준 전진",
                    "위험과 화력을 균형 있게 받아냅니다.",
                    selectedPostureIndex == 1),
                new TacticalWorkshopPostureViewState(
                    "TwPosture_ProtectCarry",
                    "TwPosture_ProtectCarry_Title",
                    "TwPosture_ProtectCarry_Body",
                    "캐리 보호",
                    "핵심 딜러에게 보호와 회복을 집중합니다.",
                    selectedPostureIndex == 2),
                new TacticalWorkshopPostureViewState(
                    "TwPosture_CollapseWeakSide",
                    "TwPosture_CollapseWeakSide_Title",
                    "TwPosture_CollapseWeakSide_Body",
                    "약측 무너뜨리기",
                    "노출된 적을 빠르게 꺾어 수적 우위를 만듭니다.",
                    selectedPostureIndex == 3),
                new TacticalWorkshopPostureViewState(
                    "TwPosture_AllInBackline",
                    "TwPosture_AllInBackline_Title",
                    "TwPosture_AllInBackline_Body",
                    "후열 깊이 침투",
                    "후열 위협을 감수하고 적 핵심을 노립니다.",
                    selectedPostureIndex == 4),
            },
            new[]
            {
                new TacticalWorkshopScenarioViewState(
                    "TwScenario_BalancedOpening",
                    "TwPreview_BalancedOpening",
                    "Opening",
                    selectedScenarioIndex == 0),
                new TacticalWorkshopScenarioViewState(
                    "TwScenario_AntiBurst",
                    "TwPreview_AntiBurst",
                    "Anti Burst",
                    selectedScenarioIndex == 1),
                new TacticalWorkshopScenarioViewState(
                    "TwScenario_EndgameFortress",
                    "TwPreview_EndgameFortress",
                    "Fortress",
                    selectedScenarioIndex == 2),
            },
            new[]
            {
                new TacticalWorkshopTacticStripViewState("TwTactic_Vanguard_Text", "Burst shield when ally HP drops."),
                new TacticalWorkshopTacticStripViewState("TwTactic_Duelist_Text", "Dive exposed enemy at burst window."),
                new TacticalWorkshopTacticStripViewState("TwTactic_Ranger_Text", "Focus lowest HP enemy from range."),
                new TacticalWorkshopTacticStripViewState("TwTactic_Mystic_Text", "Cleanse and heal at danger spike."),
            });
    }

    private static int ClampIndex(int value, int count)
    {
        if (value < 0)
        {
            return 0;
        }

        return value >= count ? count - 1 : value;
    }
}

public sealed record TacticalWorkshopPostureViewState(
    string ButtonName,
    string TitleName,
    string BodyName,
    string Title,
    string Body,
    bool IsSelected);

public sealed record TacticalWorkshopScenarioViewState(
    string TabName,
    string PreviewName,
    string Label,
    bool IsSelected);

public sealed record TacticalWorkshopTacticStripViewState(
    string TextName,
    string Text);
