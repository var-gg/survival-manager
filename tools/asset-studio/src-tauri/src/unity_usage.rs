use std::collections::{HashMap, HashSet};
use std::ffi::OsStr;
use std::fs;
use std::path::{Path, PathBuf};

use walkdir::WalkDir;

use crate::assets::is_image_file;
use crate::config::{path_to_string, AppConfig, ScanRoot};

#[derive(Debug, Clone)]
pub struct UnityUsage {
    pub guid: Option<String>,
    pub status: String,
    pub label: String,
    pub reference_count: usize,
    pub reference_paths: Vec<String>,
}

#[derive(Debug, Default)]
struct GuidReferences {
    count: usize,
    samples: Vec<String>,
}

#[derive(Debug, Default)]
pub struct UnityUsageIndex {
    references: HashMap<String, GuidReferences>,
}

impl UnityUsageIndex {
    pub fn build(config: &AppConfig) -> Self {
        let project_root = PathBuf::from(&config.project_root);
        let scan_roots = [
            project_root.join("Assets").join("_Game"),
            project_root.join("Assets").join("Resources").join("_Game"),
            project_root.join("Assets").join("Scenes"),
        ];
        let mut references: HashMap<String, GuidReferences> = HashMap::new();

        for root in scan_roots {
            if !root.exists() {
                continue;
            }
            for entry in WalkDir::new(&root).follow_links(false) {
                let entry = match entry {
                    Ok(entry) => entry,
                    Err(_) => continue,
                };
                let path = entry.path();
                if !path.is_file() || !is_reference_file(path) {
                    continue;
                }

                let Ok(text) = fs::read_to_string(path) else {
                    continue;
                };
                let mut file_guids = HashSet::new();
                collect_guids(&text, "guid:", &mut file_guids);
                collect_guids(&text, "guid=", &mut file_guids);

                if file_guids.is_empty() {
                    continue;
                }

                let reference_path = path_to_string(path);
                for guid in file_guids {
                    let refs = references.entry(guid).or_default();
                    refs.count += 1;
                    if refs.samples.len() < 8 {
                        refs.samples.push(reference_path.clone());
                    }
                }
            }
        }

        Self { references }
    }

    pub fn usage_for(
        &self,
        root: &ScanRoot,
        absolute_path: &Path,
        relative_path: &str,
        asset_type: &str,
    ) -> UnityUsage {
        let path_text = absolute_path.to_string_lossy().replace('\\', "/");
        let relative = relative_path.replace('\\', "/").to_ascii_lowercase();
        let is_unity_asset = path_text.contains("/Assets/");
        let guid = is_image_file(absolute_path)
            .then(|| read_meta_guid(absolute_path))
            .flatten();
        let references = guid.as_ref().and_then(|guid| self.references.get(guid));
        let reference_count = references.map(|refs| refs.count).unwrap_or(0);
        let reference_paths = references
            .map(|refs| refs.samples.clone())
            .unwrap_or_default();
        let status = classify_usage(
            root,
            asset_type,
            is_unity_asset,
            &relative,
            guid.as_deref(),
            reference_count,
        );
        let label = usage_label(&status).to_string();

        UnityUsage {
            guid,
            status,
            label,
            reference_count,
            reference_paths,
        }
    }
}

fn classify_usage(
    root: &ScanRoot,
    asset_type: &str,
    is_unity_asset: bool,
    relative_path: &str,
    guid: Option<&str>,
    reference_count: usize,
) -> String {
    if root.source == "ai-infra" {
        return "external".to_string();
    }
    if root.id == "art-selected" {
        if relative_path.contains("_review/_rejected") {
            return "rejected".to_string();
        }
        return "import_queue".to_string();
    }
    if !is_unity_asset {
        return "raw".to_string();
    }
    if asset_type == "image" && guid.is_some() && reference_count > 0 {
        return "in_game".to_string();
    }
    if is_unity_asset {
        return "unity_imported".to_string();
    }
    "raw".to_string()
}

fn usage_label(status: &str) -> &'static str {
    match status {
        "in_game" => "In Game",
        "unity_imported" => "Unity Import",
        "import_queue" => "Import Queue",
        "rejected" => "Rejected",
        "external" => "External",
        "raw" => "Raw/Reference",
        _ => "Unknown",
    }
}

fn is_reference_file(path: &Path) -> bool {
    matches!(
        path.extension().and_then(OsStr::to_str),
        Some("asset")
            | Some("prefab")
            | Some("unity")
            | Some("mat")
            | Some("controller")
            | Some("overrideController")
            | Some("anim")
            | Some("spriteatlas")
            | Some("spriteatlasv2")
            | Some("uxml")
            | Some("uss")
            | Some("playable")
    )
}

fn collect_guids(text: &str, prefix: &str, output: &mut HashSet<String>) {
    let mut offset = 0;
    while let Some(index) = text[offset..].find(prefix) {
        let start = offset + index + prefix.len();
        let rest = &text[start..];
        let rest = rest.trim_start_matches(char::is_whitespace);
        let candidate: String = rest
            .chars()
            .take_while(|character| character.is_ascii_hexdigit())
            .take(32)
            .collect();
        if candidate.len() == 32 {
            output.insert(candidate.to_ascii_lowercase());
        }
        offset = start;
    }
}

fn read_meta_guid(path: &Path) -> Option<String> {
    let mut meta_path = path.as_os_str().to_os_string();
    meta_path.push(".meta");
    let text = fs::read_to_string(PathBuf::from(meta_path)).ok()?;
    for line in text.lines() {
        let trimmed = line.trim();
        let Some(guid) = trimmed.strip_prefix("guid:") else {
            continue;
        };
        let guid = guid.trim();
        if guid.len() == 32 && guid.chars().all(|character| character.is_ascii_hexdigit()) {
            return Some(guid.to_ascii_lowercase());
        }
    }
    None
}
