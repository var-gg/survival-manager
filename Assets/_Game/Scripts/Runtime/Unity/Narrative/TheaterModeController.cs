using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SM.Content;
using SM.Content.Definitions;
using SM.Core;
using SM.Meta;
using SM.Unity;
using SM.Unity.UI;
using UnityEngine;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SM.Unity.Narrative;

/// <summary>
/// 극장 모드 — 위키 시드로 임포트된 dialogue scene(`dialogue_seq_*`)을 목록에서 골라 재생한다.
/// 재생 자체는 <see cref="StoryPresentationRunner"/>에 위임한다(localization·portrait·
/// graceful degradation 내장). 본 컨트롤러는 scene 목록 UI와 재생 오케스트레이션만 담당한다.
/// 배경 이미지·BGM은 아직 없으나 DialogueSceneView가 그것들 없이도 동작하므로 현 상태로 재생 가능하다.
/// </summary>
[DisallowMultipleComponent]
public sealed class TheaterModeController : MonoBehaviour
{
    private const string DialogueSequencesResourcesPath = "_Game/Content/Definitions/DialogueSequences";
    private const string WikiSequencePrefix = "dialogue_seq_";
    private const string TheaterTreePath = "Assets/_Game/UI/Narrative/TheaterMode.uxml";
    private const string TheaterStylePath = "Assets/_Game/UI/Narrative/TheaterMode.uss";

    [SerializeField] private RuntimePanelHost _panelHost = null!;
    [SerializeField] private StoryPresentationRunner _storyRunner = null!;
    [SerializeField] private VisualTreeAsset _theaterTree = null!;
    [SerializeField] private StyleSheet _theaterStyle = null!;

    private VisualElement? _theaterRoot;
    private VisualElement? _listContainer;
    private Label? _statusLabel;
    private GameLocalizationController? _localization;
    private bool _playing;

    private IEnumerator Start()
    {
        var root = GameSessionRoot.EnsureInstance();
        yield return root.Localization.EnsureInitialized();
        _localization = root.Localization;

        ResolvePanelHost();
        ResolveStoryRunner();
        ApplyEditorAssetFallback();

        if (_panelHost == null)
        {
            Debug.LogError("[TheaterModeController] RuntimePanelHost를 찾을 수 없습니다. scene에 RuntimePanelHost를 두세요.");
            yield break;
        }

        if (_theaterTree == null)
        {
            Debug.LogError("[TheaterModeController] TheaterMode.uxml이 할당되지 않았습니다.");
            yield break;
        }

        if (_storyRunner == null)
        {
            Debug.LogError("[TheaterModeController] StoryPresentationRunner를 찾을 수 없습니다. scene에 StoryPresentationRunner를 두세요.");
            yield break;
        }

        _panelHost.EnsureReady();

        var sequences = Resources.LoadAll<DialogueSequenceDefinition>(DialogueSequencesResourcesPath)
            .Where(sequence => sequence != null
                               && !string.IsNullOrEmpty(sequence.Id)
                               && sequence.Id.StartsWith(WikiSequencePrefix, StringComparison.Ordinal))
            .OrderBy(sequence => sequence.Id, StringComparer.Ordinal)
            .ToList();

        BuildUI(sequences);
    }

    private void BuildUI(IReadOnlyList<DialogueSequenceDefinition> sequences)
    {
        var container = _theaterTree.CloneTree();
        if (_theaterStyle != null && !container.styleSheets.Contains(_theaterStyle))
        {
            container.styleSheets.Add(_theaterStyle);
        }

        _panelHost.Root.Add(container);
        _theaterRoot = container.Q<VisualElement>("theater-root") ?? container;
        _listContainer = container.Q<VisualElement>("theater-list");
        _statusLabel = container.Q<Label>("theater-status");

        if (_statusLabel != null)
        {
            _statusLabel.text = $"{sequences.Count}개 scene · 항목을 누르면 재생합니다";
        }

        if (_listContainer == null)
        {
            Debug.LogError("[TheaterModeController] UXML에 'theater-list' 요소가 없습니다.");
            return;
        }

        _listContainer.Clear();
        foreach (var sequence in sequences)
        {
            var presentationKey = sequence.Id.StartsWith(WikiSequencePrefix, StringComparison.Ordinal)
                ? sequence.Id[WikiSequencePrefix.Length..]
                : sequence.Id;
            var label = ResolveTitle(presentationKey, sequence.Id);
            var capturedSequence = sequence;
            var item = new Button(() => PlayScene(capturedSequence)) { text = label };
            item.AddToClassList("theater-list-item");
            _listContainer.Add(item);
        }
    }

    private string ResolveTitle(string presentationKey, string fallback)
    {
        if (_localization == null)
        {
            return fallback;
        }

        var key = NarrativeLocalizationKeys.PresentationTitle(presentationKey);
        var resolved = _localization.LocalizePlayerFacingContent(ContentLocalizationTables.Story, key, fallback);
        return string.IsNullOrWhiteSpace(resolved) ? fallback : resolved;
    }

    private void PlayScene(DialogueSequenceDefinition sequence)
    {
        if (_playing || _storyRunner == null || sequence == null)
        {
            return;
        }

        _playing = true;
        SetListVisible(false);
        _storyRunner.Enqueue(
            new[]
            {
                new StoryPresentationRequest
                {
                    PresentationKind = StoryPresentationKind.DialogueScene,
                    PresentationKey = sequence.Id,
                },
            },
            () =>
            {
                _playing = false;
                SetListVisible(true);
            });
    }

    private void SetListVisible(bool visible)
    {
        if (_theaterRoot == null)
        {
            return;
        }

        _theaterRoot.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        _theaterRoot.visible = visible;
        _theaterRoot.pickingMode = visible ? PickingMode.Position : PickingMode.Ignore;
    }

    private void ResolvePanelHost()
    {
        if (_panelHost != null)
        {
            return;
        }

        foreach (var root in gameObject.scene.GetRootGameObjects())
        {
            var host = root.GetComponentsInChildren<RuntimePanelHost>(true).FirstOrDefault();
            if (host != null)
            {
                _panelHost = host;
                return;
            }
        }
    }

    private void ResolveStoryRunner()
    {
        if (_storyRunner != null)
        {
            return;
        }

        _storyRunner = FindAnyObjectByType<StoryPresentationRunner>();
    }

    private void ApplyEditorAssetFallback()
    {
#if UNITY_EDITOR
        _theaterTree ??= AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TheaterTreePath);
        _theaterStyle ??= AssetDatabase.LoadAssetAtPath<StyleSheet>(TheaterStylePath);
#endif
    }
}
