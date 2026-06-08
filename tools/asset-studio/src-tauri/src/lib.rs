mod asset_index;
mod assets;
mod backdrop;
mod commands;
mod config;
mod engines;
mod narrative;
mod thumbnails;
mod unity_usage;
mod voice;

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    tauri::Builder::default()
        .invoke_handler(tauri::generate_handler![
            commands::get_config,
            commands::scan_assets,
            commands::list_cached_assets,
            commands::read_sidecar,
            commands::ensure_thumbnail,
            commands::get_image_info,
            commands::open_asset_external,
            commands::get_narrative_index,
            commands::resolve_backdrop_image,
            commands::check_engine_health,
            commands::start_engine,
            commands::generate_voice,
        ])
        .run(tauri::generate_context!())
        .expect("failed to run Survival Asset Studio");
}
