import { Activity, Loader2, Play, RefreshCw } from "lucide-react";

import type { EngineHealth } from "../types";

interface Props {
  engines: EngineHealth[];
  loading: boolean;
  onRefresh: () => void;
  onStart: (engineId: string) => void;
}

export function EngineStrip({ engines, loading, onRefresh, onStart }: Props) {
  return (
    <section className="engine-strip" aria-label="Engine status">
      <div className="engine-strip__title">
        <Activity size={18} />
        <span>Engines</span>
      </div>
      <div className="engine-strip__list">
        {engines.map((engine) => (
          <article className="engine-chip" key={engine.id} title={engine.message}>
            <span className={`status-dot status-dot--${engine.status}`} />
            <div className="engine-chip__copy">
              <strong>{engine.name}</strong>
              <span>
                {engine.status}
                {engine.latencyMs != null ? ` · ${engine.latencyMs}ms` : ""}
              </span>
            </div>
            {engine.canStart && engine.status !== "online" ? (
              <button
                className="icon-button"
                type="button"
                onClick={() => onStart(engine.id)}
                title={`Start ${engine.name}`}
                aria-label={`Start ${engine.name}`}
              >
                <Play size={16} />
              </button>
            ) : null}
          </article>
        ))}
      </div>
      <button
        className="icon-button icon-button--framed"
        type="button"
        onClick={onRefresh}
        title="Refresh engine status"
        aria-label="Refresh engine status"
      >
        {loading ? <Loader2 className="spin" size={17} /> : <RefreshCw size={17} />}
      </button>
    </section>
  );
}
