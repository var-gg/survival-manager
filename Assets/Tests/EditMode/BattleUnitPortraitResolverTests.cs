using NUnit.Framework;
using SM.Combat.Model;
using SM.Unity.UI.Battle;
using UnityEngine;

namespace SM.Tests.EditMode;

[Category("BatchOnly")]
public sealed class BattleUnitPortraitResolverTests
{
    [TestCase("hero_dawn_priest")]
    [TestCase("hero_pack_raider")]
    [TestCase("hero_grave_hexer")]
    public void Resolve_ReturnsDefaultPortrait_ForPromotedBattleCharacters(string characterId)
    {
        var resolver = new BattleUnitPortraitResolver();

        var portrait = resolver.Resolve(CreateUnit(characterId));

        Assert.That(portrait, Is.Not.Null);
        Assert.That(portrait!.name, Is.EqualTo("portrait_face_serious"));
    }

    [TestCase("hero_dawn_priest", "skill_priest_core", "skill_icon_sigil_shield")]
    [TestCase("hero_dawn_priest", "skill_minor_heal", "skill_icon_platinum_aegis")]
    [TestCase("hero_pack_raider", "skill_raider_core", "skill_icon_wind_read")]
    [TestCase("hero_pack_raider", "skill_power_strike", "skill_icon_fang_strike")]
    [TestCase("hero_grave_hexer", "skill_hexer_core", "skill_icon_time_distance")]
    [TestCase("hero_grave_hexer", "skill_minor_heal", "skill_icon_memory_project")]
    public void ResolveSkillIcon_UsesCharacterSpecificAliases(string characterId, string skillId, string expectedTextureName)
    {
        var resolver = new BattleUnitPortraitResolver();

        var icon = resolver.ResolveSkillIcon(characterId, skillId);

        Assert.That(icon, Is.Not.Null);
        Assert.That(icon!.name, Is.EqualTo(expectedTextureName));
    }

    private static BattleUnitReadModel CreateUnit(string characterId)
    {
        return new BattleUnitReadModel(
            Id: characterId,
            Name: characterId,
            Side: TeamSide.Ally,
            Anchor: DeploymentAnchorId.FrontCenter,
            RaceId: "human",
            ClassId: "vanguard",
            Position: new CombatVector2(0f, 0f),
            CurrentHealth: 20f,
            MaxHealth: 20f,
            IsAlive: true,
            ActionState: CombatActionState.AcquireTarget,
            PendingActionType: BattleActionType.BasicAttack,
            TargetId: null,
            TargetName: null,
            WindupProgress: 0f,
            CooldownRemaining: 0f,
            CurrentEnergy: 0f,
            MaxEnergy: 100f,
            IsDefending: false,
            CharacterId: characterId);
    }
}
