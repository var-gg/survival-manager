using NUnit.Framework;
using SM.Meta;

namespace SM.Tests.EditMode;

/// <summary>
/// BattleFormationPayoffFormatter 계약 — 전투 후 진형 페이오프 carrier 를 보상 화면 행 값으로 포맷.
/// 어휘는 전투 피드(BattleHighlightLedger.TryAppendBattleEndLines)와 1:1, 0 카운트는 억제.
/// </summary>
[Category("FastUnit")]
public sealed class BattleFormationPayoffFormatterFastTests
{
    [Test]
    public void Empty_AllValues_AreBlank()
    {
        var p = BattleFormationPayoffSummary.Empty;

        Assert.AreEqual("", BattleFormationPayoffFormatter.BuildMvpValue(p));
        Assert.AreEqual("", BattleFormationPayoffFormatter.BuildHighlightValue(p));
        Assert.AreEqual("", BattleFormationPayoffFormatter.BuildManifestValue(p));
    }

    [Test]
    public void NoData_SuppressesEvenWhenCountsPresent()
    {
        // HasData 가 master gate — false 면 카운트가 있어도 노출하지 않는다.
        var p = BattleFormationPayoffSummary.Empty with { FlankStrikes = 4, SynergyActivations = 2 };

        Assert.AreEqual("", BattleFormationPayoffFormatter.BuildHighlightValue(p));
        Assert.AreEqual("", BattleFormationPayoffFormatter.BuildManifestValue(p));
    }

    [Test]
    public void Mvp_FormatsNameDamageHealingKills()
    {
        var p = BattleFormationPayoffSummary.Empty with
        {
            HasData = true,
            MvpUnitName = "홍련",
            MvpDamage = 1240f,
            MvpHealing = 0f,
            MvpKills = 3,
        };

        Assert.AreEqual("홍련 · 피해 1240 · 회복 0 · 처치 3", BattleFormationPayoffFormatter.BuildMvpValue(p));
    }

    [Test]
    public void Mvp_BlankWhenNoName()
    {
        var p = BattleFormationPayoffSummary.Empty with { HasData = true, MvpUnitName = "", MvpKills = 2 };

        Assert.AreEqual("", BattleFormationPayoffFormatter.BuildMvpValue(p));
    }

    [Test]
    public void Highlight_OnlyPositiveTokens_InLedgerOrder()
    {
        var p = BattleFormationPayoffSummary.Empty with
        {
            HasData = true,
            RearStrikes = 2,
            FlankStrikes = 4,
            ScreenAbsorbs = 0,
            SaveMoments = 1,
            BacklineDiveKills = 0,
        };

        Assert.AreEqual("후방 강타 2 · 측면 협격 4 · 구출 1", BattleFormationPayoffFormatter.BuildHighlightValue(p));
    }

    [Test]
    public void Highlight_AllFive_JoinsInOrder()
    {
        var p = BattleFormationPayoffSummary.Empty with
        {
            HasData = true,
            RearStrikes = 1,
            FlankStrikes = 1,
            ScreenAbsorbs = 1,
            SaveMoments = 1,
            BacklineDiveKills = 1,
        };

        Assert.AreEqual(
            "후방 강타 1 · 측면 협격 1 · 전열 차단 1 · 구출 1 · 후열 격파 1",
            BattleFormationPayoffFormatter.BuildHighlightValue(p));
    }

    [Test]
    public void Manifest_OnlyPositiveBeatTokens()
    {
        var p = BattleFormationPayoffSummary.Empty with
        {
            HasData = true,
            SynergyActivations = 3,
            ComboConsumes = 0,
            TriggeredEffects = 2,
        };

        Assert.AreEqual("시너지 발동 3 · 증강 발동 2", BattleFormationPayoffFormatter.BuildManifestValue(p));
    }

    [Test]
    public void Highlight_NoPositiveCounts_IsBlankEvenWithData()
    {
        var p = BattleFormationPayoffSummary.Empty with { HasData = true, MvpUnitName = "홍련", MvpKills = 1 };

        Assert.AreEqual("", BattleFormationPayoffFormatter.BuildHighlightValue(p));
        Assert.AreEqual("", BattleFormationPayoffFormatter.BuildManifestValue(p));
    }
}
