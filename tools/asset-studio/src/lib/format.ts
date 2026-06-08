import type { AssetItem, AssetType } from "../types";

export const ASSET_TYPES: Array<AssetType | "all"> = [
  "all",
  "voice",
  "image",
  "video",
  "bgm",
  "sfx",
];

export function formatBytes(bytes: number): string {
  if (bytes < 1024) {
    return `${bytes} B`;
  }
  const units = ["KB", "MB", "GB", "TB"];
  let value = bytes / 1024;
  let index = 0;
  while (value >= 1024 && index < units.length - 1) {
    value /= 1024;
    index += 1;
  }
  return `${value.toFixed(value >= 10 ? 1 : 2)} ${units[index]}`;
}

export function formatDate(ms: number): string {
  if (ms <= 0) {
    return "-";
  }
  return new Intl.DateTimeFormat(undefined, {
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
  }).format(new Date(ms));
}

export function labelForType(type: AssetType | "all"): string {
  switch (type) {
    case "all":
      return "All";
    case "voice":
      return "Voice";
    case "image":
      return "Image";
    case "video":
      return "Video";
    case "bgm":
      return "BGM";
    case "sfx":
      return "SFX";
  }
}

export function compactPath(path: string): string {
  return path.replace(/\\/g, "/");
}

export function searchableText(asset: AssetItem): string {
  return [
    asset.name,
    asset.relativePath,
    asset.absolutePath,
    asset.rootLabel,
    asset.source,
    asset.assetType,
    asset.extension,
    asset.structureKey,
    asset.structureLabel,
    asset.unityGuid ?? "",
    asset.unityUsageStatus,
    asset.unityUsageLabel,
    asset.unityUsagePaths.join(" "),
    asset.sfxHookId ?? "",
    asset.sfxRuntimeHookId ?? "",
    asset.sfxVariantKey ?? "",
    asset.sfxProfileKey ?? "",
    asset.sfxQcStatus ?? "",
    asset.sfxQcLabel ?? "",
  ]
    .join(" ")
    .toLowerCase();
}
