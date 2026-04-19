using NUnit.Framework;
using SM.Combat.Model;
using SM.Unity;
using UnityEngine;

namespace SM.Tests.EditMode;

[Category("BatchOnly")]
public sealed class BattleActorPresentationCatalogTests
{
    [Test]
    public void Resolve_PrefersCharacterThenArchetypeThenSideThenDefault()
    {
        var defaultGo = new GameObject("Default");
        var sideGo = new GameObject("Side");
        var archetypeGo = new GameObject("Archetype");
        var characterGo = new GameObject("Character");
        var catalog = ScriptableObject.CreateInstance<BattleActorPresentationCatalog>();

        try
        {
            var defaultWrapper = defaultGo.AddComponent<BattleActorWrapper>();
            var sideWrapper = sideGo.AddComponent<BattleActorWrapper>();
            var archetypeWrapper = archetypeGo.AddComponent<BattleActorWrapper>();
            var characterWrapper = characterGo.AddComponent<BattleActorWrapper>();

            catalog.SetDefaultWrapper(defaultWrapper);
            catalog.SetTeamDefaultWrapper(TeamSide.Enemy, sideWrapper);
            catalog.SetArchetypeOverride("guardian", archetypeWrapper);
            catalog.SetCharacterOverride("boss_alpha", characterWrapper);

            Assert.That(catalog.ResolveWrapperPrefab(CreateUnit(TeamSide.Enemy, "guardian", "boss_alpha")), Is.SameAs(characterWrapper));
            Assert.That(catalog.ResolveWrapperPrefab(CreateUnit(TeamSide.Enemy, "guardian", string.Empty)), Is.SameAs(archetypeWrapper));
            Assert.That(catalog.ResolveWrapperPrefab(CreateUnit(TeamSide.Enemy, string.Empty, string.Empty)), Is.SameAs(sideWrapper));
            Assert.That(catalog.ResolveWrapperPrefab(CreateUnit(TeamSide.Ally, string.Empty, string.Empty)), Is.SameAs(defaultWrapper));
        }
        finally
        {
            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(characterGo);
            Object.DestroyImmediate(archetypeGo);
            Object.DestroyImmediate(sideGo);
            Object.DestroyImmediate(defaultGo);
        }
    }

    private static BattleUnitReadModel CreateUnit(TeamSide side, string archetypeId, string characterId)
    {
        return new BattleUnitReadModel(
            Id: "actor",
            Name: "Actor",
            Side: side,
            Anchor: side == TeamSide.Ally ? DeploymentAnchorId.FrontCenter : DeploymentAnchorId.BackCenter,
            RaceId: "human",
            ClassId: "vanguard",
            Position: side == TeamSide.Ally ? new CombatVector2(-1f, 0f) : new CombatVector2(1f, 0f),
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
            ArchetypeId: archetypeId,
            CharacterId: characterId);
    }
}
