import { convertFileSrc } from "@tauri-apps/api/core";
import { FolderOpen, Info, RefreshCw } from "lucide-react";
import { useEffect, useState } from "react";

import { compactPath, formatBytes, formatDate } from "../lib/format";
import { getImageInfo, openAssetExternal } from "../lib/ipc";
import { buildAssetReviewContext } from "../lib/reviewContext";
import { materialLabelForVariantKey } from "../lib/sfxPlan";
import type { AssetItem, ImageInfo } from "../types";

interface Props {
  asset: AssetItem | null;
  sidecar: unknown | null;
  sidecarLoading: boolean;
}

export function Inspector({ asset, sidecar, sidecarLoading }: Props) {
  const [imageInfo, setImageInfo] = useState<ImageInfo | null>(null);
  const [imageInfoLoading, setImageInfoLoading] = useState(false);
  const [openError, setOpenError] = useState<string | null>(null);

  useEffect(() => {
    setOpenError(null);
    setImageInfo(null);
    if (!asset || asset.assetType !== "image") {
      setImageInfoLoading(false);
      return;
    }

    let disposed = false;
    setImageInfoLoading(true);
    getImageInfo(asset.absolutePath)
      .then((info) => {
        if (!disposed) {
          setImageInfo(info);
        }
      })
      .catch(() => {
        if (!disposed) {
          setImageInfo(null);
        }
      })
      .finally(() => {
        if (!disposed) {
          setImageInfoLoading(false);
        }
      });

    return () => {
      disposed = true;
    };
  }, [asset]);

  if (!asset) {
    return (
      <aside className="inspector inspector--empty">
        <Info size={30} />
        <p>Select an asset to inspect metadata and preview details.</p>
      </aside>
    );
  }

  const src = convertFileSrc(asset.absolutePath);
  const canOpenExternal = asset.assetType === "image" || asset.assetType === "video";
  const reviewContext = buildAssetReviewContext(asset, sidecar);
  const sfxMaterialLabel = materialLabelForVariantKey(asset.sfxVariantKey);

  const handleOpenExternal = () => {
    if (!canOpenExternal) {
      return;
    }

    void openAssetExternal(asset.absolutePath).catch((err: unknown) => {
      setOpenError(err instanceof Error ? err.message : String(err));
    });
  };

  return (
    <aside className="inspector">
      <div className="inspector__preview">
        {asset.assetType === "image" ? (
          <button
            className="inspector__preview-button"
            type="button"
            onClick={handleOpenExternal}
            title="Open original in default Windows viewer"
          >
            <img src={src} alt="" />
          </button>
        ) : null}
        {asset.assetType === "video" ? <video src={src} controls /> : null}
        {asset.assetType !== "image" && asset.assetType !== "video" ? (
          <audio src={src} controls />
        ) : null}
      </div>

      <section className="inspector-section">
        <h2>{asset.name}</h2>
        <dl>
          <dt>Type</dt>
          <dd>{asset.assetType}</dd>
          <dt>Source</dt>
          <dd>{asset.source}</dd>
          <dt>Root</dt>
          <dd>{asset.rootLabel}</dd>
          <dt>Unity</dt>
          <dd>{asset.unityUsageLabel}</dd>
          <dt>Size</dt>
          <dd>{formatBytes(asset.sizeBytes)}</dd>
          <dt>Resolution</dt>
          <dd>
            {imageInfoLoading
              ? "Loading"
              : imageInfo
                ? `${imageInfo.width} x ${imageInfo.height}`
                : asset.assetType === "image"
                  ? "-"
                  : "n/a"}
          </dd>
          <dt>Modified</dt>
          <dd>{formatDate(asset.modifiedMs)}</dd>
        </dl>
        {asset.assetType === "image" ? (
          <p className="muted inspector-hint">Click preview to open original file.</p>
        ) : null}
        {openError ? <p className="muted inspector-error">{openError}</p> : null}
      </section>

      <section className="inspector-section">
        <h2>Unity Usage</h2>
        <dl>
          <dt>Status</dt>
          <dd>{asset.unityUsageLabel}</dd>
          <dt>GUID</dt>
          <dd title={asset.unityGuid ?? ""}>{asset.unityGuid ?? "-"}</dd>
          <dt>Refs</dt>
          <dd>{asset.unityUsageCount}</dd>
        </dl>
        {asset.unityUsagePaths.length > 0 ? (
          <ul className="reference-list">
            {asset.unityUsagePaths.map((path) => (
              <li key={path}>{compactPath(path)}</li>
            ))}
          </ul>
        ) : (
          <p className="muted">No Unity serialized reference found.</p>
        )}
      </section>

      {asset.assetType === "sfx" ? (
        <section className="inspector-section">
          <h2>SFX QA</h2>
          <dl>
            <dt>QC</dt>
            <dd>
              <span className={`qc-badge qc-badge--${asset.sfxQcStatus ?? "unknown"}`}>
                {asset.sfxQcLabel ?? "QC Unknown"}
              </span>
            </dd>
            <dt>Hook</dt>
            <dd title={asset.sfxHookId ?? ""}>{asset.sfxHookId ?? "-"}</dd>
            <dt>Runtime</dt>
            <dd title={asset.sfxRuntimeHookId ?? ""}>{asset.sfxRuntimeHookId ?? "-"}</dd>
            <dt>Material</dt>
            <dd title={asset.sfxVariantKey ?? ""}>{sfxMaterialLabel ?? "-"}</dd>
            <dt>Variant</dt>
            <dd title={asset.sfxVariantKey ?? ""}>{asset.sfxVariantKey ?? "-"}</dd>
            <dt>Profile</dt>
            <dd title={asset.sfxProfileKey ?? ""}>{asset.sfxProfileKey ?? "-"}</dd>
          </dl>
        </section>
      ) : null}

      {reviewContext ? (
        <section className="inspector-section">
          <h2>Review Context</h2>
          <dl>
            <dt>Runtime</dt>
            <dd title={reviewContext.runtimeHookId ?? ""}>{reviewContext.runtimeHookId ?? "-"}</dd>
            <dt>Variant</dt>
            <dd title={reviewContext.variantKey ?? ""}>{reviewContext.variantKey ?? "-"}</dd>
            <dt>Profile</dt>
            <dd title={reviewContext.profileKey ?? ""}>{reviewContext.profileKey ?? "-"}</dd>
            <dt>Hook</dt>
            <dd title={reviewContext.hookId}>{reviewContext.hookId}</dd>
            <dt>Category</dt>
            <dd>{reviewContext.category}</dd>
            <dt>Trigger</dt>
            <dd>{reviewContext.trigger}</dd>
            <dt>Source</dt>
            <dd>{reviewContext.source}</dd>
            {reviewContext.generationSummary ? (
              <>
                <dt>Generate</dt>
                <dd>{reviewContext.generationSummary}</dd>
              </>
            ) : null}
          </dl>
          <div className="review-context-copy">
            <h3>Where Used</h3>
            <p>{reviewContext.whereUsed}</p>
            {reviewContext.variantRole ? (
              <>
                <h3>Variant Role</h3>
                <p>{reviewContext.variantRole}</p>
              </>
            ) : null}
            <h3>Review Focus</h3>
            <p>{reviewContext.reviewFocus}</p>
          </div>
        </section>
      ) : null}

      <section className="inspector-section">
        <h2>Path</h2>
        <p className="path-line">
          <FolderOpen size={15} />
          <span>{compactPath(asset.absolutePath)}</span>
        </p>
      </section>

      <section className="inspector-section">
        <h2>Sidecar</h2>
        {sidecarLoading ? (
          <p className="muted">
            <RefreshCw className="spin inline-icon" size={14} />
            Loading sidecar
          </p>
        ) : asset.sidecarPath ? (
          <pre>{JSON.stringify(sidecar, null, 2)}</pre>
        ) : (
          <p className="muted">No JSON sidecar found.</p>
        )}
      </section>
    </aside>
  );
}
