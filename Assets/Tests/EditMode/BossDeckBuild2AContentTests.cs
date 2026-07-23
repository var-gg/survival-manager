using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Core.Contracts;
using UnityEditor;
using UnityEngine;

namespace SM.Tests.EditMode;

[Category("BatchOnly")]
public sealed class BossDeckBuild2AContentTests
{
    private const string Root = "Assets/Resources/_Game/Content/Definitions";

    [TestCase("site_ashen_gate", TeamPostureTypeValue.ProtectCarry, "warden", "guardian", "vanguard")]
    [TestCase("site_sunken_bastion", TeamPostureTypeValue.HoldLine, "sunken_adjudicator_boss", "bastion_penitent", "vanguard")]
    [TestCase("site_tithe_road", TeamPostureTypeValue.StandardAdvance, "bastion_penitent_tithe_boss", "warden", "vanguard")]
    [TestCase("site_ruined_crypts", TeamPostureTypeValue.HoldLine, "guardian", "bastion_penitent", "mystic,vanguard")]
    [TestCase("site_bone_orchard", TeamPostureTypeValue.ProtectCarry, "warden", "reaver", "mystic")]
    public void BossDecks_UseCaptainAndScreenBodiesWithIntentionalClassPairs(
        string siteId,
        TeamPostureTypeValue posture,
        string captainArchetype,
        string screenArchetype,
        string pairedClassIds)
    {
        var squad = Load<EnemySquadTemplateDefinition>($"EnemySquads/{siteId}_boss_1_squad.asset");
        Assert.That(squad.EnemyPosture, Is.EqualTo(posture));
        Assert.That(squad.Members, Has.Count.EqualTo(4));

        var captain = squad.Members.Single(member => member.Role == EnemySquadMemberRoleValue.Captain);
        Assert.That(captain.ArchetypeId, Is.EqualTo(captainArchetype));
        Assert.That(captain.Anchor, Is.EqualTo(DeploymentAnchorValue.FrontCenter));

        var screen = squad.Members.Single(member => string.Equals(member.ArchetypeId, screenArchetype, StringComparison.Ordinal));
        Assert.That(screen.Role, Is.EqualTo(EnemySquadMemberRoleValue.Escort));
        Assert.That(screen.Anchor, Is.EqualTo(DeploymentAnchorValue.FrontTop));

        var classCounts = squad.Members
            .Select(member => Load<UnitArchetypeDefinition>($"Archetypes/archetype_{member.ArchetypeId}.asset").Class.Id)
            .GroupBy(classId => classId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var expectedPairs = pairedClassIds.Split(',');
        Assert.That(expectedPairs.All(classId => classCounts[classId] == 2), Is.True);
        Assert.That(classCounts.Where(pair => pair.Value >= 2).Select(pair => pair.Key), Is.EquivalentTo(expectedPairs));
    }

    [TestCase("site_ashen_gate", 5f)]
    [TestCase("site_sunken_bastion", 8f)]
    [TestCase("site_tithe_road", 8f)]
    [TestCase("site_ruined_crypts", 14f)]
    [TestCase("site_bone_orchard", 12f)]
    public void BossOverlays_ReplaceDecorativeSelfNerfsWithGuardedAndBarrier(string siteId, float barrier)
    {
        var suffix = siteId.Substring("site_".Length);
        var overlay = Load<BossOverlayDefinition>($"BossOverlays/boss_overlay_{suffix}.asset");
        Assert.That(overlay.AppliedStatuses.Select(status => status.StatusId), Is.EquivalentTo(new[] { "guarded", "barrier" }));
        Assert.That(overlay.AppliedStatuses.Single(status => status.StatusId == "guarded").DurationSeconds, Is.EqualTo(999f));
        Assert.That(overlay.AppliedStatuses.Single(status => status.StatusId == "barrier").Magnitude, Is.EqualTo(barrier));
        var removedSelfNerfs = new HashSet<string>(new[] { "marked", "silence", "burn", "exposed", "sunder", "wound" }, StringComparer.Ordinal);
        Assert.That(overlay.AppliedStatuses.Any(status => removedSelfNerfs.Contains(status.StatusId)), Is.False);
    }

    [Test]
    public void SunkenExecutor_IsNonDivingAndUsesLowestHpRangedAction()
    {
        var squad = Load<EnemySquadTemplateDefinition>("EnemySquads/site_sunken_bastion_boss_1_squad.asset");
        var executor = squad.Members.Single(member => member.ArchetypeId == "pale_executor_nondiving_boss");
        Assert.That(executor.Anchor, Is.EqualTo(DeploymentAnchorValue.FrontBottom));
        Assert.That(executor.RuleModifierTags, Does.Contain("duelist_hold_bruiser"));
        Assert.That(executor.RuleModifierTags, Does.Contain("execute_low_hp"));
        Assert.That(executor.RuleModifierTags, Does.Not.Contain("duelist_dive_commit"));

        var skill = Load<SkillDefinitionAsset>("Skills/skill_boss_low_hp_execution_shot.asset");
        Assert.That(skill.Delivery, Is.EqualTo(SkillDeliveryValue.Projectile));
        Assert.That(skill.TargetRule, Is.EqualTo(SkillTargetRuleValue.LowestHpEnemy));
        Assert.That(skill.TargetRuleData.Domain, Is.EqualTo(TargetDomain.EnemyUnit));
        Assert.That(skill.TargetRuleData.PrimarySelector, Is.EqualTo(TargetSelector.LowestHpPercentEnemy));
    }

    [Test]
    public void AuthoredSupportAndClockSkills_TargetTheIntendedSide()
    {
        var sustain = Load<SkillDefinitionAsset>("Skills/skill_boss_sustain_mend.asset");
        Assert.That(sustain.Kind, Is.EqualTo(SkillKindValue.Heal));
        Assert.That(sustain.TargetRuleData.Domain, Is.EqualTo(TargetDomain.AlliedUnit));
        Assert.That(sustain.TargetRuleData.PrimarySelector, Is.EqualTo(TargetSelector.LowestHpPercentAlly));

        foreach (var id in new[] { "skill_boss_ruined_attrition_hex", "skill_boss_orchard_burn_pulse" })
        {
            var skill = Load<SkillDefinitionAsset>($"Skills/{id}.asset");
            Assert.That(skill.TargetRuleData.Domain, Is.EqualTo(TargetDomain.EnemyUnit));
            Assert.That(skill.AreaEffectFamily, Is.EqualTo(AreaEffectFamilyValue.GroundAoe));
            Assert.That(skill.AppliedStatuses.Single().StatusId, Is.EqualTo("burn"));
            Assert.That(skill.AppliedStatuses.Single().DurationSeconds, Is.LessThan(1f));
        }

        var sunken = Load<SkillDefinitionAsset>("Skills/skill_sunken_anticluster_bombardment.asset");
        Assert.That(sunken.Power, Is.EqualTo(14f));
        Assert.That(sunken.PowerFlat, Is.EqualTo(14f));
        Assert.That(sunken.PunishCluster, Is.True);
        Assert.That(sunken.TargetRuleData.PreferredMinTargets, Is.EqualTo(3));
        Assert.That(sunken.CastWindupSeconds, Is.EqualTo(3.75f));

        foreach (var id in new[] { "skill_boss_ruined_attrition_hex", "skill_boss_orchard_burn_pulse" })
        {
            var skill = Load<SkillDefinitionAsset>($"Skills/{id}.asset");
            Assert.That(skill.RangeMax, Is.EqualTo(12f));
            Assert.That(skill.CastWindupSeconds, Is.EqualTo(3.35f));
            Assert.That(skill.BaseCooldownSeconds, Is.EqualTo(5.6f));
        }
    }

    [Test]
    public void SingleAdjustmentPass_UsesOnlyTheRatifiedNumericLevers()
    {
        var executor = Load<UnitArchetypeDefinition>("Archetypes/archetype_pale_executor.asset");
        var titheExecutor = Load<UnitArchetypeDefinition>("Archetypes/archetype_pale_executor_tithe_boss.asset");
        Assert.That(titheExecutor.BaseMaxHealth, Is.EqualTo(executor.BaseMaxHealth * 1.10f).Within(0.001f));

        var tithe = Load<EnemySquadTemplateDefinition>("EnemySquads/site_tithe_road_boss_1_squad.asset");
        Assert.That(tithe.Members.Select(member => member.ArchetypeId), Does.Contain("pale_executor_tithe_boss"));
    }

    [Test]
    public void ElitePreviews_ExposeTheUpcomingMechanicWithoutChangingAshen()
    {
        var sunken = Load<EnemySquadTemplateDefinition>("EnemySquads/site_sunken_bastion_elite_1_squad.asset");
        Assert.That(sunken.EnemyPosture, Is.EqualTo(TeamPostureTypeValue.HoldLine));

        var tithe = Load<EnemySquadTemplateDefinition>("EnemySquads/site_tithe_road_elite_1_squad.asset");
        Assert.That(
            tithe.Members.Single(member => member.ArchetypeId == "pale_executor").RuleModifierTags,
            Does.Contain("duelist_dive_commit"));

        var ruined = Load<EnemySquadTemplateDefinition>("EnemySquads/site_ruined_crypts_elite_1_squad.asset");
        var priest = ruined.Members.Single(member => member.ArchetypeId == "priest");
        Assert.That(priest.EquipmentItemBaseId, Is.EqualTo("item_priest_focus"));
        Assert.That(priest.EquipmentAffixIds, Does.Contain("affix_mender"));

        var orchard = Load<EnemySquadTemplateDefinition>("EnemySquads/site_bone_orchard_elite_1_squad.asset");
        Assert.That(orchard.Members.Select(member => member.ArchetypeId), Does.Contain("shaman_burn_boss"));
    }

    private static T Load<T>(string relativePath) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>($"{Root}/{relativePath}");
        Assert.That(asset, Is.Not.Null, relativePath);
        return asset!;
    }
}
