using System;
using System.Collections.Generic;
using System.Linq;
using SM.Content.Definitions;
using SM.Core.Content;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UIElements;

namespace SM.Unity.UI.SiteEvents;

internal sealed class SiteEventChoicePanelController : IDisposable
{
    private const string SelectedClass = "site-event-choice__card--selected";

    private readonly VisualElement _overlay;
    private readonly VisualElement _choiceList;
    private readonly Label _hudLabel;
    private readonly Label _dialogueEyebrowLabel;
    private readonly Label _dialogueBodyLabel;
    private readonly GameLocalizationController _localization;
    private readonly ContentIconResolver _iconResolver;
    private readonly Action<string> _choiceSelected;
    private readonly List<Button> _choiceButtons = new();

    private SiteEventPresentationViewModel? _presentation;
    private HashSet<string> _legalChoiceIds = new(StringComparer.Ordinal);
    private bool _choiceCommitted;

    internal SiteEventChoicePanelController(
        VisualElement hostRoot,
        GameLocalizationController localization,
        ContentIconResolver iconResolver,
        Action<string> choiceSelected)
    {
        if (hostRoot == null) throw new ArgumentNullException(nameof(hostRoot));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _iconResolver = iconResolver ?? throw new ArgumentNullException(nameof(iconResolver));
        _choiceSelected = choiceSelected ?? throw new ArgumentNullException(nameof(choiceSelected));

        _overlay = Require<VisualElement>(hostRoot, "SiteEventChoiceOverlay");
        _choiceList = Require<VisualElement>(hostRoot, "SiteEventChoiceList");
        _hudLabel = Require<Label>(hostRoot, "SiteEventHudLabel");
        _dialogueEyebrowLabel = Require<Label>(hostRoot, "SiteEventDialogueEyebrowLabel");
        _dialogueBodyLabel = Require<Label>(hostRoot, "SiteEventDialogueBodyLabel");
        _localization.LocaleChanged += HandleLocaleChanged;
        Hide();
    }

    internal bool IsVisible => _overlay.style.display.value == DisplayStyle.Flex;
    internal int RenderedChoiceCount => _choiceButtons.Count;

    internal void Show(
        SiteEventPresentationViewModel presentation,
        IEnumerable<string> legalChoiceIds)
    {
        _presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
        _legalChoiceIds = (legalChoiceIds ?? Array.Empty<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);
        _choiceCommitted = false;
        Render();
        _overlay.style.display = DisplayStyle.Flex;
        _overlay.pickingMode = PickingMode.Position;
        _choiceButtons.FirstOrDefault(button => button.enabledSelf)?.Focus();
    }

    internal void Hide()
    {
        _overlay.style.display = DisplayStyle.None;
        _overlay.pickingMode = PickingMode.Ignore;
    }

    public void Dispose()
    {
        _localization.LocaleChanged -= HandleLocaleChanged;
    }

    private void Render()
    {
        if (_presentation == null)
        {
            return;
        }

        _hudLabel.text = Ui("ui.expedition.site_event.hud", "SITE EVENT");
        _dialogueEyebrowLabel.text = Ui("ui.expedition.site_event.dialogue.eyebrow", "FIELD REPORT");
        _dialogueBodyLabel.text = _localization.LocalizePlayerFacingContent(
            ContentLocalizationTables.Campaign,
            _presentation.SetupKey,
            string.Empty);

        _choiceList.Clear();
        _choiceButtons.Clear();
        for (var index = 0; index < _presentation.Choices.Count; index++)
        {
            var choice = _presentation.Choices[index];
            var button = BuildChoiceCard(choice, index + 1, _legalChoiceIds.Contains(choice.Id));
            _choiceList.Add(button);
            _choiceButtons.Add(button);
        }
    }

    private Button BuildChoiceCard(SiteEventChoiceViewModel choice, int number, bool isLegal)
    {
        var title = _localization.LocalizePlayerFacingContent(
            ContentLocalizationTables.Campaign,
            choice.LabelKey,
            string.Empty);
        var card = new Button
        {
            name = $"SiteEventChoice_{choice.Id}",
            text = string.Empty,
            tooltip = BuildTooltip(title, choice.OutcomePreviews),
        };
        card.AddToClassList("site-event-choice__card");

        var chevron = new Label("›");
        chevron.AddToClassList("site-event-choice__chevron");
        chevron.pickingMode = PickingMode.Ignore;
        card.Add(chevron);

        var numberDiamond = new VisualElement();
        numberDiamond.AddToClassList("site-event-choice__number");
        numberDiamond.pickingMode = PickingMode.Ignore;
        var numberLabel = new Label(number.ToString());
        numberLabel.AddToClassList("site-event-choice__number-text");
        numberDiamond.Add(numberLabel);
        card.Add(numberDiamond);

        card.Add(BuildChoiceIcon(choice.Id, choice.IconId));

        var copy = new VisualElement();
        copy.AddToClassList("site-event-choice__copy");
        copy.pickingMode = PickingMode.Ignore;
        var titleLabel = new Label(title) { name = $"SiteEventChoiceTitle_{choice.Id}" };
        titleLabel.AddToClassList("site-event-choice__title");
        copy.Add(titleLabel);

        var subtitle = new Label(BuildChoiceSubtitle(choice.OutcomePreviews.Count));
        subtitle.AddToClassList("site-event-choice__subtitle");
        copy.Add(subtitle);

        var outcomes = new VisualElement { name = $"SiteEventOutcomePreviews_{choice.Id}" };
        outcomes.AddToClassList("site-event-choice__outcomes");
        foreach (var preview in choice.OutcomePreviews)
        {
            outcomes.Add(BuildOutcomeRow(preview));
        }
        copy.Add(outcomes);

        if (!isLegal)
        {
            var unavailable = new Label(Ui(
                "ui.expedition.site_event.availability.unavailable",
                "Unavailable now"));
            unavailable.AddToClassList("site-event-choice__unavailable");
            copy.Add(unavailable);
        }

        card.Add(copy);
        card.SetEnabled(isLegal);
        card.RegisterCallback<FocusInEvent>(_ => SelectCard(card));
        card.clicked += () =>
        {
            if (_choiceCommitted)
            {
                return;
            }

            _choiceCommitted = true;
            SelectCard(card);
            foreach (var choiceButton in _choiceButtons)
            {
                choiceButton.SetEnabled(false);
            }
            _choiceSelected(choice.Id);
        };
        return card;
    }

    private VisualElement BuildChoiceIcon(string choiceId, string iconId)
    {
        var icon = new VisualElement { name = $"SiteEventChoiceIcon_{choiceId}" };
        icon.AddToClassList("site-event-choice__icon");
        icon.pickingMode = PickingMode.Ignore;
        var texture = _iconResolver.ResolveSiteEventChoice(iconId);
        if (texture != null)
        {
            icon.style.backgroundImage = new StyleBackground(texture);
            icon.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            return icon;
        }

        icon.AddToClassList("site-event-choice__icon--missing");
        var missing = new Label(Ui("ui.expedition.site_event.icon.pending", "Icon pending"));
        missing.AddToClassList("site-event-choice__icon-missing-label");
        icon.Add(missing);
        return icon;
    }

    private VisualElement BuildOutcomeRow(SiteEventOutcomePreviewViewModel preview)
    {
        var row = new VisualElement();
        row.AddToClassList("site-event-choice__outcome-row");
        row.pickingMode = PickingMode.Ignore;

        var badge = new Label(CategoryLabel(preview.Category));
        badge.AddToClassList("site-event-choice__outcome-badge");
        badge.EnableInClassList(
            "site-event-choice__outcome-badge--safe",
            preview.Category is SiteEventOutcomePreviewCategory.WoundRecovery);
        badge.EnableInClassList(
            "site-event-choice__outcome-badge--danger",
            preview.IsCost || preview.Category is SiteEventOutcomePreviewCategory.WoundRisk);
        row.Add(badge);

        var pips = new VisualElement();
        pips.AddToClassList("site-event-choice__pip-row");
        for (var index = 0; index < 5; index++)
        {
            var pip = new VisualElement();
            pip.AddToClassList("site-event-choice__pip");
            pip.EnableInClassList("site-event-choice__pip--filled", index < preview.IntensityPips);
            pips.Add(pip);
        }
        row.Add(pips);

        var certainty = CertaintyLabel(preview.Certainty);
        if (!string.IsNullOrWhiteSpace(certainty))
        {
            var certaintyLabel = new Label(certainty);
            certaintyLabel.AddToClassList("site-event-choice__certainty");
            row.Add(certaintyLabel);
        }

        return row;
    }

    private string BuildChoiceSubtitle(int outcomeCount)
    {
        return outcomeCount switch
        {
            0 => Ui("ui.expedition.site_event.choice.none", "No immediate change"),
            1 => Ui("ui.expedition.site_event.choice.single", "One consequence"),
            _ => Ui("ui.expedition.site_event.choice.multiple", "{0} consequences", outcomeCount),
        };
    }

    private string BuildTooltip(
        string title,
        IReadOnlyList<SiteEventOutcomePreviewViewModel> previews)
    {
        var summaries = previews.Select(preview =>
            Ui(
                "ui.expedition.site_event.preview.summary",
                "{0} · {1} intensity",
                CategoryLabel(preview.Category),
                IntensityLabel(preview.IntensityPips)));
        return $"{title}\n{string.Join("\n", summaries)}";
    }

    private string CategoryLabel(SiteEventOutcomePreviewCategory category)
    {
        return category switch
        {
            SiteEventOutcomePreviewCategory.NoChange => Ui("ui.expedition.site_event.category.no_change", "No change"),
            SiteEventOutcomePreviewCategory.Item => Ui("ui.expedition.site_event.category.item", "Item"),
            SiteEventOutcomePreviewCategory.Echo => Ui("ui.expedition.site_event.category.echo", "Echo"),
            SiteEventOutcomePreviewCategory.Experience => Ui("ui.expedition.site_event.category.experience", "Experience"),
            SiteEventOutcomePreviewCategory.WoundRecovery => Ui("ui.expedition.site_event.category.wound_recovery", "Wound recovery"),
            SiteEventOutcomePreviewCategory.WoundRisk => Ui("ui.expedition.site_event.category.wound_risk", "Wound risk"),
            SiteEventOutcomePreviewCategory.Route => Ui("ui.expedition.site_event.category.route", "Route change"),
            SiteEventOutcomePreviewCategory.Recruit => Ui("ui.expedition.site_event.category.recruit", "Recruit offer"),
            SiteEventOutcomePreviewCategory.Consumable => Ui("ui.expedition.site_event.category.consumable", "Consumable"),
            SiteEventOutcomePreviewCategory.ExtractBonus => Ui("ui.expedition.site_event.category.extract_bonus", "Extract bonus"),
            _ => Ui("ui.expedition.site_event.category.unknown", "Unknown"),
        };
    }

    private string CertaintyLabel(SiteEventOutcomePreviewCertainty certainty)
    {
        return certainty switch
        {
            SiteEventOutcomePreviewCertainty.TargetVaries => Ui(
                "ui.expedition.site_event.certainty.target_varies",
                "Target varies"),
            SiteEventOutcomePreviewCertainty.Unknown => Ui(
                "ui.expedition.site_event.certainty.unknown",
                "Outcome unclear"),
            _ => string.Empty,
        };
    }

    private string IntensityLabel(int pips)
    {
        return pips switch
        {
            <= 0 => Ui("ui.expedition.site_event.intensity.0", "None"),
            1 => Ui("ui.expedition.site_event.intensity.1", "Slight"),
            2 => Ui("ui.expedition.site_event.intensity.2", "Modest"),
            3 => Ui("ui.expedition.site_event.intensity.3", "Strong"),
            4 => Ui("ui.expedition.site_event.intensity.4", "Major"),
            _ => Ui("ui.expedition.site_event.intensity.5", "Extreme"),
        };
    }

    private void SelectCard(Button selected)
    {
        foreach (var button in _choiceButtons)
        {
            button.EnableInClassList(SelectedClass, ReferenceEquals(button, selected));
        }
    }

    private void HandleLocaleChanged(Locale _)
    {
        if (_presentation != null)
        {
            Render();
        }
    }

    private string Ui(string key, string fallback, params object[] arguments)
    {
        return _localization.LocalizeOrFallback(
            GameLocalizationTables.UIExpedition,
            key,
            fallback,
            arguments);
    }

    private static T Require<T>(VisualElement root, string name)
        where T : VisualElement
    {
        return root.Q<T>(name)
               ?? throw new InvalidOperationException($"Site event choice panel is missing '{name}'.");
    }
}
