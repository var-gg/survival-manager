using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Meta;
using SM.Unity;
using SM.Unity.UI.Reward;

namespace SM.Tests.EditMode.FastUnit.Reward;

/// <summary>
/// ADR-0028 #1 가독성 — 정치 정산 report → reward 화면 progression 행 변환을 검증(View Render 없이 Presenter core만).
/// 표시명/standing은 주입 resolver로 격리(ID/label 분리). tone(gain/loss)·신뢰 부호·현재 standing 노출을 본다.
/// </summary>
[Category("FastUnit")]
public sealed class RewardPoliticalRowsFastTests
{
    [Test]
    public void BuildPoliticalRowsCore_RendersFactionReasonDeltaAndStanding()
    {
        var report = new PoliticalSettlementReport(
            WarrantCatalog.SolarumOrderId,
            WarrantCatalog.SolarumId,
            WarrantCatalog.PaleConclaveId,
            WarrantOutcome.Kept,
            new[]
            {
                new PoliticalSettlementLine(WarrantCatalog.SolarumId, 2, PoliticalSettlementReason.KeptIssuer),
                new PoliticalSettlementLine(WarrantCatalog.PaleConclaveId, -1, PoliticalSettlementReason.DefiedOpposed),
                new PoliticalSettlementLine(WarrantCatalog.WolfpineId, -1, PoliticalSettlementReason.RejectedOffer),
            });

        var standings = new Dictionary<string, int>
        {
            [WarrantCatalog.SolarumId] = 5,
            [WarrantCatalog.PaleConclaveId] = -3,
            [WarrantCatalog.WolfpineId] = 0,
        };

        var rows = RewardScreenPresenter.BuildPoliticalRowsCore(
            report,
            WarrantDisplayDefaults.FactionName,
            WarrantDisplayDefaults.SettlementReasonText,
            id => standings.TryGetValue(id, out var trust) ? trust : 0);

        Assert.That(rows.Count, Is.EqualTo(3));

        var gain = rows.Single(row => row.KeyText == "솔라룸");
        Assert.That(gain.ToneKey, Is.EqualTo("politics-gain"));
        Assert.That(gain.ValueText, Does.Contain("약속 이행").And.Contain("+2").And.Contain("현재 5"));

        var defied = rows.Single(row => row.KeyText == "회상 결사");
        Assert.That(defied.ToneKey, Is.EqualTo("politics-loss"));
        Assert.That(defied.ValueText, Does.Contain("거스름").And.Contain("-1").And.Contain("현재 -3"));

        var rejected = rows.Single(row => row.KeyText == "이리솔 부족");
        Assert.That(rejected.ToneKey, Is.EqualTo("politics-loss"));
        Assert.That(rejected.ValueText, Does.Contain("거절").And.Contain("-1"));
    }

    [Test]
    public void BuildPoliticalRowsCore_EmptyReport_NoRows()
    {
        var rows = RewardScreenPresenter.BuildPoliticalRowsCore(
            PoliticalSettlementReport.Empty,
            id => id,
            WarrantDisplayDefaults.SettlementReasonText,
            _ => 0);

        Assert.That(rows, Is.Empty);
    }
}
