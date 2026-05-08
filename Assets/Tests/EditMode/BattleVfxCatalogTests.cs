using NUnit.Framework;
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
}
