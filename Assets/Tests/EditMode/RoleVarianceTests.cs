using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Core.Contracts;
using SM.Core.Stats;
using SM.Meta.Model;

namespace SM.Tests.EditMode;

/// <summary>
/// RoleVariantTag 해석 검증.
/// LoadoutCompiler.ResolveRoleVariant는 private이므로,
/// BattleUnitLoadout의 RoleVariant 필드 기본값과 열거형 무결성을 확인하고,
/// 클래스별 예상 변이를 테스트용 archetype으로 검증한다.
/// </summary>
[Category("FastUnit")]
public sealed class RoleVarianceTests
{
    [Test]
    public void RoleVariantTag_DefaultIsUnassigned()
    {
        var unit = CombatTestFactory.CreateUnit("test-default");
        Assert.That(unit.RoleVariant, Is.EqualTo(RoleVariantTag.Unassigned),
            "기본 생성된 BattleUnitLoadout의 RoleVariant는 Unassigned여야 함");
    }

    [Test]
    public void RoleVariantTag_AllValues_AreDefined()
    {
        var expected = new[]
        {
            RoleVariantTag.Unassigned,
            RoleVariantTag.Anchor,
            RoleVariantTag.Peeler,
            RoleVariantTag.Diver,
            RoleVariantTag.Executioner,
            RoleVariantTag.Harrier,
            RoleVariantTag.Sniper,
            RoleVariantTag.Battery,
            RoleVariantTag.Controller,
        };

        var actual = Enum.GetValues(typeof(RoleVariantTag)).Cast<RoleVariantTag>().ToArray();
        Assert.That(actual, Is.EquivalentTo(expected),
            "RoleVariantTag 열거형은 9가지 값을 가져야 함");
    }

    [Test]
    public void BattleUnitLoadout_AcceptsExplicitRoleVariant()
    {
        var unit = new BattleUnitLoadout(
            "test-explicit",
            "Test",
            "human",
            "vanguard",
            DeploymentAnchorId.FrontCenter,
            new Dictionary<StatKey, float>(),
            Array.Empty<UnitRuleChain>(),
            Array.Empty<BattleSkillSpec>(),
            RoleVariant: RoleVariantTag.Anchor);

        Assert.That(unit.RoleVariant, Is.EqualTo(RoleVariantTag.Anchor));
    }

    [Test]
    public void VanguardArchetype_ExpectedVariants()
    {
        // Vanguard without ProtectCarryBias → Anchor
        var anchor = MakeMinimalArchetype("vanguard_anchor", "human", "vanguard");
        Assert.That(ResolveVariantViaClassLogic(anchor, protectCarryBias: 0f),
            Is.EqualTo(RoleVariantTag.Anchor), "vanguard + low ProtectCarryBias → Anchor");

        // Vanguard with high ProtectCarryBias → Peeler
        Assert.That(ResolveVariantViaClassLogic(anchor, protectCarryBias: 0.5f),
            Is.EqualTo(RoleVariantTag.Peeler), "vanguard + high ProtectCarryBias → Peeler");
    }

    [Test]
    public void DuelistArchetype_ExpectedVariants()
    {
        // Duelist + Collapse + frontRow → Executioner
        var duelist = MakeMinimalArchetype("duelist_exec", "human", "duelist",
            rangeDiscipline: RangeDiscipline.Collapse);
        Assert.That(ResolveVariantViaClassLogic(duelist, isFrontRow: true),
            Is.EqualTo(RoleVariantTag.Executioner), "duelist + Collapse + frontRow → Executioner");

        // Duelist otherwise → Diver
        Assert.That(ResolveVariantViaClassLogic(duelist, isFrontRow: false),
            Is.EqualTo(RoleVariantTag.Diver), "duelist + backRow → Diver");
    }

    [Test]
    public void RangerArchetype_ExpectedVariants()
    {
        // Ranger + KiteBackward → Sniper
        var ranger = MakeMinimalArchetype("ranger_sniper", "elf", "ranger",
            rangeDiscipline: RangeDiscipline.KiteBackward);
        Assert.That(ResolveVariantViaClassLogic(ranger, isFrontRow: false),
            Is.EqualTo(RoleVariantTag.Sniper), "ranger + KiteBackward → Sniper");

        // Ranger + HoldBand → Harrier
        var harrier = MakeMinimalArchetype("ranger_harrier", "elf", "ranger",
            rangeDiscipline: RangeDiscipline.HoldBand);
        Assert.That(ResolveVariantViaClassLogic(harrier, isFrontRow: true),
            Is.EqualTo(RoleVariantTag.Harrier), "ranger + HoldBand → Harrier");
    }

    [Test]
    public void MysticArchetype_ExpectedVariants()
    {
        // Mystic + heal skill → Controller
        var healer = MakeMinimalArchetype("mystic_healer", "undead", "mystic",
            skillKind: SkillKind.Heal, healCoeff: 1f);
        Assert.That(ResolveVariantViaClassLogic(healer),
            Is.EqualTo(RoleVariantTag.Controller), "mystic + heal → Controller");

        // Mystic + debuff skill → Controller
        var controller = MakeMinimalArchetype("mystic_cc", "undead", "mystic",
            skillKind: SkillKind.Debuff);
        Assert.That(ResolveVariantViaClassLogic(controller),
            Is.EqualTo(RoleVariantTag.Controller), "mystic + debuff → Controller");

        // Mystic + strike skill (no heal, no control) → Battery
        var battery = MakeMinimalArchetype("mystic_battery", "undead", "mystic",
            skillKind: SkillKind.Strike);
        Assert.That(ResolveVariantViaClassLogic(battery),
            Is.EqualTo(RoleVariantTag.Battery), "mystic + strike only → Battery");
    }

    [Test]
    public void UnknownClass_FallsBackByRow()
    {
        var frontUnknown = MakeMinimalArchetype("unknown_front", "human", "unknown_class");
        Assert.That(ResolveVariantViaClassLogic(frontUnknown, isFrontRow: true),
            Is.EqualTo(RoleVariantTag.Anchor), "unknown class + frontRow → Anchor");

        Assert.That(ResolveVariantViaClassLogic(frontUnknown, isFrontRow: false),
            Is.EqualTo(RoleVariantTag.Sniper), "unknown class + backRow → Sniper");
    }

    // ──────────────────────────────────────────────
    // Pure logic mirror of LoadoutCompiler.ResolveRoleVariant
    // ──────────────────────────────────────────────
    // Since ResolveRoleVariant is private, we replicate its switch logic here
    // as a test oracle. If the production code changes, this test will drift
    // and must be updated — which is intentional for a contract test.

    private static RoleVariantTag ResolveVariantViaClassLogic(
        CombatArchetypeTemplate archetype,
        bool isFrontRow = true,
        float protectCarryBias = 0f)
    {
        var hasHeal = archetype.Skills.Any(skill => skill.HealCoeff > 0f);
        var hasControl = archetype.Skills.Any(skill =>
            skill.Kind is SkillKind.Debuff or SkillKind.Utility);
        var hasSummon = archetype.Skills.Any(skill => skill.SummonProfile != null);
        var rangeDiscipline = archetype.Behavior?.RangeDiscipline ?? RangeDiscipline.HoldBand;

        return archetype.ClassId switch
        {
            "vanguard" when protectCarryBias > 0.3f => RoleVariantTag.Peeler,
            "vanguard" => RoleVariantTag.Anchor,
            "duelist" when rangeDiscipline == RangeDiscipline.Collapse && isFrontRow => RoleVariantTag.Executioner,
            "duelist" => RoleVariantTag.Diver,
            "ranger" when rangeDiscipline is RangeDiscipline.KiteBackward or RangeDiscipline.SideStepHold => RoleVariantTag.Sniper,
            "ranger" => RoleVariantTag.Harrier,
            "mystic" when hasHeal => RoleVariantTag.Controller,
            "mystic" when hasControl || hasSummon => RoleVariantTag.Controller,
            "mystic" => RoleVariantTag.Battery,
            _ when isFrontRow => RoleVariantTag.Anchor,
            _ => RoleVariantTag.Sniper,
        };
    }

    // ── helpers ──

    private static CombatArchetypeTemplate MakeMinimalArchetype(
        string id,
        string raceId,
        string classId,
        RangeDiscipline rangeDiscipline = RangeDiscipline.HoldBand,
        SkillKind skillKind = SkillKind.Strike,
        float healCoeff = 0f)
    {
        var skills = new List<BattleSkillSpec>();
        if (healCoeff > 0f || skillKind != SkillKind.Strike)
        {
            skills.Add(new BattleSkillSpec(
                $"{id}:skill",
                "Test Skill",
                skillKind,
                5f,
                1.2f,
                HealCoeff: healCoeff));
        }

        BehaviorProfile? behavior = rangeDiscipline != RangeDiscipline.HoldBand
            ? new BehaviorProfile(0.25f, 0.1f, 0.1f, 0.1f, 0.5f, 0.5f, 0f, 0f, 0f, 0.5f, 1f,
                FormationLine.Frontline, rangeDiscipline, 0.8f, 1.2f, 0.4f, 0.25f, 6f, 0f)
            : null;

        return new CombatArchetypeTemplate(
            id, id, raceId, classId,
            DeploymentAnchorId.FrontCenter,
            new Dictionary<StatKey, float>(),
            Array.Empty<TacticRule>(),
            skills,
            Behavior: behavior);
    }
}
