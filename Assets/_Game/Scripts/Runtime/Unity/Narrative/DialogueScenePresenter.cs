using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Unity.Narrative;

public sealed class DialogueScenePresenter : IDisposable
{
    private readonly DialogueSceneView _view;
    private readonly IStoryPortraitResolver _portraitResolver;
    private DialogueScenePlaybackModel? _model;
    private Action? _onCompleted;
    private IVisualElementScheduledItem? _typingSchedule;
    private string _resolvedLineText = string.Empty;
    private int _visibleCharacterCount;
    private double _typingStartedAt;
    private bool _resumeTypingAfterConfirmation;

    public DialogueScenePresenter(DialogueSceneView view, IStoryPortraitResolver portraitResolver)
    {
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _portraitResolver = portraitResolver ?? throw new ArgumentNullException(nameof(portraitResolver));
        _view.NextRequested += AdvanceLine;
        _view.SkipAllRequested += RequestSkipAll;
        _view.SkipConfirmed += ConfirmSkipAll;
        _view.SkipCancelled += CancelSkipAll;
    }

    public bool IsPlaying { get; private set; }
    public int CurrentLineIndex { get; private set; } = -1;
    public bool IsTyping { get; private set; }
    public bool IsSkipConfirmationOpen { get; private set; }

    public void Present(DialogueScenePlaybackModel model, Action? onCompleted)
    {
        HideImmediate();

        if (model == null || model.Lines.Count == 0)
        {
            onCompleted?.Invoke();
            return;
        }

        _model = model;
        _onCompleted = onCompleted;
        CurrentLineIndex = 0;
        IsPlaying = true;
        _view.Show();
        RenderCurrentLine(restartTyping: true);
    }

    public void AdvanceLine()
    {
        if (!IsPlaying || _model == null || IsSkipConfirmationOpen)
        {
            return;
        }

        if (IsTyping)
        {
            CompleteTypingImmediately();
            return;
        }

        if (CurrentLineIndex >= _model.Lines.Count - 1)
        {
            Complete();
            return;
        }

        CurrentLineIndex++;
        RenderCurrentLine(restartTyping: true);
    }

    public void RequestSkipAll()
    {
        if (!IsPlaying || _model == null)
        {
            return;
        }

        if (!_model.RequireSkipConfirmation)
        {
            Complete();
            return;
        }

        if (IsSkipConfirmationOpen)
        {
            return;
        }

        IsSkipConfirmationOpen = true;
        _resumeTypingAfterConfirmation = IsTyping;
        StopTypingSchedule();
        RenderState();
    }

    public void ConfirmSkipAll()
    {
        if (!IsPlaying)
        {
            return;
        }

        Complete();
    }

    public void CancelSkipAll()
    {
        if (!IsPlaying || !IsSkipConfirmationOpen)
        {
            return;
        }

        IsSkipConfirmationOpen = false;
        RenderState();
        if (_resumeTypingAfterConfirmation)
        {
            ResumeTyping();
        }

        _resumeTypingAfterConfirmation = false;
    }

    public void HideImmediate()
    {
        StopTypingSchedule();
        _model = null;
        _onCompleted = null;
        _resolvedLineText = string.Empty;
        _visibleCharacterCount = 0;
        _resumeTypingAfterConfirmation = false;
        CurrentLineIndex = -1;
        IsPlaying = false;
        IsTyping = false;
        IsSkipConfirmationOpen = false;
        _view.Hide();
    }

    public void Dispose()
    {
        StopTypingSchedule();
        _view.NextRequested -= AdvanceLine;
        _view.SkipAllRequested -= RequestSkipAll;
        _view.SkipConfirmed -= ConfirmSkipAll;
        _view.SkipCancelled -= CancelSkipAll;
        _view.Dispose();
    }

    private void Complete()
    {
        if (!IsPlaying)
        {
            return;
        }

        var callback = _onCompleted;
        HideImmediate();
        callback?.Invoke();
    }

    private void RenderCurrentLine(bool restartTyping)
    {
        if (_model == null || CurrentLineIndex < 0 || CurrentLineIndex >= _model.Lines.Count)
        {
            return;
        }

        var line = _model.Lines[CurrentLineIndex];
        _resolvedLineText = line.IsNarrator ? line.LineText : Quote(line.LineText);
        _visibleCharacterCount = restartTyping ? 0 : Math.Min(_visibleCharacterCount, _resolvedLineText.Length);
        IsSkipConfirmationOpen = false;
        _resumeTypingAfterConfirmation = false;

        if (restartTyping && ShouldTypeCurrentLine())
        {
            IsTyping = true;
            StartTyping();
        }
        else
        {
            IsTyping = false;
            StopTypingSchedule();
            _visibleCharacterCount = _resolvedLineText.Length;
        }

        RenderState();
    }

    private void RenderState()
    {
        if (_model == null || CurrentLineIndex < 0 || CurrentLineIndex >= _model.Lines.Count)
        {
            return;
        }

        var line = _model.Lines[CurrentLineIndex];
        var activeSide = line.IsNarrator ? StorySpeakerSide.None : line.SpeakerSide;
        var leftPortrait = ResolvePortrait(_model.LeftSpeaker, line, StorySpeakerSide.Left);
        var rightPortrait = ResolvePortrait(_model.RightSpeaker, line, StorySpeakerSide.Right);
        var state = new DialogueSceneViewState(
            line.IsNarrator ? string.Empty : line.SpeakerNameText,
            _resolvedLineText,
            line.IsNarrator,
            activeSide,
            leftPortrait,
            _model.LeftSpeaker.HasValue && leftPortrait != null,
            rightPortrait,
            _model.RightSpeaker.HasValue && rightPortrait != null,
            ShowSkipAll: true,
            ShowSkipConfirmation: IsSkipConfirmationOpen,
            _model.SkipConfirmTitleText,
            _model.SkipConfirmBodyText,
            IsTyping,
            ShowContinueHint: !IsTyping);

        _view.Render(state);
        _view.SetDisplayedText(_resolvedLineText[..Math.Min(_visibleCharacterCount, _resolvedLineText.Length)]);
    }

    private void StartTyping()
    {
        StopTypingSchedule();
        var cps = GetCharactersPerSecond();
        _typingStartedAt = Time.realtimeSinceStartupAsDouble;
        var intervalMilliseconds = Math.Max(16, Mathf.RoundToInt(1000f / cps));
        _typingSchedule = _view.Root.schedule.Execute(UpdateTyping).Every(intervalMilliseconds);
    }

    private void ResumeTyping()
    {
        if (_resolvedLineText.Length == 0)
        {
            CompleteTypingImmediately();
            return;
        }

        var cps = GetCharactersPerSecond();
        _typingStartedAt = Time.realtimeSinceStartupAsDouble - (_visibleCharacterCount / cps);
        IsTyping = true;
        StopTypingSchedule();
        var intervalMilliseconds = Math.Max(16, Mathf.RoundToInt(1000f / cps));
        _typingSchedule = _view.Root.schedule.Execute(UpdateTyping).Every(intervalMilliseconds);
    }

    private void UpdateTyping()
    {
        if (!IsPlaying || !IsTyping)
        {
            StopTypingSchedule();
            return;
        }

        var cps = GetCharactersPerSecond();
        var visibleCharacters = Mathf.Clamp(
            Mathf.FloorToInt((float)((Time.realtimeSinceStartupAsDouble - _typingStartedAt) * cps)),
            0,
            _resolvedLineText.Length);

        if (visibleCharacters == _visibleCharacterCount)
        {
            return;
        }

        _visibleCharacterCount = visibleCharacters;
        _view.SetDisplayedText(_resolvedLineText[.._visibleCharacterCount]);
        if (_visibleCharacterCount >= _resolvedLineText.Length)
        {
            CompleteTypingImmediately();
        }
    }

    private void CompleteTypingImmediately()
    {
        StopTypingSchedule();
        IsTyping = false;
        _visibleCharacterCount = _resolvedLineText.Length;
        RenderState();
    }

    private void StopTypingSchedule()
    {
        if (_typingSchedule == null)
        {
            return;
        }

        _typingSchedule.Pause();
        _typingSchedule = null;
    }

    private bool ShouldTypeCurrentLine()
    {
        return _model != null
               && _model.EnableTypingEffect
               && !IsSkipConfirmationOpen
               && !string.IsNullOrEmpty(_resolvedLineText);
    }

    private float GetCharactersPerSecond()
    {
        return _model == null || _model.CharactersPerSecond <= 0f
            ? 40f
            : _model.CharactersPerSecond;
    }

    private Sprite? ResolvePortrait(StorySpeakerModel? speaker, StoryDialogueLineModel line, StorySpeakerSide side)
    {
        if (!speaker.HasValue)
        {
            return null;
        }

        var resolvedSpeaker = speaker.Value;
        var emoteId = line.IsNarrator
            ? resolvedSpeaker.DefaultEmoteId
            : line.SpeakerSide == side
                ? line.EmoteId
                : resolvedSpeaker.DefaultEmoteId;

        return _portraitResolver.TryResolve(resolvedSpeaker.CharacterId, emoteId, side, out var portrait)
            ? portrait
            : null;
    }

    private static string Quote(string lineText)
    {
        return $"\"{lineText}\"";
    }
}
