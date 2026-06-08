import { useCallback, useDeferredValue, useEffect, useMemo, useState } from "react";
import { AlertTriangle, Images, MessageSquareText, Mic, RefreshCw, Volume2 } from "lucide-react";

import { EngineStrip } from "./components/EngineStrip";
import { FilterSidebar } from "./components/FilterSidebar";
import { Gallery } from "./components/Gallery";
import { Inspector } from "./components/Inspector";
import { NarrativeWorkbench } from "./components/NarrativeWorkbench";
import { SettingsPanel } from "./components/SettingsPanel";
import { SfxWorkbench } from "./components/SfxWorkbench";
import { VoiceLab } from "./components/VoiceLab";
import { searchableText } from "./lib/format";
import {
  checkEngineHealth,
  getConfig,
  listCachedAssets,
  readSidecar,
  scanAssets,
  startEngine,
} from "./lib/ipc";
import type { AppConfig, AssetItem, EngineHealth, Filters } from "./types";

const defaultFilters: Filters = {
  assetType: "all",
  source: "all",
  query: "",
  rootId: "all",
  structureKey: "all",
  unityUsageStatus: "all",
  includeRaw: false,
};

type ActiveView = "gallery" | "sfx" | "narrative" | "voice";

function rootIdsForCurrentScope(filters: Filters, config: AppConfig | null): string[] {
  if (!config) {
    return [];
  }

  const visibleRoots = config.scanRoots.filter((root) => filters.includeRaw || root.defaultVisible);
  if (filters.rootId !== "all") {
    return [filters.rootId];
  }
  if (filters.structureKey !== "all") {
    const rootIds = visibleRoots
      .filter((root) => root.structureKey === filters.structureKey)
      .map((root) => root.id);
    if (rootIds.length > 0) {
      return rootIds;
    }
  }
  if (filters.source !== "all") {
    const rootIds = visibleRoots
      .filter((root) => root.source === filters.source)
      .map((root) => root.id);
    if (rootIds.length > 0) {
      return rootIds;
    }
  }

  switch (filters.assetType) {
    case "voice":
      return ["ai-chatterbox"];
    case "video":
      return ["ai-wan"];
    case "bgm":
      return ["ai-acestep"];
    case "sfx":
      return ["ai-sfx"];
    case "image":
    case "all":
      return [];
  }
}

export default function App() {
  const [config, setConfig] = useState<AppConfig | null>(null);
  const [assets, setAssets] = useState<AssetItem[]>([]);
  const [health, setHealth] = useState<EngineHealth[]>([]);
  const [filters, setFilters] = useState<Filters>(defaultFilters);
  const [selected, setSelected] = useState<AssetItem | null>(null);
  const [sidecar, setSidecar] = useState<unknown | null>(null);
  const [sidecarLoading, setSidecarLoading] = useState(false);
  const [sidecarRefreshKey, setSidecarRefreshKey] = useState(0);
  const [assetLoading, setAssetLoading] = useState(false);
  const [healthLoading, setHealthLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [activeView, setActiveView] = useState<ActiveView>("gallery");

  const applyAssetList = useCallback((nextAssets: AssetItem[]) => {
    setAssets(nextAssets);
    setSelected((current) => {
      if (!current) {
        return nextAssets[0] ?? null;
      }
      return nextAssets.find((asset) => asset.id === current.id) ?? nextAssets[0] ?? null;
    });
    setSidecarRefreshKey((current) => current + 1);
  }, []);

  const loadCachedAssets = useCallback(async () => {
    setAssetLoading(true);
    setError(null);
    try {
      const [nextConfig, cachedAssets] = await Promise.all([
        getConfig(),
        listCachedAssets({ includeRaw: filters.includeRaw }),
      ]);
      setConfig(nextConfig);
      if (cachedAssets.length > 0 && !needsSfxSummaryRefresh(cachedAssets)) {
        applyAssetList(cachedAssets);
        return;
      }

      applyAssetList(await scanAssets({ includeRaw: filters.includeRaw }));
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setAssetLoading(false);
    }
  }, [applyAssetList, filters.includeRaw]);

  const rescanAssets = useCallback(async () => {
    setAssetLoading(true);
    setError(null);
    try {
      const nextConfig = config ?? (await getConfig());
      setConfig(nextConfig);
      const rootIds = rootIdsForCurrentScope(filters, nextConfig);
      const nextAssets = await scanAssets({
        includeRaw: filters.includeRaw,
        rootIds,
      });
      applyAssetList(nextAssets);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setAssetLoading(false);
    }
  }, [applyAssetList, config, filters]);

  const refreshHealth = useCallback(async () => {
    setHealthLoading(true);
    try {
      setHealth(await checkEngineHealth());
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setHealthLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadCachedAssets();
    void refreshHealth();
  }, [loadCachedAssets, refreshHealth]);

  useEffect(() => {
    const handle = window.setInterval(() => {
      void refreshHealth();
    }, 10000);
    return () => window.clearInterval(handle);
  }, [refreshHealth]);

  useEffect(() => {
    if (!selected?.sidecarPath) {
      setSidecar(null);
      setSidecarLoading(false);
      return;
    }

    let disposed = false;
    setSidecarLoading(true);
    readSidecar(selected.sidecarPath)
      .then((value) => {
        if (!disposed) {
          setSidecar(value);
        }
      })
      .catch((err: unknown) => {
        if (!disposed) {
          setSidecar({ error: err instanceof Error ? err.message : String(err) });
        }
      })
      .finally(() => {
        if (!disposed) {
          setSidecarLoading(false);
        }
      });

    return () => {
      disposed = true;
    };
  }, [selected, sidecarRefreshKey]);

  const deferredQuery = useDeferredValue(filters.query);
  const assetSearchText = useMemo(
    () => new Map(assets.map((asset) => [asset.id, searchableText(asset)])),
    [assets],
  );

  const filteredAssets = useMemo(() => {
    const query = deferredQuery.trim().toLowerCase();
    return assets.filter((asset) => {
      if (filters.assetType !== "all" && asset.assetType !== filters.assetType) {
        return false;
      }
      if (filters.source !== "all" && asset.source !== filters.source) {
        return false;
      }
      if (filters.rootId !== "all" && asset.rootId !== filters.rootId) {
        return false;
      }
      if (filters.structureKey !== "all" && asset.structureKey !== filters.structureKey) {
        return false;
      }
      if (
        filters.unityUsageStatus !== "all" &&
        asset.unityUsageStatus !== filters.unityUsageStatus
      ) {
        return false;
      }
      return query.length === 0 || (assetSearchText.get(asset.id) ?? "").includes(query);
    });
  }, [
    assetSearchText,
    assets,
    deferredQuery,
    filters.assetType,
    filters.rootId,
    filters.source,
    filters.structureKey,
    filters.unityUsageStatus,
  ]);

  const counts = useMemo(() => {
    const next: Record<string, number> = { all: assets.length };
    for (const asset of assets) {
      next[asset.assetType] = (next[asset.assetType] ?? 0) + 1;
    }
    return next;
  }, [assets]);

  const sources = useMemo(
    () => Array.from(new Set(assets.map((asset) => asset.source))).sort(),
    [assets],
  );

  const structureCounts = useMemo(() => {
    const next: Record<string, number> = { all: assets.length };
    for (const asset of assets) {
      next[asset.structureKey] = (next[asset.structureKey] ?? 0) + 1;
    }
    return next;
  }, [assets]);

  const unityUsageCounts = useMemo(() => {
    const next: Record<string, number> = { all: assets.length };
    for (const asset of assets) {
      next[asset.unityUsageStatus] = (next[asset.unityUsageStatus] ?? 0) + 1;
    }
    return next;
  }, [assets]);

  const handleStartEngine = useCallback(
    async (engineId: string) => {
      try {
        const result = await startEngine(engineId);
        setError(result.message);
        window.setTimeout(() => void refreshHealth(), 2500);
      } catch (err) {
        setError(err instanceof Error ? err.message : String(err));
      }
    },
    [refreshHealth],
  );

  const handleReviewAsset = useCallback((asset: AssetItem) => {
    setSelected(asset);
    setFilters((current) => ({
      ...current,
      assetType: "sfx",
      source: "all",
      rootId: "all",
      structureKey: "all",
      unityUsageStatus: "all",
      query: "",
    }));
    setActiveView("gallery");
  }, []);

  return (
    <div className="app-shell">
      <header className="app-header">
        <div>
          <h1>Survival Asset Studio</h1>
          <p>
            {activeView === "gallery"
              ? `${assets.length.toLocaleString()} indexed media assets${
                  filters.includeRaw ? " including raw output" : " in focused scope"
                }`
              : activeView === "sfx"
                ? "SFX manifest coverage and QC triage"
                : activeView === "voice"
                  ? "Character voice casting & audition"
                  : "Narrative voice coverage"}
          </p>
        </div>
        <div className="header-actions">
          <div className="view-tabs" role="tablist" aria-label="Asset Studio view">
            <button
              className={activeView === "gallery" ? "selected" : ""}
              type="button"
              role="tab"
              aria-selected={activeView === "gallery"}
              onClick={() => setActiveView("gallery")}
            >
              <Images size={16} />
              <span>Assets</span>
            </button>
            <button
              className={activeView === "sfx" ? "selected" : ""}
              type="button"
              role="tab"
              aria-selected={activeView === "sfx"}
              onClick={() => setActiveView("sfx")}
            >
              <Volume2 size={16} />
              <span>SFX</span>
            </button>
            <button
              className={activeView === "voice" ? "selected" : ""}
              type="button"
              role="tab"
              aria-selected={activeView === "voice"}
              onClick={() => setActiveView("voice")}
            >
              <Mic size={16} />
              <span>Voice</span>
            </button>
            <button
              className={activeView === "narrative" ? "selected" : ""}
              type="button"
              role="tab"
              aria-selected={activeView === "narrative"}
              onClick={() => setActiveView("narrative")}
            >
              <MessageSquareText size={16} />
              <span>Narrative</span>
            </button>
          </div>
          {activeView === "gallery" || activeView === "sfx" ? (
            <button
              className="command-button"
              type="button"
              onClick={rescanAssets}
              disabled={assetLoading}
            >
              <RefreshCw className={assetLoading ? "spin" : ""} size={17} />
              <span>Rescan</span>
            </button>
          ) : null}
        </div>
      </header>

      <EngineStrip
        engines={health}
        loading={healthLoading}
        onRefresh={refreshHealth}
        onStart={handleStartEngine}
      />

      {error ? (
        <div className="notice" role="status">
          <AlertTriangle size={16} />
          <span>{error}</span>
        </div>
      ) : null}

      {activeView === "gallery" ? (
        <div className="content-grid">
          <FilterSidebar
            filters={filters}
            roots={config?.scanRoots ?? []}
            sources={sources}
            counts={counts}
            structureCounts={structureCounts}
            unityUsageCounts={unityUsageCounts}
            onChange={setFilters}
          />
          <Gallery
            assets={filteredAssets}
            selectedId={selected?.id ?? null}
            onSelect={setSelected}
          />
          <Inspector asset={selected} sidecar={sidecar} sidecarLoading={sidecarLoading} />
        </div>
      ) : activeView === "sfx" ? (
        <SfxWorkbench assets={assets} onReviewAsset={handleReviewAsset} />
      ) : activeView === "voice" ? (
        <VoiceLab health={health} onStartEngine={handleStartEngine} />
      ) : (
        <NarrativeWorkbench />
      )}

      <SettingsPanel config={config} />
    </div>
  );
}

function needsSfxSummaryRefresh(assets: AssetItem[]): boolean {
  return assets.some(
    (asset) => asset.assetType === "sfx" && asset.sidecarPath !== null && asset.sfxQcStatus === null,
  );
}
