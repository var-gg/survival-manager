using System.Linq;
using NUnit.Framework;
using SM.Meta;
using SM.Unity.UI.Reward;

namespace SM.Tests.EditMode.FastUnit.Reward;

/// <summary>
/// RewardScreenPresenter.BuildFormationPayoffRowsCore 계약 — 직전 전투의 진형 페이오프(MVP/진형 전과/발현)를
/// 보상 정산 ledger 행으로 surfacing. 게임의 중심 카타르시스를 빌드 결정 화면으로 운반한다.
/// HasData=false(미전투/집계 전무)면 무행, 값이 빈 섹션은 행을 만들지 않는다(평시 무잡음). 순수 코어(세션 불요).
/// </summary>
[Category("FastUnit")]
public sealed class RewardFormationPayoffRowFastTests
{
    [Test]
    public void NoData_ReturnsNoRow()
    {
        Assert.IsEmpty(RewardScreenPresenter.BuildFormationPayoffRowsCore(BattleFormationPayoffSummary.Empty));
        Assert.IsEmpty(RewardScreenPresenter.BuildFormationPayoffRowsCore(null));
    }

    [Test]
    public void FullPayoff_ReturnsThreeRows_WithFormationTones()
    {
        var payoff = BattleFormationPayoffSummary.Empty with
        {
            HasData = true,
            MvpUnitName = "홍련",
            MvpDamage = 1240f,
            MvpKills = 3,
            FlankStrikes = 4,
            RearStrikes = 2,
            SaveMoments = 1,
            SynergyActivations = 3,
            ComboConsumes = 2,
        };

        var rows = RewardScreenPresenter.BuildFormationPayoffRowsCore(payoff);

        Assert.AreEqual(3, rows.Count);

        var mvp = rows[0];
        Assert.AreEqual("MVP", mvp.KeyText);
        Assert.AreEqual("formation-mvp", mvp.ToneKey);
        Assert.AreEqual("홍련 · 피해 1240 · 회복 0 · 처치 3", mvp.ValueText);

        var highlight = rows[1];
        Assert.AreEqual("진형 전과", highlight.KeyText);
        Assert.AreEqual("formation-highlight", highlight.ToneKey);
        Assert.AreEqual("후방 강타 2 · 측면 협격 4 · 구출 1", highlight.ValueText);

        var manifest = rows[2];
        Assert.AreEqual("발현", manifest.KeyText);
        Assert.AreEqual("formation-manifest", manifest.ToneKey);
        Assert.AreEqual("시너지 발동 3 · 콤보 작렬 2", manifest.ValueText);
    }

    [Test]
    public void OnlyMvp_ReturnsSingleRow()
    {
        var payoff = BattleFormationPayoffSummary.Empty with
        {
            HasData = true,
            MvpUnitName = "단현",
            MvpDamage = 800f,
            MvpKills = 1,
        };

        var rows = RewardScreenPresenter.BuildFormationPayoffRowsCore(payoff);

        Assert.AreEqual(1, rows.Count);
        Assert.AreEqual("formation-mvp", rows.Single().ToneKey);
    }

    [Test]
    public void DataWithoutAnyDisplayableValue_ReturnsNoRow()
    {
        // HasData=true 이나 MVP 이름 없음 + 모든 카운트 0 → 표시할 값이 없으니 행 없음(방어).
        var payoff = BattleFormationPayoffSummary.Empty with { HasData = true };

        Assert.IsEmpty(RewardScreenPresenter.BuildFormationPayoffRowsCore(payoff));
    }
}
