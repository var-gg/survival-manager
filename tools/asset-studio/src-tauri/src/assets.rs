use std::ffi::OsStr;
use std::fs;
use std::path::{Path, PathBuf};
use std::time::UNIX_EPOCH;

use serde::{Deserialize, Serialize};
use serde_json::Value;

use crate::asset_index::AssetIndex;
use crate::config::{path_to_string, AppConfig, ScanRoot};
use crate::unity_usage::UnityUsage;

#[derive(Debug, Clone, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct AssetScanOptions {
    #[serde(default)]
    pub include_raw: bool,
    #[serde(default)]
    pub root_ids: Vec<String>,
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct AssetItem {
    pub id: String,
    pub asset_type: String,
    pub source: String,
    pub root_id: String,
    pub root_label: String,
    pub name: String,
    pub absolute_path: String,
    pub relative_path: String,
    pub extension: String,
    pub size_bytes: u64,
    pub modified_ms: u64,
    pub sidecar_path: Option<String>,
    pub sfx_hook_id: Option<String>,
    pub sfx_runtime_hook_id: Option<String>,
    pub sfx_variant_key: Option<String>,
    pub sfx_profile_key: Option<String>,
    pub sfx_qc_status: Option<String>,
    pub sfx_qc_label: Option<String>,
    pub sfx_qc_score: Option<f64>,
    pub thumbnail_path: Option<String>,
    pub structure_key: String,
    pub structure_label: String,
    pub default_visible: bool,
    pub unity_guid: Option<String>,
    pub unity_usage_status: String,
    pub unity_usage_label: String,
    pub unity_usage_count: usize,
    pub unity_usage_paths: Vec<String>,
    pub indexed_ms: u64,
}

#[derive(Default)]
struct SfxSidecarSummary {
    hook_id: Option<String>,
    runtime_hook_id: Option<String>,
    variant_key: Option<String>,
    profile_key: Option<String>,
    qc_status: Option<String>,
    qc_label: Option<String>,
    qc_score: Option<f64>,
}

impl Default for AssetScanOptions {
    fn default() -> Self {
        Self {
            include_raw: false,
            root_ids: Vec::new(),
        }
    }
}

pub fn scan(config: &AppConfig, options: AssetScanOptions) -> Result<Vec<AssetItem>, String> {
    let mut index = AssetIndex::open(config)?;
    index.refresh(config, &options)?;
    index.list(options.include_raw)
}

pub fn list_cached(
    config: &AppConfig,
    options: AssetScanOptions,
) -> Result<Vec<AssetItem>, String> {
    let index = AssetIndex::open(config)?;
    index.list(options.include_raw)
}

pub(crate) fn build_item(
    root: &ScanRoot,
    root_path: &Path,
    path: &Path,
    thumbnail_path: Option<String>,
    unity_usage: UnityUsage,
    indexed_ms: u64,
) -> Option<AssetItem> {
    let metadata = fs::metadata(path).ok()?;
    let extension = extension(path)?;
    let name = path.file_name()?.to_string_lossy().to_string();
    let relative_path = path
        .strip_prefix(root_path)
        .unwrap_or(path)
        .to_string_lossy()
        .replace('\\', "/");
    let modified_ms = metadata
        .modified()
        .ok()
        .and_then(|time| time.duration_since(UNIX_EPOCH).ok())
        .map(|duration| duration.as_millis() as u64)
        .unwrap_or(0);
    let absolute_path = path_to_string(path);
    let asset_type = infer_asset_type(path, &root.id);
    let structure_key = derive_structure_key(root, &relative_path, &asset_type);
    let sidecar_path = find_sidecar(path);
    let sfx_summary = summarize_sfx_sidecar(&asset_type, sidecar_path.as_deref(), &name);

    Some(AssetItem {
        id: stable_id(&absolute_path),
        asset_type,
        source: root.source.clone(),
        root_id: root.id.clone(),
        root_label: root.label.clone(),
        name,
        absolute_path,
        relative_path,
        extension,
        size_bytes: metadata.len(),
        modified_ms,
        sidecar_path: sidecar_path.as_ref().map(path_to_string),
        sfx_hook_id: sfx_summary.hook_id,
        sfx_runtime_hook_id: sfx_summary.runtime_hook_id,
        sfx_variant_key: sfx_summary.variant_key,
        sfx_profile_key: sfx_summary.profile_key,
        sfx_qc_status: sfx_summary.qc_status,
        sfx_qc_label: sfx_summary.qc_label,
        sfx_qc_score: sfx_summary.qc_score,
        thumbnail_path,
        structure_label: structure_label(&structure_key).to_string(),
        structure_key,
        default_visible: root.default_visible,
        unity_guid: unity_usage.guid,
        unity_usage_status: unity_usage.status,
        unity_usage_label: unity_usage.label,
        unity_usage_count: unity_usage.reference_count,
        unity_usage_paths: unity_usage.reference_paths,
        indexed_ms,
    })
}

pub(crate) fn should_scan_entry(path: &Path) -> bool {
    let Some(name) = path.file_name().and_then(OsStr::to_str) else {
        return true;
    };
    !matches!(
        name,
        ".git" | "Library" | "Temp" | "obj" | "node_modules" | "__pycache__" | ".cache"
    )
}

pub(crate) fn is_media_file(path: &Path) -> bool {
    matches!(
        extension(path).as_deref(),
        Some("png")
            | Some("jpg")
            | Some("jpeg")
            | Some("webp")
            | Some("gif")
            | Some("svg")
            | Some("wav")
            | Some("mp3")
            | Some("ogg")
            | Some("flac")
            | Some("mp4")
            | Some("mov")
            | Some("webm")
            | Some("avi")
    )
}

pub(crate) fn is_image_file(path: &Path) -> bool {
    matches!(
        extension(path).as_deref(),
        Some("png") | Some("jpg") | Some("jpeg") | Some("webp")
    )
}

pub(crate) fn infer_asset_type(path: &Path, root_id: &str) -> String {
    let ext = extension(path).unwrap_or_default();
    if matches!(
        ext.as_str(),
        "png" | "jpg" | "jpeg" | "webp" | "gif" | "svg"
    ) {
        return "image".to_string();
    }
    if matches!(ext.as_str(), "mp4" | "mov" | "webm" | "avi") {
        return "video".to_string();
    }

    let lowered = format!(
        "{} {}",
        root_id.to_ascii_lowercase(),
        path.to_string_lossy().to_ascii_lowercase()
    );
    if lowered.contains("chatterbox")
        || lowered.contains("voice")
        || lowered.contains("voices")
        || lowered.contains("dialogue")
    {
        return "voice".to_string();
    }
    if lowered.contains("acestep") || lowered.contains("bgm") || lowered.contains("music") {
        return "bgm".to_string();
    }
    "sfx".to_string()
}

pub(crate) fn extension(path: &Path) -> Option<String> {
    path.extension()
        .and_then(OsStr::to_str)
        .map(|ext| ext.to_ascii_lowercase())
}

fn find_sidecar(path: &Path) -> Option<PathBuf> {
    let same_stem = path.with_extension("json");
    if same_stem.exists() {
        return Some(same_stem);
    }

    let parent = path.parent()?;
    if path.file_stem().and_then(OsStr::to_str) == Some("output") {
        return fs::read_dir(parent)
            .ok()?
            .filter_map(Result::ok)
            .find_map(|entry| {
                let candidate = entry.path();
                (candidate.extension().and_then(OsStr::to_str) == Some("json")).then_some(candidate)
            });
    }

    None
}

fn summarize_sfx_sidecar(
    asset_type: &str,
    sidecar_path: Option<&Path>,
    file_name: &str,
) -> SfxSidecarSummary {
    if asset_type != "sfx" {
        return SfxSidecarSummary::default();
    }

    let mut summary = SfxSidecarSummary::default();
    let mut identity_text = file_name.to_string();

    let Some(sidecar_path) = sidecar_path else {
        summary.qc_status = Some("missing".to_string());
        summary.qc_label = Some(qc_label("missing"));
        apply_identity_from_text(&mut summary, &identity_text);
        return summary;
    };

    let record = fs::read_to_string(sidecar_path)
        .ok()
        .and_then(|text| serde_json::from_str::<Value>(&text).ok());

    let Some(record) = record else {
        summary.qc_status = Some("unknown".to_string());
        summary.qc_label = Some(qc_label("unknown"));
        apply_identity_from_text(&mut summary, &identity_text);
        return summary;
    };

    let review_context = record
        .get("review_context")
        .or_else(|| record.get("reviewContext"));
    summary.hook_id = string_field(review_context, &["hook_id", "hookId"]).or_else(|| {
        string_field(
            Some(&record),
            &["hook_id", "hookId", "variant_id", "variantId"],
        )
    });
    summary.runtime_hook_id = string_field(review_context, &["runtime_hook_id", "runtimeHookId"])
        .or_else(|| string_field(Some(&record), &["runtime_hook_id", "runtimeHookId"]));
    summary.variant_key = string_field(review_context, &["variant_key", "variantKey"])
        .or_else(|| string_field(Some(&record), &["variant_key", "variantKey"]));
    summary.profile_key = string_field(review_context, &["profile_key", "profileKey"])
        .or_else(|| string_field(Some(&record), &["profile_key", "profileKey"]));

    for value in [
        string_field(Some(&record), &["prompt"]),
        string_field(
            record.get("settings"),
            &["prompt", "output_name", "outputName"],
        ),
        string_field(review_context, &["prompt_contract", "promptContract"]),
    ]
    .into_iter()
    .flatten()
    {
        identity_text.push(' ');
        identity_text.push_str(&value);
    }

    apply_identity_from_text(&mut summary, &identity_text);
    fill_identity_gaps(&mut summary);

    let explicit_status = explicit_qc_status(&record);
    let inferred_status = infer_audio_qc_status(&record);
    let qc_status = explicit_status
        .or(inferred_status)
        .unwrap_or_else(|| "unknown".to_string());
    summary.qc_label = Some(qc_label(&qc_status));
    summary.qc_status = Some(qc_status);
    summary.qc_score = explicit_qc_score(&record);

    summary
}

fn apply_identity_from_text(summary: &mut SfxSidecarSummary, text: &str) {
    let Some(identity) = infer_sfx_identity(text) else {
        return;
    };

    if summary.hook_id.is_none() {
        summary.hook_id = Some(identity.hook_id);
    }
    if summary.runtime_hook_id.is_none() {
        summary.runtime_hook_id = identity.runtime_hook_id;
    }
    if summary.variant_key.is_none() {
        summary.variant_key = identity.variant_key;
    }
    if summary.profile_key.is_none() {
        summary.profile_key = identity.profile_key;
    }
}

fn fill_identity_gaps(summary: &mut SfxSidecarSummary) {
    if summary.runtime_hook_id.is_none() {
        summary.runtime_hook_id = summary
            .hook_id
            .as_deref()
            .and_then(runtime_hook_from_hook_id);
    }
    if summary.variant_key.is_none() {
        summary.variant_key = summary
            .hook_id
            .as_deref()
            .and_then(variant_key_from_hook_id);
    }
    if summary.hook_id.is_none() {
        summary.hook_id = summary
            .variant_key
            .as_deref()
            .and_then(hook_id_from_variant_key)
            .or_else(|| summary.runtime_hook_id.clone());
    }
    if summary.profile_key.is_none() {
        summary.profile_key = summary.variant_key.clone();
    }
}

struct SfxIdentity {
    hook_id: String,
    runtime_hook_id: Option<String>,
    variant_key: Option<String>,
    profile_key: Option<String>,
}

fn infer_sfx_identity(text: &str) -> Option<SfxIdentity> {
    let lowered = text.to_ascii_lowercase();

    if let Some(hook_id) = find_sfx_hook(text) {
        let runtime_hook_id = runtime_hook_from_hook_id(&hook_id);
        let variant_key = variant_key_from_hook_id(&hook_id)
            .or_else(|| infer_variant_for_runtime(runtime_hook_id.as_deref(), &lowered));
        return Some(SfxIdentity {
            hook_id,
            runtime_hook_id,
            profile_key: variant_key.clone(),
            variant_key,
        });
    }

    if lowered.contains("impact-damage") || lowered.contains("impact damage") {
        return Some(combat_identity(
            "sfx.combat.impact_damage",
            infer_impact_variant(&lowered),
        ));
    }
    if lowered.contains("guard-enter") || lowered.contains("guard enter") {
        return Some(combat_identity(
            "sfx.combat.guard_enter",
            infer_guard_variant(&lowered),
        ));
    }
    if lowered.contains("reposition-start") || lowered.contains("reposition start") {
        return Some(combat_identity(
            "sfx.combat.reposition_start",
            infer_reposition_variant("combat.reposition_start.boot_dirt", &lowered),
        ));
    }
    if lowered.contains("reposition-stop") || lowered.contains("reposition stop") {
        return Some(combat_identity(
            "sfx.combat.reposition_stop",
            infer_reposition_variant("combat.reposition_stop.boot_dirt", &lowered),
        ));
    }

    for status in [
        "barrier",
        "bleed",
        "burn",
        "exposed",
        "guarded",
        "marked",
        "root",
        "silence",
        "slow",
        "sunder",
        "unstoppable",
        "wound",
    ] {
        if lowered.contains(&format!("{status}-apply"))
            || lowered.contains(&format!("{status} apply"))
        {
            let hook_id = format!("sfx.status.{status}.apply");
            return Some(SfxIdentity {
                hook_id: hook_id.clone(),
                runtime_hook_id: Some(hook_id),
                variant_key: None,
                profile_key: None,
            });
        }
    }

    None
}

fn combat_identity(runtime_hook_id: &str, variant_key: Option<String>) -> SfxIdentity {
    let hook_id = variant_key
        .as_deref()
        .and_then(hook_id_from_variant_key)
        .unwrap_or_else(|| runtime_hook_id.to_string());
    SfxIdentity {
        hook_id,
        runtime_hook_id: Some(runtime_hook_id.to_string()),
        profile_key: variant_key.clone(),
        variant_key,
    }
}

fn find_sfx_hook(text: &str) -> Option<String> {
    text.split(|ch: char| !(ch.is_ascii_alphanumeric() || ch == '.' || ch == '_'))
        .find_map(|token| {
            let token = token.trim_matches('.');
            (token.starts_with("sfx.") && token.len() > 4).then(|| token.to_string())
        })
}

fn infer_variant_for_runtime(runtime_hook_id: Option<&str>, lowered: &str) -> Option<String> {
    match runtime_hook_id {
        Some("sfx.combat.impact_damage") => infer_impact_variant(lowered),
        Some("sfx.combat.guard_enter") => infer_guard_variant(lowered),
        Some("sfx.combat.reposition_start") => {
            infer_reposition_variant("combat.reposition_start.boot_dirt", lowered)
        }
        Some("sfx.combat.reposition_stop") => {
            infer_reposition_variant("combat.reposition_stop.boot_dirt", lowered)
        }
        _ => None,
    }
}

fn infer_impact_variant(text: &str) -> Option<String> {
    if text.contains("wood") || text.contains("shield") || text.contains("block") {
        return Some("combat.impact.wood_block.light".to_string());
    }
    if text.contains("leather") {
        return Some("combat.impact.leather.light".to_string());
    }
    if text.contains("metal") || text.contains("clink") || text.contains("buckle") {
        return Some("combat.impact.metal.light".to_string());
    }
    if text.contains("flesh") || text.contains("body") || text.contains("cloth") {
        return Some("combat.impact.flesh.light".to_string());
    }
    None
}

fn infer_guard_variant(text: &str) -> Option<String> {
    if text.contains("wood") || text.contains("shield") {
        return Some("combat.guard_enter.wood_shield".to_string());
    }
    if text.contains("leather") || text.contains("bracer") || text.contains("strap") {
        return Some("combat.guard_enter.leather_bracer".to_string());
    }
    None
}

fn infer_reposition_variant(variant_key: &str, text: &str) -> Option<String> {
    if text.contains("boot")
        || text.contains("dirt")
        || text.contains("stone")
        || text.contains("dust")
    {
        return Some(variant_key.to_string());
    }
    None
}

fn runtime_hook_from_hook_id(hook_id: &str) -> Option<String> {
    if let Some(rest) = hook_id.strip_prefix("sfx.skill.") {
        let mut parts = rest.rsplitn(2, '.');
        let phase = parts.next()?;
        let skill_id = parts.next()?;
        if phase == "cast" || phase == "impact" {
            return Some(format!("sfx.skill.{skill_id}"));
        }
    }

    if hook_id.starts_with("sfx.status.") && hook_id.ends_with(".apply") {
        return Some(hook_id.to_string());
    }

    [
        "sfx.combat.action_commit_basic",
        "sfx.combat.action_commit_skill",
        "sfx.combat.action_commit_heal",
        "sfx.combat.impact_damage",
        "sfx.combat.impact_heal",
        "sfx.combat.guard_enter",
        "sfx.combat.guard_exit",
        "sfx.combat.reposition_start",
        "sfx.combat.reposition_stop",
        "sfx.combat.death_start",
    ]
    .into_iter()
    .find(|prefix| hook_id == *prefix || hook_id.starts_with(&format!("{prefix}.")))
    .map(str::to_string)
}

fn variant_key_from_hook_id(hook_id: &str) -> Option<String> {
    for (prefix, variant_prefix) in [
        ("sfx.combat.impact_damage.", "combat.impact."),
        ("sfx.combat.guard_enter.", "combat.guard_enter."),
        ("sfx.combat.guard_exit.", "combat.guard_exit."),
        ("sfx.combat.reposition_start.", "combat.reposition_start."),
        ("sfx.combat.reposition_stop.", "combat.reposition_stop."),
        ("sfx.combat.action_commit_basic.", "combat.action_commit."),
        ("sfx.combat.death_start.", "combat.death_start."),
    ] {
        if let Some(rest) = hook_id.strip_prefix(prefix) {
            return Some(format!("{variant_prefix}{rest}"));
        }
    }
    None
}

fn hook_id_from_variant_key(variant_key: &str) -> Option<String> {
    for (prefix, hook_prefix) in [
        ("combat.impact.", "sfx.combat.impact_damage."),
        ("combat.guard_enter.", "sfx.combat.guard_enter."),
        ("combat.guard_exit.", "sfx.combat.guard_exit."),
        ("combat.reposition_start.", "sfx.combat.reposition_start."),
        ("combat.reposition_stop.", "sfx.combat.reposition_stop."),
        ("combat.action_commit.", "sfx.combat.action_commit_basic."),
        ("combat.death_start.", "sfx.combat.death_start."),
    ] {
        if let Some(rest) = variant_key.strip_prefix(prefix) {
            return Some(format!("{hook_prefix}{rest}"));
        }
    }
    None
}

fn explicit_qc_status(record: &Value) -> Option<String> {
    let status = string_field(Some(record), &["qc_status", "qcStatus"])
        .or_else(|| {
            string_field(
                record.get("qc"),
                &["status", "overall_status", "overallStatus"],
            )
        })
        .or_else(|| {
            string_field(
                record.get("sfx_qc"),
                &["status", "overall_status", "overallStatus"],
            )
        })
        .or_else(|| string_field(record.get("sfxQc"), &["status", "overallStatus"]))
        .or_else(|| {
            string_field(
                record.get("validation"),
                &["status", "overall_status", "overallStatus"],
            )
        })
        .or_else(|| {
            string_field(
                record.get("audio_qc"),
                &["status", "overall_status", "overallStatus"],
            )
        })
        .or_else(|| string_field(record.get("audioQc"), &["status", "overallStatus"]))?;
    normalize_qc_status(&status)
}

fn explicit_qc_score(record: &Value) -> Option<f64> {
    number_field(Some(record), &["qc_score", "qcScore"])
        .or_else(|| {
            number_field(
                record.get("qc"),
                &["score", "overall_score", "overallScore"],
            )
        })
        .or_else(|| {
            number_field(
                record.get("sfx_qc"),
                &["score", "overall_score", "overallScore"],
            )
        })
        .or_else(|| number_field(record.get("sfxQc"), &["score", "overallScore"]))
        .or_else(|| {
            number_field(
                record.get("validation"),
                &["score", "overall_score", "overallScore"],
            )
        })
}

fn infer_audio_qc_status(record: &Value) -> Option<String> {
    let audio_qc = record.get("audio_qc").or_else(|| record.get("audioQc"))?;
    let peak = number_field(Some(audio_qc), &["peak", "peak_amplitude", "peakAmplitude"]);
    let rms = number_field(Some(audio_qc), &["rms"]);
    let duration = number_field(
        Some(audio_qc),
        &["duration_seconds", "durationSeconds", "duration"],
    );

    if duration.is_some_and(|value| value <= 0.05)
        || peak.is_some_and(|value| value < 0.005)
        || rms.is_some_and(|value| value < 0.0001)
    {
        return Some("red".to_string());
    }

    if peak.is_some_and(|value| value >= 0.999) {
        return Some("yellow".to_string());
    }

    if peak.is_some_and(|value| value < 0.03) || rms.is_some_and(|value| value < 0.001) {
        return Some("yellow".to_string());
    }

    if peak.is_some() || rms.is_some() || duration.is_some() {
        return Some("green".to_string());
    }

    None
}

fn normalize_qc_status(value: &str) -> Option<String> {
    let lowered = value.trim().to_ascii_lowercase();
    if lowered.contains("red") || lowered.contains("fail") || lowered.contains("bad") {
        return Some("red".to_string());
    }
    if lowered.contains("yellow")
        || lowered.contains("warn")
        || lowered.contains("review")
        || lowered.contains("suspect")
    {
        return Some("yellow".to_string());
    }
    if lowered.contains("green") || lowered.contains("pass") || lowered.contains("ok") {
        return Some("green".to_string());
    }
    if lowered.contains("missing") {
        return Some("missing".to_string());
    }
    if lowered.contains("unknown") {
        return Some("unknown".to_string());
    }
    None
}

fn qc_label(status: &str) -> String {
    match status {
        "red" => "QC Red",
        "yellow" => "QC Yellow",
        "green" => "QC Green",
        "missing" => "QC Missing",
        _ => "QC Unknown",
    }
    .to_string()
}

fn string_field(value: Option<&Value>, keys: &[&str]) -> Option<String> {
    let object = value?.as_object()?;
    keys.iter().find_map(|key| {
        object
            .get(*key)
            .and_then(Value::as_str)
            .map(str::trim)
            .filter(|value| !value.is_empty())
            .map(str::to_string)
    })
}

fn number_field(value: Option<&Value>, keys: &[&str]) -> Option<f64> {
    let object = value?.as_object()?;
    keys.iter()
        .find_map(|key| object.get(*key).and_then(Value::as_f64))
}

fn derive_structure_key(root: &ScanRoot, relative_path: &str, asset_type: &str) -> String {
    let root_key = if root.structure_key.is_empty() {
        root.source.as_str()
    } else {
        root.structure_key.as_str()
    };
    let lowered = relative_path.to_ascii_lowercase();

    if root.source == "ai-infra" {
        return "ai".to_string();
    }
    if root.id == "ui-owned" || root_key == "ui" {
        return "ui".to_string();
    }
    if asset_type == "image" && (lowered.contains("icon") || lowered.contains("/icons/")) {
        return "icons".to_string();
    }
    if asset_type == "image"
        && (lowered.contains("character")
            || lowered.contains("portrait")
            || lowered.contains("stance")
            || lowered.contains("sprite"))
    {
        return "characters".to_string();
    }
    // Narrative illustrations: bespoke story cutscene CGs (concept_art_*) vs reusable
    // environment backdrops (site_/town_/loc_*). Keyed by path-segment prefix so they
    // categorize correctly whether canonical (art-pipeline/cg) or raw (art-pipeline/output).
    if asset_type == "image"
        && (has_segment_prefix(&lowered, "concept_art") || lowered.contains("cutscene"))
    {
        return "cutscene".to_string();
    }
    if asset_type == "image"
        && (has_segment_prefix(&lowered, "site_")
            || has_segment_prefix(&lowered, "town_")
            || has_segment_prefix(&lowered, "loc_")
            || lowered.contains("backdrop"))
    {
        return "backgrounds".to_string();
    }
    root_key.to_string()
}

/// True when any "/"-separated segment of `path` starts with `prefix`.
/// Avoids false positives from substrings (e.g. "opposite_" containing "site_").
fn has_segment_prefix(path: &str, prefix: &str) -> bool {
    path.split('/').any(|segment| segment.starts_with(prefix))
}

fn structure_label(key: &str) -> &'static str {
    match key {
        "selected" => "Selected",
        "runtime" => "Runtime",
        "characters" => "Characters",
        "cutscene" => "Cutscene CG",
        "backgrounds" => "Backgrounds",
        "icons" => "Icons",
        "ui" => "UI",
        "ai" => "AI Results",
        "raw" => "Raw Output",
        "reference" => "References",
        "captures" => "Captures",
        _ => "Other",
    }
}

pub(crate) fn stable_id(text: &str) -> String {
    let mut hash = 0xcbf29ce484222325u64;
    for byte in text.as_bytes() {
        hash ^= u64::from(*byte);
        hash = hash.wrapping_mul(0x100000001b3);
    }
    format!("{hash:016x}")
}
