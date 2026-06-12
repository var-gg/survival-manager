using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;

namespace SM.Tests.EditMode;

/// <summary>
/// T1 detector 의미론 계약 — 카운터는 "틱"이 아니라 "순간"을 센다.
/// 구출(SaveMoment)은 빈사 episode당 1회(회복 후 재빈사만 재카운트), 차단(우회 유도)은 공격자당 1회.
/// </summary>
[Category("FastUnit")]
public sealed class BattleActivityDetectorContractTests
{
    [Test]
    public void SaveMoment_CountsOncePerNearDeathEpisode_AndResetsAfterRecovery()
    {
        var ally = CombatTestFactory.CreateUnit("ally_tank", classId: "vanguard", anchor: DeploymentAnchorId.FrontCenter, hp: 100f);
        var enemy = CombatTestFactory.CreateUnit("enemy_dummy", race: "undead", classId: "vanguard", anchor: DeploymentAnchorId.FrontCenter, hp: 100f);
        var state = CombatTestFactory.CreateBattleState(new[] { ally }, new[] { enemy }, seed: 7);
        var tank = state.Allies[0];
        var telemetry = state.ActivityTelemetry;

        // 같은 빈사 episode 안의 연속 힐 — 1회만.
        Assert.That(telemetry.TryBeginSaveMomentEpisode(tank.Id.Value), Is.True, "첫 구출은 카운트");
        Assert.That(telemetry.TryBeginSaveMomentEpisode(tank.Id.Value), Is.False, "같은 episode 재힐은 비카운트");
        Assert.That(telemetry.SaveMomentCount, Is.EqualTo(1));

        // 빈사 상태(HP < reset 문턱) 유지 중에는 RecordStep이 episode를 풀지 않는다.
        tank.TakeDamage(85f); // 15% — reset 문턱(35%) 아래
        telemetry.RecordStep(state);
        Assert.That(telemetry.TryBeginSaveMomentEpisode(tank.Id.Value), Is.False, "빈사 유지 중엔 episode 비해제");

        // reset 문턱 이상으로 회복하면 다음 빈사가 새 episode다.
        tank.Heal(60f); // 75%
        telemetry.RecordStep(state);
        Assert.That(telemetry.TryBeginSaveMomentEpisode(tank.Id.Value), Is.True, "회복 후 재빈사는 새 구출");
        Assert.That(telemetry.SaveMomentCount, Is.EqualTo(2));
    }

    [Test]
    public void ScreenDeterrence_CountsOncePerActor()
    {
        var ally = CombatTestFactory.CreateUnit("ally", classId: "vanguard", anchor: DeploymentAnchorId.FrontCenter, hp: 100f);
        var enemy = CombatTestFactory.CreateUnit("enemy", race: "undead", classId: "vanguard", anchor: DeploymentAnchorId.FrontCenter, hp: 100f);
        var state = CombatTestFactory.CreateBattleState(new[] { ally }, new[] { enemy }, seed: 7);
        var telemetry = state.ActivityTelemetry;

        telemetry.RecordScreenDeterrence("attacker_a");
        telemetry.RecordScreenDeterrence("attacker_a");
        Assert.That(telemetry.ScreenDeterrenceCount, Is.EqualTo(1), "같은 공격자의 지속 차단은 1회");

        telemetry.RecordScreenDeterrence("attacker_b");
        Assert.That(telemetry.ScreenDeterrenceCount, Is.EqualTo(2), "다른 공격자는 별도 카운트");
    }
}
