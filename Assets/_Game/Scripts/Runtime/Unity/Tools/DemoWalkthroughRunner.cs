using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using SM.Meta.Model;
using SM.Unity.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SM.Unity.Tools;

/// <summary>
/// wave-62-2 — 5분 시연 영상 walkthrough automation prototype.
/// PlayMode 진입 후 Editor menu trigger 가 본 component 를 Boot scene root 에 attach + StartCoroutine.
/// cut sheet 순서로 panel 자동 navigate (button click + visual hold + close). 완료 시 OnComplete 콜백
/// 호출 → DemoVideoCaptureTool 이 record stop + PlayMode exit.
///
/// 본 1차 prototype 은 3 panel scope: Town hub (3s) → Recruit (8s) → Roster (8s) → Complete.
/// 총 약 35~40초 walkthrough. full 5분 cut sheet (Recruit / Roster / CharSheet / Squad / Atlas /
/// Battle / Reward / Town 복귀) 는 본 prototype 검증 후 별도 wave.
///
/// helper 는 DemoAlphaWalkTests 의 NUnit 의존 패턴을 Debug.LogError 기반으로 reskin (Runtime asmdef 라
/// NUnit 사용 불가). assertion fail 은 walkthrough 중단 + OnComplete(success: false) 콜백.
/// </summary>
[DisallowMultipleComponent]
public sealed class DemoWalkthroughRunner : MonoBehaviour
{
    public Action<bool>? OnComplete;

    private bool _started;

    private void Start()
    {
        if (_started) return;
        _started = true;
        StartCoroutine(RunWalkthroughCoroutine());
    }

    private IEnumerator RunWalkthroughCoroutine()
    {
        Debug.Log("[DemoWalkthroughRunner] Walkthrough 시작.");

        // GameSessionRoot ready 대기 — Boot scene GameBootstrap 이 EnsureRoot 후 SessionRoot 생성.
        var rootWait = 0f;
        while (GameSessionRoot.Instance == null && rootWait < 10f)
        {
            rootWait += Time.unscaledDeltaTime;
            yield return null;
        }
        var sessionRoot = GameSessionRoot.Instance;
        if (sessionRoot == null)
        {
            Debug.LogError("[DemoWalkthroughRunner] GameSessionRoot 못 찾음 — 10초 timeout. Walkthrough 중단.");
            OnComplete?.Invoke(false);
            yield break;
        }

        // Boot scene 인 경우 Town 진입 trigger — DemoAlphaWalkTests.EnterOfflineTownFromBoot 패턴 reuse.
        // 시연 영상 1차 prototype 은 isolation 위해 SmokeNamespace 사용. 사용자 production profile
        // 시연은 별도 wave (default profile + main menu interaction).
        if (SceneManager.GetActiveScene().name != "Town")
        {
            Debug.Log("[DemoWalkthroughRunner] Boot scene 감지 — Town 진입 trigger.");
            sessionRoot.UseDedicatedSmokeNamespace();
            if (!sessionRoot.StartRealm(SessionRealm.OfflineLocal, out var err))
            {
                Debug.LogError($"[DemoWalkthroughRunner] StartRealm 실패: {err}. Walkthrough 중단.");
                OnComplete?.Invoke(false);
                yield break;
            }
            sessionRoot.SessionState.AbandonExpeditionRun();
            sessionRoot.SaveProfile(SessionCheckpointKind.ManualSave);
            sessionRoot.SceneFlow.GoToTown();

            var sceneWait = 0f;
            while (SceneManager.GetActiveScene().name != "Town" && sceneWait < 8f)
            {
                sceneWait += Time.unscaledDeltaTime;
                yield return null;
            }
            if (SceneManager.GetActiveScene().name != "Town")
            {
                Debug.LogError("[DemoWalkthroughRunner] Town scene 진입 8초 timeout. Walkthrough 중단.");
                OnComplete?.Invoke(false);
                yield break;
            }
            Debug.Log("[DemoWalkthroughRunner] Town scene 진입 완료.");
        }

        // panelHost ready 대기 — Town scene 진입 후 panelHost 가 attach 되기까지 idle.
        RuntimePanelHost? panelHost = null;
        var hostWait = 0f;
        while (panelHost == null && hostWait < 15f)
        {
            panelHost = FindTownPanelHost();
            if (panelHost != null) break;
            hostWait += Time.unscaledDeltaTime;
            yield return null;
        }

        if (panelHost == null)
        {
            Debug.LogError("[DemoWalkthroughRunner] TownRuntimePanelHost 못 찾음 — 15초 timeout. Walkthrough 중단.");
            OnComplete?.Invoke(false);
            yield break;
        }

        panelHost.EnsureReady();
        var root = panelHost.Root;
        Debug.Log("[DemoWalkthroughRunner] panelHost ready. Town hub 진입 hold 3s.");
        yield return WaitSeconds(3f);

        // Recruit cut — FaceCard_dalmok click → RcpRoot visible → 8s hold → close
        if (!ClickButtonByName(root, "FaceCard_dalmok"))
        {
            Debug.LogError("[DemoWalkthroughRunner] FaceCard_dalmok click 실패 — Recruit cut skip.");
        }
        else
        {
            yield return WaitForVisible(root, "RcpRoot", 4f);
            Debug.Log("[DemoWalkthroughRunner] Recruit panel visible. 8s hold.");
            yield return WaitSeconds(8f);
            ClickFirstButtonWithClass(root, "rcp-header__close");
            yield return WaitForHidden(root, "RcpRoot", 3f);
            Debug.Log("[DemoWalkthroughRunner] Recruit panel hidden. 3s hold.");
            yield return WaitSeconds(3f);
        }

        // Roster cut — RosterButton click → RosterGridPreviewRoot visible → 8s hold → close
        if (!ClickButtonByName(root, "RosterButton"))
        {
            Debug.LogError("[DemoWalkthroughRunner] RosterButton click 실패 — Roster cut skip.");
        }
        else
        {
            yield return WaitForVisible(root, "RosterGridPreviewRoot", 4f);
            Debug.Log("[DemoWalkthroughRunner] Roster panel visible. 8s hold.");
            yield return WaitSeconds(8f);
            ClickFirstButtonWithClass(root, "rgp-header__close");
            yield return WaitForHidden(root, "RosterGridPreviewRoot", 3f);
            Debug.Log("[DemoWalkthroughRunner] Roster panel hidden. 3s hold.");
            yield return WaitSeconds(3f);
        }

        // Character Sheet cut — hero face card click → 4 CTA tour (PassiveBoard sub-cut + Refit sub-cut)
        // → close. cut sheet 1:30-2:00 단계. DemoAlphaWalkTests.Demo_CharacterSheet_4CTA_Bindings_All_Click_Without_Exception
        // 패턴 reuse — Profile.Heroes.First().HeroId 로 FaceCard_{heroId} 식별.
        var heroes = sessionRoot.SessionState.Profile.Heroes;
        if (heroes.Count == 0)
        {
            Debug.LogError("[DemoWalkthroughRunner] Profile.Heroes 비어있음 — CharSheet cut skip.");
        }
        else
        {
            var heroId = heroes[0].HeroId;
            if (!ClickButtonByName(root, $"FaceCard_{heroId}"))
            {
                Debug.LogError($"[DemoWalkthroughRunner] FaceCard_{heroId} click 실패 — CharSheet cut skip.");
            }
            else
            {
                yield return WaitForVisible(root, "TownCharacterSheetRoot", 4f);
                Debug.Log("[DemoWalkthroughRunner] CharSheet visible. 8s hold.");
                yield return WaitSeconds(8f);

                // PassiveBoard sub-cut — TcsPassiveBoardButton → PbpRoot 5s → close
                if (ClickButtonByName(root, "TcsPassiveBoardButton"))
                {
                    yield return WaitForVisible(root, "PbpRoot", 4f);
                    Debug.Log("[DemoWalkthroughRunner] CharSheet → PassiveBoard visible. 5s hold.");
                    yield return WaitSeconds(5f);
                    ClickFirstButtonWithClass(root, "pbp-footer__close");
                    yield return WaitForHidden(root, "PbpRoot", 3f);
                    yield return WaitSeconds(2f);
                }

                // Refit sub-cut — TcsRefitButton → ErpRoot 5s → close
                if (ClickButtonByName(root, "TcsRefitButton"))
                {
                    yield return WaitForVisible(root, "ErpRoot", 4f);
                    Debug.Log("[DemoWalkthroughRunner] CharSheet → Refit visible. 5s hold.");
                    yield return WaitSeconds(5f);
                    ClickFirstButtonWithClass(root, "erp-header__close");
                    yield return WaitForHidden(root, "ErpRoot", 3f);
                    yield return WaitSeconds(2f);
                }

                ClickButtonByName(root, "TownCharacterSheetCloseButton");
                yield return WaitForHidden(root, "TownCharacterSheetRoot", 3f);
                Debug.Log("[DemoWalkthroughRunner] CharSheet hidden. 3s hold.");
                yield return WaitSeconds(3f);
            }
        }

        // Squad Workshop cut — cut sheet 2:00-2:30. TacticalSetupButton → SquadBuilderRoot visible 8s → close
        if (ClickButtonByName(root, "TacticalSetupButton"))
        {
            yield return WaitForVisible(root, "SquadBuilderRoot", 4f);
            Debug.Log("[DemoWalkthroughRunner] Squad Workshop visible. 8s hold.");
            yield return WaitSeconds(8f);
            ClickButtonByName(root, "SquadBuilderCloseButton");
            yield return WaitForHidden(root, "SquadBuilderRoot", 3f);
            Debug.Log("[DemoWalkthroughRunner] Squad Workshop hidden. 3s hold.");
            yield return WaitSeconds(3f);
        }

        // Permanent Augment cut — PermanentAugmentButton → PapRoot visible 8s → close
        if (ClickButtonByName(root, "PermanentAugmentButton"))
        {
            yield return WaitForVisible(root, "PapRoot", 4f);
            Debug.Log("[DemoWalkthroughRunner] Permanent Augment visible. 8s hold.");
            yield return WaitSeconds(8f);
            ClickFirstButtonWithClass(root, "pap-header__close");
            yield return WaitForHidden(root, "PapRoot", 3f);
            Debug.Log("[DemoWalkthroughRunner] Permanent Augment hidden. 3s hold.");
            yield return WaitSeconds(3f);
        }

        // Inventory cut — FaceCard_solgil click → InvRoot visible 8s → close
        if (ClickButtonByName(root, "FaceCard_solgil"))
        {
            yield return WaitForVisible(root, "InvRoot", 4f);
            Debug.Log("[DemoWalkthroughRunner] Inventory visible. 8s hold.");
            yield return WaitSeconds(8f);
            ClickFirstButtonWithClass(root, "inv-currency__close");
            yield return WaitForHidden(root, "InvRoot", 3f);
            Debug.Log("[DemoWalkthroughRunner] Inventory hidden. 3s hold.");
            yield return WaitSeconds(3f);
        }

        Debug.Log("[DemoWalkthroughRunner] Walkthrough complete.");
        OnComplete?.Invoke(true);
    }

    private static RuntimePanelHost? FindTownPanelHost()
    {
        return Resources.FindObjectsOfTypeAll<RuntimePanelHost>()
            .FirstOrDefault(host => host != null
                                    && host.gameObject.scene.IsValid()
                                    && host.gameObject.name == "TownRuntimePanelHost");
    }

    private static IEnumerator WaitSeconds(float seconds)
    {
        var elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private static IEnumerator WaitForVisible(VisualElement root, string name, float timeout)
    {
        var elapsed = 0f;
        while (elapsed < timeout)
        {
            var element = root.Q<VisualElement>(name);
            if (element != null
                && IsEffectivelyVisible(element)
                && element.worldBound.width > 8f
                && element.worldBound.height > 8f)
            {
                yield break;
            }
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        Debug.LogError($"[DemoWalkthroughRunner] WaitForVisible '{name}' timeout {timeout}s.");
    }

    private static IEnumerator WaitForHidden(VisualElement root, string name, float timeout)
    {
        var elapsed = 0f;
        while (elapsed < timeout)
        {
            var element = root.Q<VisualElement>(name);
            if (element != null && element.style.display.value == DisplayStyle.None)
            {
                yield break;
            }
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        Debug.LogError($"[DemoWalkthroughRunner] WaitForHidden '{name}' timeout {timeout}s.");
    }

    private static bool ClickButtonByName(VisualElement root, string name)
    {
        var button = root.Q<Button>(name);
        if (button == null)
        {
            return false;
        }
        InvokeButton(button);
        return true;
    }

    private static void ClickFirstButtonWithClass(VisualElement root, string className)
    {
        var button = root.Query<Button>(className: className).First();
        if (button == null)
        {
            Debug.LogError($"[DemoWalkthroughRunner] Button with class '{className}' 못 찾음.");
            return;
        }
        InvokeButton(button);
    }

    private static void InvokeButton(Button button)
    {
        if (!button.enabledInHierarchy)
        {
            Debug.LogError($"[DemoWalkthroughRunner] Button '{button.name}' 비활성 상태 click 시도.");
            return;
        }
        using var evt = ClickEvent.GetPooled();
        var invoke = typeof(Clickable).GetMethod("Invoke", BindingFlags.Instance | BindingFlags.NonPublic);
        if (invoke == null)
        {
            Debug.LogError("[DemoWalkthroughRunner] Clickable.Invoke method 못 찾음.");
            return;
        }
        invoke.Invoke(button.clickable, new object[] { evt });
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
}
