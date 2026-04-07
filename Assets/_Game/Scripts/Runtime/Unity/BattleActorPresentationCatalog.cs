using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using UnityEngine;

namespace SM.Unity;

[CreateAssetMenu(menuName = "SM/Battle/Battle Actor Presentation Catalog", fileName = "BattleActorPresentationCatalog")]
public sealed class BattleActorPresentationCatalog : ScriptableObject
{
    public const string ResourcesPath = "_Game/Battle/BattleActorPresentationCatalog";

    [SerializeField] private BattleActorWrapper defaultPrimitiveWrapper = null!;
    [SerializeField] private BattleActorWrapper allyDefaultWrapper = null!;
    [SerializeField] private BattleActorWrapper enemyDefaultWrapper = null!;
    [SerializeField] private List<BattleActorPresentationEntry> characterOverrides = new();
    [SerializeField] private List<BattleActorPresentationEntry> archetypeOverrides = new();

    private static BattleActorPresentationCatalog? _runtimeFallback;

    public void SetDefaultWrapper(BattleActorWrapper wrapper)
    {
        defaultPrimitiveWrapper = wrapper;
    }

    public void SetTeamDefaultWrapper(TeamSide side, BattleActorWrapper wrapper)
    {
        if (side == TeamSide.Ally)
        {
            allyDefaultWrapper = wrapper;
            return;
        }

        enemyDefaultWrapper = wrapper;
    }

    public void SetCharacterOverride(string characterId, BattleActorWrapper wrapper)
    {
        Upsert(characterOverrides, characterId, wrapper);
    }

    public void SetArchetypeOverride(string archetypeId, BattleActorWrapper wrapper)
    {
        Upsert(archetypeOverrides, archetypeId, wrapper);
    }

    public BattleActorWrapper ResolveWrapperPrefab(BattleUnitReadModel actor)
    {
        if (TryResolve(characterOverrides, actor.CharacterId, out var characterWrapper))
        {
            return characterWrapper;
        }

        if (TryResolve(archetypeOverrides, actor.ArchetypeId, out var archetypeWrapper))
        {
            return archetypeWrapper;
        }

        var sideDefault = actor.Side == TeamSide.Ally ? allyDefaultWrapper : enemyDefaultWrapper;
        if (sideDefault != null)
        {
            return sideDefault;
        }

        if (defaultPrimitiveWrapper != null)
        {
            return defaultPrimitiveWrapper;
        }

        throw new InvalidOperationException($"No battle actor wrapper could be resolved for actor '{actor.Id}'.");
    }

    public static BattleActorPresentationCatalog ResolveRuntimeCatalog(BattleActorPresentationCatalog? configuredCatalog)
    {
        if (configuredCatalog != null)
        {
            return configuredCatalog;
        }

        var loaded = Resources.Load<BattleActorPresentationCatalog>(ResourcesPath);
        if (loaded != null)
        {
            return loaded;
        }

        _runtimeFallback ??= CreateRuntimeFallbackCatalog();
        return _runtimeFallback;
    }

    private static bool TryResolve(
        IEnumerable<BattleActorPresentationEntry> entries,
        string id,
        out BattleActorWrapper wrapper)
    {
        wrapper = null!;
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        var match = entries.FirstOrDefault(entry => string.Equals(entry.Id, id, StringComparison.Ordinal));
        if (match == null || match.WrapperPrefab == null)
        {
            return false;
        }

        wrapper = match.WrapperPrefab;
        return true;
    }

    private static void Upsert(ICollection<BattleActorPresentationEntry> entries, string id, BattleActorWrapper wrapper)
    {
        var existing = entries.FirstOrDefault(entry => string.Equals(entry.Id, id, StringComparison.Ordinal));
        if (existing != null)
        {
            existing.WrapperPrefab = wrapper;
            return;
        }

        entries.Add(new BattleActorPresentationEntry
        {
            Id = id,
            WrapperPrefab = wrapper,
        });
    }

    private static BattleActorPresentationCatalog CreateRuntimeFallbackCatalog()
    {
        var catalog = CreateInstance<BattleActorPresentationCatalog>();
        catalog.hideFlags = HideFlags.DontSave;
        catalog.defaultPrimitiveWrapper = CreateRuntimePrimitiveWrapperTemplate();
        return catalog;
    }

    private static BattleActorWrapper CreateRuntimePrimitiveWrapperTemplate()
    {
        var root = new GameObject("BattleActor_PrimitiveWrapper_RuntimeTemplate");
        root.hideFlags = HideFlags.DontSave;

        var wrapper = root.AddComponent<BattleActorWrapper>();
        root.AddComponent<BattleActorView>();
        var adapter = root.AddComponent<BattlePrimitiveActorVisualAdapter>();
        root.AddComponent<BattleAnimationEventBridge>();
        root.AddComponent<BattleActorVfxSurface>();
        root.AddComponent<BattleActorAudioSurface>();

        var socketRig = CreateChild(root.transform, "SocketRig");
        var center = CreateChild(socketRig, "Center");
        center.localPosition = new Vector3(0f, 0.10f, 0f);
        var feet = CreateChild(socketRig, "Feet");
        feet.localPosition = new Vector3(0f, -0.98f, 0f);
        var telegraph = CreateChild(socketRig, "Telegraph");
        telegraph.localPosition = feet.localPosition;
        var cameraFocus = CreateChild(socketRig, "CameraFocus");
        cameraFocus.localPosition = center.localPosition;

        var visualRoot = CreateChild(root.transform, "VisualRoot");
        var vendorSlot = CreateChild(visualRoot, "VendorVisualSlot");

        wrapper.ConfigureAuthoring(
            visualRoot,
            vendorSlot,
            center,
            null,
            null,
            null,
            feet,
            telegraph,
            null,
            null,
            cameraFocus);
        adapter.ConfigureAuthoring(visualRoot, null, null, true);
        return wrapper;
    }

    private static Transform CreateChild(Transform parent, string name)
    {
        var child = new GameObject(name).transform;
        child.SetParent(parent, false);
        child.gameObject.hideFlags = HideFlags.DontSave;
        return child;
    }
}

[Serializable]
public sealed class BattleActorPresentationEntry
{
    public string Id = string.Empty;
    public BattleActorWrapper WrapperPrefab = null!;
}
