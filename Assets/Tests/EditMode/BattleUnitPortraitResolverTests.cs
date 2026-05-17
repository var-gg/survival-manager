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

    [TestCase("hero_dawn_priest")]
    [TestCase("hero_pack_raider")]
    [TestCase("hero_grave_hexer")]
    public void ResolveFullBody_PrefersFullPortrait_ForPromotedBattleCharacters(string characterId)
    {
        var resolver = new BattleUnitPortraitResolver();

        var portrait = resolver.ResolveFullBody(CreateUnit(characterId));

        Assert.That(portrait, Is.Not.Null);
        Assert.That(portrait!.name, Is.EqualTo("portrait_full"));
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
