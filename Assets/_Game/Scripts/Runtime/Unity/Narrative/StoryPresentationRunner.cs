using System;
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

[DisallowMultipleComponent]
public sealed class StoryPresentationRunner : MonoBehaviour
{
    private const float DefaultCharactersPerSecond = 40f;
    private const float DefaultToastHoldSeconds = 2.2f;
    private const string DefaultNarrativeLocalizationTable = ContentLocalizationTables.Story;
    private const string DefaultEmoteId = "Default";
    private const string NarratorSpeakerId = "Narrator";
    private const string NarrativeCardsResourcesPath = "Narrative/Cards";
    private const string DialogueSequencesResourcesPath = "_Game/Content/Definitions/DialogueSequences";
    private const string HeroLoreResourcesPath = "_Game/Content/Definitions/HeroLore";
    private const string NarrativeRootName = "story-narrative-root";
    private const string ToastTreePath = "Assets/_Game/UI/Narrative/StoryToastBanner.uxml";
    private const string ToastStylePath = "Assets/_Game/UI/Narrative/StoryToastBanner.uss";
    private const string OverlayTreePath = "Assets/_Game/UI/Narrative/DialogueOverlay.uxml";
    private const string OverlayStylePath = "Assets/_Game/UI/Narrative/DialogueOverlay.uss";
    private const string SceneTreePath = "Assets/_Game/UI/Narrative/DialogueScene.uxml";
    private const string SceneStylePath = "Assets/_Game/UI/Narrative/DialogueScene.uss";
    private const string CardTreePath = "Assets/_Game/UI/Narrative/StoryCard.uxml";
    private const string CardStylePath = "Assets/_Game/UI/Narrative/StoryCard.uss";
    private const string CommonStylePath = "Assets/_Game/UI/Narrative/StoryCommon.uss";

    [SerializeField] private RuntimePanelHost _panelHost = null!;
    [SerializeField] private VisualTreeAsset _toastTree = null!;
    [SerializeField] private VisualTreeAsset _dialogueOverlayTree = null!;
    [SerializeField] private VisualTreeAsset _dialogueSceneTree = null!;
    [SerializeField] private VisualTreeAsset _storyCardTree = null!;
    [SerializeField] private StyleSheet _storyCommonStyle = null!;
    [SerializeField] private StyleSheet _toastStyle = null!;
    [SerializeField] private StyleSheet _dialogueOverlayStyle = null!;
    [SerializeField] private StyleSheet _dialogueSceneStyle = null!;
    [SerializeField] private StyleSheet _storyCardStyle = null!;

    private readonly Queue<PendingBatch> _pendingBatches = new();
    private VisualElement? _narrativeRoot;
    private StoryToastBannerView? _toastView;
    private DialogueOverlayView? _dialogueOverlayView;
    private DialogueSceneView? _dialogueSceneView;
    private StoryCardView? _storyCardView;
    private ToastBannerPresenter? _toastPresenter;
    private DialogueOverlayPresenter? _dialogueOverlayPresenter;
    private DialogueScenePresenter? _dialogueScenePresenter;
    private StoryCardPresenter? _storyCardPresenter;
    private IStoryPortraitResolver? _portraitResolver;
    private GameLocalizationController? _localization;
    private ContentTextResolver? _contentText;
    private Dictionary<string, DialogueSequenceDefinition>? _dialogueSequencesById;
    private Dictionary<string, HeroLoreDefinition>? _heroLoreById;
    private PendingBatch? _currentBatch;
    private int _currentRequestIndex;
    private int _boundRootBuildCount = -1;
    private int _executionVersion;

    public bool IsBusy => _currentBatch != null || _pendingBatches.Count > 0 || IsAnyPresenterPlaying();

    public event Action<StoryPresentationRequest>? PresentationStarted;
    public event Action<StoryPresentationRequest>? PresentationCompleted;
    public event Action? QueueDrained;

    private void Awake()
    {
        EnsureReady();
    }

    private void OnDestroy()
    {
        StopAndClear();
        DisposeNarrativeLayer();
    }

    public void Enqueue(IReadOnlyList<StoryPresentationRequest> requests, Action? onBatchCompleted = null)
    {
        if (requests == null || requests.Count == 0)
        {
            onBatchCompleted?.Invoke();
            return;
        }

        if (!EnsureReady())
        {
            return;
        }

        _pendingBatches.Enqueue(new PendingBatch(requests.ToArray(), onBatchCompleted));
        if (_currentBatch == null && !IsAnyPresenterPlaying())
        {
            TryAdvanceQueue();
        }
    }

    public void StopAndClear()
    {
        _executionVersion++;
        _pendingBatches.Clear();
        _currentBatch = null;
        _currentRequestIndex = 0;
        _toastPresenter?.DismissImmediate();
        _dialogueOverlayPresenter?.HideImmediate();
        _dialogueScenePresenter?.HideImmediate();
        _storyCardPresenter?.HideImmediate();
    }

    internal void SetPanelHost(RuntimePanelHost panelHost)
    {
        _panelHost = panelHost;
    }

    private bool EnsureReady()
    {
        if (!EnsureRuntimeServices())
        {
            return false;
        }

        if (_panelHost == null)
        {
            _panelHost = ResolvePanelHost();
            if (_panelHost == null)
            {
                Debug.LogError("[StoryPresentationRunner] Missing RuntimePanelHost reference.");
                return false;
            }
        }

        _panelHost.EnsureReady();
        ApplyEditorAssetFallback();
        EnsurePresentationCatalog();

        if (_narrativeRoot != null && _boundRootBuildCount == _panelHost.RootBuildCount)
        {
            return true;
        }

        StopAndClear();
        DisposeNarrativeLayer();
        _portraitResolver = new ResourcesStoryPortraitResolver();
        _narrativeRoot = new VisualElement
        {
            name = NarrativeRootName,
            pickingMode = PickingMode.Ignore,
        };
        _narrativeRoot.style.position = Position.Absolute;
        _narrativeRoot.style.left = 0f;
        _narrativeRoot.style.right = 0f;
        _narrativeRoot.style.top = 0f;
        _narrativeRoot.style.bottom = 0f;
        _panelHost.Root.Add(_narrativeRoot);

        _toastView = new StoryToastBannerView(CreateRootView(_toastTree, _toastStyle, "story-toast-root"));
        _dialogueOverlayView = new DialogueOverlayView(CreateRootView(_dialogueOverlayTree, _dialogueOverlayStyle, "dialogue-overlay-root"));
        _dialogueSceneView = new DialogueSceneView(CreateRootView(_dialogueSceneTree, _dialogueSceneStyle, "dialogue-scene-root"));
        _storyCardView = new StoryCardView(CreateRootView(_storyCardTree, _storyCardStyle, "story-card-root"));

        _toastPresenter = new ToastBannerPresenter(_toastView);
        _dialogueOverlayPresenter = new DialogueOverlayPresenter(_dialogueOverlayView, _portraitResolver);
        _dialogueScenePresenter = new DialogueScenePresenter(_dialogueSceneView, _portraitResolver);
        _storyCardPresenter = new StoryCardPresenter(_storyCardView);
        _boundRootBuildCount = _panelHost.RootBuildCount;
        return true;
    }

    private VisualElement CreateRootView(VisualTreeAsset treeAsset, StyleSheet styleSheet, string rootName)
    {
        if (treeAsset == null)
        {
            throw new InvalidOperationException($"Missing narrative VisualTreeAsset for '{rootName}'.");
        }

        var container = treeAsset.CloneTree();
        if (_storyCommonStyle != null && !container.styleSheets.Contains(_storyCommonStyle))
        {
            container.styleSheets.Add(_storyCommonStyle);
        }

        if (styleSheet != null && !container.styleSheets.Contains(styleSheet))
        {
            container.styleSheets.Add(styleSheet);
        }

        container.pickingMode = PickingMode.Ignore;
        _narrativeRoot!.Add(container);
        return container.Q<VisualElement>(rootName)
               ?? throw new InvalidOperationException($"Missing UITK element '{rootName}'.");
    }

    private void TryAdvanceQueue()
    {
        if (!EnsureReady())
        {
            return;
        }

        if (_currentBatch == null)
        {
            if (_pendingBatches.Count == 0)
            {
                QueueDrained?.Invoke();
                return;
            }

            _currentBatch = _pendingBatches.Dequeue();
            _currentRequestIndex = 0;
        }

        if (_currentRequestIndex >= _currentBatch.Requests.Count)
        {
            var completedCallback = _currentBatch.OnCompleted;
            _currentBatch = null;
            _currentRequestIndex = 0;
            completedCallback?.Invoke();
            if (_pendingBatches.Count == 0)
            {
                QueueDrained?.Invoke();
                return;
            }

            TryAdvanceQueue();
            return;
        }

        var request = _currentBatch.Requests[_currentRequestIndex];
        PresentationStarted?.Invoke(request);
        DispatchRequest(request, _executionVersion);
    }

    private void DispatchRequest(StoryPresentationRequest request, int version)
    {
        switch (request.PresentationKind)
        {
            case StoryPresentationKind.ToastBanner:
                if (TryMapToast(request, out var toastState, out var holdSeconds))
                {
                    _toastPresenter!.Present(toastState, holdSeconds, () => HandleRequestCompleted(request, version));
                }
                else
                {
                    HandleRequestCompleted(request, version);
                }
                return;

            case StoryPresentationKind.DialogueOverlay:
                if (TryMapDialogueOverlay(request, out var overlayModel))
                {
                    _dialogueOverlayPresenter!.Present(overlayModel, () => HandleRequestCompleted(request, version));
                }
                else
                {
                    HandleRequestCompleted(request, version);
                }
                return;

            case StoryPresentationKind.DialogueScene:
                if (TryMapDialogueScene(request, out var sceneModel))
                {
                    _dialogueScenePresenter!.Present(sceneModel, () => HandleRequestCompleted(request, version));
                }
                else
                {
                    HandleRequestCompleted(request, version);
                }
                return;

            case StoryPresentationKind.StoryCard:
                if (TryMapStoryCard(request, out var storyCardState))
                {
                    _storyCardPresenter!.Present(storyCardState, () => HandleRequestCompleted(request, version));
                }
                else
                {
                    HandleRequestCompleted(request, version);
                }
                return;
        }

        Debug.LogWarning($"[StoryPresentationRunner] Ignoring invalid request kind '{request.PresentationKind}'.");
        HandleRequestCompleted(request, version);
    }

    private void HandleRequestCompleted(StoryPresentationRequest request, int version)
    {
        if (version != _executionVersion)
        {
            return;
        }

        PresentationCompleted?.Invoke(request);
        _currentRequestIndex++;
        TryAdvanceQueue();
    }

    private bool TryMapToast(
        StoryPresentationRequest request,
        out StoryToastBannerViewState state,
        out float holdSeconds)
    {
        var titleText = ResolvePresentationText(
            request.PresentationKey,
            isTitle: true,
            HumanizePresentationKey(request.PresentationKey));
        var bodyText = ResolvePresentationText(
            request.PresentationKey,
            isTitle: false,
            request.PresentationKey);
        state = new StoryToastBannerViewState(titleText, bodyText, AllowTapDismiss: true);
        holdSeconds = DefaultToastHoldSeconds;
        return !string.IsNullOrWhiteSpace(titleText) || !string.IsNullOrWhiteSpace(bodyText);
    }

    private bool TryMapDialogueOverlay(StoryPresentationRequest request, out DialogueOverlayPlaybackModel model)
    {
        if (!TryResolveDialogueSequence(request, out var sequence))
        {
            Debug.LogWarning($"[StoryPresentationRunner] Dialogue overlay sequence '{request.PresentationKey}' could not be resolved.");
            model = new DialogueOverlayPlaybackModel();
            return false;
        }

        var sideBySpeaker = BuildSpeakerSideMap(request, sequence);
        var lines = BuildDialogueLines(sequence, sideBySpeaker);
        model = new DialogueOverlayPlaybackModel
        {
            LeftSpeaker = BuildSpeakerModel(sequence, sideBySpeaker, StorySpeakerSide.Left),
            RightSpeaker = BuildSpeakerModel(sequence, sideBySpeaker, StorySpeakerSide.Right),
            Lines = lines,
            ShowSkipAll = true,
        };
        return lines.Count > 0;
    }

    private bool TryMapDialogueScene(StoryPresentationRequest request, out DialogueScenePlaybackModel model)
    {
        if (!TryResolveDialogueSequence(request, out var sequence))
        {
            Debug.LogWarning($"[StoryPresentationRunner] Dialogue scene sequence '{request.PresentationKey}' could not be resolved.");
            model = new DialogueScenePlaybackModel();
            return false;
        }

        var sideBySpeaker = BuildSpeakerSideMap(request, sequence);
        var lines = BuildDialogueLines(sequence, sideBySpeaker);
        model = new DialogueScenePlaybackModel
        {
            LeftSpeaker = BuildSpeakerModel(sequence, sideBySpeaker, StorySpeakerSide.Left),
            RightSpeaker = BuildSpeakerModel(sequence, sideBySpeaker, StorySpeakerSide.Right),
            Lines = lines,
            EnableTypingEffect = true,
            CharactersPerSecond = DefaultCharactersPerSecond,
            RequireSkipConfirmation = true,
            SkipConfirmTitleText = LocalizeUiCommon(
                "ui.story.skip_confirm.title",
                "Skip scene?"),
            SkipConfirmBodyText = LocalizeUiCommon(
                "ui.story.skip_confirm.body",
                "This only skips presentation."),
        };
        return lines.Count > 0;
    }

    private bool TryMapStoryCard(StoryPresentationRequest request, out StoryCardViewState state)
    {
        if (TryResolveHeroLore(request.PresentationKey, out var heroLore))
        {
            var loreTitleFallback = _contentText!.GetCharacterName(heroLore.HeroId);
            var loreBodyFallback = FirstNonEmpty(heroLore.CanonBio, heroLore.UnresolvedHook, request.PresentationKey);
            state = new StoryCardViewState(
                ResolveHeroLoreTitle(heroLore, loreTitleFallback),
                ResolveHeroLoreBody(heroLore, loreBodyFallback),
                ResolveStoryCardSprite(request.PresentationKey, heroLore.HeroId),
                ParseTint(string.Empty),
                ShowSkip: true,
                ShowContinueHint: true);
            return true;
        }

        var titleText = ResolvePresentationText(
            request.PresentationKey,
            isTitle: true,
            HumanizePresentationKey(request.PresentationKey));
        var bodyText = ResolvePresentationText(
            request.PresentationKey,
            isTitle: false,
            request.PresentationKey);
        state = new StoryCardViewState(
            titleText,
            bodyText,
            ResolveStoryCardSprite(request.PresentationKey),
            ParseTint(string.Empty),
            ShowSkip: true,
            ShowContinueHint: true);
        return !string.IsNullOrWhiteSpace(titleText)
               || !string.IsNullOrWhiteSpace(bodyText)
               || state.BackgroundSprite != null;
    }

    private Sprite? ResolveStoryCardSprite(params string[] spriteKeys)
    {
        foreach (var spriteKey in spriteKeys)
        {
            if (string.IsNullOrWhiteSpace(spriteKey))
            {
                continue;
            }

            var direct = Resources.Load<Sprite>($"{NarrativeCardsResourcesPath}/{spriteKey}");
            if (direct != null)
            {
                return direct;
            }

            var normalized = Resources.Load<Sprite>($"{NarrativeCardsResourcesPath}/{ContentLocalizationTables.NormalizeId(spriteKey)}");
            if (normalized != null)
            {
                return normalized;
            }
        }

        return null;
    }

    private bool TryResolveDialogueSequence(StoryPresentationRequest request, out DialogueSequenceDefinition sequence)
    {
        var sequenceId = request.PresentationKey;
        if (sequenceId.StartsWith("dialogue_scene_", StringComparison.Ordinal))
        {
            sequenceId = $"dialogue_seq_{sequenceId["dialogue_scene_".Length..]}";
        }
        else if (sequenceId.StartsWith("dialogue_overlay_", StringComparison.Ordinal))
        {
            sequenceId = $"dialogue_seq_{sequenceId["dialogue_overlay_".Length..]}";
        }

        if (_dialogueSequencesById != null && _dialogueSequencesById.TryGetValue(sequenceId, out var resolvedSequence))
        {
            sequence = resolvedSequence;
            return true;
        }

        sequence = null!;
        return false;
    }

    private bool TryResolveHeroLore(string presentationKey, out HeroLoreDefinition heroLore)
    {
        if (_heroLoreById != null && _heroLoreById.TryGetValue(presentationKey, out var resolvedHeroLore))
        {
            heroLore = resolvedHeroLore;
            return true;
        }

        heroLore = null!;
        return false;
    }

    private Dictionary<string, StorySpeakerSide> BuildSpeakerSideMap(
        StoryPresentationRequest request,
        DialogueSequenceDefinition sequence)
    {
        var orderedSpeakerIds = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var speakerId in request.SpeakerIds ?? Array.Empty<string>())
        {
            if (IsNarrator(speakerId) || string.IsNullOrWhiteSpace(speakerId) || !seen.Add(speakerId))
            {
                continue;
            }

            orderedSpeakerIds.Add(speakerId);
        }

        foreach (var line in sequence.Lines ?? Array.Empty<DialogueLineDefinition>())
        {
            if (line == null
                || IsNarrator(line.SpeakerId)
                || string.IsNullOrWhiteSpace(line.SpeakerId)
                || !seen.Add(line.SpeakerId))
            {
                continue;
            }

            orderedSpeakerIds.Add(line.SpeakerId);
        }

        var map = new Dictionary<string, StorySpeakerSide>(StringComparer.Ordinal);
        if (orderedSpeakerIds.Count > 0)
        {
            map[orderedSpeakerIds[0]] = StorySpeakerSide.Left;
        }

        if (orderedSpeakerIds.Count > 1)
        {
            map[orderedSpeakerIds[1]] = StorySpeakerSide.Right;
        }

        return map;
    }

    private StorySpeakerModel? BuildSpeakerModel(
        DialogueSequenceDefinition sequence,
        IReadOnlyDictionary<string, StorySpeakerSide> sideBySpeaker,
        StorySpeakerSide side)
    {
        var speakerId = sideBySpeaker.FirstOrDefault(pair => pair.Value == side).Key;
        if (string.IsNullOrWhiteSpace(speakerId))
        {
            return null;
        }

        return new StorySpeakerModel(
            speakerId,
            ResolveSpeakerDisplayName(speakerId),
            side,
            ResolveDefaultEmote(sequence, speakerId));
    }

    private IReadOnlyList<StoryDialogueLineModel> BuildDialogueLines(
        DialogueSequenceDefinition sequence,
        IReadOnlyDictionary<string, StorySpeakerSide> sideBySpeaker)
    {
        var lines = new List<StoryDialogueLineModel>();
        foreach (var line in sequence.Lines ?? Array.Empty<DialogueLineDefinition>())
        {
            if (line == null)
            {
                continue;
            }

            var isNarrator = IsNarrator(line.SpeakerId);
            var speakerSide = !isNarrator && sideBySpeaker.TryGetValue(line.SpeakerId, out var resolvedSide)
                ? resolvedSide
                : StorySpeakerSide.None;
            lines.Add(new StoryDialogueLineModel(
                line.SpeakerId ?? string.Empty,
                isNarrator ? string.Empty : ResolveSpeakerDisplayName(line.SpeakerId),
                speakerSide,
                string.IsNullOrWhiteSpace(line.Emote) ? DefaultEmoteId : line.Emote,
                ResolveDialogueLineText(line.TextKey),
                isNarrator));
        }

        return lines;
    }

    private static Color ParseTint(string tintHtml)
    {
        return !string.IsNullOrWhiteSpace(tintHtml) && ColorUtility.TryParseHtmlString(tintHtml, out var tint)
            ? tint
            : new Color(0f, 0f, 0f, 0.62f);
    }

    private bool EnsureRuntimeServices()
    {
        var root = GameSessionRoot.EnsureInstance();
        if (root == null)
        {
            Debug.LogError("[StoryPresentationRunner] GameSessionRoot could not be resolved.");
            return false;
        }

        _localization = root.Localization;
        _contentText ??= new ContentTextResolver(_localization, root.CombatContentLookup);
        return true;
    }

    private void EnsurePresentationCatalog()
    {
        if (_dialogueSequencesById != null && _heroLoreById != null)
        {
            return;
        }

        _dialogueSequencesById = BuildDialogueSequenceIndex();
        _heroLoreById = BuildHeroLoreIndex();
    }

    private static Dictionary<string, DialogueSequenceDefinition> BuildDialogueSequenceIndex()
    {
        var index = new Dictionary<string, DialogueSequenceDefinition>(StringComparer.Ordinal);
        foreach (var sequence in Resources.LoadAll<DialogueSequenceDefinition>(DialogueSequencesResourcesPath))
        {
            if (sequence == null || string.IsNullOrWhiteSpace(sequence.Id) || index.ContainsKey(sequence.Id))
            {
                continue;
            }

            index.Add(sequence.Id, sequence);
        }

        return index;
    }

    private static Dictionary<string, HeroLoreDefinition> BuildHeroLoreIndex()
    {
        var index = new Dictionary<string, HeroLoreDefinition>(StringComparer.Ordinal);
        foreach (var heroLore in Resources.LoadAll<HeroLoreDefinition>(HeroLoreResourcesPath))
        {
            if (heroLore == null || string.IsNullOrWhiteSpace(heroLore.Id) || index.ContainsKey(heroLore.Id))
            {
                continue;
            }

            index.Add(heroLore.Id, heroLore);
        }

        return index;
    }

    private string ResolveSpeakerDisplayName(string speakerId)
    {
        if (string.IsNullOrWhiteSpace(speakerId))
        {
            return string.Empty;
        }

        return _contentText!.GetCharacterName(speakerId);
    }

    private string ResolveDefaultEmote(DialogueSequenceDefinition sequence, string speakerId)
    {
        foreach (var line in sequence.Lines ?? Array.Empty<DialogueLineDefinition>())
        {
            if (line != null
                && string.Equals(line.SpeakerId, speakerId, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(line.Emote))
            {
                return line.Emote;
            }
        }

        return DefaultEmoteId;
    }

    private string ResolveDialogueLineText(string textKey)
    {
        if (string.IsNullOrWhiteSpace(textKey))
        {
            return string.Empty;
        }

        return _localization!.LocalizePlayerFacingContent(
            DefaultNarrativeLocalizationTable,
            textKey,
            textKey);
    }

    private string ResolvePresentationText(string presentationKey, bool isTitle, string fallback)
    {
        var suffix = isTitle ? "title" : "body";
        var localizationKey = $"loc.story.presentation.{ContentLocalizationTables.NormalizeId(presentationKey)}.{suffix}";
        return _localization!.LocalizePlayerFacingContent(
            DefaultNarrativeLocalizationTable,
            localizationKey,
            fallback);
    }

    private string ResolveHeroLoreTitle(HeroLoreDefinition heroLore, string fallback)
    {
        var localizationKey = $"loc.story.hero_lore.{ContentLocalizationTables.NormalizeId(heroLore.HeroId)}.0";
        return _localization!.LocalizePlayerFacingContent(
            DefaultNarrativeLocalizationTable,
            localizationKey,
            fallback);
    }

    private string ResolveHeroLoreBody(HeroLoreDefinition heroLore, string fallback)
    {
        var localizationKey = $"loc.story.hero_lore.{ContentLocalizationTables.NormalizeId(heroLore.HeroId)}.canon";
        return _localization!.LocalizePlayerFacingContent(
            DefaultNarrativeLocalizationTable,
            localizationKey,
            fallback);
    }

    private string LocalizeUiCommon(string key, string fallback)
    {
        return _localization!.LocalizeOrFallback(GameLocalizationTables.UICommon, key, fallback);
    }

    private static string HumanizePresentationKey(string presentationKey)
    {
        if (string.IsNullOrWhiteSpace(presentationKey))
        {
            return "Narrative";
        }

        var parts = presentationKey
            .Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..]);
        return string.Join(" ", parts);
    }

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    private static bool IsNarrator(string speakerId)
    {
        return string.Equals(speakerId, NarratorSpeakerId, StringComparison.OrdinalIgnoreCase);
    }

    private RuntimePanelHost? ResolvePanelHost()
    {
        foreach (var root in gameObject.scene.GetRootGameObjects())
        {
            var panelHost = root.GetComponentsInChildren<RuntimePanelHost>(true).FirstOrDefault();
            if (panelHost != null)
            {
                return panelHost;
            }
        }

        return null;
    }

    private bool IsAnyPresenterPlaying()
    {
        return (_toastPresenter?.IsPlaying ?? false)
               || (_dialogueOverlayPresenter?.IsPlaying ?? false)
               || (_dialogueScenePresenter?.IsPlaying ?? false)
               || (_storyCardPresenter?.IsPlaying ?? false);
    }

    private void DisposeNarrativeLayer()
    {
        _toastPresenter?.Dispose();
        _dialogueOverlayPresenter?.Dispose();
        _dialogueScenePresenter?.Dispose();
        _storyCardPresenter?.Dispose();
        _toastPresenter = null;
        _dialogueOverlayPresenter = null;
        _dialogueScenePresenter = null;
        _storyCardPresenter = null;
        _toastView = null;
        _dialogueOverlayView = null;
        _dialogueSceneView = null;
        _storyCardView = null;
        _portraitResolver = null;
        if (_narrativeRoot != null)
        {
            _narrativeRoot.RemoveFromHierarchy();
            _narrativeRoot = null;
        }
    }

    private void ApplyEditorAssetFallback()
    {
#if UNITY_EDITOR
        _toastTree ??= AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(ToastTreePath);
        _dialogueOverlayTree ??= AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(OverlayTreePath);
        _dialogueSceneTree ??= AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SceneTreePath);
        _storyCardTree ??= AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(CardTreePath);
        _storyCommonStyle ??= AssetDatabase.LoadAssetAtPath<StyleSheet>(CommonStylePath);
        _toastStyle ??= AssetDatabase.LoadAssetAtPath<StyleSheet>(ToastStylePath);
        _dialogueOverlayStyle ??= AssetDatabase.LoadAssetAtPath<StyleSheet>(OverlayStylePath);
        _dialogueSceneStyle ??= AssetDatabase.LoadAssetAtPath<StyleSheet>(SceneStylePath);
        _storyCardStyle ??= AssetDatabase.LoadAssetAtPath<StyleSheet>(CardStylePath);
#endif
    }

    private sealed record PendingBatch(IReadOnlyList<StoryPresentationRequest> Requests, Action? OnCompleted);
}
