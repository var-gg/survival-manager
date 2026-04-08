using UnityEngine;

namespace SM.Unity.UI;

public sealed class ScreenHelpState
{
    private readonly string _prefsKey;
    private bool _isVisible;
    private bool _loaded;

    public ScreenHelpState(string prefsKey)
    {
        _prefsKey = prefsKey;
    }

    public bool IsVisible
    {
        get
        {
            EnsureLoaded();
            return _isVisible;
        }
    }

    public void Toggle()
    {
        EnsureLoaded();
        _isVisible = !_isVisible;
    }

    public void Dismiss()
    {
        EnsureLoaded();
        _isVisible = false;
        PlayerPrefs.SetInt(_prefsKey, 1);
        PlayerPrefs.Save();
    }

    private void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }

        _isVisible = PlayerPrefs.GetInt(_prefsKey, 0) == 0;
        _loaded = true;
    }
}
