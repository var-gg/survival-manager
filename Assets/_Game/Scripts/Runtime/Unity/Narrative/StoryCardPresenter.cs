using System;

namespace SM.Unity.Narrative;

public sealed class StoryCardPresenter : IDisposable
{
    private readonly StoryCardView _view;
    private Action? _onCompleted;

    public StoryCardPresenter(StoryCardView view)
    {
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _view.ContinueRequested += Continue;
        _view.SkipRequested += Skip;
    }

    public bool IsPlaying { get; private set; }

    public void Present(StoryCardViewState viewState, Action? onCompleted)
    {
        HideImmediate();
        IsPlaying = true;
        _onCompleted = onCompleted;
        _view.Render(viewState);
        _view.Show();
    }

    public void Continue()
    {
        if (!IsPlaying)
        {
            return;
        }

        Complete();
    }

    public void Skip()
    {
        if (!IsPlaying)
        {
            return;
        }

        Complete();
    }

    public void HideImmediate()
    {
        _onCompleted = null;
        IsPlaying = false;
        _view.Hide();
    }

    public void Dispose()
    {
        _view.ContinueRequested -= Continue;
        _view.SkipRequested -= Skip;
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
}
