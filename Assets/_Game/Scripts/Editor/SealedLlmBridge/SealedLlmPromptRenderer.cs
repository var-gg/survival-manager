using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using SM.Combat.Model;
using SM.HeadlessMetrics;
using SM.HeadlessPolicies;

namespace SM.SealedLlmBridge;

/// <summary>Player-visible observation plus exact legal syntax to deterministic UTF-8 prompt text.</summary>
public static class SealedLlmPromptRenderer
{
    private const string DecisionResponseSchema =
        "{\"selected_action\":\"string\",\"declared_intent\":{\"intent_id\":\"string\","
        + "\"track_token_ids\":[\"kind:exact_observed_id\"],\"expected_payoff\":\"string\","
        + "\"evidence_fact_ids\":[\"exact_observed_fact_id\"],\"next_acquisition_plan\":\"string\","
        + "\"allowed_substitutions\":[\"string\"],\"pivot_conditions\":[\"string\"],\"confidence\":1.0},"
        + "\"intent_ref\":\"string\",\"build_hypotheses\":[{\"subject_kind\":\"string\","
        + "\"subject_id\":\"string\",\"relation\":\"string\",\"target_kind\":\"string\","
        + "\"target_id\":\"string\",\"evidence_refs\":[\"string\"],\"confidence\":1.0}]}";

    private const string RunReportResponseSchema =
        "{\"desire_retrospective\":\"string\",\"payoff_or_near_miss\":\"string\","
        + "\"next_concept\":\"string\",\"complaints\":[\"string\"],"
        + "\"evaluation_sentences\":[{\"sentence\":\"string\","
        + "\"telemetry_event_ids\":[\"string\"]}],\"retry_intent\":\"string\"}";

    private const string TrackKinds =
        "archetype,skill,item,item_instance,affix,augment,passive,synergy,status,tag,team_rule";

    public static string Render(
        SealedDecisionSeamKey seamKey,
        HeadlessPolicyObservation observation,
        IReadOnlyList<string> legalActionKeys,
        LlmPromptManifestV1 manifest)
    {
        ValidateSeam(seamKey, SealedLlmSeamTypes.Deployment, SealedLlmSeamTypes.Prep, SealedLlmSeamTypes.Reward);
        HeadlessPolicyGuard.ValidateObservation(
            observation ?? throw new ArgumentNullException(nameof(observation)));
        return RenderDecision(seamKey, observation, legalActionKeys, manifest);
    }

    public static string Render(
        SealedDecisionSeamKey seamKey,
        HeadlessRosterPolicyObservation observation,
        IReadOnlyList<string> legalActionKeys,
        LlmPromptManifestV1 manifest)
    {
        ValidateSeam(
            seamKey,
            SealedLlmSeamTypes.Recruit,
            SealedLlmSeamTypes.Passive,
            SealedLlmSeamTypes.Refit);
        HeadlessRosterPolicyGuard.ValidateObservation(
            observation ?? throw new ArgumentNullException(nameof(observation)));
        return RenderDecision(seamKey, observation, legalActionKeys, manifest);
    }

    public static string RenderRunReport(
        SealedDecisionSeamKey seamKey,
        string statusToken,
        LlmPromptManifestV1 manifest)
    {
        ValidateSeam(seamKey, SealedLlmSeamTypes.RunReport);
        RequireManifest(manifest);
        if (string.IsNullOrWhiteSpace(statusToken))
        {
            throw new ArgumentException("Run-report status token is required.", nameof(statusToken));
        }

        var prompt = new StringBuilder();
        AppendHeader(prompt, manifest);
        prompt.Append("Response schema (return one JSON object, without a Markdown fence):\n")
            .Append(RunReportResponseSchema)
            .Append("\n[B] RUN REPORT\nseam_key=")
            .Append(FormatSeam(seamKey))
            .Append("\nstatus_token=")
            .Append(statusToken)
            .Append("\nDo not restate prior prompts. Report only your remembered run assessment in the exact schema.");
        return prompt.ToString();
    }

    public static IReadOnlyList<string> LegalActionKeys(
        SealedDecisionSeamKey seamKey,
        HeadlessPolicyObservation observation)
    {
        if (seamKey == null) throw new ArgumentNullException(nameof(seamKey));
        if (observation == null) throw new ArgumentNullException(nameof(observation));
        return seamKey.SeamType switch
        {
            SealedLlmSeamTypes.Deployment => DeploymentPairKeys(observation),
            SealedLlmSeamTypes.Prep => PrepComponentKeys(observation),
            SealedLlmSeamTypes.Reward => SealedLlmLegalActionSet.RewardKeys(observation),
            _ => throw new ArgumentException($"Policy observation cannot serve seam '{seamKey.SeamType}'.", nameof(seamKey)),
        };
    }

    public static IReadOnlyList<string> LegalActionKeys(
        SealedDecisionSeamKey seamKey,
        HeadlessRosterPolicyObservation observation)
    {
        if (seamKey == null) throw new ArgumentNullException(nameof(seamKey));
        if (observation == null) throw new ArgumentNullException(nameof(observation));
        return seamKey.SeamType switch
        {
            SealedLlmSeamTypes.Recruit => SealedLlmLegalActionSet.RecruitKeys(observation),
            SealedLlmSeamTypes.Passive => SealedLlmLegalActionSet.PassiveKeys(observation),
            SealedLlmSeamTypes.Refit => SealedLlmLegalActionSet.RefitKeys(observation),
            _ => throw new ArgumentException($"Roster observation cannot serve seam '{seamKey.SeamType}'.", nameof(seamKey)),
        };
    }

    public static SealedLlmDeploymentActionSpaceV1 DeploymentActionSpace(
        HeadlessPolicyObservation observation)
    {
        HeadlessPolicyGuard.ValidateObservation(
            observation ?? throw new ArgumentNullException(nameof(observation)));
        return new SealedLlmDeploymentActionSpaceV1(
            observation.Roster.Select(hero => hero.HeroId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            observation.Anchors.Select(anchor =>
                    SealedLlmCanonicalValue.EnumName(anchor, nameof(observation.Anchors)))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            observation.DeployCapacity);
    }

    private static string RenderDecision(
        SealedDecisionSeamKey seamKey,
        object observation,
        IReadOnlyList<string> legalActionKeys,
        LlmPromptManifestV1 manifest)
    {
        RequireManifest(manifest);
        var keys = RequireLegalKeys(legalActionKeys);
        var prompt = new StringBuilder();
        AppendHeader(prompt, manifest);
        prompt.Append("Response schema (return one JSON object, without a Markdown fence):\n")
            .Append(DecisionResponseSchema)
            .Append("\nID discipline: track_token_ids must use kind:exact_observed_id. Allowed kinds: ")
            .Append(TrackKinds)
            .Append(". evidence_fact_ids must copy exact IDs from EvidenceFactIdsBySignal. confidence must be in (0,1].\n")
            .Append("Every response field must be substantive. This is a form requirement, not strategy advice.\n")
            .Append("[B] CURRENT DECISION\nseam_key=")
            .Append(FormatSeam(seamKey))
            .Append("\nobservation=")
            .Append(RenderObservation(observation))
            .Append("\nlegal_action_keys=")
            .Append(RenderObservation(keys));

        if (string.Equals(seamKey.SeamType, SealedLlmSeamTypes.Deployment, StringComparison.Ordinal))
        {
            var policyObservation = (HeadlessPolicyObservation)observation;
            prompt.Append("\ndeployment_action_space=")
                .Append(RenderObservation(DeploymentActionSpace(policyObservation)))
                .Append("\nDeployment selected_action must contain exactly deploy_capacity distinct anchorId=heroId pairs, joined by ';', with pairs strictly ordinal-sorted by anchorId.");
        }
        else if (string.Equals(seamKey.SeamType, SealedLlmSeamTypes.Prep, StringComparison.Ordinal))
        {
            var policyObservation = (HeadlessPolicyObservation)observation;
            prompt.Append("\ndeployment_action_space=")
                .Append(RenderObservation(DeploymentActionSpace(policyObservation)))
                .Append("\nPrep selected_action must be formation#<full canonical deployment>|equipment#<skip or itemInstanceId=heroId entries sorted by item id and joined by ','>. Keep at most two retained-hero anchor edits and at most one bench swap; equipment may use only observed owned items and selected heroes.");
        }
        else
        {
            prompt.Append("\nselected_action must equal exactly one legal_action_keys entry.");
        }

        return prompt.ToString();
    }

    private static void AppendHeader(StringBuilder prompt, LlmPromptManifestV1 manifest)
    {
        prompt.Append("[A] FIXED COLD-START CONTRACT\n")
            .Append("prompt_template_id=")
            .Append(manifest.PromptTemplateId)
            .Append("\n")
            .Append(manifest.PromptTemplate)
            .Append("\n")
            .Append(manifest.ColdStartBriefing)
            .Append("\n");
    }

    private static IReadOnlyList<string> DeploymentPairKeys(HeadlessPolicyObservation observation)
    {
        HeadlessPolicyGuard.ValidateObservation(observation);
        return observation.Anchors
            .SelectMany(anchor => observation.Roster.Select(hero => new
            {
                AnchorId = SealedLlmCanonicalValue.EnumName(anchor, nameof(observation.Anchors)),
                Key = SealedLlmActionGrammar.DeploymentPair(anchor, hero.HeroId),
            }))
            .OrderBy(value => value.AnchorId, StringComparer.Ordinal)
            .ThenBy(value => value.Key, StringComparer.Ordinal)
            .Select(value => value.Key)
            .ToArray();
    }

    private static IReadOnlyList<string> PrepComponentKeys(HeadlessPolicyObservation observation)
        => DeploymentPairKeys(observation)
            .Concat(new[] { SealedLlmActionGrammar.Skip })
            .Concat(observation.OwnedItems.SelectMany(item => observation.Roster.Select(hero =>
                SealedLlmActionGrammar.PrepEquipmentPair(item.Mechanics.ItemInstanceId, hero.HeroId))))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<string> RequireLegalKeys(IReadOnlyList<string> legalActionKeys)
    {
        if (legalActionKeys == null)
        {
            throw new ArgumentNullException(nameof(legalActionKeys));
        }

        var ordered = legalActionKeys
            .Select(value => string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException("Legal action keys cannot contain blank values.", nameof(legalActionKeys))
                : value)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Length == 0 || ordered.Distinct(StringComparer.Ordinal).Count() != ordered.Length)
        {
            throw new ArgumentException("Legal action keys must be non-empty and distinct.", nameof(legalActionKeys));
        }

        return ordered;
    }

    private static void RequireManifest(LlmPromptManifestV1 manifest)
    {
        if (manifest == null) throw new ArgumentNullException(nameof(manifest));
        _ = LlmWireCanonicalSerializer.CanonicalBytes(manifest);
    }

    private static void ValidateSeam(SealedDecisionSeamKey seamKey, params string[] allowed)
    {
        if (seamKey == null) throw new ArgumentNullException(nameof(seamKey));
        if (seamKey.DecisionIndex < 0 || seamKey.Ordinal < 0
            || !allowed.Contains(seamKey.SeamType, StringComparer.Ordinal))
        {
            throw new ArgumentException("Seam key is not valid for this renderer overload.", nameof(seamKey));
        }
    }

    private static string FormatSeam(SealedDecisionSeamKey seamKey)
        => string.Format(
            CultureInfo.InvariantCulture,
            "{0}:{1}:{2}",
            seamKey.DecisionIndex,
            seamKey.SeamType,
            seamKey.Ordinal);

    private static string RenderObservation(object value)
    {
        var result = new StringBuilder();
        AppendValue(result, value, 0);
        return result.ToString();
    }

    private static void AppendValue(StringBuilder output, object value, int depth)
    {
        if (depth > 64) throw new InvalidOperationException("Observation nesting exceeds the renderer limit.");
        if (value == null)
        {
            output.Append("null");
            return;
        }

        switch (value)
        {
            case string text:
                AppendQuoted(output, text);
                return;
            case bool boolean:
                output.Append(boolean ? "true" : "false");
                return;
            case Enum enumValue:
                AppendQuoted(
                    output,
                    Enum.GetName(enumValue.GetType(), enumValue)
                    ?? throw new InvalidOperationException($"Unknown enum value '{enumValue}'."));
                return;
            case float single:
                RequireFinite(single);
                output.Append(single.ToString("R", CultureInfo.InvariantCulture));
                return;
            case double number:
                RequireFinite(number);
                output.Append(number.ToString("R", CultureInfo.InvariantCulture));
                return;
            case decimal decimalValue:
                output.Append(decimalValue.ToString(CultureInfo.InvariantCulture));
                return;
            case byte or sbyte or short or ushort or int or uint or long or ulong:
                output.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            case IDictionary dictionary:
                AppendDictionary(output, dictionary, depth + 1);
                return;
            case IEnumerable sequence:
                AppendSequence(output, sequence, depth + 1);
                return;
        }

        var properties = value.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.GetIndexParameters().Length == 0 && property.GetMethod != null)
            .Where(property => !ShouldOmitEmptyEnemyEquipment(value, property))
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .ToArray();
        if (properties.Length == 0)
        {
            throw new InvalidOperationException($"Observation value type '{value.GetType().FullName}' has no public fields.");
        }

        output.Append('{');
        for (var index = 0; index < properties.Length; index++)
        {
            if (index > 0) output.Append(',');
            AppendQuoted(output, properties[index].Name);
            output.Append(':');
            AppendValue(output, properties[index].GetValue(value), depth + 1);
        }

        output.Append('}');
    }

    private static bool ShouldOmitEmptyEnemyEquipment(object value, PropertyInfo property)
        => value is HeadlessEnemyUnitPreview enemy
           && string.Equals(property.Name, nameof(enemy.EquippedItems), StringComparison.Ordinal)
           && enemy.EquippedItems.Count == 0;

    private static void AppendDictionary(StringBuilder output, IDictionary dictionary, int depth)
    {
        var entries = new List<DictionaryEntry>();
        foreach (DictionaryEntry entry in dictionary)
        {
            if (entry.Key is not string)
            {
                throw new InvalidOperationException("Observation dictionaries must use string keys.");
            }

            entries.Add(entry);
        }

        output.Append('{');
        var ordered = entries.OrderBy(entry => (string)entry.Key, StringComparer.Ordinal).ToArray();
        for (var index = 0; index < ordered.Length; index++)
        {
            if (index > 0) output.Append(',');
            AppendQuoted(output, (string)ordered[index].Key);
            output.Append(':');
            AppendValue(output, ordered[index].Value, depth);
        }

        output.Append('}');
    }

    private static void AppendSequence(StringBuilder output, IEnumerable sequence, int depth)
    {
        output.Append('[');
        var first = true;
        foreach (var item in sequence)
        {
            if (!first) output.Append(',');
            AppendValue(output, item, depth);
            first = false;
        }

        output.Append(']');
    }

    private static void AppendQuoted(StringBuilder output, string value)
    {
        output.Append('"');
        foreach (var character in value ?? string.Empty)
        {
            switch (character)
            {
                case '"': output.Append("\\\""); break;
                case '\\': output.Append("\\\\"); break;
                case '\b': output.Append("\\b"); break;
                case '\f': output.Append("\\f"); break;
                case '\n': output.Append("\\n"); break;
                case '\r': output.Append("\\r"); break;
                case '\t': output.Append("\\t"); break;
                default:
                    if (character < 0x20)
                    {
                        output.Append("\\u")
                            .Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        output.Append(character);
                    }

                    break;
            }
        }

        output.Append('"');
    }

    private static void RequireFinite(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            throw new InvalidOperationException("Observation numbers must be finite.");
        }
    }
}
