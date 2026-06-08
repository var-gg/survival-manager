import { invoke } from "@tauri-apps/api/core";

import type {
  AppConfig,
  AssetScanOptions,
  AssetItem,
  EngineHealth,
  EngineStartResult,
  GenerateVoiceOutput,
  GenerateVoiceRequest,
  ImageInfo,
  NarrativeIndex,
} from "../types";

export async function getConfig(): Promise<AppConfig> {
  return invoke<AppConfig>("get_config");
}

export async function scanAssets(options: AssetScanOptions): Promise<AssetItem[]> {
  return invoke<AssetItem[]>("scan_assets", { options });
}

export async function listCachedAssets(options: AssetScanOptions): Promise<AssetItem[]> {
  return invoke<AssetItem[]>("list_cached_assets", { options });
}

export async function readSidecar(path: string): Promise<unknown | null> {
  return invoke<unknown | null>("read_sidecar", { path });
}

export async function ensureThumbnail(
  path: string,
  modifiedMs: number,
  sizeBytes: number,
): Promise<string | null> {
  return invoke<string | null>("ensure_thumbnail", { path, modifiedMs, sizeBytes });
}

export async function getImageInfo(path: string): Promise<ImageInfo | null> {
  return invoke<ImageInfo | null>("get_image_info", { path });
}

export async function openAssetExternal(path: string): Promise<void> {
  return invoke<void>("open_asset_external", { path });
}

export async function getNarrativeIndex(): Promise<NarrativeIndex> {
  return invoke<NarrativeIndex>("get_narrative_index");
}

export async function checkEngineHealth(): Promise<EngineHealth[]> {
  return invoke<EngineHealth[]>("check_engine_health");
}

export async function startEngine(engineId: string): Promise<EngineStartResult> {
  return invoke<EngineStartResult>("start_engine", { engineId });
}

export async function generateVoice(request: GenerateVoiceRequest): Promise<GenerateVoiceOutput> {
  return invoke<GenerateVoiceOutput>("generate_voice", { request });
}
