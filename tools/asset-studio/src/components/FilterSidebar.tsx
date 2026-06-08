import {
  Archive,
  Camera,
  CheckCircle2,
  Clapperboard,
  Cpu,
  Folder,
  Image,
  Layers,
  Mic2,
  Mountain,
  Music,
  PackageCheck,
  PanelTop,
  Search,
  Shapes,
  SlidersHorizontal,
  UserRound,
  Video,
  Volume2,
} from "lucide-react";

import { ASSET_TYPES, labelForType } from "../lib/format";
import type { AssetType, Filters, ScanRoot } from "../types";

interface Props {
  filters: Filters;
  roots: ScanRoot[];
  sources: string[];
  counts: Record<string, number>;
  structureCounts: Record<string, number>;
  unityUsageCounts: Record<string, number>;
  onChange: (filters: Filters) => void;
}

const typeIcons: Record<AssetType | "all", React.ReactNode> = {
  all: <SlidersHorizontal size={16} />,
  voice: <Mic2 size={16} />,
  image: <Image size={16} />,
  video: <Video size={16} />,
  bgm: <Music size={16} />,
  sfx: <Volume2 size={16} />,
};

const collections: Array<{ key: string | "all"; label: string; icon: React.ReactNode }> = [
  { key: "all", label: "Loaded", icon: <Folder size={16} /> },
  { key: "selected", label: "Selected", icon: <Layers size={16} /> },
  { key: "runtime", label: "Runtime", icon: <Cpu size={16} /> },
  { key: "characters", label: "Characters", icon: <UserRound size={16} /> },
  { key: "cutscene", label: "Cutscene CG", icon: <Clapperboard size={16} /> },
  { key: "backgrounds", label: "Backgrounds", icon: <Mountain size={16} /> },
  { key: "icons", label: "Icons", icon: <Shapes size={16} /> },
  { key: "ui", label: "UI", icon: <PanelTop size={16} /> },
  { key: "ai", label: "AI Results", icon: <SlidersHorizontal size={16} /> },
  { key: "raw", label: "Raw Output", icon: <Archive size={16} /> },
  { key: "reference", label: "References", icon: <Image size={16} /> },
  { key: "captures", label: "Captures", icon: <Camera size={16} /> },
];

const unityUsageFilters: Array<{ key: string | "all"; label: string; icon: React.ReactNode }> = [
  { key: "all", label: "All Usage", icon: <Folder size={16} /> },
  { key: "in_game", label: "In Game", icon: <CheckCircle2 size={16} /> },
  { key: "unity_imported", label: "Unity Import", icon: <PackageCheck size={16} /> },
  { key: "import_queue", label: "Import Queue", icon: <Layers size={16} /> },
  { key: "external", label: "External", icon: <SlidersHorizontal size={16} /> },
  { key: "raw", label: "Raw/Reference", icon: <Archive size={16} /> },
  { key: "rejected", label: "Rejected", icon: <Archive size={16} /> },
];

export function FilterSidebar({
  filters,
  roots,
  sources,
  counts,
  structureCounts,
  unityUsageCounts,
  onChange,
}: Props) {
  const visibleRoots = roots.filter((root) => filters.includeRaw || root.defaultVisible);

  return (
    <aside className="sidebar">
      <label className="search-box">
        <Search size={17} />
        <input
          value={filters.query}
          onChange={(event) => onChange({ ...filters, query: event.target.value })}
          placeholder="Search assets"
        />
      </label>

      <section className="sidebar-section">
        <h2>Scope</h2>
        <label className="toggle-row">
          <input
            type="checkbox"
            checked={filters.includeRaw}
            onChange={(event) => {
              const includeRaw = event.target.checked;
              onChange({
                ...filters,
                includeRaw,
                rootId: includeRaw ? filters.rootId : "all",
                structureKey: includeRaw ? filters.structureKey : "all",
                unityUsageStatus: includeRaw ? filters.unityUsageStatus : "all",
              });
            }}
          />
          <span>Include raw output and captures</span>
        </label>
      </section>

      <section className="sidebar-section">
        <h2>Unity Usage</h2>
        <div className="segmented-list">
          {unityUsageFilters.map((usage) => {
            const count = unityUsageCounts[usage.key] ?? 0;
            if (count === 0 && usage.key !== "all") {
              return null;
            }
            return (
              <button
                className={filters.unityUsageStatus === usage.key ? "selected" : ""}
                key={usage.key}
                type="button"
                onClick={() => onChange({ ...filters, unityUsageStatus: usage.key })}
              >
                {usage.icon}
                <span>{usage.label}</span>
                <em>{count}</em>
              </button>
            );
          })}
        </div>
      </section>

      <section className="sidebar-section">
        <h2>Collection</h2>
        <div className="segmented-list">
          {collections.map((collection) => {
            const count = structureCounts[collection.key] ?? 0;
            if (count === 0 && collection.key !== "all") {
              return null;
            }
            return (
              <button
                className={filters.structureKey === collection.key ? "selected" : ""}
                key={collection.key}
                type="button"
                onClick={() => onChange({ ...filters, structureKey: collection.key })}
              >
                {collection.icon}
                <span>{collection.label}</span>
                <em>{count}</em>
              </button>
            );
          })}
        </div>
      </section>

      <section className="sidebar-section">
        <h2>Type</h2>
        <div className="segmented-list">
          {ASSET_TYPES.map((type) => (
            <button
              className={filters.assetType === type ? "selected" : ""}
              key={type}
              type="button"
              onClick={() => onChange({ ...filters, assetType: type })}
            >
              {typeIcons[type]}
              <span>{labelForType(type)}</span>
              <em>{counts[type] ?? 0}</em>
            </button>
          ))}
        </div>
      </section>

      <section className="sidebar-section">
        <h2>Source</h2>
        <select
          value={filters.source}
          onChange={(event) => onChange({ ...filters, source: event.target.value })}
        >
          <option value="all">All sources</option>
          {sources.map((source) => (
            <option value={source} key={source}>
              {source}
            </option>
          ))}
        </select>
      </section>

      <section className="sidebar-section">
        <h2>Root</h2>
        <select
          value={filters.rootId}
          onChange={(event) => onChange({ ...filters, rootId: event.target.value })}
        >
          <option value="all">All roots</option>
          {visibleRoots.map((root) => (
            <option value={root.id} key={root.id}>
              {root.label}
            </option>
          ))}
        </select>
      </section>
    </aside>
  );
}
