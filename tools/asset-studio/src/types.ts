export type AssetType = "voice" | "image" | "video" | "bgm" | "sfx";

export interface ScanRoot {
  id: string;
  label: string;
  path: string;
  source: string;
  defaultVisible: boolean;
  structureKey: string;
}

export interface EngineConfig {
  id: string;
  name: string;
  kind: string;
  baseUrl: string | null;
  healthUrl: string | null;
  startScript: string | null;
  outputRoot: string | null;
  enabled: boolean;
}

export interface AppConfig {
  projectRoot: string;
  aiInfraRoot: string;
  cacheRoot: string;
  configSource: string;
  scanRoots: ScanRoot[];
  engines: EngineConfig[];
}

export interface AssetItem {
  id: string;
  assetType: AssetType;
  source: string;
  rootId: string;
  rootLabel: string;
  name: string;
  absolutePath: string;
  relativePath: string;
  extension: string;
  sizeBytes: number;
  modifiedMs: number;
  sidecarPath: string | null;
  sfxHookId: string | null;
  sfxRuntimeHookId: string | null;
  sfxVariantKey: string | null;
  sfxProfileKey: string | null;
  sfxQcStatus: SfxQcStatus | null;
  sfxQcLabel: string | null;
  sfxQcScore: number | null;
  thumbnailPath: string | null;
  structureKey: string;
  structureLabel: string;
  defaultVisible: boolean;
  unityGuid: string | null;
  unityUsageStatus: string;
  unityUsageLabel: string;
  unityUsageCount: number;
  unityUsagePaths: string[];
  indexedMs: number;
}

export type SfxQcStatus = "red" | "yellow" | "green" | "missing" | "unknown";

export interface EngineHealth {
  id: string;
  name: string;
  status: "online" | "offline" | "configured" | "missing_config" | "disabled" | "unknown";
  message: string;
  latencyMs: number | null;
  canStart: boolean;
}

export interface EngineStartResult {
  id: string;
  started: boolean;
  message: string;
}

export interface ImageInfo {
  width: number;
  height: number;
}

export interface Filters {
  assetType: AssetType | "all";
  source: string | "all";
  query: string;
  rootId: string | "all";
  structureKey: string | "all";
  unityUsageStatus: string | "all";
  includeRaw: boolean;
}

export interface AssetScanOptions {
  includeRaw: boolean;
  rootIds?: string[];
}

export interface NarrativeIndex {
  source: string;
  sourceHash: string;
  seedPath: string;
  seedModifiedMs: number;
  unityRoot: string;
  totalSequences: number;
  totalLines: number;
  unityMatchedSequences: number;
  unityMissingSequences: number;
  unityMismatchedSequences: number;
  jaReadyLines: number;
  voiceGeneratedLines: number;
  voiceRoleLines: number;
  liveSequences: number;
  unreachableSequences: number;
  voiceRoots: string[];
  sequences: NarrativeSequence[];
  speakers: NarrativeSpeakerSummary[];
  diagnostics: string[];
}

export interface SceneVisual {
  tier: string; // T0 | T1 | T2 | T3 | T4 | card
  medium: string; // cutscene | dialogue-scene | dialogue-overlay | combat-bark | reward-join | story-card | dialogue
  backdrop: string | null; // null | "shared:{bg_id}" | "bespoke:{cut_id}"
  motion: string; // none | static | kenburns | parallax | video
  lut: string; // ash | teal | violet | frost | ember | neutral
  curated: boolean;
  note?: string;
}

export interface NarrativeSequence {
  id: string;
  sequenceId: string;
  presentationKey: string;
  presentationKind: string;
  artifactSlug: string;
  title: string;
  meta: Record<string, unknown> | null;
  lineCount: number;
  jaReadyCount: number;
  voiceGeneratedCount: number;
  voiceRoleCount: number;
  reachability: "live" | "wired_unbuilt" | "unreachable" | string;
  liveMoment: string;
  liveEventId: string;
  speakers: string[];
  unity: UnitySequenceStatus;
  lines: NarrativeLine[];
}

export interface UnitySequenceStatus {
  status: "matched" | "missing" | "line_mismatch" | string;
  label: string;
  assetPath: string | null;
  lineCount: number;
  modifiedMs: number;
}

export interface NarrativeLine {
  id: string;
  lineId: string;
  lineIndex: number;
  speakerAlias: string;
  speakerId: string;
  emotionRaw: string;
  emotionId: string;
  emoteId: string;
  koText: string;
  jaTtsText: string;
  ttsTextStatus: "ready" | "pending_translation" | string;
  branchTag: string;
  sectionLabel: string;
  voiceRole: string;
  textKey: string;
  voiceKey: string;
  voiceStatus: "generated" | "missing" | string;
  voiceAssetPath: string | null;
  refs: NarrativeLineRef[];
}

export interface NarrativeLineRef {
  alias: string;
  id: string;
  selfRef: boolean;
}

export interface NarrativeSpeakerSummary {
  speakerId: string;
  alias: string;
  lineCount: number;
  jaReadyCount: number;
  voiceGeneratedCount: number;
  voiceRoleCount: number;
}

export interface GenerateVoiceRequest {
  text: string;
  engine: string;
  language?: string;
  voiceId?: string;
  referenceAudioPath?: string;
  outputName?: string;
  seed?: number;
  exaggeration?: number;
  cfgWeight?: number;
}

export interface GenerateVoiceOutput {
  id: string;
  audioUrl: string;
  outputPath: string;
  engine: string;
  modelId: string;
  audioDuration: number;
}
