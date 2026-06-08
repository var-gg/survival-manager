using System;
using SM.Combat.Model;
using UnityEngine.UIElements;

namespace SM.Unity.UI.Atlas;

/// <summary>
/// 출격 편성 확인 overlay View — WarrantSelectionView 패턴(Require/Bind/Render + 동적 행 빌드).
/// presentation만 담당하고 battle/save truth를 만들지 않으며, Presenter가 만든 view-state만 그린다.
/// 배치 슬롯은 클릭하면 SlotCycled를 발화해 해당 anchor를 바로 cycle한다(Atlas에서 직접 편성 조정).
/// Launch는 빈 배치(CanLaunch=false)일 때 disabled로 막아 빈 파티 출전 dead-end를 UI에서 차단한다.
/// </summary>
public sealed class SortieConfirmView
{
    private readonly Label _titleLabel;
    private readonly Label _deployHeader;
    private readonly Label _deployHint;
    private readonly VisualElement _deployList;
    private readonly VisualElement _synergyChips;
    private readonly Label _synergyEmpty;
    private readonly Button _launchButton;
    private readonly Button _editButton;
    private readonly Button _cancelButton;

    public SortieConfirmView(VisualElement root)
    {
        _titleLabel = Require<Label>(root, "SortieTitleLabel");
        _deployHeader = Require<Label>(root, "SortieDeployHeader");
        _deployHint = Require<Label>(root, "SortieDeployHint");
        _deployList = Require<VisualElement>(root, "SortieDeployList");
        _synergyChips = Require<VisualElement>(root, "SortieSynergyChips");
        _synergyEmpty = Require<Label>(root, "SortieSynergyEmpty");
        _launchButton = Require<Button>(root, "SortieLaunchButton");
        _editButton = Require<Button>(root, "SortieEditButton");
        _cancelButton = Require<Button>(root, "SortieCancelButton");
    }

    /// <summary>슬롯 클릭 — 해당 anchor를 cycle(통합 계층이 CycleDeploymentAssignment로 받는다).</summary>
    public event Action<DeploymentAnchorId>? SlotCycled;

    /// <summary>이대로 출격 — 흐름 통합이 warrant 게이트 또는 GoToBattle로 받는다.</summary>
    public event Action? Launched;

    /// <summary>마을에서 상세 편성 — posture/tactic 등 세부 편성은 Town SquadBuilder로.</summary>
    public event Action? EditRequested;

    /// <summary>돌아가기 — overlay만 닫고 Atlas에 머문다.</summary>
    public event Action? Cancelled;

    public void Bind()
    {
        _launchButton.clicked += () => Launched?.Invoke();
        _editButton.clicked += () => EditRequested?.Invoke();
        _cancelButton.clicked += () => Cancelled?.Invoke();
    }

    public void Render(SortieConfirmViewState state)
    {
        _titleLabel.text = state.Title;
        _deployHeader.text = state.DeployHeader;
        _deployHint.text = state.DeployHint;
        _launchButton.text = state.LaunchLabel;
        _launchButton.SetEnabled(state.CanLaunch);
        _editButton.text = state.EditLabel;
        _cancelButton.text = state.CancelLabel;
        RenderDeploySlots(state);
        RenderSynergyChips(state);
    }

    private void RenderDeploySlots(SortieConfirmViewState state)
    {
        _deployList.Clear();
        foreach (var slot in state.DeploySlots)
        {
            var anchorId = slot.Anchor;
            var row = new VisualElement();
            row.AddToClassList("sortie-deploy-row");
            row.EnableInClassList("sortie-deploy-row--empty", slot.IsEmpty);
            row.RegisterCallback<ClickEvent>(_ => SlotCycled?.Invoke(anchorId));

            var anchor = new Label(slot.AnchorLabel);
            anchor.AddToClassList("sortie-deploy-row__anchor");
            row.Add(anchor);

            var name = new Label(slot.HeroName);
            name.AddToClassList("sortie-deploy-row__name");
            row.Add(name);

            var archetype = new Label(slot.ArchetypeName);
            archetype.AddToClassList("sortie-deploy-row__archetype");
            archetype.AddToClassList("panel__text--muted");
            row.Add(archetype);

            _deployList.Add(row);
        }
    }

    private void RenderSynergyChips(SortieConfirmViewState state)
    {
        _synergyChips.Clear();
        var hasChips = state.SynergyChips.Count > 0;
        _synergyEmpty.text = state.SynergyEmptyText;
        _synergyEmpty.style.display = hasChips ? DisplayStyle.None : DisplayStyle.Flex;

        foreach (var chip in state.SynergyChips)
        {
            // 활성 칩은 "{family} {count} ({minor}/{major})", 미달 칩은 "한 명만 더 모으면" 신호를 덧붙인다.
            var label = chip.IsActive
                ? $"{chip.FamilyName} {chip.Count} ({chip.Minor}/{chip.Major})"
                : $"{chip.FamilyName} {chip.Count}/{chip.Minor} · {chip.Minor - chip.Count}명 더";
            var element = new Label(label);
            element.AddToClassList("sortie-synergy-chip");
            element.EnableInClassList("sortie-synergy-chip--major", chip.MajorReached);
            element.EnableInClassList("sortie-synergy-chip--inactive", !chip.IsActive);
            _synergyChips.Add(element);
        }
    }

    private static T Require<T>(VisualElement root, string name) where T : VisualElement
    {
        return root.Q<T>(name) ?? throw new InvalidOperationException($"Missing UITK element '{name}'.");
    }
}
