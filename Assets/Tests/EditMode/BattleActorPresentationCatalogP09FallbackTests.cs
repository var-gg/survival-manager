using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Content.Definitions;
using SM.Unity;
using UnityEngine;

namespace SM.Tests.EditMode;

[Category("BatchOnly")]
public sealed class BattleActorPresentationCatalogP09FallbackTests
{
    private const string EnemySquadResourcesFolder = "_Game/Content/Definitions/EnemySquads";
    private const string ArchetypeResourcesFolder = "_Game/Content/Definitions/Archetypes";

    [Test]
    public void FallbackCatalog_EveryEnemySquadMember_ResolvesArmedP09Wrapper()
    {
        var squads = Resources.LoadAll<EnemySquadTemplateDefinition>(EnemySquadResourcesFolder);
        Assume.That(squads, Is.Not.Empty, "EnemySquads 리소스를 찾지 못했다.");

        var appearanceCatalog = Resources.Load<BattleP09AppearanceCatalog>(BattleP09AppearanceCatalog.ResourcesPath);
        Assume.That(appearanceCatalog, Is.Not.Null, "P09AppearanceCatalog 리소스를 찾지 못했다.");

        var presetCharacterIds = Resources
            .LoadAll<BattleP09AppearancePreset>(BattleP09AppearancePreset.ResourcesFolder)
            .Where(preset => preset != null && !string.IsNullOrWhiteSpace(preset.CharacterId))
            .Select(preset => preset.CharacterId)
            .ToHashSet(StringComparer.Ordinal);
        var armamentMeshNames = CollectArmamentMeshNames(appearanceCatalog);
        Assume.That(armamentMeshNames, Is.Not.Empty);

        var preexistingWrappers = new HashSet<BattleActorWrapper>(Resources.FindObjectsOfTypeAll<BattleActorWrapper>());
        BattleActorPresentationCatalog catalog = null;
        try
        {
            catalog = BattleActorPresentationCatalog.TryCreateEditorP09FallbackCatalog();
            Assume.That(catalog, Is.Not.Null, "P09 비주얼 프리팹이 없어 editor fallback 카탈로그를 만들 수 없다.");

            foreach (var squad in squads)
            {
                foreach (var member in squad.Members)
                {
                    var label = $"{squad.Id}/{member.Id} (archetype={member.ArchetypeId}, character={member.CharacterId})";
                    var wrapper = catalog!.ResolveWrapperPrefab(CreateEnemy(member.ArchetypeId, member.CharacterId));

                    if (!string.IsNullOrWhiteSpace(member.CharacterId)
                        && !presetCharacterIds.Contains(member.CharacterId))
                    {
                        var archetypeWrapper = catalog.ResolveWrapperPrefab(CreateEnemy(member.ArchetypeId, string.Empty));
                        Assert.That(
                            wrapper,
                            Is.SameAs(archetypeWrapper),
                            $"{label}: 전용 프리셋 없는 캐릭터는 archetype wrapper로 떨어져야 한다.");
                    }

                    Assert.That(
                        HasActiveArmamentMesh(wrapper.transform, armamentMeshNames),
                        Is.True,
                        $"{label}: 무기/방패 메시가 켜진 wrapper로 해석되어야 한다.");
                }
            }
        }
        finally
        {
            CleanupCreatedWrappers(preexistingWrappers);
            if (catalog != null)
            {
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }
    }

    private static HashSet<string> CollectArmamentMeshNames(BattleP09AppearanceCatalog appearanceCatalog)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var option in appearanceCatalog.Options)
        {
            if (option.Type != BattleP09AppearancePartType.Weapon
                && option.Type != BattleP09AppearancePartType.Shield)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(option.MeshName))
            {
                continue;
            }

            if (option.MeshName.Contains('{'))
            {
                AddFormatted(names, option.MeshName, "Male");
                AddFormatted(names, option.MeshName, "Female");
                AddFormatted(names, option.MeshName, "Fem");
            }
            else
            {
                names.Add(option.MeshName);
            }
        }

        return names;
    }

    private static void AddFormatted(ISet<string> names, string format, string value)
    {
        try
        {
            names.Add(string.Format(format, value));
        }
        catch (FormatException)
        {
        }
    }

    private static bool HasActiveArmamentMesh(Transform root, ISet<string> armamentMeshNames)
    {
        foreach (var child in root.GetComponentsInChildren<Transform>(true))
        {
            if (armamentMeshNames.Contains(child.name) && IsActiveBelowRoot(child, root))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsActiveBelowRoot(Transform node, Transform root)
    {
        // wrapper 템플릿 루트는 의도적으로 비활성 상태라 루트 자신은 제외하고 판정한다.
        for (var current = node; current != null && current != root; current = current.parent)
        {
            if (!current.gameObject.activeSelf)
            {
                return false;
            }
        }

        return true;
    }

    private static void CleanupCreatedWrappers(HashSet<BattleActorWrapper> preexistingWrappers)
    {
        foreach (var wrapper in Resources.FindObjectsOfTypeAll<BattleActorWrapper>())
        {
            if (wrapper == null || preexistingWrappers.Contains(wrapper))
            {
                continue;
            }

            UnityEngine.Object.DestroyImmediate(wrapper.gameObject);
        }
    }

    private static BattleUnitReadModel CreateEnemy(string archetypeId, string characterId)
    {
        return new BattleUnitReadModel(
            Id: "enemy",
            Name: "Enemy",
            Side: TeamSide.Enemy,
            Anchor: DeploymentAnchorId.BackCenter,
            RaceId: "human",
            ClassId: "vanguard",
            Position: new CombatVector2(1f, 0f),
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

    /// <summary>
    /// 보스 아키타입이 시각 매핑 없이 콘텐츠에 추가되면 <b>조용히</b> 프리미티브로 렌더된다.
    /// 실제로 보스 8종 중 7종이 그 상태로 출고돼 있었고, 컴파일도 테스트도 아무것도 잡지 못했다.
    /// 이 가드는 밸런스 밴드가 아니라 <b>모순</b>을 잡는다 — 콘텐츠에 보스가 있는데 표에 없으면 실패.
    /// </summary>
    [Test]
    public void EditorP09Fallback_EveryBossArchetype_HasVisualBaseWithPreset()
    {
        var archetypes = Resources.LoadAll<UnitArchetypeDefinition>(ArchetypeResourcesFolder);
        Assume.That(archetypes, Is.Not.Empty, "Archetypes 리소스를 찾지 못했다.");

        var presetCharacterIds = Resources
            .LoadAll<BattleP09AppearancePreset>(BattleP09AppearancePreset.ResourcesFolder)
            .Where(preset => preset != null && !string.IsNullOrWhiteSpace(preset.CharacterId))
            .Select(preset => preset.CharacterId)
            .ToHashSet(StringComparer.Ordinal);
        Assume.That(presetCharacterIds, Is.Not.Empty, "P09 프리셋을 찾지 못했다.");

        var map = BattleActorPresentationCatalog.EditorP09BossArchetypeBaseMap
            .ToDictionary(entry => entry.BossArchetypeId, entry => entry.BaseCharacterId, StringComparer.Ordinal);

        var bossArchetypeIds = archetypes
            .Where(archetype => archetype != null && !string.IsNullOrWhiteSpace(archetype.Id))
            .Select(archetype => archetype.Id)
            .Where(id => id.EndsWith("_boss", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        Assume.That(bossArchetypeIds, Is.Not.Empty, "보스 아키타입을 하나도 찾지 못했다.");

        var unmapped = bossArchetypeIds.Where(id => !map.ContainsKey(id)).ToArray();
        Assert.That(
            unmapped,
            Is.Empty,
            "보스 아키타입에 P09 시각 매핑이 없다. 이대로면 전투에서 프리미티브 도형으로 보인다. " +
            $"BattleActorPresentationCatalog.EditorP09BossArchetypeBases 에 항목을 추가하라: {string.Join(", ", unmapped)}");

        var danglingBase = map
            .Where(pair => bossArchetypeIds.Contains(pair.Key, StringComparer.Ordinal))
            .Where(pair => !presetCharacterIds.Contains(pair.Value))
            .Select(pair => $"{pair.Key} -> {pair.Value}")
            .ToArray();
        Assert.That(
            danglingBase,
            Is.Empty,
            "보스가 가리키는 기반 캐릭터의 P09 프리셋이 없다. 프리셋 이름이 바뀌었거나 지워졌다. " +
            $"프리셋을 만들거나 매핑을 고쳐라: {string.Join(", ", danglingBase)}");

        var staleMapping = map.Keys
            .Where(id => !bossArchetypeIds.Contains(id, StringComparer.Ordinal))
            .ToArray();
        Assert.That(
            staleMapping,
            Is.Empty,
            "표에는 있는데 콘텐츠에 없는 보스 아키타입이다. 아키타입이 지워졌거나 id가 바뀌었다. " +
            $"표에서 지워라: {string.Join(", ", staleMapping)}");
    }
}
