using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace SM.Editor.Narrative;

public static class NarrativePortraitValidator
{
    private const string AuthoringMapPath = "tools/narrative-authoring-map.json";
    private const string PortraitsRoot = "Assets/Resources/Narrative/Portraits";

    [MenuItem("SM/내러티브/포트레이트 자산 검증")]
    public static void ValidatePortraits()
    {
        if (!File.Exists(AuthoringMapPath))
        {
            Debug.LogError($"[PortraitValidator] Authoring map not found: {AuthoringMapPath}");
            return;
        }

        var json = JObject.Parse(File.ReadAllText(AuthoringMapPath, System.Text.Encoding.UTF8));
        var speakers = json["speakers"] as JObject;
        var emotions = json["emotions"] as JObject;

        if (speakers == null)
        {
            Debug.LogError("[PortraitValidator] No speakers in authoring map.");
            return;
        }

        var usedEmoteIds = new HashSet<string>();
        if (emotions != null)
        {
            foreach (var prop in emotions.Properties())
            {
                var emoteId = (prop.Value as JObject)?["emoteId"]?.Value<string>();
                if (!string.IsNullOrWhiteSpace(emoteId) && emoteId != "Default")
                {
                    usedEmoteIds.Add(emoteId);
                }
            }
        }

        int errors = 0;
        int warnings = 0;

        foreach (var prop in speakers.Properties())
        {
            var speakerId = prop.Value?.Value<string>();
            if (string.IsNullOrWhiteSpace(speakerId) || speakerId == "Narrator")
            {
                continue;
            }

            var defaultPath = $"{PortraitsRoot}/{speakerId}/Default.png";
            if (!File.Exists(defaultPath))
            {
                Debug.LogError($"[PortraitValidator] Missing Default.png for '{speakerId}' at {defaultPath}");
                errors++;
            }

            foreach (var emoteId in usedEmoteIds)
            {
                var emotePath = $"{PortraitsRoot}/{speakerId}/{emoteId}.png";
                if (!File.Exists(emotePath))
                {
                    Debug.LogWarning($"[PortraitValidator] Missing {emoteId}.png for '{speakerId}' — will fallback to Default");
                    warnings++;
                }
            }
        }

        if (errors == 0 && warnings == 0)
        {
            Debug.Log("[PortraitValidator] All portrait assets are valid.");
        }
        else
        {
            Debug.Log($"[PortraitValidator] Validation complete: {errors} errors, {warnings} warnings");
        }
    }
}
