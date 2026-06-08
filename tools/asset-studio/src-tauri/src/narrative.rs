use std::collections::{HashMap, HashSet};
use std::fs;
use std::path::{Path, PathBuf};
use std::time::UNIX_EPOCH;

use serde::{Deserialize, Serialize};
use walkdir::WalkDir;

use crate::assets::extension;
use crate::config::{path_to_string, AppConfig};

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct NarrativeIndex {
    pub source: String,
    pub source_hash: String,
    pub seed_path: String,
    pub seed_modified_ms: u64,
    pub unity_root: String,
    pub total_sequences: usize,
    pub total_lines: usize,
    pub unity_matched_sequences: usize,
    pub unity_missing_sequences: usize,
    pub unity_mismatched_sequences: usize,
    pub ja_ready_lines: usize,
    pub voice_generated_lines: usize,
    pub voice_role_lines: usize,
    pub live_sequences: usize,
    pub unreachable_sequences: usize,
    pub voice_roots: Vec<String>,
    pub sequences: Vec<NarrativeSequence>,
    pub speakers: Vec<NarrativeSpeakerSummary>,
    pub diagnostics: Vec<String>,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct NarrativeSequence {
    pub id: String,
    pub sequence_id: String,
    pub presentation_key: String,
    pub presentation_kind: String,
    pub artifact_slug: String,
    pub title: String,
    pub meta: serde_json::Value,
    pub line_count: usize,
    pub ja_ready_count: usize,
    pub voice_generated_count: usize,
    pub voice_role_count: usize,
    pub reachability: String,
    pub live_moment: String,
    pub live_event_id: String,
    pub speakers: Vec<String>,
    pub unity: UnitySequenceStatus,
    pub lines: Vec<NarrativeLine>,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct UnitySequenceStatus {
    pub status: String,
    pub label: String,
    pub asset_path: Option<String>,
    pub line_count: usize,
    pub modified_ms: u64,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct NarrativeLine {
    pub id: String,
    pub line_id: String,
    pub line_index: usize,
    pub speaker_alias: String,
    pub speaker_id: String,
    pub emotion_raw: String,
    pub emotion_id: String,
    pub emote_id: String,
    pub ko_text: String,
    pub ja_tts_text: String,
    pub tts_text_status: String,
    pub branch_tag: String,
    pub section_label: String,
    pub voice_role: String,
    pub text_key: String,
    pub voice_key: String,
    pub voice_status: String,
    pub voice_asset_path: Option<String>,
    pub refs: Vec<NarrativeLineRef>,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct NarrativeLineRef {
    pub alias: String,
    pub id: String,
    pub self_ref: bool,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct NarrativeSpeakerSummary {
    pub speaker_id: String,
    pub alias: String,
    pub line_count: usize,
    pub ja_ready_count: usize,
    pub voice_generated_count: usize,
    pub voice_role_count: usize,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct NarrativeSeed {
    #[serde(default)]
    source: String,
    #[serde(default)]
    source_hash: String,
    #[serde(default)]
    dialogue_sequences: Vec<SeedSequence>,
    #[serde(default)]
    story_events: Vec<SeedStoryEvent>,
    #[serde(default)]
    diagnostics: Vec<serde_json::Value>,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct SeedStoryEvent {
    #[serde(default)]
    event_id: String,
    #[serde(default)]
    chapter_id: String,
    #[serde(default)]
    site_id: String,
    #[serde(default)]
    moment: String,
    #[serde(default)]
    presentation_key: String,
    #[serde(default)]
    conditions: Vec<SeedCondition>,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct SeedCondition {
    #[serde(default)]
    kind_token: String,
    #[serde(default)]
    operand_a: String,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct SeedSequence {
    sequence_id: String,
    #[serde(default)]
    presentation_key: String,
    #[serde(default)]
    presentation_kind: String,
    #[serde(default)]
    artifact_slug: String,
    #[serde(default)]
    title: String,
    #[serde(default)]
    meta: serde_json::Value,
    #[serde(default)]
    lines: Vec<SeedLine>,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct SeedLine {
    line_id: String,
    #[serde(default)]
    line_index: usize,
    #[serde(default)]
    speaker_alias: String,
    #[serde(default)]
    speaker_id: String,
    #[serde(default)]
    emotion_raw: String,
    #[serde(default)]
    emotion_id: String,
    #[serde(default)]
    emote_id: String,
    #[serde(default)]
    text: String,
    #[serde(default)]
    ja: String,
    #[serde(default)]
    branch_tag: String,
    #[serde(default)]
    section_label: String,
    #[serde(default)]
    voice_role: String,
    #[serde(default)]
    refs: Vec<SeedRef>,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct SeedRef {
    #[serde(default)]
    alias: String,
    #[serde(default)]
    id: String,
    #[serde(default)]
    self_ref: bool,
}

#[derive(Debug, Clone)]
struct UnityAssetInfo {
    path: PathBuf,
    modified_ms: u64,
    text_keys_by_index: HashMap<usize, String>,
}

#[derive(Debug, Clone)]
struct VoiceAssetInfo {
    path: PathBuf,
    name_lower: String,
}

#[derive(Debug, Default)]
struct SpeakerAccumulator {
    alias: String,
    line_count: usize,
    ja_ready_count: usize,
    voice_generated_count: usize,
    voice_role_count: usize,
}

pub fn load(config: &AppConfig) -> Result<NarrativeIndex, String> {
    let project_root = PathBuf::from(&config.project_root);
    let seed_path = project_root
        .join("Logs")
        .join("Narrative")
        .join("narrative-seed-wiki.json");
    if !seed_path.exists() {
        return Err(format!("narrative seed not found: {}", seed_path.display()));
    }

    let seed_text = fs::read_to_string(&seed_path)
        .map_err(|err| format!("failed to read {}: {err}", seed_path.display()))?;
    let seed = serde_json::from_str::<NarrativeSeed>(&seed_text)
        .map_err(|err| format!("failed to parse {}: {err}", seed_path.display()))?;

    let unity_root = project_root
        .join("Assets")
        .join("Resources")
        .join("_Game")
        .join("Content")
        .join("Definitions")
        .join("DialogueSequences");
    let unity_assets = read_unity_dialogue_assets(&unity_root)?;
    let voice_roots = collect_voice_roots(config);
    let voice_assets = read_voice_assets(&voice_roots);

    let mut diagnostics = seed
        .diagnostics
        .iter()
        .map(|value| value.to_string())
        .collect::<Vec<_>>();
    if unity_assets.is_empty() {
        diagnostics.push(format!(
            "no Unity dialogue assets found under {}",
            unity_root.display()
        ));
    }

    let mut speakers: HashMap<String, SpeakerAccumulator> = HashMap::new();
    let mut total_lines = 0usize;
    let mut unity_matched_sequences = 0usize;
    let mut unity_missing_sequences = 0usize;
    let mut unity_mismatched_sequences = 0usize;
    let mut ja_ready_lines = 0usize;
    let mut voice_generated_lines = 0usize;
    let mut voice_role_lines = 0usize;
    let mut live_sequences = 0usize;
    let mut unreachable_sequences = 0usize;

    // storyEvents is the canonical "what actually fires in-game" list (from
    // narrative-event-map.json, baked into the seed by wiki_narrative_extract.py).
    // A sequence only plays in-game if a story event points at its presentation key.
    let live_events: HashMap<String, (String, String)> = seed
        .story_events
        .iter()
        .filter(|event| !event.presentation_key.trim().is_empty())
        .map(|event| {
            (
                event.presentation_key.clone(),
                (event.moment.clone(), event.event_id.clone()),
            )
        })
        .collect();

    // In-game play order keys, derived from the same story events. Chapters and
    // sites are ranked by first appearance (the event manifest lists them in
    // campaign order), moment orders intro(0) before boss(1), and town-return
    // (TownEntered) is pushed to each chapter's end.
    let mut chapter_rank: HashMap<String, u32> = HashMap::new();
    let mut site_rank: HashMap<String, u32> = HashMap::new();
    for event in &seed.story_events {
        if !event.chapter_id.trim().is_empty() && !chapter_rank.contains_key(&event.chapter_id) {
            let next = chapter_rank.len() as u32;
            chapter_rank.insert(event.chapter_id.clone(), next);
        }
        if !event.site_id.trim().is_empty() && !site_rank.contains_key(&event.site_id) {
            let next = site_rank.len() as u32;
            site_rank.insert(event.site_id.clone(), next);
        }
    }
    let play_keys: HashMap<String, (u32, u8, u32, u8, u32)> = seed
        .story_events
        .iter()
        .filter(|event| !event.presentation_key.trim().is_empty())
        .map(|event| {
            let chapter = chapter_rank
                .get(&event.chapter_id)
                .copied()
                .unwrap_or(u32::MAX);
            let town = if event.moment == "TownEntered" {
                1u8
            } else {
                0u8
            };
            let site = site_rank.get(&event.site_id).copied().unwrap_or(0);
            let moment = match event.moment.as_str() {
                "SiteEntered" => 0u8,
                "BattleResolved" => 1u8,
                _ => 2u8,
            };
            let node = event
                .conditions
                .iter()
                .find(|cond| cond.kind_token == "NodeIs")
                .and_then(|cond| cond.operand_a.parse::<u32>().ok())
                .unwrap_or(0);
            (
                event.presentation_key.clone(),
                (chapter, town, site, moment, node),
            )
        })
        .collect();

    let mut sequences = Vec::with_capacity(seed.dialogue_sequences.len());
    for seed_sequence in seed.dialogue_sequences {
        let presentation_key =
            non_empty(seed_sequence.presentation_key, &seed_sequence.sequence_id);
        let unity_asset = unity_assets
            .get(&presentation_key)
            .or_else(|| unity_assets.get(&seed_sequence.sequence_id));
        let unity_status = unity_status_for(unity_asset, seed_sequence.lines.len());
        match unity_status.status.as_str() {
            "matched" => unity_matched_sequences += 1,
            "line_mismatch" => unity_mismatched_sequences += 1,
            "missing" => unity_missing_sequences += 1,
            _ => {}
        }

        let live_hit = live_events
            .get(&presentation_key)
            .or_else(|| live_events.get(&seed_sequence.sequence_id));
        let (reachability, live_moment, live_event_id) = match live_hit {
            Some((moment, event_id)) => {
                let state = if unity_status.status == "missing" {
                    "wired_unbuilt"
                } else {
                    "live"
                };
                (state.to_string(), moment.clone(), event_id.clone())
            }
            None => ("unreachable".to_string(), String::new(), String::new()),
        };
        match reachability.as_str() {
            "live" => live_sequences += 1,
            "unreachable" => unreachable_sequences += 1,
            _ => {}
        }

        let mut sequence_speakers = HashSet::new();
        let mut lines = Vec::with_capacity(seed_sequence.lines.len());
        let mut sequence_ja_ready = 0usize;
        let mut sequence_voice_generated = 0usize;
        let mut sequence_voice_role = 0usize;

        for seed_line in seed_sequence.lines {
            total_lines += 1;
            let speaker_id = non_empty(seed_line.speaker_id, "unknown_speaker");
            let speaker_alias = non_empty(seed_line.speaker_alias, &speaker_id);
            let ja_tts_text = seed_line.ja.trim().to_string();
            let tts_text_status = if ja_tts_text.is_empty() {
                "pending_translation".to_string()
            } else {
                sequence_ja_ready += 1;
                ja_ready_lines += 1;
                "ready".to_string()
            };
            let voice_key = voice_key_for(&speaker_id, &presentation_key, &seed_line.line_id);
            let voice_asset_path = find_voice_asset(&voice_assets, &seed_line.line_id, &voice_key);
            let voice_status = if voice_asset_path.is_some() {
                sequence_voice_generated += 1;
                voice_generated_lines += 1;
                "generated"
            } else {
                "missing"
            }
            .to_string();
            if !seed_line.voice_role.trim().is_empty() {
                sequence_voice_role += 1;
                voice_role_lines += 1;
            }

            let text_key = unity_asset
                .and_then(|asset| asset.text_keys_by_index.get(&seed_line.line_index))
                .cloned()
                .unwrap_or_else(|| derived_text_key(&presentation_key, seed_line.line_index));

            let speaker_entry = speakers.entry(speaker_id.clone()).or_default();
            if speaker_entry.alias.is_empty() {
                speaker_entry.alias = speaker_alias.clone();
            }
            speaker_entry.line_count += 1;
            if tts_text_status == "ready" {
                speaker_entry.ja_ready_count += 1;
            }
            if voice_status == "generated" {
                speaker_entry.voice_generated_count += 1;
            }
            if !seed_line.voice_role.trim().is_empty() {
                speaker_entry.voice_role_count += 1;
            }
            sequence_speakers.insert(speaker_id.clone());

            lines.push(NarrativeLine {
                id: format!("{}:{}", seed_sequence.sequence_id, seed_line.line_id),
                line_id: seed_line.line_id,
                line_index: seed_line.line_index,
                speaker_alias,
                speaker_id,
                emotion_raw: seed_line.emotion_raw,
                emotion_id: seed_line.emotion_id,
                emote_id: seed_line.emote_id,
                ko_text: seed_line.text,
                ja_tts_text,
                tts_text_status,
                branch_tag: seed_line.branch_tag,
                section_label: seed_line.section_label,
                voice_role: seed_line.voice_role,
                text_key,
                voice_key,
                voice_status,
                voice_asset_path: voice_asset_path.map(path_to_string),
                refs: seed_line
                    .refs
                    .into_iter()
                    .map(|reference| NarrativeLineRef {
                        alias: reference.alias,
                        id: reference.id,
                        self_ref: reference.self_ref,
                    })
                    .collect(),
            });
        }

        let mut speaker_list = sequence_speakers.into_iter().collect::<Vec<_>>();
        speaker_list.sort();
        sequences.push(NarrativeSequence {
            id: seed_sequence.sequence_id.clone(),
            sequence_id: seed_sequence.sequence_id,
            presentation_key,
            presentation_kind: seed_sequence.presentation_kind,
            artifact_slug: seed_sequence.artifact_slug,
            title: seed_sequence.title,
            meta: seed_sequence.meta,
            line_count: lines.len(),
            ja_ready_count: sequence_ja_ready,
            voice_generated_count: sequence_voice_generated,
            voice_role_count: sequence_voice_role,
            reachability,
            live_moment,
            live_event_id,
            speakers: speaker_list,
            unity: unity_status,
            lines,
        });
    }

    // Live scenes first, ordered as they actually play in-game; everything else
    // keeps its original (authoring/file) order below. sort_by is stable.
    sequences.sort_by(|left, right| {
        sequence_order_key(left, &play_keys).cmp(&sequence_order_key(right, &play_keys))
    });

    let mut speaker_summaries = speakers
        .into_iter()
        .map(|(speaker_id, summary)| NarrativeSpeakerSummary {
            speaker_id,
            alias: summary.alias,
            line_count: summary.line_count,
            ja_ready_count: summary.ja_ready_count,
            voice_generated_count: summary.voice_generated_count,
            voice_role_count: summary.voice_role_count,
        })
        .collect::<Vec<_>>();
    speaker_summaries.sort_by(|left, right| right.line_count.cmp(&left.line_count));

    Ok(NarrativeIndex {
        source: seed.source,
        source_hash: seed.source_hash,
        seed_path: path_to_string(&seed_path),
        seed_modified_ms: modified_ms(&seed_path),
        unity_root: path_to_string(unity_root),
        total_sequences: sequences.len(),
        total_lines,
        unity_matched_sequences,
        unity_missing_sequences,
        unity_mismatched_sequences,
        ja_ready_lines,
        voice_generated_lines,
        voice_role_lines,
        live_sequences,
        unreachable_sequences,
        voice_roots: voice_roots.iter().map(path_to_string).collect(),
        sequences,
        speakers: speaker_summaries,
        diagnostics,
    })
}

fn read_unity_dialogue_assets(root: &Path) -> Result<HashMap<String, UnityAssetInfo>, String> {
    if !root.exists() {
        return Ok(HashMap::new());
    }

    let mut assets = HashMap::new();
    for entry in
        fs::read_dir(root).map_err(|err| format!("failed to read {}: {err}", root.display()))?
    {
        let entry = entry.map_err(|err| format!("failed to read {}: {err}", root.display()))?;
        let path = entry.path();
        if extension(&path).as_deref() != Some("asset") {
            continue;
        }
        let Some(stem) = path.file_stem().and_then(|value| value.to_str()) else {
            continue;
        };
        let text = fs::read_to_string(&path)
            .map_err(|err| format!("failed to read {}: {err}", path.display()))?;
        assets.insert(
            stem.to_string(),
            UnityAssetInfo {
                modified_ms: modified_ms(&path),
                text_keys_by_index: parse_text_keys(&text),
                path,
            },
        );
    }
    Ok(assets)
}

fn parse_text_keys(text: &str) -> HashMap<usize, String> {
    let mut keys = HashMap::new();
    let mut current_index: Option<usize> = None;
    for line in text.lines() {
        let trimmed = line.trim();
        if let Some(id) = trimmed.strip_prefix("_id: ") {
            current_index = parse_line_index(id.trim());
            continue;
        }
        if let Some(key) = trimmed.strip_prefix("_textKey: ") {
            if let Some(index) = current_index.take() {
                keys.insert(index, key.trim().to_string());
            }
        }
    }
    keys
}

fn parse_line_index(id: &str) -> Option<usize> {
    if let Some((_, suffix)) = id.rsplit_once(".line.") {
        return suffix.parse::<usize>().ok();
    }
    if let Some((_, suffix)) = id.rsplit_once("__line__") {
        return suffix.parse::<usize>().ok();
    }
    None
}

fn unity_status_for(asset: Option<&UnityAssetInfo>, seed_line_count: usize) -> UnitySequenceStatus {
    let Some(asset) = asset else {
        return UnitySequenceStatus {
            status: "missing".to_string(),
            label: "Unity Missing".to_string(),
            asset_path: None,
            line_count: 0,
            modified_ms: 0,
        };
    };

    let asset_line_count = asset.text_keys_by_index.len();
    let (status, label) = if asset_line_count == seed_line_count {
        ("matched", "Unity OK")
    } else {
        ("line_mismatch", "Line Mismatch")
    };
    UnitySequenceStatus {
        status: status.to_string(),
        label: label.to_string(),
        asset_path: Some(path_to_string(&asset.path)),
        line_count: asset_line_count,
        modified_ms: asset.modified_ms,
    }
}

fn collect_voice_roots(config: &AppConfig) -> Vec<PathBuf> {
    let project_root = PathBuf::from(&config.project_root);
    let mut roots = Vec::new();
    let mut seen = HashSet::new();

    for root in [
        project_root
            .join("Assets")
            .join("Resources")
            .join("_Game")
            .join("Audio")
            .join("Voice"),
        project_root
            .join("Assets")
            .join("_Game")
            .join("Audio")
            .join("Voice"),
        project_root
            .join("art-pipeline")
            .join("selected")
            .join("voice"),
        project_root
            .join("art-pipeline")
            .join("selected")
            .join("voices"),
    ] {
        push_existing_root(&mut roots, &mut seen, root);
    }

    for engine in &config.engines {
        let lowered = format!("{} {}", engine.id, engine.name).to_ascii_lowercase();
        if lowered.contains("chatterbox") || lowered.contains("voice") {
            if let Some(output_root) = &engine.output_root {
                push_existing_root(&mut roots, &mut seen, PathBuf::from(output_root));
            }
        }
    }
    for root in &config.scan_roots {
        let lowered = format!("{} {}", root.id, root.path).to_ascii_lowercase();
        if lowered.contains("chatterbox") || lowered.contains("voice") {
            push_existing_root(&mut roots, &mut seen, PathBuf::from(&root.path));
        }
    }

    roots
}

fn push_existing_root(roots: &mut Vec<PathBuf>, seen: &mut HashSet<String>, root: PathBuf) {
    if !root.exists() {
        return;
    }
    let key = root.to_string_lossy().to_ascii_lowercase();
    if seen.insert(key) {
        roots.push(root);
    }
}

fn read_voice_assets(roots: &[PathBuf]) -> Vec<VoiceAssetInfo> {
    roots
        .iter()
        .flat_map(|root| {
            WalkDir::new(root)
                .follow_links(false)
                .into_iter()
                .filter_map(Result::ok)
                .filter(|entry| entry.file_type().is_file())
                .filter_map(|entry| {
                    let path = entry.into_path();
                    is_audio_file(&path).then(|| VoiceAssetInfo {
                        name_lower: path
                            .file_name()
                            .and_then(|value| value.to_str())
                            .unwrap_or_default()
                            .to_ascii_lowercase(),
                        path,
                    })
                })
                .collect::<Vec<_>>()
        })
        .collect()
}

fn is_audio_file(path: &Path) -> bool {
    matches!(
        extension(path).as_deref(),
        Some("wav") | Some("mp3") | Some("ogg") | Some("flac")
    )
}

fn find_voice_asset(assets: &[VoiceAssetInfo], line_id: &str, voice_key: &str) -> Option<PathBuf> {
    let line_id = line_id.to_ascii_lowercase();
    let voice_key = voice_key.to_ascii_lowercase();
    assets
        .iter()
        .find(|asset| asset.name_lower.contains(&line_id) || asset.name_lower.contains(&voice_key))
        .map(|asset| asset.path.clone())
}

fn voice_key_for(speaker_id: &str, presentation_key: &str, line_id: &str) -> String {
    format!(
        "vo_ja_{}_{}_{}",
        sanitize_key_part(speaker_id),
        sanitize_key_part(presentation_key),
        sanitize_key_part(line_id)
    )
}

fn sanitize_key_part(text: &str) -> String {
    let mut output = String::new();
    let mut previous_underscore = false;
    for ch in text.chars() {
        let next = if ch.is_ascii_alphanumeric() {
            previous_underscore = false;
            ch.to_ascii_lowercase()
        } else if previous_underscore {
            continue;
        } else {
            previous_underscore = true;
            '_'
        };
        output.push(next);
    }
    output.trim_matches('_').to_string()
}

fn derived_text_key(presentation_key: &str, line_index: usize) -> String {
    let story_key = presentation_key
        .strip_prefix("dialogue_")
        .unwrap_or(presentation_key);
    format!("loc.story.{story_key}.{line_index}")
}

fn modified_ms(path: &Path) -> u64 {
    fs::metadata(path)
        .ok()
        .and_then(|metadata| metadata.modified().ok())
        .and_then(|time| time.duration_since(UNIX_EPOCH).ok())
        .map(|duration| duration.as_millis() as u64)
        .unwrap_or(0)
}

fn non_empty(value: String, fallback: &str) -> String {
    if value.trim().is_empty() {
        fallback.to_string()
    } else {
        value
    }
}

// Sort key: live scenes (group 0) in play order chapter → town-last → site →
// moment → node; non-live (group 1) sort equal so the stable sort preserves
// their original authoring/file order beneath the live block.
fn sequence_order_key(
    sequence: &NarrativeSequence,
    play_keys: &HashMap<String, (u32, u8, u32, u8, u32)>,
) -> (u8, u32, u8, u32, u8, u32) {
    if sequence.reachability == "live" {
        if let Some(&(chapter, town, site, moment, node)) = play_keys
            .get(&sequence.presentation_key)
            .or_else(|| play_keys.get(&sequence.sequence_id))
        {
            return (0, chapter, town, site, moment, node);
        }
        return (0, u32::MAX, 1, u32::MAX, 2, u32::MAX);
    }
    (1, 0, 0, 0, 0, 0)
}
