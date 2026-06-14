using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Core.Contracts;
using SM.Meta.Model;
using SM.Unity;

namespace SM.Tests.EditMode;

/// <summary>
/// C1 회귀 — displacement(진형붕괴) 스킬이 일부 archetype의 **기본 킷**에 실제로 배선됐는지 검증.
/// 직전엔 displacement 3종이 RecruitFlexActivePool(영입 픽)에만 있어, 영입으로 직접 고른 영웅만 썼고
/// 베이스/적 유닛은 영영 못 써서 베이스 전투에 진형붕괴 드라마가 부재했다. 이제 guardian/bulwark의
/// flex active가 넉백(skill_aegis_linebreaker)이라 플레이어 base + 적 양쪽 경로 모두 displacement를 쓴다.
/// 두 경로가 비대칭이라(플레이어=LoadoutCompiler가 archetype.Skills의 utility_active 우선,
/// 적=UnitDefinition.EffectiveFlexActive가 Loadout.FlexActive 우선) 둘 다 검증해야 한다.
/// (실콘텐츠 Resources 로드라 BatchOnly — FastUnit 게이트 밖.)
/// </summary>
[Category("BatchOnly")]
public sealed class DisplacementBaseKitContentTests
{
    // base flex active가 displacement로 배선됐다고 기대하는 archetype.
    private static readonly string[] DisplacementBaseKitArchetypes = { "guardian", "bulwark" };

    [Test]
    public void DisplacementSkills_AreWiredIntoBaseKits_ForBothPlayerAndEnemyPaths()
    {
        var lookup = new RuntimeCombatContentLookup();
        Assert.That(lookup.TryGetCombatSnapshot(out var snapshot, out var error), Is.True, error);

        foreach (var archetypeId in DisplacementBaseKitArchetypes)
        {
            Assert.That(snapshot.Archetypes.TryGetValue(archetypeId, out var template), Is.True,
                $"archetype '{archetypeId}' 가 콘텐츠 스냅샷에 없음");

            // 적 경로: UnitDefinition.EffectiveFlexActive 가 Loadout.FlexActive(template.FlexActive)를 우선한다.
            Assert.That(template!.FlexActive, Is.Not.Null, $"{archetypeId}.FlexActive 가 null");
            Assert.That(template.FlexActive!.DisplacementKind, Is.Not.EqualTo(SkillDisplacementKind.None),
                $"{archetypeId} 적 경로(Loadout.FlexActive)에 displacement 스킬이 배선되지 않음");

            // 플레이어 경로: LoadoutCompiler base 가 archetype.Skills 의 utility_active 슬롯을 우선한다.
            var skillsFlexActive = template.Skills.FirstOrDefault(skill => skill.EffectiveSlotKind == ActionSlotKind.FlexActive);
            Assert.That(skillsFlexActive, Is.Not.Null, $"{archetypeId}.Skills 에 utility_active 슬롯 스킬이 없음");
            Assert.That(skillsFlexActive!.DisplacementKind, Is.Not.EqualTo(SkillDisplacementKind.None),
                $"{archetypeId} 플레이어 경로(Skills[utility_active])에 displacement 스킬이 배선되지 않음");
        }
    }
}
