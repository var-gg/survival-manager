using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

namespace SM.Unity;

[RequireComponent(typeof(Text))]
[RequireComponent(typeof(LocalizeStringEvent))]
public sealed class UguiTextLocalizerBinder : MonoBehaviour
{
    [SerializeField] private string tableCollection = GameLocalizationTables.UICommon;
    [SerializeField] private string entryKey = string.Empty;
    [SerializeField] private Text targetText = null!;
    [SerializeField] private LocalizeStringEvent localizeStringEvent = null!;

    private bool _isBound;
    private string _fallbackText = string.Empty;

    public string TableCollection => tableCollection;
    public string EntryKey => entryKey;

    public void Configure(string table, string key)
    {
        tableCollection = table;
        entryKey = key;
        EnsureBinding();
    }

    private void Reset()
    {
        targetText = GetComponent<Text>();
        localizeStringEvent = GetComponent<LocalizeStringEvent>();
    }

    private void Awake()
    {
        EnsureBinding();
    }

    private void OnEnable()
    {
        EnsureBinding();
        localizeStringEvent.RefreshString();
    }

    private void OnDisable()
    {
        if (_isBound)
        {
            localizeStringEvent.OnUpdateString.RemoveListener(HandleStringChanged);
            _isBound = false;
        }
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            EnsureBinding();
        }
    }

    private void EnsureBinding()
    {
        targetText ??= GetComponent<Text>();
        localizeStringEvent ??= GetComponent<LocalizeStringEvent>();

        if (targetText == null || localizeStringEvent == null || string.IsNullOrWhiteSpace(entryKey))
        {
            return;
        }

        GameFontCatalog.ApplyFont(targetText);
        if (string.IsNullOrWhiteSpace(_fallbackText) && targetText != null && !string.IsNullOrWhiteSpace(targetText.text))
        {
            _fallbackText = targetText.text;
        }

        localizeStringEvent.StringReference.TableReference = tableCollection;
        localizeStringEvent.StringReference.TableEntryReference = entryKey;

        if (_isBound)
        {
            localizeStringEvent.OnUpdateString.RemoveListener(HandleStringChanged);
        }

        localizeStringEvent.OnUpdateString.AddListener(HandleStringChanged);
        _isBound = true;
    }

    private void HandleStringChanged(string value)
    {
        if (targetText != null)
        {
            if (LooksLikeMissingLocalization(value))
            {
                GameSessionRoot.Instance?.Localization?.ReportMissingKey(tableCollection, entryKey);
                targetText.text = string.IsNullOrWhiteSpace(_fallbackText) ? targetText.text : _fallbackText;
                return;
            }

            if (!string.IsNullOrWhiteSpace(value))
            {
                targetText.text = value;
            }
        }
    }

    private bool LooksLikeMissingLocalization(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        return value.Contains("[missing:", System.StringComparison.OrdinalIgnoreCase)
               || value.Contains("No translation found", System.StringComparison.OrdinalIgnoreCase)
               || value.Contains(tableCollection, System.StringComparison.OrdinalIgnoreCase)
               || value.Contains(entryKey, System.StringComparison.OrdinalIgnoreCase);
    }
}
