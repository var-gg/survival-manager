use std::fs;
use std::path::PathBuf;
#[cfg(not(target_os = "windows"))]
use std::process::Command;
use std::time::Instant;

use image::ImageReader;
use serde::Serialize;
use serde_json::Value;

use crate::assets::{self, AssetItem, AssetScanOptions};
use crate::backdrop::{self, ResolvedBackdrop};
use crate::cue::{self, ResolvedCue};
use crate::config::{load_config, AppConfig};
use crate::engines::{self, EngineHealth, EngineStartResult};
use crate::narrative::{self, NarrativeIndex};
use crate::thumbnails::ThumbnailCache;
use crate::voice::{self, GenerateVoiceOutput, GenerateVoiceRequest};

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ImageInfo {
    width: u32,
    height: u32,
}

#[tauri::command]
pub fn get_config() -> Result<AppConfig, String> {
    load_config()
}

#[tauri::command]
pub fn scan_assets(options: Option<AssetScanOptions>) -> Result<Vec<AssetItem>, String> {
    let started = Instant::now();
    let config = load_config()?;
    let options = options.unwrap_or_default();
    let assets = assets::scan(&config, options)?;
    eprintln!(
        "[asset-studio] scan_assets returned {} assets in {} ms",
        assets.len(),
        started.elapsed().as_millis()
    );
    Ok(assets)
}

#[tauri::command]
pub fn list_cached_assets(options: Option<AssetScanOptions>) -> Result<Vec<AssetItem>, String> {
    let started = Instant::now();
    let config = load_config()?;
    let assets = assets::list_cached(&config, options.unwrap_or_default())?;
    eprintln!(
        "[asset-studio] list_cached_assets returned {} assets in {} ms",
        assets.len(),
        started.elapsed().as_millis()
    );
    Ok(assets)
}

#[tauri::command]
pub fn read_sidecar(path: String) -> Result<Option<Value>, String> {
    let sidecar = PathBuf::from(path);
    if !sidecar.exists() {
        return Ok(None);
    }
    if sidecar.extension().and_then(|ext| ext.to_str()) != Some("json") {
        return Err("sidecar path must point to a JSON file".to_string());
    }

    let text = fs::read_to_string(&sidecar)
        .map_err(|err| format!("failed to read {}: {err}", sidecar.display()))?;
    let value = serde_json::from_str::<Value>(&text)
        .map_err(|err| format!("failed to parse {}: {err}", sidecar.display()))?;
    Ok(Some(value))
}

#[tauri::command]
pub fn ensure_thumbnail(
    path: String,
    modified_ms: u64,
    size_bytes: u64,
) -> Result<Option<String>, String> {
    let source = PathBuf::from(path);
    if !source.exists() {
        return Ok(None);
    }
    if !assets::is_image_file(&source) {
        return Ok(None);
    }

    let config = load_config()?;
    let cache_root = PathBuf::from(config.cache_root);
    let cache = ThumbnailCache::new(&cache_root)?;
    Ok(cache.ensure_thumbnail_for(&source, modified_ms, size_bytes))
}

#[tauri::command]
pub fn get_image_info(path: String) -> Result<Option<ImageInfo>, String> {
    let source = PathBuf::from(path);
    if !source.exists() || !assets::is_image_file(&source) {
        return Ok(None);
    }

    let reader = match ImageReader::open(&source).and_then(|reader| reader.with_guessed_format()) {
        Ok(reader) => reader,
        Err(_) => return Ok(None),
    };
    match reader.into_dimensions() {
        Ok((width, height)) => Ok(Some(ImageInfo { width, height })),
        Err(_) => Ok(None),
    }
}

#[tauri::command]
pub fn open_asset_external(path: String) -> Result<(), String> {
    let source = PathBuf::from(path);
    if !source.exists() {
        return Err(format!("asset does not exist: {}", source.display()));
    }

    open_path(&source)
}

#[tauri::command]
pub fn get_narrative_index() -> Result<NarrativeIndex, String> {
    let config = load_config()?;
    narrative::load(&config)
}

#[tauri::command]
pub fn resolve_backdrop_image(backdrop: String) -> Result<ResolvedBackdrop, String> {
    let config = load_config()?;
    Ok(backdrop::resolve(&config, &backdrop))
}

#[tauri::command]
pub fn resolve_cue_track(cue_id: String) -> Result<ResolvedCue, String> {
    let config = load_config()?;
    Ok(cue::resolve(&config, &cue_id))
}

#[tauri::command]
pub async fn check_engine_health() -> Result<Vec<EngineHealth>, String> {
    let config = load_config()?;
    Ok(engines::check_all(&config).await)
}

#[tauri::command]
pub fn start_engine(engine_id: String) -> Result<EngineStartResult, String> {
    let config = load_config()?;
    engines::start(&config, &engine_id)
}

#[tauri::command]
pub async fn generate_voice(request: GenerateVoiceRequest) -> Result<GenerateVoiceOutput, String> {
    voice::generate(request).await
}

#[cfg(target_os = "windows")]
fn open_path(path: &PathBuf) -> Result<(), String> {
    use std::os::windows::ffi::OsStrExt;
    use std::ptr;
    use windows_sys::Win32::UI::Shell::ShellExecuteW;

    let operation: Vec<u16> = "open".encode_utf16().chain(Some(0)).collect();
    let file: Vec<u16> = path.as_os_str().encode_wide().chain(Some(0)).collect();
    let result = unsafe {
        ShellExecuteW(
            ptr::null_mut(),
            operation.as_ptr(),
            file.as_ptr(),
            ptr::null(),
            ptr::null(),
            1,
        )
    } as isize;

    if result <= 32 {
        return Err(format!(
            "failed to open {} with default Windows app: ShellExecuteW returned {result}",
            path.display()
        ));
    }

    Ok(())
}

#[cfg(target_os = "macos")]
fn open_path(path: &PathBuf) -> Result<(), String> {
    Command::new("open")
        .arg(path)
        .spawn()
        .map(|_| ())
        .map_err(|err| format!("failed to open {}: {err}", path.display()))
}

#[cfg(all(unix, not(target_os = "macos")))]
fn open_path(path: &PathBuf) -> Result<(), String> {
    Command::new("xdg-open")
        .arg(path)
        .spawn()
        .map(|_| ())
        .map_err(|err| format!("failed to open {}: {err}", path.display()))
}
