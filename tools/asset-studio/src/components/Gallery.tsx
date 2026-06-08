import { convertFileSrc } from "@tauri-apps/api/core";
import { FileQuestion, Image, Mic2, Music, Video, Volume2 } from "lucide-react";
import { useEffect, useLayoutEffect, useMemo, useRef, useState } from "react";

import { formatBytes } from "../lib/format";
import { ensureThumbnail } from "../lib/ipc";
import { materialLabelForVariantKey, qcLabel } from "../lib/sfxPlan";
import type { AssetItem, AssetType } from "../types";
import "./Gallery.css";

interface Props {
  assets: AssetItem[];
  selectedId: string | null;
  onSelect: (asset: AssetItem) => void;
}

const cardMinWidth = 178;
const cardHeight = 236;
const gridGap = 10;
const overscanRows = 3;

const icons: Record<AssetType, React.ReactNode> = {
  voice: <Mic2 size={17} />,
  image: <Image size={17} />,
  video: <Video size={17} />,
  bgm: <Music size={17} />,
  sfx: <Volume2 size={17} />,
};

export function Gallery({ assets, selectedId, onSelect }: Props) {
  const containerRef = useRef<HTMLElement | null>(null);
  const [viewport, setViewport] = useState({ width: 0, height: 0, scrollTop: 0 });

  useLayoutEffect(() => {
    const element = containerRef.current;
    if (!element) {
      return;
    }

    const updateViewport = () => {
      const rect = element.getBoundingClientRect();
      setViewport({
        width: Math.max(element.clientWidth, Math.floor(rect.width)),
        height: Math.max(element.clientHeight, Math.floor(rect.height)),
        scrollTop: element.scrollTop,
      });
    };

    updateViewport();
    const raf = window.requestAnimationFrame(updateViewport);
    const observer = new ResizeObserver(updateViewport);
    observer.observe(element);
    window.addEventListener("resize", updateViewport);
    return () => {
      window.cancelAnimationFrame(raf);
      window.removeEventListener("resize", updateViewport);
      observer.disconnect();
    };
  }, []);

  useEffect(() => {
    const element = containerRef.current;
    if (!element) {
      return;
    }

    element.scrollTop = 0;
    const rect = element.getBoundingClientRect();
    setViewport({
      width: Math.max(element.clientWidth, Math.floor(rect.width)),
      height: Math.max(element.clientHeight, Math.floor(rect.height)),
      scrollTop: 0,
    });
  }, [assets]);

  const layout = useMemo(() => {
    const usableWidth = Math.max(viewport.width, cardMinWidth);
    const columns = Math.max(1, Math.floor((usableWidth + gridGap) / (cardMinWidth + gridGap)));
    const columnWidth = Math.max(
      cardMinWidth,
      Math.floor((usableWidth - gridGap * (columns - 1)) / columns),
    );
    const rowHeight = cardHeight + gridGap;
    const totalRows = Math.ceil(assets.length / columns);
    const startRow = Math.max(0, Math.floor(viewport.scrollTop / rowHeight) - overscanRows);
    const endRow = Math.min(
      totalRows,
      Math.ceil((viewport.scrollTop + viewport.height) / rowHeight) + overscanRows,
    );
    const startIndex = startRow * columns;
    const endIndex = Math.min(assets.length, endRow * columns);

    return {
      columns,
      columnWidth,
      rowHeight,
      totalHeight: totalRows * rowHeight,
      startIndex,
      visibleAssets: assets.slice(startIndex, endIndex),
    };
  }, [assets, viewport]);

  if (assets.length === 0) {
    return (
      <main className="gallery gallery--empty">
        <FileQuestion size={42} />
        <p>No assets match the current filters.</p>
      </main>
    );
  }

  return (
    <main
      className="gallery gallery--virtual"
      ref={containerRef}
      onScroll={(event) => {
        const element = event.currentTarget;
        setViewport((current) => ({ ...current, scrollTop: element.scrollTop }));
      }}
    >
      <div className="gallery__inner" style={{ height: layout.totalHeight }}>
        {layout.visibleAssets.map((asset, offset) => {
          const index = layout.startIndex + offset;
          const row = Math.floor(index / layout.columns);
          const column = index % layout.columns;
          return (
            <div
              className="gallery__slot"
              key={asset.id}
              style={{
                width: layout.columnWidth,
                transform: `translate(${column * (layout.columnWidth + gridGap)}px, ${
                  row * layout.rowHeight
                }px)`,
              }}
            >
              <button
                className={`asset-card ${selectedId === asset.id ? "asset-card--selected" : ""}`}
                type="button"
                aria-pressed={selectedId === asset.id}
                onClick={() => onSelect(asset)}
              >
                <Preview asset={asset} />
                <span className="asset-card__type">
                  {icons[asset.assetType]}
                  {asset.assetType}
                </span>
                <SfxBadges asset={asset} />
                <strong title={asset.name}>{asset.name}</strong>
                <span className="asset-card__meta">
                  {asset.unityUsageLabel}
                  {asset.unityUsageCount > 0 ? ` · ${asset.unityUsageCount} refs` : ""}
                </span>
                <span className="asset-card__meta">
                  {asset.structureLabel} · {formatBytes(asset.sizeBytes)}
                </span>
              </button>
            </div>
          );
        })}
      </div>
    </main>
  );
}

function SfxBadges({ asset }: { asset: AssetItem }) {
  if (asset.assetType !== "sfx") {
    return null;
  }

  const status = asset.sfxQcStatus ?? "unknown";
  const materialLabel = materialLabelForVariantKey(asset.sfxVariantKey);
  return (
    <span className="asset-card__badges" aria-label="SFX review badges">
      <span className={`qc-badge qc-badge--${status}`}>{asset.sfxQcLabel ?? qcLabel(status)}</span>
      {asset.sfxVariantKey ? (
        <span className="asset-card__variant" title={asset.sfxVariantKey}>
          {materialLabel ?? compactVariant(asset.sfxVariantKey)}
        </span>
      ) : null}
    </span>
  );
}

function compactVariant(variantKey: string): string {
  return variantKey.replace(/^combat\./, "").replace(/\./g, " / ");
}

function Preview({ asset }: { asset: AssetItem }) {
  if (asset.assetType === "image") {
    return <ImagePreview asset={asset} />;
  }

  return (
    <div className={`preview preview--placeholder preview--${asset.assetType}`}>
      {icons[asset.assetType]}
      <span>{asset.extension.toUpperCase()}</span>
    </div>
  );
}

function ImagePreview({ asset }: { asset: AssetItem }) {
  const [thumbnailPath, setThumbnailPath] = useState(asset.thumbnailPath);

  useEffect(() => {
    setThumbnailPath(asset.thumbnailPath);
    if (asset.thumbnailPath) {
      return;
    }

    let disposed = false;
    ensureThumbnail(asset.absolutePath, asset.modifiedMs, asset.sizeBytes)
      .then((path) => {
        if (!disposed && path) {
          setThumbnailPath(path);
        }
      })
      .catch(() => {
        if (!disposed) {
          setThumbnailPath(null);
        }
      });

    return () => {
      disposed = true;
    };
  }, [asset.absolutePath, asset.id, asset.modifiedMs, asset.sizeBytes, asset.thumbnailPath]);

  if (thumbnailPath) {
    const src = convertFileSrc(thumbnailPath);
    return (
      <div className="preview preview--image">
        <img src={src} alt="" loading="lazy" decoding="async" />
      </div>
    );
  }

  return (
    <div className="preview preview--placeholder preview--image-pending">
      {icons.image}
      <span>THUMB</span>
    </div>
  );
}
