using System;
using System.Linq;
using NUnit.Framework;
using SM.Meta;

namespace SM.Tests.EditMode;

/// <summary>
/// ADR-0028 #provenance 영속 — 정치 조건을 match audit의 stable token으로 인코딩하는지 검증.
/// "factionId|channel|reasonCode" — 표시 문구가 아니라 stable id/code(ID/label 분리, replay 재검증 입력).
/// </summary>
[Category("FastUnit")]
public sealed class PoliticalConditionAuditTests
{
    [Test]
    public void Encode_ProducesStableFactionChannelReasonToken()
    {
        var conditions = PoliticalCombatConditionService.Resolve(
            WarrantCatalog.SolarumOrderId,
            factionId => factionId == WarrantCatalog.SolarumId ? PoliticalCombatConditionService.SupportTrustThreshold
                : factionId == WarrantCatalog.PaleConclaveId ? PoliticalCombatConditionService.AlertStandingThreshold
                : 0);

        var tokens = PoliticalConditionAudit.EncodeAll(conditions);

        Assert.That(tokens.Count, Is.EqualTo(2));

        var support = tokens.Single(token => token.Contains(PoliticalChannel.AllySupport.ToString()));
        Assert.That(support, Does.StartWith($"{WarrantCatalog.SolarumId}|"));
        Assert.That(support.Split('|').Length, Is.EqualTo(3), "factionId|channel|reasonCode 3-파트.");

        var alert = tokens.Single(token => token.Contains(PoliticalChannel.EnemyAlertness.ToString()));
        Assert.That(alert, Does.StartWith($"{WarrantCatalog.PaleConclaveId}|"));
    }

    [Test]
    public void EncodeAll_EmptyOrNull_ReturnsEmpty()
    {
        Assert.That(PoliticalConditionAudit.EncodeAll(Array.Empty<PoliticalCombatCondition>()), Is.Empty);
        Assert.That(PoliticalConditionAudit.EncodeAll(null!), Is.Empty);
    }
}
