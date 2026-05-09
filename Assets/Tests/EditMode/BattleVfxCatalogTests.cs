using NUnit.Framework;
using SM.Combat.Model;
using SM.Unity;
using UnityEngine;

namespace SM.Tests.EditMode;

[Category("BatchOnly")]
public sealed class BattleVfxCatalogTests
{
    [Test]
    public void Surface_SpawnsCatalogPrefabAtResolvedSocketAndClearsIt()
    {
        var root = new GameObject("Wrapper");
        var prefab = new GameObject("HitDustTestPrefab");
        var catalog = ScriptableObject.CreateInstance<BattleVfxCatalog>();

        try
        {
            var hit = new GameObject("Hit").transform;
            hit.SetParent(root.transform, false);
            hit.localPosition = new Vector3(0f, 1.25f, 0.5f);

            var wrapper = root.AddComponent<BattleActorWrapper>();
            wrapper.ConfigureAuthoring(
                root.transform,
                root.transform,
                null,
                null,
                null,
                hit,
                null,
                null,
                null,
                null,
                null);

            catalog.SetEntry(
                BattlePresentationCueType.ImpactDamage,
                prefab,
                BattleActorSocketId.Hit,
                0.5f,
                new Vector3(0.1f, 0.2f, 0.3f),
                Vector3.zero,
                Vector3.one,
                parentToSocket: true);

            var surface = root.AddComponent<BattleActorVfxSurface>();
            surface.ConfigureCatalog(catalog);

            surface.ConsumeCue(new BattlePresentationCue(BattlePresentationCueType.ImpactDamage, 1, "actor"), wrapper);

            Assert.That(surface.TriggerCount, Is.EqualTo(1));
            Assert.That(surface.LastSocketId, Is.EqualTo(BattleActorSocketId.Hit));
            Assert.That(surface.LastSpawnedPrefabName, Is.EqualTo(prefab.name));
            Assert.That(surface.ActiveSpawnedVfxCount, Is.EqualTo(1));

            surface.ClearTransientState(BattlePresentationCueType.PlaybackReset);

            Assert.That(surface.ActiveSpawnedVfxCount, Is.EqualTo(0));
        }
        finally
        {
            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(prefab);
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void Surface_UsesFeetSocketForRepositionCueWhenCatalogHasNoPrefab()
    {
        var root = new GameObject("Wrapper");
        var catalog = ScriptableObject.CreateInstance<BattleVfxCatalog>();

        try
        {
            var feet = new GameObject("Feet").transform;
            feet.SetParent(root.transform, false);
            feet.localPosition = new Vector3(0.25f, -0.98f, 0.5f);

            var wrapper = root.AddComponent<BattleActorWrapper>();
            wrapper.ConfigureAuthoring(
                root.transform,
                root.transform,
                null,
                null,
                null,
                null,
                feet,
                null,
                null,
                null,
                null);

            var surface = root.AddComponent<BattleActorVfxSurface>();
            surface.ConfigureCatalog(catalog);

            surface.ConsumeCue(
                new BattlePresentationCue(
                    BattlePresentationCueType.RepositionStart,
                    2,
                    "actor",
                    AnimationSemantic: BattleAnimationSemantic.DashEngage),
                wrapper);

            Assert.That(surface.TriggerCount, Is.EqualTo(1));
            Assert.That(surface.LastSocketId, Is.EqualTo(BattleActorSocketId.FeetRing));
            Assert.That(surface.ActiveSpawnedVfxCount, Is.EqualTo(0));
        }
        finally
        {
            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void Catalog_ResolvesSemanticSpecificEntryBeforeGenericCueEntry()
    {
        var genericPrefab = new GameObject("GenericBasicAttackPrefab");
        var bowPrefab = new GameObject("BowShotPrefab");
        var catalog = ScriptableObject.CreateInstance<BattleVfxCatalog>();

        try
        {
            catalog.SetEntry(
                BattlePresentationCueType.ActionCommitBasic,
                genericPrefab,
                BattleActorSocketId.ProjectileOrigin,
                0.5f,
                Vector3.zero,
                Vector3.zero,
                Vector3.one,
                parentToSocket: false);

            catalog.SetEntry(
                BattlePresentationCueType.ActionCommitBasic,
                bowPrefab,
                BattleActorSocketId.ProjectileOrigin,
                0.5f,
                Vector3.zero,
                Vector3.zero,
                Vector3.one,
                parentToSocket: false,
                animationSemantic: BattleAnimationSemantic.BowShot);

            var hasBowEntry = catalog.TryResolve(
                new BattlePresentationCue(
                    BattlePresentationCueType.ActionCommitBasic,
                    1,
                    "actor",
                    AnimationSemantic: BattleAnimationSemantic.BowShot),
                out var bowEntry);
            var hasGenericEntry = catalog.TryResolve(
                new BattlePresentationCue(
                    BattlePresentationCueType.ActionCommitBasic,
                    1,
                    "actor"),
                out var genericEntry);

            Assert.That(hasBowEntry, Is.True);
            Assert.That(bowEntry.Prefab, Is.SameAs(bowPrefab));
            Assert.That(bowEntry.AnimationSemantic, Is.EqualTo(BattleAnimationSemantic.BowShot));
            Assert.That(hasGenericEntry, Is.True);
            Assert.That(genericEntry.Prefab, Is.SameAs(genericPrefab));
            Assert.That(genericEntry.AnimationSemantic, Is.EqualTo(BattleAnimationSemantic.None));
        }
        finally
        {
            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(bowPrefab);
            Object.DestroyImmediate(genericPrefab);
        }
    }

    [Test]
    public void Surface_SkipsBasicAttackWindupMagicCharge()
    {
        var root = new GameObject("Wrapper");
        var prefab = new GameObject("WindupMagicChargePrefab");
        var catalog = ScriptableObject.CreateInstance<BattleVfxCatalog>();

        try
        {
            var telegraph = new GameObject("Telegraph").transform;
            telegraph.SetParent(root.transform, false);

            var wrapper = root.AddComponent<BattleActorWrapper>();
            wrapper.ConfigureAuthoring(
                root.transform,
                root.transform,
                null,
                null,
                null,
                null,
                null,
                telegraph,
                null,
                null,
                null);

            catalog.SetEntry(
                BattlePresentationCueType.WindupEnter,
                prefab,
                BattleActorSocketId.Telegraph,
                0.5f,
                Vector3.zero,
                Vector3.zero,
                Vector3.one,
                parentToSocket: false);

            var surface = root.AddComponent<BattleActorVfxSurface>();
            surface.ConfigureCatalog(catalog);

            surface.ConsumeCue(
                new BattlePresentationCue(
                    BattlePresentationCueType.WindupEnter,
                    1,
                    "actor",
                    ActionType: BattleActionType.BasicAttack),
                wrapper);

            Assert.That(surface.TriggerCount, Is.EqualTo(0));
            Assert.That(surface.ActiveSpawnedVfxCount, Is.EqualTo(0));

            surface.ConsumeCue(
                new BattlePresentationCue(
                    BattlePresentationCueType.WindupEnter,
                    2,
                    "actor",
                    ActionType: BattleActionType.ActiveSkill),
                wrapper);

            Assert.That(surface.TriggerCount, Is.EqualTo(1));
            Assert.That(surface.LastSpawnedPrefabName, Is.EqualTo(prefab.name));
        }
        finally
        {
            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(prefab);
            Object.DestroyImmediate(root);
        }
    }
}
