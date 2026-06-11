using System;
using System.Collections.Generic;
using NUnit.Framework;
using SM.Unity.Sandbox;

namespace SM.Tests.EditMode;

/// <summary>
/// sandbox 멤버 저작의 타겟 지시가 blueprint 사전으로 수집되는 계약 —
/// 전투테스트(합성팀) 레인이 P1 지시를 LoadoutCompiler까지 전달하는 입구.
/// </summary>
[Category("FastUnit")]
public sealed class CombatSandboxTargetDirectiveTests
{
    [Test]
    public void CollectsKnownStableIds_AndNormalizes()
    {
        var directives = new Dictionary<string, string>(StringComparer.Ordinal);

        CombatSandboxScenarioCompiler.CollectTargetDirective(Member(" hunt_backline "), "hero_a", directives);
        CombatSandboxScenarioCompiler.CollectTargetDirective(Member("finish_lowest"), "hero_b", directives);

        Assert.That(directives, Has.Count.EqualTo(2));
        Assert.That(directives["hero_a"], Is.EqualTo("hunt_backline"), "공백 trim 후 안정 id로 정규화");
        Assert.That(directives["hero_b"], Is.EqualTo("finish_lowest"));
    }

    [Test]
    public void CloneMember_RoundTripsTargetDirective()
    {
        // 실제 레인은 CloneTeam→CloneMember를 반드시 거친다 — 복제 누락은 저작 지시의 무음 유실(리뷰에서 잡힌 critical).
        var cloned = CombatSandboxScenarioCompiler.CloneMember(Member("hunt_backline"));

        Assert.That(cloned.TargetDirectiveId, Is.EqualTo("hunt_backline"), "CloneMember는 TargetDirectiveId를 보존해야 한다");
    }

    [Test]
    public void IgnoresEmptyUnknownAndDefault()
    {
        var directives = new Dictionary<string, string>(StringComparer.Ordinal);

        CombatSandboxScenarioCompiler.CollectTargetDirective(Member(string.Empty), "hero_a", directives);
        CombatSandboxScenarioCompiler.CollectTargetDirective(Member("default"), "hero_b", directives);
        CombatSandboxScenarioCompiler.CollectTargetDirective(Member("not_a_directive"), "hero_c", directives);

        Assert.That(directives, Is.Empty, "Default/미상 id는 blueprint에 기록하지 않는다 — 전원 Default와 동치");
    }

    private static CombatSandboxTeamMemberDefinition Member(string directiveId)
    {
        return new CombatSandboxTeamMemberDefinition
        {
            MemberId = "m",
            ArchetypeId = "raider",
            TargetDirectiveId = directiveId,
        };
    }
}
