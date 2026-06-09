use std::path::PathBuf;

use serde::Serialize;

use crate::config::{path_to_string, AppConfig};

/// Result of resolving a scene's audio `cue_id` to an on-disk BGM track.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ResolvedCue {
    /// The cue id, e.g. "bgm_site_intro_ash".
    pub cue_id: String,
    /// Absolute path to the resolved audio file, if one exists on disk.
    pub path: Option<String>,
    pub exists: bool,
}

/// Resolve an audio `cue_id` to a concrete BGM track in the ACE-Step output dir.
///
/// Looks in `{ai_infra_root}/data/acestep/outputs/<cue_id>.<ext>`. Returns a
/// record with `exists: false` when the cue has no generated track yet (e.g. a
/// bespoke peak slot authored in narrative-audio-map.json but not yet rendered).
pub fn resolve(config: &AppConfig, cue_id: &str) -> ResolvedCue {
    let id = cue_id.trim().to_string();
    let mut resolved = ResolvedCue {
        cue_id: id.clone(),
        path: None,
        exists: false,
    };
    if id.is_empty() {
        return resolved;
    }
    let dir = PathBuf::from(&config.ai_infra_root)
        .join("data")
        .join("acestep")
        .join("outputs");
    for ext in ["wav", "mp3", "flac", "ogg"] {
        let candidate = dir.join(format!("{id}.{ext}"));
        if candidate.is_file() {
            resolved.path = Some(path_to_string(&candidate));
            resolved.exists = true;
            break;
        }
    }
    resolved
}
