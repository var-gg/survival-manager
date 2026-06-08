use std::path::PathBuf;

use serde::Serialize;

use crate::config::{path_to_string, AppConfig};

/// Result of resolving a scene's visual `backdrop` token to an on-disk illustration.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ResolvedBackdrop {
    /// Original backdrop token, e.g. "bespoke:concept_art_x" / "shared:site_y".
    pub backdrop: String,
    /// "bespoke" | "shared" | "none".
    pub mode: String,
    /// Stripped id, e.g. "concept_art_x" / "site_y".
    pub id: String,
    /// Absolute path to the resolved image, if one exists on disk.
    pub path: Option<String>,
    pub exists: bool,
    /// Which staging area satisfied the lookup: "cg" (canonical) | "output" (raw) | None.
    pub source: Option<String>,
}

/// Resolve a beat-map `backdrop` token to a concrete illustration file.
///
/// Canonical CGs (`art-pipeline/cg/<id>.png`) win over raw candidates
/// (`art-pipeline/output/<id>/default.png`), so a promoted asset always shows.
/// Returns a record with `exists: false` when nothing has been generated yet.
pub fn resolve(config: &AppConfig, backdrop: &str) -> ResolvedBackdrop {
    let (mode, id) = split_backdrop(backdrop);
    let mut resolved = ResolvedBackdrop {
        backdrop: backdrop.to_string(),
        mode,
        id: id.clone(),
        path: None,
        exists: false,
        source: None,
    };
    if id.is_empty() {
        return resolved;
    }

    let pipeline = PathBuf::from(&config.project_root).join("art-pipeline");
    let candidates = [
        ("cg", pipeline.join("cg").join(format!("{id}.png"))),
        ("cg", pipeline.join("cg").join(format!("{id}.jpg"))),
        ("cg", pipeline.join("cg").join(format!("{id}.webp"))),
        (
            "output",
            pipeline.join("output").join(&id).join("default.png"),
        ),
        ("output", pipeline.join("output").join(format!("{id}.png"))),
    ];
    for (source, candidate) in candidates {
        if candidate.is_file() {
            resolved.path = Some(path_to_string(&candidate));
            resolved.exists = true;
            resolved.source = Some(source.to_string());
            break;
        }
    }
    resolved
}

/// Split a backdrop token into (mode, id). An empty token is `("none", "")`;
/// a bare id with no prefix is treated as bespoke.
fn split_backdrop(backdrop: &str) -> (String, String) {
    match backdrop.split_once(':') {
        Some((mode, id)) => (mode.trim().to_string(), id.trim().to_string()),
        None if backdrop.trim().is_empty() => ("none".to_string(), String::new()),
        None => ("bespoke".to_string(), backdrop.trim().to_string()),
    }
}
