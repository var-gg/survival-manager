using System;
using System.Linq;
using NUnit.Framework;
using SM.Meta;
using SM.Unity;
using SM.Unity.UI.Battle;

namespace SM.Tests.EditMode.FastUnit.Battle;

/// <summary>
/// ADR-0028 #provenance — 정치 조건이 전투 readout 행으로 노출되는지 검증(View 렌더 없이 Presenter core).
/// 출처 세력 + 채널(후원/경계) + ally/enemy tone. compile/snapshot hash가 *효과*만 포착하던 정치 출처를
/// 관전 중 player-visible로(GPT Pro 잔여 20%).
/// </summary>
[Category("FastUnit")]
public sealed class BattlePoliticalReadoutFastTests
{
    [Test]
    public void BuildPoliticalReadoutRowsCore_RendersSupportAndAlertRows()
    {
        // SolarumOrder 서약: 발행 솔라룸 신뢰≥임계 → AllySupport, 거스른 회상 결사 적대≤임계 → EnemyAlertness.
        var conditions = PoliticalCombatConditionService.Resolve(
            WarrantCatalog.SolarumOrderId,
            factionId => factionId == WarrantCatalog.SolarumId ? PoliticalCombatConditionService.SupportTrustThreshold
                : factionId == WarrantCatalog.PaleConclaveId ? PoliticalCombatConditionService.AlertStandingThreshold
                : 0);

        var rows = BattleScreenPresenter.BuildPoliticalReadoutRowsCore(
            conditions, WarrantDisplayDefaults.FactionName, WarrantDisplayDefaults.ChannelText);

        Assert.That(rows.Count, Is.EqualTo(2));

        var support = rows.Single(row => row.Tone == "ally");
        Assert.That(support.Label, Is.EqualTo("솔라룸"));
        Assert.That(support.Value, Is.EqualTo(WarrantDisplayDefaults.ChannelText(PoliticalChannel.AllySupport)));

        var alert = rows.Single(row => row.Tone == "enemy");
        Assert.That(alert.Label, Is.EqualTo("회상 결사"));
        Assert.That(alert.Value, Is.EqualTo(WarrantDisplayDefaults.ChannelText(PoliticalChannel.EnemyAlertness)));
    }

    [Test]
    public void BuildPoliticalReadoutRowsCore_NoConditions_NoRows()
    {
        Assert.That(
            BattleScreenPresenter.BuildPoliticalReadoutRowsCore(
                Array.Empty<PoliticalCombatCondition>(),
                WarrantDisplayDefaults.FactionName,
                WarrantDisplayDefaults.ChannelText),
            Is.Empty);
    }
}
