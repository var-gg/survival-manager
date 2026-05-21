using System.IO;
using NUnit.Framework;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class InventoryPresenterFastTests
{
    [Test]
    public void InventoryPresenter_Declares_Target_Hero_Compare_And_Equip_Cta_Contract()
    {
        var state = File.ReadAllText("Assets/_Game/Scripts/Runtime/Unity/UI/Town/Preview/InventoryViewState.cs");
        Assert.That(state, Does.Contain("InventoryCompareLaneViewState"));
        Assert.That(state, Does.Contain("InventoryEquipCtaViewState"));
        Assert.That(state, Does.Contain("InventoryCompareRowViewState"));
        Assert.That(state, Does.Contain("InventoryCompareLaneViewState? Compare"));

        var presenter = File.ReadAllText("Assets/_Game/Scripts/Runtime/Unity/UI/Town/Preview/InventoryPresenter.cs");
        Assert.That(presenter, Does.Contain("SetTargetHero"));
        Assert.That(presenter, Does.Contain("_targetHeroId"));
        Assert.That(presenter, Does.Contain("BuildCompare"));
        Assert.That(presenter, Does.Contain("_root.SessionState.EquipItem(_targetHeroId, itemInstanceId)"));
        Assert.That(presenter, Does.Contain("슬롯/희귀도/계열"));
        Assert.That(presenter, Does.Not.Contain("StatKey"));
    }

    [Test]
    public void InventoryView_Declares_Compare_Lane_And_Disabled_Equip_Cta_Rendering()
    {
        var uxml = File.ReadAllText("Assets/_Game/UI/Panels/InventoryTab/InventoryTab.uxml");
        Assert.That(uxml, Does.Contain("CompareLane"));
        Assert.That(uxml, Does.Contain("CompareTargetHeroLabel"));
        Assert.That(uxml, Does.Contain("CompareSelectedItemLabel"));
        Assert.That(uxml, Does.Contain("CompareEquippedItemLabel"));
        Assert.That(uxml, Does.Contain("CompareRows"));
        Assert.That(uxml, Does.Contain("CompareEquipStatusLabel"));

        var view = File.ReadAllText("Assets/_Game/Scripts/Runtime/Unity/UI/Town/Preview/InventoryView.cs");
        Assert.That(view, Does.Contain("RenderCompare"));
        Assert.That(view, Does.Contain("ApplyEquipCta"));
        Assert.That(view, Does.Contain("_equipButton.SetEnabled(cta?.CanEquip == true)"));
    }
}
