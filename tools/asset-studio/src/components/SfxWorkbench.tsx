import { AlertTriangle, CheckCircle2, ClipboardList, Filter, Volume2, XCircle } from "lucide-react";
import { useMemo, useState } from "react";

import { compactPath } from "../lib/format";
import {
  buildSfxCoverage,
  SFX_NEEDED_ITEMS,
  unmatchedSfxAssets,
  type SfxCoverageRow,
  type SfxNeededCategory,
} from "../lib/sfxPlan";
import type { AssetItem, SfxQcStatus } from "../types";
import "./SfxWorkbench.css";

interface Props {
  assets: AssetItem[];
  onReviewAsset: (asset: AssetItem) => void;
}

type SfxFilter = "all" | "missing" | "needs_review" | "material_variants" | SfxNeededCategory;

const filterLabels: Array<{ key: SfxFilter; label: string }> = [
  { key: "all", label: "All Needed" },
  { key: "missing", label: "Missing" },
  { key: "needs_review", label: "Needs Review" },
  { key: "material_variants", label: "Material Variants" },
  { key: "combat_common", label: "Combat Common" },
  { key: "skill", label: "Skills" },
  { key: "status", label: "Status" },
];

export function SfxWorkbench({ assets, onReviewAsset }: Props) {
  const [filter, setFilter] = useState<SfxFilter>("all");
  const coverage = useMemo(() => buildSfxCoverage(assets), [assets]);
  const unmatched = useMemo(() => unmatchedSfxAssets(assets, coverage), [assets, coverage]);
  const sfxAssets = useMemo(() => assets.filter((asset) => asset.assetType === "sfx"), [assets]);
  const visibleRows = useMemo(
    () => coverage.filter((row) => matchesFilter(row, filter)),
    [coverage, filter],
  );
  const summary = useMemo(() => buildSummary(coverage, sfxAssets), [coverage, sfxAssets]);

  return (
    <main className="sfx-workbench">
      <section className="sfx-summary-grid" aria-label="SFX manifest summary">
        <Metric label="Needed" value={SFX_NEEDED_ITEMS.length} icon={<ClipboardList size={17} />} />
        <Metric label="Made" value={summary.madeRows} icon={<CheckCircle2 size={17} />} />
        <Metric label="Missing" value={summary.missingRows} icon={<XCircle size={17} />} />
        <Metric label="Review" value={summary.reviewRows} icon={<AlertTriangle size={17} />} />
        <Metric label="Generated" value={summary.generatedAssets} icon={<Volume2 size={17} />} />
      </section>

      <section className="sfx-panel">
        <div className="sfx-panel__toolbar">
          <div className="sfx-panel__title">
            <Filter size={16} />
            <span>SFX manifest coverage</span>
          </div>
          <div className="sfx-filter-tabs" role="tablist" aria-label="SFX coverage filter">
            {filterLabels.map((item) => (
              <button
                className={filter === item.key ? "selected" : ""}
                key={item.key}
                type="button"
                role="tab"
                aria-selected={filter === item.key}
                onClick={() => setFilter(item.key)}
              >
                {item.label}
                <em>{countForFilter(coverage, item.key)}</em>
              </button>
            ))}
          </div>
        </div>

        <div className="sfx-table" role="table" aria-label="SFX needed and made list">
          <div className="sfx-table__row sfx-table__row--head" role="row">
            <span role="columnheader">Need</span>
            <span role="columnheader">Runtime Event</span>
            <span role="columnheader">Material Variant</span>
            <span role="columnheader">Made</span>
            <span role="columnheader">QC</span>
            <span role="columnheader">Review File</span>
          </div>
          {visibleRows.map((row) => (
            <div className="sfx-table__row" key={row.id} role="row">
              <div className="sfx-need-cell" role="cell">
                <strong>{row.label}</strong>
                <small>{row.description}</small>
                <small>{row.source}</small>
              </div>
              <div className="sfx-hook-cell" role="cell">
                <code title={row.runtimeHookId}>{runtimeLabel(row.runtimeHookId)}</code>
                <small title={row.runtimeHookId}>{row.runtimeHookId}</small>
              </div>
              <div className="sfx-variant-cell" role="cell">
                <strong>{row.materialLabel ?? row.variantLabel ?? row.phase}</strong>
                <small title={row.hookId}>{row.variantKey ?? row.hookId}</small>
              </div>
              <span className={`sfx-made-pill sfx-made-pill--${row.coverageStatus}`} role="cell">
                {row.coverageStatus === "made" ? `${row.madeCount} made` : "missing"}
              </span>
              <span className={`qc-badge qc-badge--${row.qcStatus}`} role="cell">
                {row.qcLabel}
              </span>
              <div className="sfx-review-cell" role="cell">
                {row.primaryAsset ? (
                  <button type="button" onClick={() => onReviewAsset(row.primaryAsset!)}>
                    <span>{row.primaryAsset.name}</span>
                    <small>{compactPath(row.primaryAsset.relativePath)}</small>
                  </button>
                ) : (
                  <span className="muted">No candidate yet</span>
                )}
              </div>
            </div>
          ))}
        </div>
      </section>

      {unmatched.length > 0 ? (
        <section className="sfx-panel sfx-panel--unmatched">
          <div className="sfx-panel__toolbar">
            <div className="sfx-panel__title">
              <AlertTriangle size={16} />
              <span>Generated but not matched to manifest</span>
            </div>
          </div>
          <div className="sfx-unmatched-list">
            {unmatched.slice(0, 24).map((asset) => (
              <button key={asset.id} type="button" onClick={() => onReviewAsset(asset)}>
                <span>{asset.name}</span>
                <em className={`qc-badge qc-badge--${asset.sfxQcStatus ?? "unknown"}`}>
                  {asset.sfxQcLabel ?? "QC Unknown"}
                </em>
              </button>
            ))}
          </div>
        </section>
      ) : null}
    </main>
  );
}

function Metric({
  label,
  value,
  icon,
}: {
  label: string;
  value: number;
  icon: React.ReactNode;
}) {
  return (
    <div className="sfx-metric">
      {icon}
      <span>{label}</span>
      <strong>{value.toLocaleString()}</strong>
    </div>
  );
}

function matchesFilter(row: SfxCoverageRow, filter: SfxFilter): boolean {
  switch (filter) {
    case "all":
      return true;
    case "missing":
      return row.coverageStatus === "missing";
    case "needs_review":
      return row.coverageStatus === "made" && isReviewQc(row.qcStatus);
    case "material_variants":
      return row.variantKey !== null;
    case "combat_common":
    case "skill":
    case "status":
      return row.category === filter;
  }
}

function countForFilter(rows: SfxCoverageRow[], filter: SfxFilter): number {
  return rows.filter((row) => matchesFilter(row, filter)).length;
}

function buildSummary(rows: SfxCoverageRow[], assets: AssetItem[]) {
  const madeRows = rows.filter((row) => row.coverageStatus === "made").length;
  const missingRows = rows.length - madeRows;
  const reviewRows = rows.filter(
    (row) => row.coverageStatus === "made" && isReviewQc(row.qcStatus),
  ).length;
  return { madeRows, missingRows, reviewRows, generatedAssets: assets.length };
}

function isReviewQc(status: SfxQcStatus): boolean {
  return status === "red" || status === "yellow" || status === "unknown";
}

function runtimeLabel(runtimeHookId: string): string {
  return runtimeHookId
    .replace(/^sfx\.combat\./, "")
    .replace(/^sfx\.skill\./, "skill.")
    .replace(/^sfx\.status\./, "status.")
    .replace(/_/g, " ");
}
