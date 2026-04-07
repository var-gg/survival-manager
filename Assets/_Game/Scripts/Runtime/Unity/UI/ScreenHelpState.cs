using UnityEngine;

namespace SM.Unity.UI;

public sealed class ScreenHelpState
{
    private readonly string _prefsKey;
    private bool _isVisible;

    public ScreenHelpState(string prefsKey)
    {
        _prefsKey = prefsKey;
        _isVisible = PlayerPrefs.GetInt(_prefsKey, 0) == 0;
    }

    public bool IsVisible => _isVisible;

    public void Toggle()
    {
        _isVisible = !_isVisible;
    }

    public void Dismiss()
    {
        _isVisible = false;
        PlayerPrefs.SetInt(_prefsKey, 1);
        PlayerPrefs.Save();
    }
}
