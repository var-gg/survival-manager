using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Unity.UI.Panels.SettingsGlobal;

[DisallowMultipleComponent]
[RequireComponent(typeof(UIDocument))]
public sealed class SettingsGlobalController : MonoBehaviour
{
    [SerializeField] private UIDocument document = null!;
    [SerializeField] private bool openOnStart = true;

    private VisualElement _root = null!;
    private VisualElement _categorySidebar = null!;
    private VisualElement _optionList = null!;
    private Button? _closeButton;
    private Button? _applyButton;
    private Button? _cancelButton;
    private Button? _resetButton;
    private readonly List<OptionState> _options = new();
    private string _selectedCategoryId = "display";
    private bool _ready;

    private void Reset()
    {
        document = GetComponent<UIDocument>();
    }

    private void Start()
    {
        EnsureReady();
        if (openOnStart)
        {
            Open();
        }
    }

    public void Open()
    {
        EnsureReady();
        _root.style.display = DisplayStyle.Flex;
        _root.RemoveFromClassList("sm-modal-anim--enter");
    }

    public void Close()
    {
        if (_root == null)
        {
            return;
        }

        _root.style.display = DisplayStyle.None;
        _root.AddToClassList("sm-modal-anim--enter");
    }

    public void ReloadDummyData()
    {
        EnsureReady();
        ResetFixture();
        Render();
    }

    private void EnsureReady()
    {
        if (_ready)
        {
            return;
        }

        if (document == null)
        {
            document = GetComponent<UIDocument>();
        }

        _root = document.rootVisualElement.Q<VisualElement>("StpRoot")
            ?? throw new InvalidOperationException("Missing UITK element 'StpRoot'.");
        _categorySidebar = document.rootVisualElement.Q<VisualElement>("CategorySidebar")
            ?? throw new InvalidOperationException("Missing UITK element 'CategorySidebar'.");
        _optionList = document.rootVisualElement.Q<VisualElement>("OptionList")
            ?? throw new InvalidOperationException("Missing UITK element 'OptionList'.");

        _closeButton = document.rootVisualElement.Q<Button>(className: "stp-header__close");
        _applyButton = document.rootVisualElement.Q<Button>(className: "stp-actions__apply");
        _cancelButton = document.rootVisualElement.Q<Button>(className: "stp-actions__cancel");
        _resetButton = document.rootVisualElement.Q<Button>(className: "stp-actions__reset");

        if (_closeButton != null) _closeButton.clicked += Close;
        if (_applyButton != null) _applyButton.clicked += Apply;
        if (_cancelButton != null) _cancelButton.clicked += Cancel;
        if (_resetButton != null) _resetButton.clicked += ReloadDummyData;

        SetButtonText(_applyButton, "APPLY");
        SetButtonText(_cancelButton, "CANCEL");
        SetButtonText(_resetButton, "RESET");

        ResetFixture();
        Render();
        _ready = true;
    }

    private void ResetFixture()
    {
        _options.Clear();
        _options.AddRange(new[]
        {
            new OptionState("display", "해상도", OptionKind.Segmented, "1600", "1600", new[] { "1280", "1600", "1920" }),
            new OptionState("display", "밝기", OptionKind.Slider, "70", "70", Array.Empty<string>()),
            new OptionState("audio", "마스터", OptionKind.Slider, "82", "82", Array.Empty<string>()),
            new OptionState("audio", "전투 효과음", OptionKind.Toggle, "on", "on", Array.Empty<string>()),
            new OptionState("control", "카메라 흔들림", OptionKind.Toggle, "on", "on", Array.Empty<string>()),
            new OptionState("control", "커서 감도", OptionKind.Slider, "54", "54", Array.Empty<string>()),
            new OptionState("account", "클라우드 저장", OptionKind.Toggle, "off", "off", Array.Empty<string>()),
            new OptionState("help", "전투 힌트", OptionKind.Toggle, "on", "on", Array.Empty<string>()),
        });
    }

    private void Render()
    {
        RenderCategories();
        RenderOptions();
    }

    private void RenderCategories()
    {
        _categorySidebar.Clear();
        foreach (var category in Categories)
        {
            var row = new VisualElement();
            row.AddToClassList("stp-sidebar__row");
            row.AddToClassList("sm-hover-raise");
            row.EnableInClassList("stp-sidebar__row--selected", category.Id == _selectedCategoryId);
            row.RegisterCallback<ClickEvent>(_ =>
            {
                _selectedCategoryId = category.Id;
                Render();
            });

            var icon = new VisualElement();
            icon.AddToClassList("stp-sidebar__icon");
            row.Add(icon);

            var label = new Label(category.Label);
            label.AddToClassList("stp-sidebar__label");
            row.Add(label);
            _categorySidebar.Add(row);
        }
    }

    private void RenderOptions()
    {
        _optionList.Clear();
        foreach (var option in _options)
        {
            if (!string.Equals(option.CategoryId, _selectedCategoryId, StringComparison.Ordinal))
            {
                continue;
            }

            _optionList.Add(BuildOptionRow(option));
        }
    }

    private VisualElement BuildOptionRow(OptionState option)
    {
        var row = new VisualElement();
        row.AddToClassList("stp-opt-row");
        row.EnableInClassList("stp-opt-row--dirty", option.IsDirty);

        var label = new Label(option.Label);
        label.AddToClassList("stp-opt-row__label");
        row.Add(label);

        var dirtyMark = new Label(option.IsDirty ? "*" : string.Empty);
        dirtyMark.AddToClassList("stp-opt-row__dirty-mark");
        row.Add(dirtyMark);

        var control = new VisualElement();
        control.AddToClassList("stp-opt-row__control");
        BuildControl(control, option);
        row.Add(control);

        var reset = new Button(() =>
        {
            option.Value = option.DefaultValue;
            RenderOptions();
        })
        {
            text = "R",
        };
        reset.AddToClassList("stp-opt-row__reset");
        reset.EnableInClassList("stp-opt-row__reset--disabled", !option.IsDirty);
        reset.SetEnabled(option.IsDirty);
        row.Add(reset);
        return row;
    }

    private void BuildControl(VisualElement control, OptionState option)
    {
        switch (option.Kind)
        {
            case OptionKind.Slider:
                var bar = new VisualElement();
                bar.AddToClassList("stp-opt-row__slider-bar");
                var fill = new VisualElement();
                fill.AddToClassList("stp-opt-row__slider-fill");
                fill.style.width = new StyleLength(new Length(ParsePercent(option.Value), LengthUnit.Percent));
                var knob = new VisualElement();
                knob.AddToClassList("stp-opt-row__slider-knob");
                bar.Add(fill);
                control.Add(bar);
                control.Add(knob);
                control.RegisterCallback<ClickEvent>(_ =>
                {
                    option.Value = option.Value == "70" ? "86" : "70";
                    RenderOptions();
                });
                break;
            case OptionKind.Toggle:
                var toggle = new VisualElement();
                toggle.AddToClassList("stp-opt-row__toggle");
                toggle.EnableInClassList("stp-opt-row__toggle--on", option.Value == "on");
                toggle.RegisterCallback<ClickEvent>(_ =>
                {
                    option.Value = option.Value == "on" ? "off" : "on";
                    RenderOptions();
                });
                var knobToggle = new VisualElement();
                knobToggle.AddToClassList("stp-opt-row__toggle-knob");
                toggle.Add(knobToggle);
                control.Add(toggle);
                break;
            case OptionKind.Segmented:
                var segment = new VisualElement();
                segment.AddToClassList("stp-opt-row__seg");
                foreach (var choice in option.Choices)
                {
                    var cell = new Button(() =>
                    {
                        option.Value = choice;
                        RenderOptions();
                    })
                    {
                        text = choice,
                    };
                    cell.AddToClassList("stp-opt-row__seg-cell");
                    cell.EnableInClassList("stp-opt-row__seg-cell--selected", option.Value == choice);
                    segment.Add(cell);
                }
                control.Add(segment);
                break;
        }
    }

    private void Apply()
    {
        foreach (var option in _options)
        {
            option.DefaultValue = option.Value;
        }

        RenderOptions();
    }

    private void Cancel()
    {
        foreach (var option in _options)
        {
            option.Value = option.DefaultValue;
        }

        RenderOptions();
    }

    private static float ParsePercent(string value)
    {
        return float.TryParse(value, out var parsed)
            ? Mathf.Clamp(parsed, 0f, 100f)
            : 0f;
    }

    private static void SetButtonText(Button? button, string text)
    {
        if (button != null)
        {
            button.text = text;
        }
    }

    private static readonly CategorySpec[] Categories =
    {
        new("display", "화면"),
        new("audio", "오디오"),
        new("control", "조작"),
        new("account", "계정"),
        new("help", "도움말"),
    };

    private sealed record CategorySpec(string Id, string Label);

    private sealed class OptionState
    {
        public OptionState(string categoryId, string label, OptionKind kind, string value, string defaultValue, IReadOnlyList<string> choices)
        {
            CategoryId = categoryId;
            Label = label;
            Kind = kind;
            Value = value;
            DefaultValue = defaultValue;
            Choices = choices;
        }

        public string CategoryId { get; }
        public string Label { get; }
        public OptionKind Kind { get; }
        public string Value { get; set; }
        public string DefaultValue { get; set; }
        public IReadOnlyList<string> Choices { get; }
        public bool IsDirty => !string.Equals(Value, DefaultValue, StringComparison.Ordinal);
    }

    private enum OptionKind
    {
        Slider,
        Toggle,
        Segmented,
    }
}
