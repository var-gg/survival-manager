using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SM.Meta.Model;
using SM.Unity;
using SM.Unity.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SM.Tests.PlayMode;

/// <summary>
/// wave-55 — wave-50-functional fix 결과를 PlayMode에서 검증.
/// 기존 <see cref="UxBiblePlayModeWitnessTests"/>가 panel open/close path 대부분을 cover하므로
/// 본 class는 빈자리만 채운다:
/// - Character Sheet 4 CTA bindings click (wave-50 P2)
/// - RosterGrid raw archetype id 노출 없음 (wave-50 P0a)
/// - EquipmentRefit pool 비어있지 않음 (wave-50 P0b SeedDevDemoInventoryIfEmpty)
///
/// helper는 <see cref="UxBiblePlayModeWitnessTests"/>의 private static 패턴을 inline 차용 (asmdef 동일,
/// helper public 노출은 wave-55 scope 외).
/// </summary>
public sealed class DemoAlphaWalkTests
{
    private static readonly string[] KnownRawArchetypeIds =
    {
        "warden", "duelist", "ranger", "mystic", "vanguard",
        "raider", "hexer", "marksman", "priest", "shaman",
    };

    [UnitySetUp]
    public IEnumerator ResetRoot()
    {
        if (GameSessionRoot.Instance != null)
        {
            Object.Destroy(GameSessionRoot.Instance.gameObject);
        }

        var guard = 0;
        while (GameSessionRoot.Instance != null && guard++ < 10)
        {
            yield return null;
        }
    }

    [UnityTest]
    public IEnumerator Demo_CharacterSheet_4CTA_Bindings_All_Click_Without_Exception()
    {
        yield return EnterOfflineTownFromBoot();
        var town = RequireAny<TownScreenController>("Town controller should exist for 4 CTA witness.");
        town.EnsureRuntimeControls();
        yield return WaitFrames(2);

        var townHost = RequirePanelHost("TownRuntimePanelHost");
        var heroId = GameSessionRoot.Instance!.SessionState.Profile.Heroes.First().HeroId;
        ClickButton(townHost.Root, $"FaceCard_{heroId}");
        yield return WaitForVisible(townHost.Root, "TownCharacterSheetRoot");

        // 4 CTA element 존재 확인 — wave-50 P2 UXML element 추가
        Assert.That(townHost.Root.Q<Button>("TcsDismissButton"), Is.Not.Null,
            "TcsDismissButton 누락 (wave-50 P2 UXML 신규 element).");
        Assert.That(townHost.Root.Q<Button>("TcsRetrainButton"), Is.Not.Null,
            "TcsRetrainButton 누락 (wave-50 P2 UXML 신규 element).");
        Assert.That(townHost.Root.Q<Button>("TcsPassiveBoardButton"), Is.Not.Null,
            "TcsPassiveBoardButton 누락 (wave-50 P2 UXML 신규 element).");
        Assert.That(townHost.Root.Q<Button>("TcsRefitButton"), Is.Not.Null,
            "TcsRefitButton 누락 (wave-50 P2 UXML 신규 element).");

        // dismiss / retrain click — controller wire는 callback 미연결 (Debug.Log placeholder).
        // 예외 없이 click 통과하면 binding 성공.
        ClickButton(townHost.Root, "TcsDismissButton");
        yield return WaitFrames(1);
        ClickButton(townHost.Root, "TcsRetrainButton");
        yield return WaitFrames(1);

        // passive board click — cross-presenter Open (TownScreenController BindActionBar wire).
        // PassiveBoard panel이 PbpRoot으로 visible화돼야 한다.
        ClickButton(townHost.Root, "TcsPassiveBoardButton");
        yield return WaitForVisible(townHost.Root, "PbpRoot");
        ClickFirstButtonWithClass(townHost.Root, "pbp-footer__close");
        yield return WaitForHidden(townHost.Root, "PbpRoot");

        // refit click — cross-presenter Open. EquipmentRefit ErpRoot visible.
        ClickButton(townHost.Root, "TcsRefitButton");
        yield return WaitForVisible(townHost.Root, "ErpRoot");
        ClickFirstButtonWithClass(townHost.Root, "erp-header__close");
        yield return WaitForHidden(townHost.Root, "ErpRoot");

        ClickButton(townHost.Root, "TownCharacterSheetCloseButton");
        yield return WaitForHidden(townHost.Root, "TownCharacterSheetRoot");
    }

    [UnityTest]
    public IEnumerator Demo_RosterGrid_No_Raw_Archetype_Id_Leak()
    {
        yield return EnterOfflineTownFromBoot();
        var town = RequireAny<TownScreenController>("Town controller should exist for roster grid raw id witness.");
        town.EnsureRuntimeControls();
        yield return WaitFrames(2);

        var townHost = RequirePanelHost("TownRuntimePanelHost");
        ClickButton(townHost.Root, "RosterButton");
        yield return WaitForVisible(townHost.Root, "RosterGridPreviewRoot");

        // hero card name label scan — wave-50 P0a fix 검증.
        // h.Name이 "warden" 같은 raw archetype.Id가 들어가던 것을 ContentTextResolver.GetCharacterName
        // 거치도록 변경. ko locale이 채워져 있으면 한국어 표시명, 비어 있으면 HumanizeIdentifier
        // ("Warden" 등 영문 capitalized)로 fallback. 어느 쪽이든 raw lower-case archetype id
        // ("warden", "duelist" 등)가 그대로 보이면 fail.
        var gridContainer = townHost.Root.Q<VisualElement>("GridContainer");
        Assert.That(gridContainer, Is.Not.Null, "GridContainer 누락 — RosterGrid UXML 깨졌나 확인.");

        var nameLabels = gridContainer!.Query<Label>(className: "sm-hpc__name-ko").ToList();
        Assert.That(nameLabels.Count, Is.GreaterThan(0),
            "RosterGrid에 hero card name label이 없음 — RosterGridView가 render 못함.");

        var offenders = new List<string>();
        foreach (var label in nameLabels)
        {
            var text = (label.text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(text)) continue;
            var lower = text.ToLowerInvariant();
            if (KnownRawArchetypeIds.Any(id => string.Equals(lower, id, StringComparison.Ordinal)))
            {
                offenders.Add($"hero card name='{text}' is raw archetype id");
            }
        }

        Assert.That(offenders, Is.Empty,
            $"RosterGrid hero card name labels에 raw archetype id 노출 — wave-50 P0a fix 효과 없음: {string.Join(" | ", offenders.Take(5))}");

        ClickFirstButtonWithClass(townHost.Root, "rgp-header__close");
        yield return WaitForHidden(townHost.Root, "RosterGridPreviewRoot");
    }

    [UnityTest]
    public IEnumerator Demo_EquipmentRefit_PoolHasItems_AfterSeedDevInventory()
    {
        yield return EnterOfflineTownFromBoot();
        var town = RequireAny<TownScreenController>("Town controller should exist for refit pool witness.");
        town.EnsureRuntimeControls();
        yield return WaitFrames(2);

        var townHost = RequirePanelHost("TownRuntimePanelHost");
        ClickButton(townHost.Root, "FaceCard_soemae");
        yield return WaitForVisible(townHost.Root, "ErpRoot");

        // pool row count > 0 — wave-50 P0b SeedDevDemoInventoryIfEmpty 효과 검증.
        // EquipmentRefitView.RenderPool이 각 InventoryItemRecord마다 .erp-pool-row VisualElement를 추가.
        // SeedDemoProfile은 4 hero에만 item을 주므로 pool은 4행. SeedDevDemoInventoryIfEmpty가
        // 비어있을 때 hero count만큼 채워주므로 최소 4행 이상 보장.
        var refitRoot = townHost.Root.Q<VisualElement>("ErpRoot");
        Assert.That(refitRoot, Is.Not.Null, "ErpRoot 누락.");

        var poolRows = refitRoot!.Query<VisualElement>(className: "erp-pool-row").ToList();
        Assert.That(poolRows.Count, Is.GreaterThan(0),
            "EquipmentRefit pool 비어있음 — wave-50 P0b SeedDevDemoInventoryIfEmpty 효과 없음 또는 RenderPool 실패.");

        ClickFirstButtonWithClass(townHost.Root, "erp-header__close");
        yield return WaitForHidden(townHost.Root, "ErpRoot");
    }

    // ---------------------------------------------------------------
    // helpers (UxBiblePlayModeWitnessTests 패턴 차용, 본 class에 필요 최소만 inline)
    // ---------------------------------------------------------------

    private static IEnumerator EnterOfflineTownFromBoot()
    {
        SceneManager.LoadScene(SceneNames.Boot);
        yield return WaitForScene(SceneNames.Boot);
        yield return WaitForCondition(() => GameSessionRoot.Instance != null, 8f);

        var root = GameSessionRoot.Instance!;
        root.UseDedicatedSmokeNamespace();
        Assert.That(root.StartRealm(SessionRealm.OfflineLocal, out var error), Is.True, error);
        root.SessionState.AbandonExpeditionRun();
        var checkpoint = root.SaveProfile(SessionCheckpointKind.ManualSave);
        Assert.That(checkpoint.IsSuccessful, Is.True, checkpoint.Message);
        root.SceneFlow.GoToTown();

        yield return WaitForScene(SceneNames.Town);
        yield return WaitForComponent<TownScreenController>();
    }

    private static IEnumerator WaitForScene(string sceneName, float timeout = 8f)
    {
        var elapsed = 0f;
        while (SceneManager.GetActiveScene().name != sceneName && elapsed < timeout)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(sceneName));
        yield return null;
    }

    private static IEnumerator WaitForComponent<T>() where T : Component
    {
        var elapsed = 0f;
        while (FindAny<T>() == null && elapsed < 8f)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        Assert.That(FindAny<T>(), Is.Not.Null);
    }

    private static IEnumerator WaitForCondition(Func<bool> predicate, float timeout)
    {
        var elapsed = 0f;
        while (!predicate() && elapsed < timeout)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        Assert.That(predicate(), Is.True);
    }

    private static IEnumerator WaitForVisible(VisualElement root, string name)
    {
        yield return WaitForCondition(() =>
        {
            var element = root.Q<VisualElement>(name);
            return element != null
                   && IsEffectivelyVisible(element)
                   && element.worldBound.width > 8f
                   && element.worldBound.height > 8f;
        }, 3f);
    }

    private static IEnumerator WaitForHidden(VisualElement root, string name)
    {
        yield return WaitForCondition(() =>
        {
            var element = root.Q<VisualElement>(name);
            return element != null && element.style.display.value == DisplayStyle.None;
        }, 3f);
    }

    private static IEnumerator WaitFrames(int frameCount)
    {
        for (var i = 0; i < frameCount; i++)
        {
            yield return null;
        }
    }

    private static void ClickButton(VisualElement root, string name)
    {
        var button = Require<Button>(root, name);
        InvokeButton(button);
    }

    private static void ClickFirstButtonWithClass(VisualElement root, string className)
    {
        var button = root.Query<Button>(className: className).First();
        Assert.That(button, Is.Not.Null, $"Button with class '{className}' should exist.");
        InvokeButton(button);
    }

    private static void InvokeButton(Button button)
    {
        Assert.That(button.enabledInHierarchy, Is.True, $"{button.name} should be enabled before click.");
        using var evt = ClickEvent.GetPooled();
        var invoke = typeof(Clickable).GetMethod("Invoke", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(invoke, Is.Not.Null, "Clickable.Invoke should be available for PlayMode button witness.");
        invoke!.Invoke(button.clickable, new object[] { evt });
    }

    private static bool IsEffectivelyVisible(VisualElement element)
    {
        for (var current = element; current != null; current = current.parent)
        {
            if (current.style.display.value == DisplayStyle.None
                || current.resolvedStyle.display == DisplayStyle.None)
            {
                return false;
            }
        }

        return true;
    }

    private static T Require<T>(VisualElement root, string name) where T : VisualElement
    {
        return root.Q<T>(name) ?? throw new AssertionException($"Missing UITK element '{name}'.");
    }

    private static T RequireAny<T>(string message) where T : Component
    {
        var component = FindAny<T>();
        Assert.That(component, Is.Not.Null, message);
        return component!;
    }

    private static T? FindAny<T>() where T : Component
    {
        var active = Object.FindObjectOfType<T>();
        if (active != null)
        {
            return active;
        }

        return Resources.FindObjectsOfTypeAll<T>().FirstOrDefault(component =>
            component.gameObject.scene.IsValid());
    }

    private static RuntimePanelHost RequirePanelHost(string objectName)
    {
        var host = Resources.FindObjectsOfTypeAll<RuntimePanelHost>()
            .FirstOrDefault(candidate => candidate.gameObject.scene.IsValid() && candidate.gameObject.name == objectName);
        Assert.That(host, Is.Not.Null, $"{objectName} should exist.");
        host!.EnsureReady();
        return host;
    }
}
