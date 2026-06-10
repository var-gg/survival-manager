use std::env;
use std::fs;
use std::path::{Path, PathBuf};

use serde::{Deserialize, Serialize};

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ScanRoot {
    pub id: String,
    pub label: String,
    pub path: String,
    pub source: String,
    #[serde(default = "default_true")]
    pub default_visible: bool,
    #[serde(default)]
    pub structure_key: String,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct EngineConfig {
    pub id: String,
    pub name: String,
    pub kind: String,
    pub base_url: Option<String>,
    pub health_url: Option<String>,
    pub start_script: Option<String>,
    pub output_root: Option<String>,
    pub enabled: bool,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct AppConfig {
    pub project_root: String,
    pub ai_infra_root: String,
    pub cache_root: String,
    pub config_source: String,
    pub scan_roots: Vec<ScanRoot>,
    pub engines: Vec<EngineConfig>,
}

#[derive(Debug, Default, Deserialize)]
#[serde(rename_all = "camelCase")]
struct FileConfig {
    project_root: Option<String>,
    ai_infra_root: Option<String>,
    cache_root: Option<String>,
    scan_roots: Option<Vec<ScanRoot>>,
    engines: Option<Vec<EngineConfig>>,
}

pub fn load_config() -> Result<AppConfig, String> {
    let (file_config, config_source) = load_file_config()?;
    let project_root = pick_path(
        file_config.project_root,
        "SURVIVAL_MANAGER_ROOT",
        default_project_root(),
    );
    let ai_infra_root = pick_path(
        file_config.ai_infra_root,
        "AI_INFRA_ROOT",
        PathBuf::from(r"C:\projects\ai-infra"),
    );
    let cache_root = pick_path(
        file_config.cache_root,
        "ASSET_STUDIO_CACHE_ROOT",
        default_cache_root(),
    );

    let scan_roots = file_config
        .scan_roots
        .unwrap_or_else(|| default_scan_roots(&project_root, &ai_infra_root));
    let engines = file_config
        .engines
        .unwrap_or_else(|| default_engines(&ai_infra_root));

    Ok(AppConfig {
        project_root: path_to_string(project_root),
        ai_infra_root: path_to_string(ai_infra_root),
        cache_root: path_to_string(cache_root),
        config_source,
        scan_roots,
        engines,
    })
}

fn load_file_config() -> Result<(FileConfig, String), String> {
    let Some(path) = env::var_os("ASSET_STUDIO_CONFIG").map(PathBuf::from) else {
        return Ok((FileConfig::default(), "defaults+env".to_string()));
    };

    if !path.exists() {
        return Err(format!(
            "ASSET_STUDIO_CONFIG points to a missing file: {}",
            path.display()
        ));
    }

    let text = fs::read_to_string(&path)
        .map_err(|err| format!("failed to read {}: {err}", path.display()))?;
    let config = serde_json::from_str::<FileConfig>(&text)
        .map_err(|err| format!("failed to parse {}: {err}", path.display()))?;
    Ok((config, path_to_string(path)))
}

fn pick_path(config_value: Option<String>, env_key: &str, fallback: PathBuf) -> PathBuf {
    config_value
        .map(PathBuf::from)
        .or_else(|| env::var_os(env_key).map(PathBuf::from))
        .unwrap_or(fallback)
}

fn default_project_root() -> PathBuf {
    let manifest_dir = PathBuf::from(env!("CARGO_MANIFEST_DIR"));
    manifest_dir
        .parent()
        .and_then(Path::parent)
        .and_then(Path::parent)
        .map(Path::to_path_buf)
        .unwrap_or(manifest_dir)
}

fn default_cache_root() -> PathBuf {
    let manifest_dir = PathBuf::from(env!("CARGO_MANIFEST_DIR"));
    manifest_dir
        .parent()
        .map(|path| path.join(".cache"))
        .unwrap_or_else(|| manifest_dir.join(".cache"))
}

fn default_scan_roots(project_root: &Path, ai_infra_root: &Path) -> Vec<ScanRoot> {
    let project_roots = [
        (
            "art-output",
            "Art Pipeline Output",
            "art-pipeline/output",
            "working",
            false,
            "raw",
        ),
        (
            "art-selected",
            "Art Pipeline Selected",
            "art-pipeline/selected",
            "selected",
            true,
            "selected",
        ),
        (
            "art-cg",
            "Narrative CG",
            "art-pipeline/cg",
            "narrative",
            true,
            "cutscene",
        ),
        (
            "art-sfx",
            "SFX Approved",
            "art-pipeline/sfx/approved",
            "approved",
            true,
            "sfx",
        ),
        (
            "art-ref",
            "Art Pipeline References",
            "art-pipeline/ref",
            "reference",
            false,
            "reference",
        ),
        (
            "runtime-art",
            "Runtime Art",
            "Assets/Resources/_Game/Art",
            "runtime",
            true,
            "runtime",
        ),
        (
            "ui-owned",
            "Project UI",
            "Assets/_Game/UI",
            "runtime-ui",
            true,
            "ui",
        ),
        (
            "captures", "Captures", "Captures", "capture", false, "captures",
        ),
        (
            "screenshots",
            "Screenshots",
            "Screenshots",
            "screenshot",
            false,
            "captures",
        ),
    ];

    let mut roots: Vec<ScanRoot> = project_roots
        .iter()
        .map(
            |(id, label, relative, source, default_visible, structure_key)| ScanRoot {
                id: (*id).to_string(),
                label: (*label).to_string(),
                path: path_to_string(project_root.join(relative)),
                source: (*source).to_string(),
                default_visible: *default_visible,
                structure_key: (*structure_key).to_string(),
            },
        )
        .collect();

    roots.extend([
        ai_output_root(ai_infra_root, "chatterbox", "AI Infra Voice"),
        ai_output_root(ai_infra_root, "acestep", "AI Infra BGM"),
        ai_output_root(ai_infra_root, "sfx", "AI Infra SFX"),
        ai_output_root(ai_infra_root, "wan", "AI Infra Video"),
        ai_output_root(ai_infra_root, "image-api", "AI Infra Images"),
    ]);

    roots
}

fn ai_output_root(ai_infra_root: &Path, engine: &str, label: &str) -> ScanRoot {
    ScanRoot {
        id: format!("ai-{engine}"),
        label: label.to_string(),
        path: path_to_string(ai_infra_root.join("data").join(engine).join("outputs")),
        source: "ai-infra".to_string(),
        default_visible: true,
        structure_key: "ai".to_string(),
    }
}

fn default_engines(ai_infra_root: &Path) -> Vec<EngineConfig> {
    vec![
        local_engine(ai_infra_root, "chatterbox", "Chatterbox Voice", 8001),
        local_engine(ai_infra_root, "acestep", "ACE-Step BGM", 8002),
        local_engine(ai_infra_root, "sfx", "MOSS SFX", 8003),
        local_engine(ai_infra_root, "wan", "Wan Video", 8004),
        EngineConfig {
            id: "image-api".to_string(),
            name: "External Image API".to_string(),
            kind: "external-image".to_string(),
            base_url: None,
            health_url: None,
            start_script: None,
            output_root: Some(path_to_string(
                ai_infra_root.join("data").join("image-api").join("outputs"),
            )),
            enabled: true,
        },
    ]
}

fn local_engine(ai_infra_root: &Path, id: &str, name: &str, port: u16) -> EngineConfig {
    let base_url = env::var(format!(
        "ASSET_STUDIO_{}_URL",
        id.to_uppercase().replace('-', "_")
    ))
    .unwrap_or_else(|_| format!("http://127.0.0.1:{port}"));

    EngineConfig {
        id: id.to_string(),
        name: name.to_string(),
        kind: "local-http".to_string(),
        health_url: Some(format!("{base_url}/health")),
        base_url: Some(base_url),
        start_script: Some(path_to_string(
            ai_infra_root
                .join("scripts")
                .join(format!("serve-{id}.ps1")),
        )),
        output_root: Some(path_to_string(
            ai_infra_root.join("data").join(id).join("outputs"),
        )),
        enabled: true,
    }
}

pub fn path_to_string(path: impl AsRef<Path>) -> String {
    path.as_ref().to_string_lossy().to_string()
}

fn default_true() -> bool {
    true
}
