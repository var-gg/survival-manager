using System.IO;
using NUnit.Framework;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class RecruitPresenterFastTests
{
    [Test]
    public void RecruitPresenter_Keeps_Four_Slot_Runtime_Contract_With_Selected_Detail()
    {
        var state = File.ReadAllText("Assets/_Game/Scripts/Runtime/Unity/UI/Town/Preview/RecruitViewState.cs");
        Assert.That(state, Does.Contain("RecruitSelectedCandidateDetailViewState"));
        Assert.That(state, Does.Contain("RecruitRosterPressureViewState"));
        Assert.That(state, Does.Contain("RecruitDecisionMetricViewState"));
        Assert.That(state, Does.Contain("RecruitSkillPipViewState"));
        Assert.That(state, Does.Contain("bool IsSelected"));
        Assert.That(state, Does.Contain("IReadOnlyList<string> StateChips"));

        var presenter = File.ReadAllText("Assets/_Game/Scripts/Runtime/Unity/UI/Town/Preview/RecruitPresenter.cs");
        Assert.That(presenter, Does.Contain("new List<RecruitCandidateViewState>(offers.Count)"));
        Assert.That(presenter, Does.Contain("BuildStateChips"));
        Assert.That(presenter, Does.Contain("BuildSelectedDetail"));
        Assert.That(presenter, Does.Contain("BuildRosterPressure"));
        Assert.That(presenter, Does.Contain("_selectedSlotIndex"));
    }

    [Test]
    public void RecruitView_Declares_Selected_Detail_And_Roster_Pressure_Decision_Panel()
    {
        var uxml = File.ReadAllText("Assets/_Game/UI/Panels/RecruitPack/RecruitPack.uxml");
        Assert.That(uxml, Does.Contain("CardRow"));
        Assert.That(uxml, Does.Contain("RecruitDecisionPanel"));
        Assert.That(uxml, Does.Contain("SelectedCandidateDetail"));
        Assert.That(uxml, Does.Contain("SelectedCandidateClassGlyph"));
        Assert.That(uxml, Does.Contain("SelectedCandidateMetricBars"));
        Assert.That(uxml, Does.Contain("SelectedCandidateSkillPips"));
        Assert.That(uxml, Does.Contain("SelectedCandidateStateChips"));
        Assert.That(uxml, Does.Contain("SelectedCandidateRecruitButton"));
        Assert.That(uxml, Does.Contain("WalletGoldLabel"));
        Assert.That(uxml, Does.Contain("WalletEchoLabel"));
        Assert.That(uxml, Does.Contain("RosterPressurePanel"));
        Assert.That(uxml, Does.Contain("RosterNeedChips"));

        var view = File.ReadAllText("Assets/_Game/Scripts/Runtime/Unity/UI/Town/Preview/RecruitView.cs");
        Assert.That(view, Does.Contain("RenderSelectedDetail"));
        Assert.That(view, Does.Contain("RenderRosterPressure"));
        Assert.That(view, Does.Contain("RenderMetrics"));
        Assert.That(view, Does.Contain("BuildSkillPip"));
        Assert.That(view, Does.Contain("OnCandidateSelected"));
    }
}
