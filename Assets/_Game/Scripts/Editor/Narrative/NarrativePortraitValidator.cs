using System;
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
    private const string PortraitsRoot = "Assets/Resources/_Game/Art/Characters";

    // 자산 구조 SoT: pindoc://analysis-character-asset-matrix-dawn-priest.
    // face/bust emote는 8종 고정 — ResourcesStoryPortraitResolver와 정합한다.
    private static readonly string[] CanonicalEmotes =
    {
        "default", "smile", "serious", "shock", "anger", "sad", "cry", "quiet",
    };

    [MenuItem("SM/내러티브/포트레이트 자산 검증")]
    public static void ValidatePortraits()
    {
        if (!File.Exists(AuthoringMapPath))
        {
            Debug.LogError($"[PortraitValidator] Authoring map not found: {AuthoringMapPath}");
            return;
        }

        var json = JObject.Parse(File.ReadAllText(AuthoringMapPath, System.Text.Encoding.UTF8));
        if (json["speakers"] is not JObject speakers)
        {
            Debug.LogError("[PortraitValidator] No speakers in authoring map.");
            return;
        }

        // speakers의 값이 곧 캐릭터 폴더 id다. 여러 화자명이 한 폴더를 가리키므로 중복을 제거한다.
        var characterIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var prop in speakers.Properties())
        {
            var characterId = prop.Value?.Value<string>();
            if (!string.IsNullOrWhiteSpace(characterId) && characterId != "Narrator")
            {
                characterIds.Add(characterId);
            }
        }

        int errors = 0;
        int warnings = 0;

        foreach (var characterId in characterIds.OrderBy(id => id, StringComparer.Ordinal))
        {
            var folder = $"{PortraitsRoot}/{characterId}";

            // portrait_face_default는 resolver의 최종 fallback이라 모든 화자에 필수다.
            if (!File.Exists($"{folder}/portrait_face_default.png"))
            {
                Debug.LogError($"[PortraitValidator] Missing portrait_face_default.png for '{characterId}' at {folder}");
                errors++;
            }

            foreach (var emote in CanonicalEmotes)
            {
                if (emote != "default" && !File.Exists($"{folder}/portrait_face_{emote}.png"))
                {
                    Debug.LogWarning($"[PortraitValidator] Missing portrait_face_{emote}.png for '{characterId}' — resolver degrades to face_default");
                    warnings++;
                }
            }

            // bust가 하나라도 있으면 VN 좌우 배치 화자로 보고 8종 x L/R 누락을 warning한다.
            // bust가 전무하면 face-only 화자로 간주해 bust 검사를 건너뛴다.
            var bustCapable = File.Exists($"{folder}/portrait_bust_default_L.png")
                              || File.Exists($"{folder}/portrait_bust_default_R.png");
            if (bustCapable)
            {
                foreach (var emote in CanonicalEmotes)
                {
                    foreach (var direction in new[] { "L", "R" })
                    {
                        if (!File.Exists($"{folder}/portrait_bust_{emote}_{direction}.png"))
                        {
                            Debug.LogWarning($"[PortraitValidator] Missing portrait_bust_{emote}_{direction}.png for '{characterId}' — resolver degrades to face");
                            warnings++;
                        }
                    }
                }
            }
        }

        if (errors == 0 && warnings == 0)
        {
            Debug.Log($"[PortraitValidator] All portrait assets valid ({characterIds.Count} characters).");
        }
        else
        {
            Debug.Log($"[PortraitValidator] Validation complete: {errors} errors, {warnings} warnings ({characterIds.Count} characters).");
        }
    }
}
