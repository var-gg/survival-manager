using System;
using UnityEngine.UIElements;

namespace SM.Unity.Narrative;

public sealed class ToastBannerPresenter : IDisposable
{
    private readonly StoryToastBannerView _view;
    private Action? _onCompleted;
    private IVisualElementScheduledItem? _dismissSchedule;

    public ToastBannerPresenter(StoryToastBannerView view)
    {
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _view.DismissRequested += HandleDismissRequested;
    }

    public bool IsPlaying { get; private set; }

    public void Present(StoryToastBannerViewState viewState, float holdSeconds, Action? onCompleted)
    {
        DismissImmediate();

        IsPlaying = true;
        _onCompleted = onCompleted;
        _view.Render(viewState);
        _view.Show();

        var delayMilliseconds = Math.Max(0, (int)Math.Round(holdSeconds * 1000f));
        if (delayMilliseconds <= 0)
        {
            return;
        }

        _dismissSchedule = _view.Root.schedule.Execute(Complete);
        _dismissSchedule.ExecuteLater(delayMilliseconds);
    }

    public void DismissImmediate()
    {
        StopDismissSchedule();
        _onCompleted = null;
        IsPlaying = false;
        _view.Hide();
    }

    public void Dispose()
    {
        StopDismissSchedule();
        _view.DismissRequested -= HandleDismissRequested;
        _view.Dispose();
    }

    private void HandleDismissRequested()
    {
        if (!IsPlaying)
        {
            return;
        }

        Complete();
    }

    private void Complete()
    {
        if (!IsPlaying)
        {
            return;
        }

        var callback = _onCompleted;
        StopDismissSchedule();
        _onCompleted = null;
        IsPlaying = false;
        _view.Hide();
        callback?.Invoke();
    }

    private void StopDismissSchedule()
    {
        if (_dismissSchedule == null)
        {
            return;
        }

        _dismissSchedule.Pause();
        _dismissSchedule = null;
    }
}
