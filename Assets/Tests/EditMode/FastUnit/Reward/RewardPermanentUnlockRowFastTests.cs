using System.Linq;
using NUnit.Framework;
using SM.Unity.UI.Reward;

namespace SM.Tests.EditMode.FastUnit.Reward;

/// <summary>
/// RewardScreenPresenter.BuildPermanentUnlockRowsCore 계약 — 첫 임시 증강 픽으로 예정된 영구 해금을
/// 정산 ledger 행으로 surfacing. pending id 가 없으면 행을 만들지 않는다(평시 무잡음).
/// 표시명/로컬라이즈는 presenter instance 책임이므로 여기선 순수 코어(행 생성 여부 + tone)만 검증.
/// </summary>
[Category("FastUnit")]
public sealed class RewardPermanentUnlockRowFastTests
{
    [Test]
    public void Core_EmptyPendingId_ReturnsNoRow()
    {
        Assert.IsEmpty(RewardScreenPresenter.BuildPermanentUnlockRowsCore(string.Empty, "영구 해금", "x 확정"));
        Assert.IsEmpty(RewardScreenPresenter.BuildPermanentUnlockRowsCore("   ", "영구 해금", "x 확정"));
    }

    [Test]
    public void Core_PendingId_ReturnsSingleRow_WithPermanentUnlockTone()
    {
        var rows = RewardScreenPresenter.BuildPermanentUnlockRowsCore(
            "augment_perm_hunt",
            "영구 해금",
            "헌트 강령 · 귀환 시 영구 후보로 확정");

        Assert.AreEqual(1, rows.Count);
        var row = rows.Single();
        Assert.AreEqual("영구 해금", row.KeyText);
        Assert.AreEqual("헌트 강령 · 귀환 시 영구 후보로 확정", row.ValueText);
        Assert.AreEqual("permanent-unlock", row.ToneKey, "View 가 reward-progression-row--permanent-unlock 클래스를 붙인다");
    }
}
