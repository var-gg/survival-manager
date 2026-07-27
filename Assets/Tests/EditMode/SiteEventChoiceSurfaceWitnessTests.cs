using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Tests.EditMode.Fakes;
using SM.Unity;
using SM.Unity.ContentConversion;
using SM.Unity.UI;
using SM.Unity.UI.SiteEvents;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SM.Tests.EditMode;

[Category("BatchOnly")]
public sealed class SiteEventChoiceSurfaceWitnessTests
{
    private static readonly string[] RequiredUiKeys =
    {
        "ui.expedition.site_event.hud",
        "ui.expedition.site_event.dialogue.eyebrow",
        "ui.expedition.site_event.choice.none",
        "ui.expedition.site_event.choice.single",
        "ui.expedition.site_event.choice.multiple",
        "ui.expedition.site_event.icon.pending",
        "ui.expedition.site_event.availability.unavailable",
        "ui.expedition.site_event.category.no_change",
        "ui.expedition.site_event.category.item",
        "ui.expedition.site_event.category.echo",
        "ui.expedition.site_event.category.experience",
        "ui.expedition.site_event.category.wound_recovery",
        "ui.expedition.site_event.category.wound_risk",
        "ui.expedition.site_event.category.route",
        "ui.expedition.site_event.category.recruit",
        "ui.expedition.site_event.category.consumable",
        "ui.expedition.site_event.category.extract_bonus",
        "ui.expedition.site_event.category.unknown",
        "ui.expedition.site_event.certainty.target_varies",
        "ui.expedition.site_event.certainty.unknown",
        "ui.expedition.site_event.intensity.0",
        "ui.expedition.site_event.intensity.1",
        "ui.expedition.site_event.intensity.2",
        "ui.expedition.site_event.intensity.3",
        "ui.expedition.site_event.intensity.4",
        "ui.expedition.site_event.intensity.5",
        "ui.expedition.site_event.preview.summary",
        "ui.expedition.site_event.error.surface_missing",
        "ui.expedition.site_event.error.choice_failed",
    };

    [Test]
    public void AllSixAuthoredEvents_HaveLocalizedLabels_AuthoredIcons_AndNonEmptyTruthfulPreviews()
    {
        var definitions = Resources
            .LoadAll<SiteEventDefinition>("_Game/Content/Definitions/SiteEvents")
            .OrderBy(definition => definition.Id, StringComparer.Ordinal)
            .ToArray();
        Assert.That(definitions, Has.Length.EqualTo(6));

        var campaignTables = RequireTables(ContentLocalizationTables.Campaign);
        var uiTables = RequireTables(GameLocalizationTables.UIExpedition);
        foreach (var key in RequiredUiKeys)
        {
            AssertLocalized(uiTables, key);
        }

        var choiceCount = 0;
        foreach (var definition in definitions)
        {
            AssertLocalized(campaignTables, definition.SetupKey.Replace("{siteId}", "site_ashen_gate", StringComparison.Ordinal));
            var template = CampaignConverter.BuildSiteEventTemplate(definition);
            Assert.That(template.Choices, Has.Count.EqualTo(definition.Choices.Count), definition.Id);
            for (var index = 0; index < template.Choices.Count; index++)
            {
                var choice = template.Choices[index];
                var authoredChoice = definition.Choices[index];
                choiceCount++;
                Assert.That(authoredChoice.IconId, Is.Not.Empty, $"{definition.Id}/{choice.Id} IconId");
                Assert.That(
                    authoredChoice.IconId,
                    Does.StartWith("site_event_choice_"),
                    $"{definition.Id}/{choice.Id} must carry authored site-event icon identity.");
                AssertLocalized(campaignTables, choice.LabelKey);

                var previews = SiteEventOutcomePreviewBuilder.Build(choice.Outcomes);
                Assert.That(previews, Is.Not.Empty, $"{definition.Id}/{choice.Id} preview");
                Assert.That(
                    previews.Count,
                    Is.EqualTo(Math.Max(1, choice.Outcomes.Count)),
                    $"{definition.Id}/{choice.Id} must render every authored consequence.");
                Assert.That(
                    previews.All(preview => preview.IntensityPips is >= 0 and <= 5),
                    Is.True,
                    $"{definition.Id}/{choice.Id} intensity band");
            }
        }

        Assert.That(choiceCount, Is.EqualTo(17));
    }

    [Test]
    public void AllSixAuthoredEvents_RenderThroughTheProductionPanel_WithoutRawKeysOrEmptyPreviews()
    {
        LocalizationSettings.InitializationOperation.WaitForCompletion();
        var selectedLocaleBefore = LocalizationSettings.SelectedLocale;
        var hadSelectedLocalePreference = PlayerPrefs.HasKey("selected-locale");
        var selectedLocalePreferenceBefore = PlayerPrefs.GetString("selected-locale", string.Empty);
        var localizationObject = new GameObject("SiteEventChoiceLocalizationWitness");
        var localization = localizationObject.AddComponent<GameLocalizationController>();
        var initialization = localization.EnsureInitialized();
        while (initialization.MoveNext())
        {
            // InitializationOperation is already complete; advancing the enumerator runs the normal controller path.
        }

        var atlasTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
            "Assets/_Game/UI/Screens/Atlas/AtlasScreen.uxml");
        Assert.That(atlasTree, Is.Not.Null);
        var host = atlasTree!.CloneTree();
        var controller = new SiteEventChoicePanelController(
            host,
            localization,
            new ContentIconResolver(new FakeCombatContentLookup()),
            _ => { });

        try
        {
            var definitions = Resources
                .LoadAll<SiteEventDefinition>("_Game/Content/Definitions/SiteEvents")
                .OrderBy(definition => definition.Id, StringComparer.Ordinal);
            foreach (var definition in definitions)
            {
                var template = CampaignConverter.BuildSiteEventTemplate(definition);
                var presentation = new SiteEventPresentationViewModel(
                    template.Id,
                    template.SetupKey.Replace("{siteId}", "site_ashen_gate", StringComparison.Ordinal),
                    template.Choices
                        .Select((choice, index) => new SiteEventChoiceViewModel(
                            choice.Id,
                            choice.LabelKey,
                            definition.Choices[index].IconId,
                            SiteEventOutcomePreviewBuilder.Build(choice.Outcomes)))
                        .ToArray());
                controller.Show(presentation, presentation.Choices.Select(choice => choice.Id));

                Assert.That(controller.IsVisible, Is.True, definition.Id);
                Assert.That(controller.RenderedChoiceCount, Is.EqualTo(definition.Choices.Count), definition.Id);
                foreach (var choice in presentation.Choices)
                {
                    var title = host.Q<Label>($"SiteEventChoiceTitle_{choice.Id}")?.text ?? string.Empty;
                    AssertPlayerFacingText(title, $"{definition.Id}/{choice.Id} title");
                    var previewRoot = host.Q<VisualElement>($"SiteEventOutcomePreviews_{choice.Id}");
                    Assert.That(previewRoot, Is.Not.Null, $"{definition.Id}/{choice.Id} preview root");
                    Assert.That(previewRoot!.childCount, Is.EqualTo(choice.OutcomePreviews.Count));
                    Assert.That(previewRoot.childCount, Is.GreaterThan(0));
                }
            }
        }
        finally
        {
            controller.Dispose();
            Object.DestroyImmediate(localizationObject);
            if (selectedLocaleBefore != null)
            {
                LocalizationSettings.SelectedLocale = selectedLocaleBefore;
            }

            if (hadSelectedLocalePreference)
            {
                PlayerPrefs.SetString("selected-locale", selectedLocalePreferenceBefore);
            }
            else
            {
                PlayerPrefs.DeleteKey("selected-locale");
            }

            PlayerPrefs.Save();
        }
    }

    private static IReadOnlyList<StringTable> RequireTables(string collectionName)
    {
        var collection = LocalizationEditorSettings.GetStringTableCollection(collectionName);
        Assert.That(collection, Is.Not.Null, collectionName);
        var tables = new List<StringTable>();
        foreach (var localeCode in new[] { "ko", "en" })
        {
            var table = collection!.GetTable(new LocaleIdentifier(localeCode)) as StringTable;
            Assert.That(table, Is.Not.Null, $"{collectionName}/{localeCode}");
            tables.Add(table!);
        }

        return tables;
    }

    private static void AssertLocalized(IReadOnlyList<StringTable> tables, string key)
    {
        foreach (var table in tables)
        {
            var value = table.GetEntry(key)?.Value ?? string.Empty;
            Assert.That(value, Is.Not.Empty, $"{table.LocaleIdentifier.Code}:{key}");
            Assert.That(value, Is.Not.EqualTo(key), $"{table.LocaleIdentifier.Code}:{key}");
            Assert.That(value, Does.Not.StartWith("content."), $"{table.LocaleIdentifier.Code}:{key}");
            Assert.That(value, Does.Not.StartWith("ui."), $"{table.LocaleIdentifier.Code}:{key}");
            Assert.That(value, Does.Not.StartWith("No translation found"), $"{table.LocaleIdentifier.Code}:{key}");
        }
    }

    private static void AssertPlayerFacingText(string value, string context)
    {
        Assert.That(value, Is.Not.Empty, context);
        Assert.That(value, Does.Not.StartWith("content."), context);
        Assert.That(value, Does.Not.StartWith("ui."), context);
        Assert.That(value, Does.Not.StartWith("No translation found"), context);
        Assert.That(value, Is.Not.EqualTo("[missing-localization]"), context);
    }
}
